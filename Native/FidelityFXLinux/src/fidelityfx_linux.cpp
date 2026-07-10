#include "fidelityfx_linux.h"

#include <cstring>

namespace
{
thread_local char g_last_error[256] = "Sem erro";

void copy_message(char* destination, const char* source)
{
	if (destination == nullptr)
	{
		return;
	}

	std::strncpy(destination, source, 255);
	destination[255] = '\0';
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
	capabilities->has_vulkan_backend = 0;
	capabilities->has_fsr2 = 0;
	capabilities->has_fsr31_upscaling = 0;
	capabilities->has_frame_generation = 0;
	copy_message(capabilities->message, "Plugin ABI carregado; backend Vulkan temporal ainda não implementado");
	copy_message(g_last_error, "Sem erro");
	return 1;
}

U3FFX_API const char* u3ffx_get_last_error()
{
	return g_last_error;
}
}
