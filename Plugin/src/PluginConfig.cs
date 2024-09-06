using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using BagConfig.Dependency;
using BepInEx.Configuration;

namespace BagConfig;

internal static class PluginConfig
{
    private const string ToolsCategory = "Tools";
    private const string OneHandedCategory = "One Handed Scrap";
    private const string TwoHandedCategory = "Two Handed Scrap";
    
    
    
    internal static void Init()
    {
        var config = BagConfig.INSTANCE.Config;
        //Initialize Configs

        Misc.Tooltip = config.Bind("Miscellaneous", "Tooltip", true, "Show a tooltip if the target item cannot be stored");
        Misc.DropAll = config.Bind("Miscellaneous", "Add Empty Bag Key", true, "Add [E] action to Drop the entire bag inventory");
        Misc.GrabRange = config.Bind("Miscellaneous", "Grab Range", 4f, new ConfigDescription("Show a tooltip if the target item cannot be stored", new AcceptableValueRange<float>(0f,20f)));
        
        Host.Capacity = config.Bind("Host Settings", "Enforce Capacity"    , true, "Server-side check to limit the bag Capacity");
        Host.Category = config.Bind("Host Settings", "Enforce Restrictions", true, "Server-side check to limit the items allowed inside the Bag");
        Host.Range    = config.Bind("Host Settings", "Enforce Range"       , true, "Server-side check to limit the grab range");
        
        Limits.Capacity = config.Bind("Limits", "Capacity", 15, new ConfigDescription("How many items can the bag store", new AcceptableValueRange<int>(0, int.MaxValue)));
        Limits.ItemCategories = config.Bind("Limits", "Item Categories", "Body: Blocklist, Toothpaste: Utils", new ConfigDescription("Dictionary describing the association between a Item and a Category name"));

        Limits.CategoryConfigs[ToolsCategory] = new CategoryConfig(ToolsCategory, true);
        Limits.CategoryConfigs[OneHandedCategory] = new CategoryConfig(OneHandedCategory);
        Limits.CategoryConfigs[TwoHandedCategory] = new CategoryConfig(TwoHandedCategory);
        ProcessCategories(Limits.ItemCategories);
        Limits.ItemCategories.SettingChanged += (_, _) => ProcessCategories(Limits.ItemCategories);
        
        if (LethalConfigProxy.Enabled)
        {
            LethalConfigProxy.AddConfig(Misc.Tooltip);
            LethalConfigProxy.AddConfig(Misc.DropAll, true);
            LethalConfigProxy.AddConfig(Misc.GrabRange);
            
            LethalConfigProxy.AddConfig(Limits.Capacity);
            LethalConfigProxy.AddConfig(Limits.ItemCategories);
        }

        CleanAndSave();
        return;
        
        StringDictionary ProcessDictionary(ConfigEntry<string> entry)
        {
            var val = entry.Value;
            var returnDict = new StringDictionary();
            foreach (var keyPair in val.Split(","))
            {
                var trimmed1 = keyPair.Trim();

                var parts = trimmed1.Split(":");
                
                if (parts.Length != 2)
                {
                    BagConfig.Log.LogError($"Item Association: malformed Entry! {trimmed1}");
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (returnDict.ContainsKey(key))
                {
                    BagConfig.Log.LogWarning($"Item Association: {key} was already defined - overwriting!");
                }
                    
                returnDict[key] = value;
            }

            return returnDict;
        }
        
        void ProcessCategories(ConfigEntry<string> entry)
        {
            Limits.ItemCategoryAssociations = ProcessDictionary(entry);

            foreach (string category in Limits.ItemCategoryAssociations.Values)
            {
                if (Limits.CategoryConfigs.ContainsKey(category))
                    continue;
                
                Limits.CategoryConfigs[category] = new CategoryConfig(category);
            }
        }
    }
    
    public static class Misc
    {
        public static ConfigEntry<bool> Tooltip { get; internal set; }
        public static ConfigEntry<bool> DropAll { get; internal set; }
        
        public static ConfigEntry<float> GrabRange { get; internal set; }

    }
    
    public static class Host
    {
        public static ConfigEntry<bool> Capacity { get; internal set; }
        public static ConfigEntry<bool> Category { get; internal set; }
        public static ConfigEntry<bool> Range    { get; internal set; }
    }
    
    public static class Limits
    {
        public static ConfigEntry<int> Capacity { get; internal set; }
        
        public static ConfigEntry<string> ItemCategories { get; internal set; }

        public static StringDictionary ItemCategoryAssociations = new StringDictionary();

        public static readonly Dictionary<string, CategoryConfig> CategoryConfigs = [];
    }
    
    public class CategoryConfig
    {
        public readonly string CategoryName;

        public readonly ConfigEntry<bool> Allow;
        public readonly ConfigEntry<int> Limit;

        public CategoryConfig(string categoryName, bool allow = false)
        {
            CategoryName = categoryName;
            Allow = BagConfig.INSTANCE.Config.Bind($"Limit.{CategoryName}", "Allow", allow, new ConfigDescription("Allow grabbing this category!"));
            Limit = BagConfig.INSTANCE.Config.Bind($"Limit.{CategoryName}", "Max Amount", -1, new ConfigDescription("How many items of this category can be stored at the same time?", new AcceptableValueRange<int>(-1, Limits.Capacity?.Value ?? int.MaxValue)));

            if (LethalConfigProxy.Enabled)
            {
                LethalConfigProxy.AddConfig(Allow);
                LethalConfigProxy.AddConfig(Limit);
            }
        }
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

    public static CategoryConfig GetBagCategory(this GrabbableObject grabbable)
    {
        if (Limits.ItemCategoryAssociations.ContainsKey(grabbable.itemProperties.itemName))
        {
            return Limits.CategoryConfigs[Limits.ItemCategoryAssociations[grabbable.itemProperties.itemName]];
        }
        
        var isScrap = grabbable.itemProperties.isScrap;
        var isTwoHanded = grabbable.itemProperties.twoHanded;

        if (!isScrap)
            return Limits.CategoryConfigs[ToolsCategory];
        
        if (!isTwoHanded)
            return Limits.CategoryConfigs[OneHandedCategory];
        
        return Limits.CategoryConfigs[TwoHandedCategory];
    }
}