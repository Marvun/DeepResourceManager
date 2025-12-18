using HarmonyLib;
using Verse;

namespace DeepResourceManager
{
    /// <summary>
    /// Main mod class. RimWorld will call OnStartup() when the mod loads.
    /// </summary>
    public class DeepResourceManagerMod : Mod
    {
        public DeepResourceManagerMod(ModContentPack content) : base(content)
        {
            // Apply Harmony patches
            var harmony = new Harmony("com.deepresourcemanager.patches");
            harmony.PatchAll();
            
            // Manually patch CompTreasureScanner (from RomyTreasures mod) since it's from another mod
            CompTreasureScanner_Patch.Initialize(harmony);
            
            Log.Message("Deep Resource Manager: Mod loaded successfully!");
        }
    }
}

