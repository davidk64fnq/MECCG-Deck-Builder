using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed partial class DeckAssemblyMatrixDialog
    {
        private const string ResourceEventsIdleStatusText =
            "Fills remaining Play Deck (Resources) capacity up to Play Deck Size, plus requested Sideboard events.\n" +
            "Drafts Environment cards (Gates of Morning, Twilight) first, then distributes remaining slots proportionately across checked Role tags.";

        private const string CreaturesIdleStatusText =
            "Drafts Nazgûl, Nazgûl Support events, Standard Creatures (1.0), and non-Nazgûl Dual-Mode Half-Creatures (0.5).\n" +
            "Uses proportionate Creature Race buckets with variety-first sampling; all Unique creatures remain eligible.";

        private const string HazardEventsIdleStatusText =
            "Fills remaining Play Deck (Hazards) capacity up to Play Deck Size, plus requested Sideboard events.\n" +
            "Drafts Environment cards (Doors of Night, Twilight) first, then distributes remaining slots proportionately across checked Role tags.";

        private Panel BuildResourceEventsPage()
        {
            var tabResourceEvents = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpEnv = new GroupBox
            {
                Text = "Environment (Resources && Sideboard)",
                Location = new Point(16, 16),
                Size = new Size(495, 148)
            };

            var lblEnvHdrCard = new Label { Text = "Card", Location = new Point(16, 22), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblEnvHdrRes = new Label { Text = "Resources", Location = new Point(190, 22), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblEnvHdrSide = new Label { Text = "Sideboard", Location = new Point(295, 22), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblGom = new Label { Text = "Gates of Morning:", Location = new Point(16, 51), Size = new Size(165, 20) };
            _numResGatesOfMorning = CreateItemSpinner(190, 48, 0, 3, 0);
            _numSideGatesOfMorning = CreateItemSpinner(295, 48, 0, 3, 0);
            _toolTip.SetToolTip(_numResGatesOfMorning, "Permanent-event (up to 3 total across Resources + Sideboard).");
            _toolTip.SetToolTip(_numSideGatesOfMorning, "Permanent-event (up to 3 total across Resources + Sideboard).");

            var lblTwi = new Label { Text = "Twilight:", Location = new Point(16, 83), Size = new Size(165, 20) };
            _numResTwilight = CreateItemSpinner(190, 80, 0, 3, 0);
            _numSideTwilight = CreateItemSpinner(295, 80, 0, 3, 0);
            _toolTip.SetToolTip(_numResTwilight, "Dual-purpose Resource/Hazard Short-event (shares 3-copy limit across Resources, Hazards & Sideboard).");
            _toolTip.SetToolTip(_numSideTwilight, "Dual-purpose Resource/Hazard Short-event (shares 3-copy limit across Resources, Hazards & Sideboard).");

            var lblEnvNote = new Label
            {
                Location = new Point(16, 112),
                Size = new Size(465, 30),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text = "Twilight counts as Resource or Hazard and shares its 3-copy limit across Resources, Hazards & Sideboard."
            };

            grpEnv.Controls.AddRange([
                lblEnvHdrCard, lblEnvHdrRes, lblEnvHdrSide,
                lblGom, _numResGatesOfMorning, _numSideGatesOfMorning,
                lblTwi, _numResTwilight, _numSideTwilight,
                lblEnvNote
            ]);

            var grpAutoFill = new GroupBox
            {
                Text = "Resource Events Auto-Fill && Sideboard",
                Location = new Point(16, 174),
                Size = new Size(495, 134)
            };

            var lblAutoPlayDeck = new Label
            {
                Location = new Point(16, 24),
                Size = new Size(465, 20),
                UseMnemonic = false,
                Text = "Play Deck (Resources):   Auto-fills all remaining slots up to Play Deck Size."
            };

            var lblSideAuto = new Label
            {
                Text = "Additional Events in Sideboard:",
                Location = new Point(16, 54),
                Size = new Size(205, 20)
            };
            _numSideboardResourceEvents = CreateItemSpinner(228, 51, 0, 20, 4);
            _toolTip.SetToolTip(
                _numSideboardResourceEvents,
                "Number of general Resource Events to draft into Sideboard (beyond Sideboard Gates of Morning / Twilight).");

            var lblAutoNote = new Label
            {
                Location = new Point(16, 82),
                Size = new Size(465, 44),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text =
                    "• Distributes slots proportionately by eligible cards per checked Role (min 1 each if slots permit).\n" +
                    "• Excludes Gates of Morning, Twilight, Quests, Ring Tests & Palantír Use events.\n" +
                    "• Enforces Skill, Target Species, Specific, Site/Region Requirements & existing Prerequisites."
            };

            grpAutoFill.Controls.AddRange([lblAutoPlayDeck, lblSideAuto, _numSideboardResourceEvents, lblAutoNote]);

            var grpResEventRoles = new GroupBox
            {
                Text = "Role (Uncheck to exclude)",
                Location = new Point(525, 16),
                Size = new Size(300, 292)
            };

            _clbResourceEventRoles = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(276, 224),
                CheckOnClick = true
            };
            _clbResourceEventRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbResourceEventRoles, "Auto-filled Resource Events must carry at least one checked Role tag.");

            var btnAllEventRoles = new Button { Text = "All", Location = new Point(12, 256), Size = new Size(65, 24) };
            btnAllEventRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbResourceEventRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearEventRoles = new Button { Text = "Clear", Location = new Point(83, 256), Size = new Size(65, 24) };
            btnClearEventRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbResourceEventRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpResEventRoles.Controls.AddRange([_clbResourceEventRoles, btnAllEventRoles, btnClearEventRoles]);

            GroupBox grpResourceEventsStatus;
            (grpResourceEventsStatus, _rtbResourceEventsStatus) = CreateStatusGroupBox(16, 318, 809, 344, ResourceEventsIdleStatusText);

            tabResourceEvents.Controls.AddRange([grpEnv, grpAutoFill, grpResEventRoles, grpResourceEventsStatus]);
            return tabResourceEvents;
        }

        private Panel BuildCreaturesPage()
        {
            var tabCreatures = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpNazgul = new GroupBox
            {
                Text = "Nazgûl Package (Hazards && Sideboard)",
                Location = new Point(16, 14),
                Size = new Size(475, 116)
            };

            var lblNazHdrCat = new Label { Text = "Card Group", Location = new Point(16, 22), Size = new Size(160, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblNazHdrHaz = new Label { Text = "Hazards", Location = new Point(180, 22), Size = new Size(72, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblNazHdrSide = new Label { Text = "Sideboard", Location = new Point(258, 22), Size = new Size(74, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            NumericUpDown CreateCreatureSpinner(int x, int y, int min, int max, int defVal)
            {
                var nud = CreateItemSpinner(x, y, min, max, defVal);
                nud.ValueChanged += (s, e) => UpdateCreatureEquivalentsReadout();
                return nud;
            }

            var lblNazgul = new Label { Text = "Nazgûl (0.5 equiv):", Location = new Point(16, 49), Size = new Size(160, 20) };
            _numHazNazgul = CreateCreatureSpinner(180, 46, 0, 9, 0);
            _numSideNazgul = CreateCreatureSpinner(258, 46, 0, 9, 0);
            var lblNazNote = new Label { Text = "Unique (<= 9 total)", Location = new Point(334, 49), Size = new Size(135, 20), ForeColor = Color.DimGray };
            _toolTip.SetToolTip(_numHazNazgul, "Unique Nazgûl (Race = Nazgûl; 0.5 creature equivalent each). Combined Hazards + Sideboard <= 9.");
            _toolTip.SetToolTip(_numSideNazgul, "Unique Nazgûl in Sideboard (combined Hazards + Sideboard <= 9).");

            var lblNazSup = new Label { Text = "Nazgûl Support:", Location = new Point(16, 81), Size = new Size(160, 20) };
            _numHazNazgulSupport = CreateCreatureSpinner(180, 78, 0, 15, 0);
            _numSideNazgulSupport = CreateCreatureSpinner(258, 78, 0, 10, 0);
            var lblNazSupNote = new Label { Text = "Target: Nazgûl", Location = new Point(334, 81), Size = new Size(135, 20), ForeColor = Color.DimGray };
            _toolTip.SetToolTip(_numHazNazgulSupport, "Hazard support events tagged Target Species = Nazgûl.");
            _toolTip.SetToolTip(_numSideNazgulSupport, "Sideboard Nazgûl support events tagged Target Species = Nazgûl.");

            grpNazgul.Controls.AddRange([
                lblNazHdrCat, lblNazHdrHaz, lblNazHdrSide,
                lblNazgul, _numHazNazgul, _numSideNazgul, lblNazNote,
                lblNazSup, _numHazNazgulSupport, _numSideNazgulSupport, lblNazSupNote
            ]);

            var grpCreatures = new GroupBox
            {
                Text = "Standard && Dual-Mode Creatures",
                Location = new Point(16, 138),
                Size = new Size(475, 168)
            };

            var lblCrHdrCat = new Label { Text = "Creature Tier", Location = new Point(16, 22), Size = new Size(160, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblCrHdrHaz = new Label { Text = "Hazards", Location = new Point(180, 22), Size = new Size(72, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblCrHdrSide = new Label { Text = "Sideboard", Location = new Point(258, 22), Size = new Size(74, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblStdCr = new Label { Text = "Standard Creatures (1.0):", Location = new Point(16, 49), Size = new Size(160, 20) };
            _numHazStandardCreatures = CreateCreatureSpinner(180, 46, 0, 30, 12);
            _numSideStandardCreatures = CreateCreatureSpinner(258, 46, 0, 15, 2);
            var lblStdNote = new Label { Text = "Secondary = Creature", Location = new Point(334, 49), Size = new Size(135, 20), ForeColor = Color.DimGray };

            var lblDualCr = new Label { Text = "Dual-Mode Half (0.5):", Location = new Point(16, 81), Size = new Size(160, 20) };
            _numHazDualCreatures = CreateCreatureSpinner(180, 78, 0, 12, 0);
            _numSideDualCreatures = CreateCreatureSpinner(258, 78, 0, 10, 0);
            var lblDualNote = new Label { Text = "Non-Nazgûl duals", Location = new Point(334, 81), Size = new Size(135, 20), ForeColor = Color.DimGray };
            _toolTip.SetToolTip(_numHazDualCreatures, "Non-Nazgûl dual-mode creatures (e.g. Shelob, Mouth of Sauron; 0.5 creature equivalent each).");

            _lblCreatureEquivReadout = new Label
            {
                Location = new Point(16, 114),
                Size = new Size(445, 44),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93),
                UseMnemonic = false
            };

            grpCreatures.Controls.AddRange([
                lblCrHdrCat, lblCrHdrHaz, lblCrHdrSide,
                lblStdCr, _numHazStandardCreatures, _numSideStandardCreatures, lblStdNote,
                lblDualCr, _numHazDualCreatures, _numSideDualCreatures, lblDualNote,
                _lblCreatureEquivReadout
            ]);

            var grpCreatureSpecies = new GroupBox
            {
                Text = "Creature Race (Uncheck to exclude)",
                Location = new Point(505, 14),
                Size = new Size(321, 200)
            };

            _clbCreatureSpecies = new CheckedListBox
            {
                Location = new Point(12, 22),
                Size = new Size(297, 136),
                CheckOnClick = true
            };
            _clbCreatureSpecies.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbCreatureSpecies, "Drafts Standard & Dual-Mode creatures proportionately across checked species (variety-first).");

            var btnAllCrSpecies = new Button { Text = "All", Location = new Point(12, 166), Size = new Size(65, 24) };
            btnAllCrSpecies.Click += (s, e) =>
            {
                SetAllChecked(_clbCreatureSpecies, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearCrSpecies = new Button { Text = "Clear", Location = new Point(83, 166), Size = new Size(65, 24) };
            btnClearCrSpecies.Click += (s, e) =>
            {
                SetAllChecked(_clbCreatureSpecies, false);
                ValidateWorkbenchReadiness();
            };

            grpCreatureSpecies.Controls.AddRange([_clbCreatureSpecies, btnAllCrSpecies, btnClearCrSpecies]);

            var grpCreatureKeying = new GroupBox
            {
                Text = "Keying Filters (Unknown Opponent Only)",
                Location = new Point(505, 222),
                Size = new Size(321, 226)
            };

            var lblCrRegTypes = new Label { Text = "Region Terrains:", Location = new Point(12, 22), Size = new Size(144, 16) };
            _clbCreatureRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 40),
                Size = new Size(144, 142),
                CheckOnClick = true
            };
            _clbCreatureRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);
            SetAllChecked(_clbCreatureRegionTypes, true);
            _clbCreatureRegionTypes.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };

            var lblCrSiteTypes = new Label { Text = "Site Holds:", Location = new Point(165, 22), Size = new Size(144, 16) };
            _clbCreatureSiteTypes = new CheckedListBox
            {
                Location = new Point(165, 40),
                Size = new Size(144, 142),
                CheckOnClick = true
            };
            _clbCreatureSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold"]);
            SetAllChecked(_clbCreatureSiteTypes, true);
            _clbCreatureSiteTypes.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };

            _btnAllCreatureKeying = new Button { Text = "All", Location = new Point(12, 190), Size = new Size(65, 24) };
            _btnAllCreatureKeying.Click += (s, e) =>
            {
                SetAllChecked(_clbCreatureRegionTypes, true);
                SetAllChecked(_clbCreatureSiteTypes, true);
                ValidateWorkbenchReadiness();
            };

            _btnClearCreatureKeying = new Button { Text = "Clear", Location = new Point(83, 190), Size = new Size(65, 24) };
            _btnClearCreatureKeying.Click += (s, e) =>
            {
                SetAllChecked(_clbCreatureRegionTypes, false);
                SetAllChecked(_clbCreatureSiteTypes, false);
                ValidateWorkbenchReadiness();
            };

            grpCreatureKeying.Controls.AddRange([
                lblCrRegTypes, _clbCreatureRegionTypes,
                lblCrSiteTypes, _clbCreatureSiteTypes,
                _btnAllCreatureKeying, _btnClearCreatureKeying
            ]);

            _lblCreatureKeyingNote = new Label
            {
                Location = new Point(505, 456),
                Size = new Size(321, 60),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false
            };

            GroupBox grpCreaturesStatus;
            (grpCreaturesStatus, _rtbCreaturesStatus) = CreateStatusGroupBox(16, 316, 475, 346, CreaturesIdleStatusText);

            tabCreatures.Controls.AddRange([
                grpNazgul, grpCreatures,
                grpCreatureSpecies, grpCreatureKeying, _lblCreatureKeyingNote,
                grpCreaturesStatus
            ]);

            return tabCreatures;
        }

        private Panel BuildHazardEventsPage()
        {
            var tabHazardEvents = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpHazEnv = new GroupBox
            {
                Text = "Environment (Hazards && Sideboard)",
                Location = new Point(16, 14),
                Size = new Size(475, 148)
            };

            var lblHazEnvHdrCard = new Label { Text = "Card", Location = new Point(16, 22), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHazEnvHdrHaz = new Label { Text = "Hazards", Location = new Point(190, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHazEnvHdrSide = new Label { Text = "Sideboard", Location = new Point(282, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblDon = new Label { Text = "Doors of Night:", Location = new Point(16, 51), Size = new Size(165, 20) };
            _numHazDoorsOfNight = CreateItemSpinner(190, 48, 0, 3, 0);
            _numSideDoorsOfNight = CreateItemSpinner(282, 48, 0, 3, 0);
            _toolTip.SetToolTip(_numHazDoorsOfNight, "Permanent-event (up to 3 total across Hazards + Sideboard).");
            _toolTip.SetToolTip(_numSideDoorsOfNight, "Permanent-event (up to 3 total across Hazards + Sideboard).");

            var lblHazTwi = new Label { Text = "Twilight:", Location = new Point(16, 83), Size = new Size(165, 20) };
            _numHazTwilight = CreateItemSpinner(190, 80, 0, 3, 0);
            _numSideHazTwilight = CreateItemSpinner(282, 80, 0, 3, 0);
            _toolTip.SetToolTip(_numHazTwilight, "Dual-purpose Resource/Hazard Short-event (shares 3-copy limit across Resources, Hazards & Sideboard).");
            _toolTip.SetToolTip(_numSideHazTwilight, "Dual-purpose Resource/Hazard Short-event (shares 3-copy limit across Resources, Hazards & Sideboard).");

            var lblHazEnvNote = new Label
            {
                Location = new Point(16, 112),
                Size = new Size(445, 30),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text = "Twilight shares its 3-copy limit across Resources, Hazards & Sideboard."
            };

            grpHazEnv.Controls.AddRange([
                lblHazEnvHdrCard, lblHazEnvHdrHaz, lblHazEnvHdrSide,
                lblDon, _numHazDoorsOfNight, _numSideDoorsOfNight,
                lblHazTwi, _numHazTwilight, _numSideHazTwilight,
                lblHazEnvNote
            ]);

            var grpHazAutoFill = new GroupBox
            {
                Text = "Hazard Events Auto-Fill && Sideboard",
                Location = new Point(16, 170),
                Size = new Size(475, 140)
            };

            var lblHazAutoPlayDeck = new Label
            {
                Location = new Point(16, 24),
                Size = new Size(445, 20),
                UseMnemonic = false,
                Text = "Play Deck (Hazards):   Auto-fills all remaining slots up to Play Deck Size."
            };

            var lblSideHazAuto = new Label
            {
                Text = "Additional Events in Sideboard:",
                Location = new Point(16, 54),
                Size = new Size(205, 20)
            };
            _numSideboardHazardEvents = CreateItemSpinner(228, 51, 0, 20, 4);
            _toolTip.SetToolTip(
                _numSideboardHazardEvents,
                "Number of general Hazard Events to draft into Sideboard (beyond Sideboard Doors of Night / Twilight).");

            var lblHazAutoNote = new Label
            {
                Location = new Point(16, 82),
                Size = new Size(445, 50),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text =
                    "• Distributes slots proportionately by eligible cards per checked Role (min 1 each if slots permit).\n" +
                    "• Excludes Doors of Night, Twilight, Dual-Mode Creatures & Nazgûl Support.\n" +
                    "• Duration (The Will of Sauron) is held last and requires >= 1 Hazard Long-event."
            };

            grpHazAutoFill.Controls.AddRange([lblHazAutoPlayDeck, lblSideHazAuto, _numSideboardHazardEvents, lblHazAutoNote]);

            var grpHazEventRoles = new GroupBox
            {
                Text = "Role (Uncheck to exclude)",
                Location = new Point(505, 14),
                Size = new Size(321, 200)
            };

            _clbHazardEventRoles = new CheckedListBox
            {
                Location = new Point(12, 22),
                Size = new Size(297, 136),
                CheckOnClick = true
            };
            _clbHazardEventRoles.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };
            _toolTip.SetToolTip(_clbHazardEventRoles, "Auto-filled Hazard Events must carry at least one checked Role tag.");

            var btnAllHazRoles = new Button { Text = "All", Location = new Point(12, 166), Size = new Size(65, 24) };
            btnAllHazRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbHazardEventRoles, true);
                ValidateWorkbenchReadiness();
            };

            var btnClearHazRoles = new Button { Text = "Clear", Location = new Point(83, 166), Size = new Size(65, 24) };
            btnClearHazRoles.Click += (s, e) =>
            {
                SetAllChecked(_clbHazardEventRoles, false);
                ValidateWorkbenchReadiness();
            };

            grpHazEventRoles.Controls.AddRange([_clbHazardEventRoles, btnAllHazRoles, btnClearHazRoles]);

            var grpHazEventKeying = new GroupBox
            {
                Text = "Keying Filters (Unknown Opponent Only)",
                Location = new Point(505, 222),
                Size = new Size(321, 226)
            };

            var lblHazRegTypes = new Label { Text = "Region Terrains:", Location = new Point(12, 22), Size = new Size(144, 16) };
            _clbHazardEventRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 40),
                Size = new Size(144, 142),
                CheckOnClick = true
            };
            _clbHazardEventRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);
            SetAllChecked(_clbHazardEventRegionTypes, true);
            _clbHazardEventRegionTypes.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };

            var lblHazSiteTypes = new Label { Text = "Site Holds:", Location = new Point(165, 22), Size = new Size(144, 16) };
            _clbHazardEventSiteTypes = new CheckedListBox
            {
                Location = new Point(165, 40),
                Size = new Size(144, 142),
                CheckOnClick = true
            };
            _clbHazardEventSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold"]);
            SetAllChecked(_clbHazardEventSiteTypes, true);
            _clbHazardEventSiteTypes.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(ValidateWorkbenchReadiness);
                }
            };

            _btnAllHazardEventKeying = new Button { Text = "All", Location = new Point(12, 190), Size = new Size(65, 24) };
            _btnAllHazardEventKeying.Click += (s, e) =>
            {
                SetAllChecked(_clbHazardEventRegionTypes, true);
                SetAllChecked(_clbHazardEventSiteTypes, true);
                ValidateWorkbenchReadiness();
            };

            _btnClearHazardEventKeying = new Button { Text = "Clear", Location = new Point(83, 190), Size = new Size(65, 24) };
            _btnClearHazardEventKeying.Click += (s, e) =>
            {
                SetAllChecked(_clbHazardEventRegionTypes, false);
                SetAllChecked(_clbHazardEventSiteTypes, false);
                ValidateWorkbenchReadiness();
            };

            grpHazEventKeying.Controls.AddRange([
                lblHazRegTypes, _clbHazardEventRegionTypes,
                lblHazSiteTypes, _clbHazardEventSiteTypes,
                _btnAllHazardEventKeying, _btnClearHazardEventKeying
            ]);

            _lblHazardEventKeyingNote = new Label
            {
                Location = new Point(505, 456),
                Size = new Size(321, 60),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false
            };

            GroupBox grpHazardEventsStatus;
            (grpHazardEventsStatus, _rtbHazardEventsStatus) = CreateStatusGroupBox(16, 320, 475, 342, HazardEventsIdleStatusText);

            tabHazardEvents.Controls.AddRange([
                grpHazEnv, grpHazAutoFill,
                grpHazEventRoles, grpHazEventKeying, _lblHazardEventKeyingNote,
                grpHazardEventsStatus
            ]);

            return tabHazardEvents;
        }

        private bool IsEligibleBaseResourceEvent(Card card)
        {
            if (card == null) return false;
            if (!string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsHeroPlayableAlignment(card.Alignment)) return false;
            if (card.Secondary == null || !card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase)) return false;

            // Environment & earlier dedicated tabs handle these explicitly
            if (CardFilterService.EqualsWithDiacriticFallback(card.Name, "Gates of Morning") ||
                CardFilterService.EqualsWithDiacriticFallback(card.Name, "Twilight"))
            {
                return false;
            }

            if (HasRoleTag(card, "Quest")) return false;
            if (HasSpecialAbility(card, "Gold Ring: Allow Test")) return false;
            if (HasSpecialAbility(card, "Palantír: Allow Use")) return false;

            return true;
        }

        private void PopulateResourceEventRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbResourceEventRoles.Items.Count; i++)
            {
                if (!_clbResourceEventRoles.GetItemChecked(i))
                {
                    string role = _clbResourceEventRoles.Items[i]?.ToString();
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
                if (!IsEligibleBaseResourceEvent(card)) continue;

                foreach (var role in _filterService.GetCardTagsForCategory(card.Id, "Role"))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }

            var sortedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            _clbResourceEventRoles.BeginUpdate();
            _clbResourceEventRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbResourceEventRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbResourceEventRoles.EndUpdate();
        }

        private void PopulateCreatureSpeciesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbCreatureSpecies.Items.Count; i++)
            {
                if (!_clbCreatureSpecies.GetItemChecked(i))
                {
                    string sp = _clbCreatureSpecies.Items[i]?.ToString();
                    if (!string.IsNullOrWhiteSpace(sp))
                    {
                        previouslyExcluded.Add(sp);
                    }
                }
            }

            var activeSets = _getActiveSets();
            var species = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in _catalog.Cards)
            {
                if (activeSets.Count > 0 && !activeSets.Contains(card.Set)) continue;
                if (!IsHeroPlayableAlignment(card.Alignment)) continue;
                if (!IsStandardCreatureCard(card) && !IsDualModeNonNazgulCreature(card)) continue;

                if (string.IsNullOrWhiteSpace(card.Race)) continue;
                foreach (var part in card.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        species.Add(part.Trim());
                    }
                }
            }

            var sortedSpecies = species.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            _clbCreatureSpecies.BeginUpdate();
            _clbCreatureSpecies.Items.Clear();
            foreach (var sp in sortedSpecies)
            {
                _clbCreatureSpecies.Items.Add(sp, !previouslyExcluded.Contains(sp));
            }
            _clbCreatureSpecies.EndUpdate();
        }

        private bool IsEligibleBaseHazardEvent(Card card)
        {
            if (card == null) return false;
            if (!string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsHeroPlayableAlignment(card.Alignment)) return false;
            if (card.Secondary == null || !card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase)) return false;

            if (IsCreaturesTabCard(card)) return false;

            if (CardFilterService.EqualsWithDiacriticFallback(card.Name, "Doors of Night") ||
                CardFilterService.EqualsWithDiacriticFallback(card.Name, "Twilight"))
            {
                return false;
            }

            return true;
        }

        private void PopulateHazardEventRolesChecklist()
        {
            var previouslyExcluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbHazardEventRoles.Items.Count; i++)
            {
                if (!_clbHazardEventRoles.GetItemChecked(i))
                {
                    string role = _clbHazardEventRoles.Items[i]?.ToString();
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
                if (!IsEligibleBaseHazardEvent(card)) continue;

                foreach (var role in _filterService.GetCardTagsForCategory(card.Id, "Role"))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role.Trim());
                    }
                }
            }

            var sortedRoles = roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

            _clbHazardEventRoles.BeginUpdate();
            _clbHazardEventRoles.Items.Clear();
            foreach (var role in sortedRoles)
            {
                _clbHazardEventRoles.Items.Add(role, !previouslyExcluded.Contains(role));
            }
            _clbHazardEventRoles.EndUpdate();
        }

        private void UpdateHazardKeyingEnabledState()
        {
            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;

            if (_clbCreatureRegionTypes != null && _clbCreatureSiteTypes != null && _lblCreatureKeyingNote != null)
            {
                _clbCreatureRegionTypes.Enabled = !tailorToDeck;
                _clbCreatureSiteTypes.Enabled = !tailorToDeck;
                _btnAllCreatureKeying.Enabled = !tailorToDeck;
                _btnClearCreatureKeying.Enabled = !tailorToDeck;

                _lblCreatureKeyingNote.Text = tailorToDeck
                    ? "Keying Filters disabled: 'Tailor Hazards to current deck' is enabled on General Settings, so Creatures automatically key to drafted Sites && Regions."
                    : "Unknown Opponent mode: Creatures pass if any Region Requirement terrain or Site Requirement hold is checked above.";
            }

            if (_clbHazardEventRegionTypes != null && _clbHazardEventSiteTypes != null && _lblHazardEventKeyingNote != null)
            {
                _clbHazardEventRegionTypes.Enabled = !tailorToDeck;
                _clbHazardEventSiteTypes.Enabled = !tailorToDeck;
                _btnAllHazardEventKeying.Enabled = !tailorToDeck;
                _btnClearHazardEventKeying.Enabled = !tailorToDeck;

                _lblHazardEventKeyingNote.Text = tailorToDeck
                    ? "Keying Filters disabled: 'Tailor Hazards to current deck' is enabled on General Settings, so Hazard Events target drafted Sites, Regions && Resources."
                    : "Unknown Opponent mode: Hazard Events with Region/Site Requirements pass if any terrain or hold is checked above.";
            }
        }

        private void UpdateCreatureEquivalentsReadout()
        {
            if (_lblCreatureEquivReadout == null || _numPlayDeckSize == null) return;

            int hazStd = (int)_numHazStandardCreatures.Value;
            int hazDual = (int)_numHazDualCreatures.Value;
            int hazNaz = (int)_numHazNazgul.Value;
            int hazSup = (int)_numHazNazgulSupport.Value;

            int equiv = hazStd + ((hazNaz + hazDual) / 2);
            int totalHazCards = hazStd + hazDual + hazNaz + hazSup;
            int playSize = (int)_numPlayDeckSize.Value;
            int remainingForEvents = Math.Max(0, playSize - totalHazCards);

            string minWarning = equiv < 12 ? "  [Below 12 min!]" : "  [Min: 12]";
            _lblCreatureEquivReadout.ForeColor = equiv < 12 ? Color.FromArgb(180, 83, 9) : Color.FromArgb(26, 54, 93);
            _lblCreatureEquivReadout.Text =
                $"Creature Equivalents in Hazards: {equiv}  ({hazStd} Std + ({hazDual} Dual + {hazNaz} Nazgûl)/2){minWarning}\n" +
                $"Hazard Play Deck: {totalHazCards} / {playSize} used ({remainingForEvents} remaining for Hazard Events)";
        }

        private bool RunResourceEventsDraft(bool silent = false)
        {
            int reqResGom = (int)_numResGatesOfMorning.Value;
            int reqSideGom = (int)_numSideGatesOfMorning.Value;
            int reqResTwi = (int)_numResTwilight.Value;
            int reqSideTwi = (int)_numSideTwilight.Value;
            int reqSideAuto = (int)_numSideboardResourceEvents.Value;

            if (reqResGom + reqSideGom > 3)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Gates of Morning is non-unique: combined Resources + Sideboard count cannot exceed 3.", "Resource Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (reqResTwi + reqSideTwi > 3)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Twilight is non-unique: combined Resources + Sideboard count cannot exceed 3.", "Resource Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            int resAutoTarget = Math.Max(0, resCapacity - reqResGom - reqResTwi);
            int sideAutoTarget = Math.Min(reqSideAuto, Math.Max(0, sideCapacity - reqSideGom - reqSideTwi));

            var checkedRoles = new HashSet<string>(_clbResourceEventRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if ((resAutoTarget > 0 || reqSideAuto > 0) && _clbResourceEventRoles.Items.Count > 0 && checkedRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Role under Role on the Resource Events tab.", "Resource Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                .Concat(_deck.Hazards)
                .Concat(_deck.Sideboard)
                .ToList();

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in allDeckNonSiteCards)
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            // 1. Draft Environment cards first (Gates of Morning, Twilight)
            Card gomCard = FindHeroPreferredCard(allCards, activeSets, "Resource", "Gates of Morning");
            Card twilightCard = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, "Twilight"))
                .OrderBy(c => string.Equals(c.Alignment, "Neutral", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();

            List<Card> DraftFixedCopies(Card card, List<Card> targetSection, int requested, ref int cap)
            {
                var added = new List<Card>();
                if (card == null || requested <= 0 || cap <= 0) return added;

                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                int cur = usedCountsByName.GetValueOrDefault(card.Name, 0);

                while (added.Count < requested && cap > 0 && cur < max)
                {
                    targetSection.Add(card);
                    cur++;
                    usedCountsByName[card.Name] = cur;
                    added.Add(card);
                    cap--;
                }
                return added;
            }

            var draftedResGom = DraftFixedCopies(gomCard, _deck.Resources, reqResGom, ref resCapacity);
            var draftedResTwi = DraftFixedCopies(twilightCard, _deck.Resources, reqResTwi, ref resCapacity);
            var draftedSideGom = DraftFixedCopies(gomCard, _deck.Sideboard, reqSideGom, ref sideCapacity);
            var draftedSideTwi = DraftFixedCopies(twilightCard, _deck.Sideboard, reqSideTwi, ref sideCapacity);

            // 2. Build eligible general Resource Events pool
            bool PassesResourceEventGuards(Card evt)
            {
                var roleTags = _filterService.GetCardTagsForCategory(evt.Id, "Role");
                if (roleTags.Count == 0 || !roleTags.Any(r => checkedRoles.Contains(r)))
                {
                    return false;
                }

                if (!MatchesItemRegionRequirement(evt, draftedRegionNames, draftedRegionTypes))
                {
                    return false;
                }

                var siteReqs = _filterService.GetCardTagsForCategory(evt.Id, "Site Requirement");
                if (siteReqs.Count > 0 && !siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(evt.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, evt.Specific)))
                {
                    return false;
                }

                if (!SatisfiesExistingPrerequisites(evt, _deck.Pool.Concat(_deck.Resources).Concat(_deck.Sideboard).ToList()))
                {
                    return false;
                }

                if (!MatchesSkillOrRaceRequirements(evt, allDraftedChars))
                {
                    return false;
                }

                if (!MatchesItemTargetSpecies(evt, allDraftedChars))
                {
                    return false;
                }

                return true;
            }

            var eligiblePool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    IsEligibleBaseResourceEvent(c))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null && PassesResourceEventGuards(c))
                .ToList();

            // 3. Auto-fill remaining Resources to Play Deck Size, then requested Sideboard count
            var (draftedResAuto, resRoleSummary) = DraftEventsProportionately(
                eligiblePool, checkedRoles, _deck.Resources, resCapacity, usedCountsByName);

            int sideFillCount = Math.Min(reqSideAuto, sideCapacity);
            var (draftedSideAuto, _) = DraftEventsProportionately(
                eligiblePool, checkedRoles, _deck.Sideboard, sideFillCount, usedCountsByName);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Resources);
            }

            // 4. Status & Shortfall Reporting
            var resAll = draftedResGom.Concat(draftedResTwi).Concat(draftedResAuto).ToList();
            var sideAll = draftedSideGom.Concat(draftedSideTwi).Concat(draftedSideAuto).ToList();

            string resText = resAll.Count > 0 ? FormatCardCountList(resAll) : "(None)";
            string sideText = sideAll.Count > 0 ? FormatCardCountList(sideAll) : "(None)";

            var bullets = new List<(string Label, string Value)>
            {
                ("Resources:", resText),
                ("Sideboard:", sideText)
            };

            if (!string.IsNullOrWhiteSpace(resRoleSummary))
            {
                bullets.Add(("Resources Role Allocation:", resRoleSummary));
            }

            var warnings = new List<string>();
            if (draftedResGom.Count < reqResGom) warnings.Add($"Resources Gates of Morning Shortfall: Drafted {draftedResGom.Count} of {reqResGom}.");
            if (draftedSideGom.Count < reqSideGom) warnings.Add($"Sideboard Gates of Morning Shortfall: Drafted {draftedSideGom.Count} of {reqSideGom}.");
            if (draftedResTwi.Count < reqResTwi) warnings.Add($"Resources Twilight Shortfall: Drafted {draftedResTwi.Count} of {reqResTwi}.");
            if (draftedSideTwi.Count < reqSideTwi) warnings.Add($"Sideboard Twilight Shortfall: Drafted {draftedSideTwi.Count} of {reqSideTwi}.");
            if (draftedResAuto.Count < resAutoTarget)
            {
                warnings.Add($"Play Deck Capacity Shortfall: Needed {resAutoTarget} auto-fill event(s), only {draftedResAuto.Count} eligible copy/copies available.");
            }
            if (draftedSideAuto.Count < reqSideAuto)
            {
                warnings.Add($"Sideboard Events Shortfall: Drafted {draftedSideAuto.Count} of {reqSideAuto} requested.");
            }

            SetRichBulletedStatus(
                _rtbResourceEventsStatus,
                $"Drafted {resAll.Count} event(s) into Resources and {sideAll.Count} into Sideboard:",
                [bullets],
                warnings);
            return true;
        }

        private (List<Card> Drafted, string RoleSummary) DraftEventsProportionately(
            List<Card> eligiblePool,
            HashSet<string> checkedRoles,
            List<Card> targetSection,
            int slotsToFill,
            Dictionary<string, int> usedCountsByName,
            Func<string, int> rolePriorityOrder = null,
            Func<Card, bool> runtimeCardGuard = null)
        {
            var drafted = new List<Card>();
            if (slotsToFill <= 0 || eligiblePool.Count == 0 || checkedRoles.Count == 0)
            {
                return (drafted, string.Empty);
            }

            bool CanAddCopy(Card card)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            var buckets = new List<(string Role, List<Card> Cards, int Quota)>();
            foreach (var role in checkedRoles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
            {
                var cardsInRole = eligiblePool
                    .Where(c => HasRoleTag(c, role) && CanAddCopy(c))
                    .ToList();

                if (cardsInRole.Count > 0)
                {
                    buckets.Add((role, cardsInRole, 0));
                }
            }

            if (buckets.Count == 0)
            {
                return (drafted, string.Empty);
            }

            // Allocate quotas: min 1 per eligible role if slots permit; otherwise weighted random subset
            if (slotsToFill < buckets.Count)
            {
                var remainingIndices = Enumerable.Range(0, buckets.Count).ToList();
                for (int pick = 0; pick < slotsToFill && remainingIndices.Count > 0; pick++)
                {
                    int totalW = remainingIndices.Sum(idx => buckets[idx].Cards.Count);
                    int roll = Random.Shared.Next(totalW);
                    int cum = 0;
                    int chosenListPos = 0;
                    for (int i = 0; i < remainingIndices.Count; i++)
                    {
                        cum += buckets[remainingIndices[i]].Cards.Count;
                        if (roll < cum)
                        {
                            chosenListPos = i;
                            break;
                        }
                    }

                    int bIdx = remainingIndices[chosenListPos];
                    buckets[bIdx] = (buckets[bIdx].Role, buckets[bIdx].Cards, 1);
                    remainingIndices.RemoveAt(chosenListPos);
                }
            }
            else
            {
                int totalWeight = buckets.Sum(b => b.Cards.Count);
                int remaining = slotsToFill - buckets.Count;

                var extras = new double[buckets.Count];
                for (int i = 0; i < buckets.Count; i++)
                {
                    double ideal = (double)slotsToFill * buckets[i].Cards.Count / totalWeight;
                    extras[i] = Math.Max(0.0, ideal - 1.0);
                }

                double sumExtra = extras.Sum();
                var intQuotas = new int[buckets.Count];
                var remainders = new (int Index, double Rem, int Tie)[buckets.Count];
                int assignedExtra = 0;

                for (int i = 0; i < buckets.Count; i++)
                {
                    double scaled = sumExtra > 0 ? (extras[i] * remaining / sumExtra) : ((double)remaining / buckets.Count);
                    int floored = (int)Math.Floor(scaled);
                    intQuotas[i] = 1 + floored;
                    assignedExtra += floored;
                    remainders[i] = (i, scaled - floored, Random.Shared.Next());
                }

                int leftover = remaining - assignedExtra;
                foreach (var item in remainders.OrderByDescending(r => r.Rem).ThenBy(r => r.Tie).Take(leftover))
                {
                    intQuotas[item.Index]++;
                }

                for (int i = 0; i < buckets.Count; i++)
                {
                    buckets[i] = (buckets[i].Role, buckets[i].Cards, intQuotas[i]);
                }
            }

            var countsPerRole = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Pick narrowest buckets first (after any caller priority ordering, e.g. Duration last)
            foreach (var (Role, Cards, Quota) in buckets
                .Where(b => b.Quota > 0)
                .OrderBy(b => rolePriorityOrder?.Invoke(b.Role) ?? 0)
                .ThenBy(b => b.Cards.Count)
                .ThenBy(_ => Random.Shared.Next()))
            {
                int addedInBucket = 0;
                while (addedInBucket < Quota && drafted.Count < slotsToFill)
                {
                    var available = Cards.Where(c => CanAddCopy(c) && (runtimeCardGuard == null || runtimeCardGuard(c))).ToList();
                    if (available.Count == 0) break;

                    int minCopyCount = available.Min(c => usedCountsByName.GetValueOrDefault(c.Name, 0));
                    var tierCandidates = available
                        .Where(c => usedCountsByName.GetValueOrDefault(c.Name, 0) == minCopyCount)
                        .ToList();

                    var chosen = tierCandidates[Random.Shared.Next(tierCandidates.Count)];
                    targetSection.Add(chosen);
                    usedCountsByName[chosen.Name] = usedCountsByName.GetValueOrDefault(chosen.Name, 0) + 1;
                    drafted.Add(chosen);
                    addedInBucket++;
                    countsPerRole[Role] = countsPerRole.GetValueOrDefault(Role, 0) + 1;
                }
            }

            // Fallback top-up if any narrow bucket hit copy-limit exhaustion
            while (drafted.Count < slotsToFill)
            {
                var available = eligiblePool.Where(c => CanAddCopy(c) && (runtimeCardGuard == null || runtimeCardGuard(c))).ToList();
                if (available.Count == 0) break;

                int minCopyCount = available.Min(c => usedCountsByName.GetValueOrDefault(c.Name, 0));
                var tierCandidates = available
                    .Where(c => usedCountsByName.GetValueOrDefault(c.Name, 0) == minCopyCount)
                    .ToList();

                var chosen = tierCandidates[Random.Shared.Next(tierCandidates.Count)];
                targetSection.Add(chosen);
                usedCountsByName[chosen.Name] = usedCountsByName.GetValueOrDefault(chosen.Name, 0) + 1;
                drafted.Add(chosen);
            }

            string summary = string.Join(", ", countsPerRole
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            return (drafted, summary);
        }

        private bool RunCreaturesDraft(bool silent = false)
        {
            int reqHazNazgul = (int)_numHazNazgul.Value;
            int reqSideNazgul = (int)_numSideNazgul.Value;
            int reqHazNazSup = (int)_numHazNazgulSupport.Value;
            int reqSideNazSup = (int)_numSideNazgulSupport.Value;
            int reqHazStd = (int)_numHazStandardCreatures.Value;
            int reqSideStd = (int)_numSideStandardCreatures.Value;
            int reqHazDual = (int)_numHazDualCreatures.Value;
            int reqSideDual = (int)_numSideDualCreatures.Value;

            int totalRequested =
                reqHazNazgul + reqSideNazgul +
                reqHazNazSup + reqSideNazSup +
                reqHazStd + reqSideStd +
                reqHazDual + reqSideDual;

            if (totalRequested == 0)
            {
                SetRichPlainStatus(_rtbCreaturesStatus, "No creatures or Nazgûl configured (0 drafted).", Color.FromArgb(74, 85, 104), FontStyle.Italic);
                return true;
            }

            if (reqHazNazgul + reqSideNazgul > 9)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Nazgûl are Unique: combined Hazards + Sideboard count cannot exceed 9.", "Creatures", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;
            int totalNonNazgulRequested = reqHazStd + reqSideStd + reqHazDual + reqSideDual;

            var checkedSpecies = new HashSet<string>(_clbCreatureSpecies.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if (totalNonNazgulRequested > 0 && _clbCreatureSpecies.Items.Count > 0 && checkedSpecies.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Creature Race under Creature Race on the Creatures tab.", "Creatures", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var checkedRegTypes = new HashSet<string>(_clbCreatureRegionTypes.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            var checkedSiteTypes = new HashSet<string>(_clbCreatureSiteTypes.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);

            if (!tailorToDeck && totalNonNazgulRequested > 0 && checkedRegTypes.Count == 0 && checkedSiteTypes.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Region Terrain or Site Hold under Keying Filters.", "Creatures", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedSites = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tailorToDeck && draftedRegions.Count == 0 && draftedSites.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "'Tailor Hazards to current deck' requires Regions/Sites in the Site deck (run Regions & Sites first).",
                        "Creatures",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

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

            var (catalogRegionTypesByName, catalogSiteHoldByName) = BuildCatalogKeyingMaps();

            bool IsKeyableToTargetDeck(Card card)
            {
                var regReqs = _filterService.GetCardTagsForCategory(card.Id, "Region Requirement");
                foreach (var req in regReqs)
                {
                    string clean = req.Trim();
                    if (draftedRegions.Any(dr => CardFilterService.EqualsWithDiacriticFallback(dr, clean)) ||
                        draftedRegionTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, clean)))
                    {
                        return true;
                    }
                }

                var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
                foreach (var req in siteReqs)
                {
                    if (MatchesAnySiteRequirement(draftedSites, req, _filterService))
                    {
                        return true;
                    }
                }

                return false;
            }

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Pool.Concat(_deck.Resources).Concat(_deck.Hazards).Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int hazCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Hazards.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var heroDistinctHazards = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
                    IsHeroPlayableAlignment(c.Alignment))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null)
                .ToList();

            // 1. Nazgûl (in Target Current Deck mode, prioritize creature-keyable Nazgûl first, then permanent-event Nazgûl)
            var nazgulCandidates = heroDistinctHazards
                .Where(IsNazgulCard)
                .OrderBy(c => tailorToDeck && !IsKeyableToTargetDeck(c) ? 1 : 0)
                .ThenBy(_ => Random.Shared.Next())
                .ToList();

            var draftedHazNazgul = DraftItemsIntoSection(nazgulCandidates, _deck.Hazards, reqHazNazgul, hazCapacity, usedCountsByName);
            hazCapacity -= draftedHazNazgul.Count;
            var draftedSideNazgul = DraftItemsIntoSection(nazgulCandidates, _deck.Sideboard, reqSideNazgul, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideNazgul.Count;

            // 2. Nazgûl Support
            var nazSupCandidates = heroDistinctHazards
                .Where(IsNazgulSupportCard)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var draftedHazNazSup = DraftItemsIntoSection(nazSupCandidates, _deck.Hazards, reqHazNazSup, hazCapacity, usedCountsByName);
            hazCapacity -= draftedHazNazSup.Count;
            var draftedSideNazSup = DraftItemsIntoSection(nazSupCandidates, _deck.Sideboard, reqSideNazSup, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideNazSup.Count;

            // 3. Standard Creatures (1.0) & Dual-Mode Half-Creatures (0.5)
            bool PassesCreatureFilters(Card c)
            {
                if (string.IsNullOrWhiteSpace(c.Race)) return false;
                var races = c.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!races.Any(r => checkedSpecies.Contains(r))) return false;

                return tailorToDeck
                    ? IsKeyableToTargetDeck(c)
                    : PassesUnknownOpponentKeying(c, checkedRegTypes, checkedSiteTypes, catalogRegionTypesByName, catalogSiteHoldByName);
            }

            var standardPool = heroDistinctHazards
                .Where(c => IsStandardCreatureCard(c) && PassesCreatureFilters(c))
                .ToList();

            var dualPool = heroDistinctHazards
                .Where(c => IsDualModeNonNazgulCreature(c) && PassesCreatureFilters(c))
                .ToList();

            var (draftedHazStd, _) = DraftCreaturesProportionately(
                standardPool, checkedSpecies, _deck.Hazards, Math.Min(reqHazStd, hazCapacity), usedCountsByName);
            hazCapacity -= draftedHazStd.Count;

            var (draftedSideStd, _) = DraftCreaturesProportionately(
                standardPool, checkedSpecies, _deck.Sideboard, Math.Min(reqSideStd, sideCapacity), usedCountsByName);
            sideCapacity -= draftedSideStd.Count;

            var (draftedHazDual, _) = DraftCreaturesProportionately(
                dualPool, checkedSpecies, _deck.Hazards, Math.Min(reqHazDual, hazCapacity), usedCountsByName);

            var (draftedSideDual, _) = DraftCreaturesProportionately(
                dualPool, checkedSpecies, _deck.Sideboard, Math.Min(reqSideDual, sideCapacity), usedCountsByName);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Hazards);
            }

            // 4. Status & Shortfall Reporting
            var hazAll = draftedHazStd.Concat(draftedHazDual).Concat(draftedHazNazgul).Concat(draftedHazNazSup).ToList();
            var sideAll = draftedSideStd.Concat(draftedSideDual).Concat(draftedSideNazgul).Concat(draftedSideNazSup).ToList();

            int hazEquiv = draftedHazStd.Count + ((draftedHazNazgul.Count + draftedHazDual.Count) / 2);

            var hazBullets = new List<(string Label, string Value)>();
            var sideBullets = new List<(string Label, string Value)>();

            void AddBulletIfRelevant(List<(string Label, string Value)> target, string label, int requested, List<Card> drafted)
            {
                if (requested > 0 || drafted.Count > 0)
                {
                    target.Add((label, drafted.Count > 0 ? FormatCardCountList(drafted) : "(None)"));
                }
            }

            AddBulletIfRelevant(hazBullets, "Hazards — Nazgûl:", reqHazNazgul, draftedHazNazgul);
            AddBulletIfRelevant(hazBullets, "Hazards — Nazgûl Support:", reqHazNazSup, draftedHazNazSup);
            AddBulletIfRelevant(hazBullets, "Hazards — Standard:", reqHazStd, draftedHazStd);
            AddBulletIfRelevant(hazBullets, "Hazards — Dual-Mode:", reqHazDual, draftedHazDual);

            AddBulletIfRelevant(sideBullets, "Sideboard — Nazgûl:", reqSideNazgul, draftedSideNazgul);
            AddBulletIfRelevant(sideBullets, "Sideboard — Nazgûl Support:", reqSideNazSup, draftedSideNazSup);
            AddBulletIfRelevant(sideBullets, "Sideboard — Standard:", reqSideStd, draftedSideStd);
            AddBulletIfRelevant(sideBullets, "Sideboard — Dual-Mode:", reqSideDual, draftedSideDual);

            var warnings = new List<string>();
            if (draftedHazNazgul.Count < reqHazNazgul) warnings.Add($"Hazards Nazgûl Shortfall: Drafted {draftedHazNazgul.Count} of {reqHazNazgul}.");
            if (draftedSideNazgul.Count < reqSideNazgul) warnings.Add($"Sideboard Nazgûl Shortfall: Drafted {draftedSideNazgul.Count} of {reqSideNazgul}.");
            if (draftedHazNazSup.Count < reqHazNazSup) warnings.Add($"Hazards Nazgûl Support Shortfall: Drafted {draftedHazNazSup.Count} of {reqHazNazSup}.");
            if (draftedSideNazSup.Count < reqSideNazSup) warnings.Add($"Sideboard Nazgûl Support Shortfall: Drafted {draftedSideNazSup.Count} of {reqSideNazSup}.");
            if (draftedHazStd.Count < reqHazStd) warnings.Add($"Hazards Standard Creatures Shortfall: Drafted {draftedHazStd.Count} of {reqHazStd}.");
            if (draftedSideStd.Count < reqSideStd) warnings.Add($"Sideboard Standard Creatures Shortfall: Drafted {draftedSideStd.Count} of {reqSideStd}.");
            if (draftedHazDual.Count < reqHazDual) warnings.Add($"Hazards Dual-Mode Creatures Shortfall: Drafted {draftedHazDual.Count} of {reqHazDual}.");
            if (draftedSideDual.Count < reqSideDual) warnings.Add($"Sideboard Dual-Mode Creatures Shortfall: Drafted {draftedSideDual.Count} of {reqSideDual}.");

            if (draftedHazNazSup.Count + draftedSideNazSup.Count > 0 &&
                draftedHazNazgul.Count + draftedSideNazgul.Count == 0)
            {
                warnings.Add("Nazgûl Support card(s) drafted with 0 Nazgûl in Hazards/Sideboard.");
            }

            if (hazEquiv < 12 && (reqHazStd + reqHazNazgul + reqHazDual) > 0)
            {
                warnings.Add($"Hazards has {hazEquiv} creature equivalents (tournament minimum is 12).");
            }

            SetRichBulletedStatus(
                _rtbCreaturesStatus,
                $"Drafted {hazAll.Count} card(s) into Hazards (Creature Equiv: {hazEquiv}/12 min) and {sideAll.Count} into Sideboard:",
                [hazBullets, sideBullets],
                warnings);

            return true;
        }

        private static (List<Card> Drafted, string SpeciesSummary) DraftCreaturesProportionately(
            List<Card> eligiblePool,
            HashSet<string> checkedSpecies,
            List<Card> targetSection,
            int slotsToFill,
            Dictionary<string, int> usedCountsByName)
        {
            var drafted = new List<Card>();
            if (slotsToFill <= 0 || eligiblePool.Count == 0 || checkedSpecies.Count == 0)
            {
                return (drafted, string.Empty);
            }

            bool CanAddCopy(Card card)
            {
                int current = usedCountsByName.GetValueOrDefault(card.Name, 0);
                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                return current < max;
            }

            var buckets = new List<(string Species, List<Card> Cards, int Quota)>();
            foreach (var sp in checkedSpecies.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var cardsInSpecies = eligiblePool
                    .Where(c => HasRaceToken(c, sp) && CanAddCopy(c))
                    .ToList();

                if (cardsInSpecies.Count > 0)
                {
                    buckets.Add((sp, cardsInSpecies, 0));
                }
            }

            if (buckets.Count == 0)
            {
                return (drafted, string.Empty);
            }

            if (slotsToFill < buckets.Count)
            {
                var remainingIndices = Enumerable.Range(0, buckets.Count).ToList();
                for (int pick = 0; pick < slotsToFill && remainingIndices.Count > 0; pick++)
                {
                    int totalW = remainingIndices.Sum(idx => buckets[idx].Cards.Count);
                    int roll = Random.Shared.Next(totalW);
                    int cum = 0;
                    int chosenListPos = 0;
                    for (int i = 0; i < remainingIndices.Count; i++)
                    {
                        cum += buckets[remainingIndices[i]].Cards.Count;
                        if (roll < cum)
                        {
                            chosenListPos = i;
                            break;
                        }
                    }

                    int bIdx = remainingIndices[chosenListPos];
                    buckets[bIdx] = (buckets[bIdx].Species, buckets[bIdx].Cards, 1);
                    remainingIndices.RemoveAt(chosenListPos);
                }
            }
            else
            {
                int totalWeight = buckets.Sum(b => b.Cards.Count);
                int remaining = slotsToFill - buckets.Count;

                var extras = new double[buckets.Count];
                for (int i = 0; i < buckets.Count; i++)
                {
                    double ideal = (double)slotsToFill * buckets[i].Cards.Count / totalWeight;
                    extras[i] = Math.Max(0.0, ideal - 1.0);
                }

                double sumExtra = extras.Sum();
                var intQuotas = new int[buckets.Count];
                var remainders = new (int Index, double Rem, int Tie)[buckets.Count];
                int assignedExtra = 0;

                for (int i = 0; i < buckets.Count; i++)
                {
                    double scaled = sumExtra > 0 ? (extras[i] * remaining / sumExtra) : ((double)remaining / buckets.Count);
                    int floored = (int)Math.Floor(scaled);
                    intQuotas[i] = 1 + floored;
                    assignedExtra += floored;
                    remainders[i] = (i, scaled - floored, Random.Shared.Next());
                }

                int leftover = remaining - assignedExtra;
                foreach (var item in remainders.OrderByDescending(r => r.Rem).ThenBy(r => r.Tie).Take(leftover))
                {
                    intQuotas[item.Index]++;
                }

                for (int i = 0; i < buckets.Count; i++)
                {
                    buckets[i] = (buckets[i].Species, buckets[i].Cards, intQuotas[i]);
                }
            }

            var countsPerSpecies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (Species, Cards, Quota) in buckets.Where(b => b.Quota > 0).OrderBy(b => b.Cards.Count).ThenBy(_ => Random.Shared.Next()))
            {
                int addedInBucket = 0;
                while (addedInBucket < Quota && drafted.Count < slotsToFill)
                {
                    var available = Cards.Where(CanAddCopy).ToList();
                    if (available.Count == 0) break;

                    int minCopyCount = available.Min(c => usedCountsByName.GetValueOrDefault(c.Name, 0));
                    var tierCandidates = available
                        .Where(c => usedCountsByName.GetValueOrDefault(c.Name, 0) == minCopyCount)
                        .ToList();

                    var chosen = tierCandidates[Random.Shared.Next(tierCandidates.Count)];
                    targetSection.Add(chosen);
                    usedCountsByName[chosen.Name] = usedCountsByName.GetValueOrDefault(chosen.Name, 0) + 1;
                    drafted.Add(chosen);
                    addedInBucket++;
                    countsPerSpecies[Species] = countsPerSpecies.GetValueOrDefault(Species, 0) + 1;
                }
            }

            while (drafted.Count < slotsToFill)
            {
                var available = eligiblePool.Where(CanAddCopy).ToList();
                if (available.Count == 0) break;

                int minCopyCount = available.Min(c => usedCountsByName.GetValueOrDefault(c.Name, 0));
                var tierCandidates = available
                    .Where(c => usedCountsByName.GetValueOrDefault(c.Name, 0) == minCopyCount)
                    .ToList();

                var chosen = tierCandidates[Random.Shared.Next(tierCandidates.Count)];
                targetSection.Add(chosen);
                usedCountsByName[chosen.Name] = usedCountsByName.GetValueOrDefault(chosen.Name, 0) + 1;
                drafted.Add(chosen);
            }

            string summary = string.Join(", ", countsPerSpecies
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            return (drafted, summary);
        }

        private (Dictionary<string, HashSet<string>> RegionTypesByName, Dictionary<string, string> SiteHoldByName) BuildCatalogKeyingMaps()
        {
            var regionTypesByName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in _catalog.Cards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name)))
            {
                string rName = regCard.Name.Trim();
                if (!regionTypesByName.TryGetValue(rName, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    regionTypesByName[rName] = set;
                }
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, _catalog.Cards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt)) set.Add(rt.Trim());
                }
            }

            var siteHoldByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var siteCard in _catalog.Cards.Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Site)))
            {
                siteHoldByName.TryAdd(siteCard.Name.Trim(), siteCard.Site.Trim());
            }

            return (regionTypesByName, siteHoldByName);
        }

        private bool MatchesTargetDeckRegionOrSite(
            Card card,
            HashSet<string> draftedRegions,
            HashSet<string> draftedRegionTypes,
            List<Card> draftedSites,
            bool requireBothWhenBothPresent = false)
        {
            var regReqs = _filterService.GetCardTagsForCategory(card.Id, "Region Requirement");
            var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");

            if (regReqs.Count == 0 && siteReqs.Count == 0) return true;

            bool regMatch = regReqs.Any(req =>
            {
                string clean = req.Trim();
                return draftedRegions.Any(dr => CardFilterService.EqualsWithDiacriticFallback(dr, clean)) ||
                       draftedRegionTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, clean));
            });

            bool siteMatch = siteReqs.Any(req => MatchesAnySiteRequirement(draftedSites, req, _filterService));

            if (regReqs.Count > 0 && siteReqs.Count > 0)
            {
                return requireBothWhenBothPresent ? (regMatch && siteMatch) : (regMatch || siteMatch);
            }

            return regReqs.Count > 0 ? regMatch : siteMatch;
        }

        private bool PassesUnknownOpponentKeying(
            Card card,
            HashSet<string> checkedRegTypes,
            HashSet<string> checkedSiteTypes,
            Dictionary<string, HashSet<string>> catalogRegionTypesByName,
            Dictionary<string, string> catalogSiteHoldByName)
        {
            var regReqs = _filterService.GetCardTagsForCategory(card.Id, "Region Requirement");
            var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");

            if (regReqs.Count == 0 && siteReqs.Count == 0)
            {
                return true;
            }

            foreach (var req in regReqs)
            {
                string clean = req.Trim();
                if (checkedRegTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, clean)))
                {
                    return true;
                }

                if (catalogRegionTypesByName.TryGetValue(clean, out var mappedTerrains) &&
                    mappedTerrains.Any(mt => checkedRegTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, mt))))
                {
                    return true;
                }
            }

            foreach (var req in siteReqs)
            {
                string clean = req.Trim();
                string norm = CardFilterService.NormalizeSiteType(clean);
                if (CanonicalHoldTypes.Contains(norm) &&
                    checkedSiteTypes.Any(st => CardFilterService.MatchesSiteType(clean, st)))
                {
                    return true;
                }

                if (catalogSiteHoldByName.TryGetValue(clean, out string holdType) &&
                    checkedSiteTypes.Any(st => CardFilterService.MatchesSiteType(holdType, st)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RunHazardEventsDraft(bool silent = false)
        {
            int reqHazDon = (int)_numHazDoorsOfNight.Value;
            int reqSideDon = (int)_numSideDoorsOfNight.Value;
            int reqHazTwi = (int)_numHazTwilight.Value;
            int reqSideTwi = (int)_numSideHazTwilight.Value;
            int reqSideAuto = (int)_numSideboardHazardEvents.Value;

            if (reqHazDon + reqSideDon > 3)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Doors of Night is non-unique: combined Hazards + Sideboard count cannot exceed 3.", "Hazard Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (reqHazTwi + reqSideTwi > 3)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Twilight is non-unique: combined Hazards + Sideboard count cannot exceed 3.", "Hazard Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            int hazCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Hazards.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            int hazAutoTarget = Math.Max(0, hazCapacity - reqHazDon - reqHazTwi);
            int sideAutoTarget = Math.Min(reqSideAuto, Math.Max(0, sideCapacity - reqSideDon - reqSideTwi));

            var checkedRoles = new HashSet<string>(_clbHazardEventRoles.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            if ((hazAutoTarget > 0 || reqSideAuto > 0) && _clbHazardEventRoles.Items.Count > 0 && checkedRoles.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Role under Role on the Hazard Events tab.", "Hazard Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;
            var checkedRegTypes = new HashSet<string>(_clbHazardEventRegionTypes.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            var checkedSiteTypes = new HashSet<string>(_clbHazardEventSiteTypes.CheckedItems.Cast<string>(), StringComparer.OrdinalIgnoreCase);

            if (!tailorToDeck && (hazAutoTarget > 0 || reqSideAuto > 0) && checkedRegTypes.Count == 0 && checkedSiteTypes.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "Please select at least one Region Terrain or Site Hold under Keying Filters.", "Hazard Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var draftedSites = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tailorToDeck && draftedRegions.Count == 0 && draftedSites.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "'Tailor Hazards to current deck' requires Regions/Sites in the Site deck (run Regions & Sites first).",
                        "Hazard Events",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

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

            var (catalogRegionTypesByName, catalogSiteHoldByName) = BuildCatalogKeyingMaps();

            var allDraftedChars = _deck.Pool
                .Concat(_deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            bool hasDraftedRing = _deck.Resources.Concat(_deck.Sideboard)
                .Any(c => string.Equals(c.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase) || IsSpecialRingItem(c));

            bool hasDraftedPalantir = _deck.Resources.Concat(_deck.Sideboard)
                .Any(IsPalantirItem);

            var draftedCreatureSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cr in _deck.Hazards.Concat(_deck.Sideboard).Where(c => IsStandardCreatureCard(c) || IsDualModeNonNazgulCreature(c) || IsNazgulCard(c)))
            {
                if (string.IsNullOrWhiteSpace(cr.Race)) continue;
                foreach (var part in cr.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrWhiteSpace(part)) draftedCreatureSpecies.Add(part.Trim());
                }
            }

            var siteAutoAttackSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var site in draftedSites)
            {
                foreach (var aa in _filterService.GetCardTagsForCategory(site.Id, "Automatic Attack"))
                {
                    if (!string.IsNullOrWhiteSpace(aa)) siteAutoAttackSpecies.Add(aa.Trim());
                }
            }

            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Pool.Concat(_deck.Resources).Concat(_deck.Hazards).Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            // 1. Draft Environment cards first (Doors of Night, Twilight)
            Card donCard = FindHeroPreferredCard(allCards, activeSets, "Hazard", "Doors of Night");
            Card twilightCard = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, "Twilight"))
                .OrderBy(c => string.Equals(c.Alignment, "Neutral", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();

            List<Card> DraftFixedCopies(Card card, List<Card> targetSection, int requested, ref int cap)
            {
                var added = new List<Card>();
                if (card == null || requested <= 0 || cap <= 0) return added;

                int max = string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase) ? 1 : 3;
                int cur = usedCountsByName.GetValueOrDefault(card.Name, 0);

                while (added.Count < requested && cap > 0 && cur < max)
                {
                    targetSection.Add(card);
                    cur++;
                    usedCountsByName[card.Name] = cur;
                    added.Add(card);
                    cap--;
                }
                return added;
            }

            var draftedHazDon = DraftFixedCopies(donCard, _deck.Hazards, reqHazDon, ref hazCapacity);
            var draftedHazTwi = DraftFixedCopies(twilightCard, _deck.Hazards, reqHazTwi, ref hazCapacity);
            var draftedSideDon = DraftFixedCopies(donCard, _deck.Sideboard, reqSideDon, ref sideCapacity);
            var draftedSideTwi = DraftFixedCopies(twilightCard, _deck.Sideboard, reqSideTwi, ref sideCapacity);

            // 2. Build eligible general Hazard Events pool
            bool PassesHazardEventGuards(Card evt)
            {
                var roleTags = _filterService.GetCardTagsForCategory(evt.Id, "Role");
                if (roleTags.Count == 0 || !roleTags.Any(r => checkedRoles.Contains(r)))
                {
                    return false;
                }

                // Geography keying
                if (tailorToDeck)
                {
                    if (!MatchesTargetDeckRegionOrSite(evt, draftedRegions, draftedRegionTypes, draftedSites))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!PassesUnknownOpponentKeying(evt, checkedRegTypes, checkedSiteTypes, catalogRegionTypesByName, catalogSiteHoldByName))
                    {
                        return false;
                    }
                }

                // Target Item (Target Current Deck mode only)
                if (tailorToDeck)
                {
                    var itemTags = _filterService.GetCardTagsForCategory(evt.Id, "Target Item");
                    if (itemTags.Count > 0)
                    {
                        bool itemMatched = itemTags.Any(t =>
                            (CardFilterService.EqualsWithDiacriticFallback(t, "Ring") && hasDraftedRing) ||
                            (CardFilterService.EqualsWithDiacriticFallback(t, "Palantír") && hasDraftedPalantir));
                        if (!itemMatched) return false;
                    }
                }

                // Target Species (3-way: Predicates vs. Attack Escalation vs. Opposing Hero Race)
                var speciesTags = _filterService.GetCardTagsForCategory(evt.Id, "Target Species");
                if (speciesTags.Count > 0)
                {
                    bool isCombatEscalation = HasRoleTag(evt, "Combat");
                    bool speciesPassed = speciesTags.Any(tag =>
                    {
                        // Case 1: Predicate (e.g. Race!=Hobbit && Race!=Wizard)
                        if (CardFilterService.TryParsePredicate(tag, out _, out _, out _))
                        {
                            return !tailorToDeck || allDraftedChars.Any(ch => CardFilterService.MatchesPredicate(ch, tag, _filterService));
                        }

                        bool isHeroRace = HeroTargetSpecies.Any(hr => CardFilterService.EqualsWithDiacriticFallback(tag, hr));

                        // Case 2: Minion Attack Escalation
                        if (!isHeroRace || isCombatEscalation)
                        {
                            return draftedCreatureSpecies.Any(cs => CardFilterService.EqualsWithDiacriticFallback(cs, tag)) ||
                                   (tailorToDeck && siteAutoAttackSpecies.Any(aa => CardFilterService.EqualsWithDiacriticFallback(aa, tag)));
                        }

                        // Case 3: Opposing Hero Race Targeting (e.g. Elf, Hobbit)
                        return !tailorToDeck || allDraftedChars.Any(ch => CardFilterService.MatchesCardnumCriterion(ch, "Race", tag));
                    });

                    if (!speciesPassed) return false;
                }

                // Specific & Prerequisite (future-safe for later expansions)
                if (tailorToDeck && !string.IsNullOrWhiteSpace(evt.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, evt.Specific)))
                {
                    return false;
                }

                if (!SatisfiesExistingPrerequisites(evt, _deck.Hazards.Concat(_deck.Sideboard).ToList()))
                {
                    return false;
                }

                return true;
            }

            var eligiblePool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    IsEligibleBaseHazardEvent(c))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null && PassesHazardEventGuards(c))
                .ToList();

            // 3. Auto-fill remaining Hazards to Play Deck Size, then requested Sideboard count
            //    Hold "Duration" (The Will of Sauron) last and gate it on >= 1 Hazard Long-event.
            int DurationLastOrder(string role) =>
                string.Equals(role, "Duration", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            bool RuntimeHazardGuard(Card evt)
            {
                if (!HasRoleTag(evt, "Duration")) return true;

                return _deck.Hazards.Concat(_deck.Sideboard).Any(c =>
                    string.Equals(c.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Secondary, "Long-event", StringComparison.OrdinalIgnoreCase));
            }

            var (draftedHazAuto, hazRoleSummary) = DraftEventsProportionately(
                eligiblePool, checkedRoles, _deck.Hazards, hazCapacity, usedCountsByName, DurationLastOrder, RuntimeHazardGuard);

            int sideFillCount = Math.Min(reqSideAuto, sideCapacity);
            var (draftedSideAuto, _) = DraftEventsProportionately(
                eligiblePool, checkedRoles, _deck.Sideboard, sideFillCount, usedCountsByName, DurationLastOrder, RuntimeHazardGuard);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Hazards);
            }

            // 4. Status & Shortfall Reporting
            var hazAll = draftedHazDon.Concat(draftedHazTwi).Concat(draftedHazAuto).ToList();
            var sideAll = draftedSideDon.Concat(draftedSideTwi).Concat(draftedSideAuto).ToList();

            string hazText = hazAll.Count > 0 ? FormatCardCountList(hazAll) : "(None)";
            string sideText = sideAll.Count > 0 ? FormatCardCountList(sideAll) : "(None)";

            var bullets = new List<(string Label, string Value)>
            {
                ("Hazards:", hazText),
                ("Sideboard:", sideText)
            };

            if (!string.IsNullOrWhiteSpace(hazRoleSummary))
            {
                bullets.Add(("Hazards Role Allocation:", hazRoleSummary));
            }

            var warnings = new List<string>();
            if (draftedHazDon.Count < reqHazDon) warnings.Add($"Hazards Doors of Night Shortfall: Drafted {draftedHazDon.Count} of {reqHazDon}.");
            if (draftedSideDon.Count < reqSideDon) warnings.Add($"Sideboard Doors of Night Shortfall: Drafted {draftedSideDon.Count} of {reqSideDon}.");
            if (draftedHazTwi.Count < reqHazTwi) warnings.Add($"Hazards Twilight Shortfall: Drafted {draftedHazTwi.Count} of {reqHazTwi} (3-copy limit shared across deck).");
            if (draftedSideTwi.Count < reqSideTwi) warnings.Add($"Sideboard Twilight Shortfall: Drafted {draftedSideTwi.Count} of {reqSideTwi} (3-copy limit shared across deck).");
            if (draftedHazAuto.Count < hazAutoTarget)
            {
                warnings.Add($"Play Deck Capacity Shortfall: Needed {hazAutoTarget} auto-fill hazard event(s), only {draftedHazAuto.Count} eligible copy/copies available.");
            }
            if (draftedSideAuto.Count < reqSideAuto)
            {
                warnings.Add($"Sideboard Hazard Events Shortfall: Drafted {draftedSideAuto.Count} of {reqSideAuto} requested.");
            }

            SetRichBulletedStatus(
                _rtbHazardEventsStatus,
                $"Drafted {hazAll.Count} event(s) into Hazards and {sideAll.Count} into Sideboard:",
                [bullets],
                warnings);
            return true;
        }
    }
}
