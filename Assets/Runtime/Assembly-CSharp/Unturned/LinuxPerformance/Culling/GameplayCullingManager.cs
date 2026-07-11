////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public enum GameplayCullableCategory
	{
		NeverCull,
		LargeStructure,
		StaticProp,
		SmallDecoration,
		Foliage,
		CosmeticLight,
		CosmeticParticle,
		CosmeticAnimator,
	}

	public static class GameplayCullingManager
	{
		public static string Status => "Culling visual regional ativo; colisores, triggers e objetos de gameplay permanecem intocados.";

		internal static bool IsCritical(bool hasInteractable, bool hasRubble, bool isNpc, bool isCollisionImportant, bool hasTrigger, bool isNetworkRelevant, bool isMissionObject)
		{
			return hasInteractable || hasRubble || isNpc || isCollisionImportant || hasTrigger || isNetworkRelevant || isMissionObject;
		}

		internal static GameplayCullableCategory Classify(EObjectType type, bool hasFoliage, bool hasLight, bool hasParticle, bool hasAnimator, bool isCritical)
		{
			if (isCritical)
				return GameplayCullableCategory.NeverCull;
			if (hasFoliage)
				return GameplayCullableCategory.Foliage;
			if (type == EObjectType.LARGE)
				return GameplayCullableCategory.LargeStructure;
			if (type == EObjectType.MEDIUM)
				return GameplayCullableCategory.StaticProp;
			if (hasLight)
				return GameplayCullableCategory.CosmeticLight;
			if (hasParticle)
				return GameplayCullableCategory.CosmeticParticle;
			if (hasAnimator)
				return GameplayCullableCategory.CosmeticAnimator;
			return GameplayCullableCategory.SmallDecoration;
		}

		internal static bool ShouldBeVisible(bool isVisible, float squaredDistance, float hideDistance, float reactivateDistance)
		{
			float threshold = isVisible ? hideDistance : reactivateDistance;
			return squaredDistance <= threshold * threshold;
		}

		internal static bool IsRegionRelevant(Vector2Int cameraRegion, Vector2Int objectRegion)
		{
			int dx = Mathf.Abs(cameraRegion.x - objectRegion.x);
			int dy = Mathf.Abs(cameraRegion.y - objectRegion.y);
			return dx <= 1 && dy <= 1;
		}
	}

	internal struct CullingWorkBudget
	{
		/// <summary>LevelObject visibility transitions, not individual Renderer components.</summary>
		public int ObjectVisibilityChanges;
		public int Shadows;
		public int Lights;
		public int Particles;
		public int Animators;

		public static CullingWorkBudget CreateDefault()
		{
			return new CullingWorkBudget()
			{
				ObjectVisibilityChanges = 32,
				Shadows = 16,
				Lights = 8,
				Particles = 8,
				Animators = 8,
			};
		}
	}
}
