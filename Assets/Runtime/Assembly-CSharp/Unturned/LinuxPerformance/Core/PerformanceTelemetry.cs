////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class PerformanceTelemetry : MonoBehaviour
	{
		public struct FrameSnapshot
		{
			public int ScreenWidth;
			public int ScreenHeight;
			public int InternalWidth;
			public int InternalHeight;
			public float DeltaTime;
			public long ManagedMemoryBytes;
			public long MonoUsedBytes;
			public long VmRssKb;
			public long PssKb;
			public int RenderTargetPoolActive;
			public int RenderTargetPoolIdle;
		}

		public static FrameSnapshot LastFrame { get; private set; }
		public static string LastUpscalerName { get; private set; } = "Off";

		public static FrameSnapshot CaptureFrameSnapshot()
		{
			using (marker.Auto())
			{
				bool sampleMemory = Time.unscaledTime >= nextMemorySampleTime;
				LinuxProcessMemorySnapshot memory = LinuxProcessMemoryReader.LastSnapshot;
				long managedMemoryBytes = LastFrame.ManagedMemoryBytes;
				long monoUsedBytes = LastFrame.MonoUsedBytes;
				if (sampleMemory)
				{
					nextMemorySampleTime = Time.unscaledTime + 1.0f;
					LinuxProcessMemoryReader.TryRead(out memory, GraphicsSettings.LinuxDebugOverlay);
					managedMemoryBytes = GC.GetTotalMemory(false);
					monoUsedBytes = Profiler.GetMonoUsedSizeLong();
				}

				LastFrame = new FrameSnapshot()
				{
					ScreenWidth = Screen.width,
					ScreenHeight = Screen.height,
					InternalWidth = DynamicResolutionController.InternalWidth,
					InternalHeight = DynamicResolutionController.InternalHeight,
					DeltaTime = Time.unscaledDeltaTime,
					ManagedMemoryBytes = managedMemoryBytes,
					MonoUsedBytes = monoUsedBytes,
					VmRssKb = memory.VmRssKb,
					PssKb = memory.PssKb,
					RenderTargetPoolActive = RenderTargetPool.ActiveCount,
					RenderTargetPoolIdle = RenderTargetPool.IdleCount,
				};
				return LastFrame;
			}
		}

		public static void NotifyUpscalerRendered(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, string backendName)
		{
			using (marker.Auto())
			{
				LastUpscalerName = backendName;
			}
		}

		private void OnGUI()
		{
			if (!GraphicsSettings.LinuxDebugOverlay)
				return;
			GUI.Box(new Rect(8, 8, 460, 174), "Diagnóstico Linux");
			GUI.Label(new Rect(18, 34, 440, 22), $"Upscaler: {LastUpscalerName} ({UpscalerManager.LastSelectionReason})");
			GUI.Label(new Rect(18, 56, 440, 22), $"Resolução interna/final: {LastFrame.InternalWidth}x{LastFrame.InternalHeight} -> {LastFrame.ScreenWidth}x{LastFrame.ScreenHeight}");
			GUI.Label(new Rect(18, 78, 440, 22), $"RSS/PSS: {LastFrame.VmRssKb / 1024} MB / {LastFrame.PssKb / 1024} MB");
			GUI.Label(new Rect(18, 100, 440, 22), $"Mono/GC: {LastFrame.MonoUsedBytes / (1024 * 1024)} MB / {LastFrame.ManagedMemoryBytes / (1024 * 1024)} MB");
			GUI.Label(new Rect(18, 122, 440, 22), $"Render targets: ativos {LastFrame.RenderTargetPoolActive}, ociosos {LastFrame.RenderTargetPoolIdle}");
			GUI.Label(new Rect(18, 144, 440, 22), $"Movimento câmera: {MotionAdaptiveResolutionController.AngularVelocityDegrees:0} deg/s, gargalo {DynamicResolutionController.Bottleneck}");
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.Telemetry");
		private static float nextMemorySampleTime;
	}
}
