////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;

namespace SDG.Unturned.LinuxPerformance
{
	public struct LinuxProcessMemorySnapshot
	{
		public long VmRssKb;
		public long VmHwmKb;
		public long VmSizeKb;
		public long RssAnonKb;
		public long RssFileKb;
		public long PssKb;
		public long PrivateCleanKb;
		public long PrivateDirtyKb;
		public long SwapKb;
	}

	public static class LinuxProcessMemoryReader
	{
		public static LinuxProcessMemorySnapshot LastSnapshot { get; private set; }

		public static bool TryRead(out LinuxProcessMemorySnapshot snapshot)
		{
			return TryRead(out snapshot, true);
		}

		public static bool TryRead(out LinuxProcessMemorySnapshot snapshot, bool includeSmapsRollup)
		{
			snapshot = default;
			try
			{
				ReadStatus(ref snapshot);
				if (includeSmapsRollup)
					ReadSmapsRollup(ref snapshot);
				else
				{
					snapshot.PssKb = LastSnapshot.PssKb;
					snapshot.PrivateCleanKb = LastSnapshot.PrivateCleanKb;
					snapshot.PrivateDirtyKb = LastSnapshot.PrivateDirtyKb;
				}
				LastSnapshot = snapshot;
				return true;
			}
			catch
			{
				snapshot = LastSnapshot;
				return false;
			}
		}

		private static void ReadStatus(ref LinuxProcessMemorySnapshot snapshot)
		{
			if (!File.Exists("/proc/self/status"))
				return;
			string[] lines = File.ReadAllLines("/proc/self/status");
			for (int i = 0; i < lines.Length; ++i)
			{
				string line = lines[i];
				if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
					snapshot.VmRssKb = ParseKb(line);
				else if (line.StartsWith("VmHWM:", StringComparison.Ordinal))
					snapshot.VmHwmKb = ParseKb(line);
				else if (line.StartsWith("VmSize:", StringComparison.Ordinal))
					snapshot.VmSizeKb = ParseKb(line);
				else if (line.StartsWith("RssAnon:", StringComparison.Ordinal))
					snapshot.RssAnonKb = ParseKb(line);
				else if (line.StartsWith("RssFile:", StringComparison.Ordinal))
					snapshot.RssFileKb = ParseKb(line);
				else if (line.StartsWith("VmSwap:", StringComparison.Ordinal))
					snapshot.SwapKb = ParseKb(line);
			}
		}

		private static void ReadSmapsRollup(ref LinuxProcessMemorySnapshot snapshot)
		{
			if (!File.Exists("/proc/self/smaps_rollup"))
				return;
			string[] lines = File.ReadAllLines("/proc/self/smaps_rollup");
			for (int i = 0; i < lines.Length; ++i)
			{
				string line = lines[i];
				if (line.StartsWith("Pss:", StringComparison.Ordinal))
					snapshot.PssKb = ParseKb(line);
				else if (line.StartsWith("Private_Clean:", StringComparison.Ordinal))
					snapshot.PrivateCleanKb = ParseKb(line);
				else if (line.StartsWith("Private_Dirty:", StringComparison.Ordinal))
					snapshot.PrivateDirtyKb = ParseKb(line);
			}
		}

		private static long ParseKb(string line)
		{
			string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return 0;
			long.TryParse(parts[1], out long value);
			return value;
		}
	}
}
