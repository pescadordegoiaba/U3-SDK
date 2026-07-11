////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace SDG.Unturned.LinuxPerformance
{
	public static class MemoryBudgetManager
	{
		public static long ManagedMemoryBytes { get; private set; }
		public static long MonoUsedBytes { get; private set; }
		public static long TargetRssMb { get; private set; }
		public static LinuxProcessMemorySnapshot ProcessMemory { get; private set; }

		public static void Apply(in PerformanceSettings settings)
		{
			ManagedMemoryBytes = GC.GetTotalMemory(false);
			MonoUsedBytes = Profiler.GetMonoUsedSizeLong();
			LinuxProcessMemoryReader.TryRead(out LinuxProcessMemorySnapshot snapshot);
			ProcessMemory = snapshot;

			switch (settings.MemoryProfile)
			{
				case ELinuxMemoryProfile.MuitoBaixo:
					TargetRssMb = 3000;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 256;
					QualitySettings.streamingMipmapsMaxLevelReduction = 3;
					break;
				case ELinuxMemoryProfile.Baixo:
					TargetRssMb = 3300;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 384;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
				case ELinuxMemoryProfile.Equilibrado:
					TargetRssMb = 4096;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 512;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
				default:
					TargetRssMb = SystemInfo.systemMemorySize <= 8192 ? 3300 : 4096;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = SystemInfo.systemMemorySize <= 8192 ? 384 : 512;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
			}
		}
	}
}
