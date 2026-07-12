////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class Fsr2Backend : IUpscalerBackend, ITemporalUpscalerBackend
	{
		public string DisplayName => "FSR 2";
		public ELinuxFeatureState State { get; private set; } = ELinuxFeatureState.Unsupported;
		public string StateReason { get; private set; } = "FSR 2 requer backend temporal Vulkan nativo com color, depth, motion vectors e jitter reais";
		public bool IsTemporal => true;

		public bool IsAvailable(in LinuxGraphicsCapabilities capabilities)
		{
			if (fatalDiagnosticFailure)
			{
				StateReason = "FSR 2 diagnóstico desativado após falha fatal: " + fatalDiagnosticReason;
				return false;
			}
			if (!capabilities.NativePluginLoaded)
			{
				StateReason = capabilities.NativePluginStatus;
				return false;
			}
			if (!capabilities.IsVulkan)
			{
				StateReason = "FSR 2 nativo está disponível apenas no caminho Vulkan/Linux";
				return false;
			}
			bool diagnostic = LinuxPerformanceBootstrap.IsFsr2DiagnosticForced;
			if (!capabilities.NativePluginHasVulkanBackend || (!capabilities.NativePluginHasFsr2 && !diagnostic))
			{
				StateReason = "Plugin nativo carregado, mas classificado como plugin de capacidades: não expõe backend Vulkan/FSR 2 funcional";
				return false;
			}
			if (MotionVectorValidator.State != ELinuxFeatureState.Available && !diagnostic)
			{
				StateReason = MotionVectorValidator.StateReason;
				return false;
			}

			State = ELinuxFeatureState.Available;
			StateReason = diagnostic
				? "FSR 2 Vulkan em tentativa diagnóstica; capability permanece falsa"
				: "FSR 2 Vulkan e inputs temporais validados";
			return true;
		}

		public bool Initialize(in TemporalBackendDescription description)
		{
			if (!description.MotionVectorsValidated && !LinuxPerformanceBootstrap.IsFsr2DiagnosticForced)
			{
				StateReason = MotionVectorValidator.StateReason;
				return false;
			}
			bool created = FidelityFxFsr2Native.TryCreate(description);
			string reason = created ? "Contexto AMD FidelityFX FSR2 criado" : FidelityFxFsr2Native.LastError;
			State = created
				? ELinuxFeatureState.Available
				: (IsTransientInitializationFailure(reason) ? ELinuxFeatureState.Disabled : ELinuxFeatureState.Error);
			StateReason = reason;
			return created;
		}
		public void PrepareCamera(Camera camera, ref TemporalFrameContext frame) { }
		public bool Dispatch(ref TemporalFrameContext frame)
		{
			return DispatchInternal(ref frame, null);
		}

		private bool DispatchInternal(ref TemporalFrameContext frame, RenderTexture presentationTarget)
		{
			if (!TemporalInputCollector.IsReadyForTemporalUpscaling(frame, out string readinessReason))
			{
				StateReason = readinessReason;
				return false;
			}
			bool submitted = presentationTarget == null
				? FidelityFxFsr2Native.Dispatch(ref frame)
				: FidelityFxFsr2Native.DispatchAndPresent(ref frame, presentationTarget);
			if (!submitted)
			{
				string reason = FidelityFxFsr2Native.LastError;
				if (IsTransientDispatchFailure(reason))
				{
					StateReason = "Fallback temporário FSR2: " + reason;
					return false;
				}
				FailFatal(reason);
				return false;
			}
			return true;
		}
		public void ResetHistory(TemporalResetReason reason) => FidelityFxFsr2Native.ResetHistory();
		public void ResetHistory() => ResetHistory(TemporalResetReason.Manual);
		public void Resize(int renderWidth, int renderHeight, int outputWidth, int outputHeight)
		{
			if (FidelityFxFsr2Native.IsCreated && !FidelityFxFsr2Native.Resize(renderWidth, renderHeight, outputWidth, outputHeight, false, SystemInfo.usesReversedZBuffer))
				FailFatal(FidelityFxFsr2Native.LastError);
		}
		public void Release()
		{
			FidelityFxFsr2Native.Release();
			fatalDiagnosticFailure = false;
			fatalDiagnosticReason = null;
		}
		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry)
		{
			if (!TemporalInputCollector.HasCurrentFrame)
				return false;
			TemporalFrameContext frame = TemporalInputCollector.CurrentFrame;
			LowResolutionWorldRenderer lowResolution = LowResolutionWorldRenderer.Instance;
			RenderTexture nativeOutput = lowResolution != null ? lowResolution.Fsr2NativeOutput : null;
			if (nativeOutput == null || !nativeOutput.IsCreated() || destination == null || !destination.IsCreated()
				|| nativeOutput.width != destination.width || nativeOutput.height != destination.height || nativeOutput == destination)
			{
				StateReason = "Output compute/apresentação FSR2 inválido ou não separado";
				return false;
			}
			frame.Color = source;
			frame.Output = nativeOutput;
			if (!FidelityFxFsr2Native.IsCreated)
			{
				TemporalBackendDescription description = new TemporalBackendDescription()
				{
					RenderWidth = source.width,
					RenderHeight = source.height,
					OutputWidth = destination.width,
					OutputHeight = destination.height,
					Hdr = frame.Hdr,
					InvertedDepth = frame.InvertedDepth,
					MotionVectorsValidated = MotionVectorValidator.State == ELinuxFeatureState.Available || LinuxPerformanceBootstrap.IsFsr2DiagnosticForced,
					GraphicsCapabilities = UpscalerManager.Graphics,
				};
				if (!Initialize(description))
				{
					string initializationReason = StateReason;
					if (IsTransientInitializationFailure(initializationReason))
					{
						// O bootstrap valida o smoke depois que o gameplay fica pronto.
						// Até lá usamos o fallback do frame atual sem bloquear o FSR2
						// permanentemente para o restante do processo.
						State = ELinuxFeatureState.Disabled;
						StateReason = "FSR 2 aguardando pré-condição: " + initializationReason;
						return false;
					}
					FailFatal(initializationReason);
					return false;
				}
			}
			return DispatchInternal(ref frame, destination);
		}

		private static bool IsTransientInitializationFailure(string reason)
		{
			if (string.IsNullOrEmpty(reason))
				return false;
			return reason.IndexOf("Compute smoke Vulkan ainda não validado", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("smoke Vulkan", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("motion vectors", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("entradas temporais", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("bridge Vulkan", System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsTransientDispatchFailure(string reason)
		{
			if (string.IsNullOrEmpty(reason))
				return false;
			return reason.IndexOf("Ring FSR2 sem slot livre", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("Depth ou motion vectors", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("GetNativeTexturePtr retornou", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| reason.IndexOf("ainda não foram capturados", System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void FailFatal(string reason)
		{
			State = ELinuxFeatureState.Error;
			StateReason = reason;
			if (LinuxPerformanceBootstrap.IsFsr2DiagnosticForced)
			{
				fatalDiagnosticFailure = true;
				fatalDiagnosticReason = reason;
			}
			FidelityFxFsr2Native.Release();
			UpscalerManager.InvalidateBackend("Falha fatal FSR2: " + reason);
		}

		private bool fatalDiagnosticFailure;
		private string fatalDiagnosticReason;
	}
}
