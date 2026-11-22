using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed partial class DeckAssemblyMatrixDialog
    {
        private const string FactionIdleStatusText =
            "Factions are strictly limited to those playable at Sites/Havens in the drafted Site deck.\n" +
            "Auto-drafted prerequisite cards (e.g. Paths of the Dead) are added alongside their faction if their own site requirements are met.";

        private const string AllyIdleStatusText =
            "Allies are strictly limited to those playable at Sites/Havens in the drafted Site deck.\n" +
            "Prioritizes allies playable in the drafted region group before outside Havens.";

        private const string ItemIdleStatusText =
            "Minor, Major, Greater, and non-Ring/non-Palantír Special Items are drafted if playable at drafted Sites/Havens.\n" +
            "Pool Minor Items (0-2) are drafted first; all items obey Role Focus, Region/Site Requirements, Specific, Prerequisite, and Hero Target Species.";

        private const string RingsIdleStatusText =
            "Gold Rings require at least one GoldRing-playable site in the drafted Site deck.\n" +
            "Allow Ring Test and Aid Ring Test counts are totals (counting Gandalf once if already in Res/Sideboard).";

        private const string PalantirIdleStatusText =
            "Palantíri require a GreaterItem- or Palantiri-playable site (or their printed Site Requirement) in the drafted Site deck.\n" +
            "Allow Palantír Use counts are totals (including Saruman counted once if already drafted in Resources/Sideboard).";

        private const string QuestIdleStatusText =
            "Drafts Role = Quest cards (events and Quest Special Items) playable at drafted Sites/Havens.\n" +
            "Drafts missing Prerequisite cards (e.g. Men of Lamedon, Narsil && Reforging) into the same section if capacity permits.";

        private const string PureQuestRoleLabel = "Quest (Only)";

#pragma warning disable SYSLIB1045 // Use 'GeneratedRegexAttribute'
        private static readonly Regex GoldRingOutcomeRegex = new(
            @"(?:the\s+)?(one\s+ring|dwarven\s+ring|magic\s+ring|spirit\s+ring)\s*\(([^)]+)\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex GoldRingRangeRegex = new(
            @"(\d+)\s*[-–]\s*(\d+)",
            RegexOptions.Compiled);

        private static readonly Regex GoldRingNumberRegex = new(
            @"\d+",
            RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

        private Panel BuildFactionsPage()
        {
            var tabFactions = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpFactions = new GroupBox
            {
                Text = "Faction Drafting Counts && Priority Rules",
                Location = new Point(16, 16),
                Size = new Size(810, 186)
            };

            var lblResFactions = new Label
            {
                Text = "Factions in Play Deck (Resources):",
                Location = new Point(16, 30),
                Size = new Size(210, 20)
            };
            _numResourceFactions = new NumericUpDown
            {
                Location = new Point(232, 27),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 15,
                Value = 1
            };
            string resFactionsTip =
                "Number of factions to draft into the Play Deck (Resources).\n" +
                "Default is 1 (minimum needed for Council scoring).";
            _toolTip.SetToolTip(lblResFactions, resFactionsTip);
            _toolTip.SetToolTip(_numResourceFactions, resFactionsTip);

            var lblResFactionsHint = new Label
            {
                Text = "factions in Resources (1 minimum for Council scoring; highest-priority matches placed here first).",
                Location = new Point(304, 29),
                Size = new Size(495, 32),
                ForeColor = Color.DimGray
            };

            var lblSideFactions = new Label
            {
                Text = "Factions in Sideboard:",
                Location = new Point(16, 66),
                Size = new Size(210, 20)
            };
            _numSideboardFactions = new NumericUpDown
            {
                Location = new Point(232, 63),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 10,
                Value = 0
            };
            string sideFactionsTip =
                "Number of backup factions to draft into the Sideboard.\n" +
                "Counts toward Sideboard Size on General Settings.";
            _toolTip.SetToolTip(lblSideFactions, sideFactionsTip);
            _toolTip.SetToolTip(_numSideboardFactions, sideFactionsTip);

            var lblSideFactionsHint = new Label
            {
                Text = "backup factions in Sideboard (unique titles are never duplicated across Resources & Sideboard).",
                Location = new Point(304, 65),
                Size = new Size(495, 32),
                ForeColor = Color.DimGray,
                UseMnemonic = false
            };

            _numResourceFactions.ValueChanged += (s, e) => ValidateWorkbenchReadiness();
            _numSideboardFactions.ValueChanged += (s, e) => ValidateWorkbenchReadiness();

            var lblFactionRulesNote = new Label
            {
                Location = new Point(16, 102),
                Size = new Size(778, 68),
                ForeColor = Color.DimGray,
                UseMnemonic = false,
                Text =
                    "Priority Order (among factions playable at drafted Sites/Havens with Specific & Prerequisite requirements met):\n" +
                    "  1. Faction Bonus / Specific Character (Pool -> Resources -> Sideboard)   2. Character Race Match (Pool -> Resources -> Sideboard)\n" +
                    "  3. Local Region Group Sites before Outside Havens                       4. Remaining Eligible Factions"
            };

            grpFactions.Controls.AddRange([
                lblResFactions, _numResourceFactions, lblResFactionsHint,
                lblSideFactions, _numSideboardFactions, lblSideFactionsHint,
                lblFactionRulesNote
            ]);

            _lblFactionStatus = new Label
            {
                Location = new Point(16, 216),
                Size = new Size(805, 160),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = FactionIdleStatusText
            };

            tabFactions.Controls.AddRange([grpFactions, _lblFactionStatus]);
            return tabFactions;
        }

        private Panel BuildAlliesPage()
        {
            var tabAllies = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpAllies = new GroupBox
            {
                Text = "Ally Drafting Counts && Priority Rules",
                Location = new Point(16, 16),
                Size = new Size(810, 172)
            };

            var lblResAllies = new Label
            {
                Text = "Allies in Play Deck (Resources):",
                Location = new Point(16, 30),
                Size = new Size(210, 20)
            };
            _numResourceAllies = new NumericUpDown
            {
                Location = new Point(232, 27),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 15,
                Value = 1
            };
            string resAlliesTip =
                "Number of allies to draft into the Play Deck (Resources).\n" +
                "Default is 1 (minimum needed for Council scoring).";
            _toolTip.SetToolTip(lblResAllies, resAlliesTip);
            _toolTip.SetToolTip(_numResourceAllies, resAlliesTip);

            var lblResAlliesHint = new Label
            {
                Text = "allies in Resources (1 minimum for Council scoring; local region group matches prioritized first).",
                Location = new Point(304, 29),
                Size = new Size(495, 32),
                ForeColor = Color.DimGray
            };

            var lblSideAllies = new Label
            {
                Text = "Allies in Sideboard:",
                Location = new Point(16, 66),
                Size = new Size(210, 20)
            };
            _numSideboardAllies = new NumericUpDown
            {
                Location = new Point(232, 63),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 10,
                Value = 0
            };
            string sideAlliesTip =
                "Number of backup allies to draft into the Sideboard.\n" +
                "Counts toward Sideboard Size on General Settings.";
            _toolTip.SetToolTip(lblSideAllies, sideAlliesTip);
            _toolTip.SetToolTip(_numSideboardAllies, sideAlliesTip);

            var lblSideAlliesHint = new Label
            {
                Text = "backup allies in Sideboard (unique titles are never duplicated across Resources & Sideboard).",
                Location = new Point(304, 65),
                Size = new Size(495, 32),
                ForeColor = Color.DimGray,
                UseMnemonic = false
            };

            _numResourceAllies.ValueChanged += (s, e) => ValidateWorkbenchReadiness();
            _numSideboardAllies.ValueChanged += (s, e) => ValidateWorkbenchReadiness();

            var lblAllyRulesNote = new Label
            {
                Location = new Point(16, 102),
                Size = new Size(778, 54),
                ForeColor = Color.DimGray,
                UseMnemonic = false,
                Text =
                    "Priority Order (among allies playable at drafted Sites/Havens with Specific & Prerequisite requirements met):\n" +
                    "  1. Local Region Group Sites (and local Havens)      2. Outside Havens"
            };

            grpAllies.Controls.AddRange([
                lblResAllies, _numResourceAllies, lblResAlliesHint,
                lblSideAllies, _numSideboardAllies, lblSideAlliesHint,
                lblAllyRulesNote
            ]);

            _lblAllyStatus = new Label
            {
                Location = new Point(16, 202),
                Size = new Size(805, 160),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = AllyIdleStatusText
            };

            tabAllies.Controls.AddRange([grpAllies, _lblAllyStatus]);
            return tabAllies;
        }

        private Panel BuildRingsPage()
        {
            var tabRings = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpRings = new GroupBox
            {
                Text = "Ring Package Counts (Resources && Sideboard)",
                Location = new Point(16, 16),
                Size = new Size(810, 298)
            };

            var lblRingHdrCat = new Label { Text = "Ring Category", Location = new Point(16, 22), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblRingHdrRes = new Label { Text = "Resources", Location = new Point(185, 22), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblRingHdrSide = new Label { Text = "Sideboard", Location = new Point(285, 22), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblRingHdrNote = new Label { Text = "Selection & Rules Behavior", Location = new Point(385, 22), Size = new Size(410, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), UseMnemonic = false };

            grpRings.Controls.AddRange([lblRingHdrCat, lblRingHdrRes, lblRingHdrSide, lblRingHdrNote]);

            (NumericUpDown ResSpinner, NumericUpDown SideSpinner) AddRingRow(
                int rowIndex,
                string labelText,
                int resMax,
                int resDefault,
                int sideMax,
                int sideDefault,
                string noteText)
            {
                int y = 46 + (rowIndex * 30);
                var lbl = new Label { Text = labelText, Location = new Point(16, y + 3), Size = new Size(165, 20) };
                var nudRes = CreateItemSpinner(185, y, 0, resMax, resDefault);
                var nudSide = CreateItemSpinner(285, y, 0, sideMax, sideDefault);
                var lblNote = new Label
                {
                    Text = noteText,
                    Location = new Point(385, y + 3),
                    Size = new Size(415, 20),
                    ForeColor = Color.DimGray,
                    UseMnemonic = false
                };

                grpRings.Controls.AddRange([lbl, nudRes, nudSide, lblNote]);
                return (nudRes, nudSide);
            }

            (_numResGoldRings, _numSideGoldRings) = AddRingRow(0, "Gold Rings:", 9, 0, 9, 0, "Auto-selected from Gold Ring roll tables to match requested Special Rings.");
            (_numResAllowRingTest, _numSideAllowRingTest) = AddRingRow(1, "Allow Ring Test:", 9, 0, 6, 0, "Total desired (counts Gandalf once if already in Res/Sideboard).");
            (_numResAidRingTest, _numSideAidRingTest) = AddRingRow(2, "Aid Ring Test:", 6, 0, 6, 0, "Total desired (drafts Scroll of Isildur before Items runs).");
            (_numResOneRing, _numSideOneRing) = AddRingRow(3, "The One Ring:", 1, 0, 1, 0, "Unique (combined Resources + Sideboard <= 1).");
            (_numResDwarvenRings, _numSideDwarvenRings) = AddRingRow(4, "Dwarven Rings:", 7, 0, 7, 0, "Unique; playable by any character (extra perks for Dwarves).");
            (_numResMagicRings, _numSideMagicRings) = AddRingRow(5, "Magic Rings:", 10, 0, 10, 0, "Unique; playable with a Gold Ring when test indicates Magic Ring.");
            (_numResLesserRings, _numSideLesserRings) = AddRingRow(6, "Lesser Rings:", 6, 0, 6, 0, "Non-unique (up to 3 copies); playable with any Gold Ring test result.");
            (_numResWizardsRing, _numSideWizardsRing) = AddRingRow(7, "Wizard's Ring:", 3, 0, 3, 0, "Wizard only, playable at a Haven (requires a Wizard in the deck).");

            var grpRingRoles = new GroupBox
            {
                Text = "Special Ring Role Focus (Uncheck to exclude)",
                Location = new Point(526, 326),
                Size = new Size(300, 224)
            };

            _clbRingRoles = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(276, 156),
                CheckOnClick = true
            };
            _clbRingRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbRingRoles, "Filters Special Rings (Dwarven, Magic, Lesser, Wizard's Ring) by Role tag.");

            var btnAllRingRoles = new Button { Text = "All", Location = new Point(12, 188), Size = new Size(65, 24) };
            btnAllRingRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbRingRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearRingRoles = new Button { Text = "Clear", Location = new Point(83, 188), Size = new Size(65, 24) };
            btnClearRingRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbRingRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpRingRoles.Controls.AddRange([_clbRingRoles, btnAllRingRoles, btnClearRingRoles]);

            _lblRingsStatus = new Label
            {
                Location = new Point(16, 326),
                Size = new Size(495, 260),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = RingsIdleStatusText
            };

            tabRings.Controls.AddRange([grpRings, grpRingRoles, _lblRingsStatus]);
            return tabRings;
        }

        private Panel BuildPalantiriPage()
        {
            var tabPalantiri = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpPalantiri = new GroupBox
            {
                Text = "Palantír Package Counts (Resources && Sideboard)",
                Location = new Point(16, 16),
                Size = new Size(495, 220)
            };

            var lblPalHdrCat = new Label { Text = "Category", Location = new Point(16, 24), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblPalHdrRes = new Label { Text = "Resources", Location = new Point(190, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblPalHdrSide = new Label { Text = "Sideboard", Location = new Point(295, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblPalantiriItems = new Label { Text = "Palantíri (Items):", Location = new Point(16, 55), Size = new Size(165, 20) };
            _numResPalantiri = CreateItemSpinner(190, 52, 0, 7, 0);
            _numSidePalantiri = CreateItemSpinner(295, 52, 0, 7, 0);
            _toolTip.SetToolTip(_numResPalantiri, "Unique Palantír items playable at GreaterItem/Palantiri sites or their printed Site Requirement.");

            var lblAllowPalantirUse = new Label { Text = "Allow Palantír Use:", Location = new Point(16, 91), Size = new Size(165, 20) };
            _numResAllowPalantirUse = CreateItemSpinner(190, 88, 0, 6, 0);
            _numSideAllowPalantirUse = CreateItemSpinner(295, 88, 0, 6, 0);
            _toolTip.SetToolTip(_numResAllowPalantirUse, "Total desired (counts Saruman once if already drafted, plus Align/Use Palantír events).");

            var lblPalScopeNote = new Label
            {
                Location = new Point(16, 128),
                Size = new Size(465, 80),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text =
                    "• Palantíri honor Site Requirement (e.g. Isengard, Minas Tirith, The White Towers),\n" +
                    "  GreaterItem/Palantiri sites (e.g. Lossadan Cairn), and Target Species (Elostirion).\n" +
                    "• Allow Palantír Use counts Saruman (once per unique title) before drafting\n" +
                    "  Align Palantír / Use Palantír (respecting Skill: Sage)."
            };

            grpPalantiri.Controls.AddRange([
                lblPalHdrCat, lblPalHdrRes, lblPalHdrSide,
                lblPalantiriItems, _numResPalantiri, _numSidePalantiri,
                lblAllowPalantirUse, _numResAllowPalantirUse, _numSideAllowPalantirUse,
                lblPalScopeNote
            ]);

            var grpPalantirRoles = new GroupBox
            {
                Text = "Role Focus (Uncheck to exclude)",
                Location = new Point(525, 16),
                Size = new Size(300, 220)
            };

            _clbPalantirRoles = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(276, 152),
                CheckOnClick = true
            };
            _clbPalantirRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbPalantirRoles, "Palantír items must carry at least one checked Role tag to be drafted.");

            var btnAllPalRoles = new Button { Text = "All", Location = new Point(12, 184), Size = new Size(65, 24) };
            btnAllPalRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbPalantirRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearPalRoles = new Button { Text = "Clear", Location = new Point(83, 184), Size = new Size(65, 24) };
            btnClearPalRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbPalantirRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpPalantirRoles.Controls.AddRange([_clbPalantirRoles, btnAllPalRoles, btnClearPalRoles]);

            _lblPalantiriStatus = new Label
            {
                Location = new Point(16, 250),
                Size = new Size(805, 240),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = PalantirIdleStatusText
            };

            tabPalantiri.Controls.AddRange([grpPalantiri, grpPalantirRoles, _lblPalantiriStatus]);
            return tabPalantiri;
        }

        private Panel BuildItemsPage()
        {
            var tabItems = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpItemCounts = new GroupBox
            {
                Text = "Item Drafting Counts",
                Location = new Point(16, 16),
                Size = new Size(495, 226)
            };

            var lblHdrItemType = new Label { Text = "Item Tier", Location = new Point(16, 24), Size = new Size(130, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrPool = new Label { Text = "Pool (0-2)", Location = new Point(156, 24), Size = new Size(78, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrRes = new Label { Text = "Resources", Location = new Point(260, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrSide = new Label { Text = "Sideboard", Location = new Point(365, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblMinor = new Label { Text = "Minor Items:", Location = new Point(16, 53), Size = new Size(130, 20) };
            _numPoolMinorItems = CreateItemSpinner(156, 50, 0, 2, 2);
            _numResourceMinorItems = CreateItemSpinner(260, 50, 0, 15, 1);
            _numSideboardMinorItems = CreateItemSpinner(365, 50, 0, 10, 0);
            _toolTip.SetToolTip(_numPoolMinorItems, "Starting Minor Items in Pool (0-2; counts toward 3-copy limit, does not consume Play Deck Size).");

            var lblMajor = new Label { Text = "Major Items:", Location = new Point(16, 87), Size = new Size(130, 20) };
            var lblMajorPoolNa = new Label { Text = "—", Location = new Point(176, 87), Size = new Size(30, 20), ForeColor = Color.DimGray };
            _numResourceMajorItems = CreateItemSpinner(260, 84, 0, 15, 1);
            _numSideboardMajorItems = CreateItemSpinner(365, 84, 0, 10, 1);

            var lblGreater = new Label { Text = "Greater Items:", Location = new Point(16, 121), Size = new Size(130, 20) };
            var lblGreaterPoolNa = new Label { Text = "—", Location = new Point(176, 121), Size = new Size(30, 20), ForeColor = Color.DimGray };
            _numResourceGreaterItems = CreateItemSpinner(260, 118, 0, 15, 1);
            _numSideboardGreaterItems = CreateItemSpinner(365, 118, 0, 10, 0);

            var lblSpecial = new Label { Text = "Special Items:", Location = new Point(16, 155), Size = new Size(130, 20) };
            var lblSpecialPoolNa = new Label { Text = "—", Location = new Point(176, 155), Size = new Size(30, 20), ForeColor = Color.DimGray };
            _numResourceSpecialItems = CreateItemSpinner(260, 152, 0, 10, 0);
            _numSideboardSpecialItems = CreateItemSpinner(365, 152, 0, 10, 0);

            var lblItemScopeNote = new Label
            {
                Location = new Point(16, 183),
                Size = new Size(465, 34),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text = "Excludes Gold Rings, Ring Special Items, Palantíri, and Quest Items (handled on dedicated steps)."
            };

            grpItemCounts.Controls.AddRange([
                lblHdrItemType, lblHdrPool, lblHdrRes, lblHdrSide,
                lblMinor, _numPoolMinorItems, _numResourceMinorItems, _numSideboardMinorItems,
                lblMajor, lblMajorPoolNa, _numResourceMajorItems, _numSideboardMajorItems,
                lblGreater, lblGreaterPoolNa, _numResourceGreaterItems, _numSideboardGreaterItems,
                lblSpecial, lblSpecialPoolNa, _numResourceSpecialItems, _numSideboardSpecialItems,
                lblItemScopeNote
            ]);

            var grpItemRoles = new GroupBox
            {
                Text = "Role Focus (Uncheck to exclude)",
                Location = new Point(525, 16),
                Size = new Size(300, 226)
            };

            _clbItemRoles = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(276, 152),
                CheckOnClick = true
            };
            _clbItemRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbItemRoles, "Items must carry at least one checked Role tag to be drafted.");

            var btnAllItemRoles = new Button { Text = "All", Location = new Point(12, 184), Size = new Size(65, 24) };
            btnAllItemRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbItemRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearItemRoles = new Button { Text = "Clear", Location = new Point(83, 184), Size = new Size(65, 24) };
            btnClearItemRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbItemRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpItemRoles.Controls.AddRange([_clbItemRoles, btnAllItemRoles, btnClearItemRoles]);

            _lblItemStatus = new Label
            {
                Location = new Point(16, 256),
                Size = new Size(805, 220),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = ItemIdleStatusText
            };

            tabItems.Controls.AddRange([grpItemCounts, grpItemRoles, _lblItemStatus]);
            return tabItems;
        }

        private Panel BuildQuestsPage()
        {
            var tabQuests = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpQuests = new GroupBox
            {
                Text = "Quest Drafting Counts && Options (Resources && Sideboard)",
                Location = new Point(16, 16),
                Size = new Size(495, 220)
            };

            var lblQuestHdrCat = new Label { Text = "Category", Location = new Point(16, 24), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblQuestHdrRes = new Label { Text = "Resources", Location = new Point(190, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblQuestHdrSide = new Label { Text = "Sideboard", Location = new Point(295, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblQuestsCount = new Label { Text = "Quests (Events & Items):", Location = new Point(16, 55), Size = new Size(165, 20), UseMnemonic = false };
            _numResourceQuests = CreateItemSpinner(190, 52, 0, 15, 0);
            _numSideboardQuests = CreateItemSpinner(295, 52, 0, 10, 0);
            _toolTip.SetToolTip(_numResourceQuests, "Number of Role = Quest cards to draft into Resources (default 1).");
            _toolTip.SetToolTip(_numSideboardQuests, "Number of Role = Quest cards to draft into Sideboard (default 0).");

            _chkAutoDraftOneRingPackage = new CheckBox
            {
                Text = "Auto-draft One Ring package if needed (Gold Ring + Ring Test + The One Ring)",
                Location = new Point(16, 88),
                Size = new Size(465, 22),
                Checked = true
            };
            _toolTip.SetToolTip(
                _chkAutoDraftOneRingPackage,
                "Allows Cracks of Doom / Gollum's Fate to auto-draft The One Ring (plus a valid Gold Ring and Allow Ring Test if missing)\n" +
                "when Mount Doom and at least one GoldRing-playable site exist in the drafted Site deck.");

            var lblQuestScopeNote = new Label
            {
                Location = new Point(16, 118),
                Size = new Size(465, 90),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text =
                    "• Includes Quest events and Quest Items (Book of Mazarbul, Earth of Galadriel's\n" +
                    "  Orchard, Red Book of Westmarch, Sapling of the White Tree).\n" +
                    "• Enforces Skill, Target Species (e.g. Dúnadan for Andúril, Hobbit for Red Book),\n" +
                    "  Site Requirement, Region Requirement, and Specific.\n" +
                    "• Prioritizes Quests whose Prerequisite cards are already in the deck."
            };

            grpQuests.Controls.AddRange([
                lblQuestHdrCat, lblQuestHdrRes, lblQuestHdrSide,
                lblQuestsCount, _numResourceQuests, _numSideboardQuests,
                _chkAutoDraftOneRingPackage,
                lblQuestScopeNote
            ]);

            var grpQuestRoles = new GroupBox
            {
                Text = "Role Focus (Uncheck to exclude)",
                Location = new Point(525, 16),
                Size = new Size(300, 220)
            };

            _clbQuestRoles = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(276, 152),
                CheckOnClick = true
            };
            _clbQuestRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbQuestRoles, "Filters pure Quests (Quest (Only)) vs. hybrid Quests carrying additional Role tags.");

            var btnAllQuestRoles = new Button { Text = "All", Location = new Point(12, 184), Size = new Size(65, 24) };
            btnAllQuestRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbQuestRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearQuestRoles = new Button { Text = "Clear", Location = new Point(83, 184), Size = new Size(65, 24) };
            btnClearQuestRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbQuestRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpQuestRoles.Controls.AddRange([_clbQuestRoles, btnAllQuestRoles, btnClearQuestRoles]);

            _lblQuestsStatus = new Label
            {
                Location = new Point(16, 250),
                Size = new Size(805, 250),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = QuestIdleStatusText
            };

            tabQuests.Controls.AddRange([grpQuests, grpQuestRoles, _lblQuestsStatus]);
            return tabQuests;
        }

        private void PopulateItemRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbItemRoles.Items.Count; i++)
            {
                if (!_clbItemRoles.GetItemChecked(i))
                {
                    string role = _clbItemRoles.Items[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        previouslyExcluded.Add(role);
                    }
                }
            }

            var activeSets = _getActiveSets();
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in _catalog.Cards)
            {
                if (activeSets.Count > 0 && !activeSets.Contains(card.Set)) continue;
                if (!IsEligibleBaseItem(card)) continue;

                foreach (var role in _filterService.GetCardTagsForCategory(card.Id, "Role"))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }

            var sortedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            _clbItemRoles.BeginUpdate();
            _clbItemRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbItemRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbItemRoles.EndUpdate();
        }

        private void PopulateRingRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbRingRoles.Items.Count; i++)
            {
                if (!_clbRingRoles.GetItemChecked(i))
                {
                    string role = _clbRingRoles.Items[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        previouslyExcluded.Add(role);
                    }
                }
            }

            var activeSets = _getActiveSets();
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in _catalog.Cards)
            {
                if (activeSets.Count > 0 && !activeSets.Contains(card.Set)) continue;
                if (!string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsHeroPlayableAlignment(card.Alignment)) continue;
                if (!IsSpecialRingItem(card) || IsOneRingCard(card)) continue;

                foreach (var role in _filterService.GetCardTagsForCategory(card.Id, "Role"))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }

            var sortedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            _clbRingRoles.BeginUpdate();
            _clbRingRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbRingRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbRingRoles.EndUpdate();
        }

        private void PopulatePalantirRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbPalantirRoles.Items.Count; i++)
            {
                if (!_clbPalantirRoles.GetItemChecked(i))
                {
                    string role = _clbPalantirRoles.Items[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        previouslyExcluded.Add(role);
                    }
                }
            }

            var activeSets = _getActiveSets();
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in _catalog.Cards)
            {
                if (activeSets.Count > 0 && !activeSets.Contains(card.Set)) continue;
                if (!IsHeroPlayableAlignment(card.Alignment)) continue;
                if (!IsPalantirItem(card)) continue;

                foreach (var role in _filterService.GetCardTagsForCategory(card.Id, "Role"))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }

            var sortedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            _clbPalantirRoles.BeginUpdate();
            _clbPalantirRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbPalantirRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbPalantirRoles.EndUpdate();
        }

        private void PopulateQuestRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbQuestRoles.Items.Count; i++)
            {
                if (!_clbQuestRoles.GetItemChecked(i))
                {
                    string role = _clbQuestRoles.Items[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        previouslyExcluded.Add(role);
                    }
                }
            }

            var activeSets = _getActiveSets();
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in _catalog.Cards)
            {
                if (activeSets.Count > 0 && !activeSets.Contains(card.Set)) continue;
                if (!string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsHeroPlayableAlignment(card.Alignment)) continue;
                if (!HasRoleTag(card, "Quest")) continue;

                var secondaryRoles = _filterService.GetCardTagsForCategory(card.Id, "Role")
                    .Where(r => !string.IsNullOrWhiteSpace(r) && !string.Equals(r, "Quest", StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Trim())
                    .ToList();

                if (secondaryRoles.Count == 0)
                {
                    roles.Add(PureQuestRoleLabel);
                }
                else
                {
                    foreach (var sr in secondaryRoles)
                    {
                        roles.Add(sr);
                    }
                }
            }

            var sortedRoles = roles
                .OrderBy(r => string.Equals(r, PureQuestRoleLabel, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _clbQuestRoles.BeginUpdate();
            _clbQuestRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbQuestRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbQuestRoles.EndUpdate();
        }

        private bool RunFactionsDraft(bool silent = false)
        {
            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            int reqRes = (int)_numResourceFactions.Value;
            int reqSide = (int)_numSideboardFactions.Value;

            if (reqRes == 0 && reqSide == 0)
            {
                _lblFactionStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblFactionStatus.Text = "No factions configured (0 drafted).";
                return true;
            }

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allSitesInDeck.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Factions requires Sites/Havens in the Site deck (run Regions & Sites first).",
                        "Factions",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var localSites = allSitesInDeck
                .Where(s =>
                    !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(s.Region) && draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var outsideHavens = allSitesInDeck
                .Where(s =>
                    string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(s.Region) || !draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var poolChars = _deck.Pool;
            var resChars = _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).ToList();
            var sideChars = _deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).ToList();
            var allDraftedChars = poolChars.Concat(resChars).Concat(sideChars).ToList();

            var factionGroups = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            var scoredCandidates = new List<(
                Card Faction,
                List<Card> Prerequisites,
                int PrimaryTier,
                int SourceRank,
                int LocalRank,
                int TieBreaker)>();

            foreach (var faction in factionGroups)
            {
                if (!string.IsNullOrWhiteSpace(faction.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, faction.Specific)))
                {
                    continue;
                }

                var siteReqTags = _filterService.GetCardTagsForCategory(faction.Id, "Site Requirement");
                if (siteReqTags.Count == 0) continue;

                bool playableLocal = siteReqTags.Any(req => MatchesAnySiteRequirement(localSites, req, _filterService));
                bool playableOutside = !playableLocal && siteReqTags.Any(req => MatchesAnySiteRequirement(outsideHavens, req, _filterService));

                if (!playableLocal && !playableOutside) continue;

                var resolvedPrereqs = new List<Card>();
                if (!TryResolvePrerequisiteChain(
                    faction,
                    allCards,
                    activeSets,
                    allSitesInDeck,
                    allDraftedChars,
                    resolvedPrereqs,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                int localRank = playableLocal ? 0 : 1;

                int bonusSourceRank = GetBestFactionBonusSourceRank(faction, poolChars, resChars, sideChars);
                int specificSourceRank = GetBestSpecificCharacterSourceRank(faction, poolChars, resChars, sideChars);
                int tier1SourceRank = Math.Min(bonusSourceRank, specificSourceRank);

                int primaryTier;
                int sourceRank;

                if (tier1SourceRank < 3)
                {
                    primaryTier = 1;
                    sourceRank = tier1SourceRank;
                }
                else
                {
                    int raceSourceRank = GetBestCharacterRaceSourceRank(faction, poolChars, resChars, sideChars);
                    if (raceSourceRank < 3)
                    {
                        primaryTier = 2;
                        sourceRank = raceSourceRank;
                    }
                    else
                    {
                        primaryTier = playableLocal ? 3 : 4;
                        sourceRank = 3;
                    }
                }

                scoredCandidates.Add((faction, resolvedPrereqs, primaryTier, sourceRank, localRank, Random.Shared.Next()));
            }

            var orderedCandidates = scoredCandidates
                .OrderBy(x => x.PrimaryTier)
                .ThenBy(x => x.SourceRank)
                .ThenBy(x => x.LocalRank)
                .ThenBy(x => x.TieBreaker)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Resources.Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);

            var draftedResFactions = new List<Card>();
            var draftedResPrereqs = new List<Card>();
            var factionOverlaps = new List<string>();
            int resCardsAdded = 0;

            // Pass 1: Respect per-cycle site capacity
            DraftFactionsIntoSection(
                orderedCandidates,
                _deck.Resources,
                reqRes,
                resCapacity,
                usedCountsByName,
                siteTracker,
                requireAvailableCapacity: true,
                factionOverlaps,
                draftedResFactions,
                draftedResPrereqs,
                ref resCardsAdded);

            // Pass 2: Fallback if requested count still unmet
            if (draftedResFactions.Count < reqRes)
            {
                DraftFactionsIntoSection(
                    orderedCandidates,
                    _deck.Resources,
                    reqRes,
                    resCapacity,
                    usedCountsByName,
                    siteTracker,
                    requireAvailableCapacity: false,
                    factionOverlaps,
                    draftedResFactions,
                    draftedResPrereqs,
                    ref resCardsAdded);
            }

            var draftedSideFactions = new List<Card>();
            var draftedSidePrereqs = new List<Card>();
            int sideCardsAdded = 0;

            DraftFactionsIntoSection(
                orderedCandidates,
                _deck.Sideboard,
                reqSide,
                sideCapacity,
                usedCountsByName,
                siteTracker: null,
                requireAvailableCapacity: false,
                overlapsOut: null,
                draftedSideFactions,
                draftedSidePrereqs,
                ref sideCardsAdded);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(draftedResFactions.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            string resText = draftedResFactions.Count > 0
                ? string.Join(", ", draftedResFactions.Select(f => f.Name)) +
                  (draftedResPrereqs.Count > 0 ? $" (+ prereq: {string.Join(", ", draftedResPrereqs.Select(p => p.Name))})" : "")
                : "(None)";

            string sideText = draftedSideFactions.Count > 0
                ? string.Join(", ", draftedSideFactions.Select(f => f.Name)) +
                  (draftedSidePrereqs.Count > 0 ? $" (+ prereq: {string.Join(", ", draftedSidePrereqs.Select(p => p.Name))})" : "")
                : "(None)";

            string statusMsg =
                $"Drafted {draftedResFactions.Count} faction(s) into Resources and {draftedSideFactions.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}";

            var warnings = new List<string>();
            if (draftedResFactions.Count < reqRes)
            {
                warnings.Add($"Resources Faction Shortfall: Drafted only {draftedResFactions.Count} of {reqRes} playable faction(s).");
            }
            if (draftedSideFactions.Count < reqSide)
            {
                warnings.Add($"Sideboard Faction Shortfall: Drafted only {draftedSideFactions.Count} of {reqSide} playable faction(s).");
            }
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblFactionStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblFactionStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblFactionStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblFactionStatus.Text = statusMsg;
            return true;
        }

        private static void DraftFactionsIntoSection(
            List<(Card Faction, List<Card> Prerequisites, int PrimaryTier, int SourceRank, int LocalRank, int TieBreaker)> orderedCandidates,
            List<Card> targetSection,
            int requestedFactions,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName,
            DeckCycleSiteTracker siteTracker,
            bool requireAvailableCapacity,
            List<string> overlapsOut,
            List<Card> draftedFactionsOut,
            List<Card> draftedPrereqsOut,
            ref int totalCardsAdded)
        {
            if (requestedFactions <= 0 || sectionCapacity <= 0) return;

            bool CanAddCopy(Card card)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            for (int copyTier = 0; copyTier < 3 && draftedFactionsOut.Count < requestedFactions; copyTier++)
            {
                foreach (var (Faction, Prerequisites, PrimaryTier, SourceRank, LocalRank, TieBreaker) in orderedCandidates)
                {
                    if (draftedFactionsOut.Count >= requestedFactions) break;

                    var faction = Faction;
                    int curCount = usedCountsByName.GetValueOrDefault(faction.Name, 0);
                    if (curCount != copyTier || !CanAddCopy(faction)) continue;

                    var neededPrereqs = new List<Card>();
                    bool prereqLegal = true;

                    foreach (var prereq in Prerequisites)
                    {
                        bool alreadyInSection = targetSection.Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, prereq.Name));
                        if (alreadyInSection) continue;

                        if (!CanAddCopy(prereq))
                        {
                            prereqLegal = false;
                            break;
                        }
                        neededPrereqs.Add(prereq);
                    }

                    if (!prereqLegal) continue;

                    int neededSlots = 1 + neededPrereqs.Count;
                    if (totalCardsAdded + neededSlots > sectionCapacity) continue;

                    Card[] cardsToAdd = [faction, .. neededPrereqs];
                    if (requireAvailableCapacity && siteTracker != null && !siteTracker.CanAddWithinCapacity(cardsToAdd))
                    {
                        continue;
                    }

                    targetSection.Add(faction);
                    usedCountsByName[faction.Name] = curCount + 1;
                    draftedFactionsOut.Add(faction);

                    foreach (var prereq in neededPrereqs)
                    {
                        targetSection.Add(prereq);
                        usedCountsByName[prereq.Name] = usedCountsByName.GetValueOrDefault(prereq.Name, 0) + 1;
                        draftedPrereqsOut.Add(prereq);
                    }

                    siteTracker?.Commit(cardsToAdd, overlapsOut);
                    totalCardsAdded += neededSlots;
                }
            }
        }

        private bool RunAlliesDraft(bool silent = false)
        {
            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            int reqRes = (int)_numResourceAllies.Value;
            int reqSide = (int)_numSideboardAllies.Value;

            if (reqRes == 0 && reqSide == 0)
            {
                _lblAllyStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblAllyStatus.Text = "No allies configured (0 drafted).";
                return true;
            }

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allSitesInDeck.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Allies requires Sites/Havens in the Site deck (run Regions & Sites first).",
                        "Allies",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var localSites = allSitesInDeck
                .Where(s =>
                    !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(s.Region) && draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var outsideHavens = allSitesInDeck
                .Where(s =>
                    string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(s.Region) || !draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var allDraftedChars = _deck.Pool
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allyGroups = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            var scoredCandidates = new List<(
                Card Ally,
                List<Card> Prerequisites,
                int LocalRank,
                int TieBreaker)>();

            foreach (var ally in allyGroups)
            {
                if (!string.IsNullOrWhiteSpace(ally.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, ally.Specific)))
                {
                    continue;
                }

                var siteReqTags = _filterService.GetCardTagsForCategory(ally.Id, "Site Requirement");
                if (siteReqTags.Count == 0) continue;

                bool playableLocal = siteReqTags.Any(req => MatchesAnySiteRequirement(localSites, req, _filterService));
                bool playableOutside = !playableLocal && siteReqTags.Any(req => MatchesAnySiteRequirement(outsideHavens, req, _filterService));

                if (!playableLocal && !playableOutside) continue;

                var resolvedPrereqs = new List<Card>();
                if (!TryResolvePrerequisiteChain(
                    ally,
                    allCards,
                    activeSets,
                    allSitesInDeck,
                    allDraftedChars,
                    resolvedPrereqs,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                int localRank = playableLocal ? 0 : 1;
                scoredCandidates.Add((ally, resolvedPrereqs, localRank, Random.Shared.Next()));
            }

            var orderedCandidates = scoredCandidates
                .OrderBy(x => x.LocalRank)
                .ThenBy(x => x.TieBreaker)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Resources.Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);

            var draftedResAllies = new List<Card>();
            var draftedResPrereqs = new List<Card>();
            var allyOverlaps = new List<string>();
            int resCardsAdded = 0;

            DraftAlliesIntoSection(
                orderedCandidates,
                _deck.Resources,
                reqRes,
                resCapacity,
                usedCountsByName,
                siteTracker,
                requireAvailableCapacity: true,
                allyOverlaps,
                draftedResAllies,
                draftedResPrereqs,
                ref resCardsAdded);

            if (draftedResAllies.Count < reqRes)
            {
                DraftAlliesIntoSection(
                    orderedCandidates,
                    _deck.Resources,
                    reqRes,
                    resCapacity,
                    usedCountsByName,
                    siteTracker,
                    requireAvailableCapacity: false,
                    allyOverlaps,
                    draftedResAllies,
                    draftedResPrereqs,
                    ref resCardsAdded);
            }

            var draftedSideAllies = new List<Card>();
            var draftedSidePrereqs = new List<Card>();
            int sideCardsAdded = 0;

            DraftAlliesIntoSection(
                orderedCandidates,
                _deck.Sideboard,
                reqSide,
                sideCapacity,
                usedCountsByName,
                siteTracker: null,
                requireAvailableCapacity: false,
                overlapsOut: null,
                draftedSideAllies,
                draftedSidePrereqs,
                ref sideCardsAdded);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(draftedResAllies.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            string resText = draftedResAllies.Count > 0
                ? string.Join(", ", draftedResAllies.Select(a => a.Name)) +
                  (draftedResPrereqs.Count > 0 ? $" (+ prereq: {string.Join(", ", draftedResPrereqs.Select(p => p.Name))})" : "")
                : "(None)";

            string sideText = draftedSideAllies.Count > 0
                ? string.Join(", ", draftedSideAllies.Select(a => a.Name)) +
                  (draftedSidePrereqs.Count > 0 ? $" (+ prereq: {string.Join(", ", draftedSidePrereqs.Select(p => p.Name))})" : "")
                : "(None)";

            string statusMsg =
                $"Drafted {draftedResAllies.Count} ally/allies into Resources and {draftedSideAllies.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}";

            var warnings = new List<string>();
            if (draftedResAllies.Count < reqRes)
            {
                warnings.Add($"Resources Ally Shortfall: Drafted only {draftedResAllies.Count} of {reqRes} playable ally/allies.");
            }
            if (draftedSideAllies.Count < reqSide)
            {
                warnings.Add($"Sideboard Ally Shortfall: Drafted only {draftedSideAllies.Count} of {reqSide} playable ally/allies.");
            }
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblAllyStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblAllyStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblAllyStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblAllyStatus.Text = statusMsg;
            return true;
        }

        private static void DraftAlliesIntoSection(
            List<(Card Ally, List<Card> Prerequisites, int LocalRank, int TieBreaker)> orderedCandidates,
            List<Card> targetSection,
            int requestedAllies,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName,
            DeckCycleSiteTracker siteTracker,
            bool requireAvailableCapacity,
            List<string> overlapsOut,
            List<Card> draftedAlliesOut,
            List<Card> draftedPrereqsOut,
            ref int totalCardsAdded)
        {
            if (requestedAllies <= 0 || sectionCapacity <= 0) return;

            bool CanAddCopy(Card card)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            for (int copyTier = 0; copyTier < 3 && draftedAlliesOut.Count < requestedAllies; copyTier++)
            {
                foreach (var (Ally, Prerequisites, LocalRank, TieBreaker) in orderedCandidates)
                {
                    if (draftedAlliesOut.Count >= requestedAllies) break;

                    int curCount = usedCountsByName.GetValueOrDefault(Ally.Name, 0);
                    if (curCount != copyTier || !CanAddCopy(Ally)) continue;

                    var neededPrereqs = new List<Card>();
                    bool prereqLegal = true;

                    foreach (var prereq in Prerequisites)
                    {
                        bool alreadyInSection = targetSection.Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, prereq.Name));
                        if (alreadyInSection) continue;

                        if (!CanAddCopy(prereq))
                        {
                            prereqLegal = false;
                            break;
                        }
                        neededPrereqs.Add(prereq);
                    }

                    if (!prereqLegal) continue;

                    int neededSlots = 1 + neededPrereqs.Count;
                    if (totalCardsAdded + neededSlots > sectionCapacity) continue;

                    Card[] cardsToAdd = [Ally, .. neededPrereqs];
                    if (requireAvailableCapacity && siteTracker != null && !siteTracker.CanAddWithinCapacity(cardsToAdd))
                    {
                        continue;
                    }

                    targetSection.Add(Ally);
                    usedCountsByName[Ally.Name] = curCount + 1;
                    draftedAlliesOut.Add(Ally);

                    foreach (var prereq in neededPrereqs)
                    {
                        targetSection.Add(prereq);
                        usedCountsByName[prereq.Name] = usedCountsByName.GetValueOrDefault(prereq.Name, 0) + 1;
                        draftedPrereqsOut.Add(prereq);
                    }

                    siteTracker?.Commit(cardsToAdd, overlapsOut);
                    totalCardsAdded += neededSlots;
                }
            }
        }

        private bool RunRingsDraft(bool silent = false)
        {
            int reqResGold = (int)_numResGoldRings.Value;
            int reqSideGold = (int)_numSideGoldRings.Value;
            int reqResAllow = (int)_numResAllowRingTest.Value;
            int reqSideAllow = (int)_numSideAllowRingTest.Value;
            int reqResAid = (int)_numResAidRingTest.Value;
            int reqSideAid = (int)_numSideAidRingTest.Value;
            int reqResOne = (int)_numResOneRing.Value;
            int reqSideOne = (int)_numSideOneRing.Value;
            int reqResDwarf = (int)_numResDwarvenRings.Value;
            int reqSideDwarf = (int)_numSideDwarvenRings.Value;
            int reqResMagic = (int)_numResMagicRings.Value;
            int reqSideMagic = (int)_numSideMagicRings.Value;
            int reqResLesser = (int)_numResLesserRings.Value;
            int reqSideLesser = (int)_numSideLesserRings.Value;
            int reqResWizard = (int)_numResWizardsRing.Value;
            int reqSideWizard = (int)_numSideWizardsRing.Value;

            int totalRequested =
                reqResGold + reqSideGold +
                reqResAllow + reqSideAllow +
                reqResAid + reqSideAid +
                reqResOne + reqSideOne +
                reqResDwarf + reqSideDwarf +
                reqResMagic + reqSideMagic +
                reqResLesser + reqSideLesser +
                reqResWizard + reqSideWizard;

            if (totalRequested == 0)
            {
                _lblRingsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblRingsStatus.Text = "No ring cards configured (0 drafted).";
                return true;
            }

            if (reqResOne + reqSideOne > 1)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "The One Ring is Unique: combined Resources + Sideboard count cannot exceed 1.", "Rings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var checkedRingRoles = new HashSet<string>(_clbRingRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            int totalSpecialRings = reqResDwarf + reqSideDwarf + reqResMagic + reqSideMagic + reqResLesser + reqSideLesser + reqResWizard + reqSideWizard;
            if (totalSpecialRings > 0 && _clbRingRoles.Items.Count > 0 && checkedRingRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Role under Special Ring Role Focus on the Rings tab.", "Rings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var goldRingSites = allSitesInDeck
                .Where(s => string.Equals(s.GoldRing, "yes", StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool hasGoldRingSite = goldRingSites.Count > 0;
            bool needsGoldRingSite =
                reqResGold + reqSideGold +
                reqResAllow + reqSideAllow +
                reqResAid + reqSideAid +
                reqResOne + reqSideOne +
                reqResDwarf + reqSideDwarf +
                reqResMagic + reqSideMagic +
                reqResLesser + reqSideLesser > 0;

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var draftedRegionNames = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedRegionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in _deck.Sites.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt))
                    {
                        draftedRegionTypes.Add(rt.Trim());
                    }
                }
            }

            var allDraftedChars = _deck.Pool
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allDeckNonSiteCards = _deck.Pool
                .Concat(_deck.Resources)
                .Concat(_deck.Sideboard)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in allDeckNonSiteCards)
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            bool PassesRingCardGuards(Card card, bool enforceTargetSpecies, bool checkStandardItemSiteFlags, bool enforceRoleFocus)
            {
                if (!IsHeroPlayableAlignment(card.Alignment)) return false;

                if (enforceRoleFocus)
                {
                    var roleTags = _filterService.GetCardTagsForCategory(card.Id, "Role");
                    if (roleTags.Count > 0 && !roleTags.Any(r => checkedRingRoles.Contains(r)))
                    {
                        return false;
                    }
                }

                if (!MatchesItemRegionRequirement(card, draftedRegionNames, draftedRegionTypes)) return false;

                var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
                if (siteReqs.Count > 0)
                {
                    if (!siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                    {
                        return false;
                    }
                }
                else if (checkStandardItemSiteFlags && !MatchesItemSitePlayability(card, allSitesInDeck))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(card.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, card.Specific)))
                {
                    return false;
                }

                if (!SatisfiesExistingPrerequisites(card, allDeckNonSiteCards)) return false;

                if (!MatchesSkillOrRaceRequirements(card, allDraftedChars)) return false;

                if (enforceTargetSpecies && !MatchesItemTargetSpecies(card, allDraftedChars)) return false;

                return true;
            }

            var heroDistinctPool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            List<Card> GetSpecialRingCandidates(Func<Card, bool> predicate, bool enforceSpecies, bool enforceRoleFocus) =>
                [.. heroDistinctPool
                    .Where(c =>
                        string.Equals(c.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase) &&
                        predicate(c) &&
                        PassesRingCardGuards(c, enforceTargetSpecies: enforceSpecies, checkStandardItemSiteFlags: false, enforceRoleFocus: enforceRoleFocus))
                    .OrderBy(_ => Random.Shared.Next())];

            List<Card> draftedResGold = [], draftedSideGold = [];
            List<Card> draftedResAllow = [], draftedSideAllow = [];
            List<Card> draftedResAid = [], draftedSideAid = [];
            List<Card> draftedResOne = [], draftedSideOne = [];
            List<Card> draftedResDwarf = [], draftedSideDwarf = [];
            List<Card> draftedResMagic = [], draftedSideMagic = [];
            List<Card> draftedResLesser = [], draftedSideLesser = [];
            List<Card> innateResAllow = [], innateSideAllow = [];
            List<Card> innateResAid = [], innateSideAid = [];
            int resAllowEffective = 0, sideAllowEffective = 0;
            int resAidEffective = 0, sideAidEffective = 0;
            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);
            var ringOverlaps = new List<string>();

            if (hasGoldRingSite)
            {
                // 1. Gold Rings (ranked by 2d6 outcome probability for requested One / Dwarven / Magic Rings; variety-first otherwise)
                var goldCandidates = heroDistinctPool
                    .Where(c =>
                        string.Equals(c.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase) &&
                        PassesRingCardGuards(c, enforceTargetSpecies: true, checkStandardItemSiteFlags: false, enforceRoleFocus: false))
                    .ToList();

                int wOne = reqResOne + reqSideOne;
                int wDwarf = reqResDwarf + reqSideDwarf;
                int wMagic = reqResMagic + reqSideMagic;

                if (wOne + wDwarf + wMagic > 0)
                {
                    var rankedGold = goldCandidates
                        .Select(c => (Card: c, Score: ScoreGoldRingForSpecials(c.Text, wOne, wDwarf, wMagic), Tie: Random.Shared.Next()))
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.Tie)
                        .Select(x => x.Card)
                        .ToList();

                    draftedResGold = DraftCardsRankedPlaysetFirst(rankedGold, _deck.Resources, reqResGold, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, ringOverlaps);
                    resCapacity -= draftedResGold.Count;
                    if (draftedResGold.Count < reqResGold && resCapacity > 0)
                    {
                        var extraGold = DraftCardsRankedPlaysetFirst(rankedGold, _deck.Resources, reqResGold - draftedResGold.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, ringOverlaps);
                        draftedResGold.AddRange(extraGold);
                        resCapacity -= extraGold.Count;
                    }

                    draftedSideGold = DraftCardsRankedPlaysetFirst(rankedGold, _deck.Sideboard, reqSideGold, sideCapacity, usedCountsByName);
                    sideCapacity -= draftedSideGold.Count;
                }
                else
                {
                    var shuffledGold = goldCandidates.OrderBy(_ => Random.Shared.Next()).ToList();
                    draftedResGold = DraftItemsIntoSection(shuffledGold, _deck.Resources, reqResGold, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, ringOverlaps);
                    resCapacity -= draftedResGold.Count;
                    if (draftedResGold.Count < reqResGold && resCapacity > 0)
                    {
                        var extraGold = DraftItemsIntoSection(shuffledGold, _deck.Resources, reqResGold - draftedResGold.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, ringOverlaps);
                        draftedResGold.AddRange(extraGold);
                        resCapacity -= extraGold.Count;
                    }

                    draftedSideGold = DraftItemsIntoSection(shuffledGold, _deck.Sideboard, reqSideGold, sideCapacity, usedCountsByName);
                    sideCapacity -= draftedSideGold.Count;
                }

                // 2. Allow Ring Test (counts Unique testers such as Gandalf once per distinct title)
                innateResAllow = _deck.Resources.Where(c => HasSpecialAbility(c, "Gold Ring: Allow Test")).ToList();
                innateSideAllow = _deck.Sideboard.Where(c => HasSpecialAbility(c, "Gold Ring: Allow Test")).ToList();
                resAllowEffective = CountEffectiveSpecialAbility(innateResAllow);
                sideAllowEffective = CountEffectiveSpecialAbility(innateSideAllow);
                int neededResAllow = Math.Max(0, reqResAllow - resAllowEffective);
                int neededSideAllow = Math.Max(0, reqSideAllow - sideAllowEffective);

                var baseAllowCandidates = heroDistinctPool
                    .Where(c =>
                        HasSpecialAbility(c, "Gold Ring: Allow Test") &&
                        PassesRingCardGuards(c, enforceTargetSpecies: true, checkStandardItemSiteFlags: IsStandardTierItem(c), enforceRoleFocus: false))
                    .ToList();

                List<Card> OrderAllowCandidatesForSection(int oneRingCount, int otherTestedRingsCount) =>
                    [.. baseAllowCandidates
                        .Where(c => !(oneRingCount > 0 && otherTestedRingsCount == 0 && HasSpecialAbility(c, "Gold Ring: Exclude One Ring")))
                        .OrderBy(c => oneRingCount > 0 && HasSpecialAbility(c, "Gold Ring: Exclude One Ring") ? 1 : 0)
                        .ThenBy(_ => Random.Shared.Next())];

                var resAllowCandidates = OrderAllowCandidatesForSection(reqResOne, reqResDwarf + reqResMagic + reqResLesser);
                draftedResAllow = DraftItemsIntoSection(resAllowCandidates, _deck.Resources, neededResAllow, resCapacity, usedCountsByName);
                resCapacity -= draftedResAllow.Count;

                var sideAllowCandidates = OrderAllowCandidatesForSection(reqSideOne, reqSideDwarf + reqSideMagic + reqSideLesser);
                draftedSideAllow = DraftItemsIntoSection(sideAllowCandidates, _deck.Sideboard, neededSideAllow, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideAllow.Count;

                // 3. Aid Ring Test (counts already-drafted aid cards such as Scroll of Isildur from Items)
                innateResAid = _deck.Resources.Where(c => HasSpecialAbility(c, "Gold Ring: Aid Test")).ToList();
                innateSideAid = _deck.Sideboard.Where(c => HasSpecialAbility(c, "Gold Ring: Aid Test")).ToList();
                resAidEffective = CountEffectiveSpecialAbility(innateResAid);
                sideAidEffective = CountEffectiveSpecialAbility(innateSideAid);
                int neededResAid = Math.Max(0, reqResAid - resAidEffective);
                int neededSideAid = Math.Max(0, reqSideAid - sideAidEffective);

                var aidCandidates = heroDistinctPool
                    .Where(c =>
                        HasSpecialAbility(c, "Gold Ring: Aid Test") &&
                        PassesRingCardGuards(c, enforceTargetSpecies: true, checkStandardItemSiteFlags: IsStandardTierItem(c), enforceRoleFocus: false))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                draftedResAid = DraftItemsIntoSection(aidCandidates, _deck.Resources, neededResAid, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true);
                resCapacity -= draftedResAid.Count;
                if (draftedResAid.Count < neededResAid && resCapacity > 0)
                {
                    var extraAid = DraftItemsIntoSection(aidCandidates, _deck.Resources, neededResAid - draftedResAid.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false);
                    draftedResAid.AddRange(extraAid);
                    resCapacity -= extraAid.Count;
                }
                draftedSideAid = DraftItemsIntoSection(aidCandidates, _deck.Sideboard, neededSideAid, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideAid.Count;

                // 4a. Gold-Ring-Tested Special Rings
                var oneCandidates = GetSpecialRingCandidates(IsOneRingCard, enforceSpecies: true, enforceRoleFocus: false);
                draftedResOne = DraftItemsIntoSection(oneCandidates, _deck.Resources, reqResOne, resCapacity, usedCountsByName);
                resCapacity -= draftedResOne.Count;
                draftedSideOne = DraftItemsIntoSection(oneCandidates, _deck.Sideboard, reqSideOne, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideOne.Count;

                // Dwarven Rings: playable by any character (do not enforce Target Species: Dwarf)
                var dwarfCandidates = GetSpecialRingCandidates(IsDwarvenRingCard, enforceSpecies: false, enforceRoleFocus: true);
                draftedResDwarf = DraftItemsIntoSection(dwarfCandidates, _deck.Resources, reqResDwarf, resCapacity, usedCountsByName);
                resCapacity -= draftedResDwarf.Count;
                draftedSideDwarf = DraftItemsIntoSection(dwarfCandidates, _deck.Sideboard, reqSideDwarf, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideDwarf.Count;

                var magicCandidates = GetSpecialRingCandidates(IsMagicRingCard, enforceSpecies: true, enforceRoleFocus: true);
                draftedResMagic = DraftItemsIntoSection(magicCandidates, _deck.Resources, reqResMagic, resCapacity, usedCountsByName);
                resCapacity -= draftedResMagic.Count;
                draftedSideMagic = DraftItemsIntoSection(magicCandidates, _deck.Sideboard, reqSideMagic, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideMagic.Count;

                var lesserCandidates = GetSpecialRingCandidates(IsLesserRingCard, enforceSpecies: true, enforceRoleFocus: true);
                draftedResLesser = DraftItemsIntoSection(lesserCandidates, _deck.Resources, reqResLesser, resCapacity, usedCountsByName);
                resCapacity -= draftedResLesser.Count;
                draftedSideLesser = DraftItemsIntoSection(lesserCandidates, _deck.Sideboard, reqSideLesser, sideCapacity, usedCountsByName);
                sideCapacity -= draftedSideLesser.Count;
            }

            // 4b. Wizard's Ring (playable directly at a Haven without a GoldRing site)
            var wizardRingCandidates = GetSpecialRingCandidates(IsWizardsRingCard, enforceSpecies: true, enforceRoleFocus: true);
            var draftedResWizard = DraftItemsIntoSection(wizardRingCandidates, _deck.Resources, reqResWizard, resCapacity, usedCountsByName);
            resCapacity -= draftedResWizard.Count;
            var draftedSideWizard = DraftItemsIntoSection(wizardRingCandidates, _deck.Sideboard, reqSideWizard, sideCapacity, usedCountsByName);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Resources);
            }

            // 5. Status & Shortfall Reporting
            var resAll = draftedResGold.Concat(draftedResAllow).Concat(draftedResAid)
                .Concat(draftedResOne).Concat(draftedResDwarf).Concat(draftedResMagic).Concat(draftedResLesser).Concat(draftedResWizard)
                .ToList();
            var sideAll = draftedSideGold.Concat(draftedSideAllow).Concat(draftedSideAid)
                .Concat(draftedSideOne).Concat(draftedSideDwarf).Concat(draftedSideMagic).Concat(draftedSideLesser).Concat(draftedSideWizard)
                .ToList();

            string resText = resAll.Count > 0 ? FormatCardCountList(resAll) : "(None)";
            string sideText = sideAll.Count > 0 ? FormatCardCountList(sideAll) : "(None)";

            var innateNotes = new List<string>();
            if (innateResAllow.Count > 0) innateNotes.Add($"Resources Allow Test: {FormatInnateAbilitySummary(innateResAllow, resAllowEffective)}");
            if (innateSideAllow.Count > 0) innateNotes.Add($"Sideboard Allow Test: {FormatInnateAbilitySummary(innateSideAllow, sideAllowEffective)}");
            if (innateResAid.Count > 0) innateNotes.Add($"Resources Aid Test: {FormatInnateAbilitySummary(innateResAid, resAidEffective)}");
            if (innateSideAid.Count > 0) innateNotes.Add($"Sideboard Aid Test: {FormatInnateAbilitySummary(innateSideAid, sideAidEffective)}");

            string goldSiteNames = goldRingSites.Count > 0
                ? string.Join(", ", goldRingSites.Select(s => s.Name))
                : "(None)";

            string statusMsg =
                $"Drafted {resAll.Count} ring card(s) into Resources and {sideAll.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}\n" +
                $"• GoldRing-playable sites ({goldRingSites.Count}): {goldSiteNames}";

            if (innateNotes.Count > 0)
            {
                statusMsg += $"\n• Already in deck from earlier tabs: {string.Join(" | ", innateNotes)}";
            }

            var warnings = new List<string>();
            void CheckShortfall(string label, int actual, int requested, string extra = null)
            {
                if (actual < requested)
                {
                    string suffix = !string.IsNullOrWhiteSpace(extra) ? $" ({extra})" : string.Empty;
                    warnings.Add($"{label} Shortfall: Only {actual} of {requested} met{suffix}.");
                }
            }

            if (!hasGoldRingSite && needsGoldRingSite)
            {
                warnings.Add("0 GoldRing-playable sites exist in the drafted Site deck; skipped Gold Rings, Ring Tests & tested Special Rings (widen Seed Region / Region Count or enable Ruins & Lairs).");
            }
            else
            {
                CheckShortfall("Resources Gold Rings", draftedResGold.Count, reqResGold);
                CheckShortfall("Sideboard Gold Rings", draftedSideGold.Count, reqSideGold);
                CheckShortfall("Resources Allow Ring Test", resAllowEffective + draftedResAllow.Count, reqResAllow);
                CheckShortfall("Sideboard Allow Ring Test", sideAllowEffective + draftedSideAllow.Count, reqSideAllow);
                CheckShortfall("Resources Aid Ring Test", resAidEffective + draftedResAid.Count, reqResAid);
                CheckShortfall("Sideboard Aid Ring Test", sideAidEffective + draftedSideAid.Count, reqSideAid);
                CheckShortfall("Resources The One Ring", draftedResOne.Count, reqResOne);
                CheckShortfall("Sideboard The One Ring", draftedSideOne.Count, reqSideOne);
                CheckShortfall("Resources Dwarven Rings", draftedResDwarf.Count, reqResDwarf);
                CheckShortfall("Sideboard Dwarven Rings", draftedSideDwarf.Count, reqSideDwarf);
                CheckShortfall("Resources Magic Rings", draftedResMagic.Count, reqResMagic);
                CheckShortfall("Sideboard Magic Rings", draftedSideMagic.Count, reqSideMagic);
                CheckShortfall("Resources Lesser Rings", draftedResLesser.Count, reqResLesser);
                CheckShortfall("Sideboard Lesser Rings", draftedSideLesser.Count, reqSideLesser);
            }

            bool hasWizard = allDraftedChars.Any(c => string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase));
            CheckShortfall("Resources Wizard's Ring", draftedResWizard.Count, reqResWizard, !hasWizard ? "requires a Wizard in the deck" : null);
            CheckShortfall("Sideboard Wizard's Ring", draftedSideWizard.Count, reqSideWizard, !hasWizard ? "requires a Wizard in the deck" : null);
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblRingsStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblRingsStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblRingsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblRingsStatus.Text = statusMsg;
            return true;
        }

        private bool RunPalantiriDraft(bool silent = false)
        {
            int reqResPal = (int)_numResPalantiri.Value;
            int reqSidePal = (int)_numSidePalantiri.Value;
            int reqResAllow = (int)_numResAllowPalantirUse.Value;
            int reqSideAllow = (int)_numSideAllowPalantirUse.Value;

            int totalRequested = reqResPal + reqSidePal + reqResAllow + reqSideAllow;
            if (totalRequested == 0)
            {
                _lblPalantiriStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblPalantiriStatus.Text = "No Palantír cards configured (0 drafted).";
                return true;
            }

            var checkedPalantirRoles = new HashSet<string>(_clbPalantirRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if (reqResPal + reqSidePal > 0 && _clbPalantirRoles.Items.Count > 0 && checkedPalantirRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Role under Role Focus on the Palantíri tab.", "Palantíri", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var draftedRegionNames = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedRegionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in _deck.Sites.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt))
                    {
                        draftedRegionTypes.Add(rt.Trim());
                    }
                }
            }

            var allDraftedChars = _deck.Pool
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allDeckNonSiteCards = _deck.Pool
                .Concat(_deck.Resources)
                .Concat(_deck.Sideboard)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in allDeckNonSiteCards)
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            bool MatchesPalantirSitePlayability(Card palantirItem)
            {
                var siteReqs = _filterService.GetCardTagsForCategory(palantirItem.Id, "Site Requirement");
                if (siteReqs.Count > 0)
                {
                    return siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService));
                }

                bool needsHoard = string.Equals(palantirItem.Hoard, "yes", StringComparison.OrdinalIgnoreCase);
                return allSitesInDeck.Any(site =>
                {
                    if (needsHoard && !string.Equals(site.Hoard, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(site.Palantiri, "yes", StringComparison.OrdinalIgnoreCase);
                });
            }

            bool PassesPalantirCardGuards(Card card, bool enforceRoleFocus, bool isPalantirItem)
            {
                if (!IsHeroPlayableAlignment(card.Alignment)) return false;

                if (enforceRoleFocus)
                {
                    var roleTags = _filterService.GetCardTagsForCategory(card.Id, "Role");
                    if (roleTags.Count > 0 && !roleTags.Any(r => checkedPalantirRoles.Contains(r)))
                    {
                        return false;
                    }
                }

                if (!MatchesItemRegionRequirement(card, draftedRegionNames, draftedRegionTypes)) return false;

                if (isPalantirItem)
                {
                    if (!MatchesPalantirSitePlayability(card)) return false;
                }
                else
                {
                    var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
                    if (siteReqs.Count > 0)
                    {
                        if (!siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                        {
                            return false;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(card.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, card.Specific)))
                {
                    return false;
                }

                if (!SatisfiesExistingPrerequisites(card, allDeckNonSiteCards)) return false;

                if (!MatchesSkillOrRaceRequirements(card, allDraftedChars)) return false;

                if (!MatchesItemTargetSpecies(card, allDraftedChars)) return false;

                return true;
            }

            var heroDistinctPool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            // 1. Draft Palantíri Items (Resources Pass 1 within site capacity -> Pass 2 fallback -> Sideboard)
            var palantirCandidates = heroDistinctPool
                .Where(c =>
                    IsPalantirItem(c) &&
                    PassesPalantirCardGuards(c, enforceRoleFocus: true, isPalantirItem: true))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);
            var palantirOverlaps = new List<string>();

            var draftedResPal = DraftItemsIntoSection(palantirCandidates, _deck.Resources, reqResPal, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, palantirOverlaps);
            resCapacity -= draftedResPal.Count;
            if (draftedResPal.Count < reqResPal && resCapacity > 0)
            {
                var extraPal = DraftItemsIntoSection(palantirCandidates, _deck.Resources, reqResPal - draftedResPal.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, palantirOverlaps);
                draftedResPal.AddRange(extraPal);
                resCapacity -= extraPal.Count;
            }

            var draftedSidePal = DraftItemsIntoSection(palantirCandidates, _deck.Sideboard, reqSidePal, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSidePal.Count;

            // 2. Allow Palantír Use (counts Unique Saruman once per distinct title before drafting Align/Use Palantír)
            var innateResAllow = _deck.Resources.Where(c => HasSpecialAbility(c, "Palantír: Allow Use")).ToList();
            var innateSideAllow = _deck.Sideboard.Where(c => HasSpecialAbility(c, "Palantír: Allow Use")).ToList();
            int resAllowEffective = CountEffectiveSpecialAbility(innateResAllow);
            int sideAllowEffective = CountEffectiveSpecialAbility(innateSideAllow);
            int neededResAllow = Math.Max(0, reqResAllow - resAllowEffective);
            int neededSideAllow = Math.Max(0, reqSideAllow - sideAllowEffective);

            var allowCandidates = heroDistinctPool
                .Where(c =>
                    HasSpecialAbility(c, "Palantír: Allow Use") &&
                    PassesPalantirCardGuards(c, enforceRoleFocus: false, isPalantirItem: false))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var draftedResAllow = DraftItemsIntoSection(allowCandidates, _deck.Resources, neededResAllow, resCapacity, usedCountsByName);
            resCapacity -= draftedResAllow.Count;
            var draftedSideAllow = DraftItemsIntoSection(allowCandidates, _deck.Sideboard, neededSideAllow, sideCapacity, usedCountsByName);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Resources);
            }

            // 3. Discover playable sites in _deck.Sites (GreaterItem / Palantiri flags or Palantír Site Requirements)
            var siteSpecificTargets = heroDistinctPool
                .Where(IsPalantirItem)
                .SelectMany(p => _filterService.GetCardTagsForCategory(p.Id, "Site Requirement"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var palantirSites = allSitesInDeck
                .Where(site =>
                    string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(site.Palantiri, "yes", StringComparison.OrdinalIgnoreCase) ||
                    siteSpecificTargets.Any(req => MatchesAnySiteRequirement([site], req, _filterService)))
                .ToList();

            // 4. Status & Shortfall Reporting
            var resAll = draftedResPal.Concat(draftedResAllow).ToList();
            var sideAll = draftedSidePal.Concat(draftedSideAllow).ToList();

            string resText = resAll.Count > 0 ? FormatCardCountList(resAll) : "(None)";
            string sideText = sideAll.Count > 0 ? FormatCardCountList(sideAll) : "(None)";
            string palSiteNames = palantirSites.Count > 0 ? string.Join(", ", palantirSites.Select(s => s.Name)) : "(None)";

            var innateNotes = new List<string>();
            if (innateResAllow.Count > 0) innateNotes.Add($"Resources Allow Use: {FormatInnateAbilitySummary(innateResAllow, resAllowEffective)}");
            if (innateSideAllow.Count > 0) innateNotes.Add($"Sideboard Allow Use: {FormatInnateAbilitySummary(innateSideAllow, sideAllowEffective)}");

            string statusMsg =
                $"Drafted {resAll.Count} Palantír card(s) into Resources and {sideAll.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}\n" +
                $"• Palantír-playable sites ({palantirSites.Count}): {palSiteNames}";

            if (innateNotes.Count > 0)
            {
                statusMsg += $"\n• Already in deck from earlier tabs: {string.Join(" | ", innateNotes)}";
            }

            bool hasSage = allDraftedChars.Any(c => CardFilterService.MatchesCardnumCriterion(c, "Skill", "Sage"));
            var warnings = new List<string>();

            if (draftedResPal.Count < reqResPal)
            {
                warnings.Add($"Resources Palantíri Shortfall: Only {draftedResPal.Count} of {reqResPal} playable Palantír item(s) drafted.");
            }
            if (draftedSidePal.Count < reqSidePal)
            {
                warnings.Add($"Sideboard Palantíri Shortfall: Only {draftedSidePal.Count} of {reqSidePal} playable Palantír item(s) drafted.");
            }
            if (resAllowEffective + draftedResAllow.Count < reqResAllow)
            {
                string extra = !hasSage ? " (requires a Sage or Saruman)" : string.Empty;
                warnings.Add($"Resources Allow Palantír Use Shortfall: Only {resAllowEffective + draftedResAllow.Count} of {reqResAllow} met{extra}.");
            }
            if (sideAllowEffective + draftedSideAllow.Count < reqSideAllow)
            {
                string extra = !hasSage ? " (requires a Sage or Saruman)" : string.Empty;
                warnings.Add($"Sideboard Allow Palantír Use Shortfall: Only {sideAllowEffective + draftedSideAllow.Count} of {reqSideAllow} met{extra}.");
            }
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblPalantiriStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblPalantiriStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblPalantiriStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblPalantiriStatus.Text = statusMsg;
            return true;
        }

        private bool RunItemsDraft(bool silent = false)
        {
            int reqPoolMinor = (int)_numPoolMinorItems.Value;
            int reqResMinor = (int)_numResourceMinorItems.Value;
            int reqSideMinor = (int)_numSideboardMinorItems.Value;
            int reqResMajor = (int)_numResourceMajorItems.Value;
            int reqSideMajor = (int)_numSideboardMajorItems.Value;
            int reqResGreater = (int)_numResourceGreaterItems.Value;
            int reqSideGreater = (int)_numSideboardGreaterItems.Value;
            int reqResSpecial = (int)_numResourceSpecialItems.Value;
            int reqSideSpecial = (int)_numSideboardSpecialItems.Value;

            int totalRequested = reqPoolMinor + reqResMinor + reqSideMinor +
                                 reqResMajor + reqSideMajor +
                                 reqResGreater + reqSideGreater +
                                 reqResSpecial + reqSideSpecial;

            if (totalRequested == 0)
            {
                _lblItemStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblItemStatus.Text = "No items configured (0 drafted).";
                return true;
            }

            var checkedRoles = new HashSet<string>(_clbItemRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if (_clbItemRoles.Items.Count > 0 && checkedRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Please select at least one Role under Role Focus on the Items tab.",
                        "Items",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var draftedRegionNames = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedRegionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in _deck.Sites.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt))
                    {
                        draftedRegionTypes.Add(rt.Trim());
                    }
                }
            }

            var poolChars = _deck.Pool
                .Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var allDraftedChars = poolChars
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allDeckNonSiteCards = _deck.Pool
                .Concat(_deck.Resources)
                .Concat(_deck.Sideboard)
                .ToList();

            // Build base item pool (grouped by Name, preferring Hero alignment), excluding Rings & Palantíri
            var candidateItems = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    IsEligibleBaseItem(c))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            bool PassesNonSiteGuards(Card item)
            {
                var roleTags = _filterService.GetCardTagsForCategory(item.Id, "Role");
                if (roleTags.Count == 0 || !roleTags.Any(r => checkedRoles.Contains(r)))
                {
                    return false;
                }

                if (!MatchesItemRegionRequirement(item, draftedRegionNames, draftedRegionTypes))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(item.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, item.Specific)))
                {
                    return false;
                }

                if (!SatisfiesExistingPrerequisites(item, allDeckNonSiteCards))
                {
                    return false;
                }

                if (!MatchesItemTargetSpecies(item, allDraftedChars))
                {
                    return false;
                }

                return true;
            }

            List<Card> GetTierCandidates(string secondaryType, bool checkSites) =>
                [.. candidateItems
                    .Where(item =>
                        string.Equals(item.Secondary, secondaryType, StringComparison.OrdinalIgnoreCase) &&
                        PassesNonSiteGuards(item) &&
                        (!checkSites || MatchesItemSitePlayability(item, allSitesInDeck)))
                    .OrderBy(_ => Random.Shared.Next())];

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in allDeckNonSiteCards)
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);
            var itemOverlaps = new List<string>();

            // 1. Pool Minor Items (placed at Rivendell before Turn 1; no site tap)
            int effectivePoolCap = Math.Min(reqPoolMinor, poolChars.Count);
            var poolMinorCandidates = GetTierCandidates("Minor Item", checkSites: false);
            var draftedPoolMinor = DraftItemsIntoSection(poolMinorCandidates, _deck.Pool, effectivePoolCap, effectivePoolCap, usedCountsByName);

            var specialCandidates = GetTierCandidates("Special Item", checkSites: true);
            var greaterCandidates = GetTierCandidates("Greater Item", checkSites: true);
            var majorCandidates = GetTierCandidates("Major Item", checkSites: true);
            var minorCandidates = GetTierCandidates("Minor Item", checkSites: true);

            // 2. Resources Items Pass 1 (within per-cycle site capacity across Special -> Greater -> Major -> Minor)
            var draftedResSpecial = DraftItemsIntoSection(specialCandidates, _deck.Resources, reqResSpecial, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, itemOverlaps);
            resCapacity -= draftedResSpecial.Count;
            var draftedResGreater = DraftItemsIntoSection(greaterCandidates, _deck.Resources, reqResGreater, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, itemOverlaps);
            resCapacity -= draftedResGreater.Count;
            var draftedResMajor = DraftItemsIntoSection(majorCandidates, _deck.Resources, reqResMajor, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, itemOverlaps);
            resCapacity -= draftedResMajor.Count;
            var draftedResMinor = DraftItemsIntoSection(minorCandidates, _deck.Resources, reqResMinor, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: true, itemOverlaps);
            resCapacity -= draftedResMinor.Count;

            // 3. Resources Items Pass 2 (fallback if any tier is short)
            if (draftedResSpecial.Count < reqResSpecial && resCapacity > 0)
            {
                var extra = DraftItemsIntoSection(specialCandidates, _deck.Resources, reqResSpecial - draftedResSpecial.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, itemOverlaps);
                draftedResSpecial.AddRange(extra);
                resCapacity -= extra.Count;
            }
            if (draftedResGreater.Count < reqResGreater && resCapacity > 0)
            {
                var extra = DraftItemsIntoSection(greaterCandidates, _deck.Resources, reqResGreater - draftedResGreater.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, itemOverlaps);
                draftedResGreater.AddRange(extra);
                resCapacity -= extra.Count;
            }
            if (draftedResMajor.Count < reqResMajor && resCapacity > 0)
            {
                var extra = DraftItemsIntoSection(majorCandidates, _deck.Resources, reqResMajor - draftedResMajor.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, itemOverlaps);
                draftedResMajor.AddRange(extra);
                resCapacity -= extra.Count;
            }
            if (draftedResMinor.Count < reqResMinor && resCapacity > 0)
            {
                var extra = DraftItemsIntoSection(minorCandidates, _deck.Resources, reqResMinor - draftedResMinor.Count, resCapacity, usedCountsByName, siteTracker, requireAvailableCapacity: false, itemOverlaps);
                draftedResMinor.AddRange(extra);
            }

            // 4. Sideboard Items
            var draftedSideSpecial = DraftItemsIntoSection(specialCandidates, _deck.Sideboard, reqSideSpecial, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideSpecial.Count;
            var draftedSideGreater = DraftItemsIntoSection(greaterCandidates, _deck.Sideboard, reqSideGreater, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideGreater.Count;
            var draftedSideMajor = DraftItemsIntoSection(majorCandidates, _deck.Sideboard, reqSideMajor, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideMajor.Count;
            var draftedSideMinor = DraftItemsIntoSection(minorCandidates, _deck.Sideboard, reqSideMinor, sideCapacity, usedCountsByName);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Resources);
            }

            var draftedResAll = draftedResMinor.Concat(draftedResMajor).Concat(draftedResGreater).Concat(draftedResSpecial).ToList();
            var draftedSideAll = draftedSideMinor.Concat(draftedSideMajor).Concat(draftedSideGreater).Concat(draftedSideSpecial).ToList();

            string poolText = draftedPoolMinor.Count > 0 ? string.Join(", ", draftedPoolMinor.Select(c => c.Name)) : "(None)";
            string resText = draftedResAll.Count > 0 ? string.Join(", ", draftedResAll.Select(c => c.Name)) : "(None)";
            string sideText = draftedSideAll.Count > 0 ? string.Join(", ", draftedSideAll.Select(c => c.Name)) : "(None)";

            string statusMsg =
                $"Drafted {draftedPoolMinor.Count} Pool, {draftedResAll.Count} Resources, and {draftedSideAll.Count} Sideboard item(s):\n" +
                $"• Pool: {poolText}\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}";

            var warnings = new List<string>();
            if (reqPoolMinor > poolChars.Count)
            {
                warnings.Add($"Pool Minor Item Shortfall: Requested {reqPoolMinor}, but only {poolChars.Count} starting character(s) in Pool.");
            }
            else if (draftedPoolMinor.Count < reqPoolMinor)
            {
                warnings.Add($"Pool Minor Item Shortfall: Drafted only {draftedPoolMinor.Count} of {reqPoolMinor}.");
            }

            void CheckShortfall(string label, int drafted, int requested)
            {
                if (drafted < requested)
                {
                    warnings.Add($"{label} Shortfall: Drafted only {drafted} of {requested} eligible item(s).");
                }
            }

            CheckShortfall("Resources Minor Item", draftedResMinor.Count, reqResMinor);
            CheckShortfall("Sideboard Minor Item", draftedSideMinor.Count, reqSideMinor);
            CheckShortfall("Resources Major Item", draftedResMajor.Count, reqResMajor);
            CheckShortfall("Sideboard Major Item", draftedSideMajor.Count, reqSideMajor);
            CheckShortfall("Resources Greater Item", draftedResGreater.Count, reqResGreater);
            CheckShortfall("Sideboard Greater Item", draftedSideGreater.Count, reqSideGreater);
            CheckShortfall("Resources Special Item", draftedResSpecial.Count, reqResSpecial);
            CheckShortfall("Sideboard Special Item", draftedSideSpecial.Count, reqSideSpecial);
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblItemStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblItemStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblItemStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblItemStatus.Text = statusMsg;
            return true;
        }

        private bool RunQuestsDraft(bool silent = false)
        {
            int reqRes = (int)_numResourceQuests.Value;
            int reqSide = (int)_numSideboardQuests.Value;

            if (reqRes == 0 && reqSide == 0)
            {
                _lblQuestsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblQuestsStatus.Text = "No quests configured (0 drafted).";
                return true;
            }

            var checkedQuestRoles = new HashSet<string>(_clbQuestRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if (_clbQuestRoles.Items.Count > 0 && checkedQuestRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Role under Role Focus on the Quests tab.", "Quests", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allSitesInDeck.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Quests requires Sites/Havens in the Site deck (run Regions & Sites first).",
                        "Quests",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var goldRingSites = allSitesInDeck
                .Where(s => string.Equals(s.GoldRing, "yes", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var draftedRegionNames = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedRegionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in _deck.Sites.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt))
                    {
                        draftedRegionTypes.Add(rt.Trim());
                    }
                }
            }

            var allDraftedChars = _deck.Pool
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allDeckNonSiteCards = _deck.Pool
                .Concat(_deck.Resources)
                .Concat(_deck.Sideboard)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in allDeckNonSiteCards)
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var heroDistinctPool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            bool PassesGeneralCardGuards(Card card, bool enforceQuestSiteFallback)
            {
                if (!IsHeroPlayableAlignment(card.Alignment)) return false;

                if (!MatchesItemRegionRequirement(card, draftedRegionNames, draftedRegionTypes)) return false;

                var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
                if (siteReqs.Count > 0)
                {
                    if (!siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                    {
                        return false;
                    }
                }
                else if (enforceQuestSiteFallback && IsStandardTierItem(card) && !MatchesItemSitePlayability(card, allSitesInDeck))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(card.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, card.Specific)))
                {
                    return false;
                }

                if (!MatchesSkillOrRaceRequirements(card, allDraftedChars)) return false;

                if (!MatchesItemTargetSpecies(card, allDraftedChars)) return false;

                return true;
            }

            bool CanAddCopyWithPending(Card card, Dictionary<string, int> pendingCounts)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0) + pendingCounts.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            bool IsCardAlreadySatisfiedForSection(Card card, List<Card> targetSection)
            {
                if (card == null) return false;
                if (string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    return _deck.Pool.Concat(_deck.Resources).Concat(_deck.Sideboard)
                        .Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, card.Name));
                }

                if (targetSection == _deck.Resources)
                {
                    return _deck.Resources.Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, card.Name));
                }

                return _deck.Resources.Concat(_deck.Sideboard)
                    .Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, card.Name));
            }

            string oneRingDiag = null;

            bool TryResolveOneRingPackage(
                List<Card> targetSection,
                List<Card> branchAdded,
                Dictionary<string, int> pendingCounts)
            {
                var oneRingCard = heroDistinctPool.FirstOrDefault(IsOneRingCard);
                if (oneRingCard == null)
                {
                    oneRingDiag ??= "Cracks of Doom skipped: The One Ring not found in active sets.";
                    return false;
                }

                if (IsCardAlreadySatisfiedForSection(oneRingCard, targetSection))
                {
                    return true;
                }

                if (!_chkAutoDraftOneRingPackage.Checked || goldRingSites.Count == 0)
                {
                    oneRingDiag ??= !_chkAutoDraftOneRingPackage.Checked
                        ? "Cracks of Doom skipped: 'Auto-draft One Ring package' is unchecked."
                        : "Cracks of Doom skipped: 0 GoldRing-playable sites in Sites.";
                    return false;
                }

                if (!PassesGeneralCardGuards(oneRingCard, enforceQuestSiteFallback: false) ||
                    !CanAddCopyWithPending(oneRingCard, pendingCounts))
                {
                    oneRingDiag ??= "Cracks of Doom skipped: The One Ring failed card guards or copy limit.";
                    return false;
                }

                branchAdded.Add(oneRingCard);
                pendingCounts[oneRingCard.Name] = pendingCounts.GetValueOrDefault(oneRingCard.Name, 0) + 1;

                var supportScope = targetSection == _deck.Resources
                    ? _deck.Resources
                    : _deck.Resources.Concat(_deck.Sideboard);

                var eligibleOneRingGolds = heroDistinctPool
                    .Where(c =>
                        string.Equals(c.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase) &&
                        ScoreGoldRingForSpecials(c.Text, 1, 0, 0) > 0 &&
                        PassesGeneralCardGuards(c, enforceQuestSiteFallback: false))
                    .Select(c => (Card: c, Score: ScoreGoldRingForSpecials(c.Text, 1, 0, 0)))
                    .ToList();

                if (eligibleOneRingGolds.Count == 0)
                {
                    oneRingDiag ??= "Cracks of Doom skipped: 0 eligible Gold Rings found for The One Ring.";
                    return false;
                }

                double bestOneRingGoldScore = eligibleOneRingGolds.Max(x => x.Score);

                bool hasOptimalGoldRing = supportScope.Any(c =>
                    string.Equals(c.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase) &&
                    ScoreGoldRingForSpecials(c.Text, 1, 0, 0) >= bestOneRingGoldScore);

                if (!hasOptimalGoldRing)
                {
                    var chosenGold = eligibleOneRingGolds
                        .Where(x => x.Score >= bestOneRingGoldScore && CanAddCopyWithPending(x.Card, pendingCounts))
                        .OrderBy(_ => Random.Shared.Next())
                        .Select(x => x.Card)
                        .FirstOrDefault();

                    chosenGold ??= eligibleOneRingGolds
                        .Where(x => CanAddCopyWithPending(x.Card, pendingCounts))
                        .OrderByDescending(x => x.Score)
                        .ThenBy(_ => Random.Shared.Next())
                        .Select(x => x.Card)
                        .FirstOrDefault();

                    if (chosenGold == null)
                    {
                        oneRingDiag ??= "Cracks of Doom skipped: no Gold Ring copy available.";
                        return false;
                    }

                    branchAdded.Add(chosenGold);
                    pendingCounts[chosenGold.Name] = pendingCounts.GetValueOrDefault(chosenGold.Name, 0) + 1;
                }

                var oneRingAllowSources = supportScope
                    .Where(c =>
                        HasSpecialAbility(c, "Gold Ring: Allow Test") &&
                        !HasSpecialAbility(c, "Gold Ring: Exclude One Ring"))
                    .ToList();

                int reusableAllowCount = CountEffectiveSpecialAbility(
                    oneRingAllowSources.Where(c => !string.Equals(c.Secondary, "Short-event", StringComparison.OrdinalIgnoreCase)));

                if (reusableAllowCount == 0)
                {
                    int singleUseUsableAllowCount = oneRingAllowSources
                        .Count(c => string.Equals(c.Secondary, "Short-event", StringComparison.OrdinalIgnoreCase));

                    int existingTestedSpecialRings = supportScope
                        .Count(c => IsDwarvenRingCard(c) || IsMagicRingCard(c) || IsLesserRingCard(c));

                    int excludedSingleUseAllowCount = supportScope.Count(c =>
                        HasSpecialAbility(c, "Gold Ring: Allow Test") &&
                        HasSpecialAbility(c, "Gold Ring: Exclude One Ring") &&
                        string.Equals(c.Secondary, "Short-event", StringComparison.OrdinalIgnoreCase));

                    int uncoveredPriorRings = Math.Max(0, existingTestedSpecialRings - excludedSingleUseAllowCount);

                    if (singleUseUsableAllowCount == 0 || singleUseUsableAllowCount <= uncoveredPriorRings)
                    {
                        var chosenAllow = heroDistinctPool
                            .Where(c =>
                                HasSpecialAbility(c, "Gold Ring: Allow Test") &&
                                !HasSpecialAbility(c, "Gold Ring: Exclude One Ring") &&
                                PassesGeneralCardGuards(c, enforceQuestSiteFallback: true) &&
                                CanAddCopyWithPending(c, pendingCounts))
                            .OrderBy(c => usedCountsByName.GetValueOrDefault(c.Name, 0) + pendingCounts.GetValueOrDefault(c.Name, 0))
                            .ThenBy(_ => Random.Shared.Next())
                            .FirstOrDefault();

                        if (chosenAllow == null)
                        {
                            oneRingDiag ??= "Cracks of Doom skipped: no eligible Allow Ring Test card found.";
                            return false;
                        }

                        branchAdded.Add(chosenAllow);
                        pendingCounts[chosenAllow.Name] = pendingCounts.GetValueOrDefault(chosenAllow.Name, 0) + 1;
                    }
                }

                return true;
            }

            bool TryResolveQuestPrereqBranch(
                Card card,
                List<Card> targetSection,
                List<Card> resolvedExtras,
                Dictionary<string, int> pendingCounts,
                HashSet<string> visitedNames)
            {
                if (card == null || !visitedNames.Add(card.Name)) return true;

                var prereqTags = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite");
                if (prereqTags.Count == 0) return true;

                var candidateBranches = new List<(List<Card> Extras, Dictionary<string, int> Counts)>();

                foreach (var orBranch in prereqTags)
                {
                    var andTerms = orBranch.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (andTerms.Length == 0) continue;

                    var branchExtras = new List<Card>();
                    var branchCounts = new Dictionary<string, int>(pendingCounts, StringComparer.OrdinalIgnoreCase);
                    var branchVisited = new HashSet<string>(visitedNames, StringComparer.OrdinalIgnoreCase);
                    bool branchOk = true;

                    foreach (var prereqName in andTerms)
                    {
                        if (CardFilterService.EqualsWithDiacriticFallback(prereqName, "The One Ring"))
                        {
                            if (!TryResolveOneRingPackage(targetSection, branchExtras, branchCounts))
                            {
                                branchOk = false;
                                break;
                            }
                            continue;
                        }

                        var prereqCard = heroDistinctPool.FirstOrDefault(c =>
                            CardFilterService.EqualsWithDiacriticFallback(c.Name, prereqName));

                        if (prereqCard == null)
                        {
                            branchOk = false;
                            break;
                        }

                        if (!IsCardAlreadySatisfiedForSection(prereqCard, targetSection) &&
                            branchCounts.GetValueOrDefault(prereqCard.Name, 0) == 0)
                        {
                            if (!PassesGeneralCardGuards(prereqCard, enforceQuestSiteFallback: true) ||
                                !CanAddCopyWithPending(prereqCard, branchCounts))
                            {
                                branchOk = false;
                                break;
                            }

                            branchExtras.Add(prereqCard);
                            branchCounts[prereqCard.Name] = branchCounts.GetValueOrDefault(prereqCard.Name, 0) + 1;
                        }

                        if (!TryResolveQuestPrereqBranch(prereqCard, targetSection, branchExtras, branchCounts, branchVisited))
                        {
                            branchOk = false;
                            break;
                        }
                    }

                    if (branchOk)
                    {
                        candidateBranches.Add((branchExtras, branchCounts));
                    }
                }

                if (candidateBranches.Count == 0) return false;

                var (Extras, Counts) = candidateBranches
                    .OrderBy(b => b.Extras.Count)
                    .ThenBy(_ => Random.Shared.Next())
                    .First();

                resolvedExtras.AddRange(Extras);
                foreach (var kvp in Counts)
                {
                    pendingCounts[kvp.Key] = kvp.Value;
                }

                return true;
            }

            var questCandidates = heroDistinctPool
                .Where(c =>
                    HasRoleTag(c, "Quest") &&
                    MatchesQuestRoleFocus(c, checkedQuestRoles) &&
                    PassesGeneralCardGuards(c, enforceQuestSiteFallback: true))
                .Select(c => (Card: c, TieBreaker: Random.Shared.Next()))
                .OrderBy(x => x.TieBreaker)
                .ToList();

            if (!questCandidates.Any(q => CardFilterService.EqualsWithDiacriticFallback(q.Card.Name, "Cracks of Doom")))
            {
                oneRingDiag ??= "Cracks of Doom not in questCandidates (failed Role / Role Focus / Mount Doom / Guards).";
            }

            void DraftQuestsIntoSection(
                List<Card> targetSection,
                int requestedCount,
                ref int remainingCapacity,
                List<Card> draftedQuestsOut,
                List<Card> draftedExtrasOut,
                DeckCycleSiteTracker siteTracker = null,
                bool requireAvailableCapacity = false,
                List<string> overlapsOut = null)
            {
                if (requestedCount <= 0 || remainingCapacity <= 0) return;

                for (int copyTier = 0; copyTier < 3 && draftedQuestsOut.Count < requestedCount && remainingCapacity > 0; copyTier++)
                {
                    while (draftedQuestsOut.Count < requestedCount && remainingCapacity > 0)
                    {
                        (Card Quest, List<Card> Extras)? bestPick = null;
                        int bestTier = int.MaxValue;
                        int bestTie = int.MaxValue;

                        foreach (var (candidate, tieBreaker) in questCandidates)
                        {
                            int curCount = usedCountsByName.GetValueOrDefault(candidate.Name, 0);
                            int max = string.Equals(candidate.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                            if (curCount != copyTier || curCount >= max) continue;

                            var extras = new List<Card>();
                            var pending = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                            {
                                [candidate.Name] = 1
                            };

                            if (!TryResolveQuestPrereqBranch(
                                candidate,
                                targetSection,
                                extras,
                                pending,
                                new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            int slotsNeeded = 1 + extras.Count;
                            if (slotsNeeded > remainingCapacity)
                            {
                                if (CardFilterService.EqualsWithDiacriticFallback(candidate.Name, "Cracks of Doom"))
                                {
                                    oneRingDiag ??= $"Cracks of Doom skipped: needs {slotsNeeded} slots, only {remainingCapacity} remaining.";
                                }
                                continue;
                            }

                            if (requireAvailableCapacity && siteTracker != null &&
                                !siteTracker.CanAddWithinCapacity([candidate, .. extras]))
                            {
                                if (CardFilterService.EqualsWithDiacriticFallback(candidate.Name, "Cracks of Doom"))
                                {
                                    oneRingDiag ??= "Cracks of Doom skipped in Pass 1: Gold Ring exceeds non-Haven site capacity for the planned deck cycle(s).";
                                }
                                continue;
                            }

                            bool hasPrereqTag = _filterService.GetCardTagsForCategory(candidate.Id, "Prerequisite").Count > 0;
                            int tier = (hasPrereqTag && extras.Count == 0) ? 1 : 2;

                            if (tier < bestTier || (tier == bestTier && tieBreaker < bestTie))
                            {
                                bestTier = tier;
                                bestTie = tieBreaker;
                                bestPick = (candidate, extras);
                            }
                        }

                        if (!bestPick.HasValue) break;

                        var (chosenQuest, chosenExtras) = bestPick.Value;

                        targetSection.Add(chosenQuest);
                        usedCountsByName[chosenQuest.Name] = usedCountsByName.GetValueOrDefault(chosenQuest.Name, 0) + 1;
                        draftedQuestsOut.Add(chosenQuest);

                        foreach (var extra in chosenExtras)
                        {
                            targetSection.Add(extra);
                            usedCountsByName[extra.Name] = usedCountsByName.GetValueOrDefault(extra.Name, 0) + 1;
                            draftedExtrasOut.Add(extra);
                        }

                        siteTracker?.Commit([chosenQuest, .. chosenExtras], overlapsOut);
                        remainingCapacity -= 1 + chosenExtras.Count;
                    }
                }
            }

            var siteTracker = new DeckCycleSiteTracker(this, (int)_numDecksBeforeCouncil.Value, _deck.Resources, allSitesInDeck);
            var questOverlaps = new List<string>();

            var draftedResQuests = new List<Card>();
            var draftedResExtras = new List<Card>();
            DraftQuestsIntoSection(_deck.Resources, reqRes, ref resCapacity, draftedResQuests, draftedResExtras, siteTracker, requireAvailableCapacity: true, questOverlaps);
            if (draftedResQuests.Count < reqRes && resCapacity > 0)
            {
                DraftQuestsIntoSection(_deck.Resources, reqRes, ref resCapacity, draftedResQuests, draftedResExtras, siteTracker, requireAvailableCapacity: false, questOverlaps);
            }

            var draftedSideQuests = new List<Card>();
            var draftedSideExtras = new List<Card>();
            DraftQuestsIntoSection(_deck.Sideboard, reqSide, ref sideCapacity, draftedSideQuests, draftedSideExtras);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(draftedResQuests.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            string resText = draftedResQuests.Count > 0
                ? FormatCardCountList(draftedResQuests) +
                  (draftedResExtras.Count > 0 ? $" (+ prereq/ring: {FormatCardCountList(draftedResExtras)})" : "")
                : "(None)";

            string sideText = draftedSideQuests.Count > 0
                ? FormatCardCountList(draftedSideQuests) +
                  (draftedSideExtras.Count > 0 ? $" (+ prereq/ring: {FormatCardCountList(draftedSideExtras)})" : "")
                : "(None)";

            string statusMsg =
                $"Drafted {draftedResQuests.Count} quest(s) into Resources and {draftedSideQuests.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}";

            if (!string.IsNullOrEmpty(oneRingDiag) &&
                !draftedResQuests.Any(q => CardFilterService.EqualsWithDiacriticFallback(q.Name, "Cracks of Doom")))
            {
                statusMsg += $"\n• Diagnostic: {oneRingDiag}";
            }

            var warnings = new List<string>();
            if (draftedResQuests.Count < reqRes)
            {
                warnings.Add($"Resources Quest Shortfall: Drafted only {draftedResQuests.Count} of {reqRes} eligible quest(s).");
            }
            if (draftedSideQuests.Count < reqSide)
            {
                warnings.Add($"Sideboard Quest Shortfall: Drafted only {draftedSideQuests.Count} of {reqSide} eligible quest(s).");
            }
            warnings.AddRange(siteTracker.BuildOverlapMessages());

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblQuestsStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblQuestsStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblQuestsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblQuestsStatus.Text = statusMsg;
            return true;
        }

        private static List<Card> DraftItemsIntoSection(
            List<Card> candidates,
            List<Card> targetSection,
            int requestedCount,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName,
            DeckCycleSiteTracker siteTracker = null,
            bool requireAvailableCapacity = false,
            List<string> overlapsOut = null)
        {
            var drafted = new List<Card>();
            if (requestedCount <= 0 || sectionCapacity <= 0 || candidates.Count == 0)
            {
                return drafted;
            }

            bool CanAddCopy(Card card)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            // Variety-first: pick distinct titles before adding duplicate non-unique copies
            for (int copyTier = 0; copyTier < 3 && drafted.Count < requestedCount && drafted.Count < sectionCapacity; copyTier++)
            {
                foreach (var item in candidates)
                {
                    if (drafted.Count >= requestedCount || drafted.Count >= sectionCapacity) break;

                    int curCount = usedCountsByName.GetValueOrDefault(item.Name, 0);
                    if (curCount != copyTier || !CanAddCopy(item)) continue;

                    if (requireAvailableCapacity && siteTracker != null && !siteTracker.CanAddWithinCapacity([item]))
                    {
                        continue;
                    }

                    targetSection.Add(item);
                    usedCountsByName[item.Name] = curCount + 1;
                    drafted.Add(item);
                    siteTracker?.Commit([item], overlapsOut);
                }
            }

            return drafted;
        }

        private bool IsEligibleBaseItem(Card card)
        {
            if (card == null) return false;
            if (!string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsHeroPlayableAlignment(card.Alignment)) return false;

            bool isTargetItemTier =
                string.Equals(card.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase);

            if (!isTargetItemTier) return false;

            // Exclude Palantíri (dedicated Palantíri tab)
            if (HasRaceToken(card, "Palantír")) return false;

            // Exclude Ring Special Items (handled by dedicated Rings tab)
            if (IsSpecialRingItem(card)) return false;

            // Exclude Quest items (handled by dedicated Quests tab)
            if (HasRoleTag(card, "Quest")) return false;

            return true;
        }

        private bool MatchesQuestRoleFocus(Card quest, HashSet<string> checkedRoles)
        {
            var secondaryRoles = _filterService.GetCardTagsForCategory(quest.Id, "Role")
                .Where(r => !string.IsNullOrWhiteSpace(r) && !string.Equals(r, "Quest", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Trim())
                .ToList();

            if (secondaryRoles.Count == 0)
            {
                return checkedRoles.Contains(PureQuestRoleLabel);
            }

            return secondaryRoles.Any(r => checkedRoles.Contains(r));
        }

        private static List<Card> DraftCardsRankedPlaysetFirst(
            List<Card> rankedCandidates,
            List<Card> targetSection,
            int requestedCount,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName,
            DeckCycleSiteTracker siteTracker = null,
            bool requireAvailableCapacity = false,
            List<string> overlapsOut = null)
        {
            var drafted = new List<Card>();
            if (requestedCount <= 0 || sectionCapacity <= 0 || rankedCandidates.Count == 0)
            {
                return drafted;
            }

            foreach (var card in rankedCandidates)
            {
                if (drafted.Count >= requestedCount || drafted.Count >= sectionCapacity) break;

                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);

                while (current < max && drafted.Count < requestedCount && drafted.Count < sectionCapacity)
                {
                    if (requireAvailableCapacity && siteTracker != null && !siteTracker.CanAddWithinCapacity([card]))
                    {
                        break;
                    }

                    targetSection.Add(card);
                    current++;
                    usedCountsByName[card.Name] = current;
                    drafted.Add(card);
                    siteTracker?.Commit([card], overlapsOut);
                }
            }

            return drafted;
        }

        private static double ScoreGoldRingForSpecials(string cardText, int wOne, int wDwarf, int wMagic)
        {
            if (string.IsNullOrWhiteSpace(cardText)) return 0;

            int oneWays = 0;
            int dwarfWays = 0;
            int magicWays = 0;

            foreach (Match match in GoldRingOutcomeRegex.Matches(cardText))
            {
                string ringType = match.Groups[1].Value.Trim().ToLowerInvariant();
                string numbersText = match.Groups[2].Value;
                int ways = SumTwoDiceWays(numbersText);

                if (ringType.Contains("one")) oneWays = Math.Max(oneWays, ways);
                else if (ringType.Contains("dwarven")) dwarfWays = Math.Max(dwarfWays, ways);
                else if (ringType.Contains("magic") || ringType.Contains("spirit")) magicWays = Math.Max(magicWays, ways);
            }

            return (wOne * 5.0 * oneWays) + (wDwarf * 2.0 * dwarfWays) + (wMagic * magicWays);
        }

        private static int SumTwoDiceWays(string numbersText)
        {
            if (string.IsNullOrWhiteSpace(numbersText)) return 0;

            var outcomes = new HashSet<int>();

            foreach (Match rangeMatch in GoldRingRangeRegex.Matches(numbersText))
            {
                if (int.TryParse(rangeMatch.Groups[1].Value, out int start) &&
                    int.TryParse(rangeMatch.Groups[2].Value, out int end))
                {
                    for (int n = Math.Min(start, end); n <= Math.Max(start, end); n++)
                    {
                        if (n >= 2 && n <= 12) outcomes.Add(n);
                    }
                }
            }

            foreach (Match numMatch in GoldRingNumberRegex.Matches(numbersText))
            {
                if (int.TryParse(numMatch.Value, out int n) && n >= 2 && n <= 12)
                {
                    outcomes.Add(n);
                }
            }

            int totalWays = 0;
            foreach (int n in outcomes)
            {
                totalWays += 6 - Math.Abs(7 - n); // 2d6 combinations out of 36
            }
            return totalWays;
        }

        private int GetBestFactionBonusSourceRank(
            Card faction,
            List<Card> poolChars,
            List<Card> resChars,
            List<Card> sideChars)
        {
            if (AnyCharacterHasFactionBonus(poolChars, faction)) return 0;
            if (AnyCharacterHasFactionBonus(resChars, faction)) return 1;
            if (AnyCharacterHasFactionBonus(sideChars, faction)) return 2;
            return 3;
        }

        private static int GetBestSpecificCharacterSourceRank(
            Card faction,
            List<Card> poolChars,
            List<Card> resChars,
            List<Card> sideChars)
        {
            if (string.IsNullOrWhiteSpace(faction.Specific)) return 3;

            if (poolChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, faction.Specific))) return 0;
            if (resChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, faction.Specific))) return 1;
            if (sideChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, faction.Specific))) return 2;
            return 3;
        }

        private bool AnyCharacterHasFactionBonus(List<Card> chars, Card faction)
        {
            if (chars == null || chars.Count == 0) return false;

            foreach (var ch in chars)
            {
                var bonusTags = _filterService.GetCardTagsForCategory(ch.Id, "Faction Bonus");
                foreach (var tag in bonusTags)
                {
                    if (string.IsNullOrWhiteSpace(tag) ||
                        tag == "*" ||
                        string.Equals(tag, "All", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (CardFilterService.TryParsePredicate(tag, out _, out _, out _))
                    {
                        if (CardFilterService.MatchesPredicate(faction, tag, _filterService))
                        {
                            return true;
                        }
                    }
                    else if (CardFilterService.EqualsWithDiacriticFallback(faction.Name, tag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetBestCharacterRaceSourceRank(
            Card faction,
            List<Card> poolChars,
            List<Card> resChars,
            List<Card> sideChars)
        {
            if (string.IsNullOrWhiteSpace(faction.Race)) return 3;

            if (AnyCharacterMatchesFactionRace(poolChars, faction.Race)) return 0;
            if (AnyCharacterMatchesFactionRace(resChars, faction.Race)) return 1;
            if (AnyCharacterMatchesFactionRace(sideChars, faction.Race)) return 2;
            return 3;
        }

        private static bool AnyCharacterMatchesFactionRace(List<Card> chars, string factionRace)
        {
            if (chars == null || chars.Count == 0 || string.IsNullOrWhiteSpace(factionRace)) return false;

            var factionRaces = factionRace.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var ch in chars)
            {
                if (string.IsNullOrWhiteSpace(ch.Race)) continue;
                var charRaces = ch.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (charRaces.Any(cr => factionRaces.Any(fr => CardFilterService.EqualsWithDiacriticFallback(cr, fr))))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

