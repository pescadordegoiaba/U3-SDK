////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public static class NativePluginDiagnostics
	{
		public static string LastSummary
		{
			get
			{
				NativeRenderBackend.TryGetCapabilities(out NativeRenderBackend.PluginCapabilities capabilities, out string status);
				return $"{status}; ABI={capabilities.AbiVersion}; tipo={capabilities.BackendKind}; Vulkan={capabilities.HasVulkanBackend}; FSR2={capabilities.HasFsr2}; FSR3.1={capabilities.HasFsr31Upscaling}";
			}
		}
	}
}
