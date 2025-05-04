using System;
using System.Collections.Generic;
using System.Linq;
using Automated_Work_Assignment; // Assuming this namespace exists from the original AWA mod
using RimWorld;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Manages the skill-based priority rules (Expert Mode) for a specific save game.
    /// This component is attached to the Game object and handles storing, loading,
    /// and applying the custom priority rules defined by the player.
    /// It periodically checks and applies these rules to eligible colonists.
    /// </summary>
    public class ExpertModeRuleManager : GameComponent
    {
        /// <summary>
        /// The core data structure holding all defined Expert Mode rules.
        /// It maps each <see cref="WorkTypeDef"/> (like Mining, Cooking) to a list
        /// of <see cref="SkillPriorityRule"/> objects that define priorities based on skill levels for that work type.
        /// This dictionary is serialized with the save game.
        /// </summary>
        public Dictionary<WorkTypeDef, List<SkillPriorityRule>> workTypeRules =
            new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();

        /// <summary>
        /// Temporary list used by RimWorld's Scribe system during the serialization (saving/loading)
        /// of the <see cref="workTypeRules"/> dictionary keys (<see cref="WorkTypeDef"/>).
        /// </summary>
        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        /// <summary>
        /// Temporary list used by RimWorld's Scribe system during the serialization (saving/loading)
        /// of the <see cref="workTypeRules"/> dictionary values (List&lt;<see cref="SkillPriorityRule"/>&gt;).
        /// </summary>
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList; // Note: List of lists

        /// <summary>
        /// Internal counter used to throttle the execution of the assignment logic in <see cref="GameComponentTick"/>.
        /// </summary>
        private int ticksCounter = 0;
        /// <summary>
        /// Defines the interval (in game ticks) at which the <see cref="AssignPrioritiesBasedOnRules"/> logic
        /// is triggered by <see cref="GameComponentTick"/>. 120 ticks corresponds to approximately 2 real-time seconds at normal speed.
        /// </summary>
        private const int TicksInterval = 120;

        /// <summary>
        /// The default priority value (typically 0 for 'disabled') assigned to a pawn's work type
        /// if no Expert Mode rule matches their skill level, or if the work type is explicitly disabled
        /// in the pawn's standard work settings.
        /// </summary>
        private const int DefaultPriority = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModeRuleManager"/> class.
        /// Required constructor for RimWorld's GameComponent system.
        /// </summary>
        /// <param name="game">The current game instance this component belongs to.</param>
        public ExpertModeRuleManager(Game game) { } // Base constructor is sufficient.

        /// <summary>
        /// Called by the RimWorld game engine on every game tick.
        /// This implementation uses a counter (<see cref="ticksCounter"/>) to periodically
        /// trigger the <see cref="AssignPrioritiesBasedOnRules"/> method, ensuring rules are
        /// reapplied even if manual changes occur or to catch state changes.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            ticksCounter++;
            if (ticksCounter >= TicksInterval)
            {
                ticksCounter = 0;
                // Run assignment logic periodically if the game state allows (not paused, map exists etc.)
                if (CanRunLogic()) {
                    AssignPrioritiesBasedOnRules();
                }
            }
        }

        /// <summary>
        /// Determines if the conditions are suitable for running the priority assignment logic.
        /// Checks if the game is loaded, a map exists, and the game is not paused.
        /// </summary>
        /// <returns><c>true</c> if the logic can run; otherwise, <c>false</c>.</returns>
        private bool CanRunLogic() {
            // Ensure game, world, map, and tick manager are available and the game isn't paused.
            return Current.Game != null
                && Current.Game.World != null
                && Find.CurrentMap != null
                && Find.TickManager != null
                && !Find.TickManager.Paused;
        }


        /// <summary>
        /// Iterates through eligible colonists and defined Expert Mode rules, setting pawn work priorities
        /// based on their skills and the configured rules.
        /// This method is the core logic of the Expert Mode. It fetches eligible pawns,
        /// checks their skills against the rules for each relevant work type, and applies the
        /// corresponding priority found in the matching rule. Respects vanilla work disabling and AWA exclusions.
        /// This method can be called periodically by <see cref="GameComponentTick"/> or directly (e.g., by Harmony patches).
        /// </summary>
        public void AssignPrioritiesBasedOnRules() // Public exposure allows Harmony patches to call it directly after AWA updates.
        {
            // Early exit if no rules are defined at all to save performance.
            if (workTypeRules == null || workTypeRules.Count == 0 || !workTypeRules.Any(kvp => kvp.Key != null && kvp.Value != null && kvp.Value.Count > 0))
            {
                return;
            }

            // Attempt to get AWA's save data to respect its pawn exclusion list.
            AutomatedWork_SaveData awaSaveData = Current.Game?.GetComponent<AutomatedWork_SaveData>();
            if (awaSaveData == null)
            {
                Log.ErrorOnce("[AWA Expert Mode] Could not find AWA's AutomatedWork_SaveData component. Cannot get eligible colonists or respect exclusions.", 9487201);
                return; // Cannot proceed without AWA's exclusion data.
            }

            // Get the list of pawns eligible for rule application.
            List<Pawn> colonists = GetEligibleColonistsForExpertMode(awaSaveData);
            if (colonists == null || !colonists.Any())
            {
                return; // No eligible colonists found.
            }

            // Process each eligible colonist.
            foreach (Pawn pawn in colonists)
            {
                // Skip invalid pawns (null, dead, downed, or missing necessary components).
                if (pawn?.skills == null || pawn.workSettings == null || pawn.Dead || pawn.Downed) continue;

                // Iterate through only the work types that have rules defined in Expert Mode.
                foreach (var kvp in this.workTypeRules)
                {
                    WorkTypeDef workDef = kvp.Key;
                    List<SkillPriorityRule> rules = kvp.Value;

                    // Skip if the workDef became invalid or has no rules after cleanup.
                    if (workDef == null || rules == null || !rules.Any())
                    {
                        continue;
                    }

                    int priorityToSet = DefaultPriority; // Default to disabled priority.

                    // Respect the pawn's standard work settings - if disabled there, keep it disabled (priority 0).
                    if (pawn.WorkTypeIsDisabled(workDef))
                    {
                        priorityToSet = DefaultPriority;
                    }
                    else // If enabled in standard settings, apply Expert Mode rules.
                    {
                        // Determine the pawn's skill level for this work type's relevant skill.
                        SkillDef relevantSkill = workDef.relevantSkills?.FirstOrDefault();
                        int skillLevel = 0; // Assume 0 if no relevant skill or pawn lacks the skill record.
                        if (relevantSkill != null)
                        {
                            skillLevel = pawn.skills.GetSkill(relevantSkill)?.Level ?? 0;
                        }

                        // Find the first rule that matches the pawn's current skill level.
                        SkillPriorityRule matchingRule = rules.FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);

                        if (matchingRule != null)
                        {
                            // Apply the priority defined in the matching rule.
                            priorityToSet = matchingRule.Priority;
                        }
                        else
                        {
                            // If no rule matches (e.g., skill is outside all defined ranges),
                            // set priority to default (disabled), overriding any manual setting.
                            priorityToSet = DefaultPriority;
                        }
                    }

                    // Apply the determined priority to the pawn's work settings if it's different from the current value.
                    if (pawn.workSettings.GetPriority(workDef) != priorityToSet)
                    {
                        pawn.workSettings.SetPriority(workDef, priorityToSet);
                    }

                } // End loop through WorkTypeDefs with rules
            } // End loop through Pawns
        }


        /// <summary>
        /// Retrieves a list of colonists currently eligible for work assignment rules.
        /// This filters the map's colonists based on criteria like being alive, not downed,
        /// belonging to the player faction, not being a baby, and crucially, respecting
        /// the exclusion list configured in the base Automated Work Assignment mod's settings.
        /// </summary>
        /// <param name="saveData">The <see cref="AutomatedWork_SaveData"/> component instance containing AWA's settings, including the pawn exclusion list.</param>
        /// <returns>A list of <see cref="Pawn"/> objects eligible for Expert Mode rule application. Returns an empty list if none are found or an error occurs.</returns>
        private List<Pawn> GetEligibleColonistsForExpertMode(AutomatedWork_SaveData saveData)
        {
            // Safely access the exclusion list, defaulting to empty if saveData is null.
            List<string> excludedIDs = saveData?.excludedPawnIDs ?? new List<string>();

            // Ensure the map and its pawn list are accessible.
            if (Find.CurrentMap?.mapPawns == null) return new List<Pawn>();

            try
            {
                // Query the free, spawned colonists on the current map.
                return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                           ?.Where(p => p != null                         // Ensure pawn exists
                                        && !p.Dead                          // Is alive
                                        && !p.Downed                        // Is not downed
                                        // && p.Spawned                     // Already guaranteed by FreeColonistsSpawned
                                        && p.Faction == Faction.OfPlayer    // Belongs to the player
                                        && p.HostFaction == null            // Is not a guest or prisoner
                                        && p.workSettings != null           // Has work settings component
                                        && !p.DevelopmentalStage.Baby()     // Is not a baby
                                        && (saveData == null || !excludedIDs.Contains(p.ThingID)) // Is not in AWA's exclusion list
                           )
                           .ToList()
                       ?? new List<Pawn>(); // Return empty list if the LINQ query itself results in null.
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception in GetEligibleColonistsForExpertMode: {ex}");
                return new List<Pawn>(); // Return empty list in case of unexpected errors during filtering.
            }
        }


        /// <summary>
        /// Handles saving and loading the component's data (<see cref="workTypeRules"/>)
        /// as part of the RimWorld save game process. Uses the Scribe system.
        /// Includes post-load cleanup to handle potential data corruption or inconsistencies
        /// (e.g., null rules, rules referencing removed WorkTypeDefs).
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Use Scribe_Collections to save/load the dictionary.
            // LookMode.Def for WorkTypeDef keys (references definitions).
            // LookMode.Deep for List<SkillPriorityRule> values (serializes the list content and rule objects).
            // Use slightly different label ("_EM") to avoid potential conflicts if AWA used a similar label.
            Scribe_Collections.Look(ref workTypeRules, "workTypeRules_EM",
                LookMode.Def, LookMode.Deep,
                ref workTypeDefKeysWorkingList, ref skillPriorityRuleValuesWorkingList);

            // Perform cleanup and initialization after all data has been loaded.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // If the entire dictionary failed to load, initialize it as empty.
                if (workTypeRules == null)
                {
                    workTypeRules = new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
                    Log.Warning("[AWA Expert Mode] workTypeRules dictionary was null after loading. Initialized as empty.");
                }
                else // If the dictionary loaded, perform internal cleanup.
                {
                    // Remove entries where the WorkTypeDef key is null (e.g., the Def was removed by uninstalling a mod).
                    workTypeRules = workTypeRules
                        .Where(kvp => kvp.Key != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Iterate through the remaining entries to clean up the rule lists.
                    List<WorkTypeDef> keys = workTypeRules.Keys.ToList(); // Create a copy of keys for safe iteration.
                    foreach (WorkTypeDef key in keys)
                    {
                        // If a rule list for a valid key is null, initialize it as an empty list.
                        if (workTypeRules[key] == null)
                        {
                            Log.Warning($"[AWA Expert Mode] Rule list for WorkTypeDef '{key.defName}' was null after loading. Initialized as empty list.");
                            workTypeRules[key] = new List<SkillPriorityRule>();
                        }
                        else
                        {
                            // Remove any null SkillPriorityRule objects from within the list.
                            int removedCount = workTypeRules[key].RemoveAll(rule => rule == null);
                            // if (removedCount > 0) { // Optional logging
                            //     Log.Warning($"[AWA Expert Mode] Removed {removedCount} null rules from list for WorkTypeDef '{key.defName}'.");
                            // }
                        }
                    }
                }
            }
        }
    }
}