////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using Unity.Profiling;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class Fsr1Backend : IUpscalerBackend
	{
		public string DisplayName => "FSR 1";
		public ELinuxFeatureState State { get; private set; } = ELinuxFeatureState.Disabled;
		public string StateReason { get; private set; } = "Não inicializado";
		public bool IsTemporal => false;

		public bool IsAvailable(in LinuxGraphicsCapabilities capabilities)
		{
			if (!(capabilities.IsVulkan || capabilities.IsOpenGLCore))
			{
				State = ELinuxFeatureState.Unsupported;
				StateReason = "FSR 1 requer Vulkan ou OpenGL Core";
				return false;
			}

			EnsureMaterial();
			bool available = material != null;
			State = available ? ELinuxFeatureState.Available : ELinuxFeatureState.Unsupported;
			StateReason = available ? "FSR 1 disponível" : "Shader Hidden/Unturned/LinuxPerformance/FSR1 não encontrado";
			return available;
		}

		public void ResetHistory()
		{
		}

		public static bool ShouldRenderForResolution(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight, bool forceDiagnostic)
		{
			if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0)
				return false;
			return forceDiagnostic || sourceWidth != destinationWidth || sourceHeight != destinationHeight;
		}

		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry)
		{
			EnsureMaterial();
			if (material == null || source == null || destination == null || source.width <= 0 || source.height <= 0 || destination.width <= 0 || destination.height <= 0)
				return false;
			if (material.passCount < 2)
				return false;
			if (!ShouldRenderForResolution(source.width, source.height, destination.width, destination.height, IsForceDiagnosticEnabled()))
			{
				LogNativeResolutionSkipOnce(source, destination);
				return false;
			}

			material.SetVector(sourceSizeId, new Vector4(source.width, source.height, 1.0f / source.width, 1.0f / source.height));
			material.SetVector(outputSizeId, new Vector4(destination.width, destination.height, 1.0f / destination.width, 1.0f / destination.height));
			material.SetFloat(sharpnessId, settings.RcasSharpness);

			RenderTextureDescriptor descriptor = source.descriptor;
			descriptor.depthBufferBits = 0;
			descriptor.width = destination.width;
			descriptor.height = destination.height;
			descriptor.msaaSamples = 1;
			descriptor.useMipMap = false;
			descriptor.autoGenerateMips = false;
			RenderTexture intermediate = null;
			bool oldSrgbWrite = GL.sRGBWrite;
			try
			{
				intermediate = RenderTargetPool.Get(descriptor, "FSR1_EASU");
				using (easuMarker.Auto())
				{
					SetSrgbWriteForTarget(intermediate);
					Graphics.Blit(source, intermediate, material, 0);
				}

				if (settings.RcasSharpness > 0.001f)
				{
					material.SetVector(sourceSizeId, new Vector4(intermediate.width, intermediate.height, 1.0f / intermediate.width, 1.0f / intermediate.height));
					using (rcasMarker.Auto())
					{
						SetSrgbWriteForTarget(destination);
						Graphics.Blit(intermediate, destination, material, 1);
					}
				}
				else
				{
					SetSrgbWriteForTarget(destination);
					Graphics.Blit(intermediate, destination);
				}
			}
			finally
			{
				GL.sRGBWrite = oldSrgbWrite;
				if (intermediate != null)
					RenderTargetPool.Release(intermediate);
			}
			return true;
		}

		public void Release()
		{
			if (material != null)
			{
				UnityEngine.Object.Destroy(material);
				material = null;
			}
		}

		private void EnsureMaterial()
		{
			if (material != null)
				return;

			Shader shader = Shader.Find("Hidden/Unturned/LinuxPerformance/FSR1");
			if (shader != null)
			{
				material = new Material(shader);
				material.hideFlags = HideFlags.HideAndDontSave;
			}
		}

		private static bool IsForceDiagnosticEnabled()
		{
			if (forceDiagnostic.HasValue)
				return forceDiagnostic.Value;

			string[] args = Environment.GetCommandLineArgs();
			for (int i = 0; i < args.Length; ++i)
			{
				if (string.Equals(args[i], "-LinuxPerformanceForceFsr1Diagnostic", StringComparison.OrdinalIgnoreCase))
				{
					forceDiagnostic = true;
					return true;
				}
			}

			forceDiagnostic = false;
			return false;
		}

		private static void SetSrgbWriteForTarget(RenderTexture target)
		{
			GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear && target != null && target.sRGB;
		}

		private static void LogNativeResolutionSkipOnce(RenderTexture source, RenderTexture destination)
		{
			if (loggedNativeResolutionSkip)
				return;

			loggedNativeResolutionSkip = true;
			UnturnedLog.info("Linux Performance: FSR 1 ignorado em resolução nativa ({0}x{1} -> {2}x{3}); usando blit nativo para preservar brilho e desempenho",
				source.width,
				source.height,
				destination.width,
				destination.height);
		}

		private Material material;
		private static readonly int sourceSizeId = Shader.PropertyToID("_SourceSize");
		private static readonly int outputSizeId = Shader.PropertyToID("_OutputSize");
		private static readonly int sharpnessId = Shader.PropertyToID("_Sharpness");
		private static readonly ProfilerMarker easuMarker = new ProfilerMarker("LinuxPerformance.FSR1.EASU");
		private static readonly ProfilerMarker rcasMarker = new ProfilerMarker("LinuxPerformance.FSR1.RCAS");
		private static bool? forceDiagnostic;
		private static bool loggedNativeResolutionSkip;
	}
}
