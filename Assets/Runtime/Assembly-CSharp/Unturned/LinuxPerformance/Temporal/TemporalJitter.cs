////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class TemporalJitter
	{
		public static Vector2 GetHalton23(uint frameIndex, int renderWidth, int renderHeight)
		{
			float x = Halton(frameIndex + 1u, 2) - 0.5f;
			float y = Halton(frameIndex + 1u, 3) - 0.5f;
			return new Vector2(x / Mathf.Max(1, renderWidth), y / Mathf.Max(1, renderHeight));
		}

		private static float Halton(uint index, uint radix)
		{
			float result = 0.0f;
			float fraction = 1.0f / radix;
			while (index > 0u)
			{
				result += (index % radix) * fraction;
				index /= radix;
				fraction /= radix;
			}
			return result;
		}
	}
}
