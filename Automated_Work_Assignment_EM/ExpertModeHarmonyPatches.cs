using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Contains Harmony patches applied by the Expert Mode addon.
    /// - Patches AWA's settings window to add an access button.
    /// - Patches AWA's assignment logic (Postfix) to apply Expert Mode rules *after* AWA runs.
    /// </summary>
    [StaticConstructorOnStartup] // Ensures the static constructor runs on game startup
    public static class ExpertModeHarmonyPatches
    {
        /// <summary>
        /// Static constructor applies Harmony patches when the game loads.
        /// </summary>
        static ExpertModeHarmonyPatches()
        {
            var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment.ExpertMode.Patches");
            Log.Message("[AWA Expert Mode] Applying Harmony patches...");

            // Patch for adding button to AWA's Settings Window
            PatchAWASettingsWindow(harmony);

            // Patch for applying EM rules AFTER AWA's Assignment Logic
            PatchAWARefreshAssignments_Postfix(harmony);

        }

        /// <summary>
        /// Applies the postfix patch to AWA's settings window.
        /// </summary>
        private static void PatchAWASettingsWindow(Harmony harmony)
        {
            try
            {
                // Ensure the package ID is correct based on the latest About.xml
                Type awaModType = AccessTools.TypeByName("Automated_Work_Assignment.AutomatedWorkAssignmentMod");
                if (awaModType == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find type 'Automated_Work_Assignment.AutomatedWorkAssignmentMod'."); return; }

                MethodInfo originalMethod = AccessTools.Method(awaModType, "DoSettingsWindowContents", new Type[] { typeof(Rect) });
                if (originalMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find method 'DoSettingsWindowContents(Rect)' in AWA's Mod class."); return; }

                MethodInfo postfixMethod = AccessTools.Method(typeof(ExpertModeHarmonyPatches), nameof(AWASettingsWindowPostfix));
                if (postfixMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find Postfix method 'AWASettingsWindowPostfix'."); return; }

                harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                Log.Message("[AWA Expert Mode] Successfully patched AWA's DoSettingsWindowContents.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception during AWA Settings Window Harmony patching: {ex}");
            }
        }

        /// <summary>
        /// Applies the POSTFIX patch to AWA's main assignment logic method.
        /// </summary>
        private static void PatchAWARefreshAssignments_Postfix(Harmony harmony) // Renamed for clarity
        {
            try
            {
                // Target method is static: Automated_Work_Assignment.WorkAssigner.RefreshAssignments()
                Type awaWorkAssignerType = AccessTools.TypeByName("Automated_Work_Assignment.WorkAssigner");
                if (awaWorkAssignerType == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find type 'Automated_Work_Assignment.WorkAssigner'."); return; }

                MethodInfo originalMethod = AccessTools.Method(awaWorkAssignerType, "RefreshAssignments");
                if (originalMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find method 'RefreshAssignments' in AWA's WorkAssigner class."); return; }

                // Get the Postfix method from this class
                MethodInfo postfixMethod = AccessTools.Method(typeof(ExpertModeHarmonyPatches), nameof(RefreshAssignmentsPostfix));
                if (postfixMethod == null) { Log.Error("[AWA Expert Mode] Harmony Patch Error: Could not find Postfix method 'RefreshAssignmentsPostfix'."); return; }

                // Apply the POSTFIX patch
                harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                Log.Message("[AWA Expert Mode] Successfully applied POSTFIX patch to AWA's RefreshAssignments.");

            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception during AWA RefreshAssignments Postfix Harmony patching: {ex}");
            }
        }


        /// <summary>
        /// Harmony Postfix patch for AutomatedWorkAssignmentMod.DoSettingsWindowContents.
        /// Adds a button to open the Expert Mode rule configuration dialog.
        /// </summary>
        public static void AWASettingsWindowPostfix(Rect inRect)
        {
            // This postfix adds a button to the *base AWA* settings window
            try
            {
                // Ensure game is running and component exists
                if (Current.Game == null || Current.Game.GetComponent<ExpertModeRuleManager>() == null) {
                    // Optionally draw a disabled button or message if game not loaded
                    return;
                }

                float buttonWidth = 200f;
                float buttonHeight = 30f;
                float padding = 5f; // Padding from bottom and left/right edges

                // Position the button, e.g., at the bottom right or bottom left
                // Example: Bottom Right
                // float buttonX = inRect.xMax - buttonWidth - padding;
                // float buttonY = inRect.yMax - buttonHeight - padding;

                // Example: Bottom Left (might overlap if AWA uses that space)
                float buttonX = inRect.x + padding;
                float buttonY = inRect.yMax - buttonHeight - padding;


                Rect expertModeButtonRect = new Rect(buttonX, buttonY, buttonWidth, buttonHeight);

                if (Widgets.ButtonText(expertModeButtonRect, "AWA_ExpertMode_ConfigureRulesButton".Translate())) // Use translation key
                {
                    // Log.Message("[AWA Expert Mode] Configure Rules button clicked!"); // Optional debug log
                    try
                    {
                        Find.WindowStack.Add(new Dialog_ExpertModeSettings());
                    }
                    catch (Exception windowEx)
                    {
                        Log.Error($"[AWA Expert Mode] Exception while trying to open Dialog_ExpertModeSettings: {windowEx}");
                    }
                }
                TooltipHandler.TipRegion(expertModeButtonRect, "AWA_ExpertMode_ConfigureRulesTooltip".Translate()); // Use translation key
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA Expert Mode] Exception in AWASettingsWindowPostfix patch: {ex}");
            }
        }

        /// <summary>
        /// Harmony POSTFIX patch for Automated_Work_Assignment.WorkAssigner.RefreshAssignments.
        /// Runs *after* AWA's logic and applies Expert Mode rules for relevant work types.
        /// </summary>
        public static void RefreshAssignmentsPostfix()
        {
            // Log.Message("[AWA Expert Mode] RefreshAssignmentsPostfix executing..."); // Optional debug log
            if (Current.Game == null) return; // Safety check

            var expertModeManager = Current.Game.GetComponent<ExpertModeRuleManager>();

            // Check if manager exists and has any rules defined more robustly
            bool hasRules = expertModeManager != null
                            && expertModeManager.workTypeRules != null
                            && expertModeManager.workTypeRules.Count > 0
                            && expertModeManager.workTypeRules.Any(kvp => kvp.Key != null && kvp.Value != null && kvp.Value.Count > 0); // Ensure key and value list are valid

            if (hasRules)
            {
                // Expert Mode has rules, apply them now (potentially overwriting AWA's settings for these jobs)
                try
                {
                    // Log.Message("[AWA Expert Mode] Postfix: Running Expert Mode assignment logic..."); // Optional debug log
                    expertModeManager.AssignPrioritiesBasedOnRules();
                }
                catch (Exception ex)
                {
                    Log.Error($"[AWA Expert Mode] Exception during Postfix trigger of AssignPrioritiesBasedOnRules: {ex}");
                }
            }
            // If no rules, do nothing - AWA's assignments remain untouched.
            // else { Log.Message("[AWA Expert Mode] Postfix: No rules defined, doing nothing."); } // Optional debug log
        }
    }
}