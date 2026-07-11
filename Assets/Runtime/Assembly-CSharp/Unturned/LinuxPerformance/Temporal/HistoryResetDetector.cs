////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public static class HistoryResetDetector
	{
		public static bool IsAbnormalDelta(float deltaTimeMilliseconds)
		{
			return deltaTimeMilliseconds <= 0.0f || deltaTimeMilliseconds > 500.0f;
		}
	}
}
