////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public struct VulkanMemoryBudgetSnapshot
	{
		public bool Available;
		public ulong DeviceLocalBudgetBytes;
		public ulong DeviceLocalUsageBytes;
		public string Limitation;
	}

	public static class VulkanMemoryBudgetReader
	{
		public static bool TryRead(out VulkanMemoryBudgetSnapshot snapshot)
		{
			snapshot = default;
			snapshot.Limitation = "VK_EXT_memory_budget ainda não está ligado ao plugin Vulkan; dados indisponíveis.";
			return false;
		}
	}
}
