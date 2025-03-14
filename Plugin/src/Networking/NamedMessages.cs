using System.Collections.Generic;
using BagConfig.Patches;
using Unity.Collections;
using Unity.Netcode;

namespace BagConfig.Networking;

internal static class NamedMessages
{
    private static readonly string BaseName = typeof(NamedMessages).FullName;
    private static readonly string HostPresentClientRpcMessage = $"{BaseName}|HostPresentClientRpc";

    internal static void RegisterNamedMessages()
    {
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(HostPresentClientRpcMessage,
            OnHostPresentClientRpc);
    }

    internal static void UnregisterNamedMessages()
    {
        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(HostPresentClientRpcMessage);
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
}
