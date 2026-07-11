////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class BenchmarkCameraPath : MonoBehaviour
	{
		public float Radius = 12.0f;
		public float Speed = 0.2f;

		private void Update()
		{
			float t = Time.time * Speed;
			transform.position = new Vector3(Mathf.Cos(t) * Radius, 4.0f, Mathf.Sin(t) * Radius);
			transform.LookAt(Vector3.zero);
		}
	}
}
