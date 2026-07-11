////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	public sealed class LinuxGraphicsCapabilities
	{
		public GraphicsDeviceType GraphicsDeviceType;
		public string GraphicsDeviceName;
		public string GraphicsDeviceVendor;
		public int GraphicsMemoryMb;
		public bool IsVulkan;
		public bool IsOpenGLCore;
		public bool IsAmd;
		public bool IsRx580OrPolaris;
		public bool SupportsComputeShaders;
		public bool SupportsDepth;
		public bool SupportsMotionVectors;
		public bool SupportsARGBHalf;
		public bool SupportsARGBFloat;
		public bool SupportsDefaultHDR;
		public bool NativePluginLoaded;
		public int NativePluginAbiVersion;
		public string NativePluginStatus;
		public string NativePluginBackend;
		public ELinuxNativeBackendKind NativePluginBackendKind;
		public bool NativePluginHasVulkanBackend;
		public bool NativePluginHasFsr2;
		public bool NativePluginHasFsr31Upscaling;
		public bool NativePluginHasFrameGeneration;

		public static LinuxGraphicsCapabilities Detect()
		{
			LinuxGraphicsCapabilities result = new LinuxGraphicsCapabilities();
			result.GraphicsDeviceType = SystemInfo.graphicsDeviceType;
			result.GraphicsDeviceName = SystemInfo.graphicsDeviceName ?? string.Empty;
			result.GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor ?? string.Empty;
			result.GraphicsMemoryMb = SystemInfo.graphicsMemorySize;
			result.IsVulkan = result.GraphicsDeviceType == GraphicsDeviceType.Vulkan;
			result.IsOpenGLCore = result.GraphicsDeviceType == GraphicsDeviceType.OpenGLCore;
			result.IsAmd = result.GraphicsDeviceVendor.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0;
			result.IsRx580OrPolaris = result.GraphicsDeviceName.IndexOf("RX 580", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("POLARIS", StringComparison.OrdinalIgnoreCase) >= 0
				|| result.GraphicsDeviceName.IndexOf("Polaris", StringComparison.OrdinalIgnoreCase) >= 0;
			result.SupportsComputeShaders = SystemInfo.supportsComputeShaders;
			result.SupportsDepth = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
			result.SupportsMotionVectors = SystemInfo.supportsMotionVectors;
			result.SupportsARGBHalf = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
			result.SupportsARGBFloat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);
			result.SupportsDefaultHDR = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
			result.NativePluginLoaded = NativeRenderBackend.TryGetCapabilities(out NativeRenderBackend.PluginCapabilities pluginCapabilities, out result.NativePluginStatus);
			result.NativePluginAbiVersion = pluginCapabilities.AbiVersion;
			result.NativePluginBackend = pluginCapabilities.BackendName;
			result.NativePluginBackendKind = pluginCapabilities.BackendKind;
			result.NativePluginHasVulkanBackend = pluginCapabilities.HasVulkanBackend;
			result.NativePluginHasFsr2 = pluginCapabilities.HasFsr2;
			result.NativePluginHasFsr31Upscaling = pluginCapabilities.HasFsr31Upscaling;
			result.NativePluginHasFrameGeneration = pluginCapabilities.HasFrameGeneration;
			return result;
		}
	}
}
