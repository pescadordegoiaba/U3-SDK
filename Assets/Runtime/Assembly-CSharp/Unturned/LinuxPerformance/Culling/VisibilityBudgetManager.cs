////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Unturned.LinuxPerformance
{
	public static class VisibilityBudgetManager
	{
		public static int TotalObjects => entries.Count;
		public static int TotalRenderers { get; private set; }
		public static int PotentiallyVisibleRenderers { get; private set; }

		public static void Apply(Camera camera, in PerformanceSettings settings)
		{
			using (marker.Auto())
			{
				SetProfile(settings.CullingProfile);
				if (camera == null)
					return;

				int cameraId = camera.GetInstanceID();
				if (!baseLayerCullDistances.TryGetValue(cameraId, out float[] baseDistances))
				{
					baseDistances = new float[32];
					float[] current = camera.layerCullDistances;
					if (current != null)
						Array.Copy(current, baseDistances, Mathf.Min(current.Length, baseDistances.Length));
					baseLayerCullDistances.Add(cameraId, baseDistances);
				}

				if (settings.CullingProfile == ELinuxCullingProfile.Original)
				{
					camera.layerCullDistances = baseDistances;
					return;
				}

				float multiplier = settings.CullingProfile == ELinuxCullingProfile.Agressivo ? 0.82f : 0.92f;
				if (!scaledLayerCullDistances.TryGetValue(cameraId, out float[] distances))
				{
					distances = new float[32];
					scaledLayerCullDistances.Add(cameraId, distances);
				}
				for (int i = 0; i < distances.Length; ++i)
					distances[i] = baseDistances[i] > 0.01f ? baseDistances[i] * multiplier : baseDistances[i];
				camera.layerCullDistances = distances;
				camera.layerCullSpherical = true;
			}
		}

		public static void Register(LevelObject levelObject)
		{
			if (levelObject == null || levelObject.transform == null || entries.ContainsKey(levelObject))
				return;

			CullingEntry entry = new CullingEntry(levelObject);
			entries.Add(levelObject, entry);
			AddToRegion(entry);
			TotalRenderers += entry.Renderers.Count;
		}

		public static void Unregister(LevelObject levelObject)
		{
			if (levelObject == null || !entries.TryGetValue(levelObject, out CullingEntry entry))
				return;
			entry.RestoreImmediately();
			RemoveFromRegion(entry);
			entries.Remove(levelObject);
			TotalRenderers = Mathf.Max(0, TotalRenderers - entry.Renderers.Count);
		}

		public static void NotifyMoved(LevelObject levelObject)
		{
			if (levelObject == null || levelObject.transform == null || !entries.TryGetValue(levelObject, out CullingEntry entry))
				return;
			Vector2Int newRegion = Regions.GetCoordinateVector2Int(levelObject.transform.position);
			if (newRegion != entry.Region)
			{
				RemoveFromRegion(entry);
				entry.Region = newRegion;
				AddToRegion(entry);
			}
			entry.RefreshPositionAndBounds();
		}

		public static void Tick(Camera camera, in PerformanceSettings settings)
		{
			if (camera == null || settings.CullingProfile == ELinuxCullingProfile.Original || GraphicsSettings.WantsCinematicMode || !Level.isLoaded)
			{
				if (!isSuspended)
				{
					RestoreAll();
					isSuspended = true;
				}
				return;
			}

			isSuspended = false;
			Vector3 cameraPosition = MainCamera.RenderingPosition;
			Vector2Int cameraRegion = Regions.GetCoordinateVector2Int(cameraPosition);
			regionTracker.MaxDistance = Regions.REGION_SIZE * 1.5f;
			regionTracker.CameraCoord = cameraRegion;
			ProcessEnteringAndLeavingRegions(cameraPosition);
			ScanRelevantRegions(cameraRegion, cameraPosition, settings.CullingProfile);
			ProcessQueuedChanges();
		}

		public static void RestoreAll()
		{
			foreach (CullingEntry entry in entries.Values)
				entry.RestoreImmediately();
			ClearQueues();
		}

		public static void Clear()
		{
			RestoreAll();
			entries.Clear();
			entriesByRegion.Clear();
			scanIndices.Clear();
			regionTracker.FlushProgress();
			TotalRenderers = 0;
			PotentiallyVisibleRenderers = 0;
		}

		public static void CaptureSceneMetrics()
		{
			if (!PerformanceSettingsCache.Current.DebugOverlay)
				return;
			if (Time.unscaledTime < nextDetailedMetricsTime)
				return;
			nextDetailedMetricsTime = Time.unscaledTime + 0.5f;
			using (marker.Auto())
			{
				PotentiallyVisibleRenderers = 0;
				Camera camera = MainCamera.instance;
				bool hasCamera = camera != null;
				if (hasCamera)
					GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
				foreach (CullingEntry entry in entries.Values)
				{
					for (int i = 0; i < entry.Renderers.Count; ++i)
					{
						Renderer renderer = entry.Renderers[i];
						if (renderer != null && renderer.enabled && (!hasCamera || GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds)))
							++PotentiallyVisibleRenderers;
					}
				}
			}
		}

		private static void SetProfile(ELinuxCullingProfile profile)
		{
			if (activeProfile == profile)
				return;
			activeProfile = profile;
			RestoreAll();
			isSuspended = profile == ELinuxCullingProfile.Original;
		}

		private static void ProcessEnteringAndLeavingRegions(Vector3 cameraPosition)
		{
			regionChanges.Clear();
			regionTracker.UpdateRegions(regionChanges);
			foreach (KeyValuePair<Vector2Int, RegionVisibilityData> pair in regionChanges)
			{
				if (!entriesByRegion.TryGetValue(pair.Key, out List<CullingEntry> regionEntries) || pair.Value.progressIndex >= regionEntries.Count)
				{
					regionTracker.NotifyRegionFinishedUpdating(pair.Key);
					continue;
				}
				EvaluateAndQueue(regionEntries[pair.Value.progressIndex], cameraPosition, activeProfile);
			}
		}

		private static void ScanRelevantRegions(Vector2Int cameraRegion, Vector3 cameraPosition, ELinuxCullingProfile profile)
		{
			int remaining = CandidateChecksPerFrame;
			for (int y = -1; y <= 1 && remaining > 0; ++y)
			{
				for (int x = -1; x <= 1 && remaining > 0; ++x)
				{
					Vector2Int coord = new Vector2Int(cameraRegion.x + x, cameraRegion.y + y);
					if (!entriesByRegion.TryGetValue(coord, out List<CullingEntry> regionEntries) || regionEntries.Count == 0)
						continue;
					scanIndices.TryGetValue(coord, out int index);
					int checks = Mathf.Min(regionEntries.Count, Mathf.Min(6, remaining));
					for (int i = 0; i < checks; ++i)
					{
						if (index >= regionEntries.Count)
							index = 0;
						EvaluateAndQueue(regionEntries[index++], cameraPosition, profile);
						--remaining;
					}
					scanIndices[coord] = index;
				}
			}
		}

		internal static void EvaluateAndQueue(CullingEntry entry, Vector3 cameraPosition, ELinuxCullingProfile profile)
		{
			if (entry == null || entry.LevelObject == null || entry.LevelObject.transform == null)
			{
				if (entry != null)
					destroyedEntries.Add(entry);
				return;
			}
			if (entry.Category == GameplayCullableCategory.NeverCull)
				return;
			entry.ConfigureDistances(profile);
			float squaredDistance = (entry.Position - cameraPosition).sqrMagnitude;
			bool desiredVisible = GameplayCullingManager.ShouldBeVisible(entry.DesiredVisible, squaredDistance, entry.HideDistance, entry.ReactivateDistance);
			if (desiredVisible == entry.DesiredVisible)
				return;
			entry.DesiredVisible = desiredVisible;
			Queue(entry);
		}

		internal static void Queue(CullingEntry entry)
		{
			unchecked
			{
				++entry.QueueGeneration;
			}
			entry.IsQueued = true;
			CullingQueueItem item = new CullingQueueItem(entry, entry.QueueGeneration, entry.DesiredVisible);
			if (entry.DesiredVisible)
				showQueue.Add(item);
			else if (entry.Category == GameplayCullableCategory.LargeStructure)
				importantQueue.Add(item);
			else
				hideQueue.Add(item);
		}

		internal static void ProcessQueuedChanges()
		{
			CullingWorkBudget budget = CullingWorkBudget.CreateDefault();
			ProcessQueue(showQueue, ref showQueueIndex, ref budget);
			ProcessQueue(importantQueue, ref importantQueueIndex, ref budget);
			ProcessQueue(hideQueue, ref hideQueueIndex, ref budget);
			RemoveDestroyedEntries();
		}

		private static void ProcessQueue(List<CullingQueueItem> queue, ref int index, ref CullingWorkBudget budget)
		{
			while (index < queue.Count)
			{
				CullingQueueItem item = queue[index];
				CullingEntry entry = item.Entry;
				if (entry == null || item.Generation != entry.QueueGeneration || item.DesiredVisible != entry.DesiredVisible)
				{
					++index;
					continue;
				}
				if (entry == null || entry.LevelObject == null || entry.LevelObject.transform == null)
				{
					if (entry != null)
						destroyedEntries.Add(entry);
					++index;
					continue;
				}
				if (!entry.ApplyIncrementally(ref budget))
					break;
				if (item.Generation == entry.QueueGeneration)
					entry.IsQueued = false;
				++index;
			}
			if (index >= queue.Count)
			{
				queue.Clear();
				index = 0;
			}
		}

		private static void RemoveDestroyedEntries()
		{
			for (int i = 0; i < destroyedEntries.Count; ++i)
			{
				CullingEntry entry = destroyedEntries[i];
				if (entry != null && entry.LevelObject != null)
					Unregister(entry.LevelObject);
			}
			destroyedEntries.Clear();
		}

		private static void AddToRegion(CullingEntry entry)
		{
			if (!entriesByRegion.TryGetValue(entry.Region, out List<CullingEntry> regionEntries))
			{
				regionEntries = new List<CullingEntry>();
				entriesByRegion.Add(entry.Region, regionEntries);
			}
			regionEntries.Add(entry);
		}

		private static void RemoveFromRegion(CullingEntry entry)
		{
			if (!entriesByRegion.TryGetValue(entry.Region, out List<CullingEntry> regionEntries))
				return;
			regionEntries.RemoveFast(entry);
			if (regionEntries.Count == 0)
			{
				entriesByRegion.Remove(entry.Region);
				scanIndices.Remove(entry.Region);
			}
		}

		private static void ClearQueues()
		{
			showQueue.Clear();
			importantQueue.Clear();
			hideQueue.Clear();
			showQueueIndex = 0;
			importantQueueIndex = 0;
			hideQueueIndex = 0;
		}

		private const int CandidateChecksPerFrame = 48;
		private static readonly ProfilerMarker marker = new ProfilerMarker("LinuxPerformance.Culling");
		private static readonly Dictionary<int, float[]> baseLayerCullDistances = new Dictionary<int, float[]>();
		private static readonly Dictionary<int, float[]> scaledLayerCullDistances = new Dictionary<int, float[]>();
		private static readonly Dictionary<LevelObject, CullingEntry> entries = new Dictionary<LevelObject, CullingEntry>();
		private static readonly Dictionary<Vector2Int, List<CullingEntry>> entriesByRegion = new Dictionary<Vector2Int, List<CullingEntry>>();
		private static readonly Dictionary<Vector2Int, int> scanIndices = new Dictionary<Vector2Int, int>();
		private static readonly RegionIncrementalVisibilityTracker regionTracker = new RegionIncrementalVisibilityTracker();
		private static readonly Dictionary<Vector2Int, RegionVisibilityData> regionChanges = new Dictionary<Vector2Int, RegionVisibilityData>();
		private static readonly List<CullingQueueItem> showQueue = new List<CullingQueueItem>();
		private static readonly List<CullingQueueItem> importantQueue = new List<CullingQueueItem>();
		private static readonly List<CullingQueueItem> hideQueue = new List<CullingQueueItem>();
		private static readonly List<CullingEntry> destroyedEntries = new List<CullingEntry>();
		private static readonly Plane[] frustumPlanes = new Plane[6];
		private static ELinuxCullingProfile activeProfile = ELinuxCullingProfile.Original;
		private static bool isSuspended = true;
		private static int showQueueIndex;
		private static int importantQueueIndex;
		private static int hideQueueIndex;
		private static float nextDetailedMetricsTime;

		internal static int PendingQueueCount => showQueue.Count - showQueueIndex + importantQueue.Count - importantQueueIndex + hideQueue.Count - hideQueueIndex;

		internal static bool TryGetEntry(LevelObject levelObject, out CullingEntry entry)
		{
			return entries.TryGetValue(levelObject, out entry);
		}
	}

	internal readonly struct CullingQueueItem
	{
		public readonly CullingEntry Entry;
		public readonly uint Generation;
		public readonly bool DesiredVisible;

		public CullingQueueItem(CullingEntry entry, uint generation, bool desiredVisible)
		{
			Entry = entry;
			Generation = generation;
			DesiredVisible = desiredVisible;
		}
	}

	internal sealed class CullingEntry
	{
		public LevelObject LevelObject { get; }
		public Vector2Int Region;
		public Bounds CombinedBounds;
		public Vector3 Position;
		public GameplayCullableCategory Category;
		public float HideDistance;
		public float ReactivateDistance;
		public readonly List<Renderer> Renderers = new List<Renderer>();
		public readonly List<Light> Lights = new List<Light>();
		public readonly List<ParticleSystem> Particles = new List<ParticleSystem>();
		public readonly List<Animator> Animators = new List<Animator>();
		public bool IsCriticalGameplay;
		public bool HasInteractable;
		public bool HasRubble;
		public bool HasImportantCollision;
		public bool HasTrigger;
		public bool IsNetworkRelevant;
		public bool IsMissionObject;
		public bool DesiredVisible = true;
		public bool AppliedVisible = true;
		public bool IsQueued;
		public uint QueueGeneration;

		public CullingEntry(LevelObject levelObject)
		{
			LevelObject = levelObject;
			Transform transform = levelObject.transform;
			transform.GetComponentsInChildren(true, Renderers);
			transform.GetComponentsInChildren(true, Lights);
			transform.GetComponentsInChildren(true, Particles);
			transform.GetComponentsInChildren(true, Animators);
			colliders.Clear();
			transform.GetComponentsInChildren(true, colliders);
			for (int i = 0; i < colliders.Count; ++i)
				HasTrigger |= colliders[i] != null && colliders[i].isTrigger;

			ObjectAsset asset = levelObject.asset;
			HasInteractable = asset != null && asset.interactability != EObjectInteractability.NONE;
			HasRubble = asset != null && asset.rubble != EObjectRubble.NONE;
			HasImportantCollision = asset != null && asset.isCollisionImportant;
			IsNetworkRelevant = asset != null && asset.interactabilityRemote;
			IsMissionObject = asset != null && asset.visibilityConditionsList.conditions != null && asset.visibilityConditionsList.conditions.Length > 0;
			bool isNpc = asset != null && asset.type == EObjectType.NPC;
			IsCriticalGameplay = GameplayCullingManager.IsCritical(HasInteractable, HasRubble, isNpc, HasImportantCollision, HasTrigger, IsNetworkRelevant, IsMissionObject);
			Category = GameplayCullingManager.Classify(asset != null ? asset.type : EObjectType.LARGE, asset != null && asset.foliage.isValid, Lights.Count > 0, Particles.Count > 0, Animators.Count > 0, IsCriticalGameplay);

			shadowStates = new ShadowCastingMode[Renderers.Count];
			for (int i = 0; i < Renderers.Count; ++i)
				shadowStates[i] = Renderers[i] != null ? Renderers[i].shadowCastingMode : ShadowCastingMode.Off;
			lightEnabledStates = new bool[Lights.Count];
			lightShadowStates = new LightShadows[Lights.Count];
			for (int i = 0; i < Lights.Count; ++i)
			{
				if (Lights[i] == null)
					continue;
				lightEnabledStates[i] = Lights[i].enabled;
				lightShadowStates[i] = Lights[i].shadows;
			}
			particlePlayingStates = new bool[Particles.Count];
			for (int i = 0; i < Particles.Count; ++i)
				particlePlayingStates[i] = Particles[i] != null && Particles[i].isPlaying;
			animatorEnabledStates = new bool[Animators.Count];
			for (int i = 0; i < Animators.Count; ++i)
				animatorEnabledStates[i] = Animators[i] != null && Animators[i].enabled;
			RefreshPositionAndBounds();
			ConfigureDistances(ELinuxCullingProfile.Agressivo);
		}

		public void RefreshPositionAndBounds()
		{
			if (LevelObject == null || LevelObject.transform == null)
				return;
			Position = LevelObject.transform.position;
			Region = Regions.GetCoordinateVector2Int(Position);
			bool hasBounds = false;
			for (int i = 0; i < Renderers.Count; ++i)
			{
				Renderer renderer = Renderers[i];
				if (renderer == null)
					continue;
				if (hasBounds)
					CombinedBounds.Encapsulate(renderer.bounds);
				else
				{
					CombinedBounds = renderer.bounds;
					hasBounds = true;
				}
			}
			if (!hasBounds)
				CombinedBounds = new Bounds(Position, Vector3.zero);
		}

		public void ConfigureDistances(ELinuxCullingProfile profile)
		{
			switch (profile)
			{
				case ELinuxCullingProfile.Basico:
					HideDistance = 180.0f;
					ReactivateDistance = 165.0f;
					break;
				case ELinuxCullingProfile.Experimental:
					HideDistance = 110.0f;
					ReactivateDistance = 95.0f;
					break;
				default:
					HideDistance = 130.0f;
					ReactivateDistance = 115.0f;
					break;
			}
			if (Category == GameplayCullableCategory.LargeStructure)
			{
				HideDistance *= 1.5f;
				ReactivateDistance *= 1.5f;
			}
			else if (Category == GameplayCullableCategory.StaticProp)
			{
				HideDistance *= 1.2f;
				ReactivateDistance *= 1.2f;
			}
		}

		public bool ApplyIncrementally(ref CullingWorkBudget budget)
		{
			if (AppliedVisible != DesiredVisible)
			{
				if (budget.ObjectVisibilityChanges <= 0)
					return false;
				LevelObject.SetIsVisibleByGameplayBudget(DesiredVisible);
				AppliedVisible = DesiredVisible;
				--budget.ObjectVisibilityChanges;
				shadowIndex = lightIndex = particleIndex = animatorIndex = 0;
			}

			while (shadowIndex < Renderers.Count && budget.Shadows > 0)
			{
				Renderer renderer = Renderers[shadowIndex];
				if (renderer != null)
					renderer.shadowCastingMode = DesiredVisible ? shadowStates[shadowIndex] : ShadowCastingMode.Off;
				++shadowIndex;
				--budget.Shadows;
			}
			while (lightIndex < Lights.Count && budget.Lights > 0)
			{
				Light light = Lights[lightIndex];
				if (light != null)
				{
					light.enabled = DesiredVisible && lightEnabledStates[lightIndex];
					light.shadows = DesiredVisible ? lightShadowStates[lightIndex] : LightShadows.None;
				}
				++lightIndex;
				--budget.Lights;
			}
			while (particleIndex < Particles.Count && budget.Particles > 0)
			{
				ParticleSystem particle = Particles[particleIndex];
				if (particle != null)
				{
					if (DesiredVisible && particlePlayingStates[particleIndex])
						particle.Play(true);
					else if (!DesiredVisible)
						particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
				}
				++particleIndex;
				--budget.Particles;
			}
			while (animatorIndex < Animators.Count && budget.Animators > 0)
			{
				Animator animator = Animators[animatorIndex];
				if (animator != null)
					animator.enabled = DesiredVisible && animatorEnabledStates[animatorIndex];
				++animatorIndex;
				--budget.Animators;
			}

			bool complete = shadowIndex >= Renderers.Count && lightIndex >= Lights.Count && particleIndex >= Particles.Count && animatorIndex >= Animators.Count;
			return complete;
		}

		public void RestoreImmediately()
		{
			if (LevelObject != null)
				LevelObject.SetIsVisibleByGameplayBudget(true);
			for (int i = 0; i < Renderers.Count; ++i)
				if (Renderers[i] != null)
					Renderers[i].shadowCastingMode = shadowStates[i];
			for (int i = 0; i < Lights.Count; ++i)
			{
				if (Lights[i] == null)
					continue;
				Lights[i].enabled = lightEnabledStates[i];
				Lights[i].shadows = lightShadowStates[i];
			}
			for (int i = 0; i < Particles.Count; ++i)
				if (Particles[i] != null && particlePlayingStates[i] && !Particles[i].isPlaying)
					Particles[i].Play(true);
			for (int i = 0; i < Animators.Count; ++i)
				if (Animators[i] != null)
					Animators[i].enabled = animatorEnabledStates[i];
			DesiredVisible = AppliedVisible = true;
			IsQueued = false;
			unchecked
			{
				++QueueGeneration;
			}
			shadowIndex = lightIndex = particleIndex = animatorIndex = 0;
		}

		private readonly ShadowCastingMode[] shadowStates;
		private readonly bool[] lightEnabledStates;
		private readonly LightShadows[] lightShadowStates;
		private readonly bool[] particlePlayingStates;
		private readonly bool[] animatorEnabledStates;
		private int shadowIndex;
		private int lightIndex;
		private int particleIndex;
		private int animatorIndex;
		private static readonly List<Collider> colliders = new List<Collider>();
	}
}
