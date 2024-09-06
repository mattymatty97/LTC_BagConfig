using System;
using HarmonyLib;
using Unity.Netcode;

namespace BagConfig.Patches;

internal static class PatchLateJoin
{
    private static uint _tryAddObjectToBagClientRpc;
    
    internal static void Init()
    {
        var target = AccessTools.Method(typeof(BeltBagItem), nameof(BeltBagItem.TryAddObjectToBagClientRpc));
        
        if (!Utils.TryGetRpcID(target, out _tryAddObjectToBagClientRpc))
        {
            throw new MissingMemberException(nameof(BeltBagItem), nameof(BeltBagItem.TryAddObjectToBagClientRpc));
        }
    }
    
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncAlreadyHeldObjectsClientRpc))]
    private static void SyncItemsInBags(StartOfRound __instance,
        NetworkObjectReference[] gObjects, int joiningClientId)
    {
        var networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (__instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Client ||
            (!networkManager.IsServer && !networkManager.IsHost))
            return;

        foreach (var gObject in gObjects)
        {
            if (!gObject.TryGet(out var networkObject))
                continue;
            
            if (!networkObject.TryGetComponent(out BeltBagItem beltBagItem))
                continue;

            foreach (var gComponent in beltBagItem.objectsInBag)
            {
                //beltBagItem.TryAddObjectToBagClientRpc(grabbableObject.NetworkObject, (int)beltBagItem.playerHeldBy.actualClientId);
                NetworkObjectReference reference = gComponent.NetworkObject;
                var clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = [(ulong)joiningClientId]}};
                var bufferWriter = beltBagItem.__beginSendClientRpc(_tryAddObjectToBagClientRpc, clientRpcParams, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in reference);
                BytePacker.WriteValueBitPacked(bufferWriter, beltBagItem.playerHeldBy.actualClientId);
                beltBagItem.__endSendClientRpc(ref bufferWriter, _tryAddObjectToBagClientRpc, clientRpcParams, RpcDelivery.Reliable);
            }
        }
    }
}