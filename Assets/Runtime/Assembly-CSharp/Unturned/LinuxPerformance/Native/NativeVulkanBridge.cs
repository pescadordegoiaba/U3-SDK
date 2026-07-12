////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	public static class NativeVulkanBridge
	{
		public const int SmokeStatusPending = 0;
		public const int SmokeStatusRecorded = 1;
		public const int SmokeStatusError = -1;
		private const int SlotFree = -2;
		private const int RingSize = 8;
		private const uint AbiVersion = 3;
		private const uint VulkanSmokeCommand = 1;

		[StructLayout(LayoutKind.Sequential)]
		private struct SmokeParameters
		{
			public uint struct_size;
			public uint abi_version;
			public uint command;
			public uint frame_slot;
			public uint generation;
			public IntPtr source_texture;
			public IntPtr output_texture;
			public uint width;
			public uint height;
			public uint frame_index;
			public int status;
		}

		public readonly struct SmokeTicket
		{
			internal readonly int Slot;
			internal readonly uint FrameIndex;

			internal SmokeTicket(int slot, uint frameIndex)
			{
				Slot = slot;
				FrameIndex = frameIndex;
			}
		}

		public static ELinuxFeatureState State { get; private set; } = ELinuxFeatureState.Disabled;
		public static string StateReason { get; private set; } = "Smoke Vulkan não inicializado";
		public static bool IsPluginLoaded { get; private set; }
		public static bool IsVulkanDetected { get; private set; }
		public static bool IsBridgeReady { get; private set; }
		public static bool IsSmokeValidated { get; private set; }

		public static bool Initialize()
		{
			if (isInitialized)
				return eventFunction != IntPtr.Zero;
			isInitialized = true;
			IsVulkanDetected = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;
			if (!IsVulkanDetected)
			{
				State = ELinuxFeatureState.Unsupported;
				StateReason = "Renderer ativo não é Vulkan";
				return false;
			}
			try
			{
				eventFunction = GetRenderEventAndDataFunc();
				eventId = u3ffx_get_vulkan_smoke_event_id();
				IsPluginLoaded = true;
				if (eventFunction == IntPtr.Zero || eventId <= 0)
				{
					State = ELinuxFeatureState.Error;
					StateReason = "Plugin não forneceu callback/event ID Vulkan";
					return false;
				}
				int stride = Marshal.SizeOf<SmokeParameters>();
				ringMemory = Marshal.AllocHGlobal(stride * RingSize);
				eventCommands = new CommandBuffer()
				{
					name = "LinuxPerformance.VulkanBridgeEvents",
				};
				for (int i = 0; i < RingSize; ++i)
				{
					SmokeParameters parameters = new SmokeParameters() { status = SlotFree };
					Marshal.StructureToPtr(parameters, GetSlotPointer(i), false);
				}
				State = ELinuxFeatureState.Disabled;
				StateReason = "Bridge carregado; compute smoke aguardando dispatch/readback";
				IsBridgeReady = true;
				return true;
			}
			catch (Exception e)
			{
				State = ELinuxFeatureState.Error;
				StateReason = "Falha ao inicializar bridge Vulkan: " + e.Message;
				return false;
			}
		}

		public static bool TryIssueSmoke(RenderTexture source, RenderTexture output, out SmokeTicket ticket)
		{
			ticket = default;
			if (!Initialize() || source == null || output == null || !source.IsCreated() || !output.IsCreated()
				|| !source.enableRandomWrite || !output.enableRandomWrite || source.width != output.width || source.height != output.height)
			{
				StateReason = "Source/output do smoke devem existir, usar enableRandomWrite e ter dimensões iguais";
				return false;
			}
			IntPtr nativeSource = source.GetNativeTexturePtr();
			IntPtr nativeOutput = output.GetNativeTexturePtr();
			if (nativeSource == IntPtr.Zero || nativeOutput == IntPtr.Zero || nativeSource == nativeOutput)
			{
				StateReason = "GetNativeTexturePtr retornou recurso inválido ou source/output iguais";
				return false;
			}

			for (int attempt = 0; attempt < RingSize; ++attempt)
			{
				int slot = (nextSlot + attempt) % RingSize;
				SmokeParameters existing = Marshal.PtrToStructure<SmokeParameters>(GetSlotPointer(slot));
				if (existing.status == SmokeStatusPending)
					continue;
				uint frameIndex = ++nextFrameIndex;
				SmokeParameters parameters = new SmokeParameters()
				{
					struct_size = (uint)Marshal.SizeOf<SmokeParameters>(),
					abi_version = AbiVersion,
					command = VulkanSmokeCommand,
					frame_slot = (uint)slot,
					generation = frameIndex,
					source_texture = nativeSource,
					output_texture = nativeOutput,
					width = (uint)output.width,
					height = (uint)output.height,
					frame_index = frameIndex,
					status = SmokeStatusPending,
				};
				Marshal.StructureToPtr(parameters, GetSlotPointer(slot), false);
				eventCommands.Clear();
				eventCommands.IssuePluginEventAndData(eventFunction, eventId, GetSlotPointer(slot));
				Graphics.ExecuteCommandBuffer(eventCommands);
				nextSlot = (slot + 1) % RingSize;
				ticket = new SmokeTicket(slot, frameIndex);
				StateReason = "Compute smoke enviado ao Render Thread";
				return true;
			}

			StateReason = "Ring unmanaged do bridge Vulkan está cheio";
			return false;
		}

		public static bool TryGetSmokeResult(in SmokeTicket ticket, out int status, out uint width, out uint height)
		{
			status = SmokeStatusError;
			width = 0;
			height = 0;
			if (!isInitialized || ringMemory == IntPtr.Zero || ticket.Slot < 0 || ticket.Slot >= RingSize)
				return false;
			SmokeParameters parameters = Marshal.PtrToStructure<SmokeParameters>(GetSlotPointer(ticket.Slot));
			if (parameters.frame_index != ticket.FrameIndex)
				return false;
			status = parameters.status;
			width = parameters.width;
			height = parameters.height;
			if (status == SmokeStatusRecorded)
			{
				StateReason = "Compute gravado; aguardando validação do readback";
				return true;
			}
			if (status == SmokeStatusError)
			{
				State = ELinuxFeatureState.Error;
				StateReason = GetLastError();
			}
			return true;
		}

		public static bool MarkSmokeValidated()
		{
			try
			{
				if (u3ffx_mark_vulkan_smoke_validated(1) == 0)
					return false;
				State = ELinuxFeatureState.Available;
				StateReason = "Bridge Vulkan compute validado por readback";
				IsSmokeValidated = true;
				UpscalerManager.InvalidateBackend("Smoke Vulkan validado");
				return true;
			}
			catch (Exception e)
			{
				State = ELinuxFeatureState.Error;
				StateReason = "Falha ao confirmar smoke Vulkan: " + e.Message;
				return false;
			}
		}

		public static IEnumerator ValidateSmokeCoroutine(Action<bool, string> completed)
		{
			return ValidateSmokeCoroutine(64, 64, completed);
		}

		public static IEnumerator ValidateSmokeCoroutine(int width, int height, Action<bool, string> completed)
		{
			if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
			{
				completed?.Invoke(false, "Renderer ativo não é Vulkan");
				yield break;
			}

			if (width <= 0 || height <= 0)
			{
				completed?.Invoke(false, "Dimensões inválidas para smoke Vulkan");
				yield break;
			}

			RenderTexture source = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				name = "LinuxPerformance.VulkanSmokeSource",
				enableRandomWrite = true,
				antiAliasing = 1,
			};
			RenderTexture target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				name = "LinuxPerformance.VulkanSmokeOutput",
				enableRandomWrite = true,
				antiAliasing = 1,
			};
			Texture2D readback = null;
			try
			{
				if (!source.Create() || !target.Create())
				{
					completed?.Invoke(false, "Falha ao criar source/output do smoke Vulkan");
					yield break;
				}
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = source;
				GL.Clear(false, true, new Color(0.2f, 0.6f, 0.3f, 1f));
				RenderTexture.active = target;
				GL.Clear(false, true, Color.black);
				RenderTexture.active = previous;
				if (!TryIssueSmoke(source, target, out SmokeTicket ticket))
				{
					completed?.Invoke(false, StateReason);
					yield break;
				}

				int status = SmokeStatusPending;
				uint reportedWidth = 0;
				uint reportedHeight = 0;
				for (int frame = 0; frame < 60 && status == SmokeStatusPending; ++frame)
				{
					yield return null;
					TryGetSmokeResult(ticket, out status, out reportedWidth, out reportedHeight);
				}
				if (status != SmokeStatusRecorded || reportedWidth != (uint)source.width || reportedHeight != (uint)source.height)
				{
					completed?.Invoke(false, status == SmokeStatusError ? StateReason : "Timeout aguardando compute smoke Vulkan");
					yield break;
				}

				yield return null;
				previous = RenderTexture.active;
				RenderTexture.active = target;
				readback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
				readback.ReadPixels(new Rect(source.width / 2, source.height / 2, 1, 1), 0, 0, false);
				readback.Apply(false, false);
				RenderTexture.active = previous;
				Color pixel = readback.GetPixel(0, 0);
				bool validPixel = pixel.r > 0.7f && pixel.r < 0.9f && pixel.g > 0.5f && pixel.g < 0.7f
					&& pixel.b > 0.2f && pixel.b < 0.4f && pixel.a > 0.9f;
				if (!validPixel || !MarkSmokeValidated())
				{
					completed?.Invoke(false, $"Readback inesperado: {pixel}");
					yield break;
				}
				completed?.Invoke(true, StateReason);
			}
			finally
			{
				if (readback != null)
					UnityEngine.Object.Destroy(readback);
				source.Release();
				UnityEngine.Object.Destroy(source);
				target.Release();
				UnityEngine.Object.Destroy(target);
			}
		}

		public static void Release()
		{
			if (ringMemory == IntPtr.Zero)
				return;
			for (int i = 0; i < RingSize; ++i)
			{
				SmokeParameters parameters = Marshal.PtrToStructure<SmokeParameters>(GetSlotPointer(i));
				if (parameters.status == SmokeStatusPending)
					return;
			}
			Marshal.FreeHGlobal(ringMemory);
			ringMemory = IntPtr.Zero;
			eventCommands?.Release();
			eventCommands = null;
			isInitialized = false;
			IsBridgeReady = false;
			IsSmokeValidated = false;
		}

		private static IntPtr GetSlotPointer(int slot)
		{
			return IntPtr.Add(ringMemory, Marshal.SizeOf<SmokeParameters>() * slot);
		}

		private static string GetLastError()
		{
			try
			{
				IntPtr value = u3ffx_get_last_error();
				return value != IntPtr.Zero ? Marshal.PtrToStringAnsi(value) : "Erro Vulkan sem mensagem";
			}
			catch
			{
				return "Erro Vulkan sem mensagem";
			}
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr GetRenderEventAndDataFunc();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_vulkan_smoke_event_id();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr u3ffx_get_last_error();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_mark_vulkan_smoke_validated(int validated);

		private static IntPtr eventFunction;
		private static IntPtr ringMemory;
		private static CommandBuffer eventCommands;
		private static int eventId;
		private static int nextSlot;
		private static uint nextFrameIndex;
		private static bool isInitialized;
	}
}
