////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class TemporalCameraController : MonoBehaviour
	{
		public TemporalFrameContext CurrentFrame => currentFrame;
		public bool IsTemporalActive => UpscalerManager.ActiveBackend != null && UpscalerManager.ActiveBackend.IsTemporal;

		public void RequestReset(TemporalResetReason reason)
		{
			pendingReset = true;
			pendingResetReason = reason;
			UpscalerManager.ResetHistory(reason.ToString());
		}

		private void Awake()
		{
			cameraComponent = GetComponent<Camera>();
			previousFieldOfView = cameraComponent != null ? cameraComponent.fieldOfView : 0.0f;
			if (cameraComponent != null)
			{
				nonJitteredProjection = cameraComponent.projectionMatrix;
				previousViewProjection = GL.GetGPUProjectionMatrix(nonJitteredProjection, true) * cameraComponent.worldToCameraMatrix;
			}
			else
			{
				nonJitteredProjection = Matrix4x4.identity;
				previousViewProjection = Matrix4x4.identity;
			}
			MainCamera.instanceChanged += OnMainCameraInstanceChanged;
		}

		private void OnDestroy()
		{
			MainCamera.instanceChanged -= OnMainCameraInstanceChanged;
		}

		private void OnPreCull()
		{
			didApplyJitterThisFrame = false;
			if (cameraComponent == null || !IsTemporalActive)
				return;

			cameraComponent.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
			originalProjection = cameraComponent.projectionMatrix;
			nonJitteredProjection = originalProjection;

			PerformanceSettings settings = PerformanceSettings.FromGraphicsSettings();
			int renderWidth = Mathf.Max(1, Mathf.RoundToInt(Screen.width * settings.GetPresetScale()));
			int renderHeight = Mathf.Max(1, Mathf.RoundToInt(Screen.height * settings.GetPresetScale()));
			Vector2 jitter = TemporalJitter.GetHalton23(frameIndex, renderWidth, renderHeight);
			Matrix4x4 jitteredProjection = nonJitteredProjection;
			jitteredProjection.m02 += jitter.x * 2.0f;
			jitteredProjection.m12 += jitter.y * 2.0f;
			cameraComponent.nonJitteredProjectionMatrix = nonJitteredProjection;
			cameraComponent.projectionMatrix = jitteredProjection;
			didApplyJitterThisFrame = true;

			bool reset = pendingReset || Mathf.Abs(cameraComponent.fieldOfView - previousFieldOfView) > 0.05f || Time.unscaledDeltaTime > 0.5f;
			currentFrame = new TemporalFrameContext()
			{
				Camera = cameraComponent,
				FrameIndex = frameIndex,
				View = cameraComponent.worldToCameraMatrix,
				Projection = jitteredProjection,
				NonJitteredProjection = nonJitteredProjection,
				PreviousViewProjection = previousViewProjection,
				Jitter = jitter,
				MotionVectorScale = new Vector2(renderWidth, renderHeight),
				RenderWidth = renderWidth,
				RenderHeight = renderHeight,
				OutputWidth = Screen.width,
				OutputHeight = Screen.height,
				DeltaTimeMilliseconds = Time.unscaledDeltaTime * 1000.0f,
				NearPlane = cameraComponent.nearClipPlane,
				FarPlane = cameraComponent.farClipPlane,
				VerticalFovRadians = cameraComponent.fieldOfView * Mathf.Deg2Rad,
				PreExposure = 1.0f,
				ResetHistory = reset,
				InvertedDepth = SystemInfo.usesReversedZBuffer,
				Hdr = cameraComponent.allowHDR,
			};
		}

		private void OnPostRender()
		{
			if (cameraComponent == null)
				return;
			if (!didApplyJitterThisFrame || !IsTemporalActive)
				return;

			if (cameraComponent.projectionMatrix != nonJitteredProjection)
				cameraComponent.projectionMatrix = nonJitteredProjection;

			previousViewProjection = GL.GetGPUProjectionMatrix(nonJitteredProjection, true) * cameraComponent.worldToCameraMatrix;
			previousFieldOfView = cameraComponent.fieldOfView;
			pendingReset = false;
			++frameIndex;
			didApplyJitterThisFrame = false;
		}

		private void OnMainCameraInstanceChanged()
		{
			RequestReset(TemporalResetReason.CameraChanged);
		}

		private Camera cameraComponent;
		private TemporalFrameContext currentFrame;
		private Matrix4x4 originalProjection;
		private Matrix4x4 nonJitteredProjection;
		private Matrix4x4 previousViewProjection;
		private float previousFieldOfView;
		private uint frameIndex;
		private bool pendingReset = true;
		private bool didApplyJitterThisFrame;
		private TemporalResetReason pendingResetReason = TemporalResetReason.Unknown;
	}
}
