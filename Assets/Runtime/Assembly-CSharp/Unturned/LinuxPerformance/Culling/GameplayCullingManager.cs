////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public enum GameplayCullableCategory
	{
		NeverCull,
		CriticalGameplay,
		DynamicActor,
		Vehicle,
		Interactable,
		StaticStructure,
		StaticProp,
		SmallDecoration,
		Foliage,
		Particle,
		Audio,
		Light,
		Animator,
	}

	public static class GameplayCullingManager
	{
		public static string Status => "Culling avançado preparado; integração profunda com atores dinâmicos e HZB permanece experimental/desligada.";
	}
}
