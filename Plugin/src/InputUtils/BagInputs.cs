using BagConfig.Patches;
using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace BagConfig.InputUtils;

public class BagInputs : LcInputActions
{
    static BagInputs()
    {
        Instance = new BagInputs();
        Instance.DropAll.performed += _ =>
        {
            if (!BeltBagPatch.Enabled)
                return;
            
            if (!TryGetBeltBag(out var beltBag))
                return;

            BeltBagPatch.TryDumpItems(beltBag);
        };

        Instance.GrabOne.performed += _ =>
        {
            if (!BeltBagPatch.Enabled)
                return;
            
            if (!TryGetBeltBag(out var beltBag))
                return;

            BeltBagPatch.TryGrabItem(beltBag);
        };
        
        return;
        
        static bool TryGetBeltBag(out BeltBagItem beltBag)
        {
            beltBag = null;
            var gameNetworkManager = GameNetworkManager.Instance;
            if (gameNetworkManager == null)
                return false;
            
            if(gameNetworkManager.localPlayerController.currentlyHeldObjectServer is not BeltBagItem item)
                return false;
        
            beltBag = item;
            return true;
        }
    }

    public static BagInputs Instance { get; }
    
    [InputAction(KeyboardControl.E, ActionType=InputActionType.Button, Name = "Empty Held BeltBag")]
    public InputAction DropAll { get; set; }

    [InputAction(KeyboardControl.Q, ActionType=InputActionType.Button, Name = "Put item in Held BeltBag")]
    public InputAction GrabOne { get; set; }
}