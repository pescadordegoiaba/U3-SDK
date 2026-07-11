////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.Collections;
using System.IO;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class VisualValidationController : MonoBehaviour
	{
		private IEnumerator Start()
		{
			yield return new WaitForSecondsRealtime(2.0f);
			string path = Path.Combine(BenchmarkRecorder.ResultsRoot, "visual-validation.png");
			ScreenCapture.CaptureScreenshot(path);
		}
	}
}
