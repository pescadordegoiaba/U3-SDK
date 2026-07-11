////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class TemporalInputCollector
	{
		public static TemporalFrameContext CurrentFrame { get; private set; }
		public static bool HasCurrentFrame { get; private set; }

		public static void Publish(in TemporalFrameContext frame)
		{
			CurrentFrame = frame;
			HasCurrentFrame = true;
		}

		public static void Clear()
		{
			CurrentFrame = default(TemporalFrameContext);
			HasCurrentFrame = false;
		}

		public static bool IsReadyForTemporalUpscaling(in TemporalFrameContext frame, out string reason)
		{
			if (frame.Camera == null)
			{
				reason = "Câmera temporal ausente";
				return false;
			}
			if (frame.RenderWidth <= 0 || frame.RenderHeight <= 0 || frame.OutputWidth <= 0 || frame.OutputHeight <= 0)
			{
				reason = "Dimensões temporais inválidas";
				return false;
			}
			if (frame.Color == null || frame.Output == null)
			{
				reason = "Color ou output temporal ausente";
				return false;
			}
			if (frame.Color.width != frame.RenderWidth || frame.Color.height != frame.RenderHeight || frame.Output.width != frame.OutputWidth || frame.Output.height != frame.OutputHeight)
			{
				reason = "Dimensões declaradas não correspondem às texturas temporais reais";
				return false;
			}
			if (frame.Depth == null || frame.MotionVectors == null)
			{
				reason = "Depth ou motion vectors ainda não foram capturados para o backend nativo";
				return false;
			}

			reason = "Entradas temporais prontas";
			return true;
		}
	}
}
