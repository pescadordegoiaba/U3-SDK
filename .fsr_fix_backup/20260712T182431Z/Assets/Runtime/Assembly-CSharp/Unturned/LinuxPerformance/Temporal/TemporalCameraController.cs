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

		public bool TryPopulateFrameResources(RenderTexture color, RenderTexture depth, RenderTexture motionVectors, RenderTexture reactiveMask, RenderTexture output, out TemporalFrameContext frame)
		{
			frame = currentFrame;
			if (!IsTemporalActive || cameraComponent == null || color == null || output == null)
				return false;

			int renderWidth = color.width;
			int renderHeight = color.height;
			int outputWidth = output.width;
			int outputHeight = output.height;
			if ((previousRenderWidth > 0 && (previousRenderWidth != renderWidth || previousRenderHeight != renderHeight)))
				RequestReset(TemporalResetReason.RenderScaleChanged);
			if ((previousOutputWidth > 0 && (previousOutputWidth != outputWidth || previousOutputHeight != outputHeight)))
				RequestReset(TemporalResetReason.ResolutionChanged);

			currentFrame.Color = color;
			currentFrame.Depth = depth;
			currentFrame.MotionVectors = motionVectors;
			currentFrame.ReactiveMask = reactiveMask;
			currentFrame.CompositionMask = null;
			currentFrame.Output = output;
			currentFrame.RenderWidth = renderWidth;
			currentFrame.RenderHeight = renderHeight;
			currentFrame.OutputWidth = outputWidth;
			currentFrame.OutputHeight = outputHeight;
			currentFrame.MotionVectorScale = new Vector2(renderWidth, SystemInfo.graphicsUVStartsAtTop ? -renderHeight : renderHeight);
			currentFrame.ResetHistory |= pendingReset;
			previousRenderWidth = renderWidth;
			previousRenderHeight = renderHeight;
			previousOutputWidth = outputWidth;
			previousOutputHeight = outputHeight;
			frame = currentFrame;
			return true;
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
			Level.onLevelLoaded += OnLevelLoaded;
			Player.onPlayerCreated += OnPlayerCreated;
			BindLocalPlayer(Player.LocalPlayer);
		}

		private void OnDestroy()
		{
			MainCamera.instanceChanged -= OnMainCameraInstanceChanged;
			Level.onLevelLoaded -= OnLevelLoaded;
			Player.onPlayerCreated -= OnPlayerCreated;
			UnbindLocalPlayer();
		}

		private void OnPreCull()
		{
			didApplyJitterThisFrame = false;
			bool isTemporalActive = IsTemporalActive;
			if (isTemporalActive != wasTemporalActive)
			{
				wasTemporalActive = isTemporalActive;
				RequestReset(TemporalResetReason.BackendChanged);
			}
			if (cameraComponent == null || !isTemporalActive)
				return;

			cameraComponent.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
			originalProjection = cameraComponent.projectionMatrix;
			nonJitteredProjection = originalProjection;

			PerformanceSettings settings = PerformanceSettingsCache.Current;
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
			PerformanceSettingsCache.NotifyMainCameraChanged(MainCamera.instance);
			RequestReset(TemporalResetReason.CameraChanged);
		}

		private void OnLevelLoaded(int level)
		{
			RequestReset(TemporalResetReason.MapChanged);
		}

		private void OnPlayerCreated(Player player)
		{
			if (player != null && player.channel != null && player.channel.IsLocalPlayer)
				BindLocalPlayer(player);
		}

		private void BindLocalPlayer(Player player)
		{
			if (boundLocalPlayer == player)
				return;
			UnbindLocalPlayer();
			if (player == null)
				return;
			boundLocalPlayer = player;
			boundLocalPlayer.onPlayerTeleported += OnPlayerTeleported;
			if (boundLocalPlayer.life != null)
				boundLocalPlayer.life.onLifeUpdated += OnLifeUpdated;
		}

		private void UnbindLocalPlayer()
		{
			if (boundLocalPlayer == null)
				return;
			boundLocalPlayer.onPlayerTeleported -= OnPlayerTeleported;
			if (boundLocalPlayer.life != null)
				boundLocalPlayer.life.onLifeUpdated -= OnLifeUpdated;
			boundLocalPlayer = null;
		}

		private void OnPlayerTeleported(Player player, Vector3 position)
		{
			RequestReset(TemporalResetReason.Teleport);
		}

		private void OnLifeUpdated(bool isDead)
		{
			RequestReset(isDead ? TemporalResetReason.CameraCut : TemporalResetReason.Respawn);
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
		private bool wasTemporalActive;
		private int previousRenderWidth;
		private int previousRenderHeight;
		private int previousOutputWidth;
		private int previousOutputHeight;
		private Player boundLocalPlayer;
		private TemporalResetReason pendingResetReason = TemporalResetReason.Unknown;
	}
}
