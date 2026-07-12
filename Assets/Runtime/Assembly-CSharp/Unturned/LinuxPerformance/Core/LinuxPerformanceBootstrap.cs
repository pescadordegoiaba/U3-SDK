////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class LinuxPerformanceBootstrap : MonoBehaviour
	{
		public static bool IsDisableAllRequested => disableAllRequested;

		public static LinuxPerformanceBootstrap GetOrCreate(GameObject host)
		{
			if (disableAllRequested)
			{
				ApplyDisableAllOverrides();
				DisableOptionalComponent<PerformanceTelemetry>(host);
				DisableOptionalComponent<TemporalCameraController>(host);
				DisableOptionalComponent<LowResolutionWorldRenderer>(host);
				return null;
			}
			LinuxPerformanceBootstrap bootstrap = host.GetComponent<LinuxPerformanceBootstrap>();
			if (bootstrap == null)
				bootstrap = host.AddComponent<LinuxPerformanceBootstrap>();
			if (host.GetComponent<PerformanceTelemetry>() == null)
				host.AddComponent<PerformanceTelemetry>();
			if (host.GetComponent<TemporalCameraController>() == null)
				host.AddComponent<TemporalCameraController>();
			if (host.GetComponent<LowResolutionWorldRenderer>() == null)
				host.AddComponent<LowResolutionWorldRenderer>();
			return bootstrap;
		}

		private void Awake()
		{
			ApplyCommandLineOverrides();
			cameraComponent = GetComponent<Camera>();
			PerformanceSettingsCache.NotifyMainCameraChanged(cameraComponent);
			PerformanceSettingsCache.Invalidate("Plugin inicializado");
			UpscalerManager.InitializeForCamera(cameraComponent);
			Level.onLevelLoaded += OnLevelLoaded;
			Level.onLevelExited += OnLevelExited;
		}

		private IEnumerator Start()
		{
			if (ShouldRunVulkanSmoke(Environment.GetCommandLineArgs()) && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
			{
				bool smokePassed = false;
				string smokeReason = "Não executado";
				yield return NativeVulkanBridge.ValidateSmokeCoroutine((passed, reason) =>
				{
					smokePassed = passed;
					smokeReason = reason;
				});
				if (smokePassed)
					UnturnedLog.info("Linux Performance: {0}", smokeReason);
				else
					UnturnedLog.warn("Linux Performance: smoke Vulkan não validado: {0}", smokeReason);
			}

			if (!HasCommandLineArg("-LinuxPerformanceCapture"))
				yield break;

			yield return new WaitForSecondsRealtime(10.0f);
			string captureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Benchmark", "Captures"));
			Directory.CreateDirectory(captureDirectory);
			string capturePath = Path.Combine(captureDirectory, "linux-performance-capture.png");
			ScreenCapture.CaptureScreenshot(capturePath);
			UnturnedLog.info("Captura Linux Performance solicitada por linha de comando: {0}", capturePath);
			yield return new WaitForSecondsRealtime(2.0f);
			Application.Quit(0);
		}

		private void Update()
		{
			VisibilityBudgetManager.Tick(cameraComponent, PerformanceSettingsCache.Current);
		}

		private static void ApplyCommandLineOverrides()
		{
			if (disableAllRequested)
			{
				ApplyDisableAllOverrides();
				return;
			}
			if (HasCommandLineArg("-LinuxPerformanceForceFsr1"))
			{
				GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Fsr1;
				GraphicsSettings.LinuxFsrQualityPreset = EFsrQualityPreset.Quality;
				GraphicsSettings.LinuxRcasSharpness = 0.2f;
				GraphicsSettings.LinuxDynamicResolution = false;
				UnturnedLog.info("Linux Performance: FSR 1 Quality forçado por linha de comando");
			}
			else if (HasCommandLineArg("-LinuxPerformanceForceNative"))
			{
				GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Off;
				GraphicsSettings.LinuxDynamicResolution = false;
				GraphicsSettings.LinuxMotionAdaptiveResolution = false;
				GraphicsSettings.LinuxLowLatencyMode = false;
				GraphicsSettings.LinuxCullingProfile = ELinuxCullingProfile.Original;
				GraphicsSettings.LinuxCasEnabled = false;
				GraphicsSettings.LinuxCacaoEnabled = false;
				GraphicsSettings.LinuxSssrExperimental = false;
				GraphicsSettings.LinuxDebugOverlay = false;
				UnturnedLog.info("Linux Performance: modo nativo forçado por linha de comando");
			}

			if (HasCommandLineArg("-LinuxPerformanceDisableDynamicResolution"))
			{
				GraphicsSettings.LinuxDynamicResolution = false;
				UnturnedLog.info("Linux Performance: resolução dinâmica desativada por linha de comando");
			}

			if (HasCommandLineArg("-LinuxPerformanceDebugOverlay"))
				GraphicsSettings.LinuxDebugOverlay = true;
		}

		internal static bool ShouldRunVulkanSmoke(string[] args)
		{
			return !HasCommandLineArg(args, "-LinuxPerformanceDisableAll") && HasCommandLineArg(args, "-LinuxPerformanceRunVulkanSmoke");
		}

		internal static bool HasCommandLineArg(string[] args, string value)
		{
			if (args == null)
				return false;
			for (int i = 0; i < args.Length; ++i)
			{
				if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private static bool HasCommandLineArg(string value) => HasCommandLineArg(Environment.GetCommandLineArgs(), value);

		private static void ApplyDisableAllOverrides()
		{
			GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Off;
			GraphicsSettings.LinuxDynamicResolution = false;
			GraphicsSettings.LinuxMotionAdaptiveResolution = false;
			GraphicsSettings.LinuxLowLatencyMode = false;
			GraphicsSettings.LinuxCullingProfile = ELinuxCullingProfile.Original;
			GraphicsSettings.LinuxCasEnabled = false;
			GraphicsSettings.LinuxCacaoEnabled = false;
			GraphicsSettings.LinuxSssrExperimental = false;
			GraphicsSettings.LinuxDebugOverlay = false;
			TemporalDebugViews.ActiveView = ETemporalDebugView.None;
			VisibilityBudgetManager.RestoreAll();
			PerformanceSettingsCache.Invalidate("LinuxPerformanceDisableAll");
		}

		private static void DisableOptionalComponent<T>(GameObject host) where T : Behaviour
		{
			T component = host.GetComponent<T>();
			if (component != null)
				component.enabled = false;
		}

		private void OnDisable()
		{
			PerformanceSettingsCache.Invalidate("Plugin encerrado");
			UpscalerManager.Release();
		}

		private void OnDestroy()
		{
			Level.onLevelLoaded -= OnLevelLoaded;
			Level.onLevelExited -= OnLevelExited;
			PerformanceSettingsCache.Invalidate("Plugin destruído");
			VisibilityBudgetManager.RestoreAll();
			LowResolutionWorldRenderer.ReleaseAll();
			TemporalDebugViews.Release();
			TemporalInputCollector.Clear();
			NativeVulkanBridge.Release();
			UpscalerManager.Release();
		}

		private static void OnLevelLoaded(int level)
		{
			PerformanceSettingsCache.Invalidate("Mapa carregado");
			UpscalerManager.InvalidateBackend("Mapa carregado");
		}

		private static void OnLevelExited()
		{
			VisibilityBudgetManager.Clear();
			PerformanceSettingsCache.Invalidate("Mapa encerrado");
		}

		private Camera cameraComponent;
		private static readonly bool disableAllRequested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceDisableAll");
	}
}
