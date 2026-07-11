////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
namespace SDG.Unturned.LinuxPerformance
{
	public static class ReactiveMaskGenerator
	{
		public static ELinuxFeatureState State => ELinuxFeatureState.Unsupported;
		public static string StateReason => "Máscaras reactive/composition dependem da captura opaca/final e backend temporal; ainda não geradas.";
	}
}
