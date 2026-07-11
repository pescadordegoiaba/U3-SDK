////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class VisibilityBudgetManager
	{
		public static int TotalRenderers { get; private set; }
		public static int PotentiallyVisibleRenderers { get; private set; }

		public static void Apply(Camera camera, in PerformanceSettings settings)
		{
			using (marker.Auto())
			{
				if (camera == null || settings.CullingProfile == ELinuxCullingProfile.Original)
					return;

				float multiplier = settings.CullingProfile == ELinuxCullingProfile.Agressivo ? 0.82f : 0.92f;
				int cameraId = camera.GetInstanceID();
				if (!baseLayerCullDistances.TryGetValue(cameraId, out float[] baseDistances))
				{
					float[] current = camera.layerCullDistances;
					baseDistances = new float[32];
					if (current != null)
						Array.Copy(current, baseDistances, Mathf.Min(current.Length, baseDistances.Length));
					baseLayerCullDistances.Add(cameraId, baseDistances);
				}

				float[] distances = new float[32];
				for (int i = 0; i < distances.Length; ++i)
					distances[i] = baseDistances[i] > 0.01f ? baseDistances[i] * multiplier : baseDistances[i];
				camera.layerCullDistances = distances;
				camera.layerCullSpherical = true;
			}
		}

		public static void Register(Renderer renderer)
		{
			if (renderer != null && registeredRenderers.Add(renderer))
				TotalRenderers = registeredRenderers.Count;
		}

		public static void Unregister(Renderer renderer)
		{
			if (renderer != null && registeredRenderers.Remove(renderer))
				TotalRenderers = registeredRenderers.Count;
		}

		public static void CaptureSceneMetrics()
		{
			using (marker.Auto())
			{
				PotentiallyVisibleRenderers = 0;
				Plane[] planes = null;
				Camera camera = MainCamera.instance;
				if (camera != null)
					planes = GeometryUtility.CalculateFrustumPlanes(camera);
				foreach (Renderer renderer in registeredRenderers)
				{
					if (renderer != null && renderer.enabled && (planes == null || GeometryUtility.TestPlanesAABB(planes, renderer.bounds)))
						++PotentiallyVisibleRenderers;
				}
			}
		}

		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.Culling");
		private static readonly Dictionary<int, float[]> baseLayerCullDistances = new Dictionary<int, float[]>();
		private static readonly HashSet<Renderer> registeredRenderers = new HashSet<Renderer>();
	}
}
