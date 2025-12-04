using System.Collections.Generic;
using BagConfig.Patches;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BagConfig.Networking;

internal static class NamedMessages
{
    private static readonly string BaseName = typeof(NamedMessages).FullName;
    private static readonly string HostPresentClientRpcMessage = $"{BaseName}|HostPresentClientRpc";
    private static readonly string FixBagsClientRpcMessage = $"{BaseName}|FixBagsClientRpc";

    internal static void RegisterNamedMessages()
    {
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(HostPresentClientRpcMessage,
            OnHostPresentClientRpc);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(FixBagsClientRpcMessage,
            OnFixBagsClientRpc);
    }

    internal static void UnregisterNamedMessages()
    {
        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HostPresentClientRpcMessage);
        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(FixBagsClientRpcMessage);
    }

    internal static void HostPresentClientRpc(IReadOnlyList<ulong> targets = null)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        var buffer = new FastBufferWriter(0, Allocator.Temp);

        if (targets == null)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(HostPresentClientRpcMessage, buffer);
        else
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(HostPresentClientRpcMessage, targets,
                buffer);
    }

    private static void OnHostPresentClientRpc(ulong senderId, FastBufferReader data)
    {
        if (senderId != NetworkManager.ServerClientId)
            return;

        BeltBagPatch.Enabled = true;
    }

    internal static void FixBagsClientRpc(IReadOnlyList<ulong> targets = null)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        var buffer = new FastBufferWriter(0, Allocator.Temp);

        if (targets == null)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(FixBagsClientRpcMessage, buffer);
        else
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(FixBagsClientRpcMessage, targets,
                buffer);
    }

    private static void OnFixBagsClientRpc(ulong senderId, FastBufferReader data)
    {
        if (senderId != NetworkManager.ServerClientId)
            return;

        if (StartOfRound.Instance == null)
            return;

        BagConfig.Log.LogWarning("Host requested all beltBags to be freed!");
        var allBags = Object.FindObjectsByType<BeltBagItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var bag in allBags)
        {
            BagConfig.Log.LogDebug($"Fixing status of 0x{bag.NetworkObjectId:X}, was: {bag.tryingAddToBag}");
            bag.tryingAddToBag = false;
            bag.tryingCheckBag = false;
        }
    }
}
