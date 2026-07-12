////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	public sealed class LowResolutionWorldRenderer : MonoBehaviour
	{
		public static LowResolutionWorldRenderer Instance { get; private set; }

		public RenderTexture SceneColorLowRes => sceneColorLowRes;
		public RenderTexture SceneDepthLowRes => sceneDepthLowRes;
		public RenderTexture UpscaledColor => upscaledColor;
		public RenderTexture Fsr2NativeOutput => fsr2NativeOutput;
		public RenderTexture MotionVectorsLowRes => motionVectorsLowRes;
		public RenderTexture ReactiveMaskLowRes => reactiveMaskLowRes;
		public bool IsActiveThisFrame { get; private set; }
		public int RenderWidth { get; private set; }
		public int RenderHeight { get; private set; }
		public int OutputWidth { get; private set; }
		public int OutputHeight { get; private set; }

		public static void CalculateDimensions(int outputWidth, int outputHeight, float scale, out int renderWidth, out int renderHeight)
		{
			outputWidth = Mathf.Max(1, outputWidth);
			outputHeight = Mathf.Max(1, outputHeight);
			scale = Mathf.Clamp(scale, 0.333f, 1.0f);
			renderWidth = Mathf.Clamp(Mathf.RoundToInt(outputWidth * scale), 1, outputWidth);
			if (renderWidth < outputWidth && (renderWidth & 1) != 0)
				renderWidth = Mathf.Min(outputWidth, renderWidth + 1);
			renderHeight = Mathf.Clamp(Mathf.RoundToInt(outputHeight * scale), 1, outputHeight);
			if (renderHeight < outputHeight && (renderHeight & 1) != 0)
				renderHeight = Mathf.Min(outputHeight, renderHeight + 1);
		}

		public static bool TryGetPresentationTarget(Camera camera, RenderTexture source, out RenderTexture target)
		{
			LowResolutionWorldRenderer instance = Instance;
			if (instance == null || camera == null || camera != instance.cameraComponent || !instance.IsActiveThisFrame)
			{
				target = null;
				return false;
			}

			target = instance.upscaledColor;
			if (source == null || target == null || !target.IsCreated()
				|| source.width != instance.RenderWidth || source.height != instance.RenderHeight
				|| target.width != instance.OutputWidth || target.height != instance.OutputHeight)
			{
				instance.LogInvalidSourceOnce(source);
				target = null;
				return false;
			}
			return true;
		}

		public static void CompleteFrame(Camera camera)
		{
			if (Instance != null && camera == Instance.cameraComponent)
				Instance.ResetCameraTarget();
		}

		public RenderTexture CaptureMotionVectors()
		{
			if (!IsActiveThisFrame || motionVectorsLowRes == null)
				return null;
			Texture source = Shader.GetGlobalTexture(cameraMotionVectorsTextureId);
			if (source == null)
			{
				MotionVectorValidator.MarkUnavailable("_CameraMotionVectorsTexture não foi fornecida pela câmera Built-in");
				return null;
			}
			Graphics.Blit(source, motionVectorsLowRes);
			return motionVectorsLowRes;
		}

		public static void ReleaseAll()
		{
			if (Instance != null)
				Instance.ReleaseTargets();
		}

		private void Awake()
		{
			cameraComponent = GetComponent<Camera>();
			temporalController = GetComponent<TemporalCameraController>();
		}

		private void OnEnable()
		{
			if (cameraComponent == null)
				cameraComponent = GetComponent<Camera>();
			Instance = this;
			MainCamera.instanceChanged += OnMainCameraChanged;
			Level.onLevelExited += OnLevelExited;
			Application.quitting += OnApplicationQuitting;
		}

		private void OnPreCull()
		{
			IsActiveThisFrame = false;
			PerformanceSettings settings = PerformanceSettingsCache.Current;
			float scale = ResolveScale(settings);
			if (!CanUseLowResolutionGameplay(cameraComponent, settings, scale))
			{
				ResetCameraTarget();
				return;
			}
			// O viewport real pode divergir de Screen (ex.: 1280x986 em janela).
			OutputWidth = Mathf.Max(1, cameraComponent.pixelWidth > 0 ? cameraComponent.pixelWidth : Screen.width);
			OutputHeight = Mathf.Max(1, cameraComponent.pixelHeight > 0 ? cameraComponent.pixelHeight : Screen.height);
			CalculateDimensions(OutputWidth, OutputHeight, scale, out int renderWidth, out int renderHeight);
			RenderWidth = renderWidth;
			RenderHeight = renderHeight;
			if (RenderWidth >= OutputWidth && RenderHeight >= OutputHeight)
			{
				ResetCameraTarget();
				return;
			}

			RenderTextureFormat colorFormat = cameraComponent.allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
			if (!EnsureTargets(RenderWidth, RenderHeight, OutputWidth, OutputHeight, colorFormat))
			{
				ResetCameraTarget();
				return;
			}

			previousTargetTexture = cameraComponent.targetTexture;
			previousPixelRect = cameraComponent.pixelRect;
			didOverrideCameraBuffers = true;
			// SetTargetBuffers não informa à câmera que o framebuffer ficou menor.
			// Sem ajustar o viewport, Vulkan recorta a renderização e o upscale
			// estica uma imagem parcialmente atualizada, produzindo a divisão horizontal.
			cameraComponent.pixelRect = new Rect(0.0f, 0.0f, RenderWidth, RenderHeight);
			cameraComponent.SetTargetBuffers(sceneColorLowRes.colorBuffer, sceneDepthLowRes.depthBuffer);
			IsActiveThisFrame = true;
			PerformanceSettingsCache.NotifyResolutionChanged(OutputWidth, OutputHeight);
		}

		private void OnDisable()
		{
			MainCamera.instanceChanged -= OnMainCameraChanged;
			Level.onLevelExited -= OnLevelExited;
			Application.quitting -= OnApplicationQuitting;
			ResetCameraTarget();
			ReleaseTargets();
			if (Instance == this)
				Instance = null;
		}

		private void OnDestroy()
		{
			OnDisable();
		}

		private void OnPostRender()
		{
			ResetCameraTarget();
		}

		internal static bool MeetsLowResolutionGameplayRequirements(bool disableAll, bool levelLoaded, bool hasLocalPlayer,
			bool isMainCamera, bool cameraActive, bool backendAvailable, bool loadingBlocked, float scale)
		{
			return !disableAll && levelLoaded && hasLocalPlayer && isMainCamera && cameraActive && backendAvailable && !loadingBlocked && scale < 0.999f;
		}

		private bool CanUseLowResolutionGameplay(Camera camera, in PerformanceSettings settings, float scale)
		{
			bool backendAvailable = settings.UpscalerMode != ELinuxUpscalerMode.Off && UpscalerManager.ActiveBackend != null;
			return MeetsLowResolutionGameplayRequirements(
				LinuxPerformanceBootstrap.IsDisableAllRequested,
				Level.isLoaded,
				Player.LocalPlayer != null,
				camera != null && camera == MainCamera.instance && camera.cameraType == CameraType.Game,
				camera != null && camera.enabled && camera.gameObject.activeInHierarchy,
				backendAvailable,
				LoadingUI.isBlocked,
				scale);
		}

		private void OnMainCameraChanged() => ResetCameraTarget();
		private void OnLevelExited() => ResetCameraTarget();
		private void OnApplicationQuitting() => ResetCameraTarget();

		private static float ResolveScale(in PerformanceSettings settings)
		{
			if (settings.DynamicResolution)
				return DynamicResolutionController.CurrentScale;
			if (settings.UpscalerMode != ELinuxUpscalerMode.Off)
				return settings.GetPresetScale();
			return 1.0f;
		}

		private bool EnsureTargets(int renderWidth, int renderHeight, int outputWidth, int outputHeight, RenderTextureFormat colorFormat)
		{
			if (sceneColorLowRes != null && sceneDepthLowRes != null && upscaledColor != null && fsr2NativeOutput != null
				&& motionVectorsLowRes != null && reactiveMaskLowRes != null
				&& sceneColorLowRes.IsCreated() && sceneDepthLowRes.IsCreated() && upscaledColor.IsCreated() && fsr2NativeOutput.IsCreated()
				&& motionVectorsLowRes.IsCreated() && reactiveMaskLowRes.IsCreated()
				&& sceneColorLowRes.width == renderWidth && sceneColorLowRes.height == renderHeight && sceneColorLowRes.format == colorFormat
				&& sceneDepthLowRes.width == renderWidth && sceneDepthLowRes.height == renderHeight
				&& upscaledColor.width == outputWidth && upscaledColor.height == outputHeight && upscaledColor.format == colorFormat
				&& fsr2NativeOutput.width == outputWidth && fsr2NativeOutput.height == outputHeight && fsr2NativeOutput.format == colorFormat
				&& motionVectorsLowRes.width == renderWidth && motionVectorsLowRes.height == renderHeight
				&& reactiveMaskLowRes.width == renderWidth && reactiveMaskLowRes.height == renderHeight)
				return true;

			bool outputChanged = upscaledColor != null && (upscaledColor.width != outputWidth || upscaledColor.height != outputHeight);
			bool renderScaleChanged = sceneColorLowRes != null && (sceneColorLowRes.width != renderWidth || sceneColorLowRes.height != renderHeight);
			ReleaseTargets();
			// Inputs do FSR2 são somente sampled. Apenas o output nativo precisa de STORAGE/UAV.
			sceneColorLowRes = CreateTarget(renderWidth, renderHeight, 0, colorFormat, false, "LinuxPerformance.SceneColorLowRes");
			sceneDepthLowRes = CreateDepthTarget(renderWidth, renderHeight, "LinuxPerformance.SceneDepthLowRes");
			upscaledColor = CreateTarget(outputWidth, outputHeight, 0, colorFormat, false, "LinuxPerformance.UpscaledColor");
			fsr2NativeOutput = CreateTarget(outputWidth, outputHeight, 0, colorFormat, true, "LinuxPerformance.Fsr2NativeOutput");
			RenderTextureFormat motionFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : RenderTextureFormat.ARGBHalf;
			RenderTextureFormat maskFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
			motionVectorsLowRes = CreateTarget(renderWidth, renderHeight, 0, motionFormat, false, "LinuxPerformance.MotionVectorsLowRes");
			reactiveMaskLowRes = CreateTarget(renderWidth, renderHeight, 0, maskFormat, false, "LinuxPerformance.ReactiveMaskLowRes");
			bool valid = sceneColorLowRes != null && sceneColorLowRes.IsCreated() && sceneDepthLowRes != null && sceneDepthLowRes.IsCreated()
				&& upscaledColor != null && upscaledColor.IsCreated() && fsr2NativeOutput != null && fsr2NativeOutput.IsCreated()
				&& motionVectorsLowRes != null && motionVectorsLowRes.IsCreated() && reactiveMaskLowRes != null && reactiveMaskLowRes.IsCreated();
			if (valid)
			{
				RenderTexture previous = RenderTexture.active;
				ClearColorTarget(reactiveMaskLowRes, Color.black);
				ClearColorTarget(upscaledColor, Color.black);
				ClearColorTarget(fsr2NativeOutput, Color.black);
				RenderTexture.active = previous;
				if (temporalController != null)
					temporalController.RequestReset(outputChanged ? TemporalResetReason.ResolutionChanged : (renderScaleChanged ? TemporalResetReason.RenderScaleChanged : TemporalResetReason.BackendChanged));
				UnturnedLog.info("Linux Performance low-res targets: source={0}x{1}, destination={2}x{3}, formato={4}", renderWidth, renderHeight, outputWidth, outputHeight, colorFormat);
			}
			else
			{
				UnturnedLog.warn("Linux Performance: falha ao criar render targets low-res; usando backbuffer nativo");
				ReleaseTargets();
			}
			return valid;
		}

		private static RenderTexture CreateTarget(int width, int height, int depthBits, RenderTextureFormat format, bool randomWrite, string name)
		{
			RenderTexture target = new RenderTexture(width, height, depthBits, format, RenderTextureReadWrite.Default)
			{
				name = name,
				useMipMap = false,
				autoGenerateMips = false,
				enableRandomWrite = randomWrite,
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
			};
			if (target.Create())
				return target;
			Destroy(target);
			return null;
		}

		private static void ClearColorTarget(RenderTexture target, Color color)
		{
			if (target == null || !target.IsCreated())
				return;
			RenderTexture.active = target;
			GL.Clear(false, true, color);
		}

		private static RenderTexture CreateDepthTarget(int width, int height, string name)
		{
			RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height)
			{
				graphicsFormat = GraphicsFormat.None,
				depthStencilFormat = GraphicsFormat.D32_SFloat,
				msaaSamples = 1,
				useMipMap = false,
				autoGenerateMips = false,
				enableRandomWrite = false,
			};
			RenderTexture target = new RenderTexture(descriptor)
			{
				name = name,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};
			if (target.Create())
				return target;
			Destroy(target);
			return null;
		}

		private void ResetCameraTarget()
		{
			if (didOverrideCameraBuffers && cameraComponent != null)
			{
				// targetTexture abandona os buffers separados e pixelRect restaura
				// o viewport nativo antes da composição da UI/backbuffer.
				cameraComponent.targetTexture = previousTargetTexture;
				cameraComponent.pixelRect = previousPixelRect;
			}
			didOverrideCameraBuffers = false;
			previousTargetTexture = null;
			IsActiveThisFrame = false;
		}

		private void ReleaseTargets()
		{
			DestroyTarget(ref sceneColorLowRes);
			DestroyTarget(ref sceneDepthLowRes);
			DestroyTarget(ref upscaledColor);
			DestroyTarget(ref fsr2NativeOutput);
			DestroyTarget(ref motionVectorsLowRes);
			DestroyTarget(ref reactiveMaskLowRes);
		}

		private static void DestroyTarget(ref RenderTexture target)
		{
			if (target == null)
				return;
			target.Release();
			Destroy(target);
			target = null;
		}

		private void LogInvalidSourceOnce(RenderTexture source)
		{
			if (loggedInvalidSource)
				return;
			loggedInvalidSource = true;
			UnturnedLog.warn("Linux Performance: dimensões reais do source não correspondem ao target low-res ({0}x{1}, esperado {2}x{3}); apresentando fallback nativo",
				source != null ? source.width : 0,
				source != null ? source.height : 0,
				RenderWidth,
				RenderHeight);
		}

		private Camera cameraComponent;
		private RenderTexture sceneColorLowRes;
		private RenderTexture sceneDepthLowRes;
		private RenderTexture upscaledColor;
		private RenderTexture fsr2NativeOutput;
		private RenderTexture motionVectorsLowRes;
		private RenderTexture reactiveMaskLowRes;
		private TemporalCameraController temporalController;
		private bool loggedInvalidSource;
		private bool didOverrideCameraBuffers;
		private RenderTexture previousTargetTexture;
		private Rect previousPixelRect;
		private static readonly int cameraMotionVectorsTextureId = Shader.PropertyToID("_CameraMotionVectorsTexture");
	}
}
