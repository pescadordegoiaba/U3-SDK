////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class LowLatencyController
	{
		public static void Apply(in PerformanceSettings settings)
		{
			if (!settings.LowLatencyMode)
				return;

			if (!GraphicsSettings.buffer && GraphicsSettings.UseTargetFrameRate)
				Application.targetFrameRate = Mathf.Max(15, GraphicsSettings.TargetFrameRate);
		}
	}
}
