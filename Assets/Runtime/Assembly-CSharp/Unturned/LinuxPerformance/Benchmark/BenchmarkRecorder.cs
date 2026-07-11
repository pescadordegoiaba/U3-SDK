////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.IO;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class BenchmarkRecorder
	{
		public static string ResultsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));

		public static void EnsureResultsRoot()
		{
			Directory.CreateDirectory(ResultsRoot);
		}
	}
}
