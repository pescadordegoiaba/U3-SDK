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

		private void Awake()
		{
			cachedCamera = GetComponent<Camera>();
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			bool isMainCamera = cachedCamera == MainCamera.instance;
			RenderTexture presentationTarget = null;
			bool isLowResolutionPath = isMainCamera && LowResolutionWorldRenderer.TryGetPresentationTarget(cachedCamera, source, out presentationTarget);
			if (cachedCamera == MainCamera.instance)
			{
				int outputWidth = isLowResolutionPath && LowResolutionWorldRenderer.Instance != null ? LowResolutionWorldRenderer.Instance.OutputWidth : destination.width;
				int outputHeight = isLowResolutionPath && LowResolutionWorldRenderer.Instance != null ? LowResolutionWorldRenderer.Instance.OutputHeight : destination.height;
				PerformanceSettingsCache.NotifyResolutionChanged(outputWidth, outputHeight);
				if (!isLowResolutionPath && LinuxPerformanceRuntime.IsNativeFastPath)
				{
					Graphics.Blit(source, destination);
					return;
				}
			}

			RenderTexture finalTarget = isLowResolutionPath ? presentationTarget : destination;
			// Blit must always be called.
			if (!isMainCamera || finalTarget == null || !UpscalerManager.Render(source, finalTarget))
			{
				Graphics.Blit(source, finalTarget);
			}

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
				LowResolutionWorldRenderer.Present(finalTarget);
				LowResolutionWorldRenderer.CompleteFrame(cachedCamera);
			}
		}
	}
}
