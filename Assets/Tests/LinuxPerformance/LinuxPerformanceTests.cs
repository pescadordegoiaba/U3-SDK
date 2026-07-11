////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.Reflection;
using NUnit.Framework;
using SDG.Unturned.LinuxPerformance;
using UnityEngine;

namespace SDG.Unturned.Tests
{
	internal class LinuxPerformanceTests
	{
		[Test]
		public void PerformanceSettingsClampInvalidValues()
		{
			PerformanceSettings settings = new PerformanceSettings()
			{
				UpscalerMode = (ELinuxUpscalerMode)999,
				FsrQualityPreset = (EFsrQualityPreset)999,
				CustomRenderScale = -1.0f,
				RcasSharpness = 2.0f,
				TargetFrameTimeMs = 999.0f,
				MemoryProfile = (ELinuxMemoryProfile)999,
				CullingProfile = (ELinuxCullingProfile)999,
				CasEnabled = true,
			};

			settings.Validate();

			Assert.AreEqual(ELinuxUpscalerMode.Off, settings.UpscalerMode);
			Assert.AreEqual(EFsrQualityPreset.Quality, settings.FsrQualityPreset);
			Assert.AreEqual(0.333f, settings.CustomRenderScale, 0.001f);
			Assert.AreEqual(1.0f, settings.RcasSharpness, 0.001f);
			Assert.AreEqual(66.7f, settings.TargetFrameTimeMs, 0.001f);
			Assert.AreEqual(ELinuxMemoryProfile.Automatico, settings.MemoryProfile);
			Assert.AreEqual(ELinuxCullingProfile.Original, settings.CullingProfile);
			Assert.IsFalse(settings.CasEnabled);
		}

		[Test]
		public void FsrPresetScalesMatchExpectedRatios()
		{
			PerformanceSettings settings = new PerformanceSettings();

			settings.FsrQualityPreset = EFsrQualityPreset.UltraQuality;
			Assert.AreEqual(0.77f, settings.GetPresetScale(), 0.001f);

			settings.FsrQualityPreset = EFsrQualityPreset.Quality;
			Assert.AreEqual(0.6667f, settings.GetPresetScale(), 0.001f);

			settings.FsrQualityPreset = EFsrQualityPreset.Balanced;
			Assert.AreEqual(0.5882f, settings.GetPresetScale(), 0.001f);

			settings.FsrQualityPreset = EFsrQualityPreset.Performance;
			Assert.AreEqual(0.5f, settings.GetPresetScale(), 0.001f);
		}

		[Test]
		public void UpscalerDoesNotForceDynamicResolutionScale()
		{
			DynamicResolutionController.Reset();

			PerformanceSettings settings = new PerformanceSettings()
			{
				UpscalerMode = ELinuxUpscalerMode.Fsr1,
				FsrQualityPreset = EFsrQualityPreset.Quality,
				DynamicResolution = false,
				TargetFrameTimeMs = 16.7f,
			};

			DynamicResolutionController.Apply(settings);

			Assert.AreEqual(1.0f, DynamicResolutionController.CurrentScale, 0.001f);
		}

		[Test]
		public void Fsr1SkipsNativeResolutionUnlessDiagnosticForced()
		{
			Assert.IsFalse(Fsr1Backend.ShouldRenderForResolution(1280, 1024, 1280, 1024, false));
			Assert.IsTrue(Fsr1Backend.ShouldRenderForResolution(1280, 1024, 1280, 1024, true));
		}

		[Test]
		public void Fsr1RunsOnlyWhenUpscaling()
		{
			Assert.IsTrue(Fsr1Backend.ShouldRenderForResolution(853, 682, 1280, 1024, false));
			Assert.IsFalse(Fsr1Backend.ShouldRenderForResolution(0, 682, 1280, 1024, false));
		}

		[Test]
		public void UpscalerManagerUsesNativeFastPathWhenAllLinuxFeaturesAreOff()
		{
			PerformanceSettings settings = new PerformanceSettings()
			{
				UpscalerMode = ELinuxUpscalerMode.Off,
				DynamicResolution = false,
				MotionAdaptiveResolution = false,
				LowLatencyMode = false,
				CullingProfile = ELinuxCullingProfile.Original,
				CasEnabled = false,
				CacaoEnabled = false,
				SssrExperimental = false,
				DebugOverlay = false,
			};

			Assert.IsTrue(UpscalerManager.IsNativeFastPath(settings));
		}

		[Test]
		public void UpscalerManagerDoesNotUseNativeFastPathWhenFeatureIsEnabled()
		{
			PerformanceSettings settings = new PerformanceSettings()
			{
				UpscalerMode = ELinuxUpscalerMode.Off,
				CullingProfile = ELinuxCullingProfile.Original,
				DebugOverlay = true,
			};

			Assert.IsFalse(UpscalerManager.IsNativeFastPath(settings));
		}

		[Test]
		public void BottleneckClassifierRequiresSixtyValidSamplesAndFifteenEvaluations()
		{
			BottleneckClassifier classifier = new BottleneckClassifier();
			for (int i = 0; i < 59; ++i)
			{
				classifier.Add(5.0f, 5.0f, 20.0f, true);
				Assert.AreEqual(EPerformanceBottleneck.Unknown, classifier.Evaluate(16.667f));
			}

			classifier.Add(5.0f, 5.0f, 20.0f, true);
			for (int i = 0; i < 14; ++i)
				Assert.AreEqual(EPerformanceBottleneck.Unknown, classifier.Evaluate(16.667f));
			Assert.AreEqual(EPerformanceBottleneck.GpuBound, classifier.Evaluate(16.667f));
		}

