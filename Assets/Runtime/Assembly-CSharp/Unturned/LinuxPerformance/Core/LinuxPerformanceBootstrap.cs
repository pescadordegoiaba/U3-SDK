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
		// A seleção explícita de FSR2 nas opções deve poder executar o caminho
		// experimental. has_fsr2 continua falso até validação completa, mas isso
		// não pode tornar a opção do usuário permanentemente impossível.
		public static bool IsFsr2ExplicitlyRequested => !disableAllRequested
			&& (forceFsr2Requested || GraphicsSettings.LinuxUpscalerMode == ELinuxUpscalerMode.Fsr2);
		public static bool IsFsr2DiagnosticForced => IsFsr2ExplicitlyRequested;

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
			ApplyTemporalDebugCommandLine();
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			bool explicitSmokeRequested = ShouldRunVulkanSmoke(commandLineArgs);
			bool delayedFsr2SmokeRequested = IsFsr2DiagnosticForced && !explicitSmokeRequested;
			if ((explicitSmokeRequested || delayedFsr2SmokeRequested)
				&& SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
			{
				bool canRunSmoke = true;
				if (delayedFsr2SmokeRequested)
				{
					// Evita compute/readback durante ModuleHook, Glazier e LoadingUI.
					float smokeDeadline = Time.realtimeSinceStartup + 180.0f;
					while ((!Level.isLoaded || Player.LocalPlayer == null || LoadingUI.isBlocked)
						&& Time.realtimeSinceStartup < smokeDeadline)
						yield return null;
					canRunSmoke = Level.isLoaded && Player.LocalPlayer != null && !LoadingUI.isBlocked;
					if (!canRunSmoke)
						UnturnedLog.warn("Linux Performance: gameplay não ficou pronto para smoke FSR2");
				}

				if (canRunSmoke)
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
			}

			bool captureStartup = HasCommandLineArg("-LinuxPerformanceCapture");
			bool captureGameplay = HasCommandLineArg("-LinuxPerformanceCaptureGameplay");
			if (!captureStartup && !captureGameplay)
				yield break;

			if (captureGameplay)
			{
				float deadline = Time.realtimeSinceStartup + 180.0f;
				while ((!Level.isLoaded || Player.LocalPlayer == null || LoadingUI.isBlocked) && Time.realtimeSinceStartup < deadline)
					yield return null;
				if (!Level.isLoaded || Player.LocalPlayer == null || LoadingUI.isBlocked)
				{
					UnturnedLog.error("Linux Performance: timeout aguardando gameplay para captura");
					yield break;
				}
			}
			yield return new WaitForSecondsRealtime(captureGameplay ? 5.0f : 10.0f);
			string captureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Benchmark", "Captures"));
			Directory.CreateDirectory(captureDirectory);
			string capturePath = Path.Combine(captureDirectory, "linux-performance-capture.png");
			ScreenCapture.CaptureScreenshot(capturePath);
			UnturnedLog.info("Captura Linux Performance solicitada por linha de comando: {0}", capturePath);
			yield return new WaitForSecondsRealtime(2.0f);
			Application.Quit(0);
		}

		private static void ApplyTemporalDebugCommandLine()
		{
			string[] args = Environment.GetCommandLineArgs();
			const string prefix = "-LinuxPerformanceTemporalDebug=";
			for (int index = 0; index < args.Length; ++index)
			{
				if (!args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					continue;
				string value = args[index].Substring(prefix.Length);
				if (Enum.TryParse(value, true, out ETemporalDebugView view))
				{
					TemporalDebugViews.ActiveView = view;
					UnturnedLog.info("Linux Performance: debug temporal {0} ativado por linha de comando", view);
				}
				else
				{
					UnturnedLog.warn("Linux Performance: debug temporal desconhecido: {0}", value);
				}
				return;
			}
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
			else if (forceFsr2Requested)
			{
				GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Fsr2;
				GraphicsSettings.LinuxFsrQualityPreset = EFsrQualityPreset.Quality;
				GraphicsSettings.LinuxDynamicResolution = false;
				GraphicsSettings.LinuxMotionAdaptiveResolution = false;
				UnturnedLog.info("Linux Performance: tentativa diagnóstica FSR 2 Quality forçada por linha de comando; capability permanece desativada até validação visual");
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

		internal static void ApplyCommandLineOverrides(ref PerformanceSettings settings)
		{
			if (disableAllRequested || forceNativeRequested)
			{
				settings.UpscalerMode = ELinuxUpscalerMode.Off;
				settings.DynamicResolution = false;
				settings.MotionAdaptiveResolution = false;
				settings.LowLatencyMode = false;
				settings.CullingProfile = ELinuxCullingProfile.Original;
				settings.CasEnabled = false;
				settings.CacaoEnabled = false;
				settings.SssrExperimental = false;
				settings.DebugOverlay = false;
				return;
			}
			if (forceFsr1Requested)
			{
				settings.UpscalerMode = ELinuxUpscalerMode.Fsr1;
				settings.FsrQualityPreset = EFsrQualityPreset.Quality;
				settings.RcasSharpness = 0.2f;
				settings.DynamicResolution = false;
				settings.MotionAdaptiveResolution = false;
			}
			else if (forceFsr2Requested)
			{
				settings.UpscalerMode = ELinuxUpscalerMode.Fsr2;
				settings.FsrQualityPreset = EFsrQualityPreset.Quality;
				settings.DynamicResolution = false;
				settings.MotionAdaptiveResolution = false;
			}
			if (disableDynamicResolutionRequested)
				settings.DynamicResolution = false;
			if (debugOverlayRequested)
				settings.DebugOverlay = true;
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
		private static readonly bool forceNativeRequested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceForceNative");
		private static readonly bool forceFsr1Requested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceForceFsr1");
		private static readonly bool forceFsr2Requested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceForceFsr2");
		private static readonly bool disableDynamicResolutionRequested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceDisableDynamicResolution");
		private static readonly bool debugOverlayRequested = HasCommandLineArg(Environment.GetCommandLineArgs(), "-LinuxPerformanceDebugOverlay");
	}
}
