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
            Log.Message("Deep Resource Manager: Mod loaded successfully!");
        }
    }
}

