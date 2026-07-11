////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public enum ETemporalDebugView
	{
		None,
		Depth,
		MotionVectorsRgb,
		MotionVectorMagnitude,
		MissingMotionVectors,
		Disocclusion,
		ReactiveMask,
		CompositionMask,
		HistoryReset,
	}

	public static class TemporalDebugViews
	{
		public static ETemporalDebugView ActiveView { get; set; }
	}
}
