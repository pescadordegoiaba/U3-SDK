////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
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
	}
}
