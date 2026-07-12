#include "fidelityfx_linux.h"

#include "IUnityGraphics.h"
#include "IUnityGraphicsVulkan.h"
#include "IUnityInterface.h"
#include "vulkan_smoke_comp_spv.h"

#include <array>
#include <atomic>
#include <cstring>

namespace
{
constexpr uint32_t kFramesInFlight = 8;

struct SmokeFrameResources
{
	VkImageView source_image_view = VK_NULL_HANDLE;
	VkImageView output_image_view = VK_NULL_HANDLE;
	VkDescriptorSet descriptor_set = VK_NULL_HANDLE;
	uint64_t frame_number = 0;
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

void copy_message(char* destination, const char* source)
{
	if (destination == nullptr)
		return;
	std::strncpy(destination, source, 255);
	destination[255] = '\0';
}

void set_error(const char* message)
{
	copy_message(g_last_error, message);
	g_smoke_state.store(U3FFX_SMOKE_STATUS_ERROR, std::memory_order_release);
}

void release_frame_resources(SmokeFrameResources& frame)
{
	if (g_device == VK_NULL_HANDLE)
		return;
	if (frame.source_image_view != VK_NULL_HANDLE)
		vkDestroyImageView(g_device, frame.source_image_view, nullptr);
	if (frame.output_image_view != VK_NULL_HANDLE)
		vkDestroyImageView(g_device, frame.output_image_view, nullptr);
	if (frame.descriptor_set != VK_NULL_HANDLE && g_descriptor_pool != VK_NULL_HANDLE)
		vkFreeDescriptorSets(g_device, g_descriptor_pool, 1, &frame.descriptor_set);
	frame = {};
}

void collect_completed_frames(uint64_t safe_frame_number)
{
	for (SmokeFrameResources& frame : g_frames)
	{
		if (frame.output_image_view != VK_NULL_HANDLE && frame.frame_number <= safe_frame_number)
			release_frame_resources(frame);
	}
}

void destroy_vulkan_resources()
{
	if (g_device == VK_NULL_HANDLE)
		return;
	vkDeviceWaitIdle(g_device);
	for (SmokeFrameResources& frame : g_frames)
		release_frame_resources(frame);
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
	VkDescriptorSetLayoutCreateInfo descriptor_layout_info{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO};
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
	VkDescriptorPoolCreateInfo pool_info{VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO};
	pool_info.flags = VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT;
	pool_info.maxSets = kFramesInFlight;
	pool_info.poolSizeCount = 1;
	pool_info.pPoolSizes = &pool_size;
	if (vkCreateDescriptorPool(g_device, &pool_info, nullptr, &g_descriptor_pool) != VK_SUCCESS)
	{
		set_error("vkCreateDescriptorPool falhou");
		return false;
	}

	VkPipelineLayoutCreateInfo pipeline_layout_info{VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO};
	pipeline_layout_info.setLayoutCount = 1;
	pipeline_layout_info.pSetLayouts = &g_descriptor_set_layout;
	if (vkCreatePipelineLayout(g_device, &pipeline_layout_info, nullptr, &g_pipeline_layout) != VK_SUCCESS)
	{
		set_error("vkCreatePipelineLayout falhou");
		return false;
	}

	VkShaderModuleCreateInfo shader_info{VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO};
	shader_info.codeSize = sizeof(u3ffx_vulkan_smoke_comp_spv);
	shader_info.pCode = u3ffx_vulkan_smoke_comp_spv;
	VkShaderModule shader_module = VK_NULL_HANDLE;
	if (vkCreateShaderModule(g_device, &shader_info, nullptr, &shader_module) != VK_SUCCESS)
	{
		set_error("vkCreateShaderModule falhou");
		return false;
	}

	VkComputePipelineCreateInfo pipeline_info{VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO};
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
		|| parameters->command != U3FFX_COMMAND_VULKAN_SMOKE || parameters->frame_slot >= kFramesInFlight)
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
	if (frame.output_image_view != VK_NULL_HANDLE)
	{
		set_error("Ring Vulkan cheio; frame ainda em voo");
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

	VkImageViewCreateInfo view_info{VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO};
	view_info.image = source_image.image;
	view_info.viewType = VK_IMAGE_VIEW_TYPE_2D;
	view_info.format = source_image.format;
	view_info.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
	view_info.subresourceRange.levelCount = 1;
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
		release_frame_resources(frame);
		set_error("vkCreateImageView falhou para output");
		return;
	}

	VkDescriptorSetAllocateInfo allocate_info{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO};
	allocate_info.descriptorPool = g_descriptor_pool;
	allocate_info.descriptorSetCount = 1;
	allocate_info.pSetLayouts = &g_descriptor_set_layout;
	if (vkAllocateDescriptorSets(g_device, &allocate_info, &frame.descriptor_set) != VK_SUCCESS)
	{
		release_frame_resources(frame);
		set_error("vkAllocateDescriptorSets falhou");
		return;
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
	vkUpdateDescriptorSets(g_device, 2, writes, 0, nullptr);

	vkCmdBindPipeline(recording.commandBuffer, VK_PIPELINE_BIND_POINT_COMPUTE, g_pipeline);
	vkCmdBindDescriptorSets(recording.commandBuffer, VK_PIPELINE_BIND_POINT_COMPUTE, g_pipeline_layout, 0, 1, &frame.descriptor_set, 0, nullptr);
	uint32_t width = output_image.extent.width;
	uint32_t height = output_image.extent.height;
	vkCmdDispatch(recording.commandBuffer, (width + 7u) / 8u, (height + 7u) / 8u, 1);
	frame.frame_number = recording.currentFrameNumber;
	parameters->width = width;
	parameters->height = height;
	parameters->status = U3FFX_SMOKE_STATUS_RECORDED;
	g_smoke_state.store(U3FFX_SMOKE_STATUS_RECORDED, std::memory_order_release);
	copy_message(g_last_error, "Compute smoke Vulkan gravado no command buffer da Unity");
}

void UNITY_INTERFACE_API render_event(int32_t event_id)
{
	if (event_id == g_smoke_event_id)
		set_error("Smoke Vulkan requer IssuePluginEventAndData");
}

void UNITY_INTERFACE_API render_event_and_data(int32_t event_id, void* data)
{
	if (event_id == g_smoke_event_id)
		dispatch_smoke(static_cast<U3FfxVulkanSmokeParameters*>(data));
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
