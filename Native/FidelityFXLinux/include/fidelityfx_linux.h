#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define U3FFX_API __declspec(dllexport)
#else
#define U3FFX_API __attribute__((visibility("default")))
#endif

extern "C"
{
	static const int32_t U3FFX_ABI_VERSION = 1;

	struct U3FfxCapabilities
	{
		int32_t abi_version;
		int32_t has_vulkan_backend;
		int32_t has_fsr2;
		int32_t has_fsr31_upscaling;
		int32_t has_frame_generation;
		char message[256];
	};

	U3FFX_API int32_t u3ffx_get_abi_version();
	U3FFX_API int32_t u3ffx_get_capabilities(U3FfxCapabilities* capabilities);
	U3FFX_API const char* u3ffx_get_last_error();
}
