////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance
{
	public static class MotionAdaptiveResolutionController
	{
		public static float AngularVelocityDegrees { get; private set; }
		public static float TranslationalVelocity { get; private set; }
		public static float MotionScale { get; private set; } = 1.0f;

		public static void Update(Camera camera, ref PerformanceSettings settings)
		{
			if (!settings.MotionAdaptiveResolution || camera == null || MenuConfigurationUI.active || MenuDashboardUI.active)
			{
				MotionScale = Mathf.MoveTowards(MotionScale, 1.0f, Time.unscaledDeltaTime * 2.0f);
				return;
			}

			Transform transform = camera.transform;
			if (!hasPrevious)
			{
				previousPosition = transform.position;
				previousRotation = transform.rotation;
				hasPrevious = true;
				return;
			}

			float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
			AngularVelocityDegrees = Quaternion.Angle(previousRotation, transform.rotation) / dt;
			TranslationalVelocity = (transform.position - previousPosition).magnitude / dt;
			previousPosition = transform.position;
			previousRotation = transform.rotation;

			float target = (AngularVelocityDegrees > 140.0f || TranslationalVelocity > 8.0f) ? 0.82f : 1.0f;
			float speed = target < MotionScale ? 3.5f : 1.25f;
			MotionScale = Mathf.MoveTowards(MotionScale, target, Time.unscaledDeltaTime * speed);
			settings.CustomRenderScale = Mathf.Clamp(settings.GetPresetScale() * MotionScale, 0.333f, 1.0f);
			settings.FsrQualityPreset = EFsrQualityPreset.Custom;
		}

		private static bool hasPrevious;
		private static Vector3 previousPosition;
		private static Quaternion previousRotation;
	}
}
