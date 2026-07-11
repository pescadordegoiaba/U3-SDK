////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public enum ELinuxUpscalerMode
	{
		Off,
		Auto,
		Fsr1,
		Fsr2,
		Fsr31Upscaling,
	}

	public enum EFsrQualityPreset
	{
		NativeAA,
		UltraQuality,
		Quality,
		Balanced,
		Performance,
		UltraPerformance,
		Custom,
	}

	public enum ELinuxMemoryProfile
	{
		MuitoBaixo,
		Baixo,
		Equilibrado,
		Automatico,
	}

	public enum ELinuxCullingProfile
	{
		Original,
		Basico,
		Agressivo,
		Experimental,
	}

	public enum ELinuxFeatureState
	{
		Available,
		Unsupported,
		Disabled,
		Error,
	}

	public enum ELinuxNativeBackendKind
	{
		Unknown,
		Skeleton,
		CapabilityPlugin,
		VulkanComputeFunctional,
		Fsr2FunctionalBackend,
		Fsr31FunctionalBackend,
	}

	public enum EFsr4UnsupportedReason
	{
		None = 0,
		UnsupportedGpu = 1 << 0,
		UnsupportedOperatingSystem = 1 << 1,
		UnsupportedGraphicsApi = 1 << 2,
		SdkUnavailable = 1 << 3,
	}

	public struct PerformanceSettings
	{
		public ELinuxUpscalerMode UpscalerMode;
		public EFsrQualityPreset FsrQualityPreset;
		public float CustomRenderScale;
		public float RcasSharpness;
		public bool DynamicResolution;
		public bool MotionAdaptiveResolution;
		public float TargetFrameTimeMs;
		public bool LowLatencyMode;
		public ELinuxMemoryProfile MemoryProfile;
		public ELinuxCullingProfile CullingProfile;
		public bool CasEnabled;
		public bool CacaoEnabled;
		public bool SssrExperimental;
		public bool DebugOverlay;

		public static PerformanceSettings FromGraphicsSettings()
		{
			PerformanceSettings settings = new PerformanceSettings();
			settings.UpscalerMode = GraphicsSettings.LinuxUpscalerMode;
			settings.FsrQualityPreset = GraphicsSettings.LinuxFsrQualityPreset;
			settings.CustomRenderScale = GraphicsSettings.LinuxCustomRenderScale;
			settings.RcasSharpness = GraphicsSettings.LinuxRcasSharpness;
			settings.DynamicResolution = GraphicsSettings.LinuxDynamicResolution;
			settings.MotionAdaptiveResolution = GraphicsSettings.LinuxMotionAdaptiveResolution;
			settings.TargetFrameTimeMs = GraphicsSettings.LinuxTargetFrameTimeMs;
			settings.LowLatencyMode = GraphicsSettings.LinuxLowLatencyMode;
			settings.MemoryProfile = GraphicsSettings.LinuxMemoryProfile;
			settings.CullingProfile = GraphicsSettings.LinuxCullingProfile;
			settings.CasEnabled = GraphicsSettings.LinuxCasEnabled;
			settings.CacaoEnabled = GraphicsSettings.LinuxCacaoEnabled;
			settings.SssrExperimental = GraphicsSettings.LinuxSssrExperimental;
			settings.DebugOverlay = GraphicsSettings.LinuxDebugOverlay;
			settings.Validate();
			return settings;
		}

		public void Validate()
		{
			if (!Enum.IsDefined(typeof(ELinuxUpscalerMode), UpscalerMode))
				UpscalerMode = ELinuxUpscalerMode.Off;
			if (!Enum.IsDefined(typeof(EFsrQualityPreset), FsrQualityPreset))
				FsrQualityPreset = EFsrQualityPreset.Quality;
			if (!Enum.IsDefined(typeof(ELinuxMemoryProfile), MemoryProfile))
				MemoryProfile = ELinuxMemoryProfile.Automatico;
			if (!Enum.IsDefined(typeof(ELinuxCullingProfile), CullingProfile))
				CullingProfile = ELinuxCullingProfile.Original;

			CustomRenderScale = Mathf.Clamp(CustomRenderScale, 0.333f, 1.0f);
			RcasSharpness = Mathf.Clamp01(RcasSharpness);
			TargetFrameTimeMs = Mathf.Clamp(TargetFrameTimeMs, 6.9f, 66.7f);

			if (CasEnabled && RcasSharpness > 0.001f)
				CasEnabled = false;
		}

		public float GetPresetScale()
		{
			switch (FsrQualityPreset)
			{
				case EFsrQualityPreset.NativeAA:
					return 1.0f;
				case EFsrQualityPreset.UltraQuality:
					return 0.77f;
				case EFsrQualityPreset.Quality:
					return 0.6667f;
				case EFsrQualityPreset.Balanced:
					return 0.5882f;
				case EFsrQualityPreset.Performance:
					return 0.5f;
				case EFsrQualityPreset.UltraPerformance:
					return 0.3333f;
				case EFsrQualityPreset.Custom:
					return CustomRenderScale;
				default:
					return 1.0f;
			}
		}
	}
}
