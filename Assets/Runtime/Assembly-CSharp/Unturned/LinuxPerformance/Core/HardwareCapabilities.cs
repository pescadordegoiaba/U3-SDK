////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class HardwareCapabilities
	{
		public string OperatingSystem;
		public string ProcessorType;
		public int ProcessorCount;
		public bool IsLinux;
		public bool IsX86_64;
		public bool IsLikelyI73770;
		public long SystemMemoryMb;

		public static HardwareCapabilities Detect()
		{
			HardwareCapabilities result = new HardwareCapabilities();
			result.OperatingSystem = SystemInfo.operatingSystem ?? string.Empty;
			result.ProcessorType = SystemInfo.processorType ?? string.Empty;
			result.ProcessorCount = SystemInfo.processorCount;
			result.SystemMemoryMb = SystemInfo.systemMemorySize;
			result.IsLinux = Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
			result.IsX86_64 = IntPtr.Size == 8;
			result.IsLikelyI73770 = result.ProcessorType.IndexOf("i7-3770", StringComparison.OrdinalIgnoreCase) >= 0;
			return result;
		}
	}
}
