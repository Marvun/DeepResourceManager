using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace DeepResourceManager
{
    /// <summary>
    /// Handles persistence of filter settings using RimWorld's save system
    /// </summary>
    public class DeepResourceFilterPersistence : GameComponent
    {
        private List<string> enabledResourceDefNames = new List<string>();
        private List<string> explicitlyDisabledResourceDefNames = new List<string>(); // Track types user has manually disabled
        private bool hasBeenInitialized = false; // Track if filter has been initialized (prevents auto-enable on load)
        private static DeepResourceFilterPersistence instance;
        
        public DeepResourceFilterPersistence()
        {
            instance = this;
        }
        
        public DeepResourceFilterPersistence(Game game) : base()
        {
            instance = this;
        }
        
        public static DeepResourceFilterPersistence Instance
        {
            get
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<DeepResourceFilterPersistence>();
                if (instance == null)
                {
                    // Add component using reflection
                    var gameType = typeof(Game);
                    var componentsField = gameType.GetField("components", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (componentsField != null)
                    {
                        var components = componentsField.GetValue(Current.Game) as System.Collections.Generic.List<GameComponent>;
                        if (components != null)
                        {
                            instance = new DeepResourceFilterPersistence(Current.Game);
                            components.Add(instance);
                        }
                    }
                }
                }
                return instance;
            }
        }
        
        public HashSet<ThingDef> GetEnabledResourceTypes()
        {
            HashSet<ThingDef> result = new HashSet<ThingDef>();
            foreach (string defName in enabledResourceDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    result.Add(def);
                }
            }
            return result;
        }
        
        public void AddResourceType(ThingDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.defName))
            {
                if (!enabledResourceDefNames.Contains(def.defName))
                {
                    enabledResourceDefNames.Add(def.defName);
                }
                // Remove from explicitly disabled list if it was there (user re-enabled it)
                explicitlyDisabledResourceDefNames.Remove(def.defName);
            }
        }
        
        public void RemoveResourceType(ThingDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.defName))
            {
                enabledResourceDefNames.Remove(def.defName);
                // Mark as explicitly disabled (user manually disabled it)
                if (!explicitlyDisabledResourceDefNames.Contains(def.defName))
                {
                    explicitlyDisabledResourceDefNames.Add(def.defName);
                }
            }
        }
        
        /// <summary>
        /// Check if a resource type has been explicitly disabled by the user
        /// </summary>
        public bool IsExplicitlyDisabled(ThingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            return explicitlyDisabledResourceDefNames.Contains(def.defName);
        }
        
        /// <summary>
        /// Add a resource type without marking it as explicitly disabled (for auto-enable)
        /// </summary>
        public void AddResourceTypeAuto(ThingDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.defName))
            {
                // Only auto-enable if it hasn't been explicitly disabled
                if (!explicitlyDisabledResourceDefNames.Contains(def.defName))
                {
                    if (!enabledResourceDefNames.Contains(def.defName))
                    {
                        enabledResourceDefNames.Add(def.defName);
                    }
                }
            }
        }
        
        public bool IsResourceTypeEnabled(ThingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            return enabledResourceDefNames.Contains(def.defName);
        }
        
        public bool HasBeenInitialized
        {
            get { return hasBeenInitialized; }
        }
        
        public void MarkAsInitialized()
        {
            hasBeenInitialized = true;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref enabledResourceDefNames, "enabledResourceDefNames", LookMode.Value);
            Scribe_Collections.Look(ref explicitlyDisabledResourceDefNames, "explicitlyDisabledResourceDefNames", LookMode.Value);
            Scribe_Values.Look(ref hasBeenInitialized, "hasBeenInitialized", false);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (enabledResourceDefNames == null)
                {
                    enabledResourceDefNames = new List<string>();
                }
                if (explicitlyDisabledResourceDefNames == null)
                {
                    explicitlyDisabledResourceDefNames = new List<string>();
                }
            }
        }
    }
}

