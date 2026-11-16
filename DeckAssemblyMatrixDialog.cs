using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed class DeckAssemblyMatrixDialog : Form
    {
        private static readonly string[] HeroHavens = ["Rivendell", "Lórien", "Grey Havens", "Edhellond"];

        // Near top of DeckAssemblyMatrixDialog (line 16):
        private const string StarterIdleStatusText =
            "Total Mind limit: <= 20.\n" +
            "Starting characters prioritize: 1) drafted region group (including local Havens), 2) outside Havens, 3) any matching character.";

        private const string TheaterIdleStatusText =
            "Configure seed region, region/site counts, and terrain/hold filters, then click Run Pipeline on General Settings.";

        private const string FactionIdleStatusText =
            "Factions are strictly limited to those playable at Sites/Havens in the drafted Site deck.\n" +
            "Auto-drafted prerequisite cards (e.g. Paths of the Dead) are added alongside their faction if their own site requirements are met.";

        private const string AllyIdleStatusText =
            "Allies are strictly limited to those playable at Sites/Havens in the drafted Site deck.\n" +
            "Prioritizes allies playable in the drafted region group before outside Havens.";

        private readonly Deck _deck;
        private readonly CardCatalogService _catalog;
        private readonly CardFilterService _filterService;
        private readonly Func<ISet<string>> _getActiveSets;
        private readonly Action<DeckSectionType?> _notifyDeckCallback;

        // Whole-workbench session snapshot
        private sealed record DeckSnapshot(
            string Title,
            List<Card> Pool,
            List<Card> Resources,
            List<Card> Hazards,
            List<Card> Sideboard,
            List<Card> Sites);

        private DeckSnapshot _savedDeckSnapshot;

        // Tab & Pipeline infrastructure
        private readonly TabControl _tabControl;
        private sealed record RunnableTabInfo(Func<bool, bool> Runner, Label StatusLabel);
        private readonly Dictionary<string, RunnableTabInfo> _tabInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _pipelineOrder = ["Regions & Sites", "Starting Company", "Wizards & Recruits", "Factions", "Allies"];

        // --- TAB 1: GENERAL SETTINGS & CONTROLS ---
        private readonly TextBox _txtDeckTitle;
        private readonly NumericUpDown _numPlayDeckSize;
        private readonly NumericUpDown _numSideboardSize;
        private readonly Button _btnRunPipeline;
        private readonly Button _btnClearDeck;
        private readonly Button _btnSaveSnapshot;
        private readonly Button _btnRecallSnapshot;
        private readonly Label _lblPipelineStatus;
        private readonly Label _lblDeckSummary;

        // --- TAB 2: REGIONS & SITES ---
        private readonly ComboBox _cmbSeedRegion;
        private readonly NumericUpDown _numRegionCount;
        private readonly NumericUpDown _numMinSiteCount;
        private readonly NumericUpDown _numMaxSiteCount;
        private readonly CheckedListBox _clbRegionTypes;
        private readonly CheckedListBox _clbSiteTypes;
        private readonly Label _lblTheaterStatus;

        // --- TAB 3: STARTING COMPANY ---
        private readonly List<CharacterRowControls> _starterRows = [];
        private readonly Label _lblStarterStatus;

        // --- TAB 4: WIZARDS & RECRUITS ---
        private readonly ComboBox _cmbWizard1;
        private readonly ComboBox _cmbWizard1Copies;
        private readonly ComboBox _cmbWizard2;
        private readonly ComboBox _cmbWizard2Copies;
        private readonly ComboBox _cmbWizard2Dest;
        private bool _isUpdatingWizardControls;

        private readonly List<CharacterRowControls> _recruitRows = [];
        private readonly List<CharacterRowControls> _reserveRows = [];
        private readonly Label _lblRecruitStatus;

        // --- TAB 5: FACTIONS ---
        private readonly NumericUpDown _numResourceFactions;
        private readonly NumericUpDown _numSideboardFactions;
        private readonly Label _lblFactionStatus;

        // --- TAB 6: ALLIES ---
        private readonly NumericUpDown _numResourceAllies;
        private readonly NumericUpDown _numSideboardAllies;
        private readonly Label _lblAllyStatus;

        private readonly ToolTip _toolTip = new()
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 150,
            ShowAlways = true
        };

        public DeckAssemblyMatrixDialog(
            Deck deck,
            CardCatalogService catalog,
            CardFilterService filterService,
            Func<ISet<string>> getActiveSets,
            Action<DeckSectionType?> notifyDeckChanged)
        {
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _getActiveSets = getActiveSets ?? (() => new HashSet<string>());
            _notifyDeckCallback = notifyDeckChanged ?? (_ => { });

            Text = "Modular Deck Assembly Workbench";
            ClientSize = new Size(860, 650);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.0f);

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 5)
            };

            // ==========================================
            // TAB 1: GENERAL SETTINGS
            // ==========================================
            var tabGeneral = new TabPage("General Settings");
            var grpGeneral = new GroupBox
            {
                Text = "Target Deck Sizing && Workbench Profiles",
                Location = new Point(16, 12),
                Size = new Size(810, 172)
            };

            var lblTitle = new Label { Text = "Deck Title:", Location = new Point(16, 24), Size = new Size(110, 20) };
            _txtDeckTitle = new TextBox
            {
                Text = _deck.Title,
                Location = new Point(130, 21),
                Size = new Size(400, 23)
            };
            _txtDeckTitle.TextChanged += (s, e) =>
            {
                _deck.Title = _txtDeckTitle.Text;
                NotifyDeckChanged();
            };

            var lblPlaySize = new Label { Text = "Play Deck Size:", Location = new Point(16, 58), Size = new Size(110, 20) };
            _numPlayDeckSize = new NumericUpDown
            {
                Location = new Point(130, 55),
                Size = new Size(60, 23),
                Minimum = 20,
                Maximum = 60,
                Value = 30
            };
            _numPlayDeckSize.ValueChanged += (s, e) =>
            {
                UpdateDeckSummary();
                ValidateWorkbenchReadiness();
            };

            var lblPlaySizeHint = new Label
            {
                Text = "cards in Resources and equal cards in Hazards (Official tournament standard is 30/30).",
                Location = new Point(200, 58),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            var lblSideSize = new Label { Text = "Sideboard Size:", Location = new Point(16, 92), Size = new Size(110, 20) };
            _numSideboardSize = new NumericUpDown
            {
                Location = new Point(130, 89),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 30,
                Value = 20
            };
            _numSideboardSize.ValueChanged += (s, e) =>
            {
                UpdateDeckSummary();
                ValidateWorkbenchReadiness();
            };

            var lblSideSizeHint = new Label
            {
                Text = "cards in reserve (Official tournament standard is up to 20 or 25).",
                Location = new Point(200, 92),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            var lblProfiles = new Label
            {
                Text = "Profile Settings:",
                Location = new Point(16, 129),
                Size = new Size(110, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            var btnSaveSettings = new Button
            {
                Text = "Save Settings...",
                Location = new Point(130, 126),
                Size = new Size(120, 26)
            };
            btnSaveSettings.Click += (s, e) => SaveSettingsToFile();

            var btnLoadSettings = new Button
            {
                Text = "Load Settings...",
                Location = new Point(258, 126),
                Size = new Size(120, 26)
            };
            btnLoadSettings.Click += (s, e) => LoadSettingsFromFile();

            var lblProfileHint = new Label
            {
                Text = "Save or load workbench configurations (.workbench.json) to reuse tab criteria.",
                Location = new Point(390, 129),
                Size = new Size(410, 24),
                ForeColor = Color.DimGray
            };

            grpGeneral.Controls.AddRange([
                lblTitle, _txtDeckTitle,
                lblPlaySize, _numPlayDeckSize, lblPlaySizeHint,
                lblSideSize, _numSideboardSize, lblSideSizeHint,
                lblProfiles, btnSaveSettings, btnLoadSettings, lblProfileHint
            ]);

            var grpPipeline = new GroupBox
            {
                Text = "Deck Assembly Pipeline && Session Snapshots",
                Location = new Point(16, 192),
                Size = new Size(810, 146)
            };

            var lblOrder = new Label
            {
                Text = "Execution Order:   1. Regions & Sites  ->  2. Starting Company  ->  3. Wizards & Recruits  ->  4. Factions  ->  5. Allies",
                Location = new Point(16, 22),
                Size = new Size(778, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93),
                UseMnemonic = false
            };

            _btnRunPipeline = new Button
            {
                Text = "Run Pipeline",
                Location = new Point(16, 50),
                Size = new Size(125, 30),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunPipeline.Click += (s, e) => RunPipelineDraft();
            _toolTip.SetToolTip(
                _btnRunPipeline,
                "Drafts the full deck in sequence (Regions & Sites -> Starting Company -> Wizards & Recruits -> Factions -> Allies).\n" +
                "Automatically retries up to 25 times if random region rolls fail downstream constraints.");

            _btnClearDeck = new Button
            {
                Text = "Clear Deck",
                Location = new Point(149, 50),
                Size = new Size(110, 30)
            };
            _btnClearDeck.Click += BtnClearDeck_Click;
            _toolTip.SetToolTip(_btnClearDeck, "Clears all cards across Pool, Resources, Hazards, Sideboard, and Sites.");

            _btnSaveSnapshot = new Button
            {
                Text = "Save Snapshot",
                Location = new Point(280, 50),
                Size = new Size(125, 30)
            };
            _btnSaveSnapshot.Click += BtnSaveSnapshot_Click;
            _toolTip.SetToolTip(_btnSaveSnapshot, "Saves an in-session snapshot of all currently drafted deck lists so you can re-roll and restore.");

            _btnRecallSnapshot = new Button
            {
                Text = "Recall Snapshot",
                Location = new Point(413, 50),
                Size = new Size(125, 30),
                Enabled = false
            };
            _btnRecallSnapshot.Click += BtnRecallSnapshot_Click;
            _toolTip.SetToolTip(_btnRecallSnapshot, "Restores the complete deck from your last saved session snapshot.");

            _lblPipelineStatus = new Label
            {
                Location = new Point(16, 90),
                Size = new Size(778, 46),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            grpPipeline.Controls.AddRange([
                lblOrder,
                _btnRunPipeline, _btnClearDeck, _btnSaveSnapshot, _btnRecallSnapshot,
                _lblPipelineStatus
            ]);

            var grpSummary = new GroupBox
            {
                Text = "Live Deck Composition Status",
                Location = new Point(16, 346),
                Size = new Size(810, 260)
            };

            _lblDeckSummary = new Label
            {
                Location = new Point(20, 24),
                Size = new Size(770, 224),
                Font = new Font("Segoe UI", 10.0f),
                ForeColor = Color.FromArgb(26, 54, 93),
                UseMnemonic = false
            };
            grpSummary.Controls.Add(_lblDeckSummary);

            tabGeneral.Controls.AddRange([grpGeneral, grpPipeline, grpSummary]);

            // ==========================================
            // TAB 2: REGIONS & SITES
            // ==========================================
            var tabTheater = new TabPage("Regions & Sites");

            var grpDraftOpts = new GroupBox
            {
                Text = "Drafting Guidelines",
                Location = new Point(16, 16),
                Size = new Size(415, 235)
            };

            var lblSeed = new Label { Text = "Seed Region:", Location = new Point(12, 27), Size = new Size(134, 20) };
            _toolTip.SetToolTip(
                lblSeed,
                "The starting region from which the connected region group\n" +
                "expands outward (border-to-border) up to Region Count.");

            _cmbSeedRegion = new ComboBox
            {
                Location = new Point(150, 24),
                Size = new Size(252, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbSeedRegion.SelectedIndexChanged += (s, e) => UpdateSeedTooltip();

            var lblRegCount = new Label { Text = "Region Count:", Location = new Point(12, 59), Size = new Size(134, 20) };
            _numRegionCount = new NumericUpDown
            {
                Location = new Point(150, 56),
                Size = new Size(60, 23),
                Minimum = 4,
                Maximum = 12,
                Value = 4
            };
            string regCountTip =
                "Target number of connected (border-to-border) regions to draft.\n" +
                "Minimum is 4, allowing most local site-to-site moves within a single turn.";
            _toolTip.SetToolTip(lblRegCount, regCountTip);
            _toolTip.SetToolTip(_numRegionCount, regCountTip);

            var lblMinSites = new Label { Text = "Min Adventure Sites:", Location = new Point(12, 91), Size = new Size(134, 20) };
            _numMinSiteCount = new NumericUpDown
            {
                Location = new Point(150, 88),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 50,
                Value = 4
            };
            string minSitesTip =
                "Minimum number of matching adventure sites (non-Haven sites)\n" +
                "desired inside the region group. Set to 0 for no minimum.";
            _toolTip.SetToolTip(lblMinSites, minSitesTip);
            _toolTip.SetToolTip(_numMinSiteCount, minSitesTip);

            var lblMaxSites = new Label { Text = "Max Adventure Sites:", Location = new Point(12, 123), Size = new Size(134, 20) };
            _numMaxSiteCount = new NumericUpDown
            {
                Location = new Point(150, 120),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 50,
                Value = 12
            };
            string maxSitesTip =
                "Maximum number of matching adventure sites (non-Haven sites) to draft.\n" +
                "If more than Max exist across enabled expansions, a random subset up to Max is kept.";
            _toolTip.SetToolTip(lblMaxSites, maxSitesTip);
            _toolTip.SetToolTip(_numMaxSiteCount, maxSitesTip);

            _numMinSiteCount.ValueChanged += (s, e) =>
            {
                if (_numMinSiteCount.Value > _numMaxSiteCount.Value)
                {
                    _numMaxSiteCount.Value = _numMinSiteCount.Value;
                }
            };
            _numMaxSiteCount.ValueChanged += (s, e) =>
            {
                if (_numMaxSiteCount.Value < _numMinSiteCount.Value)
                {
                    _numMinSiteCount.Value = _numMaxSiteCount.Value;
                }
            };

            var lblTheaterRulesNote = new Label
            {
                Location = new Point(12, 154),
                Size = new Size(390, 74),
                ForeColor = Color.DimGray,
                UseMnemonic = false,
                Text =
                    "Automatic Site Deck Rules:\n" +
                    "• Always drafts all 4 Havens (Rivendell, Lórien, Grey Havens, Edhellond).\n" +
                    "• Starting Company & Recruits prioritize these regions, sites, & Havens."
            };

            grpDraftOpts.Controls.AddRange([
                lblSeed, _cmbSeedRegion,
                lblRegCount, _numRegionCount,
                lblMinSites, _numMinSiteCount,
                lblMaxSites, _numMaxSiteCount,
                lblTheaterRulesNote
            ]);

            var grpFilters = new GroupBox
            {
                Text = "Terrain && Hold Classifications (Uncheck to exclude)",
                Location = new Point(445, 16),
                Size = new Size(380, 235)
            };

            var lblRegTypes = new Label
            {
                Text = "Region Terrains:",
                Location = new Point(12, 22),
                Size = new Size(172, 16)
            };
            _clbRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 42),
                Size = new Size(172, 178),
                CheckOnClick = true
            };
            _clbRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);
            SetAllChecked(_clbRegionTypes, true);
            _clbRegionTypes.ItemCheck += (s, e) => BeginInvoke(ValidateWorkbenchReadiness);
            _toolTip.SetToolTip(
                _clbRegionTypes,
                "Only regions with a checked terrain type will be drafted.\n" +
                "All terrains are checked by default; uncheck any terrain you wish to avoid.");

            var lblSiteTypes = new Label
            {
                Text = "Site Holds:",
                Location = new Point(196, 22),
                Size = new Size(172, 16)
            };
            _clbSiteTypes = new CheckedListBox
            {
                Location = new Point(196, 42),
                Size = new Size(172, 178),
                CheckOnClick = true
            };
            _clbSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold"]);
            SetAllChecked(_clbSiteTypes, true);
            _clbSiteTypes.ItemCheck += (s, e) => BeginInvoke(ValidateWorkbenchReadiness);
            _toolTip.SetToolTip(
                _clbSiteTypes,
                "Only adventure sites with a checked hold type will be drafted\n" +
                "(all 4 Havens are always included automatically).\n" +
                "Unchecking Free-hold/Border-hold will limit available recruit Home sites.");

            grpFilters.Controls.AddRange([lblRegTypes, _clbRegionTypes, lblSiteTypes, _clbSiteTypes]);

            _lblTheaterStatus = new Label
            {
                Location = new Point(16, 268),
                Size = new Size(805, 120),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = TheaterIdleStatusText
            };

            tabTheater.Controls.AddRange([grpDraftOpts, grpFilters, _lblTheaterStatus]);

            // ==========================================
            // TAB 3: STARTING COMPANY (5 ROWS)
            // ==========================================
            var tabStarter = new TabPage("Starting Company");
            BuildCharacterGrid(tabStarter, _starterRows, 5, 42, 28, null);

            _lblStarterStatus = new Label
            {
                Location = new Point(16, 198),
                Size = new Size(790, 90),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = StarterIdleStatusText
            };

            tabStarter.Controls.Add(_lblStarterStatus);

            // ==========================================
            // TAB 4: WIZARDS & RECRUITS
            // ==========================================
            var tabRecruits = new TabPage("Wizards & Recruits");

            var grpWizards = new GroupBox
            {
                Text = "Avatar / Wizard Selection (Primary: Resources | Secondary: Sideboard or Resources)",
                Location = new Point(16, 8),
                Size = new Size(810, 82)
            };

            var lblWiz1 = new Label { Text = "Primary Wizard:", Location = new Point(12, 25), Size = new Size(105, 18) };
            _cmbWizard1 = new ComboBox
            {
                Location = new Point(120, 22),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbWizard1.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard1.SelectedIndex = 0;

            var lblWiz1Cop = new Label { Text = "Copies:", Location = new Point(275, 25), Size = new Size(50, 18) };
            _cmbWizard1Copies = new ComboBox
            {
                Location = new Point(325, 22),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard1Copies.Items.AddRange(["1", "2", "3"]);
            _cmbWizard1Copies.SelectedIndex = 0;

            var lblWiz2 = new Label { Text = "Secondary Wizard:", Location = new Point(12, 53), Size = new Size(105, 18) };
            _cmbWizard2 = new ComboBox
            {
                Location = new Point(120, 50),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard2.SelectedIndex = 0;

            var lblWiz2Cop = new Label { Text = "Copies:", Location = new Point(275, 53), Size = new Size(50, 18) };
            _cmbWizard2Copies = new ComboBox
            {
                Location = new Point(325, 50),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Copies.Items.AddRange(["1", "2"]);
            _cmbWizard2Copies.SelectedIndex = 0;

            var lblWiz2Dest = new Label { Text = "Destination:", Location = new Point(395, 53), Size = new Size(75, 18) };
            _cmbWizard2Dest = new ComboBox
            {
                Location = new Point(475, 50),
                Size = new Size(110, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Dest.Items.AddRange(["Sideboard", "Resources"]);
            _cmbWizard2Dest.SelectedIndex = 0;

            _cmbWizard1.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard1Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Dest.SelectedIndexChanged += WizardSelection_Changed;

            grpWizards.Controls.AddRange([
                lblWiz1, _cmbWizard1, lblWiz1Cop, _cmbWizard1Copies,
                lblWiz2, _cmbWizard2, lblWiz2Cop, _cmbWizard2Copies,
                lblWiz2Dest, _cmbWizard2Dest
            ]);
            tabRecruits.Controls.Add(grpWizards);

            var lblRecruitsHdr = new Label
            {
                Text = "Play Deck Characters (Resources - Up to 10)",
                Location = new Point(16, 98),
                Size = new Size(400, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblRecruitsHdr);

            BuildCharacterGrid(tabRecruits, _recruitRows, 10, 138, 24, ValidateWorkbenchReadiness);

            var lblReservesHdr = new Label
            {
                Text = "Sideboard Characters (Sideboard - Up to 3)",
                Location = new Point(16, 390),
                Size = new Size(450, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblReservesHdr);

            BuildCharacterGrid(tabRecruits, _reserveRows, 3, 430, 24, ValidateWorkbenchReadiness);

            _lblRecruitStatus = new Label
            {
                Location = new Point(16, 514),
                Size = new Size(810, 80),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            tabRecruits.Controls.Add(_lblRecruitStatus);

            // ==========================================
            // TAB 5: FACTIONS
            // ==========================================
            var tabFactions = new TabPage("Factions");

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
                Value = 1
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
                ForeColor = Color.DimGray
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

            // ==========================================
            // TAB 6: ALLIES
            // ==========================================
            var tabAllies = new TabPage("Allies");

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
                Value = 1
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
                ForeColor = Color.DimGray
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

            // Tabs ordered to match pipeline execution order
            _tabControl.TabPages.AddRange([tabGeneral, tabTheater, tabStarter, tabRecruits, tabFactions, tabAllies]);
            _tabInfo["Regions & Sites"] = new(RunRegionsAndSitesDraft, _lblTheaterStatus);
            _tabInfo["Starting Company"] = new(RunStartingCompanyDraft, _lblStarterStatus);
            _tabInfo["Wizards & Recruits"] = new(RunNonStartingCharactersDraft, _lblRecruitStatus);
            _tabInfo["Factions"] = new(RunFactionsDraft, _lblFactionStatus);
            _tabInfo["Allies"] = new(RunAlliesDraft, _lblAllyStatus);

            Controls.Add(_tabControl);

            VisibleChanged += (s, e) =>
            {
                if (Visible)
                {
                    PopulateSeedRegionDropdown();
                    UpdateDeckSummary();
                }
            };

            PopulateSeedRegionDropdown();
            UpdateSeedTooltip();
            UpdateDeckSummary();
            ValidateWorkbenchReadiness();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void NotifyDeckChanged(DeckSectionType? section = null)
        {
            _notifyDeckCallback?.Invoke(section);
        }

        public void UpdateDeckSummary()
        {
            if (IsDisposed) return;

            if (_deck.Pool.Count == 0 && !string.Equals(_lblStarterStatus.Text, StarterIdleStatusText, StringComparison.Ordinal))
            {
                _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblStarterStatus.Text = StarterIdleStatusText;
            }

            int totalResChars = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            int totalSideChars = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            int resFactions = _deck.Resources.Count(c => string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase));
            int sideFactions = _deck.Sideboard.Count(c => string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase));
            int resAllies = _deck.Resources.Count(c => string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase));
            int sideAllies = _deck.Sideboard.Count(c => string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase));

            if (totalResChars == 0 && totalSideChars == 0 && resFactions == 0 && sideFactions == 0 && resAllies == 0 && sideAllies == 0)
            {
                ValidateWorkbenchReadiness();
            }

            int poolMind = _deck.Pool.Sum(c => ParseCardMind(c.Mind));
            int nonAvatarCharsInRes = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInRes = _deck.Resources.Count(IsAvatar);
            int nonAvatarCharsInSideboard = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInSideboard = _deck.Sideboard.Count(IsAvatar);
            int regionsInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int havensInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int advSitesInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string avatarResNote = avatarsInRes > 0 ? $" + {avatarsInRes} Avatar(s)" : "";
            string resFactionNote = resFactions > 0 ? $", {resFactions} faction(s)" : "";
            string resAllyNote = resAllies > 0 ? (resAllies == 1 ? ", 1 ally" : $", {resAllies} allies") : "";

            var sideParts = new List<string>();
            if (nonAvatarCharsInSideboard > 0) sideParts.Add($"{nonAvatarCharsInSideboard} character(s)");
            if (avatarsInSideboard > 0) sideParts.Add($"{avatarsInSideboard} Avatar");
            if (sideFactions > 0) sideParts.Add($"{sideFactions} faction(s)");
            if (sideAllies > 0) sideParts.Add(sideAllies == 1 ? "1 ally" : $"{sideAllies} allies");
            string sideDetailNote = sideParts.Count > 0 ? $" ({string.Join(", ", sideParts)})" : "";

            _lblDeckSummary.Text =
                $"Deck Title:       \"{_deck.Title}\"\n\n" +
                $"Pool:             {_deck.Pool.Count} characters ({poolMind} Mind)  [Official limit: <= 20 Mind]\n" +
                $"Resources:        {_deck.Resources.Count} cards ({nonAvatarCharsInRes} characters{avatarResNote}{resFactionNote}{resAllyNote})  [Limits: <= 10 non-Avatar, <= 3 Avatars]\n" +
                $"Hazards:          {_deck.Hazards.Count} cards\n" +
                $"Sideboard:        {_deck.Sideboard.Count} cards{sideDetailNote}  [Target: {_numSideboardSize.Value}]\n" +
                $"Sites:            {_deck.Sites.Count} cards ({havensInSites} Havens, {regionsInSites} Regions, {advSitesInSites} Adventure Sites)\n\n" +
                $"Play Deck Status: {_deck.Resources.Count} Resources / {_deck.Hazards.Count} Hazards (Target: {_numPlayDeckSize.Value}/{_numPlayDeckSize.Value})\n" +
                $"Sideboard Status: {_deck.Sideboard.Count} / {_numSideboardSize.Value}";
        }

        private void UpdateSeedTooltip()
        {
            string sel = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
            string tip = sel switch
            {
                "Random" =>
                    "Picks a random seed region anywhere across Middle-earth\n" +
                    "(respecting checked Region Terrains).",
                _ when !string.IsNullOrWhiteSpace(sel) =>
                    $"Starts expanding the connected region group outward from {sel}.",
                _ => string.Empty
            };
            _toolTip.SetToolTip(_cmbSeedRegion, tip);
        }

        private void PopulateSeedRegionDropdown()
        {
            string prev = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random";

            _cmbSeedRegion.BeginUpdate();
            _cmbSeedRegion.Items.Clear();

            _cmbSeedRegion.Items.Add("Random");

            var activeSets = _getActiveSets();
            var allRegions = _catalog.Cards
                .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                            (activeSets.Count == 0 || activeSets.Contains(c.Set)))
                .Select(c => c.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase);

            foreach (var reg in allRegions)
            {
                _cmbSeedRegion.Items.Add(reg);
            }

            int idx = _cmbSeedRegion.FindStringExact(prev);
            _cmbSeedRegion.SelectedIndex = idx >= 0 ? idx : 0;
            _cmbSeedRegion.EndUpdate();
        }

        #region TAB1_SESSION_ACTIONS

        private void BtnClearDeck_Click(object sender, EventArgs e)
        {
            int existingCount =
                _deck.Pool.Count +
                _deck.Resources.Count +
                _deck.Hazards.Count +
                _deck.Sideboard.Count +
                _deck.Sites.Count;

            if (existingCount > 0)
            {
                var res = MessageBox.Show(
                    this,
                    $"Clear all {existingCount} cards from Pool, Resources, Hazards, Sideboard, and Sites?",
                    "Clear Deck",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes) return;
            }

            _deck.Pool.Clear();
            _deck.Resources.Clear();
            _deck.Hazards.Clear();
            _deck.Sideboard.Clear();
            _deck.Sites.Clear();

            _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblTheaterStatus.Text = TheaterIdleStatusText;

            _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblStarterStatus.Text = StarterIdleStatusText;

            _lblFactionStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblFactionStatus.Text = FactionIdleStatusText;

            _lblAllyStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblAllyStatus.Text = AllyIdleStatusText;

            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = "Deck cleared.";

            NotifyDeckChanged(null);
            ValidateWorkbenchReadiness();
        }

        private void BtnSaveSnapshot_Click(object sender, EventArgs e)
        {
            _savedDeckSnapshot = new DeckSnapshot(
                _deck.Title,
                [.. _deck.Pool],
                [.. _deck.Resources],
                [.. _deck.Hazards],
                [.. _deck.Sideboard],
                [.. _deck.Sites]);

            _btnRecallSnapshot.Enabled = true;

            int total =
                _savedDeckSnapshot.Pool.Count +
                _savedDeckSnapshot.Resources.Count +
                _savedDeckSnapshot.Hazards.Count +
                _savedDeckSnapshot.Sideboard.Count +
                _savedDeckSnapshot.Sites.Count;

            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = $"Saved session snapshot ({total} cards across all deck sections).";
        }

        private void BtnRecallSnapshot_Click(object sender, EventArgs e)
        {
            if (_savedDeckSnapshot == null)
            {
                MessageBox.Show(this, "No session snapshot has been saved yet.", "Recall Snapshot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _deck.Title = _savedDeckSnapshot.Title;
            _txtDeckTitle.Text = _savedDeckSnapshot.Title;

            _deck.Pool.Clear();
            _deck.Pool.AddRange(_savedDeckSnapshot.Pool);
            _deck.Resources.Clear();
            _deck.Resources.AddRange(_savedDeckSnapshot.Resources);
            _deck.Hazards.Clear();
            _deck.Hazards.AddRange(_savedDeckSnapshot.Hazards);
            _deck.Sideboard.Clear();
            _deck.Sideboard.AddRange(_savedDeckSnapshot.Sideboard);
            _deck.Sites.Clear();
            _deck.Sites.AddRange(_savedDeckSnapshot.Sites);

            _deck.SortSections();
            NotifyDeckChanged(null);

            int total =
                _deck.Pool.Count +
                _deck.Resources.Count +
                _deck.Hazards.Count +
                _deck.Sideboard.Count +
                _deck.Sites.Count;

            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = $"Restored deck from session snapshot ({total} cards).";
        }

        #endregion

        #region PROFILE_SAVE_LOAD

        private string GetSafeDeckFileName()
        {
            if (string.IsNullOrWhiteSpace(_txtDeckTitle.Text)) return "Deck";
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeChars = _txtDeckTitle.Text.Where(c => !invalidChars.Contains(c)).ToArray();
            string safe = new string(safeChars).Trim();
            return !string.IsNullOrWhiteSpace(safe) ? safe : "Deck";
        }

        private void SaveSettingsToFile()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Save Modular Workbench Profile",
                Filter = "Workbench Settings (*.workbench.json)|*.workbench.json|All Files (*.*)|*.*",
                DefaultExt = "workbench.json",
                FileName = $"{GetSafeDeckFileName()}_Profile.workbench.json",
                InitialDirectory = StoragePathService.GetDataDirectory()
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var config = ExportCurrentSettings();
                    string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show(this, $"Workbench profile saved successfully to:\n{Path.GetFileName(sfd.FileName)}", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadSettingsFromFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Load Modular Workbench Profile",
                Filter = "Workbench Settings (*.workbench.json)|*.workbench.json|All Files (*.*)|*.*",
                DefaultExt = "workbench.json",
                InitialDirectory = StoragePathService.GetDataDirectory()
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var config = JsonConvert.DeserializeObject<DeckAssemblySettings>(json);
                    if (config != null)
                    {
                        ApplyLoadedSettings(config);
                        MessageBox.Show(this, $"Workbench profile loaded successfully from:\n{Path.GetFileName(ofd.FileName)}", "Settings Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to load settings: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private DeckAssemblySettings ExportCurrentSettings()
        {
            return new DeckAssemblySettings
            {
                DeckTitle = _txtDeckTitle.Text,
                PlayDeckSize = (int)_numPlayDeckSize.Value,
                SideboardSize = (int)_numSideboardSize.Value,

                SeedRegion = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random",
                RegionCount = (int)_numRegionCount.Value,
                MinSiteCount = (int)_numMinSiteCount.Value,
                MaxSiteCount = (int)_numMaxSiteCount.Value,
                SelectedRegionTypes = [.. _clbRegionTypes.CheckedItems.Cast<string>()],
                SelectedSiteTypes = [.. _clbSiteTypes.CheckedItems.Cast<string>()],

                StarterRows = [.. _starterRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                })],

                PrimaryWizard = _cmbWizard1.SelectedItem?.ToString() ?? "(None)",
                PrimaryWizardCopies = int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1) ? c1 : 1,
                SecondaryWizard = _cmbWizard2.SelectedItem?.ToString() ?? "(None)",
                SecondaryWizardCopies = int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int c2) ? c2 : 1,
                SecondaryWizardDestination = _cmbWizard2Dest.SelectedItem?.ToString() ?? "Sideboard",

                RecruitRows = [.. _recruitRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                })],

                ReserveRows = [.. _reserveRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                })],

                ResourceFactionCount = (int)_numResourceFactions.Value,
                SideboardFactionCount = (int)_numSideboardFactions.Value,

                ResourceAllyCount = (int)_numResourceAllies.Value,
                SideboardAllyCount = (int)_numSideboardAllies.Value
            };
        }

        private void ApplyLoadedSettings(DeckAssemblySettings config)
        {
            if (config == null) return;

            if (!string.IsNullOrWhiteSpace(config.DeckTitle))
            {
                _txtDeckTitle.Text = config.DeckTitle;
            }
            if (config.PlayDeckSize >= _numPlayDeckSize.Minimum && config.PlayDeckSize <= _numPlayDeckSize.Maximum)
            {
                _numPlayDeckSize.Value = config.PlayDeckSize;
            }
            if (config.SideboardSize >= _numSideboardSize.Minimum && config.SideboardSize <= _numSideboardSize.Maximum)
            {
                _numSideboardSize.Value = config.SideboardSize;
            }

            SetComboSelection(_cmbSeedRegion, config.SeedRegion);
            _numRegionCount.Value = Math.Clamp(config.RegionCount, (int)_numRegionCount.Minimum, (int)_numRegionCount.Maximum);

            int clampedMaxSites = Math.Clamp(config.MaxSiteCount, (int)_numMaxSiteCount.Minimum, (int)_numMaxSiteCount.Maximum);
            int clampedMinSites = Math.Clamp(config.MinSiteCount, (int)_numMinSiteCount.Minimum, clampedMaxSites);
            _numMaxSiteCount.Value = clampedMaxSites;
            _numMinSiteCount.Value = clampedMinSites;

            SetCheckedItems(_clbRegionTypes, config.SelectedRegionTypes);
            SetCheckedItems(_clbSiteTypes, config.SelectedSiteTypes);

            if (config.StarterRows != null)
            {
                for (int i = 0; i < Math.Min(_starterRows.Count, config.StarterRows.Count); i++)
                {
                    ApplyRowSettings(_starterRows[i], config.StarterRows[i]);
                }
            }

            SetComboSelection(_cmbWizard1, config.PrimaryWizard);
            SetComboSelection(_cmbWizard1Copies, config.PrimaryWizardCopies.ToString());
            SetComboSelection(_cmbWizard2, config.SecondaryWizard);
            SetComboSelection(_cmbWizard2Copies, config.SecondaryWizardCopies.ToString());
            SetComboSelection(_cmbWizard2Dest, config.SecondaryWizardDestination);
            WizardSelection_Changed(null, null);

            if (config.RecruitRows != null)
            {
                for (int i = 0; i < Math.Min(_recruitRows.Count, config.RecruitRows.Count); i++)
                {
                    ApplyRowSettings(_recruitRows[i], config.RecruitRows[i]);
                }
            }

            if (config.ReserveRows != null)
            {
                for (int i = 0; i < Math.Min(_reserveRows.Count, config.ReserveRows.Count); i++)
                {
                    ApplyRowSettings(_reserveRows[i], config.ReserveRows[i]);
                }
            }

            _numResourceFactions.Value = Math.Clamp(config.ResourceFactionCount, (int)_numResourceFactions.Minimum, (int)_numResourceFactions.Maximum);
            _numSideboardFactions.Value = Math.Clamp(config.SideboardFactionCount, (int)_numSideboardFactions.Minimum, (int)_numSideboardFactions.Maximum);

            _numResourceAllies.Value = Math.Clamp(config.ResourceAllyCount, (int)_numResourceAllies.Minimum, (int)_numResourceAllies.Maximum);
            _numSideboardAllies.Value = Math.Clamp(config.SideboardAllyCount, (int)_numSideboardAllies.Minimum, (int)_numSideboardAllies.Maximum);

            UpdateDeckSummary();
            ValidateWorkbenchReadiness();
        }

        private static void SetComboSelection(ComboBox cb, string value)
        {
            if (cb == null || string.IsNullOrWhiteSpace(value)) return;
            int idx = cb.FindStringExact(value);
            if (idx >= 0) cb.SelectedIndex = idx;
        }

        private static void SetAllChecked(CheckedListBox clb, bool isChecked)
        {
            if (clb == null) return;
            for (int i = 0; i < clb.Items.Count; i++)
            {
                clb.SetItemChecked(i, isChecked);
            }
        }

        private static void SetCheckedItems(CheckedListBox clb, List<string> selectedItems)
        {
            if (clb == null) return;
            var selectedSet = new HashSet<string>(selectedItems ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < clb.Items.Count; i++)
            {
                string itemStr = clb.Items[i]?.ToString() ?? "";
                clb.SetItemChecked(i, selectedSet.Contains(itemStr));
            }
        }

        private static void ApplyRowSettings(CharacterRowControls row, CharacterRowSettings settings)
        {
            if (row == null || settings == null) return;
            SetComboSelection(row.CmbSkill, settings.Skill);
            SetComboSelection(row.CmbSpecies, settings.Species);
            row.TxtMind.Text = settings.Mind ?? string.Empty;
            row.TxtDI.Text = settings.DI ?? string.Empty;
        }

        #endregion

        #region WIZARD_CONTROLS_AND_BUDGET_VALIDATION

        private void WizardSelection_Changed(object sender, EventArgs e)
        {
            if (_isUpdatingWizardControls) return;
            _isUpdatingWizardControls = true;

            try
            {
                bool hasWiz1 = _cmbWizard1.SelectedIndex > 0;
                _cmbWizard1Copies.Enabled = hasWiz1;

                int wiz1Copies = hasWiz1 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1) ? c1 : 0;

                bool canHaveWiz2 = hasWiz1 && wiz1Copies < 3;
                _cmbWizard2.Enabled = canHaveWiz2;

                if (!canHaveWiz2)
                {
                    _cmbWizard2.SelectedIndex = 0;
                    _cmbWizard2Copies.Enabled = false;
                    _cmbWizard2Dest.Enabled = false;
                }
                else
                {
                    string wiz1Name = _cmbWizard1.SelectedItem?.ToString() ?? "";
                    string wiz2Name = _cmbWizard2.SelectedItem?.ToString() ?? "";
                    if (_cmbWizard1.SelectedIndex > 1 && string.Equals(wiz1Name, wiz2Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _cmbWizard2.SelectedIndex = 0;
                    }

                    bool hasWiz2 = _cmbWizard2.SelectedIndex > 0;
                    _cmbWizard2Copies.Enabled = hasWiz2;
                    _cmbWizard2Dest.Enabled = hasWiz2;

                    if (hasWiz2)
                    {
                        int maxWiz2Copies = Math.Min(2, 3 - wiz1Copies);
                        string prevWiz2Cop = _cmbWizard2Copies.SelectedItem?.ToString() ?? "1";

                        _cmbWizard2Copies.Items.Clear();
                        for (int i = 1; i <= maxWiz2Copies; i++)
                        {
                            _cmbWizard2Copies.Items.Add(i.ToString());
                        }

                        int idx = _cmbWizard2Copies.FindStringExact(prevWiz2Cop);
                        _cmbWizard2Copies.SelectedIndex = idx >= 0 ? idx : 0;

                        if (_cmbWizard2Dest.SelectedIndex < 0)
                        {
                            _cmbWizard2Dest.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        _cmbWizard2Copies.Enabled = false;
                        _cmbWizard2Dest.Enabled = false;
                    }
                }
            }
            finally
            {
                _isUpdatingWizardControls = false;
            }

            ValidateWorkbenchReadiness();
        }

        private int GetSelectedWizard1Count()
        {
            if (_cmbWizard1.SelectedIndex > 0 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1))
            {
                return c1;
            }
            return 0;
        }

        private int GetSelectedWizard2Count()
        {
            if (_cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 && int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int c2))
            {
                return c2;
            }
            return 0;
        }

        private bool IsWizard2InSideboard()
        {
            return _cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 &&
                   string.Equals(_cmbWizard2Dest.SelectedItem?.ToString(), "Sideboard", StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateWorkbenchReadiness()
        {
            if (_btnRunPipeline == null || _lblRecruitStatus == null || _lblTheaterStatus == null || _lblFactionStatus == null || _lblAllyStatus == null) return;

            bool hasTheaterError = false;
            bool hasRecruitError = false;
            bool hasFactionError = false;
            bool hasAllyError = false;

            int wizInRes = GetSelectedWizard1Count() + (IsWizard2InSideboard() ? 0 : GetSelectedWizard2Count());
            int wizInSide = IsWizard2InSideboard() ? GetSelectedWizard2Count() : 0;

            int companionCount = _recruitRows.Count(r => r.IsActive);
            int reserveCount = _reserveRows.Count(r => r.IsActive);
            int totalSideboardChars = reserveCount + wizInSide;

            int resFactions = (int)_numResourceFactions.Value;
            int sideFactions = (int)_numSideboardFactions.Value;
            int resAllies = (int)_numResourceAllies.Value;
            int sideAllies = (int)_numSideboardAllies.Value;
            int playDeckLimit = (int)_numPlayDeckSize.Value;
            int sideboardLimit = (int)_numSideboardSize.Value;

            // --- Tab 2 Validation ---
            if (_clbRegionTypes.CheckedItems.Count == 0 || _clbSiteTypes.CheckedItems.Count == 0)
            {
                hasTheaterError = true;
                _lblTheaterStatus.ForeColor = Color.Firebrick;
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblTheaterStatus.Text =
                    "⚠ At least one Region Terrain and one Site Hold must be checked under Terrain & Hold Classifications.";
            }
            else if (_lblTheaterStatus.ForeColor == Color.Firebrick &&
                     _lblTheaterStatus.Text.StartsWith("⚠ At least one Region Terrain", StringComparison.Ordinal))
            {
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblTheaterStatus.Text = TheaterIdleStatusText;
            }

            // --- Tab 4 Validation ---
            if (companionCount > 10)
            {
                hasRecruitError = true;
                _lblRecruitStatus.ForeColor = Color.Firebrick;
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"⚠ Selection exceeds the 10-character limit for Play Deck Characters (Resources): {companionCount}/10 configured.\n" +
                    $"Clear {companionCount - 10} row(s) to proceed.";
            }
            else if (totalSideboardChars > sideboardLimit)
            {
                hasRecruitError = true;
                _lblRecruitStatus.ForeColor = Color.Firebrick;
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"⚠ Sideboard characters ({totalSideboardChars}: {reserveCount} Sideboard row(s){(wizInSide > 0 ? $" + {wizInSide} Wizard" : "")}) exceed the Sideboard Size limit ({sideboardLimit}) set in General Settings.\n" +
                    "Increase Sideboard Size, change Secondary Wizard destination, or clear a Sideboard row.";
            }
            else
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);

                string wizResText = wizInRes > 0 ? $", {wizInRes} Wizard(s)" : "";
                string wizSideText = wizInSide > 0 ? $" + {wizInSide} Wizard" : "";
                _lblRecruitStatus.Text =
                    $"Configured: {companionCount}/10 Resources{wizResText}  |  {reserveCount}/3 Sideboard{wizSideText}.\n" +
                    "Recruits prioritize: 1) local sites (including local Havens), 2) outside Havens, 3) any Haven-playable character.";
            }

            // --- Tab 5 (Factions) & Tab 6 (Allies) Shared Budget Validation ---
            int totalConfiguredRes = companionCount + wizInRes + resFactions + resAllies;
            int totalConfiguredSide = totalSideboardChars + sideFactions + sideAllies;

            string resOverMsg =
                $"⚠ Configured Resources cards ({totalConfiguredRes}: {companionCount + wizInRes} characters + {resFactions} factions + {resAllies} allies) exceed Play Deck Size ({playDeckLimit}).\n" +
                "Reduce Factions/Allies in Play Deck or increase Play Deck Size on General Settings.";

            string sideOverMsg =
                $"⚠ Configured Sideboard cards ({totalConfiguredSide}: {totalSideboardChars} characters + {sideFactions} factions + {sideAllies} allies) exceed Sideboard Size ({sideboardLimit}).\n" +
                "Reduce Factions/Allies in Sideboard or increase Sideboard Size on General Settings.";

            if (totalConfiguredRes > playDeckLimit && resFactions > 0)
            {
                hasFactionError = true;
                _lblFactionStatus.ForeColor = Color.Firebrick;
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblFactionStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sideFactions > 0)
            {
                hasFactionError = true;
                _lblFactionStatus.ForeColor = Color.Firebrick;
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblFactionStatus.Text = sideOverMsg;
            }
            else if ((_lblFactionStatus.ForeColor == Color.Firebrick &&
                      (_lblFactionStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblFactionStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase)) &&
                      _deck.Sideboard.All(c => !string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase)) &&
                      !_lblFactionStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblFactionStatus.Text.StartsWith("No factions configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblFactionStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblFactionStatus.Text = FactionIdleStatusText;
            }

            if (totalConfiguredRes > playDeckLimit && resAllies > 0)
            {
                hasAllyError = true;
                _lblAllyStatus.ForeColor = Color.Firebrick;
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblAllyStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sideAllies > 0)
            {
                hasAllyError = true;
                _lblAllyStatus.ForeColor = Color.Firebrick;
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblAllyStatus.Text = sideOverMsg;
            }
            else if ((_lblAllyStatus.ForeColor == Color.Firebrick &&
                      (_lblAllyStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblAllyStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase)) &&
                      _deck.Sideboard.All(c => !string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase)) &&
                      !_lblAllyStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblAllyStatus.Text.StartsWith("No allies configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblAllyStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblAllyStatus.Text = AllyIdleStatusText;
            }

            _btnRunPipeline.Enabled = !hasTheaterError && !hasRecruitError && !hasFactionError && !hasAllyError;

            if (hasTheaterError || hasRecruitError || hasFactionError || hasAllyError)
            {
                var blockedTabs = new List<string>();
                if (hasTheaterError) blockedTabs.Add("Regions & Sites");
                if (hasRecruitError) blockedTabs.Add("Wizards & Recruits");
                if (hasFactionError) blockedTabs.Add("Factions");
                if (hasAllyError) blockedTabs.Add("Allies");

                _lblPipelineStatus.ForeColor = Color.Firebrick;
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblPipelineStatus.Text = $"⚠ Run Pipeline disabled: resolve validation error(s) on {string.Join(", ", blockedTabs)}.";
            }
            else if (_lblPipelineStatus.ForeColor == Color.Firebrick &&
                     _lblPipelineStatus.Text.StartsWith("⚠ Run Pipeline disabled:", StringComparison.Ordinal))
            {
                _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblPipelineStatus.Text = "Ready to run pipeline.";
            }
        }

        #endregion

        #region GRID_BUILDER

        private static void BuildCharacterGrid(
            TabPage page,
            List<CharacterRowControls> rowList,
            int count,
            int startY,
            int rowSpacing,
            Action onRowChanged)
        {
            var lblHdrSkill = new Label { Text = "Skill Focus", Location = new Point(16, startY - 20), Size = new Size(135, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrRace = new Label { Text = "Species / Race", Location = new Point(157, startY - 20), Size = new Size(135, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrMind = new Label { Text = "Mind (e.g. <=3, 2-4)", Location = new Point(298, startY - 20), Size = new Size(115, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrDI = new Label { Text = "DI (e.g. >=2)", Location = new Point(419, startY - 20), Size = new Size(115, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            page.Controls.AddRange([lblHdrSkill, lblHdrRace, lblHdrMind, lblHdrDI]);

            for (int i = 0; i < count; i++)
            {
                int y = startY + (i * rowSpacing);
                var row = new CharacterRowControls
                {
                    CmbSkill = new ComboBox { Location = new Point(16, y), Size = new Size(135, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    CmbSpecies = new ComboBox { Location = new Point(157, y), Size = new Size(135, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    TxtMind = new TextBox { Location = new Point(298, y), Size = new Size(115, 23) },
                    TxtDI = new TextBox { Location = new Point(419, y), Size = new Size(115, 23) },
                    BtnClear = new Button { Text = "✕", Location = new Point(540, y - 1), Size = new Size(28, 25), ForeColor = Color.DimGray }
                };

                row.CmbSkill.Items.AddRange(["Any", "Diplomat", "Ranger", "Sage", "Scout", "Warrior"]);
                row.CmbSkill.SelectedIndex = 0;

                row.CmbSpecies.Items.AddRange(["Any", "Dwarf", "Dúnadan", "Elf", "Hobbit", "Man"]);
                row.CmbSpecies.SelectedIndex = 0;

                if (onRowChanged != null)
                {
                    row.CmbSkill.SelectedIndexChanged += (s, e) => onRowChanged();
                    row.CmbSpecies.SelectedIndexChanged += (s, e) => onRowChanged();
                    row.TxtMind.TextChanged += (s, e) => onRowChanged();
                    row.TxtDI.TextChanged += (s, e) => onRowChanged();
                }

                row.BtnClear.Click += (s, e) =>
                {
                    row.Clear();
                    onRowChanged?.Invoke();
                };

                rowList.Add(row);
                page.Controls.AddRange([row.CmbSkill, row.CmbSpecies, row.TxtMind, row.TxtDI, row.BtnClear]);
            }
        }

        #endregion

        #region DRAFTING_ENGINES

        private bool RunRegionsAndSitesDraft(bool silent = false)
        {
            var checkedTerrains = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            var checkedHolds = _clbSiteTypes.CheckedItems.Cast<string>().ToList();

            if (checkedTerrains.Count == 0 || checkedHolds.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Please select at least one Region Terrain and at least one Site Hold under Terrain & Hold Classifications.",
                        "Regions & Sites",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;
            int targetRegions = (int)_numRegionCount.Value;
            int minSites = (int)_numMinSiteCount.Value;
            int maxSites = (int)_numMaxSiteCount.Value;

            string terrainFilter = string.Join("; ", checkedTerrains);
            string holdFilter = string.Join("; ", checkedHolds);

            var graph = _filterService.BuildRegionAdjacencyGraph(allCards);

            var allowedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in allCards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                var rTypes = DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService);
                if (DeckGenerationService.MatchesRegionTypeFilter(rTypes, terrainFilter))
                {
                    allowedRegions.Add(regCard.Name.Trim());
                }
            }

            if (allowedRegions.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No regions match the checked Region Terrains.", "Regions & Sites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random";
            bool isRandomSeed = string.Equals(seedChoice, "Random", StringComparison.OrdinalIgnoreCase);

            var candidateSeeds = allCards
                .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                            (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                            allowedRegions.Contains(c.Name.Trim()))
                .Select(c => c.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidateSeeds.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No regions in your active expansion sets match the checked Region Terrains.", "Regions & Sites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            string terrainAdjustedNote = null;
            if (!isRandomSeed && !allowedRegions.Contains(seedChoice))
            {
                var pathToAllowed = DeckGenerationService.FindShortestRegionPath(graph, seedChoice, allowedRegions);
                if (pathToAllowed.Count == 0)
                {
                    if (!silent)
                    {
                        MessageBox.Show(
                            this,
                            $"Selected Seed Region '{seedChoice}' and all connected neighbors are excluded by the checked Region Terrains.",
                            "Regions & Sites",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    return false;
                }

                string nearestAllowed = pathToAllowed[^1];
                terrainAdjustedNote = $"Seed '{seedChoice}' excluded by terrain filter; started from nearest allowed region '{nearestAllowed}'.";
                seedChoice = nearestAllowed;
            }

            int maxAttempts = isRandomSeed ? 50 : 25;
            string bestSeed = seedChoice;
            List<string> bestCluster = [];
            int bestScore = -1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                string trialSeed = isRandomSeed
                    ? candidateSeeds[Random.Shared.Next(candidateSeeds.Count)]
                    : seedChoice;

                var trialCluster = new List<string> { trialSeed };
                ExpandClusterToTarget(graph, trialCluster, targetRegions, allowedRegions);

                int matchingSiteCount = allCards
                    .Where(c =>
                        string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                        (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                        trialCluster.Contains(c.Region, StringComparer.OrdinalIgnoreCase) &&
                        DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter) &&
                        DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                    .Select(c => c.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                bool meetsRegions = trialCluster.Count >= targetRegions;
                bool meetsMinSites = minSites == 0 ? matchingSiteCount > 0 : matchingSiteCount >= minSites;

                int score = (meetsRegions ? 10000 : trialCluster.Count * 100) +
                            (meetsMinSites ? 1000 : matchingSiteCount * 50) +
                            Math.Min(matchingSiteCount, maxSites);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                }

                if (meetsRegions && meetsMinSites)
                {
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                    break;
                }
            }

            var draftedSitesAndRegions = new List<Card>();
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var havenName in HeroHavens)
            {
                var havenCard = FindHeroPreferredCard(allCards, activeSets, "Site", havenName);
                if (havenCard != null && addedNames.Add(havenCard.Name))
                {
                    draftedSitesAndRegions.Add(havenCard);
                }
            }

            foreach (var regName in bestCluster)
            {
                var regCard = FindHeroPreferredCard(allCards, activeSets, "Region", regName);
                if (regCard != null && addedNames.Add(regCard.Name))
                {
                    draftedSitesAndRegions.Add(regCard);
                }
            }

            var matchingAdventureSites = allCards
                .Where(c =>
                    string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    bestCluster.Contains(c.Region, StringComparer.OrdinalIgnoreCase) &&
                    DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter) &&
                    DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null && !addedNames.Contains(c.Name))
                .ToList();

            int totalAvailableAdvSites = matchingAdventureSites.Count;
            bool wasCappedByMax = totalAvailableAdvSites > maxSites;

            IEnumerable<Card> selectedAdventureSites = wasCappedByMax
                ? matchingAdventureSites.OrderBy(_ => Random.Shared.Next()).Take(maxSites)
                : matchingAdventureSites;

            foreach (var site in selectedAdventureSites)
            {
                if (addedNames.Add(site.Name))
                {
                    draftedSitesAndRegions.Add(site);
                }
            }

            _deck.Sites.Clear();
            _deck.Sites.AddRange(draftedSitesAndRegions);
            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Sites);
            }

            int finalHavens = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int finalRegions = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int finalAdvSites = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string capNote = wasCappedByMax ? $" (capped from {totalAvailableAdvSites} available)" : string.Empty;
            string regionListStr = string.Join(", ", bestCluster);
            string statusMsg =
                $"Drafted {draftedSitesAndRegions.Count} cards ({finalHavens} Havens, {finalRegions} Regions, {finalAdvSites} Adventure Sites{capNote}). Seed: {bestSeed}\n" +
                $"Regions: {regionListStr}";

            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(terrainAdjustedNote))
            {
                warnings.Add(terrainAdjustedNote);
            }
            if (bestCluster.Count < targetRegions)
            {
                warnings.Add($"Region Shortfall: Found only {bestCluster.Count} of {targetRegions} connected regions matching checked terrains.");
            }
            if (minSites > 0 && finalAdvSites < minSites)
            {
                warnings.Add($"Site Shortfall: Found only {finalAdvSites} of {minSites} minimum adventure sites matching checked holds.");
            }
            else if (finalAdvSites == 0)
            {
                warnings.Add("Site Shortfall: 0 adventure sites matched the checked Site Holds inside these regions.");
            }

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblTheaterStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblTheaterStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblTheaterStatus.Text = statusMsg;
            return true;
        }

        private bool RunStartingCompanyDraft(bool silent = false)
        {
            var activeRows = _starterRows.Where(r => r.IsActive).ToList();
            if (activeRows.Count == 0)
            {
                _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblStarterStatus.Text = "No starting company rows configured (0 characters drafted into Pool).";
                return true;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var havenSites = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (draftedRegions.Count == 0 && havenSites.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Starting Company requires regions/Havens in the Site deck (run Regions & Sites first).",
                        "Starting Company",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var localHavens = havenSites
                .Where(h => !string.IsNullOrWhiteSpace(h.Region) && draftedRegions.Contains(h.Region.Trim()))
                .ToList();

            var outsideHavens = havenSites
                .Where(h => string.IsNullOrWhiteSpace(h.Region) || !draftedRegions.Contains(h.Region.Trim()))
                .ToList();

            var tier1PerRow = new List<List<Card>>(activeRows.Count);
            var tier2PerRow = new List<List<Card>>(activeRows.Count);
            var tier3PerRow = new List<List<Card>>(activeRows.Count);
            var allPerRow = new List<List<Card>>(activeRows.Count);

            for (int i = 0; i < activeRows.Count; i++)
            {
                var t1 = GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, draftedRegions, localHavens);
                var t1Ids = new HashSet<string>(t1.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);

                var t2 = outsideHavens.Count > 0
                    ? GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, null, outsideHavens)
                        .Where(c => !t1Ids.Contains(c.Id))
                        .ToList()
                    : [];
                var t12Ids = new HashSet<string>(t1Ids.Concat(t2.Select(c => c.Id)), StringComparer.OrdinalIgnoreCase);

                var t3 = GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, null, null)
                    .Where(c => !t12Ids.Contains(c.Id))
                    .ToList();

                var combined = new List<Card>(t1.Count + t2.Count + t3.Count);
                combined.AddRange(t1);
                combined.AddRange(t2);
                combined.AddRange(t3);

                if (combined.Count == 0)
                {
                    if (!silent)
                    {
                        MessageBox.Show(
                            this,
                            $"No matching characters found for Starting Company Row {i + 1} in your active expansion sets.",
                            "Starting Company",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    return false;
                }

                tier1PerRow.Add(t1);
                tier2PerRow.Add(t2);
                tier3PerRow.Add(t3);
                allPerRow.Add(combined);
            }

            var minRowMind = allPerRow.Select(list => list.Min(c => ParseCardMind(c.Mind))).ToArray();
            int minPossibleMindSum = minRowMind.Sum();

            if (minPossibleMindSum > 20)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Cannot draft starting company: The minimum possible Mind sum for your selected rows is {minPossibleMindSum}, which exceeds the official 20-Mind starting pool limit.\n\n" +
                        "Please lower your Mind thresholds or clear one of the rows.",
                        "Starting Company (20-Mind Limit)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var baseUsedTitles = new HashSet<string>(
                _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)),
                StringComparer.OrdinalIgnoreCase);

            List<Card> bestCompany = null;
            int bestScore = -1;
            int bestT1Count = 0;
            int bestT2Count = 0;
            int bestT3Count = 0;

            for (int attempt = 0; attempt < 100; attempt++)
            {
                var rowIndices = Enumerable.Range(0, activeRows.Count).ToList();
                if (attempt > 0)
                {
                    rowIndices = [.. rowIndices.OrderBy(_ => Random.Shared.Next())];
                }

                var picked = new List<Card>(activeRows.Count);
                var usedTitles = new HashSet<string>(baseUsedTitles, StringComparer.OrdinalIgnoreCase);
                int currentMind = 0;
                int remainingMinMind = minPossibleMindSum;
                int t1Count = 0;
                int t2Count = 0;
                int t3Count = 0;
                bool validPermutation = true;

                foreach (int rowIdx in rowIndices)
                {
                    remainingMinMind -= minRowMind[rowIdx];
                    int maxAllowedMind = 20 - currentMind - remainingMinMind;

                    var eligibleT1 = tier1PerRow[rowIdx]
                        .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                        .ToList();

                    Card chosen = null;
                    if (eligibleT1.Count > 0)
                    {
                        chosen = eligibleT1[Random.Shared.Next(eligibleT1.Count)];
                        t1Count++;
                    }
                    else
                    {
                        var eligibleT2 = tier2PerRow[rowIdx]
                            .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                            .ToList();

                        if (eligibleT2.Count > 0)
                        {
                            chosen = eligibleT2[Random.Shared.Next(eligibleT2.Count)];
                            t2Count++;
                        }
                        else
                        {
                            var eligibleT3 = tier3PerRow[rowIdx]
                                .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                                .ToList();

                            if (eligibleT3.Count > 0)
                            {
                                chosen = eligibleT3[Random.Shared.Next(eligibleT3.Count)];
                                t3Count++;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        validPermutation = false;
                        break;
                    }

                    picked.Add(chosen);
                    usedTitles.Add(chosen.Name);
                    currentMind += ParseCardMind(chosen.Mind);
                }

                int score = (t1Count * 10000) + (t2Count * 100);
                if (validPermutation && currentMind <= 20 && score > bestScore)
                {
                    bestCompany = picked;
                    bestScore = score;
                    bestT1Count = t1Count;
                    bestT2Count = t2Count;
                    bestT3Count = t3Count;

                    if (bestT1Count == activeRows.Count)
                    {
                        break;
                    }
                }
            }

            if (bestCompany == null)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Unable to find a valid starting company combination totaling 20 Mind or less (after 100 attempts).\n\n" +
                        "Try broadening skills/species, allowing lower-mind characters (Mind 1–3), or clearing a row.",
                        "Starting Company",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            _deck.Pool.Clear();
            _deck.Pool.AddRange(bestCompany);
            _deck.SortSections();

            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Pool);
            }

            int finalMind = bestCompany.Sum(c => ParseCardMind(c.Mind));
            string companyNames = string.Join(", ", bestCompany.Select(c => $"{c.Name} ({c.Mind} Mind)"));

            var tierParts = new List<string>();
            if (bestT1Count > 0) tierParts.Add($"{bestT1Count} local region(s)");
            if (bestT2Count > 0) tierParts.Add($"{bestT2Count} outside Haven(s)");
            if (bestT3Count > 0) tierParts.Add($"{bestT3Count} out-of-region");
            string tierNote = (bestT2Count == 0 && bestT3Count == 0)
                ? " [All from drafted regions]"
                : $" [{string.Join(", ", tierParts)}]";

            _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblStarterStatus.Text = $"Drafted {bestCompany.Count} starting characters (Total Mind: {finalMind}/20){tierNote}:\n{companyNames}";

            return true;
        }

        private bool RunNonStartingCharactersDraft(bool silent = false)
        {
            int wiz1Count = GetSelectedWizard1Count();
            int wiz2Count = GetSelectedWizard2Count();
            var activeRecruitRows = _recruitRows.Where(r => r.IsActive).ToList();
            var activeReserveRows = _reserveRows.Where(r => r.IsActive).ToList();

            if (wiz1Count == 0 && wiz2Count == 0 && activeRecruitRows.Count == 0 && activeReserveRows.Count == 0)
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text = "No Wizards or additional characters configured (0 characters drafted).";
                return true;
            }

            if (activeRecruitRows.Count > 10)
            {
                ValidateWorkbenchReadiness();
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Selection exceeds the 10-character limit for Play Deck Characters ({activeRecruitRows.Count}/10).",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            int wizInSide = IsWizard2InSideboard() ? wiz2Count : 0;
            int totalSideboardChars = activeReserveRows.Count + wizInSide;
            if (totalSideboardChars > (int)_numSideboardSize.Value)
            {
                ValidateWorkbenchReadiness();
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Sideboard characters ({totalSideboardChars}) exceed the Sideboard Size limit ({(int)_numSideboardSize.Value}) set in General Settings.",
                        "Sideboard Size Limit",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var allWizards = allCards.Where(c =>
                string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) &&
                (activeSets.Count == 0 || activeSets.Contains(c.Set))).ToList();

            string wiz1Choice = wiz1Count > 0 ? _cmbWizard1.SelectedItem.ToString() : string.Empty;
            string wiz2Choice = wiz2Count > 0 ? _cmbWizard2.SelectedItem.ToString() : string.Empty;
            bool isWiz1Random = string.Equals(wiz1Choice, "Random", StringComparison.OrdinalIgnoreCase);
            bool isWiz2Random = string.Equals(wiz2Choice, "Random", StringComparison.OrdinalIgnoreCase);

            if (wiz1Count > 0 && !isWiz1Random &&
                allWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz1Choice)) == null)
            {
                if (!silent)
                {
                    MessageBox.Show(this, $"Could not find Primary Wizard '{wiz1Choice}' in your active expansion sets.", "Wizards & Recruits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (wiz1Count > 0 && isWiz1Random && allWizards.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No Wizards found in your active expansion sets.", "Wizards & Recruits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allSitesInDeck.Count == 0 && (activeRecruitRows.Count > 0 || activeReserveRows.Count > 0))
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Wizards & Recruits requires Sites/Havens in the Site deck (run Regions & Sites first).",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var tier1Sites = allSitesInDeck
                .Where(s =>
                    !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(s.Region) && draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var tier2Sites = allSitesInDeck
                .Where(s =>
                    string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(s.Region) || !draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var rowSpecs = new List<(bool IsReserve, int RowNum, List<Card> Tier1, List<Card> Tier2, List<Card> Tier3)>();

            void AddRowSpec(CharacterRowControls row, bool isReserve, int rowNum)
            {
                var t1 = tier1Sites.Count > 0
                    ? GetCandidatesForCharacterRow(row, allCards, activeSets, null, tier1Sites)
                        .Where(c => SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                        .ToList()
                    : [];
                var t1Ids = new HashSet<string>(t1.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);

                var t2 = tier2Sites.Count > 0
                    ? GetCandidatesForCharacterRow(row, allCards, activeSets, null, tier2Sites)
                        .Where(c => !t1Ids.Contains(c.Id) && SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                        .ToList()
                    : [];
                var t12Ids = new HashSet<string>(t1Ids.Concat(t2.Select(c => c.Id)), StringComparer.OrdinalIgnoreCase);

                var t3 = GetCandidatesForCharacterRow(row, allCards, activeSets, null, null)
                    .Where(c => !t12Ids.Contains(c.Id) && SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                    .ToList();

                rowSpecs.Add((isReserve, rowNum, t1, t2, t3));
            }

            for (int i = 0; i < activeRecruitRows.Count; i++)
            {
                AddRowSpec(activeRecruitRows[i], false, i + 1);
            }

            for (int i = 0; i < activeReserveRows.Count; i++)
            {
                AddRowSpec(activeReserveRows[i], true, i + 1);
            }

            var emptyRow = rowSpecs.FirstOrDefault(r => r.Tier1.Count == 0 && r.Tier2.Count == 0 && r.Tier3.Count == 0);
            if (emptyRow.Tier1 != null)
            {
                if (!silent)
                {
                    string label = emptyRow.IsReserve
                        ? $"Sideboard Character Row {emptyRow.RowNum}"
                        : $"Play Deck Character Row {emptyRow.RowNum}";
                    MessageBox.Show(
                        this,
                        $"No matching characters found for {label} whose Site Requirement is satisfied by the drafted Site deck.",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            List<Card> finalRecruits = null;
            List<Card> finalReserves = null;
            int bestScore = -1;
            int bestT1Count = 0;
            int bestT2Count = 0;
            int bestT3Count = 0;
            string lastFailureReason = null;

            for (int attempt = 0; attempt < 25; attempt++)
            {
                var attemptRecruits = new List<Card>();
                var attemptReserves = new List<Card>();
                var usedTitles = new HashSet<string>(_deck.Pool.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

                string chosenPrimaryWizardName = null;

                if (wiz1Count > 0)
                {
                    Card wiz1Card = isWiz1Random
                        ? allWizards[Random.Shared.Next(allWizards.Count)]
                        : allWizards.First(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz1Choice));

                    chosenPrimaryWizardName = wiz1Card.Name;
                    for (int i = 0; i < wiz1Count; i++) attemptRecruits.Add(wiz1Card);
                    usedTitles.Add(wiz1Card.Name);
                }

                if (wiz2Count > 0)
                {
                    var remainingWizards = allWizards
                        .Where(w => !string.Equals(w.Name, chosenPrimaryWizardName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Card wiz2Card = isWiz2Random
                        ? (remainingWizards.Count > 0 ? remainingWizards[Random.Shared.Next(remainingWizards.Count)] : null)
                        : remainingWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz2Choice));

                    if (wiz2Card == null)
                    {
                        lastFailureReason = $"Could not find distinct Secondary Wizard '{wiz2Choice}' in your active expansion sets.";
                        continue;
                    }

                    bool toSideboard = IsWizard2InSideboard();
                    for (int i = 0; i < wiz2Count; i++)
                    {
                        if (toSideboard) attemptReserves.Add(wiz2Card);
                        else attemptRecruits.Add(wiz2Card);
                    }
                    usedTitles.Add(wiz2Card.Name);
                }

                var orderedSpecs = attempt == 0
                    ? [.. rowSpecs
                        .OrderBy(r => r.Tier1.Count > 0 ? 0 : (r.Tier2.Count > 0 ? 1 : 2))
                        .ThenBy(r => r.Tier1.Count > 0 ? r.Tier1.Count : (r.Tier2.Count > 0 ? r.Tier2.Count : r.Tier3.Count))]
                    : rowSpecs.OrderBy(_ => Random.Shared.Next()).ToList();

                int t1Count = 0;
                int t2Count = 0;
                int t3Count = 0;
                bool failed = false;

                foreach (var (IsReserve, RowNum, Tier1, Tier2, Tier3) in orderedSpecs)
                {
                    var availT1 = Tier1.Where(c => !usedTitles.Contains(c.Name)).ToList();
                    Card chosen = null;

                    if (availT1.Count > 0)
                    {
                        chosen = availT1[Random.Shared.Next(availT1.Count)];
                        t1Count++;
                    }
                    else
                    {
                        var availT2 = Tier2.Where(c => !usedTitles.Contains(c.Name)).ToList();
                        if (availT2.Count > 0)
                        {
                            chosen = availT2[Random.Shared.Next(availT2.Count)];
                            t2Count++;
                        }
                        else
                        {
                            var availT3 = Tier3.Where(c => !usedTitles.Contains(c.Name)).ToList();
                            if (availT3.Count > 0)
                            {
                                chosen = availT3[Random.Shared.Next(availT3.Count)];
                                t3Count++;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        string label = IsReserve ? $"Sideboard Character Row {RowNum}" : $"Play Deck Character Row {RowNum}";
                        lastFailureReason = $"Not enough distinct matching characters to fill {label}.";
                        failed = true;
                        break;
                    }

                    if (IsReserve) attemptReserves.Add(chosen);
                    else attemptRecruits.Add(chosen);

                    if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                    {
                        usedTitles.Add(chosen.Name);
                    }
                }

                int score = (t1Count * 10000) + (t2Count * 100);
                if (!failed && score > bestScore)
                {
                    finalRecruits = attemptRecruits;
                    finalReserves = attemptReserves;
                    bestScore = score;
                    bestT1Count = t1Count;
                    bestT2Count = t2Count;
                    bestT3Count = t3Count;

                    if (bestT1Count == rowSpecs.Count)
                    {
                        break;
                    }
                }
            }

            if (finalRecruits == null || finalReserves == null)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        lastFailureReason ?? "Unable to draft Wizards & Recruits with current criteria.",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Resources.AddRange(finalRecruits);

            _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Sideboard.AddRange(finalReserves);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(finalRecruits.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            string recruitNames = finalRecruits.Count > 0 ? string.Join(", ", finalRecruits.Select(c => c.Name)) : "(None)";
            string reserveNames = finalReserves.Count > 0 ? string.Join(", ", finalReserves.Select(c => c.Name)) : "(None)";

            var tierParts = new List<string>();
            if (bestT1Count > 0) tierParts.Add($"{bestT1Count} local site(s)");
            if (bestT2Count > 0) tierParts.Add($"{bestT2Count} outside Haven(s)");
            if (bestT3Count > 0) tierParts.Add($"{bestT3Count} out-of-region");
            string tierNote = rowSpecs.Count > 0
                ? (bestT2Count == 0 && bestT3Count == 0
                    ? " [All from local sites]"
                    : $" [{string.Join(", ", tierParts)}]")
                : string.Empty;

            var wizardNotes = new List<string>();
            if (allSitesInDeck.Count > 0)
            {
                var draftedWizards = finalRecruits.Concat(finalReserves)
                    .Where(IsAvatar)
                    .Select(w => w.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var wizName in draftedWizards)
                {
                    string landSite = null;
                    if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Saruman")) landSite = "Isengard";
                    else if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Radagast")) landSite = "Rhosgobel";

                    if (landSite != null && !allSitesInDeck.Any(s => CardFilterService.EqualsWithDiacriticFallback(s.Name, landSite)))
                    {
                        wizardNotes.Add($"{wizName}'s home site ({landSite}) is not in the Site deck (deploys at a Haven).");
                    }
                }
            }

            if (wizardNotes.Count > 0)
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard{tierNote}:\n" +
                    $"• Resources: {recruitNames}  |  Sideboard: {reserveNames}\n" +
                    $"⚠ {string.Join(" ", wizardNotes)}";
            }
            else
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard{tierNote}:\n" +
                    $"• Resources: {recruitNames}\n" +
                    $"• Sideboard: {reserveNames}";
            }

            return true;
        }

        private bool RunFactionsDraft(bool silent = false)
        {
            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            // 1. Clean Factions slice + any prerequisite cards whose "Prerequisite For" matches a Faction card
            var allFactionNames = new HashSet<string>(
                allCards
                    .Where(c => string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Name),
                StringComparer.OrdinalIgnoreCase);

            bool IsFactionOrFactionPrerequisite(Card card)
            {
                if (string.Equals(card.Secondary, "Faction", StringComparison.OrdinalIgnoreCase)) return true;
                var prereqFor = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite For");
                return prereqFor.Any(p => allFactionNames.Contains(p));
            }

            _deck.Resources.RemoveAll(IsFactionOrFactionPrerequisite);
            _deck.Sideboard.RemoveAll(IsFactionOrFactionPrerequisite);

            int reqRes = (int)_numResourceFactions.Value;
            int reqSide = (int)_numSideboardFactions.Value;

            if (reqRes == 0 && reqSide == 0)
            {
                _lblFactionStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblFactionStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblFactionStatus.Text = "No factions configured (0 drafted).";
                return true;
            }

            // 2. Gather Sites and drafted character groups
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

            // 3. Build eligible faction candidates (grouped by Name, preferring Hero alignment)
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
                // 3a. Named character "Specific" check
                if (!string.IsNullOrWhiteSpace(faction.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, faction.Specific)))
                {
                    continue;
                }

                // 3b. Site Requirement check (must match at least one Site/Haven in _deck.Sites)
                var siteReqTags = _filterService.GetCardTagsForCategory(faction.Id, "Site Requirement");
                if (siteReqTags.Count == 0) continue;

                bool playableLocal = siteReqTags.Any(req => MatchesAnySiteRequirement(localSites, req, _filterService));
                bool playableOutside = !playableLocal && siteReqTags.Any(req => MatchesAnySiteRequirement(outsideHavens, req, _filterService));

                if (!playableLocal && !playableOutside) continue;

                // 3c. Resolve complete Prerequisite chain (and verify prerequisites' own Site Requirements & Specific)
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

                // 3d. Evaluate priority tier:
                // 1 = Character Faction Bonus or Specific Character Match (Pool > Resources > Sideboard)
                // 2 = Character Race Match (Pool > Resources > Sideboard)
                // 3 = Local Site vs Outside Haven
                // 4 = Remaining Eligible Factions
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

            // 4. Draft into Resources first, then Sideboard
            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Resources.Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var draftedResFactions = new List<Card>();
            var draftedResPrereqs = new List<Card>();
            int resCardsAdded = 0;

            DraftFactionsIntoSection(
                orderedCandidates,
                _deck.Resources,
                reqRes,
                resCapacity,
                usedCountsByName,
                draftedResFactions,
                draftedResPrereqs,
                ref resCardsAdded);

            var draftedSideFactions = new List<Card>();
            var draftedSidePrereqs = new List<Card>();
            int sideCardsAdded = 0;

            DraftFactionsIntoSection(
                orderedCandidates,
                _deck.Sideboard,
                reqSide,
                sideCapacity,
                usedCountsByName,
                draftedSideFactions,
                draftedSidePrereqs,
                ref sideCardsAdded);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(draftedResFactions.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            // 5. Build Status Message
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

            // First pass prioritizes distinct titles; subsequent passes only allow non-unique duplicates if needed
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

                    targetSection.Add(faction);
                    usedCountsByName[faction.Name] = curCount + 1;
                    draftedFactionsOut.Add(faction);

                    foreach (var prereq in neededPrereqs)
                    {
                        targetSection.Add(prereq);
                        usedCountsByName[prereq.Name] = usedCountsByName.GetValueOrDefault(prereq.Name, 0) + 1;
                        draftedPrereqsOut.Add(prereq);
                    }

                    totalCardsAdded += neededSlots;
                }
            }
        }

        private bool RunAlliesDraft(bool silent = false)
        {
            var allCards = _catalog.Cards;
            var activeSets = _getActiveSets();

            // 1. Clean Allies slice + any prerequisite cards whose "Prerequisite For" matches an Ally card
            var allAllyNames = new HashSet<string>(
                allCards
                    .Where(c => string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Name),
                StringComparer.OrdinalIgnoreCase);

            bool IsAllyOrAllyPrerequisite(Card card)
            {
                if (string.Equals(card.Secondary, "Ally", StringComparison.OrdinalIgnoreCase)) return true;
                var prereqFor = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite For");
                return prereqFor.Any(p => allAllyNames.Contains(p));
            }

            _deck.Resources.RemoveAll(IsAllyOrAllyPrerequisite);
            _deck.Sideboard.RemoveAll(IsAllyOrAllyPrerequisite);

            int reqRes = (int)_numResourceAllies.Value;
            int reqSide = (int)_numSideboardAllies.Value;

            if (reqRes == 0 && reqSide == 0)
            {
                _lblAllyStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblAllyStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblAllyStatus.Text = "No allies configured (0 drafted).";
                return true;
            }

            // 2. Gather Sites and drafted characters
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

            // 3. Build eligible ally candidates (grouped by Name, preferring Hero alignment)
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
                // 3a. Named character "Specific" check
                if (!string.IsNullOrWhiteSpace(ally.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, ally.Specific)))
                {
                    continue;
                }

                // 3b. Site Requirement check (must match at least one Site/Haven in _deck.Sites)
                var siteReqTags = _filterService.GetCardTagsForCategory(ally.Id, "Site Requirement");
                if (siteReqTags.Count == 0) continue;

                bool playableLocal = siteReqTags.Any(req => MatchesAnySiteRequirement(localSites, req, _filterService));
                bool playableOutside = !playableLocal && siteReqTags.Any(req => MatchesAnySiteRequirement(outsideHavens, req, _filterService));

                if (!playableLocal && !playableOutside) continue;

                // 3c. Resolve complete Prerequisite chain (future-safe for later expansions)
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

                // 3d. Prioritize local region-group sites/Havens before outside Havens
                int localRank = playableLocal ? 0 : 1;
                scoredCandidates.Add((ally, resolvedPrereqs, localRank, Random.Shared.Next()));
            }

            var orderedCandidates = scoredCandidates
                .OrderBy(x => x.LocalRank)
                .ThenBy(x => x.TieBreaker)
                .ToList();

            // 4. Draft into Resources first (preferring distinct sites from Resources Factions), then Sideboard
            var usedCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _deck.Resources.Concat(_deck.Sideboard))
            {
                usedCountsByName[c.Name] = usedCountsByName.GetValueOrDefault(c.Name, 0) + 1;
            }

            int resCapacity = Math.Max(0, (int)_numPlayDeckSize.Value - _deck.Resources.Count);
            int sideCapacity = Math.Max(0, (int)_numSideboardSize.Value - _deck.Sideboard.Count);

            var resourceFactions = _deck.Resources
                .Where(c => string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var siteOccupyingCards = new List<Card>(resourceFactions);
            int currentSiteMatches = MaxSiteAssignments(siteOccupyingCards, allSitesInDeck);

            var draftedResAllies = new List<Card>();
            var draftedResPrereqs = new List<Card>();
            var overlappingResAllies = new List<Card>();
            int resCardsAdded = 0;

            // Pass 1: Disjoint sites only (so Resource Allies and Factions don't share the same sole site)
            DraftAlliesIntoSection(
                orderedCandidates,
                _deck.Resources,
                reqRes,
                resCapacity,
                usedCountsByName,
                allSitesInDeck,
                siteOccupyingCards,
                ref currentSiteMatches,
                requireDistinctSite: true,
                draftedResAllies,
                draftedResPrereqs,
                overlappingOut: null,
                ref resCardsAdded);

            // Pass 2: Fallback only if Pass 1 couldn't fill reqRes (emits ⚠ warning so pipeline can retry)
            if (draftedResAllies.Count < reqRes)
            {
                DraftAlliesIntoSection(
                    orderedCandidates,
                    _deck.Resources,
                    reqRes,
                    resCapacity,
                    usedCountsByName,
                    allSitesInDeck,
                    siteOccupyingCards,
                    ref currentSiteMatches,
                    requireDistinctSite: false,
                    draftedResAllies,
                    draftedResPrereqs,
                    overlappingOut: overlappingResAllies,
                    ref resCardsAdded);
            }

            // Sideboard backup allies (no disjoint-site restriction)
            var draftedSideAllies = new List<Card>();
            var draftedSidePrereqs = new List<Card>();
            int sideCardsAdded = 0;
            int unusedSiteMatches = 0;

            DraftAlliesIntoSection(
                orderedCandidates,
                _deck.Sideboard,
                reqSide,
                sideCapacity,
                usedCountsByName,
                allSitesInDeck,
                siteOccupyingCards: null,
                ref unusedSiteMatches,
                requireDistinctSite: false,
                draftedSideAllies,
                draftedSidePrereqs,
                overlappingOut: null,
                ref sideCardsAdded);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(draftedResAllies.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            // 5. Build Status Message
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

            foreach (var overlapAlly in overlappingResAllies)
            {
                var allySiteNames = GetPlayableSiteIndices(overlapAlly, allSitesInDeck)
                    .Select(idx => allSitesInDeck[idx].Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var conflictingFactions = resourceFactions
                    .Where(f => GetPlayableSiteIndices(f, allSitesInDeck).Any(idx => allySiteNames.Contains(allSitesInDeck[idx].Name)))
                    .Select(f => f.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string siteList = string.Join(", ", allySiteNames);
                string factionList = conflictingFactions.Count > 0
                    ? $" with Resource Faction(s): {string.Join(", ", conflictingFactions)}"
                    : string.Empty;

                warnings.Add($"Site overlap in Resources: {overlapAlly.Name} ({siteList}) shares its playable site(s){factionList}.");
            }

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

        private void DraftAlliesIntoSection(
            List<(Card Ally, List<Card> Prerequisites, int LocalRank, int TieBreaker)> orderedCandidates,
            List<Card> targetSection,
            int requestedAllies,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName,
            List<Card> allSitesInDeck,
            List<Card> siteOccupyingCards,
            ref int currentSiteMatches,
            bool requireDistinctSite,
            List<Card> draftedAlliesOut,
            List<Card> draftedPrereqsOut,
            List<Card> overlappingOut,
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

                    int newSiteMatches = currentSiteMatches;
                    bool isDistinctSite = true;

                    if (siteOccupyingCards != null)
                    {
                        var testCards = new List<Card>(siteOccupyingCards) { Ally };
                        newSiteMatches = MaxSiteAssignments(testCards, allSitesInDeck);
                        isDistinctSite = newSiteMatches > currentSiteMatches;

                        if (requireDistinctSite && !isDistinctSite)
                        {
                            continue;
                        }
                    }

                    targetSection.Add(Ally);
                    usedCountsByName[Ally.Name] = curCount + 1;
                    draftedAlliesOut.Add(Ally);

                    if (siteOccupyingCards != null)
                    {
                        siteOccupyingCards.Add(Ally);
                        currentSiteMatches = newSiteMatches;
                        if (!isDistinctSite)
                        {
                            overlappingOut?.Add(Ally);
                        }
                    }

                    foreach (var prereq in neededPrereqs)
                    {
                        targetSection.Add(prereq);
                        usedCountsByName[prereq.Name] = usedCountsByName.GetValueOrDefault(prereq.Name, 0) + 1;
                        draftedPrereqsOut.Add(prereq);
                    }

                    totalCardsAdded += neededSlots;
                }
            }
        }

        private List<int> GetPlayableSiteIndices(Card card, List<Card> allSitesInDeck)
        {
            var indices = new List<int>();
            if (card == null || allSitesInDeck == null || allSitesInDeck.Count == 0) return indices;

            var siteReqTags = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
            if (siteReqTags.Count == 0) return indices;

            for (int i = 0; i < allSitesInDeck.Count; i++)
            {
                var site = allSitesInDeck[i];
                bool matches = siteReqTags.Any(req =>
                {
                    string cleanReq = req.Trim();
                    if (CardFilterService.TryParsePredicate(cleanReq, out _, out _, out _))
                    {
                        return CardFilterService.MatchesPredicate(site, cleanReq, _filterService);
                    }
                    return CardFilterService.EqualsWithDiacriticFallback(site.Name, cleanReq) ||
                           string.Equals(site.Name.Replace("-", " "), cleanReq.Replace("-", " "), StringComparison.OrdinalIgnoreCase);
                });

                if (matches)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private int MaxSiteAssignments(List<Card> cards, List<Card> allSitesInDeck)
        {
            if (cards == null || cards.Count == 0 || allSitesInDeck == null || allSitesInDeck.Count == 0)
            {
                return 0;
            }

            var adj = new List<int>[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                adj[i] = GetPlayableSiteIndices(cards[i], allSitesInDeck);
            }

            int[] siteOwner = new int[allSitesInDeck.Count];
            Array.Fill(siteOwner, -1);

            int matched = 0;
            for (int u = 0; u < cards.Count; u++)
            {
                bool[] visited = new bool[allSitesInDeck.Count];
                if (TryMatchSite(u, adj, siteOwner, visited))
                {
                    matched++;
                }
            }

            return matched;
        }

        private static bool TryMatchSite(int u, List<int>[] adj, int[] siteOwner, bool[] visited)
        {
            foreach (int siteIdx in adj[u])
            {
                if (visited[siteIdx]) continue;
                visited[siteIdx] = true;

                if (siteOwner[siteIdx] < 0 || TryMatchSite(siteOwner[siteIdx], adj, siteOwner, visited))
                {
                    siteOwner[siteIdx] = u;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolvePrerequisiteChain(
            Card card,
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            List<Card> allSitesInDeck,
            List<Card> allDraftedChars,
            List<Card> resolvedPrereqs,
            HashSet<string> visitedNames)
        {
            if (card == null || !visitedNames.Add(card.Name)) return true;

            var prereqTags = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite");
            if (prereqTags.Count == 0) return true;

            foreach (var prereqName in prereqTags)
            {
                var prereqCard = allCards
                    .Where(c =>
                        (activeSets == null || activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                        CardFilterService.EqualsWithDiacriticFallback(c.Name, prereqName) &&
                        IsHeroPlayableAlignment(c.Alignment))
                    .OrderBy(c => string.Equals(c.Alignment, "Hero", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .FirstOrDefault();

                if (prereqCard == null) return false;

                if (!string.IsNullOrWhiteSpace(prereqCard.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, prereqCard.Specific)))
                {
                    return false;
                }

                var prereqSiteReqs = _filterService.GetCardTagsForCategory(prereqCard.Id, "Site Requirement");
                if (prereqSiteReqs.Count > 0 &&
                    !prereqSiteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                {
                    return false;
                }

                if (!resolvedPrereqs.Any(p => CardFilterService.EqualsWithDiacriticFallback(p.Name, prereqCard.Name)))
                {
                    resolvedPrereqs.Add(prereqCard);
                }

                if (!TryResolvePrerequisiteChain(
                    prereqCard,
                    allCards,
                    activeSets,
                    allSitesInDeck,
                    allDraftedChars,
                    resolvedPrereqs,
                    visitedNames))
                {
                    return false;
                }
            }

            return true;
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

        private static bool MatchesAnySiteRequirement(List<Card> sites, string requirement, CardFilterService filterService)
        {
            if (sites == null || sites.Count == 0 || string.IsNullOrWhiteSpace(requirement)) return false;

            string cleanReq = requirement.Trim();
            if (CardFilterService.TryParsePredicate(cleanReq, out _, out _, out _))
            {
                return sites.Any(s => CardFilterService.MatchesPredicate(s, cleanReq, filterService));
            }

            return sites.Any(s =>
                CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanReq) ||
                string.Equals(s.Name.Replace("-", " "), cleanReq.Replace("-", " "), StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHeroPlayableAlignment(string alignment)
        {
            if (string.IsNullOrWhiteSpace(alignment)) return true;
            return string.Equals(alignment, "Hero", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Neutral", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Dual", StringComparison.OrdinalIgnoreCase);
        }

        private void RunPipelineDraft()
        {
            var stepsToRun = new List<(int StepNumber, string TabName, RunnableTabInfo Info)>();
            for (int i = 0; i < _pipelineOrder.Length; i++)
            {
                string tabName = _pipelineOrder[i];
                if (_tabInfo.TryGetValue(tabName, out var info))
                {
                    stepsToRun.Add((i + 1, tabName, info));
                }
            }

            if (stepsToRun.Count == 0) return;

            int existingCardCount =
                _deck.Pool.Count +
                _deck.Resources.Count +
                _deck.Hazards.Count +
                _deck.Sideboard.Count +
                _deck.Sites.Count;

            void ResetAttemptState()
            {
                _deck.Pool.Clear();
                _deck.Resources.Clear();
                _deck.Hazards.Clear();
                _deck.Sideboard.Clear();
                _deck.Sites.Clear();

                foreach (var (_, _, info) in stepsToRun)
                {
                    info.StatusLabel.Text = string.Empty;
                }
            }

            const int maxPipelineAttempts = 25;

            int bestScore = int.MinValue;
            int bestAttempt = 0;
            List<Card> bestPool = null;
            List<Card> bestResources = null;
            List<Card> bestHazards = null;
            List<Card> bestSideboard = null;
            List<Card> bestSites = null;
            List<(Label Label, string Text, Color ForeColor, Font Font)> bestStatusLabels = null;
            List<string> bestCompletedSteps = null;
            List<string> bestWarningTabs = null;

            for (int attempt = 1; attempt <= maxPipelineAttempts; attempt++)
            {
                ResetAttemptState();
                var completedSteps = new List<string>();
                int failedIndex = -1;

                for (int s = 0; s < stepsToRun.Count; s++)
                {
                    var (stepNumber, tabName, info) = stepsToRun[s];
                    if (!info.Runner(true))
                    {
                        failedIndex = s;
                        break;
                    }
                    completedSteps.Add($"{stepNumber}. {tabName} ✓");
                }

                if (failedIndex < 0)
                {
                    var warningTabs = stepsToRun
                        .Where(step => step.Info.StatusLabel.Text.Contains('⚠'))
                        .Select(step => step.TabName)
                        .ToList();

                    if (warningTabs.Count == 0)
                    {
                        NotifyDeckChanged(null);
                        UpdateDeckSummary();

                        string attemptNote = attempt > 1 ? $" (attempt {attempt}/{maxPipelineAttempts})" : string.Empty;
                        _lblPipelineStatus.ForeColor = Color.FromArgb(26, 86, 54);
                        _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                        _lblPipelineStatus.Text = $"Pipeline complete{attemptNote}: {string.Join(" -> ", completedSteps)}";
                        return;
                    }

                    int totalCards = _deck.Pool.Count + _deck.Resources.Count + _deck.Hazards.Count + _deck.Sideboard.Count + _deck.Sites.Count;
                    int score = ((stepsToRun.Count - warningTabs.Count) * 10000) + (_deck.Sites.Count * 10) + totalCards;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAttempt = attempt;
                        bestPool = [.. _deck.Pool];
                        bestResources = [.. _deck.Resources];
                        bestHazards = [.. _deck.Hazards];
                        bestSideboard = [.. _deck.Sideboard];
                        bestSites = [.. _deck.Sites];
                        bestStatusLabels = [.. stepsToRun.Select(step => (step.Info.StatusLabel, step.Info.StatusLabel.Text, step.Info.StatusLabel.ForeColor, step.Info.StatusLabel.Font))];
                        bestCompletedSteps = completedSteps;
                        bestWarningTabs = warningTabs;
                    }
                }
                else if (failedIndex == 0 && attempt == 1)
                {
                    break;
                }
            }

            if (bestPool != null)
            {
                ResetAttemptState();
                _deck.Pool.AddRange(bestPool);
                _deck.Resources.AddRange(bestResources);
                _deck.Hazards.AddRange(bestHazards);
                _deck.Sideboard.AddRange(bestSideboard);
                _deck.Sites.AddRange(bestSites);

                foreach (var (label, text, foreColor, font) in bestStatusLabels)
                {
                    label.Text = text;
                    label.ForeColor = foreColor;
                    label.Font = font;
                }

                NotifyDeckChanged(null);
                UpdateDeckSummary();

                _lblPipelineStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblPipelineStatus.Text =
                    $"Pipeline complete with warnings (best from attempt {bestAttempt}/{maxPipelineAttempts}): {string.Join(" -> ", bestCompletedSteps)}\n" +
                    $"⚠ See notice(s) on: {string.Join(", ", bestWarningTabs)}";
                return;
            }

            ResetAttemptState();
            NotifyDeckChanged(null);

            var finalCompleted = new List<string>();
            foreach (var (stepNumber, tabName, info) in stepsToRun)
            {
                if (!info.Runner(false))
                {
                    string priorNote = finalCompleted.Count > 0
                        ? $" Completed: {string.Join(" -> ", finalCompleted)}."
                        : " No steps completed.";

                    _lblPipelineStatus.ForeColor = Color.Firebrick;
                    _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    _lblPipelineStatus.Text = $"⚠ Pipeline stopped at Step {stepNumber} ({tabName}) due to a hard lock.{priorNote}";
                    NotifyDeckChanged(null);
                    UpdateDeckSummary();
                    return;
                }

                finalCompleted.Add($"{stepNumber}. {tabName} ✓");
            }

            NotifyDeckChanged(null);
            UpdateDeckSummary();

            _lblPipelineStatus.ForeColor = Color.FromArgb(26, 86, 54);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _lblPipelineStatus.Text = $"Pipeline complete: {string.Join(" -> ", finalCompleted)}";
        }

        #endregion

        #region HELPERS

        private static Card FindHeroPreferredCard(
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            string primary,
            string name)
        {
            if (allCards == null || string.IsNullOrWhiteSpace(name)) return null;

            var inActiveSets = allCards
                .Where(c =>
                    (activeSets == null || activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, name))
                .ToList();

            if (inActiveSets.Count > 0)
            {
                return PickHeroPreferredFromGroup(inActiveSets);
            }

            var anywhere = allCards
                .Where(c =>
                    string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, name))
                .ToList();

            return PickHeroPreferredFromGroup(anywhere);
        }

        private static Card PickHeroPreferredFromGroup(IEnumerable<Card> group)
        {
            var list = group?.ToList();
            if (list == null || list.Count == 0) return null;

            return list.FirstOrDefault(c => string.Equals(c.Alignment, "Hero", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(c => string.Equals(c.Alignment, "Neutral", StringComparison.OrdinalIgnoreCase))
                ?? list[0];
        }

        private static void ExpandClusterToTarget(
            Dictionary<string, HashSet<string>> graph,
            List<string> cluster,
            int targetCount,
            HashSet<string> allowedRegions)
        {
            while (cluster.Count < targetCount)
            {
                var candidateNeighbors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var reg in cluster)
                {
                    if (graph.TryGetValue(reg, out var neighbors))
                    {
                        foreach (var n in neighbors)
                        {
                            if (!cluster.Contains(n, StringComparer.OrdinalIgnoreCase) &&
                                (allowedRegions == null || allowedRegions.Contains(n)))
                            {
                                candidateNeighbors.Add(n);
                            }
                        }
                    }
                }

                if (candidateNeighbors.Count == 0)
                {
                    break;
                }

                var candidatesList = candidateNeighbors.ToList();
                cluster.Add(candidatesList[Random.Shared.Next(candidatesList.Count)]);
            }
        }

        private static bool MatchesAnySiteInList(Card character, List<Card> siteList, CardFilterService filterService)
        {
            if (siteList == null || siteList.Count == 0 || string.IsNullOrWhiteSpace(character.Home))
            {
                return false;
            }

            string cleanHome = character.Home.Trim();
            return siteList.Any(s =>
                CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                CardFilterService.MatchesPredicate(s, cleanHome, filterService));
        }

        private List<Card> GetCandidatesForCharacterRow(
            CharacterRowControls row,
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            HashSet<string> allowedRegions,
            List<Card> allowedSites)
        {
            bool checkRegions = allowedRegions != null && allowedRegions.Count > 0;
            bool checkSites = allowedSites != null && allowedSites.Count > 0;

            return [.. allCards.Where(c =>
            {
                if (!string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)) return false;
                if (activeSets.Count > 0 && !activeSets.Contains(c.Set)) return false;
                if (IsAvatar(c)) return false;

                if (row.CmbSkill.SelectedIndex > 0)
                {
                    string targetSkill = row.CmbSkill.SelectedItem.ToString();
                    if (!CardFilterService.MatchesCardnumCriterion(c, "Skill", targetSkill)) return false;
                }

                if (row.CmbSpecies.SelectedIndex > 0)
                {
                    string targetSpecies = row.CmbSpecies.SelectedItem.ToString();
                    if (!CardFilterService.MatchesCardnumCriterion(c, "Race", targetSpecies)) return false;
                }

                if (!string.IsNullOrWhiteSpace(row.TxtMind.Text))
                {
                    if (!DeckGenerationService.EvaluateNumericAttributeComparison(c, "Mind", row.TxtMind.Text.Trim())) return false;
                }

                if (!string.IsNullOrWhiteSpace(row.TxtDI.Text))
                {
                    if (!DeckGenerationService.EvaluateNumericAttributeComparison(c, "Direct", row.TxtDI.Text.Trim())) return false;
                }

                if (checkRegions || checkSites)
                {
                    bool matchedLocation = false;

                    if (checkRegions)
                    {
                        var homeRegionTags = _filterService.GetCardTagsForCategory(c.Id, "Home Region");
                        if (homeRegionTags.Any(t => allowedRegions.Any(ar => CardFilterService.EqualsWithDiacriticFallback(t.Trim(), ar))))
                        {
                            matchedLocation = true;
                        }

                        if (!matchedLocation && !string.IsNullOrWhiteSpace(c.Home))
                        {
                            string cleanHome = c.Home.Trim();
                            var homeSite = allCards.FirstOrDefault(s =>
                                string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                                (CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                                 string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase)));

                            if (homeSite != null && !string.IsNullOrWhiteSpace(homeSite.Region) &&
                                allowedRegions.Any(ar => CardFilterService.EqualsWithDiacriticFallback(homeSite.Region.Trim(), ar)))
                            {
                                matchedLocation = true;
                            }
                        }
                    }

                    if (!matchedLocation && checkSites && MatchesAnySiteInList(c, allowedSites, _filterService))
                    {
                        matchedLocation = true;
                    }

                    if (!matchedLocation) return false;
                }

                return true;
            })];
        }

        private static bool IsAvatar(Card card)
        {
            if (card == null) return false;
            return string.Equals(card.Secondary, "Avatar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase);
        }

        private bool SatisfiesRecruitSiteRequirement(Card character, List<Card> allSitesInDeck)
        {
            var siteReqs = _filterService.GetCardTagsForCategory(character.Id, "Site Requirement");
            if (siteReqs.Count == 0) return true;
            return siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService));
        }

        private static int ParseCardMind(string mindStr)
        {
            if (string.IsNullOrWhiteSpace(mindStr)) return 0;
            string clean = mindStr.Trim('(', ')').Trim();
            return int.TryParse(clean, out int val) ? val : 0;
        }

        #endregion
    }

    internal sealed class CharacterRowControls
    {
        public ComboBox CmbSkill { get; set; }
        public ComboBox CmbSpecies { get; set; }
        public TextBox TxtMind { get; set; }
        public TextBox TxtDI { get; set; }
        public Button BtnClear { get; set; }

        public bool IsActive =>
            CmbSkill.SelectedIndex > 0 ||
            CmbSpecies.SelectedIndex > 0 ||
            !string.IsNullOrWhiteSpace(TxtMind.Text) ||
            !string.IsNullOrWhiteSpace(TxtDI.Text);

        public void Clear()
        {
            CmbSkill.SelectedIndex = 0;
            CmbSpecies.SelectedIndex = 0;
            TxtMind.Text = string.Empty;
            TxtDI.Text = string.Empty;
        }
    }

    public sealed class CharacterRowSettings
    {
        public string Skill { get; set; } = "Any";
        public string Species { get; set; } = "Any";
        public string Mind { get; set; } = string.Empty;
        public string DI { get; set; } = string.Empty;
    }

    public sealed class DeckAssemblySettings
    {
        public string Version { get; set; } = "1.0";
        public string DeckTitle { get; set; } = "New Deck";
        public int PlayDeckSize { get; set; } = 30;
        public int SideboardSize { get; set; } = 20;

        // Tab 2: Regions & Sites
        public string SeedRegion { get; set; } = "Random";
        public int RegionCount { get; set; } = 4;
        public int MinSiteCount { get; set; } = 4;
        public int MaxSiteCount { get; set; } = 12;
        public List<string> SelectedRegionTypes { get; set; } = [];
        public List<string> SelectedSiteTypes { get; set; } = [];

        // Tab 3: Starting Company
        public List<CharacterRowSettings> StarterRows { get; set; } = [];

        // Tab 4: Wizards & Recruits
        public string PrimaryWizard { get; set; } = "(None)";
        public int PrimaryWizardCopies { get; set; } = 1;
        public string SecondaryWizard { get; set; } = "(None)";
        public int SecondaryWizardCopies { get; set; } = 1;
        public string SecondaryWizardDestination { get; set; } = "Sideboard";

        public List<CharacterRowSettings> RecruitRows { get; set; } = [];
        public List<CharacterRowSettings> ReserveRows { get; set; } = [];

        // Tab 5: Factions
        public int ResourceFactionCount { get; set; } = 1;
        public int SideboardFactionCount { get; set; } = 1;

        // Tab 6: Allies
        public int ResourceAllyCount { get; set; } = 1;
        public int SideboardAllyCount { get; set; } = 1;
    }
}