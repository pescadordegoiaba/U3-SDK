////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class BenchmarkController : MonoBehaviour
	{
		public BenchmarkScenario Scenario = BenchmarkScenario.Synthetic;

		private void Awake()
		{
			BenchmarkRecorder.EnsureResultsRoot();
		}
	}
}
