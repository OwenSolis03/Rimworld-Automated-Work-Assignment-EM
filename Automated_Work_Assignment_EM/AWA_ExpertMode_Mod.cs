using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Main class for the "Automated Work Assignment: Expert Mode" Mod.
    /// Inherits from Verse.Mod and serves as the primary entry point.
    /// </summary>
    public class AWA_ExpertMode_Mod : Mod
    {
        /// <summary>
        /// Constructor for the mod. Called once when the mod is loaded.
        /// </summary>
        /// <param name="content">The mod's content pack.</param>
        public AWA_ExpertMode_Mod(ModContentPack content) : base(content)
        {
            Log.Message("[AWA Expert Mode] Main mod initialized.");
        }

        /// <summary>
        /// Defines the name that will appear in the Mod options menu.
        /// </summary>
        /// <returns>The title for the settings window.</returns>
        public override string SettingsCategory()
        {
            return "AWA: Expert Mode";
        }

        /// <summary>
        /// Draws the content of the mod's settings window.
        /// This is where the UI for configuring rules will be displayed (likely by opening Dialog_ExpertModeSettings).
        /// </summary>
        /// <param name="inRect">The area available for drawing.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Current.Game == null || Current.Game.GetComponent<ExpertModeRuleManager>() == null)
            {
                Listing_Standard listingStandard = new Listing_Standard();
                listingStandard.Begin(inRect);
                listingStandard.Label("AWA_ExpertMode_LoadSaveFirst".Translate());
                listingStandard.End();
                return;
            }

            Listing_Standard listingStandardConfig = new Listing_Standard();
            listingStandardConfig.Begin(inRect);

            if (listingStandardConfig.ButtonText("AWA_ExpertMode_ConfigureRulesButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ExpertModeSettings());
            }

            listingStandardConfig.End();
        }
    }
}