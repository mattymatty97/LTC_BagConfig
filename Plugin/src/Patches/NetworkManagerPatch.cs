using BagConfig.Networking;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace BagConfig.Patches;

[HarmonyPatch]
internal class NetworkManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Initialize))]
    private static void AfterInitialize()
    {
        BagConfig.Log.LogInfo("Registering Named Messages!");
        NamedMessages.RegisterNamedMessages();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), "SetInstanceValuesBackToDefault")]
    public static void SetInstanceValuesBackToDefault()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
            return;

        BagConfig.Log.LogInfo("Unregistering Named Messages!");
        NamedMessages.UnregisterNamedMessages();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.SetSingleton))]
    private static void RegisterPrefab()
    {
        if (PluginConfig.Host.AllowVanilla.Value)
            return;

        var prefab = new GameObject(MyPluginInfo.PLUGIN_NAME + " Prefab");
        prefab.hideFlags |= HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(prefab);
        var networkObject = prefab.AddComponent<NetworkObject>();
        networkObject.GlobalObjectIdHash = MyPluginInfo.PLUGIN_GUID.Hash32();

        NetworkManager.Singleton.PrefabHandler.AddNetworkPrefab(prefab);
    }
}
