////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public interface IUpscalerBackend
	{
		string DisplayName { get; }
		ELinuxFeatureState State { get; }
		string StateReason { get; }
		bool IsTemporal { get; }
		bool IsAvailable(in LinuxGraphicsCapabilities capabilities);
		void ResetHistory();
		void Release();
		bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry);
	}
}
