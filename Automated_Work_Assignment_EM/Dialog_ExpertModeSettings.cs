using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment_EM
{
    /// <summary>
    /// Dialog window for configuring Expert Mode rules.
    /// </summary>
    public class Dialog_ExpertModeSettings : Window
    {
// Reference to the rule manager for the current game
        private ExpertModeRuleManager ruleManager;
// UI state variables (scroll positions, selection)
        private Vector2 scrollPositionLeft = Vector2.zero; // Scroll for left list
        private Vector2 scrollPositionRight = Vector2.zero; // Scroll for right editor
        private WorkTypeDef selectedWorkDef = null;
// Cache of relevant WorkTypeDefs to avoid recalculating
        private static List<WorkTypeDef> relevantWorkTypesCache = null;
        /// <summary>
        /// Gets the initial size of the window.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(800f, 600f); // Initial window size
        /// <summary>
        /// Constructor for the settings dialog. Sets up window properties and caches data.
        /// </summary>
        public Dialog_ExpertModeSettings()
        {
// Standard window properties
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
// Get the rule manager for the current game
            ruleManager = Current.Game?.GetComponent<ExpertModeRuleManager>();
// Pre-cache the list of WorkTypeDefs if not already done
            if (relevantWorkTypesCache == null)
            {
                relevantWorkTypesCache = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(wtd => wtd != null && wtd.workTags != WorkTags.None && wtd.relevantSkills?.Any() == true) // Ensure they have a relevant skill
                    .OrderBy(wtd => wtd.labelShort)
                    .ToList();
            }
// Set the window title (using translation key)
            this.optionalTitle = "AWA_ExpertMode_RuleWindowTitle".Translate(); // TODO: Add translation
        }
        /// <summary>
        /// Called just before the window is opened. Used here to pre-select the first item.
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();
// Ensure the first WorkTypeDef is selected when opening, if available
            if (selectedWorkDef == null && relevantWorkTypesCache != null && relevantWorkTypesCache.Any())
            {
                selectedWorkDef = relevantWorkTypesCache.First();
            }
        }
        /// <summary>
        /// Draws the main content of the window.
        /// </summary>
        public override void DoWindowContents(Rect inRect)
        {
            if (ruleManager == null)
            {
                Widgets.Label(inRect, "Error: Rule manager not found. Is a game loaded?");
                return;
            }
// --- Window Layout (Left list, Right details) ---
            float contentHeight = inRect.height - CloseButSize.y;
            Rect leftRect = new Rect(inRect.x, inRect.y, inRect.width * 0.3f, contentHeight);
            Rect rightRect = new Rect(leftRect.xMax + 10f, inRect.y, inRect.width - leftRect.width - 10f, contentHeight);
// --- Left Panel: List of WorkTypeDefs ---
            DrawWorkTypeDefList(leftRect);
// --- Right Panel: Rule Editor for the selected WorkTypeDef ---
            if (selectedWorkDef != null)
            {
                DrawRuleEditor(rightRect, selectedWorkDef);
            }
            else
            {
                Widgets.Label(rightRect, "AWA_ExpertMode_SelectWorkTypePrompt".Translate()); // TODO: Add translation
            }
        }
        /// <summary>
        /// Draws the scrollable list of WorkTypeDefs in the left panel.
        /// </summary>
        private void DrawWorkTypeDefList(Rect rect)
        {
            Widgets.DrawMenuSection(rect); // Draw background/border
            if (relevantWorkTypesCache == null || !relevantWorkTypesCache.Any())
            {
                Widgets.Label(rect.ContractedBy(10f), "No relevant work types found.");
                return;
            }
            float entryHeight = 30f; // Height per list entry
            float viewHeight = relevantWorkTypesCache.Count * entryHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight); // -16f for scrollbar
            Widgets.BeginScrollView(rect, ref scrollPositionLeft, viewRect, true);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            foreach (WorkTypeDef workDef in relevantWorkTypesCache)
            {
                Color originalColor = GUI.color;
                if (selectedWorkDef == workDef)
                {
                    GUI.color = Color.yellow; // Highlight color
                }
                if (listing.ButtonText(workDef.labelShort.CapitalizeFirst(), $"Select {workDef.labelShort} to edit rules."))
                {
                    if (selectedWorkDef != workDef)
                    {
                        selectedWorkDef = workDef;
                        scrollPositionRight = Vector2.zero;
                    }
                }
                GUI.color = originalColor;
            }
            listing.End();
            Widgets.EndScrollView();
        }
        /// <summary>
        /// Draws the rule editor for the selected WorkTypeDef in the right panel using sliders.
        /// </summary>
        private void DrawRuleEditor(Rect rect, WorkTypeDef workDef)
        {
            Widgets.DrawMenuSection(rect); // Draw background/border
            if (!ruleManager.workTypeRules.ContainsKey(workDef))
            {
                ruleManager.workTypeRules[workDef] = new List<SkillPriorityRule>();
            }
            List<SkillPriorityRule> rules = ruleManager.workTypeRules[workDef];
// --- Scrollable Area for Rules ---
            float headerHeight = 35f;
            Rect scrollOuterRect = new Rect(rect.x, rect.y + headerHeight, rect.width, rect.height - headerHeight);
// Row height adjusted for two horizontal sliders + priority slider
            float ruleRowHeight = 65f;
            float viewHeight = Mathf.Max(rules.Count * ruleRowHeight, scrollOuterRect.height);
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);
// --- Header ---
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(headerRect.x + 5f, headerRect.y, headerRect.width - 110f, headerRect.height), workDef.label.CapitalizeFirst());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect addButtonRect = new Rect(headerRect.xMax - 100f - 5f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(addButtonRect, "AWA_ExpertMode_AddRule".Translate())) // TODO: Add translation
            {
                rules.Add(new SkillPriorityRule(0, 5, 4));
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
// --- ScrollView for Rules ---
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPositionRight, viewRect, true);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.ColumnWidth = viewRect.width; // Use full width
            SkillPriorityRule ruleToDelete = null;
            if (!rules.Any())
            {
                Widgets.Label(viewRect.ContractedBy(10f), "AWA_ExpertMode_NoRulesDefined".Translate()); // TODO: Add translation
            }
            else
            {
                for(int i=0; i < rules.Count; i++)
                {
                    SkillPriorityRule rule = rules[i];
                    Rect rowRect = listing.GetRect(ruleRowHeight);
// Divide row horizontally
                    float deleteButtonWidth = 24f;
                    float priorityWidth = rowRect.width * 0.25f; // Width for priority slider section
                    float skillWidth = rowRect.width - priorityWidth - deleteButtonWidth - 20f; // Remaining width for skill sliders (-20f for padding)
                    float sliderHeight = 24f; // Height of individual sliders
                    float verticalSpacing = 5f; // Vertical space between min/max sliders
                    Rect skillAreaRect = new Rect(rowRect.x, rowRect.y, skillWidth, rowRect.height);
                    Rect priorityAreaRect = new Rect(skillAreaRect.xMax + 10f, rowRect.y, priorityWidth, rowRect.height);
                    Rect deleteRect = new Rect(priorityAreaRect.xMax + 10f, rowRect.y + (rowRect.height - deleteButtonWidth)/2f, deleteButtonWidth, deleteButtonWidth);
// --- Skill Sliders (Min and Max) ---
// Min Skill
                    Rect minSliderRect = new Rect(skillAreaRect.x, skillAreaRect.y + (skillAreaRect.height / 2f - sliderHeight - verticalSpacing / 2f), skillAreaRect.width, sliderHeight);
                    string minLabel = $"Min Skill: {rule.MinSkill}";
                    rule.MinSkill = (int)Widgets.HorizontalSlider(minSliderRect, rule.MinSkill, 0f, 20f, true, minLabel, null, null, 1f);
// Max Skill
                    Rect maxSliderRect = new Rect(skillAreaRect.x, skillAreaRect.y + (skillAreaRect.height / 2f + verticalSpacing / 2f), skillAreaRect.width, sliderHeight);
                    string maxLabel = $"Max Skill: {rule.MaxSkill}";
                    rule.MaxSkill = (int)Widgets.HorizontalSlider(maxSliderRect, rule.MaxSkill, 0f, 20f, true, maxLabel, null, null, 1f);
// Validation after sliders
                    if (rule.MinSkill > rule.MaxSkill) rule.MinSkill = rule.MaxSkill;
                    if (rule.MaxSkill < rule.MinSkill) rule.MaxSkill = rule.MinSkill;
                    // --- Priority Slider ---
                    Rect prioritySliderRect = new Rect(priorityAreaRect.x, priorityAreaRect.y + (priorityAreaRect.height - sliderHeight) / 2f, priorityAreaRect.width, sliderHeight);
                    string priorityLabel = "P:" + rule.Priority; // Short label
                    rule.Priority = (int)Widgets.HorizontalSlider(prioritySliderRect, rule.Priority, 1f, 4f, true, priorityLabel, null, null, 1f);
                    TooltipHandler.TipRegion(prioritySliderRect, "AWA_ExpertMode_Priority".Translate()); // Add tooltip separately
                    // --- Delete Button ---
                    if (Widgets.ButtonImage(deleteRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor))
                    {
                        ruleToDelete = rule;
                    }
                    TooltipHandler.TipRegion(deleteRect, "AWA_ExpertMode_DeleteRule".Translate()); // TODO: Add translation
                    listing.Gap(listing.verticalSpacing); // Use Listing_Standard's gap
                }
            }
            if (ruleToDelete != null)
            {
                rules.Remove(ruleToDelete);
            }
            listing.End();
            Widgets.EndScrollView();
            // Re-sort rules after potential edits/deletions
            rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
        }
    }
}