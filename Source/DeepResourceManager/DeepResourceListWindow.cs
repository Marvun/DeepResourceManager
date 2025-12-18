using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DeepResourceManager
{
    /// <summary>
    /// Filter window for selecting which resource types to display
    /// </summary>
    public class DeepResourceFilterWindow : Window
    {
        private DeepResourceFilterPersistence filterPersistence;
        private List<ThingDef> allPossibleResources;
        private List<ThingDef> discoveredResources;
        private bool showOnlyDiscovered = true;
        private Vector2 scrollPosition = Vector2.zero;
        private readonly float lineHeight = 24f;
        private readonly float padding = 10f;

        /// <summary>
        /// Check if a ThingDef has treasure commonality (from mod extensions)
        /// </summary>
        private static bool HasTreasureCommonality(ThingDef def)
        {
            if (def.modExtensions == null) return false;
            
            foreach (var ext in def.modExtensions)
            {
                var extType = ext.GetType();
                
                // Check if this is a TreasureThingDef extension (by checking for treasureCommonality field via reflection)
                if (extType.Name == "TreasureThingDef" || extType.FullName.Contains("TreasureThingDef"))
                {
                    // Try field (it's a field, not a property)
                    var field = extType.GetField("treasureCommonality", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (field != null)
                    {
                        var value = field.GetValue(ext);
                        if (value is float commonality && commonality > 0f)
                        {
                            return true;
                        }
                    }
                    
                    // Try alternative field names
                    var altNames = new[] { "TreasureCommonality" };
                    foreach (var name in altNames)
                    {
                        field = extType.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                        if (field != null)
                        {
                            var value = field.GetValue(ext);
                            if (value is float commonality && commonality > 0f)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public DeepResourceFilterWindow(DeepResourceFilterPersistence persistence, List<ThingDef> discoveredTypes)
        {
            filterPersistence = persistence;
            discoveredResources = discoveredTypes;
            
            // Get all possible deep resource types (including treasures)
            allPossibleResources = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.deepCommonality > 0f || HasTreasureCommonality(def))
                .OrderBy(def => def.label)
                .ToList();
            
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Filter Resources");
            Text.Font = GameFont.Small;
            
            float yPos = 35f;

            // Toggle for showing only discovered vs all possible
            Rect toggleRect = new Rect(inRect.x + padding, yPos, inRect.width - padding * 2, 24f);
            Widgets.CheckboxLabeled(toggleRect, "Show only discovered resource types", ref showOnlyDiscovered);
            yPos += 30f;

            // Get the list to display based on toggle
            List<ThingDef> resourcesToShow = showOnlyDiscovered ? discoveredResources : allPossibleResources;

            // "All" / "None" buttons
            Rect allButtonRect = new Rect(inRect.x + padding, yPos, 80f, 24f);
            if (Widgets.ButtonText(allButtonRect, "Select All"))
            {
                if (filterPersistence != null)
                {
                    foreach (var resourceType in resourcesToShow)
                    {
                        filterPersistence.AddResourceType(resourceType);
                    }
                }
            }

            Rect noneButtonRect = new Rect(allButtonRect.xMax + padding, yPos, 80f, 24f);
            if (Widgets.ButtonText(noneButtonRect, "Select None"))
            {
                if (filterPersistence != null)
                {
                    foreach (var resourceType in resourcesToShow)
                    {
                        filterPersistence.RemoveResourceType(resourceType);
                    }
                }
            }
            yPos += 30f;

            // Scrollable list of resource types
            Rect outRect = new Rect(inRect.x, yPos, inRect.width, inRect.height - yPos - 10f);
            float contentHeight = resourcesToShow.Count * (lineHeight + padding) + padding;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, true);

            float currentY = 0f;
            foreach (var resourceType in resourcesToShow)
            {
                bool isEnabled = filterPersistence != null && filterPersistence.IsResourceTypeEnabled(resourceType);
                Rect checkboxRect = new Rect(padding, currentY, viewRect.width - padding * 2, lineHeight);

                Widgets.CheckboxLabeled(checkboxRect, resourceType.LabelCap, ref isEnabled);
                
                if (filterPersistence != null)
                {
                    bool wasEnabled = filterPersistence.IsResourceTypeEnabled(resourceType);
                    if (isEnabled != wasEnabled)
                    {
                        if (isEnabled)
                        {
                            filterPersistence.AddResourceType(resourceType);
                        }
                        else
                        {
                            filterPersistence.RemoveResourceType(resourceType);
                        }
                    }
                }

                currentY += lineHeight + padding;
            }

            Widgets.EndScrollView();
        }
    }

    /// <summary>
    /// Tab window that displays a list of all available deep resources
    /// </summary>
    public class MainTabWindow_DeepResources : MainTabWindow
    {
        private struct ResourceDeposit
        {
            public ThingDef resource;
            public IntVec3 position; // Center or first cell of the deposit
            public int cellCount;
            public int totalYield; // Total yield for this deposit
            public List<IntVec3> cells; // All cells in this deposit
            public int activeDrillsWithPawns; // Number of drills with pawns actively working
            public int totalDrillsOnDeposit; // Total number of drills placed on this deposit
        }

        private Vector2 scrollPosition = Vector2.zero;
        private List<ResourceDeposit> deepResources; // List of individual deposits
        private HashSet<int> expandedDeposits = new HashSet<int>(); // Track which deposits are expanded (by index)
        private Dictionary<int, List<Building>> depositDrills = new Dictionary<int, List<Building>>(); // Track drills per deposit
        private readonly float lineHeight = 32f;
        private readonly float iconSize = 28f;
        private readonly float drillRowHeight = 32f; // Height of each drill sub-row (match deposit rows)
        private ResourceDeposit? hoveredDeposit = null; // Currently hovered deposit
        private LookTargets hoveredLookTargets = null; // LookTargets for hovered deposit (used for arrow drawing)
        
        // Caching for performance - expensive operations run periodically
        private List<ResourceDeposit> cachedFilteredDeposits = null;
        private Dictionary<ThingDef, float> cachedCommonality = new Dictionary<ThingDef, float>();
        private Dictionary<Building, int> cachedMineableAmounts = new Dictionary<Building, int>(); // Cached mineable amounts per drill
        private Dictionary<Building, Pawn> cachedWorkingPawns = new Dictionary<Building, Pawn>(); // Cached working pawn per drill
        private int drillStatusUpdateNext = 0; // Tick when to update drill status cache next
        private bool cacheDirty = true; // Track if cache needs refresh
        
        // Track discovered cell count to detect when scanner finds new resources
        private int lastDiscoveredCellCount = -1; // -1 means not initialized yet
        private int cellCountCheckNext = 0; // Tick when to check cell count next (check periodically, not every frame)
        
        /// <summary>
        /// Get enabled resource types from the persistent filter component
        /// </summary>
        private HashSet<ThingDef> GetEnabledResourceTypes()
        {
            var persistence = DeepResourceFilterPersistence.Instance;
            if (persistence != null)
            {
                return persistence.GetEnabledResourceTypes();
            }
            return new HashSet<ThingDef>();
        }
        
        private struct DrillStatusInfo
        {
            public string status;
            public Color statusColor;
            public float progressPercent;
            public int mineableAmount;
            public Pawn workingPawn;
        }

        /// <summary>
        /// Get the commonality value for a resource (either deepCommonality or treasureCommonality)
        /// Uses caching to avoid expensive reflection calls
        /// </summary>
        private float GetResourceCommonality(ThingDef def)
        {
            // Check cache first
            if (cachedCommonality.TryGetValue(def, out float cached))
            {
                return cached;
            }
            
            float result = 0f;
            
            // First check for standard deepCommonality
            if (def.deepCommonality > 0f)
            {
                result = def.deepCommonality;
            }
            else
            {
                // Check for treasure commonality via mod extensions
                if (def.modExtensions != null)
                {
                    foreach (var ext in def.modExtensions)
                    {
                        var extType = ext.GetType();
                        
                        if (extType.Name == "TreasureThingDef" || extType.FullName.Contains("TreasureThingDef"))
                        {
                            // Try field first (since it's a field, not a property)
                            var field = extType.GetField("treasureCommonality", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                            if (field != null)
                            {
                                var value = field.GetValue(ext);
                                if (value is float commonality && commonality > 0f)
                                {
                                    result = commonality;
                                    break;
                                }
                            }
                            
                            // Try alternative field names
                            var altNames = new[] { "TreasureCommonality" };
                            foreach (var name in altNames)
                            {
                                field = extType.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                                if (field != null)
                                {
                                    var value = field.GetValue(ext);
                                    if (value is float commonality && commonality > 0f)
                                    {
                                        result = commonality;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Cache the result
            cachedCommonality[def] = result;
            return result;
        }

        public MainTabWindow_DeepResources()
        {
            doCloseButton = false;
            doCloseX = false;
            closeOnClickedOutside = false;
        }

        private void UpdateDeepResourceList()
        {
            
            deepResources = new List<ResourceDeposit>();
            
            if (Find.CurrentMap == null)
            {
                Log.Warning("[Deep Resource Manager] UpdateDeepResourceList: No current map, aborting scan");
                return;
            }

            Map map = Find.CurrentMap;
            DeepResourceGrid grid = map.deepResourceGrid;
            HashSet<IntVec3> processedCells = new HashSet<IntVec3>();
            
            // Scan the entire map for discovered deep resources
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    
                    // Skip if already processed as part of another deposit
                    if (processedCells.Contains(cell))
                    {
                        continue;
                    }
                    
                    ThingDef resource = grid.ThingDefAt(cell);
                    
                    if (resource != null)
                    {
                        // Find all adjacent cells of the same resource type (flood fill)
                        List<IntVec3> depositCells = new List<IntVec3>();
                        List<IntVec3> cellsToCheck = new List<IntVec3>();
                        cellsToCheck.Add(cell);
                        processedCells.Add(cell);
                        
                        int index = 0;
                        while (index < cellsToCheck.Count)
                        {
                            IntVec3 currentCell = cellsToCheck[index];
                            index++;
                            depositCells.Add(currentCell);
                            
                            // Check all 8 adjacent cells
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dz = -1; dz <= 1; dz++)
                                {
                                    if (dx == 0 && dz == 0) continue;
                                    
                                    IntVec3 neighbor = new IntVec3(currentCell.x + dx, 0, currentCell.z + dz);
                                    
                                    // Check bounds
                                    if (neighbor.x < 0 || neighbor.x >= map.Size.x ||
                                        neighbor.z < 0 || neighbor.z >= map.Size.z)
                                    {
                                        continue;
                                    }
                                    
                                    // Check if same resource and not processed
                                    if (!processedCells.Contains(neighbor) && 
                                        grid.ThingDefAt(neighbor) == resource)
                                    {
                                        processedCells.Add(neighbor);
                                        cellsToCheck.Add(neighbor);
                                    }
                                }
                            }
                        }
                        
                        // Calculate total yield by checking each cell
                        int totalYield = 0;
                        foreach (IntVec3 depositCell in depositCells)
                        {
                            int amountAtCell = grid.CountAt(depositCell);
                            totalYield += amountAtCell;
                        }
                        
                        // Create deposit entry
                        ResourceDeposit deposit = new ResourceDeposit
                        {
                            resource = resource,
                            position = depositCells[0], // Use first cell as position
                            cellCount = depositCells.Count,
                            totalYield = totalYield,
                            cells = new List<IntVec3>(depositCells), // Store all cells
                            activeDrillsWithPawns = 0, // Will be calculated separately
                            totalDrillsOnDeposit = 0 // Will be calculated separately
                        };
                        
                        deepResources.Add(deposit);
                    }
                }
            }
            
            // Count active drills for each deposit
            CountActiveDrills(map);
            
            // Update discovered cell count
            lastDiscoveredCellCount = GetDiscoveredCellCount(map);
            
            // Clear caches when deposits update
            cachedMineableAmounts.Clear();
            cachedWorkingPawns.Clear();
            cacheDirty = true;
            
        }
        
        /// <summary>
        /// Count how many cells have discovered resources (used to detect when scanner finds new resources)
        /// </summary>
        private int GetDiscoveredCellCount(Map map)
        {
            if (map == null)
            {
                return 0;
            }
            
            DeepResourceGrid grid = map.deepResourceGrid;
            int count = 0;
            
            // Count all cells with discovered resources
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (grid.ThingDefAt(cell) != null)
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Check if new resources have been discovered
        /// First checks Harmony patch notification, then falls back to cell count check
        /// </summary>
        private bool HasNewResourcesDiscovered(Map map)
        {
            if (map == null)
            {
                return false;
            }
            
            // First check if Harmony patch notified us of new resources (instant detection)
            if (DeepResourceDiscoveryNotifier.CheckAndReset(map))
            {
                // Update cell count to match
                lastDiscoveredCellCount = GetDiscoveredCellCount(map);
                return true;
            }
            
            // Fallback: check cell count periodically (every ~60 ticks, ~1 second) for cases where patch might miss
            if (cellCountCheckNext > Find.TickManager.TicksGame)
            {
                return false; // Not time to check yet
            }
            
            int currentCount = GetDiscoveredCellCount(map);
            
            // If not initialized yet, initialize and return true to trigger first scan
            if (lastDiscoveredCellCount < 0)
            {
                lastDiscoveredCellCount = currentCount;
                cellCountCheckNext = Find.TickManager.TicksGame + 60; // Check again in ~1 second
                return true; // Trigger initial scan
            }
            
            // If count changed, new resources were discovered
            if (currentCount != lastDiscoveredCellCount)
            {
                lastDiscoveredCellCount = currentCount;
                cellCountCheckNext = Find.TickManager.TicksGame + 60; // Check again in ~1 second
                return true;
            }
            
            // Schedule next check
            cellCountCheckNext = Find.TickManager.TicksGame + 60; // Check again in ~1 second
            return false;
        }

        private void CountActiveDrills(Map map)
        {
            if (map == null || deepResources == null)
            {
                return;
            }

            // Reset drill counts and clear drill lists
            depositDrills.Clear();
            for (int i = 0; i < deepResources.Count; i++)
            {
                var deposit = deepResources[i];
                deposit.activeDrillsWithPawns = 0;
                deposit.totalDrillsOnDeposit = 0;
                deepResources[i] = deposit;
                depositDrills[i] = new List<Building>();
            }

            // Find all deep drills on the map
            ThingDef deepDrillDef = ThingDefOf.DeepDrill;
            if (deepDrillDef == null)
            {
                return;
            }

            // Get all deep drill buildings
            var allDeepDrills = map.listerBuildings.allBuildingsColonist
                .Where(building => building.def == deepDrillDef)
                .ToList();

            foreach (var drill in allDeepDrills)
            {
                // Skip if drill is broken or destroyed
                if (drill.Destroyed || !drill.Spawned)
                {
                    continue;
                }

                // Check if drill is powered
                var powerComp = drill.TryGetComp<CompPowerTrader>();
                bool isPowered = powerComp == null || powerComp.PowerOn;

                // Check if drill has a pawn actively working on it
                bool hasActivePawn = false;
                
                // Check if any colonist has this drill as their current job target
                var colonists = map.mapPawns.FreeColonistsSpawned;
                foreach (var colonist in colonists)
                {
                    if (colonist.CurJob != null && 
                        colonist.CurJob.targetA.Thing == drill)
                    {
                        hasActivePawn = true;
                        break;
                    }
                }
                
                // Also check if a pawn is at the interaction cell working on the drill
                if (!hasActivePawn)
                {
                    var interactionCell = drill.InteractionCell;
                    var pawnAtDrill = map.mapPawns.AllPawnsSpawned
                        .FirstOrDefault(pawn => pawn.Position == interactionCell && 
                                               pawn.CurJob != null && 
                                               pawn.CurJob.targetA.Thing == drill);
                    hasActivePawn = pawnAtDrill != null;
                }

                // Deep drills mine resources within a 2.6 tile radius
                IntVec3 drillPos = drill.Position;
                float radius = 2.6f;
                
                // Check all cells within the drill's mining radius
                // Find which deposit any cells within radius belong to
                for (int i = 0; i < deepResources.Count; i++)
                {
                    var deposit = deepResources[i];
                    if (deposit.cells == null) continue;
                    
                    // Check if any cell in this deposit is within the drill's radius
                    bool depositInRange = false;
                    foreach (IntVec3 depositCell in deposit.cells)
                    {
                        float distance = drillPos.DistanceTo(depositCell);
                        if (distance <= radius)
                        {
                            depositInRange = true;
                            break; // Found at least one cell in range
                        }
                    }
                    
                    if (depositInRange)
                    {
                        // Store the drill in the list
                        if (!depositDrills.ContainsKey(i))
                        {
                            depositDrills[i] = new List<Building>();
                        }
                        // Only add if not already in the list (avoid duplicates)
                        if (!depositDrills[i].Contains(drill))
                        {
                            depositDrills[i].Add(drill);
                            
                            deposit.totalDrillsOnDeposit++; // Count all drills on deposit (powered or not)
                            if (hasActivePawn && isPowered)
                            {
                                deposit.activeDrillsWithPawns++; // Count only drills with active pawns
                            }
                            deepResources[i] = deposit;
                        }
                        break; // Only count this drill once per deposit
                    }
                }
            }
        }
        
        /// <summary>
        /// Recalculate yields for all deposits (resources may have been mined)
        /// </summary>
        private void RecalculateDepositYields(Map map)
        {
            if (map == null || deepResources == null)
            {
                return;
            }
            
            DeepResourceGrid grid = map.deepResourceGrid;
            
            // Recalculate yield for each deposit
            for (int i = 0; i < deepResources.Count; i++)
            {
                var deposit = deepResources[i];
                if (deposit.cells == null) continue;
                
                int totalYield = 0;
                foreach (IntVec3 depositCell in deposit.cells)
                {
                    int amountAtCell = grid.CountAt(depositCell);
                    totalYield += amountAtCell;
                }
                
                deposit.totalYield = totalYield;
                deepResources[i] = deposit;
            }
        }

        public override Vector2 RequestedTabSize => new Vector2(900f, (float)UI.screenHeight);

        public override void PreOpen()
        {
            base.PreOpen();
            // Note: GameComponent is automatically instantiated via XML registration, no need to manually add it
        }
        
        /// <summary>
        /// Update cached filtered deposits list - only recalculates when needed
        /// </summary>
        private void UpdateCachedFilteredDeposits()
        {
            // Only update if cache is dirty
            if (!cacheDirty && cachedFilteredDeposits != null)
            {
                return; // Cache is still valid
            }
            
            var enabledResourceTypes = GetEnabledResourceTypes();
            
            if (deepResources == null)
            {
                cachedFilteredDeposits = new List<ResourceDeposit>();
                cacheDirty = false;
                return;
            }
            
            // Filter and sort deposits
            cachedFilteredDeposits = deepResources
                .Where(d => enabledResourceTypes.Contains(d.resource))
                .OrderBy(r => r.resource.label)
                .ThenBy(r => r.position.x)
                .ThenBy(r => r.position.z)
                .ToList();
            
            cacheDirty = false;
        }
        
        /// <summary>
        /// Get filtered and sorted deposits list (from cache)
        /// </summary>
        private List<ResourceDeposit> GetFilteredDeposits()
        {
            UpdateCachedFilteredDeposits();
            return cachedFilteredDeposits ?? new List<ResourceDeposit>();
        }
        
        private int GetDepositIndex(ResourceDeposit deposit)
        {
            if (deepResources == null) return -1;
            return deepResources.FindIndex(d => d.resource == deposit.resource && d.position == deposit.position);
        }
        
        /// <summary>
        /// Update cached mineable amounts for all drills - runs periodically
        /// </summary>
        private void UpdateDrillStatusCache(Map map)
        {
            // Only update periodically
            if (drillStatusUpdateNext > Find.TickManager.TicksGame)
            {
                return; // Not time to update yet
            }
            
            // Clear and recalculate mineable amounts and working pawns for all drills
            cachedMineableAmounts.Clear();
            cachedWorkingPawns.Clear();
            
            if (map == null || deepResources == null)
            {
                drillStatusUpdateNext = Find.TickManager.TicksGame + GenTicks.TickRareInterval;
                return;
            }
            
            // Update drill-to-deposit mapping (detects when drills are placed/moved)
            CountActiveDrills(map);
            
            // Recalculate deposit yields (resources may have been mined)
            RecalculateDepositYields(map);
            
            // Mark cache dirty so UI updates with new drill counts and yields
            cacheDirty = true;
            
            // First, map colonists to their drills (loop through colonists once, not per drill)
            var colonists = map.mapPawns.FreeColonistsSpawned;
            foreach (var colonist in colonists)
            {
                if (colonist.CurJob != null && colonist.CurJob.targetA.Thing is Building drill)
                {
                    // Check if this is a deep drill we're tracking
                    if (depositDrills.Values.Any(drills => drills.Contains(drill)))
                    {
                        cachedWorkingPawns[drill] = colonist;
                    }
                }
            }
            
            DeepResourceGrid grid = map.deepResourceGrid;
            float radius = 2.6f;
            
            // Update mineable amounts for all drills
            foreach (var kvp in depositDrills)
            {
                var deposit = deepResources[kvp.Key];
                foreach (var drill in kvp.Value)
                {
                    if (drill.Destroyed || !drill.Spawned) continue;
                    
                    int mineableAmount = 0;
                    IntVec3 drillPos = drill.Position;
                    
                    for (int x = Mathf.FloorToInt(drillPos.x - radius); x <= Mathf.CeilToInt(drillPos.x + radius); x++)
                    {
                        for (int z = Mathf.FloorToInt(drillPos.z - radius); z <= Mathf.CeilToInt(drillPos.z + radius); z++)
                        {
                            IntVec3 checkCell = new IntVec3(x, 0, z);
                            if (checkCell.InBounds(map))
                            {
                                float distance = drillPos.DistanceTo(checkCell);
                                if (distance <= radius)
                                {
                                    ThingDef cellResource = grid.ThingDefAt(checkCell);
                                    if (cellResource == deposit.resource)
                                    {
                                        int amountAtCell = grid.CountAt(checkCell);
                                        mineableAmount += amountAtCell;
                                    }
                                }
                            }
                        }
                    }
                    
                    cachedMineableAmounts[drill] = mineableAmount;
                }
            }
            
            drillStatusUpdateNext = Find.TickManager.TicksGame + GenTicks.TickRareInterval; // Update every ~250 ticks
        }
        
        /// <summary>
        /// Get drill status info - progress updates every frame for smooth bars, other data updates less frequently
        /// </summary>
        private DrillStatusInfo GetDrillStatus(Map map, ResourceDeposit deposit, Building drill)
        {
            // Always update progress for smooth progress bars (cheap operation)
            var powerComp = drill.TryGetComp<CompPowerTrader>();
            bool isPowered = powerComp == null || powerComp.PowerOn;
            var deepDrillComp = drill.TryGetComp<CompDeepDrill>();
            float progressPercent = 0f;
            
            if (deepDrillComp != null && isPowered)
            {
                progressPercent = deepDrillComp.ProgressToNextPortionPercent;
            }
            
            // Get working pawn from cache (updated periodically)
            string status = "Idle";
            Color statusColor = Color.gray;
            Pawn workingPawn = null;
            
            if (!isPowered)
            {
                status = "No Power";
                statusColor = Color.red;
            }
            else
            {
                // Get working pawn from cache (updated periodically)
                if (cachedWorkingPawns.TryGetValue(drill, out Pawn cachedPawn))
                {
                    workingPawn = cachedPawn;
                    status = cachedPawn.NameShortColored;
                    statusColor = Color.green;
                }
            }
            
            // Get mineable amount from cache (updated periodically)
            int mineableAmount = 0;
            if (cachedMineableAmounts.TryGetValue(drill, out int cachedAmount))
            {
                mineableAmount = cachedAmount;
            }
            
            return new DrillStatusInfo
            {
                status = status,
                statusColor = statusColor,
                progressPercent = progressPercent,
                mineableAmount = mineableAmount,
                workingPawn = workingPawn
            };
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (Find.CurrentMap == null)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "No map loaded");
                return;
            }

            // Update the list only when scanner finds new resources (or on first open)
            if (HasNewResourcesDiscovered(Find.CurrentMap))
            {
                UpdateDeepResourceList();
            }
            
            // Update drill status cache periodically
            UpdateDrillStatusCache(Find.CurrentMap);
            
            // Update cached filtered deposits if needed
            UpdateCachedFilteredDeposits();
            
            // Use cached filtered deposits
            var filteredDeposits = GetFilteredDeposits();
            
            // Title and Filter button on same line
            Text.Font = GameFont.Medium;
            float titleWidth = Text.CalcSize("Discovered Deep Resources").x;
            Widgets.Label(new Rect(inRect.x, inRect.y, titleWidth, 30f), "Discovered Deep Resources");
            Text.Font = GameFont.Small;
            
            if (deepResources == null || deepResources.Count == 0)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 30f), "No deep resources discovered yet. Use a ground-penetrating scanner to find resources.");
                return;
            }
            
            // Get unique resource types from discovered resources
            var uniqueResourceTypes = deepResources.Select(r => r.resource).Distinct().OrderBy(r => r.label).ToList();
            
            // Display deposit count below title
            int totalCells = filteredDeposits.Sum(r => r.cellCount);
            int uniqueTypes = filteredDeposits.Select(r => r.resource).Distinct().Count();
            int totalDeposits = filteredDeposits.Count;
            int totalAllDeposits = deepResources.Count;
            string countText;
            if (totalDeposits < totalAllDeposits)
            {
                countText = $"Showing: {totalDeposits} of {totalAllDeposits} deposits, {uniqueTypes} resource types, {totalCells} cells (filtered)";
            }
            else
            {
                countText = $"Total: {totalDeposits} deposits, {uniqueTypes} resource types, {totalCells} cells discovered";
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + 30f, inRect.width, 20f), countText);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            
            // Get enabled resource types from component
            var enabledResourceTypes = GetEnabledResourceTypes();
            
            // Initialize filter only once on first open of a new game (not when loading from save)
            // The persistence component tracks if it has been initialized to prevent auto-enable on load
            var persistence = DeepResourceFilterPersistence.Instance;
            if (persistence != null && !persistence.HasBeenInitialized)
            {
                // Only auto-initialize if this is a new game (filter is empty and we have discovered resources)
                // If the filter is empty but the component was loaded from save, HasBeenInitialized will be true
                if (enabledResourceTypes.Count == 0 && uniqueResourceTypes.Count > 0)
                {
                    // Auto-enable all discovered types (respects explicitly disabled list, which should be empty on new game)
                    foreach (var resourceType in uniqueResourceTypes)
                    {
                        persistence.AddResourceTypeAuto(resourceType);
                    }
                    persistence.MarkAsInitialized();
                    cacheDirty = true; // Mark cache dirty when filter changes
                }
                else
                {
                    // Even if we don't auto-enable, mark as initialized so we don't check again
                    persistence.MarkAsInitialized();
                }
            }
            
            // Auto-enable newly discovered resource types (after initialization)
            // Only enables types that haven't been explicitly disabled by the user
            if (persistence != null && persistence.HasBeenInitialized)
            {
                foreach (var resourceType in uniqueResourceTypes)
                {
                    if (!persistence.IsResourceTypeEnabled(resourceType) && !persistence.IsExplicitlyDisabled(resourceType))
                    {
                        persistence.AddResourceTypeAuto(resourceType);
                        cacheDirty = true; // Mark cache dirty when filter changes
                    }
                }
            }
            
            // Filter button next to title
            Rect filterButtonRect = new Rect(inRect.x + titleWidth + 10f, inRect.y, 150f, 30f);
            if (Widgets.ButtonText(filterButtonRect, "Filter Resources"))
            {
                Find.WindowStack.Add(new DeepResourceFilterWindow(persistence, uniqueResourceTypes));
                cacheDirty = true; // Mark cache dirty when filter window is opened (user might change filter)
            }
            
            // Define column widths (same as in the table drawing code)
            float colExpandWidth = 25f; // Tiny column for expand buttons
            float colResourceWidth = 200f;
            float colCellsWidth = 80f;
            float colYieldWidth = 100f;
            float colDrillsWidth = 100f;
            float colCommonWidth = 100f;
            float colAllowedWidth = 80f; // Column for allowed checkbox on deposit rows
            float colSpacing = 2f;
            float totalTableWidth = colExpandWidth + colResourceWidth + colCellsWidth + colYieldWidth + colDrillsWidth + colCommonWidth + colAllowedWidth + (colSpacing * 6);
            float checkboxSize = 24f; // Size for checkboxes (used in both deposit and drill rows) - matches RimWorld's default checkbox size
            
            float yPos = 55f; // Start below title and count text
            // Calculate content height including expanded drill rows
            float baseHeight = (filteredDeposits.Count + 1) * (lineHeight + 2f);
            float expandedHeight = 0f;
            int calcIdx = 0;
            foreach (var deposit in filteredDeposits)
            {
                if (expandedDeposits.Contains(calcIdx) && deposit.totalDrillsOnDeposit > 0)
                {
                    int actualIdx = GetDepositIndex(deposit);
                    if (actualIdx >= 0 && depositDrills.ContainsKey(actualIdx))
                    {
                        expandedHeight += depositDrills[actualIdx].Count * (drillRowHeight + 1f);
                    }
                }
                calcIdx++;
            }
            float contentHeight = baseHeight + expandedHeight + 50f; // +1 for header row
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(totalTableWidth, inRect.width - 16f), contentHeight);
            Rect outRect = new Rect(inRect.x, yPos, inRect.width, inRect.height - yPos - 10f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, true);

            float currentX = 0f;
            float headerY = 0f;
            
            // Draw column headers with button style
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            // Header: Expand (empty, just for spacing)
            Rect headerExpandRect = new Rect(currentX, headerY, colExpandWidth, lineHeight);
            Widgets.DrawBoxSolid(headerExpandRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            currentX += colExpandWidth + colSpacing;
            
            // Header: Resource
            Rect headerResourceRect = new Rect(currentX, headerY, colResourceWidth, lineHeight);
            Widgets.DrawBoxSolid(headerResourceRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerResourceRect, "Resource");
            currentX += colResourceWidth + colSpacing;
            
            // Header: Cells
            Rect headerCellsRect = new Rect(currentX, headerY, colCellsWidth, lineHeight);
            Widgets.DrawBoxSolid(headerCellsRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerCellsRect, "Cells");
            currentX += colCellsWidth + colSpacing;
            
            // Header: Total Yield
            Rect headerYieldRect = new Rect(currentX, headerY, colYieldWidth, lineHeight);
            Widgets.DrawBoxSolid(headerYieldRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerYieldRect, "Total Yield");
            currentX += colYieldWidth + colSpacing;
            
            // Header: Active Drills
            Rect headerDrillsRect = new Rect(currentX, headerY, colDrillsWidth, lineHeight);
            Widgets.DrawBoxSolid(headerDrillsRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerDrillsRect, "Active Drills");
            currentX += colDrillsWidth + colSpacing;
            
            // Header: Commonality
            Rect headerCommonRect = new Rect(currentX, headerY, colCommonWidth, lineHeight);
            Widgets.DrawBoxSolid(headerCommonRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerCommonRect, "Commonality");
            currentX += colCommonWidth + colSpacing;
            
            // Header: Allowed
            Rect headerAllowedRect = new Rect(currentX, headerY, colAllowedWidth, lineHeight);
            Widgets.DrawBoxSolid(headerAllowedRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            GUI.color = Color.white;
            Widgets.Label(headerAllowedRect, "Allowed");
            
            // Reset text settings
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float currentY = lineHeight + 2f;
            hoveredDeposit = null; // Reset hovered deposit
            hoveredLookTargets = null; // Reset look targets
            
            int displayIndex = 0;
            foreach (var deposit in filteredDeposits)
            {
                // Get the actual index
                int depositIndex = GetDepositIndex(deposit);
                if (depositIndex < 0)
                {
                    displayIndex++;
                    continue;
                }
                
                ThingDef resource = deposit.resource;
                int count = deposit.cellCount;
                int totalYield = deposit.totalYield;
                
                // Track if any cell in this row is hovered
                bool rowHovered = false;
                currentX = 0f;
                
                // Expand/collapse button column
                bool isExpanded = expandedDeposits.Contains(displayIndex);
                bool hasDrills = deposit.totalDrillsOnDeposit > 0;
                
                Rect expandCellRect = new Rect(currentX, currentY, colExpandWidth, lineHeight);
                if (hasDrills)
                {
                    Rect expandRect = new Rect(expandCellRect.x + (colExpandWidth - 20f) / 2f, expandCellRect.y + (lineHeight - 20f) / 2f, 20f, 20f);
                    if (Widgets.ButtonText(expandRect, isExpanded ? "−" : "+"))
                    {
                        if (isExpanded)
                        {
                            expandedDeposits.Remove(displayIndex);
                        }
                        else
                        {
                            expandedDeposits.Add(displayIndex);
                        }
                    }
                }
                currentX += colExpandWidth + colSpacing;
                
                // Resource cell (with icon and label)
                Rect resourceCellRect = new Rect(currentX, currentY, colResourceWidth, lineHeight);
                bool resourceHovered = Mouse.IsOver(resourceCellRect);
                rowHovered = rowHovered || resourceHovered;
                
                // Make the cell clickable (no button style)
                if (Widgets.ButtonInvisible(resourceCellRect))
                {
                    if (Find.CurrentMap != null)
                    {
                        GlobalTargetInfo target = new GlobalTargetInfo(deposit.position, Find.CurrentMap);
                        CameraJumper.TryJumpAndSelect(target);
                    }
                }
                
                // Draw icon and label
                Rect iconRect = new Rect(resourceCellRect.x + 4f, resourceCellRect.y + (lineHeight - iconSize) / 2f, iconSize, iconSize);
                Widgets.ThingIcon(iconRect, resource);
                Rect labelRect = new Rect(iconRect.xMax + 4f, resourceCellRect.y, resourceCellRect.width - iconSize - 8f, lineHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Widgets.Label(labelRect, resource.LabelCap);
                
                if (resourceHovered)
                {
                    hoveredDeposit = deposit;
                    if (Find.CurrentMap != null)
                    {
                        hoveredLookTargets = new LookTargets(deposit.position, Find.CurrentMap);
                    }
                    TooltipHandler.TipRegion(resourceCellRect, "Click to jump to deposit");
                }
                
                currentX += colResourceWidth + colSpacing;
                
                // Cells count cell
                Rect cellsCellRect = new Rect(currentX, currentY, colCellsWidth, lineHeight);
                bool cellsHovered = Mouse.IsOver(cellsCellRect);
                rowHovered = rowHovered || cellsHovered;
                if (Widgets.ButtonInvisible(cellsCellRect))
                {
                    if (Find.CurrentMap != null)
                    {
                        GlobalTargetInfo target = new GlobalTargetInfo(deposit.position, Find.CurrentMap);
                        CameraJumper.TryJumpAndSelect(target);
                    }
                }
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(cellsCellRect, count.ToString());
                if (cellsHovered)
                {
                    hoveredDeposit = deposit;
                    if (Find.CurrentMap != null)
                    {
                        hoveredLookTargets = new LookTargets(deposit.position, Find.CurrentMap);
                    }
                }
                currentX += colCellsWidth + colSpacing;
                
                // Total yield cell
                Rect yieldCellRect = new Rect(currentX, currentY, colYieldWidth, lineHeight);
                bool yieldHovered = Mouse.IsOver(yieldCellRect);
                rowHovered = rowHovered || yieldHovered;
                if (Widgets.ButtonInvisible(yieldCellRect))
                {
                    if (Find.CurrentMap != null)
                    {
                        GlobalTargetInfo target = new GlobalTargetInfo(deposit.position, Find.CurrentMap);
                        CameraJumper.TryJumpAndSelect(target);
                    }
                }
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(yieldCellRect, totalYield.ToString());
                if (yieldHovered)
                {
                    hoveredDeposit = deposit;
                    if (Find.CurrentMap != null)
                    {
                        hoveredLookTargets = new LookTargets(deposit.position, Find.CurrentMap);
                    }
                }
                currentX += colYieldWidth + colSpacing;
                
                // Active drills cell
                Rect drillsCellRect = new Rect(currentX, currentY, colDrillsWidth, lineHeight);
                bool drillsHovered = Mouse.IsOver(drillsCellRect);
                rowHovered = rowHovered || drillsHovered;
                string drillsText = deposit.totalDrillsOnDeposit > 0 
                    ? $"{deposit.activeDrillsWithPawns}/{deposit.totalDrillsOnDeposit}" 
                    : "-";
                if (Widgets.ButtonInvisible(drillsCellRect))
                {
                    if (Find.CurrentMap != null)
                    {
                        GlobalTargetInfo target = new GlobalTargetInfo(deposit.position, Find.CurrentMap);
                        CameraJumper.TryJumpAndSelect(target);
                    }
                }
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(drillsCellRect, drillsText);
                if (drillsHovered)
                {
                    hoveredDeposit = deposit;
                    if (Find.CurrentMap != null)
                    {
                        hoveredLookTargets = new LookTargets(deposit.position, Find.CurrentMap);
                    }
                }
                currentX += colDrillsWidth + colSpacing;
                
                // Commonality cell
                Rect commonCellRect = new Rect(currentX, currentY, colCommonWidth, lineHeight);
                bool commonHovered = Mouse.IsOver(commonCellRect);
                rowHovered = rowHovered || commonHovered;
                if (Widgets.ButtonInvisible(commonCellRect))
                {
                    if (Find.CurrentMap != null)
                    {
                        GlobalTargetInfo target = new GlobalTargetInfo(deposit.position, Find.CurrentMap);
                        CameraJumper.TryJumpAndSelect(target);
                    }
                }
                Text.Anchor = TextAnchor.MiddleRight;
                float commonality = GetResourceCommonality(resource);
                Widgets.Label(commonCellRect, commonality.ToString("F2"));
                if (commonHovered)
                {
                    hoveredDeposit = deposit;
                    if (Find.CurrentMap != null)
                    {
                        hoveredLookTargets = new LookTargets(deposit.position, Find.CurrentMap);
                    }
                }
                currentX += colCommonWidth + colSpacing;
                
                // Allowed checkbox for deposit row (only show if deposit has drills)
                if (hasDrills && depositDrills.ContainsKey(depositIndex) && depositDrills[depositIndex].Count > 0)
                {
                    Rect depositAllowedRect = new Rect(currentX, currentY, colAllowedWidth, lineHeight);
                    
                    // Calculate state based on all drills in this deposit
                    MultiCheckboxState depositAllowedState = MultiCheckboxState.Off;
                    var drills = depositDrills[depositIndex];
                    int allowedCount = 0;
                    int forbiddenCount = 0;
                    
                    foreach (var drill in drills)
                    {
                        if (drill.Destroyed || !drill.Spawned) continue;
                        bool isAllowed = !drill.IsForbidden(Faction.OfPlayer);
                        if (isAllowed)
                            allowedCount++;
                        else
                            forbiddenCount++;
                    }
                    
                    if (allowedCount > 0 && forbiddenCount == 0)
                    {
                        depositAllowedState = MultiCheckboxState.On; // All allowed
                    }
                    else if (forbiddenCount > 0 && allowedCount == 0)
                    {
                        depositAllowedState = MultiCheckboxState.Off; // All forbidden
                    }
                    else if (allowedCount > 0 && forbiddenCount > 0)
                    {
                        depositAllowedState = MultiCheckboxState.Partial; // Mixed state
                    }
                    
                    // Draw checkbox using RimWorld's built-in multi-checkbox
                    Rect checkboxRect = new Rect(depositAllowedRect.x + (colAllowedWidth - checkboxSize) / 2f, depositAllowedRect.y + (lineHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
                    
                    MultiCheckboxState newState = Widgets.CheckboxMulti(checkboxRect, depositAllowedState);
                    if (newState != depositAllowedState)
                    {
                        // Apply new state to all drills
                        bool setToAllowed = (newState == MultiCheckboxState.On);
                        foreach (var drill in depositDrills[depositIndex])
                        {
                            if (drill.Destroyed || !drill.Spawned) continue;
                            drill.SetForbidden(!setToAllowed, false);
                        }
                    }
                }
                
                // Reset text anchor
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                
                currentY += lineHeight + 2f;
                
                // Draw expanded drill details if this deposit is expanded
                if (isExpanded && hasDrills && depositDrills.ContainsKey(depositIndex))
                {
                    var drills = depositDrills[depositIndex];
                    Map map = Find.CurrentMap;
                    
                    foreach (var drill in drills)
                    {
                        if (drill.Destroyed || !drill.Spawned) continue;
                        
                        float drillY = currentY;
                        float indent = colExpandWidth + 10f; // Align with expand column
                        
                        // Get drill status (calculated every frame)
                        DrillStatusInfo drillInfo = GetDrillStatus(map, deposit, drill);
                        
                        // Drill row - use as progress bar (match table width)
                        Rect drillRowRect = new Rect(0f, drillY, totalTableWidth, drillRowHeight);
                        
                        // Get power status for visual
                        var powerComp = drill.TryGetComp<CompPowerTrader>();
                        bool isPowered = powerComp == null || powerComp.PowerOn;
                        
                        // Draw progress bar background
                        Widgets.DrawBoxSolid(drillRowRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                        
                        // Draw progress bar fill
                        if (drillInfo.progressPercent > 0f && isPowered)
                        {
                            Rect progressBarRect = new Rect(drillRowRect.x, drillRowRect.y, drillRowRect.width * drillInfo.progressPercent, drillRowHeight);
                            Color progressColor = new Color(0.2f, 0.6f, 0.2f, 0.6f); // Green progress
                            Widgets.DrawBoxSolid(progressBarRect, progressColor);
                        }
                        else if (!isPowered)
                        {
                            // Red background if no power
                            Widgets.DrawBoxSolid(drillRowRect, new Color(0.4f, 0.1f, 0.1f, 0.3f));
                        }
                        
                        // Status label (on top of progress bar)
                        Rect statusRect = new Rect(indent, drillY, 150f, drillRowHeight);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Text.Font = GameFont.Small;
                        GUI.color = drillInfo.statusColor;
                        Widgets.Label(statusRect, drillInfo.status);
                        GUI.color = Color.white;
                        
                        // Progress percentage text
                        Rect progressTextRect = new Rect(statusRect.xMax + 10f, drillY, 100f, drillRowHeight);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        if (isPowered && drillInfo.progressPercent > 0f)
                        {
                            Widgets.Label(progressTextRect, $"{drillInfo.progressPercent * 100f:F1}%");
                        }
                        
                        // Mineable yield text (align with Total Yield column)
                        float yieldColumnX = colExpandWidth + colSpacing + colResourceWidth + colSpacing + colCellsWidth + colSpacing;
                        Rect drillYieldRect = new Rect(yieldColumnX, drillY, colYieldWidth, drillRowHeight);
                        Text.Anchor = TextAnchor.MiddleRight;
                        GUI.color = Color.white;
                        Widgets.Label(drillYieldRect, drillInfo.mineableAmount.ToString());
                        
                        // Allowed checkbox - align with deposit checkbox column
                        float allowedColumnX = colExpandWidth + colSpacing + colResourceWidth + colSpacing + colCellsWidth + colSpacing + colYieldWidth + colSpacing + colDrillsWidth + colSpacing + colCommonWidth + colSpacing;
                        Rect allowRect = new Rect(allowedColumnX, drillY, colAllowedWidth, drillRowHeight);
                        Vector2 checkboxPos = new Vector2(allowRect.x + (colAllowedWidth - checkboxSize) / 2f, allowRect.y + (drillRowHeight - checkboxSize) / 2f);
                        bool isAllowed = !drill.IsForbidden(Faction.OfPlayer);
                        bool oldAllowed = isAllowed;
                        
                        Widgets.Checkbox(checkboxPos, ref isAllowed, checkboxSize);
                        if (isAllowed != oldAllowed)
                        {
                            drill.SetForbidden(!isAllowed, false);
                        }
                        
                        // Click to jump to drill - exclude checkbox area from clickable region
                        Rect clickableDrillRect = new Rect(drillRowRect.x, drillRowRect.y, allowRect.x - drillRowRect.x, drillRowRect.height);
                        Rect checkboxClickRect = new Rect(checkboxPos.x, checkboxPos.y, checkboxSize, checkboxSize);
                        
                        if (Widgets.ButtonInvisible(clickableDrillRect))
                        {
                            // Only jump if not clicking on checkbox
                            if (!checkboxClickRect.Contains(Event.current.mousePosition))
                            {
                                if (Find.CurrentMap != null)
                                {
                                    GlobalTargetInfo target = new GlobalTargetInfo(drill);
                                    CameraJumper.TryJumpAndSelect(target);
                                }
                            }
                        }
                        
                Text.Anchor = TextAnchor.UpperLeft;
                        currentY += drillRowHeight + 1f;
                    }
                }

                displayIndex++;
            }

            Widgets.EndScrollView();

            // Draw arrow indicator for hovered deposit using RimWorld's LookTargets system
            // This is the same system RimWorld uses for letters - calling TryHighlight() shows the arrow
            if (hoveredLookTargets != null && hoveredLookTargets.IsValid)
            {
                // Call TryHighlight() just like RimWorld's LetterStack does
                // This will show the arrow pointing to the deposit location
                hoveredLookTargets.TryHighlight();
            }

        }
    }
}

