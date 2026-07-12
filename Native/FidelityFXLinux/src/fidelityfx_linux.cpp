#include "fidelityfx_linux.h"

#include "IUnityGraphics.h"
#include "IUnityGraphicsVulkan.h"
#include "IUnityInterface.h"
#include "ffx_fsr2.h"
#include "ffx_fsr2_vk.h"
#include "vulkan_smoke_comp_spv.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>

namespace
{
constexpr uint32_t kFramesInFlight = 8;
constexpr uint32_t kFsr2ResourceCount = 6;

struct SmokeFrameResources
{
	VkImage source_image = VK_NULL_HANDLE;
	VkImage output_image = VK_NULL_HANDLE;
	VkImageView source_image_view = VK_NULL_HANDLE;
	VkImageView output_image_view = VK_NULL_HANDLE;
	VkDescriptorSet descriptor_set = VK_NULL_HANDLE;
	uint64_t frame_number = 0;
	uint32_t generation = 0;
	bool in_flight = false;
};

struct Fsr2FrameResources
{
	std::array<VkImage, kFsr2ResourceCount> images{};
	std::array<VkImageView, kFsr2ResourceCount> views{};
	uint64_t frame_number = 0;
	uint32_t generation = 0;
	bool in_flight = false;
};

char g_last_error[256] = "Sem erro";
IUnityInterfaces* g_unity_interfaces = nullptr;
IUnityGraphics* g_unity_graphics = nullptr;
IUnityGraphicsVulkan* g_unity_vulkan = nullptr;
VkDevice g_device = VK_NULL_HANDLE;
VkDescriptorSetLayout g_descriptor_set_layout = VK_NULL_HANDLE;
VkDescriptorPool g_descriptor_pool = VK_NULL_HANDLE;
VkPipelineLayout g_pipeline_layout = VK_NULL_HANDLE;
VkPipeline g_pipeline = VK_NULL_HANDLE;
int32_t g_smoke_event_id = 0x55F200;
std::atomic<int32_t> g_smoke_state{U3FFX_SMOKE_STATUS_PENDING};
std::atomic<bool> g_smoke_validated{false};
std::atomic<bool> g_accept_dispatches{false};
std::array<SmokeFrameResources, kFramesInFlight> g_frames;
std::array<Fsr2FrameResources, kFramesInFlight> g_fsr2_frames;
FfxFsr2Context g_fsr2_context{};
std::vector<uint8_t> g_fsr2_scratch;
U3FfxFsr2CreateDescription g_fsr2_description{};
std::atomic<int32_t> g_fsr2_status{0};
std::atomic<bool> g_fsr2_reset_history{true};
bool g_fsr2_context_created = false;

void destroy_fsr2_context(bool wait_for_device);

void copy_message(char* destination, const char* source)
{
	if (destination == nullptr)
		return;
	if (source == nullptr)
	{
		destination[0] = '\0';
		return;
	}
	size_t length = std::min<size_t>(std::strlen(source), 255);
	std::memcpy(destination, source, length);
	destination[length] = '\0';
}

void set_error(const char* message)
{
	copy_message(g_last_error, message);
	g_smoke_state.store(U3FFX_SMOKE_STATUS_ERROR, std::memory_order_release);
}

void release_frame_image_views(SmokeFrameResources& frame)
{
	if (g_device == VK_NULL_HANDLE)
		return;
	if (frame.source_image_view != VK_NULL_HANDLE)
		vkDestroyImageView(g_device, frame.source_image_view, nullptr);
	if (frame.output_image_view != VK_NULL_HANDLE)
		vkDestroyImageView(g_device, frame.output_image_view, nullptr);
	frame.source_image = VK_NULL_HANDLE;
	frame.output_image = VK_NULL_HANDLE;
	frame.source_image_view = VK_NULL_HANDLE;
	frame.output_image_view = VK_NULL_HANDLE;
}

void collect_completed_frames(uint64_t safe_frame_number)
{
	for (SmokeFrameResources& frame : g_frames)
	{
		if (frame.in_flight && frame.frame_number <= safe_frame_number)
			frame.in_flight = false;
	}
}

void release_fsr2_frame_views(Fsr2FrameResources& frame)
{
	if (g_device != VK_NULL_HANDLE)
	{
		for (VkImageView view : frame.views)
		{
			if (view != VK_NULL_HANDLE)
				vkDestroyImageView(g_device, view, nullptr);
		}
	}
	frame.images.fill(VK_NULL_HANDLE);
	frame.views.fill(VK_NULL_HANDLE);
}

void collect_completed_fsr2_frames(uint64_t safe_frame_number)
{
	for (Fsr2FrameResources& frame : g_fsr2_frames)
	{
		if (frame.in_flight && frame.frame_number <= safe_frame_number)
			frame.in_flight = false;
	}
}

void destroy_vulkan_resources()
{
	if (g_device == VK_NULL_HANDLE)
		return;
	vkDeviceWaitIdle(g_device);
	destroy_fsr2_context(false);
	for (SmokeFrameResources& frame : g_frames)
	{
		release_frame_image_views(frame);
		frame = {};
	}
	if (g_pipeline != VK_NULL_HANDLE)
		vkDestroyPipeline(g_device, g_pipeline, nullptr);
	if (g_pipeline_layout != VK_NULL_HANDLE)
		vkDestroyPipelineLayout(g_device, g_pipeline_layout, nullptr);
	if (g_descriptor_pool != VK_NULL_HANDLE)
		vkDestroyDescriptorPool(g_device, g_descriptor_pool, nullptr);
	if (g_descriptor_set_layout != VK_NULL_HANDLE)
		vkDestroyDescriptorSetLayout(g_device, g_descriptor_set_layout, nullptr);
	g_pipeline = VK_NULL_HANDLE;
	g_pipeline_layout = VK_NULL_HANDLE;
	g_descriptor_pool = VK_NULL_HANDLE;
	g_descriptor_set_layout = VK_NULL_HANDLE;
	g_device = VK_NULL_HANDLE;
}

bool validate_fsr2_description(const U3FfxFsr2CreateDescription* description)
{
	return description != nullptr && description->struct_size >= sizeof(U3FfxFsr2CreateDescription)
		&& description->abi_version == static_cast<uint32_t>(U3FFX_ABI_VERSION)
		&& description->max_render_width > 0 && description->max_render_height > 0
		&& description->display_width > 0 && description->display_height > 0
		&& description->max_render_width <= description->display_width
		&& description->max_render_height <= description->display_height;
}

void UNITY_INTERFACE_API fsr2_message(FfxFsr2MsgType type, const wchar_t* message)
{
	(void)type;
	if (message == nullptr)
		return;
	char converted[256]{};
	std::wcstombs(converted, message, sizeof(converted) - 1);
	copy_message(g_last_error, converted);
}

bool create_fsr2_context(const U3FfxFsr2CreateDescription* description)
{
	if (!validate_fsr2_description(description) || g_unity_vulkan == nullptr || g_device == VK_NULL_HANDLE
		|| !g_smoke_validated.load(std::memory_order_acquire))
	{
		set_error("FSR2 create recusado: bridge, dimensões ou ABI inválidos");
		g_fsr2_status.store(-1, std::memory_order_release);
		return false;
	}
	if (g_fsr2_context_created)
		destroy_fsr2_context(true);
	UnityVulkanInstance instance = g_unity_vulkan->Instance();
	size_t scratch_size = ffxFsr2GetScratchMemorySizeVK(instance.physicalDevice);
	if (scratch_size == 0)
	{
		set_error("ffxFsr2GetScratchMemorySizeVK retornou zero");
		g_fsr2_status.store(-1, std::memory_order_release);
		return false;
	}
	g_fsr2_scratch.resize(scratch_size);
	FfxFsr2ContextDescription context_description{};
	if (ffxFsr2GetInterfaceVK(&context_description.callbacks, g_fsr2_scratch.data(), g_fsr2_scratch.size(), instance.physicalDevice, vkGetDeviceProcAddr) != FFX_OK)
	{
		set_error("ffxFsr2GetInterfaceVK falhou");
		g_fsr2_status.store(-1, std::memory_order_release);
		return false;
	}
	context_description.device = ffxGetDeviceVK(g_device);
	context_description.maxRenderSize = {description->max_render_width, description->max_render_height};
	context_description.displaySize = {description->display_width, description->display_height};
	context_description.flags = description->flags;
	context_description.fpMessage = &fsr2_message;
	if (ffxFsr2ContextCreate(&g_fsr2_context, &context_description) != FFX_OK)
	{
		set_error("ffxFsr2ContextCreate falhou");
		g_fsr2_status.store(-1, std::memory_order_release);
		g_fsr2_scratch.clear();
		return false;
	}
	g_fsr2_description = *description;
	g_fsr2_context_created = true;
	g_fsr2_reset_history.store(true, std::memory_order_release);
	g_fsr2_status.store(1, std::memory_order_release);
	copy_message(g_last_error, "Contexto AMD FidelityFX FSR2 criado; dispatch aguardando inputs validados");
	return true;
}

void destroy_fsr2_context(bool wait_for_device)
{
	if (wait_for_device && g_device != VK_NULL_HANDLE)
		vkDeviceWaitIdle(g_device);
	if (g_fsr2_context_created)
		ffxFsr2ContextDestroy(&g_fsr2_context);
	for (Fsr2FrameResources& frame : g_fsr2_frames)
	{
		release_fsr2_frame_views(frame);
		frame = {};
	}
	g_fsr2_context = {};
	g_fsr2_scratch.clear();
	g_fsr2_description = {};
	g_fsr2_context_created = false;
	g_fsr2_reset_history.store(true, std::memory_order_release);
	g_fsr2_status.store(0, std::memory_order_release);
}

bool create_vulkan_resources()
{
	if (g_pipeline != VK_NULL_HANDLE)
		return true;
	if (g_unity_vulkan == nullptr)
	{
		set_error("IUnityGraphicsVulkan indisponível");
		return false;
	}
	UnityVulkanInstance instance = g_unity_vulkan->Instance();
	if (instance.device == VK_NULL_HANDLE)
	{
		set_error("VkDevice da Unity indisponível");
		return false;
	}
	g_device = instance.device;

	VkDescriptorSetLayoutBinding bindings[2]{};
	for (uint32_t i = 0; i < 2; ++i)
	{
		bindings[i].binding = i;
		bindings[i].descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
		bindings[i].descriptorCount = 1;
		bindings[i].stageFlags = VK_SHADER_STAGE_COMPUTE_BIT;
	}
	VkDescriptorSetLayoutCreateInfo descriptor_layout_info{};
	descriptor_layout_info.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
	descriptor_layout_info.pNext = nullptr;
	descriptor_layout_info.bindingCount = 2;
	descriptor_layout_info.pBindings = bindings;
	if (vkCreateDescriptorSetLayout(g_device, &descriptor_layout_info, nullptr, &g_descriptor_set_layout) != VK_SUCCESS)
	{
		set_error("vkCreateDescriptorSetLayout falhou");
		return false;
	}

	VkDescriptorPoolSize pool_size{};
	pool_size.type = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
	pool_size.descriptorCount = kFramesInFlight * 2;
	VkDescriptorPoolCreateInfo pool_info{};
	pool_info.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
	pool_info.pNext = nullptr;
	pool_info.flags = 0;
	pool_info.maxSets = kFramesInFlight;
	pool_info.poolSizeCount = 1;
	pool_info.pPoolSizes = &pool_size;
	if (vkCreateDescriptorPool(g_device, &pool_info, nullptr, &g_descriptor_pool) != VK_SUCCESS)
	{
		set_error("vkCreateDescriptorPool falhou");
		return false;
	}
	std::array<VkDescriptorSetLayout, kFramesInFlight> set_layouts{};
	set_layouts.fill(g_descriptor_set_layout);
	std::array<VkDescriptorSet, kFramesInFlight> descriptor_sets{};
	VkDescriptorSetAllocateInfo allocate_info{};
	allocate_info.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
	allocate_info.pNext = nullptr;
	allocate_info.descriptorPool = g_descriptor_pool;
	allocate_info.descriptorSetCount = kFramesInFlight;
	allocate_info.pSetLayouts = set_layouts.data();
	if (vkAllocateDescriptorSets(g_device, &allocate_info, descriptor_sets.data()) != VK_SUCCESS)
	{
		set_error("vkAllocateDescriptorSets persistente falhou");
		return false;
	}
	for (uint32_t i = 0; i < kFramesInFlight; ++i)
		g_frames[i].descriptor_set = descriptor_sets[i];

	VkPipelineLayoutCreateInfo pipeline_layout_info{};
	pipeline_layout_info.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	pipeline_layout_info.pNext = nullptr;
	pipeline_layout_info.setLayoutCount = 1;
	pipeline_layout_info.pSetLayouts = &g_descriptor_set_layout;
	if (vkCreatePipelineLayout(g_device, &pipeline_layout_info, nullptr, &g_pipeline_layout) != VK_SUCCESS)
	{
		set_error("vkCreatePipelineLayout falhou");
		return false;
	}

	VkShaderModuleCreateInfo shader_info{};
	shader_info.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
	shader_info.pNext = nullptr;
	shader_info.codeSize = sizeof(u3ffx_vulkan_smoke_comp_spv);
	shader_info.pCode = u3ffx_vulkan_smoke_comp_spv;
	VkShaderModule shader_module = VK_NULL_HANDLE;
	if (vkCreateShaderModule(g_device, &shader_info, nullptr, &shader_module) != VK_SUCCESS)
	{
		set_error("vkCreateShaderModule falhou");
		return false;
	}

	VkComputePipelineCreateInfo pipeline_info{};
	pipeline_info.sType = VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO;
	pipeline_info.pNext = nullptr;
	pipeline_info.stage.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
	pipeline_info.stage.stage = VK_SHADER_STAGE_COMPUTE_BIT;
	pipeline_info.stage.module = shader_module;
	pipeline_info.stage.pName = "main";
	pipeline_info.layout = g_pipeline_layout;
	VkResult pipeline_result = vkCreateComputePipelines(g_device, instance.pipelineCache, 1, &pipeline_info, nullptr, &g_pipeline);
	vkDestroyShaderModule(g_device, shader_module, nullptr);
	if (pipeline_result != VK_SUCCESS)
	{
		set_error("vkCreateComputePipelines falhou");
		return false;
	}

	copy_message(g_last_error, "Bridge Vulkan inicializado; smoke aguardando validação de imagem");
	g_smoke_state.store(U3FFX_SMOKE_STATUS_PENDING, std::memory_order_release);
	g_accept_dispatches.store(true, std::memory_order_release);
	return true;
}

void UNITY_INTERFACE_API on_graphics_device_event(UnityGfxDeviceEventType event_type)
{
	if (event_type == kUnityGfxDeviceEventInitialize)
	{
		if (g_unity_graphics == nullptr || g_unity_graphics->GetRenderer() != kUnityGfxRendererVulkan)
		{
			set_error("Renderer ativo não é Vulkan");
			return;
		}
		g_unity_vulkan = g_unity_interfaces != nullptr ? g_unity_interfaces->Get<IUnityGraphicsVulkan>() : nullptr;
		if (g_unity_vulkan != nullptr)
		{
			UnityVulkanPluginEventConfig config{};
			config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
			config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
			config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission | kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
			g_unity_vulkan->ConfigureEvent(g_smoke_event_id, &config);
			create_vulkan_resources();
		}
	}
	else if (event_type == kUnityGfxDeviceEventShutdown || event_type == kUnityGfxDeviceEventBeforeReset)
	{
		g_accept_dispatches.store(false, std::memory_order_release);
		destroy_vulkan_resources();
		g_unity_vulkan = nullptr;
		g_smoke_validated.store(false, std::memory_order_release);
	}
	else if (event_type == kUnityGfxDeviceEventAfterReset)
	{
		on_graphics_device_event(kUnityGfxDeviceEventInitialize);
	}
}

void dispatch_smoke(U3FfxVulkanSmokeParameters* parameters)
{
	if (parameters == nullptr)
	{
		set_error("Parâmetros do smoke Vulkan ausentes");
		return;
	}
	parameters->status = U3FFX_SMOKE_STATUS_ERROR;
	if (!g_accept_dispatches.load(std::memory_order_acquire) || g_unity_vulkan == nullptr || g_pipeline == VK_NULL_HANDLE)
	{
		set_error("Bridge Vulkan não aceita novos dispatches");
		return;
	}
	if (parameters->struct_size < sizeof(U3FfxVulkanSmokeParameters) || parameters->abi_version != static_cast<uint32_t>(U3FFX_ABI_VERSION)
		|| parameters->command != U3FFX_COMMAND_VULKAN_SMOKE || parameters->frame_slot >= kFramesInFlight || parameters->generation == 0)
	{
		set_error("ABI ou comando inválido nos parâmetros do smoke Vulkan");
		return;
	}
	if (parameters->source_texture == nullptr || parameters->output_texture == nullptr || parameters->width == 0 || parameters->height == 0)
	{
		set_error("Texturas ou dimensões inválidas no smoke Vulkan");
		return;
	}

	UnityVulkanRecordingState before_access{};
	if (!g_unity_vulkan->CommandRecordingState(&before_access, kUnityVulkanGraphicsQueueAccess_DontCare))
	{
		set_error("CommandRecordingState inicial falhou");
		return;
	}
	collect_completed_frames(before_access.safeFrameNumber);
	SmokeFrameResources& frame = g_frames[parameters->frame_slot];
	if (frame.in_flight)
	{
		set_error("Ring Vulkan cheio; frame ainda em voo");
		return;
	}
	if (parameters->generation <= frame.generation)
	{
		set_error("Generation obsoleta no slot Vulkan");
		return;
	}

	UnityVulkanImage source_image{};
	if (!g_unity_vulkan->AccessTexture(parameters->source_texture, UnityVulkanWholeImage, VK_IMAGE_LAYOUT_GENERAL,
		VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, VK_ACCESS_SHADER_READ_BIT, kUnityVulkanResourceAccess_PipelineBarrier, &source_image))
	{
		set_error("AccessTexture falhou para source do smoke");
		return;
	}
	UnityVulkanImage output_image{};
	if (!g_unity_vulkan->AccessTexture(parameters->output_texture, UnityVulkanWholeImage, VK_IMAGE_LAYOUT_GENERAL,
		VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, VK_ACCESS_SHADER_WRITE_BIT, kUnityVulkanResourceAccess_PipelineBarrier, &output_image))
	{
		set_error("AccessTexture falhou para output do smoke");
		return;
	}
	if ((source_image.usage & VK_IMAGE_USAGE_STORAGE_BIT) == 0 || (output_image.usage & VK_IMAGE_USAGE_STORAGE_BIT) == 0
		|| (source_image.aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0 || (output_image.aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0)
	{
		set_error("RenderTexture não possui VK_IMAGE_USAGE_STORAGE_BIT color");
		return;
	}
	if (source_image.format != VK_FORMAT_R8G8B8A8_UNORM || output_image.format != VK_FORMAT_R8G8B8A8_UNORM)
	{
		set_error("Formato source/output smoke não é VK_FORMAT_R8G8B8A8_UNORM");
		return;
	}
	if (source_image.extent.width != output_image.extent.width || source_image.extent.height != output_image.extent.height
		|| output_image.extent.width != parameters->width || output_image.extent.height != parameters->height)
	{
		set_error("Dimensões reais source/output divergem dos parâmetros do smoke");
		return;
	}

	UnityVulkanRecordingState recording{};
	if (!g_unity_vulkan->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare) || recording.commandBuffer == VK_NULL_HANDLE)
	{
		set_error("Command buffer Vulkan indisponível após AccessTexture");
		return;
	}

	bool resources_changed = frame.source_image != source_image.image || frame.output_image != output_image.image;
	if (resources_changed)
	{
		release_frame_image_views(frame);
		VkImageViewCreateInfo view_info{};
		view_info.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
		view_info.pNext = nullptr;
		view_info.image = source_image.image;
		view_info.viewType = VK_IMAGE_VIEW_TYPE_2D;
		view_info.format = source_image.format;
		view_info.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
		view_info.subresourceRange.baseMipLevel = 0;
		view_info.subresourceRange.levelCount = 1;
		view_info.subresourceRange.baseArrayLayer = 0;
		view_info.subresourceRange.layerCount = 1;
		if (vkCreateImageView(g_device, &view_info, nullptr, &frame.source_image_view) != VK_SUCCESS)
		{
			set_error("vkCreateImageView falhou para source");
			return;
		}
		view_info.image = output_image.image;
		view_info.format = output_image.format;
		if (vkCreateImageView(g_device, &view_info, nullptr, &frame.output_image_view) != VK_SUCCESS)
		{
			release_frame_image_views(frame);
			set_error("vkCreateImageView falhou para output");
			return;
		}
		frame.source_image = source_image.image;
		frame.output_image = output_image.image;
	}

	VkDescriptorImageInfo descriptor_images[2]{};
	descriptor_images[0].imageView = frame.source_image_view;
	descriptor_images[0].imageLayout = VK_IMAGE_LAYOUT_GENERAL;
	descriptor_images[1].imageView = frame.output_image_view;
	descriptor_images[1].imageLayout = VK_IMAGE_LAYOUT_GENERAL;
	VkWriteDescriptorSet writes[2]{};
	for (uint32_t i = 0; i < 2; ++i)
	{
		writes[i].sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
		writes[i].dstSet = frame.descriptor_set;
		writes[i].dstBinding = i;
		writes[i].descriptorCount = 1;
		writes[i].descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
		writes[i].pImageInfo = &descriptor_images[i];
	}
	if (resources_changed)
		vkUpdateDescriptorSets(g_device, 2, writes, 0, nullptr);

	vkCmdBindPipeline(recording.commandBuffer, VK_PIPELINE_BIND_POINT_COMPUTE, g_pipeline);
	vkCmdBindDescriptorSets(recording.commandBuffer, VK_PIPELINE_BIND_POINT_COMPUTE, g_pipeline_layout, 0, 1, &frame.descriptor_set, 0, nullptr);
	uint32_t width = output_image.extent.width;
	uint32_t height = output_image.extent.height;
	vkCmdDispatch(recording.commandBuffer, (width + 7u) / 8u, (height + 7u) / 8u, 1);
	frame.frame_number = recording.currentFrameNumber;
	frame.generation = parameters->generation;
	frame.in_flight = true;
	parameters->width = width;
	parameters->height = height;
	parameters->status = U3FFX_SMOKE_STATUS_RECORDED;
	g_smoke_state.store(U3FFX_SMOKE_STATUS_RECORDED, std::memory_order_release);
	copy_message(g_last_error, "Compute smoke Vulkan gravado no command buffer da Unity");
}

bool create_fsr2_image_view(const UnityVulkanImage& image, VkImageView* view)
{
	VkImageViewCreateInfo view_info{};
	view_info.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
	view_info.pNext = nullptr;
	view_info.image = image.image;
	view_info.viewType = VK_IMAGE_VIEW_TYPE_2D;
	view_info.format = image.format;
	view_info.subresourceRange.aspectMask = image.aspect;
	view_info.subresourceRange.baseMipLevel = 0;
	view_info.subresourceRange.levelCount = 1;
	view_info.subresourceRange.baseArrayLayer = 0;
	view_info.subresourceRange.layerCount = 1;
	return vkCreateImageView(g_device, &view_info, nullptr, view) == VK_SUCCESS;
}

bool access_fsr2_texture(void* texture, bool output, UnityVulkanImage* image)
{
	if (texture == nullptr || image == nullptr)
		return false;
	VkAccessFlags access = output ? VK_ACCESS_SHADER_WRITE_BIT : VK_ACCESS_SHADER_READ_BIT;
	return g_unity_vulkan->AccessTexture(texture, UnityVulkanWholeImage, VK_IMAGE_LAYOUT_GENERAL,
		VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, access, kUnityVulkanResourceAccess_PipelineBarrier, image);
}

int32_t dispatch_fsr2(U3FfxFsr2DispatchParameters* parameters)
{
	if (parameters == nullptr)
		return 0;
	parameters->status = -1;
	if (!g_fsr2_context_created || parameters->struct_size < sizeof(U3FfxFsr2DispatchParameters)
		|| parameters->abi_version != static_cast<uint32_t>(U3FFX_ABI_VERSION)
		|| parameters->command != U3FFX_COMMAND_FSR2_DISPATCH || parameters->frame_slot >= kFramesInFlight
		|| parameters->generation == 0 || parameters->color_texture == nullptr || parameters->depth_texture == nullptr
		|| parameters->motion_vectors_texture == nullptr || parameters->output_texture == nullptr
		|| parameters->render_width == 0 || parameters->render_height == 0
		|| parameters->output_width == 0 || parameters->output_height == 0
		|| parameters->render_width > g_fsr2_description.max_render_width
		|| parameters->render_height > g_fsr2_description.max_render_height
		|| parameters->output_width != g_fsr2_description.display_width
		|| parameters->output_height != g_fsr2_description.display_height
		|| parameters->delta_time_ms <= 0.0f || parameters->vertical_fov_radians <= 0.0f || parameters->pre_exposure <= 0.0f)
	{
		set_error("Parâmetros FSR2 inválidos ou contexto ausente");
		g_fsr2_status.store(-1, std::memory_order_release);
		return 0;
	}

	UnityVulkanRecordingState before_access{};
	if (!g_unity_vulkan->CommandRecordingState(&before_access, kUnityVulkanGraphicsQueueAccess_DontCare))
	{
		set_error("CommandRecordingState FSR2 inicial falhou");
		return 0;
	}
	collect_completed_fsr2_frames(before_access.safeFrameNumber);
	Fsr2FrameResources& frame = g_fsr2_frames[parameters->frame_slot];
	if (frame.in_flight || parameters->generation <= frame.generation)
	{
		set_error("Slot FSR2 em voo ou generation obsoleta");
		return 0;
	}

	std::array<void*, kFsr2ResourceCount> texture_handles{
		parameters->color_texture,
		parameters->depth_texture,
		parameters->motion_vectors_texture,
		parameters->output_texture,
		parameters->reactive_texture,
		parameters->composition_texture,
	};
	std::array<UnityVulkanImage, kFsr2ResourceCount> images{};
	for (uint32_t i = 0; i < kFsr2ResourceCount; ++i)
	{
		if (texture_handles[i] == nullptr)
			continue;
		if (!access_fsr2_texture(texture_handles[i], i == 3, &images[i]))
		{
			set_error("AccessTexture falhou para input/output FSR2");
			return 0;
		}
		if (images[i].samples != VK_SAMPLE_COUNT_1_BIT || images[i].extent.depth != 1)
		{
			set_error("FSR2 requer imagens 2D sem MSAA");
			return 0;
		}
	}
	if (images[0].extent.width != parameters->render_width || images[0].extent.height != parameters->render_height
		|| images[1].extent.width != parameters->render_width || images[1].extent.height != parameters->render_height
		|| images[2].extent.width != parameters->render_width || images[2].extent.height != parameters->render_height
		|| images[3].extent.width != parameters->output_width || images[3].extent.height != parameters->output_height)
	{
		set_error("Dimensões reais das texturas FSR2 divergem do dispatch");
		return 0;
	}
	if ((images[0].aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0 || (images[1].aspect & VK_IMAGE_ASPECT_DEPTH_BIT) == 0
		|| (images[2].aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0 || (images[3].aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0
		|| (images[0].usage & VK_IMAGE_USAGE_SAMPLED_BIT) == 0 || (images[1].usage & VK_IMAGE_USAGE_SAMPLED_BIT) == 0
		|| (images[2].usage & VK_IMAGE_USAGE_SAMPLED_BIT) == 0 || (images[3].usage & VK_IMAGE_USAGE_STORAGE_BIT) == 0)
	{
		set_error("Aspect ou usage Vulkan incompatível com FSR2");
		return 0;
	}
	for (uint32_t i = 4; i < kFsr2ResourceCount; ++i)
	{
		if (texture_handles[i] != nullptr && ((images[i].aspect & VK_IMAGE_ASPECT_COLOR_BIT) == 0
			|| (images[i].usage & VK_IMAGE_USAGE_SAMPLED_BIT) == 0))
		{
			set_error("Mask FSR2 não possui aspect color/usage sampled");
			return 0;
		}
	}

	bool resources_changed = false;
	for (uint32_t i = 0; i < kFsr2ResourceCount; ++i)
		resources_changed |= frame.images[i] != images[i].image;
	if (resources_changed)
	{
		release_fsr2_frame_views(frame);
		for (uint32_t i = 0; i < kFsr2ResourceCount; ++i)
		{
			if (texture_handles[i] == nullptr)
				continue;
			if (!create_fsr2_image_view(images[i], &frame.views[i]))
			{
				release_fsr2_frame_views(frame);
				set_error("vkCreateImageView falhou para recurso FSR2");
				return 0;
			}
			frame.images[i] = images[i].image;
		}
	}

	UnityVulkanRecordingState recording{};
	if (!g_unity_vulkan->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare) || recording.commandBuffer == VK_NULL_HANDLE)
	{
		set_error("Command buffer Vulkan indisponível para FSR2");
		return 0;
	}
	FfxFsr2DispatchDescription dispatch{};
	dispatch.commandList = ffxGetCommandListVK(recording.commandBuffer);
	dispatch.color = ffxGetTextureResourceVK(&g_fsr2_context, images[0].image, frame.views[0], parameters->render_width, parameters->render_height, images[0].format, L"U3 FSR2 Color", FFX_RESOURCE_STATE_COMPUTE_READ);
	dispatch.depth = ffxGetTextureResourceVK(&g_fsr2_context, images[1].image, frame.views[1], parameters->render_width, parameters->render_height, images[1].format, L"U3 FSR2 Depth", FFX_RESOURCE_STATE_COMPUTE_READ);
	// The AMD Vulkan helper maps color VkFormats only. A sampled D32 image is the
	// canonical float depth input, so describe its sampled value type explicitly.
	if (images[1].format == VK_FORMAT_D32_SFLOAT)
		dispatch.depth.description.format = FFX_SURFACE_FORMAT_R32_FLOAT;
	dispatch.motionVectors = ffxGetTextureResourceVK(&g_fsr2_context, images[2].image, frame.views[2], parameters->render_width, parameters->render_height, images[2].format, L"U3 FSR2 Motion", FFX_RESOURCE_STATE_COMPUTE_READ);
	dispatch.output = ffxGetTextureResourceVK(&g_fsr2_context, images[3].image, frame.views[3], parameters->output_width, parameters->output_height, images[3].format, L"U3 FSR2 Output", FFX_RESOURCE_STATE_UNORDERED_ACCESS);
	if (texture_handles[4] != nullptr)
		dispatch.reactive = ffxGetTextureResourceVK(&g_fsr2_context, images[4].image, frame.views[4], parameters->render_width, parameters->render_height, images[4].format, L"U3 FSR2 Reactive", FFX_RESOURCE_STATE_COMPUTE_READ);
	if (texture_handles[5] != nullptr)
		dispatch.transparencyAndComposition = ffxGetTextureResourceVK(&g_fsr2_context, images[5].image, frame.views[5], parameters->render_width, parameters->render_height, images[5].format, L"U3 FSR2 Composition", FFX_RESOURCE_STATE_COMPUTE_READ);
	std::array<FfxResource*, kFsr2ResourceCount> ffx_resources{
		&dispatch.color,
		&dispatch.depth,
		&dispatch.motionVectors,
		&dispatch.output,
		&dispatch.reactive,
		&dispatch.transparencyAndComposition,
	};
	for (uint32_t i = 0; i < kFsr2ResourceCount; ++i)
	{
		if (texture_handles[i] != nullptr && ffx_resources[i]->description.format == FFX_SURFACE_FORMAT_UNKNOWN)
		{
			char message[256]{};
			std::snprintf(message, sizeof(message), "Formato Vulkan não suportado pelo backend FSR2: recurso=%u VkFormat=%d", i, static_cast<int>(images[i].format));
			set_error(message);
			return 0;
		}
	}
	dispatch.jitterOffset = {parameters->jitter_x, parameters->jitter_y};
	dispatch.motionVectorScale = {parameters->motion_vector_scale_x, parameters->motion_vector_scale_y};
	dispatch.renderSize = {parameters->render_width, parameters->render_height};
	dispatch.enableSharpening = false;
	dispatch.sharpness = 0.0f;
	dispatch.frameTimeDelta = parameters->delta_time_ms;
	dispatch.preExposure = parameters->pre_exposure;
	dispatch.reset = parameters->reset_history != 0 || g_fsr2_reset_history.exchange(false, std::memory_order_acq_rel);
	dispatch.cameraNear = parameters->camera_near;
	dispatch.cameraFar = parameters->camera_far;
	dispatch.cameraFovAngleVertical = parameters->vertical_fov_radians;
	dispatch.viewSpaceToMetersFactor = 1.0f;
	FfxErrorCode result = ffxFsr2ContextDispatch(&g_fsr2_context, &dispatch);
	if (result != FFX_OK)
	{
		set_error("ffxFsr2ContextDispatch falhou");
		g_fsr2_status.store(-1, std::memory_order_release);
		return 0;
	}
	frame.frame_number = recording.currentFrameNumber;
	frame.generation = parameters->generation;
	frame.in_flight = true;
	parameters->status = 1;
	g_fsr2_status.store(2, std::memory_order_release);
	copy_message(g_last_error, "Dispatch AMD FidelityFX FSR2 gravado no command buffer da Unity");
	return 1;
}

void UNITY_INTERFACE_API render_event(int32_t event_id)
{
	if (event_id == g_smoke_event_id)
		set_error("Smoke Vulkan requer IssuePluginEventAndData");
}

void UNITY_INTERFACE_API render_event_and_data(int32_t event_id, void* data)
{
	if (event_id != g_smoke_event_id || data == nullptr)
		return;
	const uint32_t* header = static_cast<const uint32_t*>(data);
	if (header[2] == U3FFX_COMMAND_VULKAN_SMOKE)
		dispatch_smoke(static_cast<U3FfxVulkanSmokeParameters*>(data));
	else if (header[2] == U3FFX_COMMAND_FSR2_DISPATCH)
		dispatch_fsr2(static_cast<U3FfxFsr2DispatchParameters*>(data));
	else
		set_error("Comando de render desconhecido");
}
}

extern "C"
{
U3FFX_API int32_t u3ffx_get_abi_version()
{
	return U3FFX_ABI_VERSION;
}

U3FFX_API int32_t u3ffx_get_capabilities(U3FfxCapabilities* capabilities)
{
	if (capabilities == nullptr)
	{
		set_error("Ponteiro de capacidades inválido");
		return 0;
	}
	bool validated = g_smoke_validated.load(std::memory_order_acquire) && g_pipeline != VK_NULL_HANDLE;
	capabilities->abi_version = U3FFX_ABI_VERSION;
	capabilities->backend_kind = validated ? U3FFX_BACKEND_VULKAN_COMPUTE_FUNCTIONAL : U3FFX_BACKEND_CAPABILITY_PLUGIN;
	capabilities->has_vulkan_backend = validated ? 1 : 0;
	capabilities->has_fsr2 = 0;
	capabilities->has_fsr31_upscaling = 0;
	capabilities->has_frame_generation = 0;
	copy_message(capabilities->message, validated ? "Bridge Vulkan compute validado por readback" : "Bridge Vulkan carregado; compute smoke ainda não validado por readback");
	return 1;
}

U3FFX_API const char* u3ffx_get_last_error()
{
	return g_last_error;
}

U3FFX_API int32_t u3ffx_get_vulkan_smoke_event_id()
{
	return g_smoke_event_id;
}

U3FFX_API int32_t u3ffx_get_vulkan_smoke_state()
{
	return g_smoke_state.load(std::memory_order_acquire);
}

U3FFX_API int32_t u3ffx_mark_vulkan_smoke_validated(int32_t validated)
{
	bool can_validate = validated != 0 && g_smoke_state.load(std::memory_order_acquire) == U3FFX_SMOKE_STATUS_RECORDED && g_pipeline != VK_NULL_HANDLE;
	g_smoke_validated.store(can_validate, std::memory_order_release);
	return can_validate ? 1 : 0;
}

U3FFX_API int32_t u3ffx_fsr2_create(const U3FfxFsr2CreateDescription* description)
{
	return create_fsr2_context(description) ? 1 : 0;
}

U3FFX_API void u3ffx_fsr2_destroy()
{
	destroy_fsr2_context(true);
}

U3FFX_API int32_t u3ffx_fsr2_resize(const U3FfxFsr2CreateDescription* description)
{
	return create_fsr2_context(description) ? 1 : 0;
}

U3FFX_API int32_t u3ffx_fsr2_dispatch(U3FfxFsr2DispatchParameters* parameters)
{
	return dispatch_fsr2(parameters);
}

U3FFX_API void u3ffx_fsr2_reset_history()
{
	g_fsr2_reset_history.store(true, std::memory_order_release);
}

U3FFX_API int32_t u3ffx_fsr2_get_status()
{
	return g_fsr2_status.load(std::memory_order_acquire);
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unity_interfaces)
{
	g_unity_interfaces = unity_interfaces;
	g_unity_graphics = g_unity_interfaces != nullptr ? g_unity_interfaces->Get<IUnityGraphics>() : nullptr;
	if (g_unity_graphics == nullptr)
	{
		set_error("IUnityGraphics indisponível");
		return;
	}
	int reserved_event_id = g_unity_graphics->ReserveEventIDRange(1);
	if (reserved_event_id > 0)
		g_smoke_event_id = reserved_event_id;
	g_unity_graphics->RegisterDeviceEventCallback(on_graphics_device_event);
	on_graphics_device_event(kUnityGfxDeviceEventInitialize);
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
	g_accept_dispatches.store(false, std::memory_order_release);
	if (g_unity_graphics != nullptr)
		g_unity_graphics->UnregisterDeviceEventCallback(on_graphics_device_event);
	destroy_vulkan_resources();
	g_unity_vulkan = nullptr;
	g_unity_graphics = nullptr;
	g_unity_interfaces = nullptr;
	g_smoke_validated.store(false, std::memory_order_release);
}

U3FFX_API U3FfxUnityRenderingEvent GetRenderEventFunc()
{
	return &render_event;
}

U3FFX_API U3FfxUnityRenderingEventAndData GetRenderEventAndDataFunc()
{
	return &render_event_and_data;
}
}
