////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using SDG.Unturned;
using SDG.Unturned.LinuxPerformance;
using System;
using UnityEngine;

namespace SDG.Framework.Rendering
{
	public delegate void GLRenderHandler();

	public class GLRenderer : MonoBehaviour
	{
		public static event GLRenderHandler render;
		public static event GLRenderHandler OnGameRender;

		private Camera cachedCamera;
		private TemporalCameraController temporalController;

		private void Awake()
		{
			cachedCamera = GetComponent<Camera>();
			temporalController = GetComponent<TemporalCameraController>();
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			RenderTexture previousActive = RenderTexture.active;
			bool isLowResolutionPath = false;
			try
			{
				if (LinuxPerformanceBootstrap.IsDisableAllRequested)
				{
					Graphics.Blit(source, destination);
					return;
				}

			bool isMainCamera = cachedCamera == MainCamera.instance;
			RenderTexture presentationTarget = null;
			isLowResolutionPath = isMainCamera && LowResolutionWorldRenderer.TryGetPresentationTarget(cachedCamera, source, out presentationTarget);
			if (cachedCamera == MainCamera.instance)
			{
				int outputWidth = isLowResolutionPath && LowResolutionWorldRenderer.Instance != null
					? LowResolutionWorldRenderer.Instance.OutputWidth
					: (destination != null ? destination.width : Screen.width);
				int outputHeight = isLowResolutionPath && LowResolutionWorldRenderer.Instance != null
					? LowResolutionWorldRenderer.Instance.OutputHeight
					: (destination != null ? destination.height : Screen.height);
				PerformanceSettingsCache.NotifyResolutionChanged(outputWidth, outputHeight);
				if (!isLowResolutionPath && LinuxPerformanceRuntime.IsNativeFastPath)
				{
					Graphics.Blit(source, destination);
					return;
				}
			}

			RenderTexture finalTarget = isLowResolutionPath ? presentationTarget : destination;
			TemporalFrameContext temporalFrame = default(TemporalFrameContext);
			bool hasTemporalFrame = false;
			if (isLowResolutionPath && presentationTarget != null && temporalController != null && LowResolutionWorldRenderer.Instance != null)
			{
				LowResolutionWorldRenderer lowResolutionRenderer = LowResolutionWorldRenderer.Instance;
				RenderTexture motionVectors = lowResolutionRenderer.CaptureMotionVectors();
				hasTemporalFrame = temporalController.TryPopulateFrameResources(source, lowResolutionRenderer.SceneDepthLowRes, motionVectors, lowResolutionRenderer.ReactiveMaskLowRes, presentationTarget, out temporalFrame);
				if (hasTemporalFrame)
					TemporalInputCollector.Publish(temporalFrame);
			}
			else
			{
				TemporalInputCollector.Clear();
			}
			// Blit must always be called.
			if (finalTarget == null)
				finalTarget = destination;
			if (!isMainCamera || !UpscalerManager.Render(source, finalTarget))
			{
				Graphics.Blit(source, finalTarget);
			}
			if (hasTemporalFrame)
				TemporalDebugViews.Render(temporalFrame, finalTarget);

			bool shouldRenderAny = false;
			bool shouldInvokeRenderEvent = false;
			bool shouldInvokeGameRenderEvent = false;
			bool shouldRenderGizmos = false;

			if (Level.isEditor)
			{
				shouldInvokeRenderEvent = render != null;

				if (EditorUI.window == null || !EditorUI.window.isEnabled)
				{
					shouldInvokeRenderEvent = false;
				}

				shouldRenderAny |= shouldInvokeRenderEvent;
			}
			else
			{
				shouldInvokeGameRenderEvent = OnGameRender != null;

				if (PlayerUI.window == null || !PlayerUI.window.isEnabled)
				{
					shouldInvokeGameRenderEvent = false;
				}

				shouldRenderAny |= shouldInvokeGameRenderEvent;
			}

			shouldRenderGizmos = RuntimeGizmos.Get().HasQueuedElements;
			shouldRenderAny |= shouldRenderGizmos;

			if (shouldRenderAny)
			{
				RenderTexture.active = finalTarget;

				if (shouldInvokeRenderEvent)
				{
					GL.PushMatrix();
					try
					{
						render();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GL.PopMatrix();
				}

				if (shouldInvokeGameRenderEvent)
				{
					GL.PushMatrix();
					try
					{
						OnGameRender();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GL.PopMatrix();
				}

				if (shouldRenderGizmos)
				{
					GL.PushMatrix();
					try
					{
						RuntimeGizmos.Get().Render();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GL.PopMatrix();
				}

				RenderTexture.active = null;
			}

			if (isLowResolutionPath)
			{
				// Graphics.Blit(..., null) pode voltar para os buffers customizados se
				// a câmera ainda estiver ligada ao SetTargetBuffers. Restaurar antes evita
				// feedback, tela dividida e frame antigo sobreposto.
				LowResolutionWorldRenderer.CompleteFrame(cachedCamera);
				if (finalTarget != null && finalTarget.IsCreated()
					&& LowResolutionWorldRenderer.Instance != null
					&& finalTarget.width == LowResolutionWorldRenderer.Instance.OutputWidth
					&& finalTarget.height == LowResolutionWorldRenderer.Instance.OutputHeight)
				{
					if (finalTarget != destination)
						Graphics.Blit(finalTarget, destination);
				}
				else
				{
					Graphics.Blit(source, destination);
				}
			}
			}
			catch (Exception exception)
			{
				if (!loggedRenderFailure)
				{
					loggedRenderFailure = true;
					UnturnedLog.exception(exception, "GLRenderer caught render failure; writing native fallback and restoring camera state:");
				}
				Graphics.Blit(source, destination);
			}
			finally
			{
				LowResolutionWorldRenderer.CompleteFrame(cachedCamera);
				RenderTexture.active = previousActive;
			}
		}

		private void OnDisable() => LowResolutionWorldRenderer.CompleteFrame(cachedCamera);
		private void OnDestroy() => LowResolutionWorldRenderer.CompleteFrame(cachedCamera);
		private static bool loggedRenderFailure;
	}
}
