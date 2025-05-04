using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Manages the application of Harmony patches required for the Expert Mode addon.
    /// This static class is responsible for injecting code into the original
    /// Automated Work Assignment (AWA) mod's methods to integrate Expert Mode functionality.
    /// It specifically patches AWA's settings window to add a configuration button
    /// and patches AWA's core assignment logic to apply Expert Mode rules afterwards.
    /// </summary>
    [StaticConstructorOnStartup] // Ensures the static constructor runs automatically when the game starts.
    public static class ExpertModeHarmonyPatches
    {
        /// <summary>
        /// Initializes the <see cref="ExpertModeHarmonyPatches"/> class.
        /// This static constructor is executed once on game startup due to the <see cref="StaticConstructorOnStartup"/> attribute.
        /// It creates a Harmony instance and applies all necessary patches.
        /// </summary>
        static ExpertModeHarmonyPatches()
        {
            // Create a unique Harmony instance for this mod's patches.
            var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment.ExpertMode.Patches");
            Log.Message("[AWA Expert Mode] Applying Harmony patches...");

            // Apply the patch to add the button to AWA's settings window.
            PatchAWASettingsWindow(harmony);

            // Apply the postfix patch to AWA's assignment logic.
            PatchAWARefreshAssignments_Postfix(harmony);
        }

        /// <summary>
        /// Locates and patches the 'DoSettingsWindowContents' method in AWA's main mod class.
        /// This adds the <see cref="AWASettingsWindowPostfix"/> method to run after the original,
        /// allowing the addition of the Expert Mode configuration button.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        private static void PatchAWASettingsWindow(Harmony harmony)
        {
            try
            {
                // Dynamically find the target type and method using reflection.
                Type awaModType = AccessTools.TypeByName("Automated_Work_Assignment.AutomatedWorkAssignmentMod");
                if (awaModType == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find type 'Automated_Work_Assignment.AutomatedWorkAssignmentMod'. Ensure AWA mod is active and loaded before Expert Mode."); return; }

                MethodInfo originalMethod = AccessTools.Method(awaModType, "DoSettingsWindowContents", new Type[] { typeof(Rect) });
                if (originalMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find method 'DoSettingsWindowContents(Rect)' in AWA's Mod class. AWA mod might have updated."); return; }

                // Find the postfix method within this class.
                MethodInfo postfixMethod = AccessTools.Method(typeof(ExpertModeHarmonyPatches), nameof(AWASettingsWindowPostfix));
                if (postfixMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find Postfix method 'AWASettingsWindowPostfix'."); return; }

                // Apply the patch.
                harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                Log.Message("[AWA Expert Mode] Successfully patched AWA's DoSettingsWindowContents.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception during AWA Settings Window Harmony patching: {ex}");
            }
        }

        /// <summary>
        /// Locates and patches the 'RefreshAssignments' method in AWA's WorkAssigner class.
        /// This adds the <see cref="RefreshAssignmentsPostfix"/> method to run *after* AWA's original
        /// assignment logic, allowing Expert Mode rules to potentially override AWA's assignments.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        private static void PatchAWARefreshAssignments_Postfix(Harmony harmony)
        {
            try
            {
                // Dynamically find the target type and method using reflection.
                Type awaWorkAssignerType = AccessTools.TypeByName("Automated_Work_Assignment.WorkAssigner");
                if (awaWorkAssignerType == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find type 'Automated_Work_Assignment.WorkAssigner'. Ensure AWA mod is active and loaded before Expert Mode."); return; }

                MethodInfo originalMethod = AccessTools.Method(awaWorkAssignerType, "RefreshAssignments");
                if (originalMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find method 'RefreshAssignments' in AWA's WorkAssigner class. AWA mod might have updated."); return; }

                // Find the postfix method within this class.
                MethodInfo postfixMethod = AccessTools.Method(typeof(ExpertModeHarmonyPatches), nameof(RefreshAssignmentsPostfix));
                if (postfixMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find Postfix method 'RefreshAssignmentsPostfix'."); return; }

                // Apply the postfix patch.
                harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                Log.Message("[AWA Expert Mode] Successfully applied POSTFIX patch to AWA's RefreshAssignments.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception during AWA RefreshAssignments Postfix Harmony patching: {ex}");
            }
        }


        /// <summary>
        /// Harmony Postfix method executed after <c>Automated_Work_Assignment.AutomatedWorkAssignmentMod.DoSettingsWindowContents(Rect)</c>.
        /// This method draws an additional button onto the AWA settings window, providing access
        /// to the Expert Mode configuration dialog (<see cref="Dialog_ExpertModeSettings"/>).
        /// </summary>
        /// <param name="inRect">The Rect provided by the original method, defining the area available for drawing settings content.</param>
        public static void AWASettingsWindowPostfix(Rect inRect)
        {
            try
            {
                // Only attempt to draw if the game is running and the manager component exists.
                if (Current.Game == null || Current.Game.GetComponent<ExpertModeRuleManager>() == null)
                {
                    return;
                }

                float buttonWidth = 200f;
                float buttonHeight = 30f;
                float padding = 5f;

                // Calculate position for the button (e.g., bottom-left).
                float buttonX = inRect.x + padding;
                float buttonY = inRect.yMax - buttonHeight - padding; // Positioned near the bottom edge.

                Rect expertModeButtonRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);

                // Draw the button. Uses translation keys for localization.
                if (Widgets.ButtonText(expertModeButtonRect, "AWA_ExpertMode_ConfigureRulesButton".Translate()))
                {
                    // On click, try to open the Expert Mode settings dialog.
                    try
                    {
                        Find.WindowStack.Add(new Dialog_ExpertModeSettings());
                    }
                    catch (Exception windowEx)
                    {
                        Log.Error($"[AWA Expert Mode] Exception while trying to open Dialog_ExpertModeSettings: {windowEx}");
                    }
                }
                // Add a tooltip to the button. Uses translation keys.
                TooltipHandler.TipRegion(expertModeButtonRect, "AWA_ExpertMode_ConfigureRulesTooltip".Translate());
            }
            catch (Exception ex)
            {
                // Log any exceptions occurring within the patch itself.
                Log.Error($"[AWA Expert Mode] Exception in AWASettingsWindowPostfix patch: {ex}");
            }
        }

        /// <summary>
        /// Harmony Postfix method executed after <c>Automated_Work_Assignment.WorkAssigner.RefreshAssignments()</c>.
        /// This method is called immediately after AWA finishes its own priority calculations.
        /// It retrieves the <see cref="ExpertModeRuleManager"/> and, if Expert Mode rules exist,
        /// calls its <see cref="ExpertModeRuleManager.AssignPrioritiesBasedOnRules"/> method.
        /// This ensures that Expert Mode rules are applied as the final step, potentially overriding
        /// priorities set by the base AWA logic for the work types managed by Expert Mode.
        /// </summary>
        public static void RefreshAssignmentsPostfix()
        {
            if (Current.Game == null) return; // Basic safety check

            var expertModeManager = Current.Game.GetComponent<ExpertModeRuleManager>();

            // Check if the manager exists and if any rules are actually defined.
            // This prevents unnecessary calls if Expert Mode is installed but not configured.
            bool hasRules = expertModeManager != null
                            && expertModeManager.workTypeRules != null
                            && expertModeManager.workTypeRules.Count > 0
                            && expertModeManager.workTypeRules.Any(kvp => kvp.Key != null && kvp.Value != null && kvp.Value.Count > 0);

            if (hasRules)
            {
                // If rules exist, trigger the Expert Mode assignment logic.
                try
                {
                    expertModeManager.AssignPrioritiesBasedOnRules();
                }
                catch (Exception ex)
                {
                    Log.Error($"[AWA Expert Mode] Exception during Postfix trigger of AssignPrioritiesBasedOnRules: {ex}");
                }
            }
            // If no rules are defined in the manager, this postfix does nothing, leaving AWA's assignments untouched.
        }
    }
}