////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class Fsr31UpscalerBackend : IUpscalerBackend, ITemporalUpscalerBackend
	{
		public string DisplayName => "FSR 3.1 Upscaling";
		public ELinuxFeatureState State { get; private set; } = ELinuxFeatureState.Unsupported;
		public string StateReason { get; private set; } = "FSR 3.1 Upscaling requer integração oficial Vulkan/Linux validada; Frame Generation permanece separado e desligado";
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
				StateReason = "FSR 3.1 Upscaling nativo está disponível apenas no caminho Vulkan/Linux";
				return false;
			}
			if (!capabilities.NativePluginHasVulkanBackend || !capabilities.NativePluginHasFsr31Upscaling)
			{
				StateReason = "Plugin nativo carregado, mas classificado como plugin de capacidades: não expõe backend Vulkan/FSR 3.1 funcional";
				return false;
			}

			StateReason = "FSR 3.1 bloqueado: o bridge Vulkan/FidelityFX ainda não executou dispatch temporal validado";
			return false;
		}

		public bool Initialize(in TemporalBackendDescription description) => false;
		public void PrepareCamera(Camera camera, ref TemporalFrameContext frame) { }
		public bool Dispatch(ref TemporalFrameContext frame) => false;
		public void ResetHistory(TemporalResetReason reason) { }
		public void ResetHistory() => ResetHistory(TemporalResetReason.Manual);
		public void Resize(int renderWidth, int renderHeight, int outputWidth, int outputHeight) { }
		public void Release() { }
		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry) => false;
	}
}
