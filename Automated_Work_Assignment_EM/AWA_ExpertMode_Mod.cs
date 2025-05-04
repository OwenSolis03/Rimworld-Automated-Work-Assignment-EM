using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Represents the main entry point for the "Automated Work Assignment: Expert Mode" addon mod.
    /// This class inherits from RimWorld's <see cref="Mod"/> class and is responsible for
    /// integrating the mod into the game's mod loading and settings system.
    /// It provides access to the mod's settings window.
    /// </summary>
    public class AWA_ExpertMode_Mod : Mod
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AWA_ExpertMode_Mod"/> class.
        /// This constructor is called by RimWorld when the mod is loaded.
        /// It logs a message indicating the mod has been initialized.
        /// </summary>
        /// <param name="content">The <see cref="ModContentPack"/> associated with this mod, containing its files and metadata.</param>
        public AWA_ExpertMode_Mod(ModContentPack content) : base(content)
        {
            // Log a message to confirm the mod class itself has been instantiated.
            Log.Message("[AWA Expert Mode] Main mod class initialized.");
            // Note: Harmony patches are applied via [StaticConstructorOnStartup] in ExpertModeHarmonyPatches, not here.
        }

        /// <summary>
        /// Gets the title string displayed for this mod in RimWorld's mod settings menu.
        /// </summary>
        /// <returns>A string representing the name of the mod in the settings list, e.g., "AWA: Expert Mode".</returns>
        public override string SettingsCategory()
        {
            // Returns the display name for the mod settings category.
            return "AWA: Expert Mode";
        }

        /// <summary>
        /// Draws the content area for this mod within RimWorld's mod settings window.
        /// This implementation checks if a game is currently loaded. If not, it displays a message
        /// instructing the user to load a save first. If a game is loaded, it provides a button
        /// that opens the main configuration dialog (<see cref="Dialog_ExpertModeSettings"/>)
        /// where users can define the Expert Mode rules.
        /// </summary>
        /// <param name="inRect">The <see cref="Rect"/> defining the drawable area within the settings window.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Check if a game is active and the required game component is present.
            // Settings are per-save, so configuration requires an active game.
            if (Current.Game == null || Current.Game.GetComponent<ExpertModeRuleManager>() == null)
            {
                // If no game is loaded, display an informational message.
                Listing_Standard listingStandard = new Listing_Standard();
                listingStandard.Begin(inRect);
                listingStandard.Label("AWA_ExpertMode_LoadSaveFirst".Translate()); // Use translation key
                listingStandard.End();
                return; // Stop further drawing
            }

            // If a game is loaded, draw the button to open the rule editor.
            Listing_Standard listingStandardConfig = new Listing_Standard();
            listingStandardConfig.Begin(inRect);

            // Draw the main configuration button.
            if (listingStandardConfig.ButtonText("AWA_ExpertMode_ConfigureRulesButton".Translate())) // Use translation key
            {
                // When clicked, create and add the settings dialog to the window stack.
                Find.WindowStack.Add(new Dialog_ExpertModeSettings());
            }
            // Add tooltip for the button (using the same key as the Harmony patch button for consistency, if desired, or a unique one)
            TooltipHandler.TipRegion(listingStandardConfig.GetRect(30f), "AWA_ExpertMode_ConfigureRulesTooltip".Translate()); // Assuming GetRect returns the last element's rect

            listingStandardConfig.End();

            // Call the base method implementation (optional, usually for ModSettings).
            // base.DoSettingsWindowContents(inRect); // Not needed here as we don't use ModSettings directly.
        }
    }
}