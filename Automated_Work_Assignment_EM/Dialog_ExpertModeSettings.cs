using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Provides a dialog window for configuring skill-based priority rules (Expert Mode)
    /// for different work types within the Automated Work Assignment mod system.
    /// It displays a list of available work types (filtered to those with work tags)
    /// and allows users to define skill range brackets and assign specific work priorities (1-4) to them for each selected work type.
    /// The rules configured here are managed by the <see cref="ExpertModeRuleManager"/>.
    /// </summary>
    public class Dialog_ExpertModeSettings : Window
    {
        /// <summary>
        /// Reference to the rule manager component (<see cref="ExpertModeRuleManager"/>) attached to the current game instance.
        /// Used to access and modify the expert mode rules.
        /// This is initialized in the constructor and checked for null before use in drawing methods.
        /// </summary>
        private ExpertModeRuleManager ruleManager;

        /// <summary>
        /// Stores the current vertical scroll position for the left panel, which lists the work types.
        /// Used by <see cref="Widgets.BeginScrollView"/>.
        /// </summary>
        private Vector2 scrollPositionLeft = Vector2.zero;
        /// <summary>
        /// Stores the current vertical scroll position for the right panel, which displays the rule editor.
        /// Used by <see cref="Widgets.BeginScrollView"/>.
        /// </summary>
        private Vector2 scrollPositionRight = Vector2.zero;
        /// <summary>
        /// Holds the currently selected work type definition (<see cref="WorkTypeDef"/>) for which rules are being displayed or edited in the right panel.
        /// Can be null if no work type is selected. Updated in <see cref="DrawWorkTypeDefList"/>.
        /// </summary>
        private WorkTypeDef selectedWorkDef = null;

        /// <summary>
        /// A list of all relevant <see cref="WorkTypeDef"/>s (those with `workTags != WorkTags.None`) available in the current game state.
        /// This list is populated and sorted in the constructor each time the dialog is created, ensuring it reflects currently loaded mods.
        /// It is used to populate the selectable list in the left panel (<see cref="DrawWorkTypeDefList"/>).
        /// </summary>
        private List<WorkTypeDef> relevantWorkTypesCache = new List<WorkTypeDef>();

        /// <summary>
        /// Gets the initial dimensions (width and height) of the dialog window when it first opens.
        /// Overrides the base <see cref="Window.InitialSize"/>.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog_ExpertModeSettings"/> class.
        /// Sets up standard window properties (pauses game, shows close button, draggable, etc.),
        /// retrieves the active <see cref="ExpertModeRuleManager"/> instance from the current game session,
        /// populates and sorts the cache of relevant work types (<see cref="relevantWorkTypesCache"/>) by querying the <see cref="DefDatabase{WorkTypeDef}"/>,
        /// and sets the translatable window title.
        /// </summary>
        public Dialog_ExpertModeSettings()
        {
            // Standard window properties configuration
            forcePause = true; // Pauses the game while the dialog is open
            doCloseX = true; // Adds a close 'X' button to the window corner
            closeOnClickedOutside = true; // Closes the window if the user clicks outside its bounds
            absorbInputAroundWindow = true; // Prevents clicks outside the window from affecting the game world
            draggable = true; // Allows the user to drag the window

            // Attempt to get the rule manager component from the current game.
            // Null check happens in DoWindowContents before ruleManager is used.
            ruleManager = Current.Game?.GetComponent<ExpertModeRuleManager>();

            // Populate and sort the cache with relevant work types. This is done on every dialog creation
            // to ensure dynamically added/removed work types from other mods are reflected.
// Always refresh the list of relevant WorkTypeDefs when the dialog is opened
            // Filter matches base AWA (shows all work types with WorkTags != None, regardless of relevantSkills)
            relevantWorkTypesCache = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd != null && wtd.workTags != WorkTags.None) // <-- Filtro modificado (SIN chequeo de skills)
                .OrderBy(wtd => wtd.labelShort)
                .ToList();

            // Set the window title using a translation key for localization.
            this.optionalTitle = "AWA_ExpertMode_RuleWindowTitle".Translate();
        }

        /// <summary>
        /// Called by the RimWorld UI system immediately before the window is displayed, after the constructor and potentially after recreation.
        /// Ensures that a work type is selected by default if the list (<see cref="relevantWorkTypesCache"/>) is not empty.
        /// It selects the first available work type or resets the selection if the previously selected one is no longer valid (e.g., removed by a mod change).
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen(); // Call base implementation first.
            // If no work type is currently selected and the list of work types isn't empty, select the first one.
            if (selectedWorkDef == null && relevantWorkTypesCache.Any())
            {
                selectedWorkDef = relevantWorkTypesCache.First();
            }
            // If a work type *is* selected, but it's somehow no longer present in the current cache
            // (e.g., the mod providing it was removed since the dialog was last opened),
            // attempt to select the first available one again, or set selection to null if the cache is now empty.
            else if (selectedWorkDef != null && !relevantWorkTypesCache.Contains(selectedWorkDef))
            {
                selectedWorkDef = relevantWorkTypesCache.FirstOrDefault(); // FirstOrDefault handles empty list safely (returns null).
            }
        }


        /// <summary>
        /// Draws the main interactive content of the dialog window within the specified rectangle area.
        /// This method orchestrates the drawing process:
        /// 1. Checks if the necessary <see cref="ruleManager"/> is available, showing an error if not.
        /// 2. Defines the layout rectangles for the left (list) and right (editor) panels.
        /// 3. Calls <see cref="DrawWorkTypeDefList"/> to render the work type selection list.
        /// 4. Calls <see cref="DrawRuleEditor"/> to render the rule editing interface if a work type is selected, or shows a prompt otherwise.
        /// 5. Draws the standard "Close" button at the bottom of the window.
        /// </summary>
        /// <param name="inRect">The rectangle area, provided by the UI system, available for drawing the window content.</param>
        public override void DoWindowContents(Rect inRect)
        {
            // --- Pre-checks ---
            // Display an error message and halt drawing if the rule manager isn't available (e.g., no game loaded).
            if (ruleManager == null)
            {
                Rect errorRect = new Rect(inRect.x + 10f, inRect.y + 10f, inRect.width - 20f, 30f);
                Widgets.Label(errorRect, "AWA_ExpertMode_LoadSaveFirst".Translate()); // Use translation key for message.
                return; // Stop further drawing.
            }

            // --- Layout Definition ---
            // Calculate layout geometry based on the provided inRect.
            float footerHeight = CloseButSize.y + 10f; // Reserve space for the close button area.
            float contentHeight = inRect.height - footerHeight; // Height available for the main panels.
            Rect leftRect = new Rect(inRect.x, inRect.y, inRect.width * 0.3f, contentHeight); // Left panel takes 30% of the width.
            Rect rightRect = new Rect(leftRect.xMax + 10f, inRect.y, inRect.width - leftRect.width - 20f, contentHeight); // Right panel takes remaining width with padding.

            // --- Draw Panels ---
            DrawWorkTypeDefList(leftRect); // Draw the list of work types on the left.

            // Draw the rule editor on the right only if a work type is selected.
            if (selectedWorkDef != null)
            {
                DrawRuleEditor(rightRect, selectedWorkDef);
            }
            else // If no work type is selected, display a prompt message in the right panel area.
            {
                Widgets.Label(rightRect.ContractedBy(10f), "AWA_ExpertMode_SelectWorkTypePrompt".Translate()); // Use translation key.
            }

            // --- Footer Elements ---
            // Draw the standard "Close" button at the bottom right corner.
            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x - 10f , inRect.height - CloseButSize.y - 5f , CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate())) // Use standard "Close" translation.
            {
                this.Close(); // Call the Window.Close method when the button is clicked.
            }
        }

        /// <summary>
        /// Draws the scrollable list of selectable work types (<see cref="WorkTypeDef"/>) in the left panel of the dialog.
        /// Items are drawn as buttons. Clicking a button selects the corresponding work type for editing in the right panel
        /// and updates the <see cref="selectedWorkDef"/> field. Handles highlighting the selected item.
        /// </summary>
        /// <param name="rect">The rectangle area allocated by <see cref="DoWindowContents"/> for the left panel.</param>
        private void DrawWorkTypeDefList(Rect rect)
        {
            // Draw the background frame for this section.
            Widgets.DrawMenuSection(rect);

            // Check if the cache (populated in constructor) is empty.
            if (!relevantWorkTypesCache.Any())
            {
                // Display a message if no work types met the filter criteria.
                Widgets.Label(rect.ContractedBy(10f), "No relevant work types found.");
                return;
            }

            // --- Scroll View Setup ---
            float entryHeight = 30f; // Define the height of each item (button) in the list.
            float viewHeight = relevantWorkTypesCache.Count * entryHeight; // Calculate the total vertical space needed for all items.
            // Define the inner rectangle for the scroll view content. Width adjusted for the vertical scrollbar.
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);

            // --- Scroll View Drawing ---
            Widgets.BeginScrollView(rect, ref scrollPositionLeft, viewRect, true); // `true` enables the vertical scrollbar.

            Listing_Standard listing = new Listing_Standard(); // Use Listing_Standard for easy vertical layout.
            listing.Begin(viewRect); // Start the listing within the scroll view's inner rect.

            // Iterate through the cached work types.
            foreach (WorkTypeDef workDef in relevantWorkTypesCache)
            {
                if (workDef == null) continue; // Safety check for null entries in the DefDatabase list.

                bool isSelected = selectedWorkDef == workDef; // Determine if this is the currently selected item.
                Rect entryRect = listing.GetRect(entryHeight); // Get a rectangle for this list item from the Listing_Standard.

                // Draw selection highlight if this item is selected. Draw it *before* the button text for visibility.
                if (isSelected) {
                    Widgets.DrawHighlightSelected(entryRect);
                }

                // Draw the item as a clickable button. Using drawBackground:false ensures the highlight underneath is visible.
                if (Widgets.ButtonText(entryRect, workDef.labelShort.CapitalizeFirst(), drawBackground: false, doMouseoverSound: true, active: true))
                {
                    // If the button is clicked AND it wasn't already the selected item:
                    if (!isSelected)
                    {
                        selectedWorkDef = workDef; // Update the selection.
                        scrollPositionRight = Vector2.zero; // Reset the scroll position of the rule editor panel.
                        SoundDefOf.Click?.PlayOneShotOnCamera(); // Play a standard UI click sound if available.
                    }
                }

                // Add a tooltip to the button's rectangle area.
                TooltipHandler.TipRegion(entryRect, $"Select {workDef.labelShort} to edit rules."); // Tooltip text.
            }

            listing.End(); // Finalize the Listing_Standard.
            Widgets.EndScrollView(); // End the scroll view.
        }


        /// <summary>
        /// Draws the rule editor interface in the right panel for the currently selected <see cref="WorkTypeDef"/>.
        /// This interface includes:
        /// - A header displaying the work type name and an "Add Rule" button.
        /// - A scrollable list of existing <see cref="SkillPriorityRule"/>s for the selected work type.
        /// - For each rule, sliders to adjust minimum skill, maximum skill, and target priority (1-4).
        /// - A delete button for each rule.
        /// Handles rule creation, deletion, modification, and saving changes to the <see cref="ruleManager"/>.
        /// </summary>
        /// <param name="rect">The rectangle area allocated by <see cref="DoWindowContents"/> for the right panel.</param>
        /// <param name="workDef">The currently selected <see cref="WorkTypeDef"/> whose rules are to be displayed and edited. This parameter is guaranteed to be non-null by the caller (<see cref="DoWindowContents"/>).</param>
        private void DrawRuleEditor(Rect rect, WorkTypeDef workDef)
        {
            // Secondary safety check, though DoWindowContents should already ensure ruleManager is not null.
            if (ruleManager == null) {
                Widgets.Label(rect.ContractedBy(10f), "Error: Rule manager reference lost.");
                return;
            }

            // Draw the background frame for the editor section.
            Widgets.DrawMenuSection(rect);

            // --- Rule List Management ---
            // Ensure a list entry exists in the dictionary for this work type. If not, create an empty list.
            if (!ruleManager.workTypeRules.ContainsKey(workDef))
            {
                ruleManager.workTypeRules[workDef] = new List<SkillPriorityRule>();
            }
            // Get a direct reference to the list of rules for this work type.
            List<SkillPriorityRule> rules = ruleManager.workTypeRules[workDef];

            // --- Header ---
            // Define geometry and draw the header area containing the work type label and Add button.
            float headerHeight = 35f;
            Rect headerRect = new Rect(rect.x + 10f, rect.y, rect.width - 20f, headerHeight); // Add some padding.
            // Temporarily adjust text settings for the header label.
            Text.Anchor = TextAnchor.MiddleLeft; // Align text to the middle left.
            Text.Font = GameFont.Medium; // Use medium font size.
            // Draw the work type label (e.g., "Mining"). Take only part of the header width to leave space for the button.
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 110f, headerHeight), workDef.label.CapitalizeFirst());
            // Reset text settings for subsequent UI elements.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Draw the "Add Rule" button on the right side of the header.
            Rect addButtonRect = new Rect(headerRect.xMax - 100f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f); // Center button vertically.
            if (Widgets.ButtonText(addButtonRect, "AWA_ExpertMode_AddRule".Translate())) // Use translation key.
            {
                // When clicked, add a new rule with default values to the list.
                rules.Add(new SkillPriorityRule(0, 5, 4)); // Example default: Skill 0-5 gets Priority 4.
                // Keep the list sorted visually by the minimum skill level of the rules.
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
            // TODO: Add tooltip for the Add Rule button.

            // --- Scrollable Area for Rules ---
            // Define the area below the header for the scrollable list of rules.
            Rect scrollOuterRect = new Rect(rect.x, rect.y + headerHeight, rect.width, rect.height - headerHeight);
            float ruleRowHeight = 65f; // Define the fixed height for each row displaying a rule editor.
            float rowSpacing = 4f; // Define vertical spacing between rule rows.
            // Calculate the total height required by all rule rows within the scroll view. Ensure it's at least as tall as the visible area.
            float viewHeight = Mathf.Max(rules.Count * (ruleRowHeight + rowSpacing), scrollOuterRect.height);
            // Define the rectangle for the content within the scroll view. Adjust width for the scrollbar.
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);

            // --- Draw Scroll View ---
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPositionRight, viewRect, true); // Enable vertical scrollbar.
            Listing_Standard listing = new Listing_Standard(); // Use Listing_Standard for vertical layout within the scroll view.
            listing.Begin(viewRect);

            SkillPriorityRule ruleToDelete = null; // Variable to store a rule marked for deletion; processed after the loop.

            // Check if there are any rules to display.
            if (!rules.Any())
            {
                // If no rules, display a message using the Listing_Standard for correct positioning.
                listing.Label("AWA_ExpertMode_NoRulesDefined".Translate()); // Use translation key.
            }
            else // If rules exist, iterate through them and draw the editor for each.
            {
                for(int i=0; i < rules.Count; i++)
                {
                    SkillPriorityRule rule = rules[i];
                    if (rule == null) continue; // Safety check for null rules in the list.

                    // Get a rectangle for the current rule's editor row from the Listing_Standard.
                    Rect rowRect = listing.GetRect(ruleRowHeight);

                    // --- Layout Calculation for Controls within the Row ---
                    // Calculate widths for different control areas (skill sliders, priority slider, delete button)
                    // based on proportions and fixed sizes, accounting for padding.
                    float totalDrawableWidth = rowRect.width;
                    float deleteButtonWidth = 24f; // Standard icon button size.
                    float deleteButtonPadding = 5f; // Padding around delete button.
                    float priorityWidthRatio = 0.25f; // Priority slider gets 25% of the space remaining after delete button.
                    float availableWidthForSliders = totalDrawableWidth - deleteButtonWidth - deleteButtonPadding * 2;
                    float priorityAreaWidth = availableWidthForSliders * priorityWidthRatio;
                    float skillAreaWidth = availableWidthForSliders - priorityAreaWidth; // Skill sliders get the rest.
                    float sliderHeight = 24f; // Define standard height for horizontal sliders.
                    float verticalSpacing = 5f; // Vertical space between the Min Skill and Max Skill sliders.
                    float internalPadding = 5f; // Padding within the calculated areas.

                    // Define the final Rects for each control area within the rowRect.
                    Rect skillAreaRect = new Rect(rowRect.x + internalPadding, rowRect.y, skillAreaWidth - internalPadding*2, rowRect.height);
                    Rect priorityAreaRect = new Rect(skillAreaRect.xMax + internalPadding, rowRect.y, priorityAreaWidth - internalPadding*2, rowRect.height);
                    // Vertically center the delete button within the row height.
                    Rect deleteRect = new Rect(priorityAreaRect.xMax + internalPadding + deleteButtonPadding, rowRect.y + (rowRect.height - deleteButtonWidth)/2f, deleteButtonWidth, deleteButtonWidth);

                    // --- Draw Skill Sliders (Min/Max) ---
                    // Calculate starting Y position to vertically center the two skill sliders within their area.
                    float skillSliderYStart = skillAreaRect.y + (skillAreaRect.height - (sliderHeight * 2 + verticalSpacing)) / 2f;
                    // Min Skill Slider
                    Rect minSliderRect = new Rect(skillAreaRect.x, skillSliderYStart, skillAreaRect.width, sliderHeight);
                    string minLabel = $"Min Skill: {rule.MinSkill}"; // Dynamic label showing current value.
                    // Draw the slider, updating rule.MinSkill directly. Clamp range 0-20, step 1.
                    rule.MinSkill = (int)Widgets.HorizontalSlider(minSliderRect, rule.MinSkill, 0f, 20f, true, minLabel, null, null, 1f);
                    // Max Skill Slider
                    Rect maxSliderRect = new Rect(skillAreaRect.x, minSliderRect.yMax + verticalSpacing, skillAreaRect.width, sliderHeight);
                    string maxLabel = $"Max Skill: {rule.MaxSkill}"; // Dynamic label.
                    // Draw the slider, updating rule.MaxSkill directly. Clamp range 0-20, step 1.
                    rule.MaxSkill = (int)Widgets.HorizontalSlider(maxSliderRect, rule.MaxSkill, 0f, 20f, true, maxLabel, null, null, 1f);
                    // Post-slider validation: Ensure MinSkill is never greater than MaxSkill.
                    if (rule.MinSkill > rule.MaxSkill) rule.MinSkill = rule.MaxSkill;
                    if (rule.MaxSkill < rule.MinSkill) rule.MaxSkill = rule.MinSkill;

                    // --- Draw Priority Slider ---
                    // Vertically center the priority slider within its area.
                    Rect prioritySliderRect = new Rect(priorityAreaRect.x, priorityAreaRect.y + (priorityAreaRect.height - sliderHeight) / 2f, priorityAreaRect.width, sliderHeight);
                    string priorityLabel = "P:" + rule.Priority; // Short label for priority (e.g., "P:3"). RimWorld uses 1-4.
                    // Draw the slider, updating rule.Priority directly. Clamp range 1-4, step 1.
                    rule.Priority = (int)Widgets.HorizontalSlider(prioritySliderRect, rule.Priority, 1f, 4f, true, priorityLabel, null, null, 1f);
                    TooltipHandler.TipRegion(prioritySliderRect, "AWA_ExpertMode_Priority".Translate()); // Add descriptive tooltip.

                    // --- Draw Delete Button ---
                    // Draw a standard delete icon button. Use subtle mouseover color for feedback.
                    if (Widgets.ButtonImage(deleteRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor)) {
                        ruleToDelete = rule; // If clicked, mark this rule to be deleted after the loop finishes.
                    }
                    TooltipHandler.TipRegion(deleteRect, "AWA_ExpertMode_DeleteRule".Translate()); // Add tooltip.

                    // --- End Control Drawing for Row ---
                    listing.Gap(rowSpacing); // Add defined vertical space before the next rule row begins.
                }
            }

            // --- Post-Loop Processing ---
            // If a rule was marked for deletion during the iteration, remove it from the list now.
            if (ruleToDelete != null) {
                rules.Remove(ruleToDelete);
            }

            listing.End(); // Finalize the Listing_Standard used within the scroll view.
            Widgets.EndScrollView(); // End the scroll view area.

            // If a rule was deleted, resort the list to maintain visual consistency (ordered by MinSkill).
            if (ruleToDelete != null) {
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
        }
    }
}