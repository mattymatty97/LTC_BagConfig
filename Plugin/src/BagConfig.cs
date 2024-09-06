using System;
using System.Collections.Generic;
using BagConfig.Dependency;
using BagConfig.Patches;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.RuntimeDetour;

namespace BagConfig
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("BMX.LobbyCompatibility", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig", Flags:BepInDependency.DependencyFlags.SoftDependency)]
    internal class BagConfig : BaseUnityPlugin
    {
		
	    internal static readonly ISet<Hook> Hooks = new HashSet<Hook>();
	    internal static readonly Harmony Harmony = new Harmony(GUID);
	    
        public static BagConfig INSTANCE { get; private set; }
		
        public const string GUID = MyPluginInfo.PLUGIN_GUID;
        public const string NAME = MyPluginInfo.PLUGIN_NAME;
        public const string VERSION = MyPluginInfo.PLUGIN_VERSION;

        internal static ManualLogSource Log;
            
        private void Awake()
        {
			
	        INSTANCE = this;
            Log = Logger;
            try
            {
				if (LobbyCompatibilityChecker.Enabled)
					LobbyCompatibilityChecker.Init();
				
				Log.LogInfo("Initializing Configs");

				PluginConfig.Init();
				PatchLateJoin.Init();
				
				Log.LogInfo("Patching Methods");
				
				Harmony.PatchAll();
				
				BeltBagPatch.Patch();
				
				Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
				
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }
    }
}
