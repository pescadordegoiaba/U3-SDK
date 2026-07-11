////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public sealed class TemporalHistory
	{
		public TemporalResetReason LastResetReason { get; private set; } = TemporalResetReason.Unknown;
		public uint LastResetFrame { get; private set; }

		public void Reset(TemporalResetReason reason, uint frameIndex)
		{
			LastResetReason = reason;
			LastResetFrame = frameIndex;
		}
	}
}
