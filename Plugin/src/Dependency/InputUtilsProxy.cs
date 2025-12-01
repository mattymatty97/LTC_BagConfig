using System.Runtime.CompilerServices;
using BagConfig.InputUtils;
using BagConfig.Patches;
using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using UnityEngine.InputSystem;

namespace BagConfig.Dependency;

public static class InputUtilsProxy
{
    private static bool? _enabled;

    public static bool Enabled
    {
        get
        {
            _enabled ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.LethalCompanyInputUtils");
            return _enabled.Value;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void Init()
    {
        if (!Enabled)
            return;

        _ = BagInputs.Instance;
    }
    
    
    
}