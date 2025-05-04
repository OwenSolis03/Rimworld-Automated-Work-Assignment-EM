using System;
using System.Collections.Generic;
using System.Linq;
using Automated_Work_Assignment;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// GameComponent that manages the skill-based priority rules PER SAVE GAME.
    /// Stores the rules and contains the assignment logic.
    /// </summary>
    public class ExpertModeRuleManager : GameComponent
    {
        /// <summary>
        /// Main dictionary storing the rules.
        /// Key: WorkTypeDef (e.g., Mining, Construction)
        /// Value: List of SkillPriorityRule instances for that work type.
        /// </summary>
        public Dictionary<WorkTypeDef, List<SkillPriorityRule>> workTypeRules =
            new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();

        // Temporary lists required by Scribe for dictionary serialization
        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList; // Note: List of lists

        // Counter to avoid running logic every tick
        private int ticksCounter = 0;
        private const int TicksInterval = 120; // Run every 120 ticks (2 seconds) - Adjust if needed

        // Default priority if no rule matches or work is disabled for a pawn
        private const int DefaultPriority = 0;

        /// <summary>
        /// Required constructor for GameComponent.
        /// </summary>
        public ExpertModeRuleManager(Game game) { } // Empty constructor is sufficient

        /// <summary>
        /// Method called periodically by the game engine.
        /// Triggers the assignment logic periodically to enforce Expert Mode rules.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Periodically run the assignment logic to catch manual changes or ensure rules are applied
            ticksCounter++;
            if (ticksCounter >= TicksInterval)
            {
                ticksCounter = 0;
                // Run assignment logic periodically if the game state allows
                if (CanRunLogic()) {
                    AssignPrioritiesBasedOnRules();
                }
            }
        }

        /// <summary>
        /// Checks if the assignment logic can run (e.g., game loaded, map exists).
        /// </summary>
        private bool CanRunLogic() {
            return Current.Game != null && Current.Game.World != null && Find.CurrentMap != null && Find.TickManager != null && !Find.TickManager.Paused;
        }


        /// <summary>
        /// Assigns priorities to pawns based on the defined skill rules.
        /// This method is called periodically by GameComponentTick,
        /// and also by the Harmony postfix after AWA's RefreshAssignments runs.
        /// It only affects pawns and work types for which rules are defined.
        /// </summary>
        public void AssignPrioritiesBasedOnRules() // Public so Harmony postfix can call it
        {
            // Check if there are actually any rules defined globally. If not, do nothing.
            if (workTypeRules == null || workTypeRules.Count == 0 || !workTypeRules.Any(kvp => kvp.Key != null && kvp.Value != null && kvp.Value.Count > 0))
            {
                // Log.Message("[AWA Expert Mode] AssignPrioritiesBasedOnRules called, but no rules are defined. Skipping."); // Optional debug
                return;
            }

            // Log.Message("[AWA Expert Mode] Running AssignPrioritiesBasedOnRules..."); // Optional debug log

            // Access AWA's SaveData - Ensure the 'using' statement for AWA's namespace is present
            AutomatedWork_SaveData awaSaveData = Current.Game?.GetComponent<AutomatedWork_SaveData>();
            if (awaSaveData == null)
            {
                // Log only once per session to avoid spam
                Log.ErrorOnce("[AWA Expert Mode] Could not find AWA's AutomatedWork_SaveData component. Cannot get eligible colonists or respect exclusions.", 9487201);
                return; // Cannot proceed without AWA's data
            }

            List<Pawn> colonists = GetEligibleColonistsForExpertMode(awaSaveData);
            if (colonists == null || !colonists.Any())
            {
                return; // No eligible colonists found
            }

            // Iterate through all colonists
            foreach (Pawn pawn in colonists)
            {
                // Basic null checks for pawn and required components
                if (pawn?.skills == null || pawn.workSettings == null || pawn.Dead || pawn.Downed) continue;

                // Iterate ONLY through WorkTypeDefs that have rules defined in this mod
                foreach (var kvp in this.workTypeRules) // Iterate dictionary directly
                {
                    WorkTypeDef workDef = kvp.Key;
                    List<SkillPriorityRule> rules = kvp.Value;

                    // Check again if rules exist for this specific workDef and the workDef itself is valid
                    if (workDef == null || rules == null || !rules.Any())
                    {
                        continue;
                    }

                    int priorityToSet = DefaultPriority; // Start with default (usually 0)

                    if (pawn.WorkTypeIsDisabled(workDef))
                    {
                        priorityToSet = DefaultPriority; // If disabled in vanilla settings, respect it
                    }
                    else
                    {
                        // Determine skill level (treat as 0 if no relevant skill exists)
                        SkillDef relevantSkill = workDef.relevantSkills?.FirstOrDefault();
                        int skillLevel = 0;
                        if (relevantSkill != null)
                        {
                            skillLevel = pawn.skills.GetSkill(relevantSkill)?.Level ?? 0;
                        }

                        // Find the first matching rule based on the skill level
                        SkillPriorityRule matchingRule = rules.FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);

                        if (matchingRule != null)
                        {
                            priorityToSet = matchingRule.Priority; // Apply priority from the rule
                        }
                        else
                        {
                            // If no rule matches the current skill level (including level 0 for skill-less jobs),
                            // set priority to 0, effectively disabling it unless manually set otherwise.
                            priorityToSet = DefaultPriority;
                        }
                    }

                    // Only set priority if it's different from current to potentially reduce redundant calls
                    if (pawn.workSettings.GetPriority(workDef) != priorityToSet)
                    {
                        pawn.workSettings.SetPriority(workDef, priorityToSet);
                    }

                } // End loop WorkTypeDef
            } // End loop Pawn
        }


        /// <summary>
        /// Gets a list of colonists eligible for automatic work assignment by Expert Mode.
        /// This logic aims to mirror AWA's internal GetEligibleColonists.
        /// Respects the exclusions defined in AWA's settings.
        /// </summary>
        /// <param name="saveData">The current save game's AWA data component containing the exclusion list.</param>
        /// <returns>A list of eligible Pawn objects.</returns>
        private List<Pawn> GetEligibleColonistsForExpertMode(AutomatedWork_SaveData saveData)
        {
            // Ensure saveData is not null before accessing its members
            List<string> excludedIDs = saveData?.excludedPawnIDs ?? new List<string>();

            // Ensure map and pawns list exist
            if (Find.CurrentMap?.mapPawns == null) return new List<Pawn>();

            try
            {
                // Use FreeColonistsSpawned which is generally preferred
                return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                           ?.Where(p => p != null              // Basic null check
                                        && !p.Dead               // Not dead
                                        && !p.Downed             // Not downed
                                        // && p.Spawned          // FreeColonistsSpawned implies spawned
                                        && p.Faction == Faction.OfPlayer // Belongs to player faction
                                        && p.HostFaction == null // Not a guest/prisoner
                                        && p.workSettings != null // Has work settings component
                                        && !p.DevelopmentalStage.Baby() // Exclude babies
                                        && (saveData == null || !excludedIDs.Contains(p.ThingID)) // Respect exclusion list
                           )
                           .ToList()
                       ?? new List<Pawn>(); // Return empty list if query results in null
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception in GetEligibleColonistsForExpertMode: {ex}");
                return new List<Pawn>(); // Return empty list on error
            }
        }


        /// <summary>
        /// Saves and loads the rules (`workTypeRules`) for the current game.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Save/Load the dictionary
            Scribe_Collections.Look(ref workTypeRules, "workTypeRules_EM", // Changed label slightly for clarity
                LookMode.Def, LookMode.Deep, // Key=WorkTypeDef, Value=List<Rule> (needs Deep)
                ref workTypeDefKeysWorkingList, ref skillPriorityRuleValuesWorkingList);

            // Post Load Initialization / Cleanup
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // If the dictionary itself is null after loading, create a new one
                if (workTypeRules == null)
                {
                    workTypeRules = new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
                    Log.Warning("[AWA Expert Mode] workTypeRules was null after loading, initialized as empty.");
                }
                else // If dictionary exists, clean up potentially null values inside
                {
                    // Remove entries where the WorkTypeDef key might have become invalid (e.g., mod removed)
                    workTypeRules = workTypeRules
                        .Where(kvp => kvp.Key != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);


                    // Ensure lists within the dictionary are not null
                    List<WorkTypeDef> keys = workTypeRules.Keys.ToList(); // Operate on a copy of keys
                    foreach (WorkTypeDef key in keys)
                    {
                        if (workTypeRules[key] == null)
                        {
                            Log.Warning($"[AWA Expert Mode] Rule list for {key.defName} was null after loading, initializing as empty list.");
                            workTypeRules[key] = new List<SkillPriorityRule>();
                        }
                        else
                        {
                            // Optional: Remove null rules from within the list
                            workTypeRules[key].RemoveAll(rule => rule == null);
                        }
                    }
                }
            }
        }
    }
}