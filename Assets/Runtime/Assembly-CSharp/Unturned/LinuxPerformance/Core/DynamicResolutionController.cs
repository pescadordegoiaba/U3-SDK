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
		Headroom,
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
			Apply(settings, default(PerformanceTelemetry.FrameSnapshot));
		}

		public static void Apply(in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot snapshot)
		{
			using (marker.Auto())
			{
				classifier.Add(snapshot.MainThreadMs, snapshot.RenderThreadMs, snapshot.GpuMs, snapshot.HasCpuTiming && snapshot.HasGpuTiming);
				Bottleneck = classifier.Evaluate(settings.TargetFrameTimeMs);

				float scale = CurrentScale;
				if (!settings.DynamicResolution)
					scale = 1.0f;
				else if (classifier.ScaleCooldownFrames == 0)
				{
					float minimumScale = Mathf.Clamp(settings.GetPresetScale(), 0.333f, 1.0f);
					if (Bottleneck == EPerformanceBottleneck.GpuBound)
						scale = Mathf.Max(minimumScale, CurrentScale - MaxScaleStep);
					else if (Bottleneck == EPerformanceBottleneck.Headroom)
						scale = Mathf.Min(1.0f, CurrentScale + MaxScaleStep);
					classifier.BeginScaleCooldown();
				}

				SetScale(Mathf.Clamp(scale, 0.333f, 1.0f));
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
			classifier.Reset();
			Bottleneck = EPerformanceBottleneck.Unknown;
		}

		private static void SetScale(float scale)
		{
			if (Mathf.Approximately(scale, CurrentScale))
				return;
			CurrentScale = scale;
			ScalableBufferManager.ResizeBuffers(scale, scale);
		}

		private const float MaxScaleStep = 0.025f;
		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.DynamicResolution");
		private static readonly BottleneckClassifier classifier = new BottleneckClassifier();
	}

	internal sealed class BottleneckClassifier
	{
		public const int Capacity = 120;
		public const int MinimumSamples = 60;
		public const int RequiredConsecutiveEvaluations = 15;
		public const int CooldownFrames = 30;

		public int SampleCount => count;
		public int ScaleCooldownFrames => scaleCooldownFrames;
		public EPerformanceBottleneck State { get; private set; }

		public void Add(float mainMs, float renderMs, float gpuMs, bool valid)
		{
			if (scaleCooldownFrames > 0)
				--scaleCooldownFrames;
			lastSampleValid = valid && IsValid(mainMs) && IsValid(renderMs) && IsValid(gpuMs);
			if (!lastSampleValid)
			{
				return;
			}

			mainSamples[nextIndex] = mainMs;
			renderSamples[nextIndex] = renderMs;
			gpuSamples[nextIndex] = gpuMs;
			nextIndex = (nextIndex + 1) % Capacity;
			if (count < Capacity)
				++count;
		}

		public EPerformanceBottleneck Evaluate(float targetMs)
		{
			if (!lastSampleValid)
			{
				State = EPerformanceBottleneck.Unknown;
				candidateState = EPerformanceBottleneck.Unknown;
				candidateCount = 0;
				return State;
			}

			if (count < MinimumSamples || targetMs <= 0.0f)
			{
				ObserveCandidate(EPerformanceBottleneck.Unknown);
				return State;
			}

			float mainAverage = Average(mainSamples, count);
			float renderAverage = Average(renderSamples, count);
			float gpuAverage = Average(gpuSamples, count);
			float mainP95 = Percentile95(mainSamples, count);
			float renderP95 = Percentile95(renderSamples, count);
			float gpuP95 = Percentile95(gpuSamples, count);
			float high = targetMs * 0.94f;
			float low = targetMs * 0.86f;

			EPerformanceBottleneck candidate;
			if ((mainAverage > high && mainP95 > high) || (renderAverage > high && renderP95 > high))
				candidate = EPerformanceBottleneck.CpuBound;
			else if (gpuAverage > high && gpuP95 > high && mainAverage < low && mainP95 < low && renderAverage < low && renderP95 < low)
				candidate = EPerformanceBottleneck.GpuBound;
			else if (gpuAverage < low && gpuP95 < low && mainAverage < low && mainP95 < low && renderAverage < low && renderP95 < low)
				candidate = EPerformanceBottleneck.Headroom;
			else
				candidate = EPerformanceBottleneck.Unknown;

			ObserveCandidate(candidate);
			return State;
		}

		public void BeginScaleCooldown()
		{
			scaleCooldownFrames = CooldownFrames;
		}

		public void Reset()
		{
			nextIndex = 0;
			count = 0;
			candidateCount = 0;
			scaleCooldownFrames = 0;
			State = EPerformanceBottleneck.Unknown;
			candidateState = EPerformanceBottleneck.Unknown;
		}

		private void ObserveCandidate(EPerformanceBottleneck candidate)
		{
			if (candidate != candidateState)
			{
				candidateState = candidate;
				candidateCount = 1;
			}
			else if (candidateCount < RequiredConsecutiveEvaluations)
			{
				++candidateCount;
			}

			if (candidateCount >= RequiredConsecutiveEvaluations)
				State = candidateState;
		}

		private static bool IsValid(float value)
		{
			return value > 0.0f && value < 1000.0f && !float.IsNaN(value) && !float.IsInfinity(value);
		}

		private static float Average(float[] values, int length)
		{
			float sum = 0.0f;
			for (int i = 0; i < length; ++i)
				sum += values[i];
			return sum / length;
		}

		private float Percentile95(float[] values, int length)
		{
			System.Array.Copy(values, scratch, length);
			System.Array.Sort(scratch, 0, length);
			int index = Mathf.Clamp(Mathf.CeilToInt(length * 0.95f) - 1, 0, length - 1);
			return scratch[index];
		}

		private readonly float[] mainSamples = new float[Capacity];
		private readonly float[] renderSamples = new float[Capacity];
		private readonly float[] gpuSamples = new float[Capacity];
		private readonly float[] scratch = new float[Capacity];
		private int nextIndex;
		private int count;
		private int scaleCooldownFrames;
		private int candidateCount;
		private EPerformanceBottleneck candidateState;
		private bool lastSampleValid;
	}
}
