////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	internal static class FidelityFxFsr2Native
	{
		private const uint AbiVersion = 4;
		private const uint DispatchCommand = 2;
		private const int RingSize = 8;
		private const int Pending = 0;

		[StructLayout(LayoutKind.Sequential)]
		private struct CreateDescription
		{
			public uint struct_size;
			public uint abi_version;
			public uint max_render_width;
			public uint max_render_height;
			public uint display_width;
			public uint display_height;
			public uint flags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct DispatchParameters
		{
			public uint struct_size;
			public uint abi_version;
			public uint command;
			public uint frame_slot;
			public uint generation;
			public IntPtr color_texture;
			public IntPtr depth_texture;
			public IntPtr motion_vectors_texture;
			public IntPtr output_texture;
			public IntPtr reactive_texture;
			public IntPtr composition_texture;
			public uint render_width;
			public uint render_height;
			public uint output_width;
			public uint output_height;
			public float jitter_x;
			public float jitter_y;
			public float motion_vector_scale_x;
			public float motion_vector_scale_y;
			public float delta_time_ms;
			public float camera_near;
			public float camera_far;
			public float vertical_fov_radians;
			public float pre_exposure;
			public uint reset_history;
			public uint hdr;
			public uint inverted_depth;
			public uint frame_index;
			public int status;
		}

		public static bool IsCreated { get; private set; }
		public static string LastError { get; private set; } = "FSR2 nativo não inicializado";

		public static bool TryCreate(in TemporalBackendDescription description)
		{
			if (!description.MotionVectorsValidated || !NativeVulkanBridge.IsSmokeValidated)
			{
				LastError = !description.MotionVectorsValidated ? MotionVectorValidator.StateReason : "Compute smoke Vulkan ainda não validado";
				return false;
			}
			Release();
			CreateDescription native = BuildDescription(description.RenderWidth, description.RenderHeight, description.OutputWidth, description.OutputHeight,
				description.Hdr, description.InvertedDepth);
			try
			{
				if (u3ffx_fsr2_create(ref native) == 0)
				{
					LastError = GetLastError();
					return false;
				}
				AllocateRing();
				maxRenderWidth = description.RenderWidth;
				maxRenderHeight = description.RenderHeight;
				outputWidth = description.OutputWidth;
				outputHeight = description.OutputHeight;
				createdHdr = description.Hdr;
				createdInvertedDepth = description.InvertedDepth;
				IsCreated = true;
				LastError = "Contexto AMD FidelityFX FSR2 criado";
				return true;
			}
			catch (Exception e)
			{
				LastError = "Falha ao criar contexto FSR2: " + e.Message;
				Release();
				return false;
			}
		}

		public static bool Resize(int renderWidth, int renderHeight, int displayWidth, int displayHeight, bool hdr, bool invertedDepth)
		{
			if (!IsCreated)
				return false;
			if (renderWidth == maxRenderWidth && renderHeight == maxRenderHeight && displayWidth == outputWidth && displayHeight == outputHeight
				&& hdr == createdHdr && invertedDepth == createdInvertedDepth)
				return true;
			CreateDescription native = BuildDescription(renderWidth, renderHeight, displayWidth, displayHeight, hdr, invertedDepth);
			if (u3ffx_fsr2_resize(ref native) == 0)
			{
				LastError = GetLastError();
				IsCreated = false;
				return false;
			}
			maxRenderWidth = renderWidth;
			maxRenderHeight = renderHeight;
			outputWidth = displayWidth;
			outputHeight = displayHeight;
			createdHdr = hdr;
			createdInvertedDepth = invertedDepth;
			return true;
		}

		public static bool Dispatch(ref TemporalFrameContext frame)
		{
			if (!IsCreated)
			{
				LastError = "Contexto FSR2 não criado";
				return false;
			}
			if (!TemporalInputCollector.IsReadyForTemporalUpscaling(frame, out string reason))
			{
				LastError = reason;
				return false;
			}
			if (!Resize(frame.RenderWidth, frame.RenderHeight, frame.OutputWidth, frame.OutputHeight, frame.Hdr, frame.InvertedDepth))
				return false;

			for (int attempt = 0; attempt < RingSize; ++attempt)
			{
				int slot = (nextSlot + attempt) % RingSize;
				IntPtr pointer = GetSlotPointer(slot);
				DispatchParameters existing = Marshal.PtrToStructure<DispatchParameters>(pointer);
				if (existing.status == Pending)
					continue;
				if (existing.status < 0)
				{
					LastError = GetLastError();
					return false;
				}
				uint generation = ++nextGeneration;
				DispatchParameters parameters = new DispatchParameters()
				{
					struct_size = (uint)Marshal.SizeOf<DispatchParameters>(),
					abi_version = AbiVersion,
					command = DispatchCommand,
					frame_slot = (uint)slot,
					generation = generation,
					color_texture = frame.Color.GetNativeTexturePtr(),
					depth_texture = frame.Depth.GetNativeDepthBufferPtr(),
					motion_vectors_texture = frame.MotionVectors.GetNativeTexturePtr(),
					output_texture = frame.Output.GetNativeTexturePtr(),
					reactive_texture = frame.ReactiveMask != null ? frame.ReactiveMask.GetNativeTexturePtr() : IntPtr.Zero,
					composition_texture = frame.CompositionMask != null ? frame.CompositionMask.GetNativeTexturePtr() : IntPtr.Zero,
					render_width = (uint)frame.Color.width,
					render_height = (uint)frame.Color.height,
					output_width = (uint)frame.Output.width,
					output_height = (uint)frame.Output.height,
					jitter_x = frame.Jitter.x * frame.RenderWidth,
					jitter_y = frame.Jitter.y * frame.RenderHeight,
					motion_vector_scale_x = frame.MotionVectorScale.x,
					motion_vector_scale_y = frame.MotionVectorScale.y,
					delta_time_ms = frame.DeltaTimeMilliseconds,
					camera_near = frame.InvertedDepth ? frame.FarPlane : frame.NearPlane,
					camera_far = frame.InvertedDepth ? frame.NearPlane : frame.FarPlane,
					vertical_fov_radians = frame.VerticalFovRadians,
					pre_exposure = frame.PreExposure,
					reset_history = frame.ResetHistory ? 1u : 0u,
					hdr = frame.Hdr ? 1u : 0u,
					inverted_depth = frame.InvertedDepth ? 1u : 0u,
					frame_index = frame.FrameIndex,
					status = Pending,
				};
				if (parameters.color_texture == IntPtr.Zero || parameters.depth_texture == IntPtr.Zero || parameters.motion_vectors_texture == IntPtr.Zero
					|| parameters.output_texture == IntPtr.Zero)
				{
					LastError = "GetNativeTexturePtr retornou recurso temporal inválido";
					return false;
				}
				Marshal.StructureToPtr(parameters, pointer, false);
				eventCommands.Clear();
				eventCommands.IssuePluginEventAndData(eventFunction, eventId, pointer);
				Graphics.ExecuteCommandBuffer(eventCommands);
				parameters = Marshal.PtrToStructure<DispatchParameters>(pointer);
				if (parameters.status < 0)
				{
					LastError = GetLastError();
					return false;
				}
				nextSlot = (slot + 1) % RingSize;
				// ExecuteCommandBuffer enfileira o callback no Render Thread. Status Pending é
				// esperado até ele consumir o slot; o próximo uso do ring observa erro/conclusão.
				return true;
			}
			LastError = "Ring FSR2 sem slot livre";
			return false;
		}

		public static void ResetHistory()
		{
			if (IsCreated)
				u3ffx_fsr2_reset_history();
		}

		public static void Release()
		{
			if (IsCreated)
				u3ffx_fsr2_destroy();
			IsCreated = false;
			if (ringMemory != IntPtr.Zero)
				Marshal.FreeHGlobal(ringMemory);
			ringMemory = IntPtr.Zero;
			eventCommands?.Release();
			eventCommands = null;
			eventFunction = IntPtr.Zero;
			nextSlot = 0;
		}

		private static void AllocateRing()
		{
			if (ringMemory != IntPtr.Zero)
				return;
			eventFunction = GetRenderEventAndDataFunc();
			eventId = u3ffx_get_vulkan_smoke_event_id();
			int stride = Marshal.SizeOf<DispatchParameters>();
			ringMemory = Marshal.AllocHGlobal(stride * RingSize);
			eventCommands = new CommandBuffer() { name = "LinuxPerformance.FSR2Events" };
			for (int i = 0; i < RingSize; ++i)
			{
				DispatchParameters parameters = new DispatchParameters() { status = -2 };
				Marshal.StructureToPtr(parameters, GetSlotPointer(i), false);
			}
		}

		private static CreateDescription BuildDescription(int renderWidth, int renderHeight, int displayWidth, int displayHeight, bool hdr, bool invertedDepth)
		{
			uint flags = 1u << 6;
			if (hdr) flags |= 1u;
			if (invertedDepth) flags |= 1u << 3;
			return new CreateDescription()
			{
				struct_size = (uint)Marshal.SizeOf<CreateDescription>(),
				abi_version = AbiVersion,
				max_render_width = (uint)renderWidth,
				max_render_height = (uint)renderHeight,
				display_width = (uint)displayWidth,
				display_height = (uint)displayHeight,
				flags = flags,
			};
		}

		private static IntPtr GetSlotPointer(int slot) => IntPtr.Add(ringMemory, Marshal.SizeOf<DispatchParameters>() * slot);
		private static string GetLastError()
		{
			IntPtr value = u3ffx_get_last_error();
			return value != IntPtr.Zero ? Marshal.PtrToStringAnsi(value) : "Erro FSR2 sem mensagem";
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr GetRenderEventAndDataFunc();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_get_vulkan_smoke_event_id();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_create(ref CreateDescription description);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern void u3ffx_fsr2_destroy();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_resize(ref CreateDescription description);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern void u3ffx_fsr2_reset_history();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr u3ffx_get_last_error();

		private static IntPtr ringMemory;
		private static IntPtr eventFunction;
		private static CommandBuffer eventCommands;
		private static int eventId;
		private static int nextSlot;
		private static uint nextGeneration;
		private static int maxRenderWidth;
		private static int maxRenderHeight;
		private static int outputWidth;
		private static int outputHeight;
		private static bool createdHdr;
		private static bool createdInvertedDepth;
	}
}
