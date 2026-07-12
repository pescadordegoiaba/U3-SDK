////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.InteropServices;

namespace SDG.Unturned.LinuxPerformance
{
	public static class NativeRenderBackend
	{
		public const int ExpectedAbiVersion = 4;

		public struct PluginCapabilities
		{
			public int AbiVersion;
			public string BackendName;
			public ELinuxNativeBackendKind BackendKind;
			public bool HasVulkanBackend;
			public bool HasFsr2;
			public bool HasFsr31Upscaling;
			public bool HasFrameGeneration;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct NativePluginCapabilities
		{
			public int abi_version;
			public int backend_kind;
			public int has_vulkan_backend;
			public int has_fsr2;
			public int has_fsr31_upscaling;
			public int has_frame_generation;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string message;
		}

		public static bool TryGetAbiVersion(out int version, out string status)
		{
			version = 0;
			try
			{
				version = u3ffx_get_abi_version();
				if (version != ExpectedAbiVersion)
				{
					status = $"ABI do plugin incompatível: {version}, esperado {ExpectedAbiVersion}";
					return false;
				}

				status = "Plugin nativo Linux carregado";
				return true;
			}
			catch (DllNotFoundException)
			{
				status = "Plugin nativo Linux não encontrado; usando fallbacks do Unity";
				return false;
			}
			catch (EntryPointNotFoundException)
			{
				status = "Plugin nativo Linux sem entrada ABI esperada";
				return false;
			}
			catch (Exception e)
			{
				status = "Erro ao consultar plugin nativo Linux: " + e.Message;
				return false;
			}
		}

		public static bool TryGetCapabilities(out PluginCapabilities capabilities, out string status)
		{
			capabilities = default;
			if (!TryGetAbiVersion(out int version, out status))
				return false;

			try
			{
				int ok = u3ffx_get_capabilities(out NativePluginCapabilities nativeCapabilities);
				capabilities.AbiVersion = nativeCapabilities.abi_version;
				capabilities.BackendName = nativeCapabilities.message ?? string.Empty;
				capabilities.BackendKind = (ELinuxNativeBackendKind)nativeCapabilities.backend_kind;
				capabilities.HasVulkanBackend = nativeCapabilities.has_vulkan_backend != 0;
				capabilities.HasFsr2 = nativeCapabilities.has_fsr2 != 0;
				capabilities.HasFsr31Upscaling = nativeCapabilities.has_fsr31_upscaling != 0;
				capabilities.HasFrameGeneration = nativeCapabilities.has_frame_generation != 0;

				if (ok == 0)
				{
					status = "Plugin nativo Linux respondeu erro ao consultar capacidades";
					return false;
				}
				if (capabilities.AbiVersion != ExpectedAbiVersion)
				{
					status = $"ABI do plugin incompatível: {capabilities.AbiVersion}, esperado {ExpectedAbiVersion}";
					return false;
				}

				status = "Plugin nativo Linux carregado: " + capabilities.BackendName;
				return true;
			}
			catch (EntryPointNotFoundException)
			{
				status = "Plugin nativo Linux sem entrada de capacidades";
				capabilities.AbiVersion = version;
				return false;
			}
			catch (Exception e)
			{
				status = "Erro ao consultar capacidades do plugin nativo Linux: " + e.Message;
				capabilities.AbiVersion = version;
				return false;
			}
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_abi_version();

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_capabilities(out NativePluginCapabilities capabilities);
	}

	public static class FidelityFxNative
	{
		public static string SdkVersion => "AMD FidelityFX FSR 2.2.1, tag v2.2.1, commit 1680d1edd5c034f88ebbbb793d8b88f8842cf804.";
	}
}
