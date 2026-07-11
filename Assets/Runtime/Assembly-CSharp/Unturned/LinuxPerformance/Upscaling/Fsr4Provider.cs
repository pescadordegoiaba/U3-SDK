////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	public struct Fsr4CapabilityResult
	{
		public ELinuxFeatureState State;
		public EFsr4UnsupportedReason UnsupportedReasons;
		public string Message;
	}

	public static class Fsr4Provider
	{
		public static Fsr4CapabilityResult Query(in HardwareCapabilities hardware, in LinuxGraphicsCapabilities graphics)
		{
			Fsr4CapabilityResult result = new Fsr4CapabilityResult();
			result.State = ELinuxFeatureState.Unsupported;
			result.UnsupportedReasons = EFsr4UnsupportedReason.SdkUnavailable;
			if (hardware == null || !hardware.IsLinux)
				result.UnsupportedReasons |= EFsr4UnsupportedReason.UnsupportedOperatingSystem;
			if (graphics == null || graphics.GraphicsDeviceType != GraphicsDeviceType.Direct3D12)
				result.UnsupportedReasons |= EFsr4UnsupportedReason.UnsupportedGraphicsApi;
			if (graphics == null || !graphics.IsAmd || graphics.IsRx580OrPolaris)
				result.UnsupportedReasons |= EFsr4UnsupportedReason.UnsupportedGpu;
			result.Message = "FSR 4.1 indisponível neste hardware";
			return result;
		}
	}
}
