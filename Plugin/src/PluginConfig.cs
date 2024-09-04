using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagConfig.Dependency;
using BepInEx.Configuration;

namespace BagConfig;

internal static class PluginConfig
{
    internal static void Init()
    {
        var config = BagConfig.INSTANCE.Config;
        //Initialize Configs

        General.Enabled = config.Bind("General", "Enabled", true, "Change the BeltBag Behaviour");
        General.Tooltip = config.Bind("General", "Tooltip", true, "Show a tooltip if the target item cannot be stored");
        General.DropAll = config.Bind("General", "Add Empty Bag Key", true, "Add [E] action to Drop the entire bag inventory");
        General.DetectionMode = config.Bind("General", "Detection Mode", DetectionMode.Multiple, "System used to detect scrap in line of sight");
        General.GrabRange = config.Bind("General", "Grab Range", 4f, new ConfigDescription("Show a tooltip if the target item cannot be stored", new AcceptableValueRange<float>(0f,20f)));
        
        Inventory.SlotCount = config.Bind("Inventory", "Slots", 15, new ConfigDescription("How many items can the bag store", new AcceptableValueRange<int>(0, int.MaxValue)));
        
        Inventory.Tools = config.Bind("Inventory", "Allow Tools", true, "Store Tools");
        Inventory.Scrap = config.Bind("Inventory", "Allow Scrap", false, "Store Scrap");
        Inventory.TwoHanded = config.Bind("Inventory", "Allow TwoHanded", false, "Store Two Handed");
        
        Inventory.Whitelist = config.Bind("Inventory", "Whitelist", "", "items always allowed in the bag!");
        Inventory.BlackList = config.Bind("Inventory", "Blacklist", "Body", "items always not allowed in the bag! ( has priority over whitelist! )");

        Inventory.WhitelistedItems = ProcessList(Inventory.Whitelist);
        Inventory.BlackListedItems = ProcessList(Inventory.BlackList);
        
        Inventory.Whitelist.SettingChanged += (_ ,_) => Inventory.WhitelistedItems = ProcessList(Inventory.Whitelist);
        Inventory.BlackList.SettingChanged += (_ ,_) => Inventory.BlackListedItems = ProcessList(Inventory.BlackList);

        if (LethalConfigProxy.Enabled)
        {
            LethalConfigProxy.AddConfig(General.Enabled);
            LethalConfigProxy.AddConfig(General.Tooltip);
            LethalConfigProxy.AddConfig(General.DropAll, true);
            LethalConfigProxy.AddConfig(General.GrabRange);
            
            LethalConfigProxy.AddConfig(Inventory.SlotCount);
            
            LethalConfigProxy.AddConfig(Inventory.Tools);
            LethalConfigProxy.AddConfig(Inventory.Scrap);
            LethalConfigProxy.AddConfig(Inventory.TwoHanded);
            
            LethalConfigProxy.AddConfig(Inventory.Whitelist);
            LethalConfigProxy.AddConfig(Inventory.BlackList);
        }

        CleanAndSave();
        return;

        ISet<string> ProcessList(ConfigEntry<string> entry)
        {
            var val = entry.Value;
            return val.Split(",").Select(s => s.Trim()).ToHashSet();
        }
    }
    
    public static class General
    {
        public static ConfigEntry<bool> Enabled { get; internal set; }
        public static ConfigEntry<bool> Tooltip { get; internal set; }
        public static ConfigEntry<bool> DropAll { get; internal set; }
        
        public static ConfigEntry<float> GrabRange { get; internal set; }
        
        public static ConfigEntry<DetectionMode> DetectionMode { get; internal set; }

    }
    
    public static class Inventory
    {
        public static ISet<string> WhitelistedItems = new HashSet<string>();
        public static ISet<string> BlackListedItems = new HashSet<string>();
        
        public static ConfigEntry<int> SlotCount { get; internal set; }
        public static ConfigEntry<string> Whitelist { get; internal set; }
        public static ConfigEntry<string> BlackList { get; internal set; }
        
        public static ConfigEntry<bool> Tools { get; internal set; }
        public static ConfigEntry<bool> Scrap { get; internal set; }
        public static ConfigEntry<bool> TwoHanded { get; internal set; }
        
    }
        
    
    internal static void CleanAndSave()
    {
        var config = BagConfig.INSTANCE.Config;
        //remove unused options
        var orphanedEntriesProp = config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

        orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
        config.Save(); // Save the config file
    }
            
    internal enum DetectionMode
    {
        Single,
        Multiple
    }
}