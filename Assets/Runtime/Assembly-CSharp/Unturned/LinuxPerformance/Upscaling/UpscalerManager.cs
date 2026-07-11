////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Unity.Profiling;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class UpscalerManager
	{
		public static HardwareCapabilities Hardware { get; private set; }
		public static LinuxGraphicsCapabilities Graphics { get; private set; }
		public static IUpscalerBackend ActiveBackend { get; private set; }
		public static string LastSelectionReason { get; private set; } = "Não inicializado";
		public static bool IsInitialized { get; private set; }

		public static void InitializeForCamera(Camera camera)
		{
			if (camera == null)
				return;

			Hardware = HardwareCapabilities.Detect();
			Graphics = LinuxGraphicsCapabilities.Detect();
			DynamicResolutionController.AttachCamera(camera);
			LowLatencyController.Apply(PerformanceSettings.FromGraphicsSettings());
			MemoryBudgetManager.Apply(PerformanceSettings.FromGraphicsSettings());
			VisibilityBudgetManager.Apply(camera, PerformanceSettings.FromGraphicsSettings());
			IsInitialized = true;
			SelectBackend(PerformanceSettings.FromGraphicsSettings());
			UnturnedLog.info("Linux performance capabilities: GPU={0}, API={1}, VRAM={2} MB, plugin={3}, backend={4}, tipo={5}, fsr2={6}, fsr31={7}", Graphics.GraphicsDeviceName, Graphics.GraphicsDeviceType, Graphics.GraphicsMemoryMb, Graphics.NativePluginStatus, Graphics.NativePluginBackend, Graphics.NativePluginBackendKind, Graphics.NativePluginHasFsr2, Graphics.NativePluginHasFsr31Upscaling);
		}

		public static void ResetHistory(string reason)
		{
			ActiveBackend?.ResetHistory();
			LastSelectionReason = "Histórico reiniciado: " + reason;
		}

		public static bool Render(RenderTexture source, RenderTexture destination)
		{
			using (renderHookMarker.Auto())
			{
				if (!IsValidRenderTarget(source) || !IsValidRenderTarget(destination))
				{
					LogRenderFallbackOnce("Render target de origem ou destino inválido");
					return false;
				}

				if (!IsInitialized)
					InitializeForCamera(MainCamera.instance);

				PerformanceSettings settings = PerformanceSettings.FromGraphicsSettings();
				if (IsNativeFastPath(settings))
					return false;

				PerformanceTelemetry.FrameSnapshot snapshot = PerformanceTelemetry.CaptureFrameSnapshot();
				MotionAdaptiveResolutionController.Update(MainCamera.instance, ref settings);
				DynamicResolutionController.Apply(settings);
				LowLatencyController.Apply(settings);
				SelectBackend(settings);

				if (ActiveBackend == null || settings.UpscalerMode == ELinuxUpscalerMode.Off)
					return false;

				LogRenderStateOnce(source, destination, settings);

				bool rendered;
				try
				{
					rendered = ActiveBackend.Render(source, destination, settings, snapshot);
				}
				catch (System.Exception e)
				{
					LogRenderFallbackOnce("Exceção no backend " + ActiveBackend.DisplayName + ": " + e.Message);
					return false;
				}

				if (!rendered)
					LogRenderFallbackOnce("Backend " + ActiveBackend.DisplayName + " recusou o frame");

				if (rendered)
					PerformanceTelemetry.NotifyUpscalerRendered(source, destination, settings, ActiveBackend.DisplayName);
				return rendered;
			}
		}

		public static void Release()
		{
			fsr1.Release();
			fsr2.Release();
			fsr31.Release();
			RenderTargetPool.Clear();
			DynamicResolutionController.Reset();
			IsInitialized = false;
		}

		private static void SelectBackend(in PerformanceSettings settings)
		{
			IUpscalerBackend selected = null;
			string reason = "Upscaler desativado";

			if (settings.UpscalerMode == ELinuxUpscalerMode.Fsr1)
			{
				selected = fsr1.IsAvailable(Graphics) ? fsr1 : null;
				reason = selected != null ? "FSR 1 selecionado" : fsr1.StateReason;
			}
			else if (settings.UpscalerMode == ELinuxUpscalerMode.Fsr2)
			{
				selected = fsr2.IsAvailable(Graphics) ? fsr2 : (fsr1.IsAvailable(Graphics) ? fsr1 : null);
				reason = selected == fsr2 ? "FSR 2 selecionado" : "Fallback para FSR 1: " + fsr2.StateReason;
			}
			else if (settings.UpscalerMode == ELinuxUpscalerMode.Fsr31Upscaling)
			{
				selected = fsr31.IsAvailable(Graphics) ? fsr31 : (fsr2.IsAvailable(Graphics) ? fsr2 : (fsr1.IsAvailable(Graphics) ? fsr1 : null));
				reason = selected == fsr31 ? "FSR 3.1 selecionado" : "Fallback temporal indisponível: " + fsr31.StateReason;
			}
			else if (settings.UpscalerMode == ELinuxUpscalerMode.Auto)
			{
				if (fsr31.IsAvailable(Graphics))
				{
					selected = fsr31;
					reason = "Auto: FSR 3.1";
				}
				else if (fsr2.IsAvailable(Graphics))
				{
					selected = fsr2;
					reason = "Auto: FSR 2";
				}
				else if (fsr1.IsAvailable(Graphics))
				{
					selected = fsr1;
					reason = "Auto: FSR 1";
				}
				else
				{
					reason = "Auto: resolução nativa";
				}
			}

			if (!ReferenceEquals(ActiveBackend, selected))
			{
				ActiveBackend = selected;
				ActiveBackend?.ResetHistory();
			}
			LastSelectionReason = reason;
		}

		internal static bool IsNativeFastPath(in PerformanceSettings settings)
		{
			return settings.UpscalerMode == ELinuxUpscalerMode.Off
				&& !settings.DynamicResolution
				&& !settings.MotionAdaptiveResolution
				&& !settings.LowLatencyMode
				&& settings.CullingProfile == ELinuxCullingProfile.Original
				&& !settings.CasEnabled
				&& !settings.CacaoEnabled
				&& !settings.SssrExperimental
				&& !settings.DebugOverlay;
		}

		private static bool IsValidRenderTarget(RenderTexture renderTexture)
		{
			return renderTexture != null && renderTexture.width > 0 && renderTexture.height > 0;
		}

		private static void LogRenderStateOnce(RenderTexture source, RenderTexture destination, in PerformanceSettings settings)
		{
			if (loggedRenderState)
				return;

			loggedRenderState = true;
			UnturnedLog.info("Linux Performance render: modo={0}, backend={1}, motivo={2}, resolução dinâmica={3}, escala={4:0.###}, source={5}x{6}, destination={7}x{8}, api={9}",
				settings.UpscalerMode,
				ActiveBackend != null ? ActiveBackend.DisplayName : "nenhum",
				LastSelectionReason,
				settings.DynamicResolution,
				DynamicResolutionController.CurrentScale,
				source.width,
				source.height,
				destination.width,
				destination.height,
				Graphics != null ? Graphics.GraphicsDeviceType.ToString() : SystemInfo.graphicsDeviceType.ToString());
		}

		private static void LogRenderFallbackOnce(string reason)
		{
			if (loggedFallback)
				return;

			loggedFallback = true;
			UnturnedLog.warn("Linux Performance: fallback para renderização nativa. Motivo: {0}", reason);
		}

		private static readonly Fsr1Backend fsr1 = new Fsr1Backend();
		private static readonly Fsr2Backend fsr2 = new Fsr2Backend();
		private static readonly Fsr31UpscalerBackend fsr31 = new Fsr31UpscalerBackend();
		private static readonly ProfilerMarker renderHookMarker = new ProfilerMarker("LinuxPerformance.RenderHook");
		private static bool loggedRenderState;
		private static bool loggedFallback;
	}
}
