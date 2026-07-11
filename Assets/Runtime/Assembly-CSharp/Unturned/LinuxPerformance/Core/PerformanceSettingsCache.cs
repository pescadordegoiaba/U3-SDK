////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	/// <summary>
	/// Owns the validated Linux performance settings snapshot. Expensive validation is
	/// performed only after an explicit invalidation rather than from the render hook.
	/// </summary>
	public static class PerformanceSettingsCache
	{
		public static PerformanceSettings Current
		{
			get
			{
				RefreshIfDirty();
				return current;
			}
		}

		public static uint Version { get; private set; }
		public static string LastInvalidationReason { get; private set; } = "Inicialização";
		public static bool IsDirty => isDirty;

		public static void Invalidate(string reason)
		{
			isDirty = true;
			LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Não informado" : reason;
		}

		public static bool RefreshIfDirty()
		{
			if (!isDirty)
				return false;

			current = PerformanceSettings.FromGraphicsSettings();
			isDirty = false;
			unchecked
			{
				++Version;
			}
			return true;
		}

		public static void NotifyMainCameraChanged(Camera camera)
		{
			int id = camera != null ? camera.GetInstanceID() : 0;
			if (id == mainCameraInstanceId)
				return;
			mainCameraInstanceId = id;
			Invalidate("Câmera principal alterada");
		}

		public static void NotifyResolutionChanged(int width, int height)
		{
			if (width <= 0 || height <= 0 || (width == outputWidth && height == outputHeight))
				return;
			outputWidth = width;
			outputHeight = height;
			Invalidate("Resolução alterada");
		}

		private static PerformanceSettings current;
		private static bool isDirty = true;
		private static int mainCameraInstanceId;
		private static int outputWidth;
		private static int outputHeight;
	}

	public static class LinuxPerformanceRuntime
	{
		public static bool IsNativeFastPath
		{
			get
			{
				PerformanceSettingsCache.RefreshIfDirty();
				if (cachedVersion != PerformanceSettingsCache.Version)
				{
					cachedVersion = PerformanceSettingsCache.Version;
					isNativeFastPath = UpscalerManager.IsNativeFastPath(PerformanceSettingsCache.Current);
				}
				return isNativeFastPath;
			}
		}

		private static uint cachedVersion = uint.MaxValue;
		private static bool isNativeFastPath;
	}
}
