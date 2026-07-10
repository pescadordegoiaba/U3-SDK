////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEditor;

public partial class BuildMethods
{
	/// <summary>
	/// Run Linux-only build actions from the Unity command line.
	/// <example>-executeMethod BuildMethods.runLinuxBuildFromCommandLine</example>
	/// </summary>
	public static void runLinuxBuildFromCommandLine()
	{
		scriptsOnly = hasBuildCommandLineSwitch("-scriptsOnly");

		if (!runBuildStep("Building Linux64 server", buildLinux64DedicatedServer))
		{
			EditorApplication.Exit(1);
			return;
		}

		if (!runBuildStep("Building Linux64 player", buildLinux64Player))
		{
			EditorApplication.Exit(1);
			return;
		}

		EditorApplication.Exit(0);
	}
}
