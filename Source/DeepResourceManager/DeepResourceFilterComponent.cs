using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DeepResourceManager
{
    /// <summary>
    /// GameComponent that stores persistent filter settings across save/load
    /// </summary>
    public class DeepResourceFilterComponent : GameComponent
    {
        private List<string> enabledResourceDefNames = new List<string>(); // Store defNames as strings for persistence
        
        public DeepResourceFilterComponent()
        {
        }
        
        public DeepResourceFilterComponent(Game game) : base()
        {
            // Ensure this component is properly initialized
        }
        
        /// <summary>
        /// Get the enabled resource types as a HashSet of ThingDefs
        /// </summary>
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
        
        /// <summary>
        /// Set the enabled resource types from a HashSet of ThingDefs
        /// </summary>
        public void SetEnabledResourceTypes(HashSet<ThingDef> enabledTypes)
        {
            enabledResourceDefNames.Clear();
            foreach (ThingDef def in enabledTypes)
            {
                if (def != null && !string.IsNullOrEmpty(def.defName))
                {
                    enabledResourceDefNames.Add(def.defName);
                }
            }
        }
        
        /// <summary>
        /// Add a resource type to the enabled list
        /// </summary>
        public void AddResourceType(ThingDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.defName))
            {
                if (!enabledResourceDefNames.Contains(def.defName))
                {
                    enabledResourceDefNames.Add(def.defName);
                }
            }
        }
        
        /// <summary>
        /// Remove a resource type from the enabled list
        /// </summary>
        public void RemoveResourceType(ThingDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.defName))
            {
                enabledResourceDefNames.Remove(def.defName);
            }
        }
        
        /// <summary>
        /// Check if a resource type is enabled
        /// </summary>
        public bool IsResourceTypeEnabled(ThingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            return enabledResourceDefNames.Contains(def.defName);
        }
        
        /// <summary>
        /// Clear all enabled resource types
        /// </summary>
        public void Clear()
        {
            enabledResourceDefNames.Clear();
        }
        
        /// <summary>
        /// Save/Load the filter settings
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref enabledResourceDefNames, "enabledResourceDefNames", LookMode.Value);
            
            // Handle old saves that might not have this data
            if (Scribe.mode == LoadSaveMode.LoadingVars && enabledResourceDefNames == null)
            {
                enabledResourceDefNames = new List<string>();
            }
        }
    }
}

