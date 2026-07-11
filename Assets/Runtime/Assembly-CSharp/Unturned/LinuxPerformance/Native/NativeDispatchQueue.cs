////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public static class NativeDispatchQueue
	{
		public static ELinuxFeatureState State => NativeVulkanBridge.State;
		public static string StateReason => NativeVulkanBridge.StateReason;
	}
}
