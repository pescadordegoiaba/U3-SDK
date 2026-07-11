////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Unity.Profiling;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public enum EPerformanceBottleneck
	{
		Unknown,
		CpuBound,
		GpuBound,
		Balanced,
	}

	public static class DynamicResolutionController
	{
		public static int InternalWidth { get; private set; }
		public static int InternalHeight { get; private set; }
		public static float CurrentScale { get; private set; } = 1.0f;
		public static EPerformanceBottleneck Bottleneck { get; private set; } = EPerformanceBottleneck.Unknown;

		public static void AttachCamera(Camera camera)
		{
			if (camera == null)
				return;
			camera.allowDynamicResolution = GraphicsSettings.LinuxDynamicResolution;
		}

		public static void Apply(in PerformanceSettings settings)
		{
			using (marker.Auto())
			{
				float frameMs = Time.unscaledDeltaTime * 1000.0f;
				Bottleneck = EstimateBottleneck(frameMs, settings.TargetFrameTimeMs);
				float scale = settings.DynamicResolution ? settings.GetPresetScale() : 1.0f;
				if (settings.DynamicResolution && Bottleneck != EPerformanceBottleneck.GpuBound)
					scale = Mathf.Max(scale, CurrentScale);
				scale = Mathf.Clamp(scale, 0.333f, 1.0f);
				if (!Mathf.Approximately(scale, CurrentScale))
				{
					CurrentScale = scale;
					ScalableBufferManager.ResizeBuffers(scale, scale);
				}
				InternalWidth = Mathf.Max(1, Mathf.RoundToInt(Screen.width * CurrentScale));
				InternalHeight = Mathf.Max(1, Mathf.RoundToInt(Screen.height * CurrentScale));
			}
		}

		public static void Reset()
		{
			CurrentScale = 1.0f;
			InternalWidth = Mathf.Max(1, Screen.width);
			InternalHeight = Mathf.Max(1, Screen.height);
			ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);
		}

		private static EPerformanceBottleneck EstimateBottleneck(float frameMs, float targetMs)
		{
			if (targetMs <= 0.0f)
				return EPerformanceBottleneck.Unknown;
			if (frameMs < targetMs * 0.9f)
				return EPerformanceBottleneck.Balanced;
			if (SystemInfo.supportsGpuRecorder)
				return EPerformanceBottleneck.GpuBound;
			return EPerformanceBottleneck.Unknown;
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.DynamicResolution");
	}
}
