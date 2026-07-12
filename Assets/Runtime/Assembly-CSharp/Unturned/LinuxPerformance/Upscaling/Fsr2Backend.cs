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
			if (!capabilities.NativePluginHasVulkanBackend || !capabilities.NativePluginHasFsr2)
			{
				StateReason = "Plugin nativo carregado, mas classificado como plugin de capacidades: não expõe backend Vulkan/FSR 2 funcional";
				return false;
			}
			if (MotionVectorValidator.State != ELinuxFeatureState.Available)
			{
				StateReason = MotionVectorValidator.StateReason;
				return false;
			}

			State = ELinuxFeatureState.Available;
			StateReason = "FSR 2 Vulkan e inputs temporais validados";
			return true;
		}

		public bool Initialize(in TemporalBackendDescription description)
		{
			if (!description.MotionVectorsValidated)
			{
				StateReason = MotionVectorValidator.StateReason;
				return false;
			}
			bool created = FidelityFxFsr2Native.TryCreate(description);
			State = created ? ELinuxFeatureState.Available : ELinuxFeatureState.Error;
			StateReason = created ? "Contexto AMD FidelityFX FSR2 criado" : FidelityFxFsr2Native.LastError;
			return created;
		}
		public void PrepareCamera(Camera camera, ref TemporalFrameContext frame) { }
		public bool Dispatch(ref TemporalFrameContext frame)
		{
			if (!FidelityFxFsr2Native.Dispatch(ref frame))
			{
				FailFatal(FidelityFxFsr2Native.LastError);
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
		public void Release() => FidelityFxFsr2Native.Release();
		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry)
		{
			if (!TemporalInputCollector.HasCurrentFrame)
				return false;
			TemporalFrameContext frame = TemporalInputCollector.CurrentFrame;
			frame.Color = source;
			frame.Output = destination;
			return Dispatch(ref frame);
		}

		private void FailFatal(string reason)
		{
			State = ELinuxFeatureState.Error;
			StateReason = reason;
			FidelityFxFsr2Native.Release();
			UpscalerManager.InvalidateBackend("Falha fatal FSR2: " + reason);
		}
	}
}
