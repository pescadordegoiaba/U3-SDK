////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public interface ITemporalUpscalerBackend
	{
		string DisplayName { get; }
		ELinuxFeatureState State { get; }
		string StateReason { get; }
		bool Initialize(in TemporalBackendDescription description);
		void PrepareCamera(Camera camera, ref TemporalFrameContext frame);
		bool Dispatch(ref TemporalFrameContext frame);
		void ResetHistory(TemporalResetReason reason);
		void Resize(int renderWidth, int renderHeight, int outputWidth, int outputHeight);
		void Release();
	}
}
