using HarmonyLib;
using RimWorld;
using Verse;

namespace DeepResourceManager
{
    /// <summary>
    /// Harmony patch to detect when the deep scanner finds new resources
    /// </summary>
    [HarmonyPatch(typeof(CompDeepScanner), "DoFind")]
    public static class CompDeepScanner_Patch
    {
        /// <summary>
        /// Postfix patch that runs after DoFind completes, triggering our deposit list update
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(CompDeepScanner __instance, Pawn worker)
        {
            // Notify the window that new resources were discovered
            DeepResourceDiscoveryNotifier.NotifyNewResourcesDiscovered(__instance.parent.Map);
        }
    }
    
    /// <summary>
    /// Harmony patch to detect when the treasure scanner finds new resources
    /// Uses reflection to find the type at runtime since it's from another mod
    /// </summary>
    public static class CompTreasureScanner_Patch
    {
        /// <summary>
        /// Initialize the patch for CompTreasureScanner (from RomyTreasures mod)
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            try
            {
                // Try to find CompTreasureScanner type from RomyTreasures mod at runtime
                var treasureScannerType = System.Type.GetType("RomyTreasures.CompTreasureScanner, RomyTreasures");
                
                if (treasureScannerType == null)
                {
                    return; // Mod not installed, silently skip
                }
                
                // Get the DoFind method (protected override, so NonPublic)
                var doFindMethod = treasureScannerType.GetMethod("DoFind", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                
                if (doFindMethod == null)
                {
                    Log.Warning("[Deep Resource Manager] Could not find DoFind method in CompTreasureScanner");
                    return;
                }
                
                // Get our Postfix method
                var postfixMethod = typeof(CompTreasureScanner_Patch).GetMethod("Postfix", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (postfixMethod == null)
                {
                    Log.Error("[Deep Resource Manager] Could not find Postfix method in CompTreasureScanner_Patch");
                    return;
                }
                
                // Apply the patch
                harmony.Patch(doFindMethod, postfix: new HarmonyMethod(postfixMethod));
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[Deep Resource Manager] Could not patch CompTreasureScanner: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Postfix patch that runs after DoFind completes
        /// CompTreasureScanner inherits from CompScanner, which has a 'parent' property
        /// We get parent from the instance itself (it's inherited from CompScanner)
        /// </summary>
        public static void Postfix(object __instance, Pawn worker)
        {
            try
            {
                // Get the parent field from the instance (inherited from ThingComp)
                // ThingComp.parent is a field, not a property
                var instanceType = __instance.GetType();
                var parentField = instanceType.GetField("parent", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                var parent = parentField.GetValue(__instance) as ThingWithComps;
                var map = parent?.Map;
                
                if (map != null)
                {
                    // Notify the window that new resources were discovered
                    DeepResourceDiscoveryNotifier.NotifyNewResourcesDiscovered(map);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[Deep Resource Manager] Error in CompTreasureScanner patch: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Simple notifier class to communicate between the patch and the window
    /// </summary>
    public static class DeepResourceDiscoveryNotifier
    {
        private static bool newResourcesDiscovered = false;
        private static Map lastNotifiedMap = null;
        
        /// <summary>
        /// Called by the Harmony patch when new resources are discovered
        /// </summary>
        public static void NotifyNewResourcesDiscovered(Map map)
        {
            if (map != null)
            {
                newResourcesDiscovered = true;
                lastNotifiedMap = map;
            }
        }
        
        /// <summary>
        /// Check if new resources were discovered and reset the flag
        /// </summary>
        public static bool CheckAndReset(Map map)
        {
            if (newResourcesDiscovered && lastNotifiedMap == map)
            {
                newResourcesDiscovered = false;
                return true;
            }
            return false;
        }
    }
}

