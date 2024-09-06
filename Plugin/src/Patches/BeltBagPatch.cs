using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Unity.Netcode;
using UnityEngine;

namespace BagConfig.Patches;

[HarmonyPatch]
internal static class BeltBagPatch
{
    internal static void Patch()
    {
        var beltInteractMethod = AccessTools.Method(typeof(BeltBagItem), nameof(BeltBagItem.ItemInteractLeftRight));
        var overrideInteractMethod = AccessTools.Method(typeof(BeltBagPatch), nameof(OverrideGrab));

        BagConfig.Hooks.Add(new Hook(beltInteractMethod, overrideInteractMethod, new HookConfig { Priority = -999 }));
    }

    private static readonly Action<GrabbableObject, bool> BaseInteractMethod;

    static BeltBagPatch()
    {
        var method = AccessTools.Method(typeof(GrabbableObject), nameof(GrabbableObject.ItemInteractLeftRight));
        var dm = new DynamicMethod("Base.ItemInteractLeftRight", null, [typeof(GrabbableObject), typeof(bool)],
            typeof(BeltBagItem));
        var gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldarg_1);
        gen.Emit(OpCodes.Call, method);
        gen.Emit(OpCodes.Ret);

        BaseInteractMethod = (Action<GrabbableObject, bool>)dm.CreateDelegate(typeof(Action<GrabbableObject, bool>));
    }

    private static IEnumerator EmptyBagCoroutine(BeltBagItem @this)
    {
        while (@this.objectsInBag.Count > 0)
        {
            @this.RemoveObjectFromBag(0);
            yield return new WaitForEndOfFrame();
        }
    }

    private static void OverrideGrab(Action<BeltBagItem, bool> orig, BeltBagItem @this, bool right)
    {
        BaseInteractMethod.Invoke(@this, right);

        if (right)
        {
            if (!PluginConfig.Misc.DropAll.Value)
                return;
            //dump all items!
            @this.StartCoroutine(EmptyBagCoroutine(@this));
        }
        else
        {
            if (@this.playerHeldBy == null || @this.tryingAddToBag)
                return;

            if (@this.objectsInBag.Count >= PluginConfig.Limits.Capacity.Value)
            {
                if (PluginConfig.Misc.Tooltip.Value)
                    HUDManager.Instance.DisplayTip("Belt bag Info", "This bag is Full!");
                return;
            }

            if (!Physics.Raycast(@this.playerHeldBy.gameplayCamera.transform.position,
                    @this.playerHeldBy.gameplayCamera.transform.forward,
                    out var raycastHit,
                    PluginConfig.Misc.GrabRange.Value,
                    GameNetworkManager.Instance.localPlayerController.interactableObjectsMask))
                return;
            
            BagConfig.Log.LogDebug($"Grab Hit: {raycastHit.collider.transform.parent?.name ?? ""}.{raycastHit.collider.gameObject.name}");

            if (raycastHit.collider.gameObject.layer == 8 || raycastHit.collider.tag != "PhysicsProp")
            {
                return;
            }

            var component = raycastHit.collider.gameObject.GetComponent<GrabbableObject>();

            if (!@this.CanBePutInBag(component))
                return;

            if (@this.CheckBagFilters(component))
            {
                @this.TryAddObjectToBag(component);
                return;
            }

            if (PluginConfig.Misc.Tooltip.Value)
            {
                HUDManager.Instance.DisplayTip("Belt bag Info",
                    $"Cannot store {component.itemProperties.itemName} inside of the bag!");
            }
        }
    }

    private static bool CanBePutInBag(this BeltBagItem @this, GrabbableObject grabbable)
    {
        return grabbable && grabbable != @this &&
               !grabbable.isHeld && !grabbable.isHeldByEnemy &&
               /*Maneater!*/grabbable.itemProperties.itemId != 123984;
    }

    private static bool CheckBagFilters(this BeltBagItem @this, GrabbableObject grabbable)
    {
        var config = grabbable.GetBagCategory();

        var limit = config.Limit.Value;

        if (limit < 0)
            return config.Allow.Value;

        if (limit == 0)
            return false;

        var memory = CategoryMemory.GetOrCreateValue(@this);
        if (memory.TryGetValue(config, out var count) && count.Count >= limit)
        {
            return false;
        }

        return config.Allow.Value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void AddBagTooltip(StartOfRound __instance)
    {
        if (!PluginConfig.Misc.DropAll.Value)
            return;

        foreach (var item in __instance.allItemsList.itemsList)
        {
            if (!item.spawnPrefab || !item.spawnPrefab.TryGetComponent<BeltBagItem>(out _))
                continue;

            Array.Resize(ref item.toolTips, item.toolTips.Length + 1);

            item.toolTips[^1] = "Empty Bag: [E]";
            break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.PutObjectInBagLocalClient))]
    private static void TriggerHeldActions(BeltBagItem __instance, GrabbableObject gObject)
    {
        if (gObject is LungProp lung)
        {
            if (lung.isLungDocked)
            {
                lung.isLungDocked = false;
                if (lung.disconnectAnimation != null)
                    lung.StopCoroutine(lung.disconnectAnimation);
                lung.disconnectAnimation = lung.StartCoroutine(lung.DisconnectFromMachinery());
            }

            if (lung.isLungDockedInElevator)
            {
                lung.isLungDockedInElevator = false;
                lung.gameObject.GetComponent<AudioSource>().PlayOneShot(lung.disconnectSFX);
            }
        }
    }

    //Handle Limits!

    private class CategoryCount
    {
        public int Count;
    }

    private static readonly
        ConditionalWeakTable<BeltBagItem, ConditionalWeakTable<PluginConfig.CategoryConfig, CategoryCount>>
        CategoryMemory = [];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.PutObjectInBagLocalClient))]
    private static void TrackAdd(BeltBagItem __instance, GrabbableObject gObject)
    {
        var config = gObject.GetBagCategory();
        var memory = CategoryMemory.GetOrCreateValue(__instance);
        memory.GetOrCreateValue(config).Count += 1;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.RemoveFromBagLocalClientNonElevatorParent))]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.RemoveFromBagLocalClient))]
    private static void TrackRemove(BeltBagItem __instance, NetworkObjectReference objectRef)
    {
        var memory = CategoryMemory.GetOrCreateValue(__instance);
        if (objectRef.TryGet(out var networkObject) && networkObject.TryGetComponent<GrabbableObject>(out var gObject))
        {
            var config = gObject.GetBagCategory();
            memory.GetOrCreateValue(config).Count -= 1;
        }

        if (__instance.objectsInBag.Count == 0)
        {
            memory.Clear();
        }
    }

    //ServerSide Checks

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.TryAddObjectToBagServerRpc))]
    private static bool EnforceLimits(BeltBagItem __instance, NetworkObjectReference netObjectRef, int playerWhoAdded)
    {
        var networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return true;
        
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Server ||
            !networkManager.IsServer && !networkManager.IsHost)
            return true;
        
        if (!netObjectRef.TryGet(out var networkObject)) 
            return true;
        
        var gObject = networkObject.GetComponent<GrabbableObject>();
        if (gObject == null || gObject.isHeld || gObject.heldByPlayerOnServer ||
            gObject.isHeldByEnemy) 
            return true;
            
        if (PluginConfig.Host.Capacity.Value)
        {
            if (__instance.objectsInBag.Count >= PluginConfig.Limits.Capacity.Value)
            {
                __instance.CancelAddObjectToBagClientRpc(playerWhoAdded);
                return false;
            }
        }

        if (PluginConfig.Host.Category.Value)
        {
            if (!__instance.CheckBagFilters(gObject))
            {
                __instance.CancelAddObjectToBagClientRpc(playerWhoAdded);
                return false;
            }
        }

        if (PluginConfig.Host.Range.Value)
        {
            if (Vector3.Distance(
                    gObject.transform.position,
                    __instance.playerHeldBy?.gameplayCamera.transform.position ?? Vector3.positiveInfinity
                ) > PluginConfig.Misc.GrabRange.Value)
            {
                __instance.CancelAddObjectToBagClientRpc(playerWhoAdded);
                return false;
            }
        }

        return true;
    }
    
    // dropcode!

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.RemoveObjectFromBag))]
    private static IEnumerable<CodeInstruction> FixDrop(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();

        var floorPositionMethod = AccessTools.Method(typeof(GrabbableObject), nameof(GrabbableObject.GetItemFloorPosition));
        var padPositionMethod = AccessTools.Method(typeof(BeltBagPatch), nameof(FixVerticalOffset));

        var matcher = new CodeMatcher(codes, ilGenerator);

        matcher.MatchForward(true, new CodeMatch(OpCodes.Call, floorPositionMethod));

        if (matcher.IsInvalid)
        {
            BagConfig.Log.LogError("Patch RemoveObjectFromBag, Fail - 1 ");
            return codes;
        }

        matcher.Advance(1);
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0));
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1));
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, padPositionMethod));
        
        return matcher.Instructions();
    }

    private static Vector3 FixVerticalOffset(Vector3 position, BeltBagItem beltBagItem, int index)
    {

        if (beltBagItem.objectsInBag.Count <= index)
            return position;

        var grabbableObject = beltBagItem.objectsInBag[index];

        if (!grabbableObject)
            return position;
        
        position += Vector3.down * beltBagItem.itemProperties.verticalOffset;
        position += Vector3.up * grabbableObject.itemProperties.verticalOffset;
        return position;
    }
    
}