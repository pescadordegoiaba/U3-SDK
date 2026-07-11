////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public static class ReactiveMaskGenerator
	{
		public static ELinuxFeatureState State => ELinuxFeatureState.Disabled;
		public static bool IsIncomplete => true;
		public static string StateReason => "Reactive mask neutra é fornecida, mas a classificação de transparência/partículas permanece incompleta; composition mask ainda é nula.";
	}
}
