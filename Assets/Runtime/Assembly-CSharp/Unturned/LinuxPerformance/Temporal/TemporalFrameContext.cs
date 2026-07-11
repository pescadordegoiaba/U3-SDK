////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public enum TemporalResetReason
	{
		Unknown,
		BackendChanged,
		CameraChanged,
		CameraCut,
		ResolutionChanged,
		RenderScaleChanged,
		FieldOfViewChanged,
		AbnormalDeltaTime,
		DeviceChanged,
		Manual,
	}

	public struct TemporalBackendDescription
	{
		public int RenderWidth;
		public int RenderHeight;
		public int OutputWidth;
		public int OutputHeight;
		public bool Hdr;
		public bool InvertedDepth;
		public bool MotionVectorsValidated;
		public LinuxGraphicsCapabilities GraphicsCapabilities;
	}

	public struct TemporalFrameContext
	{
		public Camera Camera;
		public uint FrameIndex;
		public RenderTexture Color;
		public RenderTexture OpaqueOnlyColor;
		public RenderTexture Depth;
		public RenderTexture MotionVectors;
		public RenderTexture ReactiveMask;
		public RenderTexture CompositionMask;
		public RenderTexture Output;
		public Matrix4x4 View;
		public Matrix4x4 Projection;
		public Matrix4x4 NonJitteredProjection;
		public Matrix4x4 PreviousViewProjection;
		public Vector2 Jitter;
		public Vector2 MotionVectorScale;
		public int RenderWidth;
		public int RenderHeight;
		public int OutputWidth;
		public int OutputHeight;
		public float DeltaTimeMilliseconds;
		public float NearPlane;
		public float FarPlane;
		public float VerticalFovRadians;
		public float PreExposure;
		public bool ResetHistory;
		public bool InvertedDepth;
		public bool Hdr;
	}
}
