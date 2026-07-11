#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define U3FFX_API __declspec(dllexport)
#else
#define U3FFX_API __attribute__((visibility("default")))
#endif

extern "C"
{
	static const int32_t U3FFX_ABI_VERSION = 3;
	static const int32_t U3FFX_SMOKE_STATUS_PENDING = 0;
	static const int32_t U3FFX_SMOKE_STATUS_RECORDED = 1;
	static const int32_t U3FFX_SMOKE_STATUS_ERROR = -1;

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
		void* output_texture;
		uint32_t width;
		uint32_t height;
		uint32_t frame_index;
		int32_t status;
	};

	U3FFX_API int32_t u3ffx_get_abi_version();
	U3FFX_API int32_t u3ffx_get_capabilities(U3FfxCapabilities* capabilities);
	U3FFX_API const char* u3ffx_get_last_error();
	U3FFX_API int32_t u3ffx_get_vulkan_smoke_event_id();
	U3FFX_API int32_t u3ffx_get_vulkan_smoke_state();
	U3FFX_API int32_t u3ffx_mark_vulkan_smoke_validated(int32_t validated);
	U3FFX_API void UnityPluginLoad(void* unityInterfaces);
	U3FFX_API void UnityPluginUnload();
	typedef void (*U3FfxUnityRenderingEvent)(int32_t eventId);
	typedef void (*U3FfxUnityRenderingEventAndData)(int32_t eventId, void* data);
	U3FFX_API U3FfxUnityRenderingEvent GetRenderEventFunc();
	U3FFX_API U3FfxUnityRenderingEventAndData GetRenderEventAndDataFunc();
}
