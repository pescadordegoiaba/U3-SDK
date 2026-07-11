////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public enum ETemporalDebugView
	{
		None,
		Depth,
		MotionVectorsRgb,
		MotionVectorMagnitude,
		MissingMotionVectors,
		Disocclusion,
		ReactiveMask,
		CompositionMask,
		HistoryReset,
		Jitter,
	}

	public static class TemporalDebugViews
	{
		public static ETemporalDebugView ActiveView { get; set; }

		public static bool Render(in TemporalFrameContext frame, RenderTexture destination)
		{
			if (ActiveView == ETemporalDebugView.None || destination == null)
				return false;
			EnsureMaterial();
			if (material == null)
				return false;

			Texture source;
			int mode;
			switch (ActiveView)
			{
				case ETemporalDebugView.Depth:
					source = frame.Depth;
					mode = 1;
					break;
				case ETemporalDebugView.MotionVectorsRgb:
					source = frame.MotionVectors;
					mode = 2;
					break;
				case ETemporalDebugView.MotionVectorMagnitude:
					source = frame.MotionVectors;
					mode = 3;
					break;
				case ETemporalDebugView.ReactiveMask:
					source = frame.ReactiveMask;
					mode = 4;
					break;
				case ETemporalDebugView.Jitter:
					source = frame.Color;
					mode = 5;
					break;
				default:
					return false;
			}
			if (source == null)
				return false;
			material.SetInt(debugModeId, mode);
			material.SetVector(jitterId, new Vector4(frame.Jitter.x, frame.Jitter.y, frame.RenderWidth, frame.RenderHeight));
			material.SetVector(nearFarId, new Vector4(frame.NearPlane, frame.FarPlane, 0.0f, 0.0f));
			Graphics.Blit(source, destination, material, 0);
			return true;
		}

		public static void Release()
		{
			if (material == null)
				return;
			Object.Destroy(material);
			material = null;
		}

		private static void EnsureMaterial()
		{
			if (material != null)
				return;
			Shader shader = Shader.Find("Hidden/Unturned/LinuxPerformance/TemporalDebug");
			if (shader == null)
				return;
			material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
		}

		private static Material material;
		private static readonly int debugModeId = Shader.PropertyToID("_DebugMode");
		private static readonly int jitterId = Shader.PropertyToID("_TemporalJitter");
		private static readonly int nearFarId = Shader.PropertyToID("_TemporalNearFar");
	}
}
