using System;
using System.Collections;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.RuntimeDetour;
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

    private static readonly RaycastHit[] RaycastHits = new RaycastHit[15];
    
    private static readonly Action<GrabbableObject, bool> BaseInteractMethod;

    static BeltBagPatch()
    {
        var method = AccessTools.Method(typeof(GrabbableObject), nameof(GrabbableObject.ItemInteractLeftRight));
        var dm = new DynamicMethod("Base.ItemInteractLeftRight", null, [typeof(GrabbableObject), typeof(bool)], typeof(BeltBagItem));
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
        if (!PluginConfig.General.Enabled.Value)
        {
            orig.Invoke(@this, right);
            return;
        }
        
        BaseInteractMethod.Invoke(@this, right);

        if (right)
        {
            if (!PluginConfig.General.DropAll.Value) 
                return;
            //dump all items!
            @this.StartCoroutine(EmptyBagCoroutine(@this));
        }
        else
        {
            if (@this.playerHeldBy == null || @this.tryingAddToBag)
                return;

            if (@this.objectsInBag.Count >= PluginConfig.Inventory.SlotCount.Value)
            {
                if (PluginConfig.General.Tooltip.Value)
                    HUDManager.Instance.DisplayTip("Belt bag Info", "This bag is Full!");
                return;
            }

            GrabbableObject component = null;
            int index = 0;
            int hits = 0;
            bool hitWall = false;
            RaycastHit curr;
            GrabbableObject currGrabbable;
            switch (PluginConfig.General.DetectionMode.Value)
            {
                case PluginConfig.DetectionMode.Multiple:
                    hits = Physics.RaycastNonAlloc(@this.playerHeldBy.gameplayCamera.transform.position,
                        @this.playerHeldBy.gameplayCamera.transform.forward, RaycastHits,
                        PluginConfig.General.GrabRange.Value,
                        GameNetworkManager.Instance.localPlayerController.interactableObjectsMask,
                        QueryTriggerInteraction.Ignore);

                    if (hits == 0)
                        return;

                    for (index = 0; index < hits; index++)
                    {
                        curr = RaycastHits[index];

                        if (curr.collider.gameObject.layer == 8 || curr.collider.tag != "PhysicsProp")
                        {
                            hitWall = true;
                            break;
                        }

                        currGrabbable = curr.collider.gameObject.GetComponent<GrabbableObject>();

                        if (!@this.CanBePutInBag(currGrabbable))
                            continue;

                        if (!CheckBagFilters(currGrabbable))
                            continue;

                        component = currGrabbable;
                        break;
                    }
                    break;
                case PluginConfig.DetectionMode.Single:
                    if(!Physics.Raycast(@this.playerHeldBy.gameplayCamera.transform.position,
                        @this.playerHeldBy.gameplayCamera.transform.forward,
                        out RaycastHits[0],
                        PluginConfig.General.GrabRange.Value,
                        GameNetworkManager.Instance.localPlayerController.interactableObjectsMask,
                        QueryTriggerInteraction.Ignore))
                        return;

                    hits = 1;
                    
                    curr = RaycastHits[0];
                    
                    if (curr.collider.gameObject.layer == 8 || curr.collider.tag != "PhysicsProp")
                    {
                        hitWall = true;
                        break;
                    }

                    currGrabbable = curr.collider.gameObject.GetComponent<GrabbableObject>();

                    if (!@this.CanBePutInBag(currGrabbable))
                        break;

                    if (!CheckBagFilters(currGrabbable))
                        break;

                    component = currGrabbable;
                    
                    
                    break;
            }

            if (index == 0 && hitWall)
                return;

            if (component)
                @this.TryAddObjectToBag(component);
            else if (PluginConfig.General.Tooltip.Value)
            {
                var collectable = RaycastHits
                    .Take(hits)
                    .Select(hit => hit.collider.gameObject.GetComponent<GrabbableObject>())
                    .FirstOrDefault(@this.CanBePutInBag);

                if (collectable)
                    HUDManager.Instance.DisplayTip("Belt bag Info",
                        $"Cannot store {collectable.itemProperties.itemName} inside of the bag!");
            }
        }
    }

    private static bool CanBePutInBag(this BeltBagItem @this, GrabbableObject grabbable)
    {
        return grabbable && grabbable != @this &&
               !grabbable.isHeld && !grabbable.isHeldByEnemy &&
               /*Maneater!*/grabbable.itemProperties.itemId != 123984;
    }
    
    private static bool CheckBagFilters(GrabbableObject grabbable)
    {
        var name = grabbable.itemProperties.itemName;

        if (PluginConfig.Inventory.BlackListedItems.Contains(name))
            return false;
        
        if (PluginConfig.Inventory.WhitelistedItems.Contains(name))
            return true;

        var isScrap = grabbable.itemProperties.isScrap;
        var isTwoHanded = grabbable.itemProperties.twoHanded;

        if (!isScrap && PluginConfig.Inventory.Tools.Value)
            return true;
        
        if (isScrap && !isTwoHanded && PluginConfig.Inventory.Scrap.Value)
            return true;
        
        if (isScrap && isTwoHanded &&  PluginConfig.Inventory.Scrap.Value && PluginConfig.Inventory.TwoHanded.Value)
            return true;
        
        return false;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void AddBagTooltip(StartOfRound __instance)
    {
        if (!PluginConfig.General.DropAll.Value)
            return;
        
        foreach (var item in __instance.allItemsList.itemsList)
        {
            if (!item.spawnPrefab || !item.spawnPrefab.TryGetComponent<BeltBagItem>(out _))
                continue;
            
            Array.Resize(ref item.toolTips,item.toolTips.Length + 1);

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
}