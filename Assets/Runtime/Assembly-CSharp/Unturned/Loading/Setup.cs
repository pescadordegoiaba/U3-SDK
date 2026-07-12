////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned
{
	public class Setup : MonoBehaviour
	{
		public UnturnedPostProcess postProcess;

		// Settings to help with tracking down unity crashes during start.
		public bool awakeDedicator = true;
		public bool awakeLogs = true;
		public bool awakeModuleHook = true;
		public bool awakeProvider = true;
		public bool startModuleHook = true;
		public bool startProvider = true;

		private void Awake()
		{
			UnturnedPlayerLoop.initialize();
			ThreadUtil.setupGameThread();

#if UNITY_EDITOR
			CommandLineFlag.applyEditorPreferencesToAllFlags();
#endif // UNITY_EDITOR

			if (awakeDedicator)
				GetComponent<Dedicator>().awake();

			if (awakeLogs)
				GetComponent<Logs>().awake();

			SDG.Framework.Modules.ModuleHook moduleHook = GetComponent<SDG.Framework.Modules.ModuleHook>();
			if (awakeModuleHook)
				moduleHook.awake();

			if (awakeProvider)
				GetComponent<Provider>().awake();
			if (Provider.WasQuitGameCalled)
			{
				UnturnedLog.error("Startup stopped after mandatory Provider dependency failure. ModuleHook, Glazier and LoadingUI will not be initialized.");
				return;
			}

			if (startModuleHook)
				moduleHook.start();
			if (moduleHook.HasFatalStartupError)
			{
				UnturnedLog.error("Startup aborted because mandatory core module initialization failed. Glazier and LoadingUI will not be created.");
#if UNITY_EDITOR
				UnityEditor.EditorApplication.ExitPlaymode();
#else
				Application.Quit(2);
#endif
				return;
			}

			if (startProvider)
				GetComponent<Provider>().start();

			if (!Dedicator.IsDedicatedServer)
			{
				GlazierFactory.Create();
				if (Glazier.Get() == null)
				{
					UnturnedLog.error("Startup fatal: GlazierFactory did not register a mandatory Glazier implementation. LoadingUI will not be created.");
#if UNITY_EDITOR
					UnityEditor.EditorApplication.ExitPlaymode();
#else
					Application.Quit(3);
#endif
					return;
				}
			}

			UnturnedPathfinding.Initialize();
		}

		private void Start()
		{
			postProcess.initialize();

			if (!Dedicator.IsDedicatedServer)
			{
				MenuSettings.load();
				GraphicsSettings.applyResolution();

				// Applying at startup prevents high CPU usage from unlimited FPS
				// during async asset bundle load. (public issue #3825)
				GraphicsSettings.ApplyVSyncAndTargetFrameRate();

				LoadingUI.updateScene();
			}
		}
	}
}