		[Test]
		public void BottleneckClassifierPrioritizesCpuAndRejectsMissingGpuTiming()
		{
			BottleneckClassifier classifier = new BottleneckClassifier();
			for (int i = 0; i < 60; ++i)
				classifier.Add(20.0f, 20.0f, 20.0f, true);
			for (int i = 0; i < 15; ++i)
				classifier.Evaluate(16.667f);
			Assert.AreEqual(EPerformanceBottleneck.CpuBound, classifier.State);

			classifier.Add(5.0f, 5.0f, 0.0f, false);
			Assert.AreEqual(EPerformanceBottleneck.Unknown, classifier.Evaluate(16.667f));
		}

		[Test]
		public void BottleneckClassifierUsesThirtyFrameScaleCooldown()
		{
			BottleneckClassifier classifier = new BottleneckClassifier();
			classifier.BeginScaleCooldown();
			Assert.AreEqual(30, classifier.ScaleCooldownFrames);
			for (int i = 0; i < 30; ++i)
				classifier.Add(5.0f, 5.0f, 5.0f, true);
			Assert.AreEqual(0, classifier.ScaleCooldownFrames);
		}

		[Test]
		public void TemporalCameraControllerIsPassiveWithoutTemporalBackend()
		{
			GameObject gameObject = new GameObject("TemporalCameraControllerTest");
			try
			{
				Camera camera = gameObject.AddComponent<Camera>();
				Matrix4x4 originalProjection = Matrix4x4.Perspective(60.0f, 1.333f, 0.3f, 1000.0f);
				camera.projectionMatrix = originalProjection;
				TemporalCameraController controller = gameObject.AddComponent<TemporalCameraController>();

				InvokeUnityMessage(controller, "Awake");
				InvokeUnityMessage(controller, "OnPreCull");
				InvokeUnityMessage(controller, "OnPostRender");

				Assert.AreEqual(originalProjection, camera.projectionMatrix);
			}
			finally
			{
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void HardwareCapabilitiesDetectsCurrentRuntime()
		{
			HardwareCapabilities capabilities = HardwareCapabilities.Detect();
			Assert.IsNotNull(capabilities.OperatingSystem);
			Assert.IsNotNull(capabilities.ProcessorType);
			Assert.Greater(capabilities.ProcessorCount, 0);
		}

		[Test]
		public void NativePluginAbiQueryDoesNotThrow()
		{
			int version = 0;
			string status = null;
			Assert.DoesNotThrow(() => NativeRenderBackend.TryGetAbiVersion(out version, out status));
			Assert.IsNotNull(status);
		}

		[Test]
		public void NativePluginCapabilitiesQueryDoesNotThrow()
		{
			NativeRenderBackend.PluginCapabilities capabilities;
			string status = null;
			Assert.DoesNotThrow(() => NativeRenderBackend.TryGetCapabilities(out capabilities, out status));
			Assert.IsNotNull(status);
		}

		[Test]
		public void TemporalJitterHaltonIsBounded()
		{
			for (uint i = 0; i < 16; ++i)
			{
				Vector2 jitter = TemporalJitter.GetHalton23(i, 1920, 1080);
				Assert.GreaterOrEqual(jitter.x, -0.5f / 1920.0f);
				Assert.LessOrEqual(jitter.x, 0.5f / 1920.0f);
				Assert.GreaterOrEqual(jitter.y, -0.5f / 1080.0f);
				Assert.LessOrEqual(jitter.y, 0.5f / 1080.0f);
			}
		}

		[Test]
		public void Fsr4ProviderRejectsLinuxRx580ClassHardware()
		{
			HardwareCapabilities hardware = new HardwareCapabilities()
			{
				IsLinux = true,
			};
			LinuxGraphicsCapabilities graphics = new LinuxGraphicsCapabilities()
			{
				GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
				IsAmd = true,
				IsRx580OrPolaris = true,
			};

			Fsr4CapabilityResult result = Fsr4Provider.Query(hardware, graphics);
			Assert.AreEqual(ELinuxFeatureState.Unsupported, result.State);
			Assert.IsTrue((result.UnsupportedReasons & EFsr4UnsupportedReason.UnsupportedGpu) != 0);
			Assert.IsTrue((result.UnsupportedReasons & EFsr4UnsupportedReason.UnsupportedGraphicsApi) != 0);
		}

		[Test]
		public void LinuxProcessMemoryReaderDoesNotThrow()
		{
			LinuxProcessMemorySnapshot snapshot;
			Assert.DoesNotThrow(() => LinuxProcessMemoryReader.TryRead(out snapshot));
		}

		[Test]
		public void RenderTargetPoolReusesDescriptor()
		{
			RenderTextureDescriptor descriptor = new RenderTextureDescriptor(64, 64, RenderTextureFormat.ARGB32, 0);
			RenderTexture first = RenderTargetPool.Get(descriptor, "TestRT");
			RenderTargetPool.Release(first);
			RenderTexture second = RenderTargetPool.Get(descriptor, "TestRT");
			Assert.AreSame(first, second);
			RenderTargetPool.Release(second);
			RenderTargetPool.Clear();
		}

		private static void InvokeUnityMessage(object target, string methodName)
		{
			MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			method.Invoke(target, null);
		}
	}
}
