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

		[UnityTest]
		public IEnumerator NativeVulkanComputeSmokeWritesExpectedImage()
		{
			if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
				Assert.Ignore("Teste requer Unity Editor em Vulkan");

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
			Assert.AreEqual(3, capabilities.backend_kind, capabilities.message);
			Assert.AreEqual(1, capabilities.has_vulkan_backend, capabilities.message);
			Assert.AreEqual(0, capabilities.has_fsr2);
			Assert.AreEqual(0, capabilities.has_fsr31_upscaling);
			Assert.AreEqual(0, capabilities.has_frame_generation);
		}

		[DllImport("FidelityFXLinux", CallingConvention = CallingConvention.Cdecl)]
		private static extern int u3ffx_get_capabilities(out NativeCapabilities capabilities);
	}
}
