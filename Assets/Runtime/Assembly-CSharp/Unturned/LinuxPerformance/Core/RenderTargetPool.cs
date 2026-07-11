////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class RenderTargetPool
	{
		public static int ActiveCount => active.Count;
		public static int IdleCount => idle.Count;
		public static long EstimatedBytes { get; private set; }
		public static long BudgetBytes = 128L * 1024L * 1024L;

		public static RenderTexture Get(RenderTextureDescriptor descriptor, string debugName)
		{
			descriptor.msaaSamples = 1;
			descriptor.useMipMap = false;
			descriptor.autoGenerateMips = false;
			RenderTargetDescriptorKey key = new RenderTargetDescriptorKey(descriptor);
			if (idle.TryGetValue(key, out Stack<RenderTexture> stack) && stack.Count > 0)
			{
				RenderTexture reused = stack.Pop();
				active.Add(reused, key);
				return reused;
			}

			RenderTexture rt = new RenderTexture(descriptor);
			rt.name = debugName;
			rt.Create();
			active.Add(rt, key);
			EstimatedBytes += key.EstimatedBytes;
			TrimIfNeeded();
			return rt;
		}

		public static void Release(RenderTexture rt)
		{
			if (rt == null)
				return;
			if (!active.TryGetValue(rt, out RenderTargetDescriptorKey key))
				return;
			active.Remove(rt);
			if (!idle.TryGetValue(key, out Stack<RenderTexture> stack))
			{
				stack = new Stack<RenderTexture>();
				idle.Add(key, stack);
			}
			stack.Push(rt);
		}

		public static void Clear()
		{
			foreach (RenderTexture rt in active.Keys)
				Destroy(rt);
			active.Clear();
			foreach (Stack<RenderTexture> stack in idle.Values)
			{
				while (stack.Count > 0)
					Destroy(stack.Pop());
			}
			idle.Clear();
			EstimatedBytes = 0;
		}

		private static void TrimIfNeeded()
		{
			if (EstimatedBytes <= BudgetBytes)
				return;
			foreach (Stack<RenderTexture> stack in idle.Values)
			{
				while (stack.Count > 0 && EstimatedBytes > BudgetBytes)
				{
					RenderTexture rt = stack.Pop();
					EstimatedBytes -= new RenderTargetDescriptorKey(rt.descriptor).EstimatedBytes;
					Destroy(rt);
				}
			}
		}

		private static void Destroy(RenderTexture rt)
		{
			if (rt == null)
				return;
			rt.Release();
			UnityEngine.Object.Destroy(rt);
		}

		private static readonly Dictionary<RenderTexture, RenderTargetDescriptorKey> active = new Dictionary<RenderTexture, RenderTargetDescriptorKey>();
		private static readonly Dictionary<RenderTargetDescriptorKey, Stack<RenderTexture>> idle = new Dictionary<RenderTargetDescriptorKey, Stack<RenderTexture>>();

		private struct RenderTargetDescriptorKey : IEquatable<RenderTargetDescriptorKey>
		{
			public readonly int Width;
			public readonly int Height;
			public readonly RenderTextureFormat ColorFormat;
			public readonly int DepthBits;
			public readonly RenderTextureReadWrite ReadWrite;
			public readonly long EstimatedBytes;

			public RenderTargetDescriptorKey(RenderTextureDescriptor descriptor)
			{
				Width = descriptor.width;
				Height = descriptor.height;
				ColorFormat = descriptor.colorFormat;
				DepthBits = descriptor.depthBufferBits;
				ReadWrite = descriptor.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
				EstimatedBytes = (long)Width * Height * EstimateBytesPerPixel(ColorFormat, DepthBits);
			}

			public bool Equals(RenderTargetDescriptorKey other)
			{
				return Width == other.Width && Height == other.Height && ColorFormat == other.ColorFormat && DepthBits == other.DepthBits && ReadWrite == other.ReadWrite;
			}

			public override bool Equals(object obj)
			{
				return obj is RenderTargetDescriptorKey other && Equals(other);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = Width;
					hash = (hash * 397) ^ Height;
					hash = (hash * 397) ^ (int)ColorFormat;
					hash = (hash * 397) ^ DepthBits;
					hash = (hash * 397) ^ (int)ReadWrite;
					return hash;
				}
			}

			private static int EstimateBytesPerPixel(RenderTextureFormat format, int depthBits)
			{
				int colorBytes = format == RenderTextureFormat.ARGBHalf ? 8 : (format == RenderTextureFormat.ARGBFloat ? 16 : 4);
				return colorBytes + Mathf.Max(0, depthBits / 8);
			}
		}
	}
}
