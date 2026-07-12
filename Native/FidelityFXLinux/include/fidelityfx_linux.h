#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define U3FFX_API __declspec(dllexport)
#else
#define U3FFX_API __attribute__((visibility("default")))
#endif

extern "C"
{
	static const int32_t U3FFX_ABI_VERSION = 4;
	static const int32_t U3FFX_SMOKE_STATUS_PENDING = 0;
	static const int32_t U3FFX_SMOKE_STATUS_RECORDED = 1;
	static const int32_t U3FFX_SMOKE_STATUS_ERROR = -1;
	static const uint32_t U3FFX_COMMAND_VULKAN_SMOKE = 1;
	static const uint32_t U3FFX_COMMAND_FSR2_DISPATCH = 2;

	enum U3FfxBackendKind
	{
		U3FFX_BACKEND_UNKNOWN = 0,
		U3FFX_BACKEND_SKELETON = 1,
		U3FFX_BACKEND_CAPABILITY_PLUGIN = 2,
		U3FFX_BACKEND_VULKAN_COMPUTE_FUNCTIONAL = 3,
		U3FFX_BACKEND_FSR2_FUNCTIONAL = 4,
		U3FFX_BACKEND_FSR31_FUNCTIONAL = 5
	};

	struct U3FfxCapabilities
	{
		int32_t abi_version;
		int32_t backend_kind;
		int32_t has_vulkan_backend;
		int32_t has_fsr2;
		int32_t has_fsr31_upscaling;
		int32_t has_frame_generation;
		char message[256];
	};

	struct U3FfxVulkanSmokeParameters
	{
		uint32_t struct_size;
		uint32_t abi_version;
		uint32_t command;
		uint32_t frame_slot;
		uint32_t generation;
		void* source_texture;
		void* output_texture;
		uint32_t width;
		uint32_t height;
		uint32_t frame_index;
		int32_t status;
	};

	struct U3FfxFsr2CreateDescription
	{
		uint32_t struct_size;
		uint32_t abi_version;
		uint32_t max_render_width;
		uint32_t max_render_height;
		uint32_t display_width;
		uint32_t display_height;
		uint32_t flags;
	};

	struct U3FfxFsr2DispatchParameters
	{
		uint32_t struct_size;
		uint32_t abi_version;
		uint32_t command;
		uint32_t frame_slot;
		uint32_t generation;
		void* color_texture;
		void* depth_texture;
		void* motion_vectors_texture;
		void* output_texture;
		void* reactive_texture;
		void* composition_texture;
		uint32_t render_width;
		uint32_t render_height;
		uint32_t output_width;
		uint32_t output_height;
		float jitter_x;
		float jitter_y;
		float motion_vector_scale_x;
		float motion_vector_scale_y;
		float delta_time_ms;
		float camera_near;
		float camera_far;
		float vertical_fov_radians;
		float pre_exposure;
		uint32_t reset_history;
		uint32_t hdr;
		uint32_t inverted_depth;
		uint32_t frame_index;
		int32_t status;
	};

	U3FFX_API int32_t u3ffx_get_abi_version();
	U3FFX_API int32_t u3ffx_get_capabilities(U3FfxCapabilities* capabilities);
	U3FFX_API const char* u3ffx_get_last_error();
	U3FFX_API int32_t u3ffx_get_vulkan_smoke_event_id();
	U3FFX_API int32_t u3ffx_get_vulkan_smoke_state();
	U3FFX_API int32_t u3ffx_mark_vulkan_smoke_validated(int32_t validated);
	U3FFX_API int32_t u3ffx_fsr2_create(const U3FfxFsr2CreateDescription* description);
	U3FFX_API void u3ffx_fsr2_destroy();
	U3FFX_API int32_t u3ffx_fsr2_resize(const U3FfxFsr2CreateDescription* description);
	U3FFX_API int32_t u3ffx_fsr2_dispatch(U3FfxFsr2DispatchParameters* parameters);
	U3FFX_API void u3ffx_fsr2_reset_history();
	U3FFX_API int32_t u3ffx_fsr2_get_status();
	typedef void (*U3FfxUnityRenderingEvent)(int32_t eventId);
	typedef void (*U3FfxUnityRenderingEventAndData)(int32_t eventId, void* data);
	U3FFX_API U3FfxUnityRenderingEvent GetRenderEventFunc();
	U3FFX_API U3FfxUnityRenderingEventAndData GetRenderEventAndDataFunc();
}
