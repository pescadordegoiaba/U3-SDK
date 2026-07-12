////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace SDG.Unturned.Tests.PlayMode
{
	internal class NativeVulkanBridgePlayModeTests
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct NativeCapabilities
		{
			public int abi_version;
			public int backend_kind;
			public int has_vulkan_backend;
			public int has_fsr2;
			public int has_fsr31_upscaling;
			public int has_frame_generation;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string message;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct Fsr2CreateDescription
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
		private struct Fsr2DispatchParameters
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

		[UnityTest]
		public IEnumerator NativeVulkanComputeSmokeWritesExpectedImage()
		{
			if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
				Assert.Ignore("Teste requer Unity Editor em Vulkan");

			Type fsr2NativeType = Type.GetType("SDG.Unturned.LinuxPerformance.FidelityFxFsr2Native, Assembly-CSharp", true);
			Type dispatchType = fsr2NativeType.GetNestedType("DispatchParameters", BindingFlags.NonPublic);
			Assert.IsNotNull(dispatchType);
			Assert.AreEqual(144, Marshal.SizeOf(dispatchType));
			Assert.AreEqual(16, Marshal.OffsetOf(dispatchType, "generation").ToInt32());
			Assert.AreEqual(24, Marshal.OffsetOf(dispatchType, "color_texture").ToInt32());
			Assert.AreEqual(140, Marshal.OffsetOf(dispatchType, "status").ToInt32());

			Type bridgeType = Type.GetType("SDG.Unturned.LinuxPerformance.NativeVulkanBridge, Assembly-CSharp", true);
			MethodInfo validateMethod = bridgeType.GetMethod("ValidateSmokeCoroutine", BindingFlags.Public | BindingFlags.Static, null,
				new Type[] { typeof(int), typeof(int), typeof(Action<bool, string>) }, null);
			Assert.IsNotNull(validateMethod);

			foreach (Vector2Int size in new Vector2Int[] { new Vector2Int(64, 64), new Vector2Int(96, 48) })
			{
				bool completed = false;
				bool passed = false;
				string reason = null;
				Action<bool, string> callback = (result, message) =>
				{
					completed = true;
					passed = result;
					reason = message;
				};
				IEnumerator validation = (IEnumerator)validateMethod.Invoke(null, new object[] { size.x, size.y, callback });
				yield return validation;
				Assert.IsTrue(completed, size.ToString());
				Assert.IsTrue(passed, $"{size}: {reason}");
			}

			Assert.AreEqual(1, u3ffx_get_capabilities(out NativeCapabilities capabilities));
			Assert.AreEqual(4, capabilities.abi_version);
			Assert.AreEqual(3, capabilities.backend_kind, capabilities.message);
			Assert.AreEqual(1, capabilities.has_vulkan_backend, capabilities.message);
			Assert.AreEqual(0, capabilities.has_fsr2);
			Assert.AreEqual(0, capabilities.has_fsr31_upscaling);
			Assert.AreEqual(0, capabilities.has_frame_generation);

			bool invertedDepth = SystemInfo.usesReversedZBuffer;
			Fsr2CreateDescription create = CreateFsr2Description(64, 64, 128, 128, invertedDepth);
			Assert.AreEqual(1, u3ffx_fsr2_create(ref create));
			Assert.AreEqual(1, u3ffx_fsr2_get_status());
			AssertRejectedDispatch(8, 1, true, "slot fora do ring deve ser recusado");
			AssertRejectedDispatch(0, 0, true, "generation zero deve ser recusada");
			AssertRejectedDispatch(0, 1, false, "dispatch sem recursos deve ser recusado");
			yield return ValidateFsr2Dispatch(64, 64, 128, 128, 0, 1, invertedDepth);
			Assert.AreEqual(2, u3ffx_fsr2_get_status());
			u3ffx_fsr2_reset_history();
			Assert.AreEqual(0, u3ffx_fsr2_dispatch(IntPtr.Zero));
			Fsr2CreateDescription resized = CreateFsr2Description(96, 54, 128, 72, invertedDepth);
			Assert.AreEqual(1, u3ffx_fsr2_resize(ref resized));
			Assert.AreEqual(1, u3ffx_fsr2_get_status());
			yield return ValidateFsr2Dispatch(96, 54, 128, 72, 1, 2, invertedDepth);
			Assert.AreEqual(2, u3ffx_fsr2_get_status());
			u3ffx_fsr2_destroy();
			u3ffx_fsr2_destroy();
			Assert.AreEqual(0, u3ffx_fsr2_get_status());
		}

		[UnityTest]
		public IEnumerator GLRendererFallbackWritesDestination()
		{
			GameObject gameObject = new GameObject("GLRendererFallbackTest");
			RenderTexture source = CreateRenderTexture(16, 16, RenderTextureFormat.ARGB32, 0, false);
			RenderTexture destination = CreateRenderTexture(16, 16, RenderTextureFormat.ARGB32, 0, false);
			Texture2D readback = null;
			RenderTexture previous = RenderTexture.active;
			try
			{
				gameObject.AddComponent<Camera>();
				Type rendererType = Type.GetType("SDG.Framework.Rendering.GLRenderer, Assembly-CSharp", true);
				Component renderer = gameObject.AddComponent(rendererType);
				Clear(source, new Color(0.2f, 0.45f, 0.7f, 1.0f), false, 1.0f);
				Clear(destination, Color.black, false, 1.0f);
				MethodInfo method = rendererType.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.IsNotNull(method);
				method.Invoke(renderer, new object[] { source, destination });
				yield return null;
				readback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
				RenderTexture.active = destination;
				readback.ReadPixels(new Rect(8, 8, 1, 1), 0, 0, false);
				readback.Apply(false, false);
				Color result = readback.GetPixel(0, 0);
				Assert.That(result.r, Is.EqualTo(0.2f).Within(0.03f));
				Assert.That(result.g, Is.EqualTo(0.45f).Within(0.03f));
				Assert.That(result.b, Is.EqualTo(0.7f).Within(0.03f));
			}
			finally
			{
				RenderTexture.active = previous;
				if (readback != null)
					UnityEngine.Object.DestroyImmediate(readback);
				Release(source);
				Release(destination);
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void VulkanSmokeRequiresExplicitOptInAndDisableAllWins()
		{
			Type bootstrapType = Type.GetType("SDG.Unturned.LinuxPerformance.LinuxPerformanceBootstrap, Assembly-CSharp", true);
			MethodInfo method = bootstrapType.GetMethod("ShouldRunVulkanSmoke", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			Assert.IsFalse((bool)method.Invoke(null, new object[] { new string[0] }));
			Assert.IsTrue((bool)method.Invoke(null, new object[] { new string[] { "game", "-LinuxPerformanceRunVulkanSmoke" } }));
			Assert.IsFalse((bool)method.Invoke(null, new object[] { new string[] { "game", "-LinuxPerformanceRunVulkanSmoke", "-LinuxPerformanceDisableAll" } }));
		}

		[Test]
		public void LowResolutionRequiresLoadedGameplayPlayerMainCameraAndBackend()
		{
			Type rendererType = Type.GetType("SDG.Unturned.LinuxPerformance.LowResolutionWorldRenderer, Assembly-CSharp", true);
			MethodInfo method = rendererType.GetMethod("MeetsLowResolutionGameplayRequirements", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			Func<bool, bool, bool, bool, bool, bool, bool, float, bool> evaluate =
				(disableAll, loaded, player, main, active, backend, loading, scale) =>
					(bool)method.Invoke(null, new object[] { disableAll, loaded, player, main, active, backend, loading, scale });
			Assert.IsTrue(evaluate(false, true, true, true, true, true, false, 0.67f));
			Assert.IsFalse(evaluate(true, true, true, true, true, true, false, 0.67f));
			Assert.IsFalse(evaluate(false, false, true, true, true, true, false, 0.67f));
			Assert.IsFalse(evaluate(false, true, false, true, true, true, false, 0.67f));
			Assert.IsFalse(evaluate(false, true, true, false, true, true, false, 0.67f));
			Assert.IsFalse(evaluate(false, true, true, true, true, false, false, 0.67f));
			Assert.IsFalse(evaluate(false, true, true, true, true, true, true, 0.67f));
			Assert.IsFalse(evaluate(false, true, true, true, true, true, false, 1.0f));
		}

		[Test]
		public void ModuleHookFiltersUnavailableReflectionTypes()
		{
			Type hookType = Type.GetType("SDG.Framework.Modules.ModuleHook, Assembly-CSharp", true);
			MethodInfo method = hookType.GetMethod("FilterLoadableTypes", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			Type[] result = (Type[])method.Invoke(null, new object[] { new Type[] { typeof(string), null, typeof(int) } });
			CollectionAssert.AreEqual(new Type[] { typeof(string), typeof(int) }, result);
		}

		[Test]
		public void GlazierFactoryRegistersImplementationBeforeWidgetCreation()
		{
			Type factoryType = Type.GetType("SDG.Unturned.GlazierFactory, Assembly-CSharp", true);
			Type glazierType = Type.GetType("SDG.Unturned.Glazier, SDG.Glazier.Runtime", true);
			FieldInfo instanceField = glazierType.GetField("instance", BindingFlags.Static | BindingFlags.Public);
			object previous = instanceField.GetValue(null);
			object implementation = null;
			try
			{
				factoryType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
				implementation = glazierType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
				Assert.IsNotNull(implementation);
				Type wrapperType = Type.GetType("SDG.Unturned.SleekWrapper, SDG.Glazier.Runtime", true);
				Assert.DoesNotThrow(() => Activator.CreateInstance(wrapperType));
			}
			finally
			{
				instanceField.SetValue(null, previous);
				Component component = implementation as Component;
				if (component != null)
					UnityEngine.Object.DestroyImmediate(component.gameObject);
			}
		}

		[UnityTest]
		public IEnumerator LowResolutionOnDisableRestoresPreviousCameraTarget()
		{
			GameObject gameObject = new GameObject("LowResolutionRestoreTest");
			RenderTexture original = CreateRenderTexture(16, 16, RenderTextureFormat.ARGB32, 0, false);
			RenderTexture overrideTarget = CreateRenderTexture(8, 8, RenderTextureFormat.ARGB32, 0, false);
			try
			{
				Camera camera = gameObject.AddComponent<Camera>();
				Type rendererType = Type.GetType("SDG.Unturned.LinuxPerformance.LowResolutionWorldRenderer, Assembly-CSharp", true);
				Behaviour renderer = (Behaviour)gameObject.AddComponent(rendererType);
				rendererType.GetField("previousTargetTexture", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(renderer, original);
				rendererType.GetField("didOverrideCameraBuffers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(renderer, true);
				camera.targetTexture = overrideTarget;
				renderer.enabled = false;
				yield return null;
				Assert.AreSame(original, camera.targetTexture);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
				Release(original);
				Release(overrideTarget);
			}
		}

		private static Fsr2CreateDescription CreateFsr2Description(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight, bool invertedDepth)
		{
			return new Fsr2CreateDescription()
			{
				struct_size = (uint)Marshal.SizeOf<Fsr2CreateDescription>(),
				abi_version = 4,
				max_render_width = renderWidth,
				max_render_height = renderHeight,
				display_width = displayWidth,
				display_height = displayHeight,
				flags = (1u << 6) | (1u << 8) | (invertedDepth ? 1u << 3 : 0u),
			};
		}

		private static IEnumerator ValidateFsr2Dispatch(int renderWidth, int renderHeight, int outputWidth, int outputHeight,
			uint frameSlot, uint generation, bool invertedDepth)
		{
			RenderTexture color = CreateRenderTexture(renderWidth, renderHeight, RenderTextureFormat.ARGBHalf, 0, true);
			RenderTexture depth = CreateDepthRenderTexture(renderWidth, renderHeight);
			RenderTexture motion = CreateRenderTexture(renderWidth, renderHeight, RenderTextureFormat.RGHalf, 0, true);
			RenderTexture output = CreateRenderTexture(outputWidth, outputHeight, RenderTextureFormat.ARGBHalf, 0, true);
			IntPtr parametersPointer = IntPtr.Zero;
			CommandBuffer commands = null;
			Texture2D readback = null;
			RenderTexture previous = RenderTexture.active;
			try
			{
				Clear(color, new Color(0.25f, 0.5f, 0.75f, 1.0f), false, 1.0f);
				Clear(motion, Color.clear, false, 1.0f);
				Clear(depth, Color.clear, true, invertedDepth ? 0.0f : 1.0f);
				Color sentinel = new Color(1.0f, 0.0f, 1.0f, 1.0f);
				Clear(output, sentinel, false, 1.0f);

				Fsr2DispatchParameters parameters = new Fsr2DispatchParameters()
				{
					struct_size = (uint)Marshal.SizeOf<Fsr2DispatchParameters>(),
					abi_version = 4,
					command = 2,
					frame_slot = frameSlot,
					generation = generation,
					color_texture = color.GetNativeTexturePtr(),
					depth_texture = depth.GetNativeDepthBufferPtr(),
					motion_vectors_texture = motion.GetNativeTexturePtr(),
					output_texture = output.GetNativeTexturePtr(),
					render_width = (uint)color.width,
					render_height = (uint)color.height,
					output_width = (uint)output.width,
					output_height = (uint)output.height,
					motion_vector_scale_x = renderWidth,
					motion_vector_scale_y = renderHeight,
					delta_time_ms = 16.6667f,
					camera_near = invertedDepth ? 1000.0f : 0.1f,
					camera_far = invertedDepth ? 0.1f : 1000.0f,
					vertical_fov_radians = 60.0f * Mathf.Deg2Rad,
					pre_exposure = 1.0f,
					reset_history = 1,
					inverted_depth = invertedDepth ? 1u : 0u,
					frame_index = generation,
					status = 0,
				};
				Assert.AreNotEqual(IntPtr.Zero, parameters.color_texture);
				Assert.AreNotEqual(IntPtr.Zero, parameters.depth_texture);
				Assert.AreNotEqual(IntPtr.Zero, parameters.motion_vectors_texture);
				Assert.AreNotEqual(IntPtr.Zero, parameters.output_texture);

				parametersPointer = Marshal.AllocHGlobal(Marshal.SizeOf<Fsr2DispatchParameters>());
				Marshal.StructureToPtr(parameters, parametersPointer, false);
				commands = new CommandBuffer() { name = "Tests.FSR2Dispatch" };
				commands.IssuePluginEventAndData(GetRenderEventAndDataFunc(), u3ffx_get_vulkan_smoke_event_id(), parametersPointer);
				Graphics.ExecuteCommandBuffer(commands);
				yield return null;

				parameters = Marshal.PtrToStructure<Fsr2DispatchParameters>(parametersPointer);
				Assert.AreEqual(1, parameters.status, GetLastError());
				readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
				RenderTexture.active = output;
				readback.ReadPixels(new Rect(outputWidth / 2, outputHeight / 2, 1, 1), 0, 0, false);
				readback.Apply(false, false);
				Color result = readback.GetPixel(0, 0);
				Assert.IsFalse(float.IsNaN(result.r) || float.IsNaN(result.g) || float.IsNaN(result.b) || float.IsNaN(result.a));
				Assert.Greater(result.r + result.g + result.b, 0.05f, "FSR2 produziu output preto");
				Assert.Greater(Vector4.Distance(result, sentinel), 0.1f, "FSR2 não alterou o sentinel do output");
			}
			finally
			{
				RenderTexture.active = previous;
				if (parametersPointer != IntPtr.Zero)
					Marshal.FreeHGlobal(parametersPointer);
				commands?.Release();
				if (readback != null)
					UnityEngine.Object.DestroyImmediate(readback);
				Release(color);
				Release(depth);
				Release(motion);
				Release(output);
			}
		}

		private static void AssertRejectedDispatch(uint frameSlot, uint generation, bool useNonNullPlaceholders, string message)
		{
			IntPtr placeholder = useNonNullPlaceholders ? new IntPtr(1) : IntPtr.Zero;
			Fsr2DispatchParameters parameters = new Fsr2DispatchParameters()
			{
				struct_size = (uint)Marshal.SizeOf<Fsr2DispatchParameters>(),
				abi_version = 4,
				command = 2,
				frame_slot = frameSlot,
				generation = generation,
				color_texture = placeholder,
				depth_texture = placeholder,
				motion_vectors_texture = placeholder,
				output_texture = placeholder,
				render_width = 64,
				render_height = 64,
				output_width = 128,
				output_height = 128,
				delta_time_ms = 16.6667f,
				vertical_fov_radians = 60.0f * Mathf.Deg2Rad,
				pre_exposure = 1.0f,
			};
			IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Fsr2DispatchParameters>());
			try
			{
				Marshal.StructureToPtr(parameters, pointer, false);
				Assert.AreEqual(0, u3ffx_fsr2_dispatch(pointer), message);
				parameters = Marshal.PtrToStructure<Fsr2DispatchParameters>(pointer);
				Assert.AreEqual(-1, parameters.status, message);
			}
			finally
			{
				Marshal.FreeHGlobal(pointer);
			}
		}

		private static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format, int depthBits, bool randomWrite)
		{
			RenderTexture texture = new RenderTexture(width, height, depthBits, format, RenderTextureReadWrite.Linear)
			{
				enableRandomWrite = randomWrite,
				antiAliasing = 1,
				useMipMap = false,
				autoGenerateMips = false,
			};
			Assert.IsTrue(texture.Create(), $"Falha ao criar RenderTexture {format} {width}x{height}");
			return texture;
		}

		private static RenderTexture CreateDepthRenderTexture(int width, int height)
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
			RenderTexture texture = new RenderTexture(descriptor);
			Assert.IsTrue(texture.Create(), $"Falha ao criar depth D32_SFloat {width}x{height}");
			return texture;
		}

		private static void Clear(RenderTexture texture, Color color, bool clearDepth, float depth)
		{
			RenderTexture.active = texture;
			GL.Clear(clearDepth, !clearDepth, color, depth);
		}

		private static void Release(RenderTexture texture)
		{
			if (texture == null)
				return;
			texture.Release();
			UnityEngine.Object.DestroyImmediate(texture);
		}

		private static string GetLastError()
		{
			IntPtr message = u3ffx_get_last_error();
			return message != IntPtr.Zero ? Marshal.PtrToStringAnsi(message) : "Erro nativo sem mensagem";
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_capabilities(out NativeCapabilities capabilities);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_create(ref Fsr2CreateDescription description);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern void u3ffx_fsr2_destroy();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_resize(ref Fsr2CreateDescription description);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_dispatch(IntPtr parameters);
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern void u3ffx_fsr2_reset_history();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_fsr2_get_status();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr u3ffx_get_last_error();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern int u3ffx_get_vulkan_smoke_event_id();
		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr GetRenderEventAndDataFunc();
	}
}
