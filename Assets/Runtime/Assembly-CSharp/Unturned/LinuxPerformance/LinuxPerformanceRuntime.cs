////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

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

	public interface IUpscalerBackend
	{
		string DisplayName { get; }
		ELinuxFeatureState State { get; }
		string StateReason { get; }
		bool IsTemporal { get; }
		bool IsAvailable(in LinuxGraphicsCapabilities capabilities);
		void ResetHistory();
		void Release();
		bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry);
	}

	public sealed class HardwareCapabilities
	{
		public string OperatingSystem;
		public string ProcessorType;
		public int ProcessorCount;
		public bool IsLinux;
		public bool IsX86_64;
		public bool IsLikelyI73770;
		public long SystemMemoryMb;

		public static HardwareCapabilities Detect()
		{
			HardwareCapabilities result = new HardwareCapabilities();
			result.OperatingSystem = SystemInfo.operatingSystem ?? string.Empty;
			result.ProcessorType = SystemInfo.processorType ?? string.Empty;
			result.ProcessorCount = SystemInfo.processorCount;
			result.SystemMemoryMb = SystemInfo.systemMemorySize;
			result.IsLinux = Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
			result.IsX86_64 = IntPtr.Size == 8;
			result.IsLikelyI73770 = result.ProcessorType.IndexOf("i7-3770", StringComparison.OrdinalIgnoreCase) >= 0;
			return result;
		}
	}

	public sealed class LinuxGraphicsCapabilities
	{
		public GraphicsDeviceType GraphicsDeviceType;
		public string GraphicsDeviceName;
		public string GraphicsDeviceVendor;
		public int GraphicsMemoryMb;
		public bool IsVulkan;
		public bool IsOpenGLCore;
		public bool IsAmd;
		public bool IsRx580OrPolaris;
		public bool SupportsComputeShaders;
		public bool SupportsDepth;
		public bool SupportsMotionVectors;
		public bool SupportsARGBHalf;
		public bool SupportsARGBFloat;
		public bool SupportsDefaultHDR;
		public bool NativePluginLoaded;
		public int NativePluginAbiVersion;
		public string NativePluginStatus;
		public string NativePluginBackend;
		public bool NativePluginHasVulkanBackend;
		public bool NativePluginHasFsr2;
		public bool NativePluginHasFsr31Upscaling;
		public bool NativePluginHasFrameGeneration;

		public static LinuxGraphicsCapabilities Detect()
		{
			LinuxGraphicsCapabilities result = new LinuxGraphicsCapabilities();
			result.GraphicsDeviceType = SystemInfo.graphicsDeviceType;
			result.GraphicsDeviceName = SystemInfo.graphicsDeviceName ?? string.Empty;
			result.GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor ?? string.Empty;
			result.GraphicsMemoryMb = SystemInfo.graphicsMemorySize;
			result.IsVulkan = result.GraphicsDeviceType == GraphicsDeviceType.Vulkan;
			result.IsOpenGLCore = result.GraphicsDeviceType == GraphicsDeviceType.OpenGLCore;
			result.IsAmd = result.GraphicsDeviceVendor.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0;
			result.IsRx580OrPolaris = result.GraphicsDeviceName.IndexOf("RX 580", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("POLARIS", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("Polaris", StringComparison.OrdinalIgnoreCase) >= 0;
			result.SupportsComputeShaders = SystemInfo.supportsComputeShaders;
			result.SupportsDepth = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
			result.SupportsMotionVectors = true; // Built-in pipeline has a motion-vector shader configured; per-camera use is validated by backends.
			result.SupportsARGBHalf = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
			result.SupportsARGBFloat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);
			result.SupportsDefaultHDR = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
			result.NativePluginLoaded = NativeRenderBackend.TryGetCapabilities(out NativeRenderBackend.PluginCapabilities pluginCapabilities, out result.NativePluginStatus);
			result.NativePluginAbiVersion = pluginCapabilities.AbiVersion;
			result.NativePluginBackend = pluginCapabilities.BackendName;
			result.NativePluginHasVulkanBackend = pluginCapabilities.HasVulkanBackend;
			result.NativePluginHasFsr2 = pluginCapabilities.HasFsr2;
			result.NativePluginHasFsr31Upscaling = pluginCapabilities.HasFsr31Upscaling;
			result.NativePluginHasFrameGeneration = pluginCapabilities.HasFrameGeneration;
			return result;
		}
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
			{
				CasEnabled = false;
			}
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

	public static class NativeRenderBackend
	{
		public const int ExpectedAbiVersion = 1;

		public struct PluginCapabilities
		{
			public int AbiVersion;
			public string BackendName;
			public bool HasVulkanBackend;
			public bool HasFsr2;
			public bool HasFsr31Upscaling;
			public bool HasFrameGeneration;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct NativePluginCapabilities
		{
			public int abi_version;
			public int has_vulkan_backend;
			public int has_fsr2;
			public int has_fsr31_upscaling;
			public int has_frame_generation;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string message;
		}

		public static bool TryGetAbiVersion(out int version, out string status)
		{
			version = 0;
			try
			{
				version = u3ffx_get_abi_version();
				if (version != ExpectedAbiVersion)
				{
					status = $"ABI do plugin incompatível: {version}, esperado {ExpectedAbiVersion}";
					return false;
				}

				status = "Plugin nativo Linux carregado";
				return true;
			}
			catch (DllNotFoundException)
			{
				status = "Plugin nativo Linux não encontrado; usando fallbacks do Unity";
				return false;
			}
			catch (EntryPointNotFoundException)
			{
				status = "Plugin nativo Linux sem entrada ABI esperada";
				return false;
			}
			catch (Exception e)
			{
				status = "Erro ao consultar plugin nativo Linux: " + e.Message;
				return false;
			}
		}

		public static bool TryGetCapabilities(out PluginCapabilities capabilities, out string status)
		{
			capabilities = default;
			if (!TryGetAbiVersion(out int version, out status))
				return false;

			try
			{
				NativePluginCapabilities nativeCapabilities;
				int ok = u3ffx_get_capabilities(out nativeCapabilities);
				capabilities.AbiVersion = nativeCapabilities.abi_version;
				capabilities.BackendName = nativeCapabilities.message ?? string.Empty;
				capabilities.HasVulkanBackend = nativeCapabilities.has_vulkan_backend != 0;
				capabilities.HasFsr2 = nativeCapabilities.has_fsr2 != 0;
				capabilities.HasFsr31Upscaling = nativeCapabilities.has_fsr31_upscaling != 0;
				capabilities.HasFrameGeneration = nativeCapabilities.has_frame_generation != 0;

				if (ok == 0)
				{
					status = "Plugin nativo Linux respondeu erro ao consultar capacidades";
					return false;
				}
				if (capabilities.AbiVersion != ExpectedAbiVersion)
				{
					status = $"ABI do plugin incompatível: {capabilities.AbiVersion}, esperado {ExpectedAbiVersion}";
					return false;
				}

				status = "Plugin nativo Linux carregado: " + capabilities.BackendName;
				return true;
			}
			catch (EntryPointNotFoundException)
			{
				status = "Plugin nativo Linux sem entrada de capacidades";
				capabilities.AbiVersion = version;
				return false;
			}
			catch (Exception e)
			{
				status = "Erro ao consultar capacidades do plugin nativo Linux: " + e.Message;
				capabilities.AbiVersion = version;
				return false;
			}
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_abi_version();

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_capabilities(out NativePluginCapabilities capabilities);
	}

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

		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry)
		{
			EnsureMaterial();
			if (material == null || source == null || destination == null)
				return false;

			material.SetVector(sourceSizeId, new Vector4(source.width, source.height, 1.0f / source.width, 1.0f / source.height));
			material.SetVector(outputSizeId, new Vector4(destination.width, destination.height, 1.0f / destination.width, 1.0f / destination.height));
			material.SetFloat(sharpnessId, settings.RcasSharpness);

			RenderTextureDescriptor descriptor = destination.descriptor;
			descriptor.depthBufferBits = 0;
			descriptor.width = destination.width;
			descriptor.height = destination.height;
			RenderTexture intermediate = null;
			try
			{
				intermediate = RenderTargetPool.Get(descriptor, "FSR1_EASU");
				using (easuMarker.Auto())
				{
					Graphics.Blit(source, intermediate, material, 0);
				}

				if (settings.RcasSharpness > 0.001f)
				{
					material.SetVector(sourceSizeId, new Vector4(intermediate.width, intermediate.height, 1.0f / intermediate.width, 1.0f / intermediate.height));
					using (rcasMarker.Auto())
					{
						Graphics.Blit(intermediate, destination, material, 1);
					}
				}
				else
				{
					Graphics.Blit(intermediate, destination);
				}
			}
			finally
			{
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

		private Material material;
		private static readonly int sourceSizeId = Shader.PropertyToID("_SourceSize");
		private static readonly int outputSizeId = Shader.PropertyToID("_OutputSize");
		private static readonly int sharpnessId = Shader.PropertyToID("_Sharpness");
		private static readonly ProfilerMarker easuMarker = new ProfilerMarker("LinuxPerformance.FSR1.EASU");
		private static readonly ProfilerMarker rcasMarker = new ProfilerMarker("LinuxPerformance.FSR1.RCAS");
	}

	public sealed class Fsr2Backend : IUpscalerBackend
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
			StateReason = "Backend FSR 2 temporal ainda não foi ativado neste build";
			return false;
		}
		public void ResetHistory() { }
		public void Release() { }
		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry) => false;
	}

	public sealed class Fsr31UpscalerBackend : IUpscalerBackend
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
			StateReason = "Backend FSR 3.1 temporal ainda não foi ativado neste build";
			return false;
		}
		public void ResetHistory() { }
		public void Release() { }
		public bool Render(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, in PerformanceTelemetry.FrameSnapshot telemetry) => false;
	}

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
			UnturnedLog.info("Linux performance capabilities: GPU={0}, API={1}, VRAM={2} MB, plugin={3}, backend={4}, fsr2={5}, fsr31={6}", Graphics.GraphicsDeviceName, Graphics.GraphicsDeviceType, Graphics.GraphicsMemoryMb, Graphics.NativePluginStatus, Graphics.NativePluginBackend, Graphics.NativePluginHasFsr2, Graphics.NativePluginHasFsr31Upscaling);
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
				if (!IsInitialized)
				{
					InitializeForCamera(MainCamera.instance);
				}

				PerformanceSettings settings = PerformanceSettings.FromGraphicsSettings();
				PerformanceTelemetry.FrameSnapshot snapshot = PerformanceTelemetry.CaptureFrameSnapshot();
				MotionAdaptiveResolutionController.Update(MainCamera.instance, ref settings);
				DynamicResolutionController.Apply(settings);
				LowLatencyController.Apply(settings);
				SelectBackend(settings);

				if (ActiveBackend == null || settings.UpscalerMode == ELinuxUpscalerMode.Off)
				{
					return false;
				}

				bool rendered = ActiveBackend.Render(source, destination, settings, snapshot);
				if (rendered)
				{
					PerformanceTelemetry.NotifyUpscalerRendered(source, destination, settings, ActiveBackend.DisplayName);
				}
				return rendered;
			}
		}

		public static void Release()
		{
			fsr1.Release();
			fsr2.Release();
			fsr31.Release();
			RenderTargetPool.Clear();
			ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);
			IsInitialized = false;
		}

		private static void SelectBackend(in PerformanceSettings settings)
		{
			IUpscalerBackend selected = null;
			string reason = "Upscaler desativado";

			if (settings.UpscalerMode == ELinuxUpscalerMode.Off)
			{
				selected = null;
			}
			else if (settings.UpscalerMode == ELinuxUpscalerMode.Fsr1)
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
			else
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

		private static readonly Fsr1Backend fsr1 = new Fsr1Backend();
		private static readonly Fsr2Backend fsr2 = new Fsr2Backend();
		private static readonly Fsr31UpscalerBackend fsr31 = new Fsr31UpscalerBackend();
		private static readonly ProfilerMarker renderHookMarker = new ProfilerMarker("LinuxPerformance.RenderHook");
	}

	public static class DynamicResolutionController
	{
		public static int InternalWidth { get; private set; }
		public static int InternalHeight { get; private set; }
		public static float CurrentScale { get; private set; } = 1.0f;

		public static void AttachCamera(Camera camera)
		{
			if (camera == null)
				return;
			camera.allowDynamicResolution = true;
		}

		public static void Apply(in PerformanceSettings settings)
		{
			using (marker.Auto())
			{
				float scale = settings.DynamicResolution || settings.UpscalerMode != ELinuxUpscalerMode.Off ? settings.GetPresetScale() : 1.0f;
				scale = Mathf.Clamp(scale, 0.333f, 1.0f);
				if (!Mathf.Approximately(scale, CurrentScale))
				{
					CurrentScale = scale;
					ScalableBufferManager.ResizeBuffers(scale, scale);
				}
				InternalWidth = Mathf.Max(1, Mathf.RoundToInt(Screen.width * CurrentScale));
				InternalHeight = Mathf.Max(1, Mathf.RoundToInt(Screen.height * CurrentScale));
			}
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.DynamicResolution");
	}

	public static class MotionAdaptiveResolutionController
	{
		public static float AngularVelocityDegrees { get; private set; }
		public static float TranslationalVelocity { get; private set; }
		public static float MotionScale { get; private set; } = 1.0f;

		public static void Update(Camera camera, ref PerformanceSettings settings)
		{
			if (!settings.MotionAdaptiveResolution || camera == null || MenuConfigurationUI.active || MenuDashboardUI.active)
			{
				MotionScale = Mathf.MoveTowards(MotionScale, 1.0f, Time.unscaledDeltaTime * 2.0f);
				return;
			}

			Transform transform = camera.transform;
			if (!hasPrevious)
			{
				previousPosition = transform.position;
				previousRotation = transform.rotation;
				hasPrevious = true;
				return;
			}

			float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
			AngularVelocityDegrees = Quaternion.Angle(previousRotation, transform.rotation) / dt;
			TranslationalVelocity = (transform.position - previousPosition).magnitude / dt;
			previousPosition = transform.position;
			previousRotation = transform.rotation;

			float target = (AngularVelocityDegrees > 140.0f || TranslationalVelocity > 8.0f) ? 0.82f : 1.0f;
			float speed = target < MotionScale ? 3.5f : 1.25f;
			MotionScale = Mathf.MoveTowards(MotionScale, target, Time.unscaledDeltaTime * speed);
			settings.CustomRenderScale = Mathf.Clamp(settings.GetPresetScale() * MotionScale, 0.333f, 1.0f);
			settings.FsrQualityPreset = EFsrQualityPreset.Custom;
		}

		private static bool hasPrevious;
		private static Vector3 previousPosition;
		private static Quaternion previousRotation;
	}

	public static class LowLatencyController
	{
		public static void Apply(in PerformanceSettings settings)
		{
			if (!settings.LowLatencyMode)
				return;

			if (!GraphicsSettings.buffer && GraphicsSettings.UseTargetFrameRate)
			{
				Application.targetFrameRate = Mathf.Max(15, GraphicsSettings.TargetFrameRate);
			}
		}
	}

	public static class MemoryBudgetManager
	{
		public static long ManagedMemoryBytes { get; private set; }
		public static long MonoUsedBytes { get; private set; }
		public static long TargetRssMb { get; private set; }

		public static void Apply(in PerformanceSettings settings)
		{
			ManagedMemoryBytes = GC.GetTotalMemory(false);
			MonoUsedBytes = Profiler.GetMonoUsedSizeLong();
			switch (settings.MemoryProfile)
			{
				case ELinuxMemoryProfile.MuitoBaixo:
					TargetRssMb = 3000;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 256;
					QualitySettings.streamingMipmapsMaxLevelReduction = 3;
					break;
				case ELinuxMemoryProfile.Baixo:
					TargetRssMb = 3300;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 384;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
				case ELinuxMemoryProfile.Equilibrado:
					TargetRssMb = 4096;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = 512;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
				default:
					TargetRssMb = SystemInfo.systemMemorySize <= 8192 ? 3300 : 4096;
					QualitySettings.streamingMipmapsActive = true;
					QualitySettings.streamingMipmapsMemoryBudget = SystemInfo.systemMemorySize <= 8192 ? 384 : 512;
					QualitySettings.streamingMipmapsMaxLevelReduction = 2;
					break;
			}
		}
	}

	public static class VisibilityBudgetManager
	{
		public static int TotalRenderers { get; private set; }
		public static int PotentiallyVisibleRenderers { get; private set; }

		public static void Apply(Camera camera, in PerformanceSettings settings)
		{
			using (marker.Auto())
			{
				if (camera == null || settings.CullingProfile == ELinuxCullingProfile.Original)
					return;

				float multiplier = settings.CullingProfile == ELinuxCullingProfile.Agressivo ? 0.82f : 0.92f;
				float[] baseDistances;
				int cameraId = camera.GetInstanceID();
				if (!baseLayerCullDistances.TryGetValue(cameraId, out baseDistances))
				{
					float[] current = camera.layerCullDistances;
					baseDistances = new float[32];
					if (current != null)
						Array.Copy(current, baseDistances, Mathf.Min(current.Length, baseDistances.Length));
					baseLayerCullDistances.Add(cameraId, baseDistances);
				}
				float[] distances = new float[32];
				for (int i = 0; i < distances.Length; ++i)
				{
					distances[i] = baseDistances[i] > 0.01f ? baseDistances[i] * multiplier : baseDistances[i];
				}
				camera.layerCullDistances = distances;
				camera.layerCullSpherical = true;
			}
		}

		public static void CaptureSceneMetrics()
		{
			using (marker.Auto())
			{
				Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
				TotalRenderers = renderers.Length;
				PotentiallyVisibleRenderers = 0;
				Plane[] planes = null;
				Camera camera = MainCamera.instance;
				if (camera != null)
					planes = GeometryUtility.CalculateFrustumPlanes(camera);
				for (int i = 0; i < renderers.Length; ++i)
				{
					if (renderers[i] != null && renderers[i].enabled && (planes == null || GeometryUtility.TestPlanesAABB(planes, renderers[i].bounds)))
						++PotentiallyVisibleRenderers;
				}
			}
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.Culling");
		private static readonly Dictionary<int, float[]> baseLayerCullDistances = new Dictionary<int, float[]>();
	}

	public static class RenderTargetPool
	{
		public static int ActiveCount => active.Count;
		public static int IdleCount => idle.Count;
		public static long EstimatedBytes { get; private set; }
		public static long BudgetBytes = 128L * 1024L * 1024L;

		public static RenderTexture Get(RenderTextureDescriptor descriptor, string debugName)
		{
			descriptor.msaaSamples = 1;
			descriptor.useMipMap = false;
			descriptor.autoGenerateMips = false;
			RenderTargetDescriptorKey key = new RenderTargetDescriptorKey(descriptor);
			Stack<RenderTexture> stack;
			if (idle.TryGetValue(key, out stack) && stack.Count > 0)
			{
				RenderTexture reused = stack.Pop();
				active.Add(reused, key);
				return reused;
			}

			RenderTexture rt = new RenderTexture(descriptor);
			rt.name = debugName;
			rt.Create();
			active.Add(rt, key);
			EstimatedBytes += key.EstimatedBytes;
			TrimIfNeeded();
			return rt;
		}

		public static void Release(RenderTexture rt)
		{
			if (rt == null)
				return;
			RenderTargetDescriptorKey key;
			if (!active.TryGetValue(rt, out key))
				return;
			active.Remove(rt);
			Stack<RenderTexture> stack;
			if (!idle.TryGetValue(key, out stack))
			{
				stack = new Stack<RenderTexture>();
				idle.Add(key, stack);
			}
			stack.Push(rt);
		}

		public static void Clear()
		{
			foreach (RenderTexture rt in active.Keys)
				Destroy(rt);
			active.Clear();
			foreach (Stack<RenderTexture> stack in idle.Values)
			{
				while (stack.Count > 0)
					Destroy(stack.Pop());
			}
			idle.Clear();
			EstimatedBytes = 0;
		}

		private static void TrimIfNeeded()
		{
			if (EstimatedBytes <= BudgetBytes)
				return;
			foreach (Stack<RenderTexture> stack in idle.Values)
			{
				while (stack.Count > 0 && EstimatedBytes > BudgetBytes)
				{
					RenderTexture rt = stack.Pop();
					EstimatedBytes -= new RenderTargetDescriptorKey(rt.descriptor).EstimatedBytes;
					Destroy(rt);
				}
			}
		}

		private static void Destroy(RenderTexture rt)
		{
			if (rt == null)
				return;
			rt.Release();
			UnityEngine.Object.Destroy(rt);
		}

		private static readonly Dictionary<RenderTexture, RenderTargetDescriptorKey> active = new Dictionary<RenderTexture, RenderTargetDescriptorKey>();
		private static readonly Dictionary<RenderTargetDescriptorKey, Stack<RenderTexture>> idle = new Dictionary<RenderTargetDescriptorKey, Stack<RenderTexture>>();

		private struct RenderTargetDescriptorKey : IEquatable<RenderTargetDescriptorKey>
		{
			public readonly int Width;
			public readonly int Height;
			public readonly RenderTextureFormat ColorFormat;
			public readonly int DepthBits;
			public readonly RenderTextureReadWrite ReadWrite;
			public readonly long EstimatedBytes;

			public RenderTargetDescriptorKey(RenderTextureDescriptor descriptor)
			{
				Width = descriptor.width;
				Height = descriptor.height;
				ColorFormat = descriptor.colorFormat;
				DepthBits = descriptor.depthBufferBits;
				ReadWrite = descriptor.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
				EstimatedBytes = (long)Width * Height * EstimateBytesPerPixel(ColorFormat, DepthBits);
			}

			public bool Equals(RenderTargetDescriptorKey other)
			{
				return Width == other.Width && Height == other.Height && ColorFormat == other.ColorFormat && DepthBits == other.DepthBits && ReadWrite == other.ReadWrite;
			}

			public override bool Equals(object obj)
			{
				return obj is RenderTargetDescriptorKey other && Equals(other);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = Width;
					hash = (hash * 397) ^ Height;
					hash = (hash * 397) ^ (int)ColorFormat;
					hash = (hash * 397) ^ DepthBits;
					hash = (hash * 397) ^ (int)ReadWrite;
					return hash;
				}
			}

			private static int EstimateBytesPerPixel(RenderTextureFormat format, int depthBits)
			{
				int colorBytes = format == RenderTextureFormat.ARGBHalf ? 8 : (format == RenderTextureFormat.ARGBFloat ? 16 : 4);
				return colorBytes + Mathf.Max(0, depthBits / 8);
			}
		}
	}

	public sealed class PerformanceTelemetry : MonoBehaviour
	{
		public struct FrameSnapshot
		{
			public int ScreenWidth;
			public int ScreenHeight;
			public int InternalWidth;
			public int InternalHeight;
			public float DeltaTime;
			public long ManagedMemoryBytes;
			public long MonoUsedBytes;
			public int RenderTargetPoolActive;
			public int RenderTargetPoolIdle;
		}

		public static FrameSnapshot LastFrame { get; private set; }
		public static string LastUpscalerName { get; private set; } = "Off";

		public static FrameSnapshot CaptureFrameSnapshot()
		{
			using (marker.Auto())
			{
				LastFrame = new FrameSnapshot()
				{
					ScreenWidth = Screen.width,
					ScreenHeight = Screen.height,
					InternalWidth = DynamicResolutionController.InternalWidth,
					InternalHeight = DynamicResolutionController.InternalHeight,
					DeltaTime = Time.unscaledDeltaTime,
					ManagedMemoryBytes = GC.GetTotalMemory(false),
					MonoUsedBytes = Profiler.GetMonoUsedSizeLong(),
					RenderTargetPoolActive = RenderTargetPool.ActiveCount,
					RenderTargetPoolIdle = RenderTargetPool.IdleCount,
				};
				return LastFrame;
			}
		}

		public static void NotifyUpscalerRendered(RenderTexture source, RenderTexture destination, in PerformanceSettings settings, string backendName)
		{
			using (marker.Auto())
			{
				LastUpscalerName = backendName;
			}
		}

		private void OnGUI()
		{
			if (!GraphicsSettings.LinuxDebugOverlay)
				return;
			GUI.Box(new Rect(8, 8, 430, 150), "Diagnóstico Linux");
			GUI.Label(new Rect(18, 34, 410, 22), $"Upscaler: {LastUpscalerName} ({UpscalerManager.LastSelectionReason})");
			GUI.Label(new Rect(18, 56, 410, 22), $"Resolução interna/final: {LastFrame.InternalWidth}x{LastFrame.InternalHeight} -> {LastFrame.ScreenWidth}x{LastFrame.ScreenHeight}");
			GUI.Label(new Rect(18, 78, 410, 22), $"Memória gerenciada: {LastFrame.ManagedMemoryBytes / (1024 * 1024)} MB; Mono: {LastFrame.MonoUsedBytes / (1024 * 1024)} MB");
			GUI.Label(new Rect(18, 100, 410, 22), $"Render targets: ativos {LastFrame.RenderTargetPoolActive}, ociosos {LastFrame.RenderTargetPoolIdle}");
			GUI.Label(new Rect(18, 122, 410, 22), $"Movimento câmera: {MotionAdaptiveResolutionController.AngularVelocityDegrees:0} deg/s, escala {DynamicResolutionController.CurrentScale:0.00}");
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.Telemetry");
	}

	public sealed class LinuxPerformanceBootstrap : MonoBehaviour
	{
		public static LinuxPerformanceBootstrap GetOrCreate(GameObject host)
		{
			LinuxPerformanceBootstrap bootstrap = host.GetComponent<LinuxPerformanceBootstrap>();
			if (bootstrap == null)
				bootstrap = host.AddComponent<LinuxPerformanceBootstrap>();
			if (host.GetComponent<PerformanceTelemetry>() == null)
				host.AddComponent<PerformanceTelemetry>();
			return bootstrap;
		}

		private void Awake()
		{
			ApplyCommandLineOverrides();
			UpscalerManager.InitializeForCamera(GetComponent<Camera>());
		}

		private IEnumerator Start()
		{
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

		private static void ApplyCommandLineOverrides()
		{
			if (HasCommandLineArg("-LinuxPerformanceForceFsr1"))
			{
				GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Fsr1;
				GraphicsSettings.LinuxFsrQualityPreset = EFsrQualityPreset.Quality;
				GraphicsSettings.LinuxRcasSharpness = 0.2f;
				UnturnedLog.info("Linux Performance: FSR 1 Quality forçado por linha de comando");
			}
			else if (HasCommandLineArg("-LinuxPerformanceForceNative"))
			{
				GraphicsSettings.LinuxUpscalerMode = ELinuxUpscalerMode.Off;
				UnturnedLog.info("Linux Performance: modo nativo forçado por linha de comando");
			}

			if (HasCommandLineArg("-LinuxPerformanceDebugOverlay"))
				GraphicsSettings.LinuxDebugOverlay = true;
		}

		private static bool HasCommandLineArg(string value)
		{
			string[] args = Environment.GetCommandLineArgs();
			for (int i = 0; i < args.Length; ++i)
			{
				if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private void OnDisable()
		{
			UpscalerManager.Release();
		}

		private void OnDestroy()
		{
			UpscalerManager.Release();
		}
	}
}
