using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Represents a specific rule mapping a skill range to a priority.
    /// Implements IExposable to be saved/loaded with LookMode.Deep.
    /// </summary>
    public class SkillPriorityRule : IExposable
    {
        /// <summary>
        /// The minimum skill level (inclusive) for this rule to apply.
        /// </summary>
        public int MinSkill = 0;
        /// <summary>
        /// The maximum skill level (inclusive) for this rule to apply.
        /// </summary>
        public int MaxSkill = 20;
        /// <summary>
        /// The work priority (1-4) to assign if a pawn's skill falls within [MinSkill, MaxSkill].
        /// </summary>
        public int Priority = 3; // Priority 1-4 (RimWorld standard: 1 is highest)

        /// <summary>
        /// Default constructor required for IExposable and dynamic creation.
        /// </summary>
        public SkillPriorityRule() { }

        /// <summary>
        /// Convenience constructor to create a rule with specific values.
        /// Ensures values are within valid ranges upon creation.
        /// </summary>
        /// <param name="min">Minimum skill level (0-20).</param>
        /// <param name="max">Maximum skill level (0-20).</param>
        /// <param name="prio">Target priority (1-4).</param>
        public SkillPriorityRule(int min, int max, int prio)
        {
            // Use Mathf.Clamp for concise boundary checking
            MinSkill = Mathf.Clamp(min, 0, 20);
            MaxSkill = Mathf.Clamp(max, 0, 20);
            Priority = Mathf.Clamp(prio, 1, 4);

            // Ensure MinSkill is not greater than MaxSkill after clamping
            if (MinSkill > MaxSkill) MinSkill = MaxSkill;
        }

        /// <summary>
        /// Saves and loads the fields for this rule using RimWorld's Scribe system.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref MinSkill, "minSkill", 0);
            Scribe_Values.Look(ref MaxSkill, "maxSkill", 20);
            Scribe_Values.Look(ref Priority, "priority", 3);

            // Post-load validation/clamping (important if data could be corrupted or from older versions)
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs) // Apply on load/resolve
            {
                MinSkill = Mathf.Clamp(MinSkill, 0, 20);
                MaxSkill = Mathf.Clamp(MaxSkill, 0, 20);
                Priority = Mathf.Clamp(Priority, 1, 4);

                // Ensure MinSkill <= MaxSkill after loading potentially invalid data
                if (MinSkill > MaxSkill)
                {
                    Log.Warning($"[AWA Expert Mode] Loaded SkillPriorityRule had minSkill ({MinSkill}) > maxSkill ({MaxSkill}). Clamping minSkill to maxSkill.");
                    MinSkill = MaxSkill;
                }
            }
        }
    }
}