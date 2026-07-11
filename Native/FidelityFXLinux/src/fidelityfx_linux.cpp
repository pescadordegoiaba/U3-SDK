#include "fidelityfx_linux.h"

#include <cstring>

namespace
{
thread_local char g_last_error[256] = "Sem erro";
void* g_unity_interfaces = nullptr;

void copy_message(char* destination, const char* source)
{
	if (destination == nullptr)
	{
		return;
	}

	std::strncpy(destination, source, 255);
	destination[255] = '\0';
}

void render_event(int32_t)
{
}

void render_event_and_data(int32_t, void*)
{
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
		copy_message(g_last_error, "Ponteiro de capacidades inválido");
		return 0;
	}

	capabilities->abi_version = U3FFX_ABI_VERSION;
	capabilities->backend_kind = U3FFX_BACKEND_CAPABILITY_PLUGIN;
	capabilities->has_vulkan_backend = 0;
	capabilities->has_fsr2 = 0;
	capabilities->has_fsr31_upscaling = 0;
	capabilities->has_frame_generation = 0;
	copy_message(capabilities->message, "Capability plugin ABI v2 carregado; backend Vulkan/FidelityFX ainda não implementado");
	copy_message(g_last_error, "Sem erro");
	return 1;
}

U3FFX_API const char* u3ffx_get_last_error()
{
	return g_last_error;
}

U3FFX_API void UnityPluginLoad(void* unityInterfaces)
{
	g_unity_interfaces = unityInterfaces;
	copy_message(g_last_error, "UnityPluginLoad executado; bridge Vulkan ainda não inicializado");
}

U3FFX_API void UnityPluginUnload()
{
	g_unity_interfaces = nullptr;
	copy_message(g_last_error, "UnityPluginUnload executado");
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
