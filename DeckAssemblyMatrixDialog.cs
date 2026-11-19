using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace MECCG_Deck_Builder
{
    public sealed class DeckAssemblyMatrixDialog : Form
    {
        private static readonly string[] HeroHavens = ["Rivendell", "Lórien", "Grey Havens", "Edhellond"];

        private static readonly HashSet<string> CanonicalHoldTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Haven", "Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Wizardhaven"
        };

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

        private const string ItemIdleStatusText =
            "Minor, Major, Greater, and non-Ring/non-Palantír Special Items are drafted if playable at drafted Sites/Havens.\n" +
            "Pool Minor Items (0-2) are drafted first; all items obey Role Focus, Region/Site Requirements, Specific, Prerequisite, and Hero Target Species.";

        private const string RingsIdleStatusText =
            "Gold Rings require at least one GoldRing-playable site in the drafted Site deck.\n" +
            "Allow Ring Test and Aid Ring Test counts are totals (including Gandalf / Scroll of Isildur if already drafted).";

        private const string PalantirIdleStatusText =
            "Palantíri require a GreaterItem- or Palantiri-playable site (or their printed Site Requirement) in the drafted Site deck.\n" +
            "Allow Palantír Use counts are totals (including Saruman counted once if already drafted in Resources/Sideboard).";

        private const string QuestIdleStatusText =
            "Drafts Role = Quest cards (events and Quest Special Items) playable at drafted Sites/Havens.\n" +
            "Drafts missing Prerequisite cards (e.g. Men of Lamedon, Narsil && Reforging) into the same section if capacity permits.";

        private const string ResourceEventsIdleStatusText =
            "Fills remaining Play Deck (Resources) capacity up to Play Deck Size, plus requested Sideboard events.\n" +
            "Drafts Environment cards (Gates of Morning, Twilight) first, then distributes remaining slots proportionately across checked Role Focus tags.";

        private const string CreaturesIdleStatusText =
            "Drafts Nazgûl, Nazgûl Support events, Standard Creatures (1.0), and non-Nazgûl Dual-Mode Half-Creatures (0.5).\n" +
            "Uses proportionate Species / Race buckets with variety-first sampling; all Unique creatures remain eligible.";

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

        private static readonly HashSet<string> HeroTargetSpecies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dúnadan", "Dwarf", "Elf", "Hobbit", "Man", "Wizard"
        };

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
        private readonly string[] _pipelineOrder =
        [
            "Regions & Sites", "Starting Company", "Wizards & Recruits", "Factions",
            "Allies", "Items", "Rings", "Palantíri", "Quests", "Resource Events",
            "Creatures"
        ];

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
        private readonly NumericUpDown _numDecksBeforeCouncil;
        private readonly CheckBox _chkTailorHazardsToCurrentDeck;

        // --- TAB 2: REGIONS & SITES ---
        private readonly ComboBox _cmbSeedRegion;
        private readonly NumericUpDown _numRegionCount;
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

        // --- TAB 7: ITEMS ---
        private readonly NumericUpDown _numPoolMinorItems;
        private readonly NumericUpDown _numResourceMinorItems;
        private readonly NumericUpDown _numSideboardMinorItems;
        private readonly NumericUpDown _numResourceMajorItems;
        private readonly NumericUpDown _numSideboardMajorItems;
        private readonly NumericUpDown _numResourceGreaterItems;
        private readonly NumericUpDown _numSideboardGreaterItems;
        private readonly NumericUpDown _numResourceSpecialItems;
        private readonly NumericUpDown _numSideboardSpecialItems;
        private readonly CheckedListBox _clbItemRoles;
        private readonly Label _lblItemStatus;

        // --- TAB 8: RINGS ---
        private readonly NumericUpDown _numResGoldRings;
        private readonly NumericUpDown _numSideGoldRings;
        private readonly NumericUpDown _numResAllowRingTest;
        private readonly NumericUpDown _numSideAllowRingTest;
        private readonly NumericUpDown _numResAidRingTest;
        private readonly NumericUpDown _numSideAidRingTest;
        private readonly NumericUpDown _numResOneRing;
        private readonly NumericUpDown _numSideOneRing;
        private readonly NumericUpDown _numResDwarvenRings;
        private readonly NumericUpDown _numSideDwarvenRings;
        private readonly NumericUpDown _numResMagicRings;
        private readonly NumericUpDown _numSideMagicRings;
        private readonly NumericUpDown _numResLesserRings;
        private readonly NumericUpDown _numSideLesserRings;
        private readonly NumericUpDown _numResWizardsRing;
        private readonly NumericUpDown _numSideWizardsRing;
        private readonly Label _lblRingsStatus;

        private readonly CheckedListBox _clbRingRoles;

        // --- TAB 9: PALANTIRI ---
        private readonly NumericUpDown _numResPalantiri;
        private readonly NumericUpDown _numSidePalantiri;
        private readonly NumericUpDown _numResAllowPalantirUse;
        private readonly NumericUpDown _numSideAllowPalantirUse;
        private readonly CheckedListBox _clbPalantirRoles;
        private readonly Label _lblPalantiriStatus;

        // --- TAB 10: QUESTS ---
        private readonly NumericUpDown _numResourceQuests;
        private readonly NumericUpDown _numSideboardQuests;
        private readonly CheckBox _chkAutoDraftOneRingPackage;
        private readonly CheckedListBox _clbQuestRoles;
        private readonly Label _lblQuestsStatus;

        // --- TAB 11: RESOURCE EVENTS ---
        private readonly NumericUpDown _numResGatesOfMorning;
        private readonly NumericUpDown _numSideGatesOfMorning;
        private readonly NumericUpDown _numResTwilight;
        private readonly NumericUpDown _numSideTwilight;
        private readonly NumericUpDown _numSideboardResourceEvents;
        private readonly CheckedListBox _clbResourceEventRoles;
        private readonly Label _lblResourceEventsStatus;

        // --- TAB 12: CREATURES ---
        private readonly NumericUpDown _numHazNazgul;
        private readonly NumericUpDown _numSideNazgul;
        private readonly NumericUpDown _numHazNazgulSupport;
        private readonly NumericUpDown _numSideNazgulSupport;
        private readonly NumericUpDown _numHazStandardCreatures;
        private readonly NumericUpDown _numSideStandardCreatures;
        private readonly NumericUpDown _numHazDualCreatures;
        private readonly NumericUpDown _numSideDualCreatures;
        private readonly Label _lblCreatureEquivReadout;
        private readonly CheckedListBox _clbCreatureSpecies;
        private readonly CheckedListBox _clbCreatureRegionTypes;
        private readonly CheckedListBox _clbCreatureSiteTypes;
        private readonly Button _btnAllCreatureKeying;
        private readonly Button _btnClearCreatureKeying;
        private readonly Label _lblCreatureKeyingNote;
        private readonly Label _lblCreaturesStatus;

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
            ClientSize = new Size(860, 690);
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
                Size = new Size(810, 224)
            };

            var lblTitle = new Label { Text = "Deck Title:", Location = new Point(16, 24), Size = new Size(135, 20) };
            _txtDeckTitle = new TextBox
            {
                Text = _deck.Title,
                Location = new Point(155, 21),
                Size = new Size(400, 23)
            };
            _txtDeckTitle.TextChanged += (s, e) =>
            {
                _deck.Title = _txtDeckTitle.Text;
                NotifyDeckChanged();
            };

            var lblPlaySize = new Label { Text = "Play Deck Size:", Location = new Point(16, 56), Size = new Size(135, 20) };
            _numPlayDeckSize = new NumericUpDown
            {
                Location = new Point(155, 53),
                Size = new Size(60, 23),
                Minimum = 20,
                Maximum = 60,
                Value = 30
            };
            _numPlayDeckSize.ValueChanged += (s, e) =>
            {
                UpdateDeckSummary();
                UpdateCreatureEquivalentsReadout();
                ValidateWorkbenchReadiness();
            };

            var lblPlaySizeHint = new Label
            {
                Text = "cards in Resources and equal cards in Hazards (Official tournament standard is 30/30).",
                Location = new Point(225, 56),
                Size = new Size(560, 20),
                ForeColor = Color.DimGray
            };

            var lblSideSize = new Label { Text = "Sideboard Size:", Location = new Point(16, 88), Size = new Size(135, 20) };
            _numSideboardSize = new NumericUpDown
            {
                Location = new Point(155, 85),
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
                Location = new Point(225, 88),
                Size = new Size(560, 20),
                ForeColor = Color.DimGray
            };

            var lblDecksBeforeCouncil = new Label { Text = "Decks Before Council:", Location = new Point(16, 120), Size = new Size(135, 20) };
            _numDecksBeforeCouncil = new NumericUpDown
            {
                Location = new Point(155, 117),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 4,
                Value = 1
            };
            _toolTip.SetToolTip(
                _numDecksBeforeCouncil,
                "Planned deck cycles before calling the Free Council.\n" +
                "At 1, Allies avoid sharing a site with Resources Factions; at >= 2, tapped sites refresh on exhaust.");

            var lblDecksHint = new Label
            {
                Text = "deck cycle(s) planned (1: distinct sites for Factions/Allies; 2+: sites refresh on exhaust).",
                Location = new Point(225, 120),
                Size = new Size(575, 20),
                ForeColor = Color.DimGray
            };

            var lblHazardTargeting = new Label { Text = "Hazard Targeting:", Location = new Point(16, 152), Size = new Size(135, 20) };
            _chkTailorHazardsToCurrentDeck = new CheckBox
            {
                Text = "Tailor Hazards to current deck (Sites, Regions && Resources)",
                Location = new Point(155, 150),
                Size = new Size(440, 22),
                Checked = false
            };
            _toolTip.SetToolTip(
                _chkTailorHazardsToCurrentDeck,
                "Unchecked: Target an unknown opponent using manual Hazard tab filters.\n" +
                "Checked: Target the Sites, Regions, and Resources of this deck (for paired/swapped two-deck play).");
            _chkTailorHazardsToCurrentDeck.CheckedChanged += (s, e) =>
            {
                UpdateCreatureKeyingEnabledState();
                ValidateWorkbenchReadiness();
            };

            var lblProfiles = new Label
            {
                Text = "Profile Settings:",
                Location = new Point(16, 189),
                Size = new Size(135, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            var btnSaveSettings = new Button
            {
                Text = "Save Settings...",
                Location = new Point(155, 186),
                Size = new Size(120, 26)
            };
            btnSaveSettings.Click += (s, e) => SaveSettingsToFile();

            var btnLoadSettings = new Button
            {
                Text = "Load Settings...",
                Location = new Point(283, 186),
                Size = new Size(120, 26)
            };
            btnLoadSettings.Click += (s, e) => LoadSettingsFromFile();

            var lblProfileHint = new Label
            {
                Text = "Save or load workbench configurations (.workbench.json) to reuse tab criteria.",
                Location = new Point(415, 183),
                Size = new Size(385, 34),
                ForeColor = Color.DimGray
            };

            grpGeneral.Controls.AddRange([
                lblTitle, _txtDeckTitle,
                lblPlaySize, _numPlayDeckSize, lblPlaySizeHint,
                lblSideSize, _numSideboardSize, lblSideSizeHint,
                lblDecksBeforeCouncil, _numDecksBeforeCouncil, lblDecksHint,
                lblHazardTargeting, _chkTailorHazardsToCurrentDeck,
                lblProfiles, btnSaveSettings, btnLoadSettings, lblProfileHint
            ]);

            var grpPipeline = new GroupBox
            {
                Text = "Deck Assembly Pipeline && Session Snapshots",
                Location = new Point(16, 244),
                Size = new Size(810, 152)
            };

            var lblOrder = new Label
            {
                Text = "Execution Order:   1. Regions & Sites -> 2. Starting Company -> 3. Wizards & Recruits -> 4. Factions -> 5. Allies -> 6. Items -> 7. Rings -> 8. Palantíri -> 9. Quests -> 10. Resource Events -> 11. Creatures",
                Location = new Point(16, 20),
                Size = new Size(778, 34),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93),
                UseMnemonic = false
            };

            _btnRunPipeline = new Button
            {
                Text = "Run Pipeline",
                Location = new Point(16, 58),
                Size = new Size(125, 30),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunPipeline.Click += (s, e) => RunPipelineDraft();
            _toolTip.SetToolTip(
                _btnRunPipeline,
                "Drafts the full deck in sequence (Regions & Sites -> Starting Company -> Wizards & Recruits -> Factions -> Allies -> Items -> Rings -> Palantíri -> Quests -> Resource Events -> Creatures).\n" +
                "Automatically retries up to 25 times if random region rolls fail downstream constraints.");

            _btnClearDeck = new Button
            {
                Text = "Clear Deck",
                Location = new Point(149, 58),
                Size = new Size(110, 30)
            };
            _btnClearDeck.Click += BtnClearDeck_Click;
            _toolTip.SetToolTip(_btnClearDeck, "Clears all cards across Pool, Resources, Hazards, Sideboard, and Sites.");

            _btnSaveSnapshot = new Button
            {
                Text = "Save Snapshot",
                Location = new Point(280, 58),
                Size = new Size(125, 30)
            };
            _btnSaveSnapshot.Click += BtnSaveSnapshot_Click;
            _toolTip.SetToolTip(_btnSaveSnapshot, "Saves an in-session snapshot of all currently drafted deck lists so you can re-roll and restore.");

            _btnRecallSnapshot = new Button
            {
                Text = "Recall Snapshot",
                Location = new Point(413, 58),
                Size = new Size(125, 30),
                Enabled = false
            };
            _btnRecallSnapshot.Click += BtnRecallSnapshot_Click;
            _toolTip.SetToolTip(_btnRecallSnapshot, "Restores the complete deck from your last saved session snapshot.");

            _lblPipelineStatus = new Label
            {
                Location = new Point(16, 96),
                Size = new Size(778, 48),
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
                Location = new Point(16, 404),
                Size = new Size(810, 244)
            };

            _lblDeckSummary = new Label
            {
                Location = new Point(20, 22),
                Size = new Size(770, 214),
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
                Value = 5
            };
            string regCountTip =
                "Target number of connected (border-to-border) regions to draft.\n" +
                "Minimum is 4, allowing most local site-to-site moves within a single turn.";
            _toolTip.SetToolTip(lblRegCount, regCountTip);
            _toolTip.SetToolTip(_numRegionCount, regCountTip);

            var lblMaxSites = new Label { Text = "Max Adventure Sites:", Location = new Point(12, 91), Size = new Size(134, 20) };
            _numMaxSiteCount = new NumericUpDown
            {
                Location = new Point(150, 88),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 50,
                Value = 12
            };
            string maxSitesTip =
                "Maximum number of matching adventure sites (non-Haven sites) to draft.\n" +
                "Prefers a connected region group within Max; if all attempts exceed Max, keeps a random subset up to Max.";
            _toolTip.SetToolTip(lblMaxSites, maxSitesTip);
            _toolTip.SetToolTip(_numMaxSiteCount, maxSitesTip);

            var lblTheaterRulesNote = new Label
            {
                Location = new Point(12, 126),
                Size = new Size(390, 86),
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

            foreach (var row in _starterRows)
            {
                row.TxtMind.Text = ">=1";
            }

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
            _cmbWizard1.SelectedIndex = 1; // Random

            var lblWiz1Cop = new Label { Text = "Copies:", Location = new Point(275, 25), Size = new Size(50, 18) };
            _cmbWizard1Copies = new ComboBox
            {
                Location = new Point(325, 22),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard1Copies.Items.AddRange(["1", "2", "3"]);
            _cmbWizard1Copies.SelectedIndex = 1; // 2 copies

            var lblWiz2 = new Label { Text = "Secondary Wizard:", Location = new Point(12, 53), Size = new Size(105, 18) };
            _cmbWizard2 = new ComboBox
            {
                Location = new Point(120, 50),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard2.SelectedIndex = 1; // Random

            var lblWiz2Cop = new Label { Text = "Copies:", Location = new Point(275, 53), Size = new Size(50, 18) };
            _cmbWizard2Copies = new ComboBox
            {
                Location = new Point(325, 50),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Copies.Items.AddRange(["1", "2"]);
            _cmbWizard2Copies.SelectedIndex = 0; // 1 copy

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
            WizardSelection_Changed(null, null);

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

            // ==========================================
            // TAB 7: ITEMS
            // ==========================================
            var tabItems = new TabPage("Items");

            var grpItemCounts = new GroupBox
            {
                Text = "Item Drafting Counts",
                Location = new Point(16, 16),
                Size = new Size(495, 220)
            };

            var lblHdrItemType = new Label { Text = "Item Tier", Location = new Point(16, 24), Size = new Size(130, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrPool = new Label { Text = "Pool (0-2)", Location = new Point(156, 24), Size = new Size(78, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrRes = new Label { Text = "Resources", Location = new Point(260, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrSide = new Label { Text = "Sideboard", Location = new Point(365, 24), Size = new Size(85, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            NumericUpDown CreateItemSpinner(int x, int y, int min, int max, int defVal)
            {
                var nud = new NumericUpDown
                {
                    Location = new Point(x, y),
                    Size = new Size(64, 23),
                    Minimum = min,
                    Maximum = max,
                    Value = defVal
                };
                nud.ValueChanged += (s, e) => ValidateWorkbenchReadiness();
                return nud;
            }

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
                Location = new Point(16, 188),
                Size = new Size(465, 24),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                UseMnemonic = false,
                Text = "Excludes Gold Rings, Ring Special Items, Palantíri, and Quest Items (reserved for dedicated tabs)."
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
                Size = new Size(300, 220)
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
                Location = new Point(16, 250),
                Size = new Size(805, 220),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = ItemIdleStatusText
            };

            tabItems.Controls.AddRange([grpItemCounts, grpItemRoles, _lblItemStatus]);

            // ==========================================
            // TAB 8: RINGS
            // ==========================================
            var tabRings = new TabPage("Rings");

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
            (_numResAllowRingTest, _numSideAllowRingTest) = AddRingRow(1, "Allow Ring Test:", 9, 0, 6, 0, "Total desired (counts Gandalf once if already drafted in Resources/Sideboard).");
            (_numResAidRingTest, _numSideAidRingTest) = AddRingRow(2, "Aid Ring Test:", 6, 0, 6, 0, "Total desired (includes Scroll of Isildur if already drafted in Items).");
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

            // ==========================================
            // TAB 9: PALANTIRI
            // ==========================================
            var tabPalantiri = new TabPage("Palantíri");

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

            // ==========================================
            // TAB 10: QUESTS
            // ==========================================
            var tabQuests = new TabPage("Quests");

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

            // ==========================================
            // TAB 11: RESOURCE EVENTS
            // ==========================================
            var tabResourceEvents = new TabPage("Resource Events");

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
                Text = "Role Focus (Uncheck to exclude)",
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

            _lblResourceEventsStatus = new Label
            {
                Location = new Point(16, 320),
                Size = new Size(805, 290),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = ResourceEventsIdleStatusText
            };

            tabResourceEvents.Controls.AddRange([grpEnv, grpAutoFill, grpResEventRoles, _lblResourceEventsStatus]);

            // ==========================================
            // TAB 12: CREATURES
            // ==========================================
            var tabCreatures = new TabPage("Creatures");

            var grpNazgul = new GroupBox
            {
                Text = "Nazgûl Package (Hazards && Sideboard)",
                Location = new Point(16, 14),
                Size = new Size(475, 116)
            };

            var lblNazHdrCat = new Label { Text = "Card Group", Location = new Point(16, 22), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblNazHdrHaz = new Label { Text = "Hazards", Location = new Point(190, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblNazHdrSide = new Label { Text = "Sideboard", Location = new Point(282, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            NumericUpDown CreateCreatureSpinner(int x, int y, int min, int max, int defVal)
            {
                var nud = CreateItemSpinner(x, y, min, max, defVal);
                nud.ValueChanged += (s, e) => UpdateCreatureEquivalentsReadout();
                return nud;
            }

            var lblNazgul = new Label { Text = "Nazgûl (0.5 equiv):", Location = new Point(16, 49), Size = new Size(165, 20) };
            _numHazNazgul = CreateCreatureSpinner(190, 46, 0, 9, 0);
            _numSideNazgul = CreateCreatureSpinner(282, 46, 0, 9, 0);
            var lblNazNote = new Label { Text = "Unique (<= 9 total)", Location = new Point(358, 49), Size = new Size(110, 20), ForeColor = Color.DimGray };
            _toolTip.SetToolTip(_numHazNazgul, "Unique Nazgûl (Race = Nazgûl; 0.5 creature equivalent each). Combined Hazards + Sideboard <= 9.");
            _toolTip.SetToolTip(_numSideNazgul, "Unique Nazgûl in Sideboard (combined Hazards + Sideboard <= 9).");

            var lblNazSup = new Label { Text = "Nazgûl Support:", Location = new Point(16, 81), Size = new Size(165, 20) };
            _numHazNazgulSupport = CreateCreatureSpinner(190, 78, 0, 15, 0);
            _numSideNazgulSupport = CreateCreatureSpinner(282, 78, 0, 10, 0);
            var lblNazSupNote = new Label { Text = "Target: Nazgûl", Location = new Point(358, 81), Size = new Size(110, 20), ForeColor = Color.DimGray };
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

            var lblCrHdrCat = new Label { Text = "Creature Tier", Location = new Point(16, 22), Size = new Size(165, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblCrHdrHaz = new Label { Text = "Hazards", Location = new Point(190, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblCrHdrSide = new Label { Text = "Sideboard", Location = new Point(282, 22), Size = new Size(80, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            var lblStdCr = new Label { Text = "Standard Creatures (1.0):", Location = new Point(16, 49), Size = new Size(165, 20) };
            _numHazStandardCreatures = CreateCreatureSpinner(190, 46, 0, 30, 12);
            _numSideStandardCreatures = CreateCreatureSpinner(282, 46, 0, 15, 2);
            var lblStdNote = new Label { Text = "Secondary = Creature", Location = new Point(358, 49), Size = new Size(112, 20), ForeColor = Color.DimGray };

            var lblDualCr = new Label { Text = "Dual-Mode Half (0.5):", Location = new Point(16, 81), Size = new Size(165, 20) };
            _numHazDualCreatures = CreateCreatureSpinner(190, 78, 0, 12, 0);
            _numSideDualCreatures = CreateCreatureSpinner(282, 78, 0, 10, 0);
            var lblDualNote = new Label { Text = "Non-Nazgûl duals", Location = new Point(358, 81), Size = new Size(112, 20), ForeColor = Color.DimGray };
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
                Text = "Species / Race Focus (Uncheck to exclude)",
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

            _lblCreaturesStatus = new Label
            {
                Location = new Point(16, 318),
                Size = new Size(475, 290),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = CreaturesIdleStatusText
            };

            tabCreatures.Controls.AddRange([
                grpNazgul, grpCreatures,
                grpCreatureSpecies, grpCreatureKeying, _lblCreatureKeyingNote,
                _lblCreaturesStatus
            ]);

            // Tabs ordered to match pipeline execution order
            // Tabs ordered to match pipeline execution order
            _tabControl.TabPages.AddRange([
                tabGeneral, tabTheater, tabStarter, tabRecruits, tabFactions,
                tabAllies, tabItems, tabRings, tabPalantiri, tabQuests, tabResourceEvents,
                tabCreatures
            ]);
            _tabInfo["Regions & Sites"] = new(RunRegionsAndSitesDraft, _lblTheaterStatus);
            _tabInfo["Starting Company"] = new(RunStartingCompanyDraft, _lblStarterStatus);
            _tabInfo["Wizards & Recruits"] = new(RunNonStartingCharactersDraft, _lblRecruitStatus);
            _tabInfo["Factions"] = new(RunFactionsDraft, _lblFactionStatus);
            _tabInfo["Allies"] = new(RunAlliesDraft, _lblAllyStatus);
            _tabInfo["Items"] = new(RunItemsDraft, _lblItemStatus);
            _tabInfo["Rings"] = new(RunRingsDraft, _lblRingsStatus);
            _tabInfo["Palantíri"] = new(RunPalantiriDraft, _lblPalantiriStatus);
            _tabInfo["Quests"] = new(RunQuestsDraft, _lblQuestsStatus);
            _tabInfo["Resource Events"] = new(RunResourceEventsDraft, _lblResourceEventsStatus);
            _tabInfo["Creatures"] = new(RunCreaturesDraft, _lblCreaturesStatus);

            Controls.Add(_tabControl);

            VisibleChanged += (s, e) =>
            {
                if (Visible)
                {
                    PopulateSeedRegionDropdown();
                    PopulateItemRolesChecklist();
                    PopulateRingRolesChecklist();
                    PopulatePalantirRolesChecklist();
                    PopulateQuestRolesChecklist();
                    PopulateResourceEventRolesChecklist();
                    PopulateCreatureSpeciesChecklist();
                    UpdateCreatureKeyingEnabledState();
                    UpdateCreatureEquivalentsReadout();
                    UpdateDeckSummary();
                    ValidateWorkbenchReadiness();
                }
            };

            PopulateSeedRegionDropdown();
            PopulateItemRolesChecklist();
            PopulateRingRolesChecklist();
            PopulatePalantirRolesChecklist();
            PopulateQuestRolesChecklist();
            PopulateResourceEventRolesChecklist();
            PopulateCreatureSpeciesChecklist();
            UpdateCreatureKeyingEnabledState();
            UpdateCreatureEquivalentsReadout();
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
            int poolItems = _deck.Pool.Count(c => IsItemCard(c) && !IsRingPackageCard(c) && !IsPalantirPackageCard(c) && !IsQuestPackageCard(c));
            int resItems = _deck.Resources.Count(c => IsItemCard(c) && !IsRingPackageCard(c) && !IsPalantirPackageCard(c) && !IsQuestPackageCard(c));
            int sideItems = _deck.Sideboard.Count(c => IsItemCard(c) && !IsRingPackageCard(c) && !IsPalantirPackageCard(c) && !IsQuestPackageCard(c));
            int resRingCards = _deck.Resources.Count(IsRingPackageCard);
            int sideRingCards = _deck.Sideboard.Count(IsRingPackageCard);
            int resPalantirCards = _deck.Resources.Count(IsPalantirPackageCard);
            int sidePalantirCards = _deck.Sideboard.Count(IsPalantirPackageCard);
            int resQuests = _deck.Resources.Count(IsQuestPackageCard);
            int sideQuests = _deck.Sideboard.Count(IsQuestPackageCard);
            int resEvents = _deck.Resources.Count(IsGeneralResourceEvent);
            int sideEvents = _deck.Sideboard.Count(IsGeneralResourceEvent);
            int hazNazgul = _deck.Hazards.Count(IsNazgulCard);
            int sideNazgul = _deck.Sideboard.Count(IsNazgulCard);
            int hazNazgulSupport = _deck.Hazards.Count(IsNazgulSupportCard);
            int sideNazgulSupport = _deck.Sideboard.Count(IsNazgulSupportCard);
            int hazStandardCreatures = _deck.Hazards.Count(IsStandardCreatureCard);
            int sideStandardCreatures = _deck.Sideboard.Count(IsStandardCreatureCard);
            int hazDualCreatures = _deck.Hazards.Count(IsDualModeNonNazgulCreature);
            int sideDualCreatures = _deck.Sideboard.Count(IsDualModeNonNazgulCreature);
            int hazCreatureEquiv = hazStandardCreatures + ((hazNazgul + hazDualCreatures) / 2);

            if (totalResChars == 0 && totalSideChars == 0 &&
                resFactions == 0 && sideFactions == 0 &&
                resAllies == 0 && sideAllies == 0 &&
                poolItems == 0 && resItems == 0 && sideItems == 0 &&
                resRingCards == 0 && sideRingCards == 0 &&
                resPalantirCards == 0 && sidePalantirCards == 0 &&
                resQuests == 0 && sideQuests == 0 &&
                resEvents == 0 && sideEvents == 0 &&
                hazNazgul == 0 && sideNazgul == 0 &&
                hazNazgulSupport == 0 && sideNazgulSupport == 0 &&
                hazStandardCreatures == 0 && sideStandardCreatures == 0 &&
                hazDualCreatures == 0 && sideDualCreatures == 0)
            {
                ValidateWorkbenchReadiness();
            }

            int poolChars = _deck.Pool.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            int poolMind = _deck.Pool.Sum(c => ParseCardMind(c.Mind));
            int nonAvatarCharsInRes = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInRes = _deck.Resources.Count(IsAvatar);
            int nonAvatarCharsInSideboard = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInSideboard = _deck.Sideboard.Count(IsAvatar);
            int regionsInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int havensInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int advSitesInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string poolItemNote = poolItems > 0 ? $" + {poolItems} item(s)" : "";
            string avatarResNote = avatarsInRes > 0 ? $" + {avatarsInRes} Avatar(s)" : "";
            string resFactionNote = resFactions > 0 ? $", {resFactions} faction(s)" : "";
            string resAllyNote = resAllies > 0 ? (resAllies == 1 ? ", 1 ally" : $", {resAllies} allies") : "";
            string resItemNote = resItems > 0 ? $", {resItems} item(s)" : "";
            string resRingNote = resRingCards > 0 ? $", {resRingCards} ring card(s)" : "";
            string resPalantirNote = resPalantirCards > 0 ? $", {resPalantirCards} palantír card(s)" : "";
            string resQuestNote = resQuests > 0 ? $", {resQuests} quest(s)" : "";
            string resEventNote = resEvents > 0 ? $", {resEvents} event(s)" : "";

            var hazParts = new List<string>();
            if (hazStandardCreatures > 0) hazParts.Add($"{hazStandardCreatures} creature(s)");
            if (hazDualCreatures > 0) hazParts.Add($"{hazDualCreatures} dual(s)");
            if (hazNazgul > 0) hazParts.Add($"{hazNazgul} Nazgûl");
            if (hazNazgulSupport > 0) hazParts.Add($"{hazNazgulSupport} Nazgûl support");
            string hazDetailNote = hazParts.Count > 0 ? $" ({string.Join(", ", hazParts)}; Equiv: {hazCreatureEquiv}/12 min)" : "";

            var sideParts = new List<string>();
            if (nonAvatarCharsInSideboard > 0) sideParts.Add($"{nonAvatarCharsInSideboard} character(s)");
            if (avatarsInSideboard > 0) sideParts.Add($"{avatarsInSideboard} Avatar");
            if (sideFactions > 0) sideParts.Add($"{sideFactions} faction(s)");
            if (sideAllies > 0) sideParts.Add(sideAllies == 1 ? "1 ally" : $"{sideAllies} allies");
            if (sideItems > 0) sideParts.Add($"{sideItems} item(s)");
            if (sideRingCards > 0) sideParts.Add($"{sideRingCards} ring card(s)");
            if (sidePalantirCards > 0) sideParts.Add($"{sidePalantirCards} palantír card(s)");
            if (sideQuests > 0) sideParts.Add($"{sideQuests} quest(s)");
            if (sideEvents > 0) sideParts.Add($"{sideEvents} event(s)");
            if (sideStandardCreatures > 0) sideParts.Add($"{sideStandardCreatures} creature(s)");
            if (sideDualCreatures > 0) sideParts.Add($"{sideDualCreatures} dual(s)");
            if (sideNazgul > 0) sideParts.Add($"{sideNazgul} Nazgûl");
            if (sideNazgulSupport > 0) sideParts.Add($"{sideNazgulSupport} Nazgûl support");
            string sideDetailNote = sideParts.Count > 0 ? $" ({string.Join(", ", sideParts)})" : "";

            static bool IsNonHazard(Card c) => !string.Equals(c.Primary, "Hazard", StringComparison.OrdinalIgnoreCase);

            static (int Base, int Cond) SumSectionMPs(IEnumerable<Card> cards)
            {
                int b = 0, c = 0;
                foreach (var card in cards.Where(IsNonHazard))
                {
                    var (cb, cc) = ParseCardMPs(card.MPs);
                    b += cb;
                    c += cc;
                }
                return (b, c);
            }

            var (poolBase, poolCond) = SumSectionMPs(_deck.Pool);
            var (resBase, resCond) = SumSectionMPs(_deck.Resources);
            var (sideBase, sideCond) = SumSectionMPs(_deck.Sideboard);
            var (siteBase, siteCond) = SumSectionMPs(_deck.Sites);

            int totalBase = poolBase + resBase + sideBase + siteBase;
            int totalCond = poolCond + resCond + sideCond + siteCond;

            string totalMpText = totalCond != totalBase ? $"{totalBase}({totalCond})" : $"{totalBase}";
            string poolMpText = poolCond != poolBase ? $"{poolBase}({poolCond})" : $"{poolBase}";
            string resMpText = $"{resBase}({resCond})";
            string sideMpText = sideCond != sideBase ? $"{sideBase}({sideCond})" : $"{sideBase}";
            string siteMpText = siteCond != siteBase ? $"{siteBase}({siteCond})" : $"{siteBase}";

            _lblDeckSummary.Text =
                $"Deck Title:       \"{_deck.Title}\"\n\n" +
                $"Pool:             {_deck.Pool.Count} cards ({poolChars} characters, {poolMind} Mind{poolItemNote})  [Limits: <= 20 Mind, <= 2 Minor Items]\n" +
                $"Resources:        {_deck.Resources.Count} cards ({nonAvatarCharsInRes} characters{avatarResNote}{resFactionNote}{resAllyNote}{resItemNote}{resRingNote}{resPalantirNote}{resQuestNote}{resEventNote})  [Limits: <= 10 non-Avatar, <= 3 Avatars]\n" +
                $"Hazards:          {_deck.Hazards.Count} cards{hazDetailNote}\n" +
                $"Sideboard:        {_deck.Sideboard.Count} cards{sideDetailNote}  [Target: {_numSideboardSize.Value}]\n" +
                $"Sites:            {_deck.Sites.Count} cards ({havensInSites} Havens, {regionsInSites} Regions, {advSitesInSites} Adventure Sites)\n\n" +
                $"Play Deck Status: {_deck.Resources.Count} Resources / {_deck.Hazards.Count} Hazards (Target: {_numPlayDeckSize.Value}/{_numPlayDeckSize.Value})\n" +
                $"Sideboard Status: {_deck.Sideboard.Count} / {_numSideboardSize.Value}\n" +
                $"MP Total:         {totalMpText} MPs (Pool: {poolMpText}, Resources: {resMpText}, Sideboard: {sideMpText}{(siteBase != 0 || siteCond != 0 ? $", Sites: {siteMpText}" : "")})";
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

        private void UpdateCreatureKeyingEnabledState()
        {
            if (_clbCreatureRegionTypes == null || _clbCreatureSiteTypes == null || _lblCreatureKeyingNote == null)
            {
                return;
            }

            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;
            _clbCreatureRegionTypes.Enabled = !tailorToDeck;
            _clbCreatureSiteTypes.Enabled = !tailorToDeck;
            _btnAllCreatureKeying.Enabled = !tailorToDeck;
            _btnClearCreatureKeying.Enabled = !tailorToDeck;

            _lblCreatureKeyingNote.Text = tailorToDeck
                ? "Keying Filters disabled: 'Tailor Hazards to current deck' is enabled on General Settings, so Creatures automatically key to drafted Sites && Regions."
                : "Unknown Opponent mode: Creatures pass if any Region Requirement terrain or Site Requirement hold is checked above.";
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

            _lblItemStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblItemStatus.Text = ItemIdleStatusText;

            _lblRingsStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblRingsStatus.Text = RingsIdleStatusText;

            _lblPalantiriStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblPalantiriStatus.Text = PalantirIdleStatusText;

            _lblQuestsStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblQuestsStatus.Text = QuestIdleStatusText;

            _lblResourceEventsStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblResourceEventsStatus.Text = ResourceEventsIdleStatusText;

            _lblCreaturesStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblCreaturesStatus.Text = CreaturesIdleStatusText;

            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);

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
                DecksBeforeCouncil = (int)_numDecksBeforeCouncil.Value,

                SeedRegion = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random",
                RegionCount = (int)_numRegionCount.Value,
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
                SideboardAllyCount = (int)_numSideboardAllies.Value,

                PoolMinorItemCount = (int)_numPoolMinorItems.Value,
                ResourceMinorItemCount = (int)_numResourceMinorItems.Value,
                SideboardMinorItemCount = (int)_numSideboardMinorItems.Value,
                ResourceMajorItemCount = (int)_numResourceMajorItems.Value,
                SideboardMajorItemCount = (int)_numSideboardMajorItems.Value,
                ResourceGreaterItemCount = (int)_numResourceGreaterItems.Value,
                SideboardGreaterItemCount = (int)_numSideboardGreaterItems.Value,
                ResourceSpecialItemCount = (int)_numResourceSpecialItems.Value,
                SideboardSpecialItemCount = (int)_numSideboardSpecialItems.Value,
                ExcludedItemRoles = [.. Enumerable.Range(0, _clbItemRoles.Items.Count)
                    .Where(i => !_clbItemRoles.GetItemChecked(i))
                    .Select(i => _clbItemRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],

                ResourceGoldRingCount = (int)_numResGoldRings.Value,
                SideboardGoldRingCount = (int)_numSideGoldRings.Value,
                ResourceAllowRingTestCount = (int)_numResAllowRingTest.Value,
                SideboardAllowRingTestCount = (int)_numSideAllowRingTest.Value,
                ResourceAidRingTestCount = (int)_numResAidRingTest.Value,
                SideboardAidRingTestCount = (int)_numSideAidRingTest.Value,
                ResourceOneRingCount = (int)_numResOneRing.Value,
                SideboardOneRingCount = (int)_numSideOneRing.Value,
                ResourceDwarvenRingCount = (int)_numResDwarvenRings.Value,
                SideboardDwarvenRingCount = (int)_numSideDwarvenRings.Value,
                ResourceMagicRingCount = (int)_numResMagicRings.Value,
                SideboardMagicRingCount = (int)_numSideMagicRings.Value,
                ResourceLesserRingCount = (int)_numResLesserRings.Value,
                SideboardLesserRingCount = (int)_numSideLesserRings.Value,
                ResourceWizardsRingCount = (int)_numResWizardsRing.Value,
                SideboardWizardsRingCount = (int)_numSideWizardsRing.Value,
                ExcludedRingRoles = [.. Enumerable.Range(0, _clbRingRoles.Items.Count)
                    .Where(i => !_clbRingRoles.GetItemChecked(i))
                    .Select(i => _clbRingRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],

                ResourcePalantirCount = (int)_numResPalantiri.Value,
                SideboardPalantirCount = (int)_numSidePalantiri.Value,
                ResourceAllowPalantirUseCount = (int)_numResAllowPalantirUse.Value,
                SideboardAllowPalantirUseCount = (int)_numSideAllowPalantirUse.Value,
                ExcludedPalantirRoles = [.. Enumerable.Range(0, _clbPalantirRoles.Items.Count)
                    .Where(i => !_clbPalantirRoles.GetItemChecked(i))
                    .Select(i => _clbPalantirRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],
                ResourceQuestCount = (int)_numResourceQuests.Value,
                SideboardQuestCount = (int)_numSideboardQuests.Value,
                AutoDraftOneRingPackage = _chkAutoDraftOneRingPackage.Checked,
                ExcludedQuestRoles = [.. Enumerable.Range(0, _clbQuestRoles.Items.Count)
                    .Where(i => !_clbQuestRoles.GetItemChecked(i))
                    .Select(i => _clbQuestRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],

                ResourceGatesOfMorningCount = (int)_numResGatesOfMorning.Value,
                SideboardGatesOfMorningCount = (int)_numSideGatesOfMorning.Value,
                ResourceTwilightCount = (int)_numResTwilight.Value,
                SideboardTwilightCount = (int)_numSideTwilight.Value,
                SideboardResourceEventCount = (int)_numSideboardResourceEvents.Value,
                ExcludedResourceEventRoles = [.. Enumerable.Range(0, _clbResourceEventRoles.Items.Count)
                    .Where(i => !_clbResourceEventRoles.GetItemChecked(i))
                    .Select(i => _clbResourceEventRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],

                TailorHazardsToCurrentDeck = _chkTailorHazardsToCurrentDeck.Checked,
                HazardNazgulCount = (int)_numHazNazgul.Value,
                SideboardNazgulCount = (int)_numSideNazgul.Value,
                HazardNazgulSupportCount = (int)_numHazNazgulSupport.Value,
                SideboardNazgulSupportCount = (int)_numSideNazgulSupport.Value,
                HazardStandardCreatureCount = (int)_numHazStandardCreatures.Value,
                SideboardStandardCreatureCount = (int)_numSideStandardCreatures.Value,
                HazardDualCreatureCount = (int)_numHazDualCreatures.Value,
                SideboardDualCreatureCount = (int)_numSideDualCreatures.Value,
                ExcludedCreatureSpecies = [.. Enumerable.Range(0, _clbCreatureSpecies.Items.Count)
                    .Where(i => !_clbCreatureSpecies.GetItemChecked(i))
                    .Select(i => _clbCreatureSpecies.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],
                SelectedCreatureRegionTypes = [.. _clbCreatureRegionTypes.CheckedItems.Cast<string>()],
                SelectedCreatureSiteTypes = [.. _clbCreatureSiteTypes.CheckedItems.Cast<string>()]
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

            _numMaxSiteCount.Value = Math.Clamp(config.MaxSiteCount, (int)_numMaxSiteCount.Minimum, (int)_numMaxSiteCount.Maximum);

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

            _numDecksBeforeCouncil.Value = Math.Clamp(config.DecksBeforeCouncil, (int)_numDecksBeforeCouncil.Minimum, (int)_numDecksBeforeCouncil.Maximum);

            _numResourceFactions.Value = Math.Clamp(config.ResourceFactionCount, (int)_numResourceFactions.Minimum, (int)_numResourceFactions.Maximum);
            _numSideboardFactions.Value = Math.Clamp(config.SideboardFactionCount, (int)_numSideboardFactions.Minimum, (int)_numSideboardFactions.Maximum);

            _numResourceAllies.Value = Math.Clamp(config.ResourceAllyCount, (int)_numResourceAllies.Minimum, (int)_numResourceAllies.Maximum);
            _numSideboardAllies.Value = Math.Clamp(config.SideboardAllyCount, (int)_numSideboardAllies.Minimum, (int)_numSideboardAllies.Maximum);

            _numPoolMinorItems.Value = Math.Clamp(config.PoolMinorItemCount, (int)_numPoolMinorItems.Minimum, (int)_numPoolMinorItems.Maximum);
            _numResourceMinorItems.Value = Math.Clamp(config.ResourceMinorItemCount, (int)_numResourceMinorItems.Minimum, (int)_numResourceMinorItems.Maximum);
            _numSideboardMinorItems.Value = Math.Clamp(config.SideboardMinorItemCount, (int)_numSideboardMinorItems.Minimum, (int)_numSideboardMinorItems.Maximum);
            _numResourceMajorItems.Value = Math.Clamp(config.ResourceMajorItemCount, (int)_numResourceMajorItems.Minimum, (int)_numResourceMajorItems.Maximum);
            _numSideboardMajorItems.Value = Math.Clamp(config.SideboardMajorItemCount, (int)_numSideboardMajorItems.Minimum, (int)_numSideboardMajorItems.Maximum);
            _numResourceGreaterItems.Value = Math.Clamp(config.ResourceGreaterItemCount, (int)_numResourceGreaterItems.Minimum, (int)_numResourceGreaterItems.Maximum);
            _numSideboardGreaterItems.Value = Math.Clamp(config.SideboardGreaterItemCount, (int)_numSideboardGreaterItems.Minimum, (int)_numSideboardGreaterItems.Maximum);
            _numResourceSpecialItems.Value = Math.Clamp(config.ResourceSpecialItemCount, (int)_numResourceSpecialItems.Minimum, (int)_numResourceSpecialItems.Maximum);
            _numSideboardSpecialItems.Value = Math.Clamp(config.SideboardSpecialItemCount, (int)_numSideboardSpecialItems.Minimum, (int)_numSideboardSpecialItems.Maximum);

            PopulateItemRolesChecklist();
            var excludedRoles = new HashSet<string>(config.ExcludedItemRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbItemRoles.Items.Count; i++)
            {
                string roleName = _clbItemRoles.Items[i]?.ToString() ?? string.Empty;
                _clbItemRoles.SetItemChecked(i, !excludedRoles.Contains(roleName));
            }

            _numResGoldRings.Value = Math.Clamp(config.ResourceGoldRingCount, (int)_numResGoldRings.Minimum, (int)_numResGoldRings.Maximum);
            _numSideGoldRings.Value = Math.Clamp(config.SideboardGoldRingCount, (int)_numSideGoldRings.Minimum, (int)_numSideGoldRings.Maximum);
            _numResAllowRingTest.Value = Math.Clamp(config.ResourceAllowRingTestCount, (int)_numResAllowRingTest.Minimum, (int)_numResAllowRingTest.Maximum);
            _numSideAllowRingTest.Value = Math.Clamp(config.SideboardAllowRingTestCount, (int)_numSideAllowRingTest.Minimum, (int)_numSideAllowRingTest.Maximum);
            _numResAidRingTest.Value = Math.Clamp(config.ResourceAidRingTestCount, (int)_numResAidRingTest.Minimum, (int)_numResAidRingTest.Maximum);
            _numSideAidRingTest.Value = Math.Clamp(config.SideboardAidRingTestCount, (int)_numSideAidRingTest.Minimum, (int)_numSideAidRingTest.Maximum);
            _numResOneRing.Value = Math.Clamp(config.ResourceOneRingCount, (int)_numResOneRing.Minimum, (int)_numResOneRing.Maximum);
            _numSideOneRing.Value = Math.Clamp(config.SideboardOneRingCount, (int)_numSideOneRing.Minimum, (int)_numSideOneRing.Maximum);
            _numResDwarvenRings.Value = Math.Clamp(config.ResourceDwarvenRingCount, (int)_numResDwarvenRings.Minimum, (int)_numResDwarvenRings.Maximum);
            _numSideDwarvenRings.Value = Math.Clamp(config.SideboardDwarvenRingCount, (int)_numSideDwarvenRings.Minimum, (int)_numSideDwarvenRings.Maximum);
            _numResMagicRings.Value = Math.Clamp(config.ResourceMagicRingCount, (int)_numResMagicRings.Minimum, (int)_numResMagicRings.Maximum);
            _numSideMagicRings.Value = Math.Clamp(config.SideboardMagicRingCount, (int)_numSideMagicRings.Minimum, (int)_numSideMagicRings.Maximum);
            _numResLesserRings.Value = Math.Clamp(config.ResourceLesserRingCount, (int)_numResLesserRings.Minimum, (int)_numResLesserRings.Maximum);
            _numSideLesserRings.Value = Math.Clamp(config.SideboardLesserRingCount, (int)_numSideLesserRings.Minimum, (int)_numSideLesserRings.Maximum);
            _numResWizardsRing.Value = Math.Clamp(config.ResourceWizardsRingCount, (int)_numResWizardsRing.Minimum, (int)_numResWizardsRing.Maximum);
            _numSideWizardsRing.Value = Math.Clamp(config.SideboardWizardsRingCount, (int)_numSideWizardsRing.Minimum, (int)_numSideWizardsRing.Maximum);

            PopulateRingRolesChecklist();
            var excludedRingRoles = new HashSet<string>(config.ExcludedRingRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbRingRoles.Items.Count; i++)
            {
                string roleName = _clbRingRoles.Items[i]?.ToString() ?? string.Empty;
                _clbRingRoles.SetItemChecked(i, !excludedRingRoles.Contains(roleName));
            }

            _numResPalantiri.Value = Math.Clamp(config.ResourcePalantirCount, (int)_numResPalantiri.Minimum, (int)_numResPalantiri.Maximum);
            _numSidePalantiri.Value = Math.Clamp(config.SideboardPalantirCount, (int)_numSidePalantiri.Minimum, (int)_numSidePalantiri.Maximum);
            _numResAllowPalantirUse.Value = Math.Clamp(config.ResourceAllowPalantirUseCount, (int)_numResAllowPalantirUse.Minimum, (int)_numResAllowPalantirUse.Maximum);
            _numSideAllowPalantirUse.Value = Math.Clamp(config.SideboardAllowPalantirUseCount, (int)_numSideAllowPalantirUse.Minimum, (int)_numSideAllowPalantirUse.Maximum);

            PopulatePalantirRolesChecklist();
            var excludedPalantirRoles = new HashSet<string>(config.ExcludedPalantirRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbPalantirRoles.Items.Count; i++)
            {
                string roleName = _clbPalantirRoles.Items[i]?.ToString() ?? string.Empty;
                _clbPalantirRoles.SetItemChecked(i, !excludedPalantirRoles.Contains(roleName));
            }

            _numResourceQuests.Value = Math.Clamp(config.ResourceQuestCount, (int)_numResourceQuests.Minimum, (int)_numResourceQuests.Maximum);
            _numSideboardQuests.Value = Math.Clamp(config.SideboardQuestCount, (int)_numSideboardQuests.Minimum, (int)_numSideboardQuests.Maximum);
            _chkAutoDraftOneRingPackage.Checked = config.AutoDraftOneRingPackage;

            PopulateQuestRolesChecklist();
            var excludedQuestRoles = new HashSet<string>(config.ExcludedQuestRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbQuestRoles.Items.Count; i++)
            {
                string roleName = _clbQuestRoles.Items[i]?.ToString() ?? string.Empty;
                _clbQuestRoles.SetItemChecked(i, !excludedQuestRoles.Contains(roleName));
            }

            _numResGatesOfMorning.Value = Math.Clamp(config.ResourceGatesOfMorningCount, (int)_numResGatesOfMorning.Minimum, (int)_numResGatesOfMorning.Maximum);
            _numSideGatesOfMorning.Value = Math.Clamp(config.SideboardGatesOfMorningCount, (int)_numSideGatesOfMorning.Minimum, (int)_numSideGatesOfMorning.Maximum);
            _numResTwilight.Value = Math.Clamp(config.ResourceTwilightCount, (int)_numResTwilight.Minimum, (int)_numResTwilight.Maximum);
            _numSideTwilight.Value = Math.Clamp(config.SideboardTwilightCount, (int)_numSideTwilight.Minimum, (int)_numSideTwilight.Maximum);
            _numSideboardResourceEvents.Value = Math.Clamp(config.SideboardResourceEventCount, (int)_numSideboardResourceEvents.Minimum, (int)_numSideboardResourceEvents.Maximum);

            PopulateResourceEventRolesChecklist();
            var excludedEventRoles = new HashSet<string>(config.ExcludedResourceEventRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbResourceEventRoles.Items.Count; i++)
            {
                string roleName = _clbResourceEventRoles.Items[i]?.ToString() ?? string.Empty;
                _clbResourceEventRoles.SetItemChecked(i, !excludedEventRoles.Contains(roleName));
            }

            _chkTailorHazardsToCurrentDeck.Checked = config.TailorHazardsToCurrentDeck;
            _numHazNazgul.Value = Math.Clamp(config.HazardNazgulCount, (int)_numHazNazgul.Minimum, (int)_numHazNazgul.Maximum);
            _numSideNazgul.Value = Math.Clamp(config.SideboardNazgulCount, (int)_numSideNazgul.Minimum, (int)_numSideNazgul.Maximum);
            _numHazNazgulSupport.Value = Math.Clamp(config.HazardNazgulSupportCount, (int)_numHazNazgulSupport.Minimum, (int)_numHazNazgulSupport.Maximum);
            _numSideNazgulSupport.Value = Math.Clamp(config.SideboardNazgulSupportCount, (int)_numSideNazgulSupport.Minimum, (int)_numSideNazgulSupport.Maximum);
            _numHazStandardCreatures.Value = Math.Clamp(config.HazardStandardCreatureCount, (int)_numHazStandardCreatures.Minimum, (int)_numHazStandardCreatures.Maximum);
            _numSideStandardCreatures.Value = Math.Clamp(config.SideboardStandardCreatureCount, (int)_numSideStandardCreatures.Minimum, (int)_numSideStandardCreatures.Maximum);
            _numHazDualCreatures.Value = Math.Clamp(config.HazardDualCreatureCount, (int)_numHazDualCreatures.Minimum, (int)_numHazDualCreatures.Maximum);
            _numSideDualCreatures.Value = Math.Clamp(config.SideboardDualCreatureCount, (int)_numSideDualCreatures.Minimum, (int)_numSideDualCreatures.Maximum);

            PopulateCreatureSpeciesChecklist();
            var excludedSpecies = new HashSet<string>(config.ExcludedCreatureSpecies ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbCreatureSpecies.Items.Count; i++)
            {
                string speciesName = _clbCreatureSpecies.Items[i]?.ToString() ?? string.Empty;
                _clbCreatureSpecies.SetItemChecked(i, !excludedSpecies.Contains(speciesName));
            }

            if (config.SelectedCreatureRegionTypes != null)
            {
                SetCheckedItems(_clbCreatureRegionTypes, config.SelectedCreatureRegionTypes);
            }
            else
            {
                SetAllChecked(_clbCreatureRegionTypes, true);
            }

            if (config.SelectedCreatureSiteTypes != null)
            {
                SetCheckedItems(_clbCreatureSiteTypes, config.SelectedCreatureSiteTypes);
            }
            else
            {
                SetAllChecked(_clbCreatureSiteTypes, true);
            }

            UpdateCreatureKeyingEnabledState();
            UpdateCreatureEquivalentsReadout();

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
            if (_btnRunPipeline == null || _lblRecruitStatus == null || _lblTheaterStatus == null ||
                _lblFactionStatus == null || _lblAllyStatus == null || _lblItemStatus == null ||
                _lblRingsStatus == null || _lblPalantiriStatus == null || _lblQuestsStatus == null ||
                _lblResourceEventsStatus == null || _lblCreaturesStatus == null)
            {
                return;
            }

            bool hasTheaterError = false;
            bool hasRecruitError = false;
            bool hasFactionError = false;
            bool hasAllyError = false;
            bool hasItemError = false;
            bool hasRingsError = false;
            bool hasPalantirError = false;
            bool hasQuestsError = false;
            bool hasResourceEventsError = false;
            bool hasCreaturesError = false;

            int wizInRes = GetSelectedWizard1Count() + (IsWizard2InSideboard() ? 0 : GetSelectedWizard2Count());
            int wizInSide = IsWizard2InSideboard() ? GetSelectedWizard2Count() : 0;

            int companionCount = _recruitRows.Count(r => r.IsActive);
            int reserveCount = _reserveRows.Count(r => r.IsActive);
            int totalSideboardChars = reserveCount + wizInSide;

            int resFactions = (int)_numResourceFactions.Value;
            int sideFactions = (int)_numSideboardFactions.Value;
            int resAllies = (int)_numResourceAllies.Value;
            int sideAllies = (int)_numSideboardAllies.Value;
            int resItems = (int)_numResourceMinorItems.Value + (int)_numResourceMajorItems.Value +
                           (int)_numResourceGreaterItems.Value + (int)_numResourceSpecialItems.Value;
            int sideItems = (int)_numSideboardMinorItems.Value + (int)_numSideboardMajorItems.Value +
                            (int)_numSideboardGreaterItems.Value + (int)_numSideboardSpecialItems.Value;
            int resRingCards = (int)_numResGoldRings.Value + (int)_numResAllowRingTest.Value + (int)_numResAidRingTest.Value +
                               (int)_numResOneRing.Value + (int)_numResDwarvenRings.Value + (int)_numResMagicRings.Value +
                               (int)_numResLesserRings.Value + (int)_numResWizardsRing.Value;
            int sideRingCards = (int)_numSideGoldRings.Value + (int)_numSideAllowRingTest.Value + (int)_numSideAidRingTest.Value +
                                (int)_numSideOneRing.Value + (int)_numSideDwarvenRings.Value + (int)_numSideMagicRings.Value +
                                (int)_numSideLesserRings.Value + (int)_numSideWizardsRing.Value;
            int resPalCards = (int)_numResPalantiri.Value + (int)_numResAllowPalantirUse.Value;
            int sidePalCards = (int)_numSidePalantiri.Value + (int)_numSideAllowPalantirUse.Value;
            int resQuests = (int)_numResourceQuests.Value;
            int sideQuests = (int)_numSideboardQuests.Value;
            int resEnvEvents = (int)_numResGatesOfMorning.Value + (int)_numResTwilight.Value;
            int sideEnvEvents = (int)_numSideGatesOfMorning.Value + (int)_numSideTwilight.Value;
            int sideAutoEvents = (int)_numSideboardResourceEvents.Value;
            int hazNazgul = (int)_numHazNazgul.Value;
            int sideNazgul = (int)_numSideNazgul.Value;
            int hazNazSup = (int)_numHazNazgulSupport.Value;
            int sideNazSup = (int)_numSideNazgulSupport.Value;
            int hazStdCr = (int)_numHazStandardCreatures.Value;
            int sideStdCr = (int)_numSideStandardCreatures.Value;
            int hazDualCr = (int)_numHazDualCreatures.Value;
            int sideDualCr = (int)_numSideDualCreatures.Value;

            int hazCreaturesTotal = hazNazgul + hazNazSup + hazStdCr + hazDualCr;
            int sideCreatures = sideNazgul + sideNazSup + sideStdCr + sideDualCr;

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

            // --- Shared Play Deck & Sideboard Budget Validation (Tabs 5-11) ---
            int totalConfiguredRes = companionCount + wizInRes + resFactions + resAllies + resItems + resRingCards + resPalCards + resQuests + resEnvEvents;
            int totalConfiguredSide = totalSideboardChars + sideFactions + sideAllies + sideItems + sideRingCards + sidePalCards + sideQuests + sideEnvEvents + sideAutoEvents + sideCreatures;

            string resOverMsg =
                $"⚠ Configured Resources cards ({totalConfiguredRes}: {companionCount + wizInRes} characters + {resFactions} factions + {resAllies} allies + {resItems} items + {resRingCards} ring cards + {resPalCards} palantír cards + {resQuests} quests + {resEnvEvents} env) exceed Play Deck Size ({playDeckLimit}).\n" +
                "Reduce Resources card counts or increase Play Deck Size on General Settings.";

            string sideOverMsg =
                $"⚠ Configured Sideboard cards ({totalConfiguredSide}: {totalSideboardChars} characters + {sideFactions} factions + {sideAllies} allies + {sideItems} items + {sideRingCards} ring cards + {sidePalCards} palantír cards + {sideQuests} quests + {sideEnvEvents + sideAutoEvents} events + {sideCreatures} creature/Nazgûl) exceed Sideboard Size ({sideboardLimit}).\n" +
                "Reduce Sideboard card counts or increase Sideboard Size on General Settings.";

            // --- Tab 5 (Factions) ---
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

            // --- Tab 6 (Allies) ---
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

            // --- Tab 7 (Items) ---
            int totalRequestedItems = (int)_numPoolMinorItems.Value + resItems + sideItems;
            if (totalRequestedItems > 0 && _clbItemRoles.Items.Count > 0 && _clbItemRoles.CheckedItems.Count == 0)
            {
                hasItemError = true;
                _lblItemStatus.ForeColor = Color.Firebrick;
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblItemStatus.Text = "⚠ At least one Role must be checked under Role Focus when drafting items.";
            }
            else if (totalConfiguredRes > playDeckLimit && resItems > 0)
            {
                hasItemError = true;
                _lblItemStatus.ForeColor = Color.Firebrick;
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblItemStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sideItems > 0)
            {
                hasItemError = true;
                _lblItemStatus.ForeColor = Color.Firebrick;
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblItemStatus.Text = sideOverMsg;
            }
            else if ((_lblItemStatus.ForeColor == Color.Firebrick &&
                      (_lblItemStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _lblItemStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblItemStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Pool.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      _deck.Resources.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      !_lblItemStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblItemStatus.Text.StartsWith("No items configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblItemStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblItemStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblItemStatus.Text = ItemIdleStatusText;
            }

            // --- Tab 8 (Rings) ---
            int totalRequestedSpecialRings =
                (int)_numResDwarvenRings.Value + (int)_numSideDwarvenRings.Value +
                (int)_numResMagicRings.Value + (int)_numSideMagicRings.Value +
                (int)_numResLesserRings.Value + (int)_numSideLesserRings.Value +
                (int)_numResWizardsRing.Value + (int)_numSideWizardsRing.Value;

            if ((int)_numResOneRing.Value + (int)_numSideOneRing.Value > 1)
            {
                hasRingsError = true;
                _lblRingsStatus.ForeColor = Color.Firebrick;
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblRingsStatus.Text = "⚠ The One Ring is Unique: combined Resources + Sideboard count cannot exceed 1.";
            }
            else if (totalRequestedSpecialRings > 0 && _clbRingRoles.Items.Count > 0 && _clbRingRoles.CheckedItems.Count == 0)
            {
                hasRingsError = true;
                _lblRingsStatus.ForeColor = Color.Firebrick;
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblRingsStatus.Text = "⚠ At least one Role must be checked under Special Ring Role Focus when drafting Special Rings.";
            }
            else if (totalConfiguredRes > playDeckLimit && resRingCards > 0)
            {
                hasRingsError = true;
                _lblRingsStatus.ForeColor = Color.Firebrick;
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblRingsStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sideRingCards > 0)
            {
                hasRingsError = true;
                _lblRingsStatus.ForeColor = Color.Firebrick;
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblRingsStatus.Text = sideOverMsg;
            }
            else if ((_lblRingsStatus.ForeColor == Color.Firebrick &&
                      (_lblRingsStatus.Text.StartsWith("⚠ The One Ring", StringComparison.Ordinal) ||
                       _lblRingsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _lblRingsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblRingsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsRingPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsRingPackageCard(c)) &&
                      !_lblRingsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblRingsStatus.Text.StartsWith("No ring cards configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblRingsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRingsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblRingsStatus.Text = RingsIdleStatusText;
            }

            // --- Tab 9 (Palantiri) ---
            int totalRequestedPalantiri = (int)_numResPalantiri.Value + (int)_numSidePalantiri.Value;
            if (totalRequestedPalantiri > 0 && _clbPalantirRoles.Items.Count > 0 && _clbPalantirRoles.CheckedItems.Count == 0)
            {
                hasPalantirError = true;
                _lblPalantiriStatus.ForeColor = Color.Firebrick;
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblPalantiriStatus.Text = "⚠ At least one Role must be checked under Role Focus when drafting Palantíri.";
            }
            else if (totalConfiguredRes > playDeckLimit && resPalCards > 0)
            {
                hasPalantirError = true;
                _lblPalantiriStatus.ForeColor = Color.Firebrick;
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblPalantiriStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sidePalCards > 0)
            {
                hasPalantirError = true;
                _lblPalantiriStatus.ForeColor = Color.Firebrick;
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblPalantiriStatus.Text = sideOverMsg;
            }
            else if ((_lblPalantiriStatus.ForeColor == Color.Firebrick &&
                      (_lblPalantiriStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _lblPalantiriStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblPalantiriStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsPalantirPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsPalantirPackageCard(c)) &&
                      !_lblPalantiriStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblPalantiriStatus.Text.StartsWith("No Palantír cards configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblPalantiriStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPalantiriStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblPalantiriStatus.Text = PalantirIdleStatusText;
            }

            // --- Tab 10 (Quests) ---
            int totalRequestedQuests = resQuests + sideQuests;
            if (totalRequestedQuests > 0 && _clbQuestRoles.Items.Count > 0 && _clbQuestRoles.CheckedItems.Count == 0)
            {
                hasQuestsError = true;
                _lblQuestsStatus.ForeColor = Color.Firebrick;
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblQuestsStatus.Text = "⚠ At least one Role must be checked under Role Focus when drafting quests.";
            }
            else if (totalConfiguredRes > playDeckLimit && resQuests > 0)
            {
                hasQuestsError = true;
                _lblQuestsStatus.ForeColor = Color.Firebrick;
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblQuestsStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && sideQuests > 0)
            {
                hasQuestsError = true;
                _lblQuestsStatus.ForeColor = Color.Firebrick;
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblQuestsStatus.Text = sideOverMsg;
            }
            else if ((_lblQuestsStatus.ForeColor == Color.Firebrick &&
                      (_lblQuestsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _lblQuestsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblQuestsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsQuestPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsQuestPackageCard(c)) &&
                      !_lblQuestsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblQuestsStatus.Text.StartsWith("No quests configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblQuestsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblQuestsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblQuestsStatus.Text = QuestIdleStatusText;
            }

            // --- Tab 11 (Resource Events) ---
            int gomTotal = (int)_numResGatesOfMorning.Value + (int)_numSideGatesOfMorning.Value;
            int twiTotal = (int)_numResTwilight.Value + (int)_numSideTwilight.Value;
            bool needsAutoEvents = totalConfiguredRes < playDeckLimit || sideAutoEvents > 0;

            if (gomTotal > 3)
            {
                hasResourceEventsError = true;
                _lblResourceEventsStatus.ForeColor = Color.Firebrick;
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblResourceEventsStatus.Text = "⚠ Gates of Morning is non-unique: combined Resources + Sideboard count cannot exceed 3.";
            }
            else if (twiTotal > 3)
            {
                hasResourceEventsError = true;
                _lblResourceEventsStatus.ForeColor = Color.Firebrick;
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblResourceEventsStatus.Text = "⚠ Twilight is non-unique: combined Resources + Sideboard count cannot exceed 3.";
            }
            else if (needsAutoEvents && _clbResourceEventRoles.Items.Count > 0 && _clbResourceEventRoles.CheckedItems.Count == 0)
            {
                hasResourceEventsError = true;
                _lblResourceEventsStatus.ForeColor = Color.Firebrick;
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblResourceEventsStatus.Text = "⚠ At least one Role must be checked under Role Focus to auto-fill Resource Events.";
            }
            else if (totalConfiguredRes > playDeckLimit && resEnvEvents > 0)
            {
                hasResourceEventsError = true;
                _lblResourceEventsStatus.ForeColor = Color.Firebrick;
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblResourceEventsStatus.Text = resOverMsg;
            }
            else if (totalConfiguredSide > sideboardLimit && (sideEnvEvents + sideAutoEvents) > 0)
            {
                hasResourceEventsError = true;
                _lblResourceEventsStatus.ForeColor = Color.Firebrick;
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblResourceEventsStatus.Text = sideOverMsg;
            }
            else if ((_lblResourceEventsStatus.ForeColor == Color.Firebrick &&
                      (_lblResourceEventsStatus.Text.StartsWith("⚠ Gates of Morning", StringComparison.Ordinal) ||
                       _lblResourceEventsStatus.Text.StartsWith("⚠ Twilight", StringComparison.Ordinal) ||
                       _lblResourceEventsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _lblResourceEventsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _lblResourceEventsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsGeneralResourceEvent(c)) &&
                      _deck.Sideboard.All(c => !IsGeneralResourceEvent(c)) &&
                      !_lblResourceEventsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase)))
            {
                _lblResourceEventsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblResourceEventsStatus.Text = ResourceEventsIdleStatusText;
            }

            // --- Tab 12 (Creatures) ---
            int totalNonNazgulRequested = hazStdCr + sideStdCr + hazDualCr + sideDualCr;
            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;

            if (hazNazgul + sideNazgul > 9)
            {
                hasCreaturesError = true;
                _lblCreaturesStatus.ForeColor = Color.Firebrick;
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblCreaturesStatus.Text = "⚠ Nazgûl are Unique: combined Hazards + Sideboard count cannot exceed 9.";
            }
            else if (totalNonNazgulRequested > 0 && _clbCreatureSpecies.Items.Count > 0 && _clbCreatureSpecies.CheckedItems.Count == 0)
            {
                hasCreaturesError = true;
                _lblCreaturesStatus.ForeColor = Color.Firebrick;
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblCreaturesStatus.Text = "⚠ At least one Species / Race must be checked when drafting Standard or Dual-Mode creatures.";
            }
            else if (!tailorToDeck && totalNonNazgulRequested > 0 &&
                     _clbCreatureRegionTypes.CheckedItems.Count == 0 &&
                     _clbCreatureSiteTypes.CheckedItems.Count == 0)
            {
                hasCreaturesError = true;
                _lblCreaturesStatus.ForeColor = Color.Firebrick;
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblCreaturesStatus.Text = "⚠ At least one Region Terrain or Site Hold must be checked under Keying Filters (or enable Tailor Hazards to current deck).";
            }
            else if (hazCreaturesTotal > playDeckLimit)
            {
                hasCreaturesError = true;
                _lblCreaturesStatus.ForeColor = Color.Firebrick;
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblCreaturesStatus.Text =
                    $"⚠ Configured Hazards cards ({hazCreaturesTotal}: {hazStdCr} Standard + {hazDualCr} Dual + {hazNazgul} Nazgûl + {hazNazSup} Support) exceed Play Deck Size ({playDeckLimit}).\n" +
                    "Reduce creature/Nazgûl counts or increase Play Deck Size on General Settings.";
            }
            else if (totalConfiguredSide > sideboardLimit && sideCreatures > 0)
            {
                hasCreaturesError = true;
                _lblCreaturesStatus.ForeColor = Color.Firebrick;
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblCreaturesStatus.Text = sideOverMsg;
            }
            else if ((_lblCreaturesStatus.ForeColor == Color.Firebrick &&
                      (_lblCreaturesStatus.Text.StartsWith("⚠ Nazgûl are Unique", StringComparison.Ordinal) ||
                       _lblCreaturesStatus.Text.StartsWith("⚠ At least one Species", StringComparison.Ordinal) ||
                       _lblCreaturesStatus.Text.StartsWith("⚠ At least one Region Terrain", StringComparison.Ordinal) ||
                       _lblCreaturesStatus.Text.StartsWith("⚠ Configured Hazards", StringComparison.Ordinal) ||
                       _lblCreaturesStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Hazards.All(c => !IsCreaturesTabCard(c)) &&
                      _deck.Sideboard.All(c => !IsCreaturesTabCard(c)) &&
                      !_lblCreaturesStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_lblCreaturesStatus.Text.StartsWith("No creatures or Nazgûl configured", StringComparison.OrdinalIgnoreCase)))
            {
                _lblCreaturesStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblCreaturesStatus.Text = CreaturesIdleStatusText;
            }

            // --- Unified Pipeline Readiness ---
            bool anyError =
                hasTheaterError || hasRecruitError || hasFactionError || hasAllyError ||
                hasItemError || hasRingsError || hasPalantirError || hasQuestsError ||
                hasResourceEventsError || hasCreaturesError;

            _btnRunPipeline.Enabled = !anyError;

            if (anyError)
            {
                var blockedTabs = new List<string>();
                if (hasTheaterError) blockedTabs.Add("Regions & Sites");
                if (hasRecruitError) blockedTabs.Add("Wizards & Recruits");
                if (hasFactionError) blockedTabs.Add("Factions");
                if (hasAllyError) blockedTabs.Add("Allies");
                if (hasItemError) blockedTabs.Add("Items");
                if (hasRingsError) blockedTabs.Add("Rings");
                if (hasPalantirError) blockedTabs.Add("Palantíri");
                if (hasQuestsError) blockedTabs.Add("Quests");
                if (hasResourceEventsError) blockedTabs.Add("Resource Events");
                if (hasCreaturesError) blockedTabs.Add("Creatures");

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
                bool hasSites = matchingSiteCount > 0;
                bool withinMaxSites = hasSites && matchingSiteCount <= maxSites;

                int regionScore = meetsRegions ? 100000 : trialCluster.Count * 1000;
                int sitePresenceScore = hasSites ? 10000 : 0;
                int maxFitScore = withinMaxSites
                    ? 5000
                    : (hasSites ? Math.Max(1, 5000 - (matchingSiteCount - maxSites)) : 0);

                int score = regionScore + sitePresenceScore + maxFitScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                }

                if (meetsRegions && withinMaxSites)
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
            if (finalAdvSites == 0)
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

            _deck.Resources.AddRange(finalRecruits);

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

            bool enforceDistinctSite = (int)_numDecksBeforeCouncil.Value == 1;

            if (enforceDistinctSite)
            {
                // Pass 1: Disjoint sites only (so Resource Allies and Factions don't share the same sole site in 1-deck games)
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
            }

            // Pass 2 (or sole pass when Decks Before Council >= 2): fill remaining requested Resources Allies
            if (draftedResAllies.Count < reqRes)
            {
                DraftAlliesIntoSection(
                    orderedCandidates,
                    _deck.Resources,
                    reqRes,
                    resCapacity,
                    usedCountsByName,
                    allSitesInDeck,
                    enforceDistinctSite ? siteOccupyingCards : null,
                    ref currentSiteMatches,
                    requireDistinctSite: false,
                    draftedResAllies,
                    draftedResPrereqs,
                    overlappingOut: enforceDistinctSite ? overlappingResAllies : null,
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
                bool matches = siteReqTags.Any(req => SiteMatchesSingleRequirement(site, req.Trim(), _filterService));

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
                // Role Focus (strict)
                var roleTags = _filterService.GetCardTagsForCategory(item.Id, "Role");
                if (roleTags.Count == 0 || !roleTags.Any(r => checkedRoles.Contains(r)))
                {
                    return false;
                }

                // Region Requirement (hard guard)
                if (!MatchesItemRegionRequirement(item, draftedRegionNames, draftedRegionTypes))
                {
                    return false;
                }

                // Specific character requirement
                if (!string.IsNullOrWhiteSpace(item.Specific) &&
                    !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, item.Specific)))
                {
                    return false;
                }

                // Prerequisite (must already exist in Pool / Resources / Sideboard; ; = OR, && = AND)
                if (!SatisfiesExistingPrerequisites(item, allDeckNonSiteCards))
                {
                    return false;
                }

                // Target Species (hard guard for Hero character races; Creature races ignored)
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

            // Execution order:
            // 1. Pool Minor Items (skipped site check; max 1 per starting character, up to 2)
            // 2. Special Items (Resources -> Sideboard)
            // 3. Greater Items (Resources -> Sideboard)
            // 4. Major Items (Resources -> Sideboard)
            // 5. Minor Items (Resources -> Sideboard)
            int effectivePoolCap = Math.Min(reqPoolMinor, poolChars.Count);
            var poolMinorCandidates = GetTierCandidates("Minor Item", checkSites: false);
            var draftedPoolMinor = DraftItemsIntoSection(poolMinorCandidates, _deck.Pool, effectivePoolCap, effectivePoolCap, usedCountsByName);

            var specialCandidates = GetTierCandidates("Special Item", checkSites: true);
            var draftedResSpecial = DraftItemsIntoSection(specialCandidates, _deck.Resources, reqResSpecial, resCapacity, usedCountsByName);
            resCapacity -= draftedResSpecial.Count;
            var draftedSideSpecial = DraftItemsIntoSection(specialCandidates, _deck.Sideboard, reqSideSpecial, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideSpecial.Count;

            var greaterCandidates = GetTierCandidates("Greater Item", checkSites: true);
            var draftedResGreater = DraftItemsIntoSection(greaterCandidates, _deck.Resources, reqResGreater, resCapacity, usedCountsByName);
            resCapacity -= draftedResGreater.Count;
            var draftedSideGreater = DraftItemsIntoSection(greaterCandidates, _deck.Sideboard, reqSideGreater, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideGreater.Count;

            var majorCandidates = GetTierCandidates("Major Item", checkSites: true);
            var draftedResMajor = DraftItemsIntoSection(majorCandidates, _deck.Resources, reqResMajor, resCapacity, usedCountsByName);
            resCapacity -= draftedResMajor.Count;
            var draftedSideMajor = DraftItemsIntoSection(majorCandidates, _deck.Sideboard, reqSideMajor, sideCapacity, usedCountsByName);
            sideCapacity -= draftedSideMajor.Count;

            var minorCandidates = GetTierCandidates("Minor Item", checkSites: true);
            var draftedResMinor = DraftItemsIntoSection(minorCandidates, _deck.Resources, reqResMinor, resCapacity, usedCountsByName);
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

        private static List<Card> DraftItemsIntoSection(
            List<Card> candidates,
            List<Card> targetSection,
            int requestedCount,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName)
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

                    targetSection.Add(item);
                    usedCountsByName[item.Name] = curCount + 1;
                    drafted.Add(item);
                }
            }

            return drafted;
        }

        private static bool IsItemCard(Card card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Secondary)) return false;
            return card.Secondary.Contains("Item", StringComparison.OrdinalIgnoreCase);
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

        private static bool HasRaceToken(Card card, string token)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Race) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var parts = card.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Any(p => CardFilterService.EqualsWithDiacriticFallback(p, token));
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

                    draftedResGold = DraftCardsRankedPlaysetFirst(rankedGold, _deck.Resources, reqResGold, resCapacity, usedCountsByName);
                    resCapacity -= draftedResGold.Count;
                    draftedSideGold = DraftCardsRankedPlaysetFirst(rankedGold, _deck.Sideboard, reqSideGold, sideCapacity, usedCountsByName);
                    sideCapacity -= draftedSideGold.Count;
                }
                else
                {
                    var shuffledGold = goldCandidates.OrderBy(_ => Random.Shared.Next()).ToList();
                    draftedResGold = DraftItemsIntoSection(shuffledGold, _deck.Resources, reqResGold, resCapacity, usedCountsByName);
                    resCapacity -= draftedResGold.Count;
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

                draftedResAid = DraftItemsIntoSection(aidCandidates, _deck.Resources, neededResAid, resCapacity, usedCountsByName);
                resCapacity -= draftedResAid.Count;
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
                    if (siteReqs.Count > 0 && !siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                    {
                        return false;
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

            // 1. Draft Palantíri Items (Resources -> Sideboard)
            var palantirCandidates = heroDistinctPool
                .Where(c =>
                    IsPalantirItem(c) &&
                    PassesPalantirCardGuards(c, enforceRoleFocus: true, isPalantirItem: true))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var draftedResPal = DraftItemsIntoSection(palantirCandidates, _deck.Resources, reqResPal, resCapacity, usedCountsByName);
            resCapacity -= draftedResPal.Count;
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

                // Already in deck (Resources or Sideboard): 0 extra cards needed
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

                // Ensure at least one Gold Ring with the best available One Ring odds (e.g. Precious Gold Ring)
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

                // Ensure an Allow Ring Test source that can bring The One Ring into play.
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

                // Prefer branch requiring the fewest new cards (e.g. already-satisfied branch first)
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
                List<Card> draftedExtrasOut)
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

                            bool hasPrereqTag = _filterService.GetCardTagsForCategory(candidate.Id, "Prerequisite").Count > 0;

                            // Tier 1: Quest has a Prerequisite that is ALREADY satisfied in the deck
                            // Tier 2: Standalone quests (no Prerequisite) and self-contained prerequisite quests compete equally
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

                        remainingCapacity -= 1 + chosenExtras.Count;
                    }
                }
            }

            var draftedResQuests = new List<Card>();
            var draftedResExtras = new List<Card>();
            DraftQuestsIntoSection(_deck.Resources, reqRes, ref resCapacity, draftedResQuests, draftedResExtras);

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

        private bool HasSpecialAbility(Card card, string abilityTag) =>
            card != null &&
            _filterService.GetCardTagsForCategory(card.Id, "Special Ability")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, abilityTag));

        private bool HasRoleTag(Card card, string roleTag) =>
            card != null &&
            _filterService.GetCardTagsForCategory(card.Id, "Role")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, roleTag));

        private bool IsQuestPackageCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
            IsHeroPlayableAlignment(card.Alignment) &&
            HasRoleTag(card, "Quest");

        private static bool IsResourceEventPackageCard(Card card) =>
            card != null &&
            ((string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
              card.Secondary != null &&
              card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase)) ||
             CardFilterService.EqualsWithDiacriticFallback(card.Name, "Twilight"));

        private bool IsGeneralResourceEvent(Card card) =>
            IsResourceEventPackageCard(card) &&
            !IsRingPackageCard(card) &&
            !IsPalantirPackageCard(card) &&
            !IsQuestPackageCard(card);

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

        private static int CountEffectiveSpecialAbility(IEnumerable<Card> cards)
        {
            int count = 0;
            var seenUnique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cards)
            {
                if (string.Equals(c.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenUnique.Add(c.Name))
                    {
                        count++;
                    }
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatInnateAbilitySummary(List<Card> cards, int effectiveCount)
        {
            string raw = FormatCardCountList(cards);
            return effectiveCount < cards.Count
                ? $"{raw} (counts as {effectiveCount} unique)"
                : raw;
        }

        private static bool MatchesSkillOrRaceRequirements(Card card, List<Card> allDraftedChars)
        {
            if (string.IsNullOrWhiteSpace(card.Skill)) return true;

            var tokens = card.Skill.Split([' ', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tokens.Any(tok => allDraftedChars.Any(ch =>
                CardFilterService.MatchesCardnumCriterion(ch, "Skill", tok) ||
                CardFilterService.MatchesCardnumCriterion(ch, "Race", tok)));
        }

        private static bool IsPalantirItem(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
            IsItemCard(card) &&
            HasRaceToken(card, "Palantír");

        private bool IsPalantirPackageCard(Card card)
        {
            if (card == null) return false;
            if (IsPalantirItem(card)) return true;

            if (card.Secondary != null && card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                return HasSpecialAbility(card, "Palantír: Allow Use");
            }

            return false;
        }

        private static List<Card> DraftCardsRankedPlaysetFirst(
            List<Card> rankedCandidates,
            List<Card> targetSection,
            int requestedCount,
            int sectionCapacity,
            Dictionary<string, int> usedCountsByName)
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
                    targetSection.Add(card);
                    current++;
                    usedCountsByName[card.Name] = current;
                    drafted.Add(card);
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

        private static bool IsStandardTierItem(Card card)
        {
            if (card == null) return false;
            return string.Equals(card.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRingPackageCard(Card card)
        {
            if (card == null) return false;
            if (string.Equals(card.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase)) return true;
            if (IsSpecialRingItem(card)) return true;

            if (card.Secondary != null && card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                return HasSpecialAbility(card, "Gold Ring: Allow Test") ||
                       HasSpecialAbility(card, "Gold Ring: Aid Test");
            }

            return false;
        }

        private bool IsSpecialRingItem(Card card)
        {
            if (card == null || !string.Equals(card.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsOneRingCard(card) ||
                   IsDwarvenRingCard(card) ||
                   IsMagicRingCard(card) ||
                   IsLesserRingCard(card) ||
                   IsWizardsRingCard(card);
        }

        private static bool IsSpecialItemCard(Card c) =>
            c != null && string.Equals(c.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase);

        private static bool IsOneRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "The One Ring") ||
             HasRaceToken(c, "One Ring") ||
             CardFilterService.EqualsWithDiacriticFallback(c.Name, "The One Ring"));

        private static bool IsDwarvenRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Dwarven Ring") ||
             c.Name.StartsWith("Dwarven Ring", StringComparison.OrdinalIgnoreCase));

        private static bool IsMagicRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Magic Ring") ||
             c.Name.StartsWith("Magic Ring", StringComparison.OrdinalIgnoreCase));

        private static bool IsLesserRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Lesser Ring") ||
             CardFilterService.EqualsWithDiacriticFallback(c.Name, "Lesser Ring"));

        private bool IsWizardsRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            HasRaceToken(c, "Ring") &&
            (CardFilterService.MatchesCardnumCriterion(c, "Skill", "Wizard") ||
             _filterService.GetCardTagsForCategory(c.Id, "Target Species")
                 .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, "Wizard")));

        private static string FormatCardCountList(IEnumerable<Card> cards) =>
            string.Join(", ", cards
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Count() > 1 ? $"{g.Count()}x {g.First().Name}" : g.First().Name));

        private bool MatchesItemSitePlayability(Card item, List<Card> allSitesInDeck)
        {
            if (allSitesInDeck == null || allSitesInDeck.Count == 0) return false;

            var siteReqs = _filterService.GetCardTagsForCategory(item.Id, "Site Requirement");
            if (siteReqs.Count > 0)
            {
                return siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService));
            }

            bool needsHoard = string.Equals(item.Hoard, "yes", StringComparison.OrdinalIgnoreCase);

            return allSitesInDeck.Any(site =>
            {
                if (needsHoard && !string.Equals(site.Hoard, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(item.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.MinorItem, "yes", StringComparison.OrdinalIgnoreCase);
                }

                if (string.Equals(item.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.MajorItem, "yes", StringComparison.OrdinalIgnoreCase);
                }

                if (string.Equals(item.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                           (HasRaceToken(item, "Scroll") && string.Equals(site.Scroll, "yes", StringComparison.OrdinalIgnoreCase));
                }

                return false;
            });
        }

        private bool MatchesItemRegionRequirement(
            Card item,
            HashSet<string> draftedRegionNames,
            HashSet<string> draftedRegionTypes)
        {
            var regReqs = _filterService.GetCardTagsForCategory(item.Id, "Region Requirement");
            if (regReqs.Count == 0) return true;

            return regReqs.Any(req =>
                draftedRegionNames.Any(rn => CardFilterService.EqualsWithDiacriticFallback(rn, req)) ||
                draftedRegionTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, req)));
        }

        private bool MatchesItemTargetSpecies(Card item, List<Card> allDraftedChars)
        {
            var speciesTags = _filterService.GetCardTagsForCategory(item.Id, "Target Species");
            if (speciesTags.Count == 0) return true;

            var requiredHeroRaces = speciesTags
                .Where(tag => HeroTargetSpecies.Any(hr => CardFilterService.EqualsWithDiacriticFallback(tag, hr)))
                .ToList();

            // Ignore Creature-only Target Species on Items
            if (requiredHeroRaces.Count == 0) return true;

            foreach (var ch in allDraftedChars)
            {
                if (string.IsNullOrWhiteSpace(ch.Race)) continue;
                var charRaces = ch.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (charRaces.Any(cr => requiredHeroRaces.Any(req => CardFilterService.EqualsWithDiacriticFallback(cr, req))))
                {
                    return true;
                }
            }

            return false;
        }

        private bool SatisfiesExistingPrerequisites(Card card, List<Card> existingDeckCards)
        {
            var prereqTags = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite");
            if (prereqTags.Count == 0) return true;

            foreach (var orBranch in prereqTags)
            {
                var andTerms = orBranch.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (andTerms.Length == 0) continue;

                bool allPresent = andTerms.All(term =>
                    existingDeckCards.Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, term)));

                if (allPresent) return true;
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

            // Semicolon-separated entries in Prerequisite are OR branches; "&&" inside an entry requires all terms (AND).
            foreach (var orBranch in prereqTags)
            {
                var andTerms = orBranch.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (andTerms.Length == 0) continue;

                var branchResolved = new List<Card>();
                var branchVisited = new HashSet<string>(visitedNames, StringComparer.OrdinalIgnoreCase);
                bool branchSucceeded = true;

                foreach (var prereqName in andTerms)
                {
                    var prereqCard = allCards
                        .Where(c =>
                            (activeSets == null || activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                            CardFilterService.EqualsWithDiacriticFallback(c.Name, prereqName) &&
                            IsHeroPlayableAlignment(c.Alignment))
                        .OrderBy(c => string.Equals(c.Alignment, "Hero", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .FirstOrDefault();

                    if (prereqCard == null)
                    {
                        branchSucceeded = false;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(prereqCard.Specific) &&
                        !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, prereqCard.Specific)))
                    {
                        branchSucceeded = false;
                        break;
                    }

                    var prereqSiteReqs = _filterService.GetCardTagsForCategory(prereqCard.Id, "Site Requirement");
                    if (prereqSiteReqs.Count > 0 &&
                        !prereqSiteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                    {
                        branchSucceeded = false;
                        break;
                    }

                    if (!resolvedPrereqs.Any(p => CardFilterService.EqualsWithDiacriticFallback(p.Name, prereqCard.Name)) &&
                        !branchResolved.Any(p => CardFilterService.EqualsWithDiacriticFallback(p.Name, prereqCard.Name)))
                    {
                        branchResolved.Add(prereqCard);
                    }

                    if (!TryResolvePrerequisiteChain(
                        prereqCard,
                        allCards,
                        activeSets,
                        allSitesInDeck,
                        allDraftedChars,
                        branchResolved,
                        branchVisited))
                    {
                        branchSucceeded = false;
                        break;
                    }
                }

                if (branchSucceeded)
                {
                    foreach (var p in branchResolved)
                    {
                        if (!resolvedPrereqs.Any(existing => CardFilterService.EqualsWithDiacriticFallback(existing.Name, p.Name)))
                        {
                            resolvedPrereqs.Add(p);
                        }
                    }

                    foreach (var v in branchVisited)
                    {
                        visitedNames.Add(v);
                    }
                    return true;
                }
            }

            return false;
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

        private static bool SiteMatchesSingleRequirement(Card site, string cleanReq, CardFilterService filterService)
        {
            if (site == null || string.IsNullOrWhiteSpace(cleanReq)) return false;

            if (CardFilterService.TryParsePredicate(cleanReq, out _, out _, out _))
            {
                return CardFilterService.MatchesPredicate(site, cleanReq, filterService);
            }

            if (CanonicalHoldTypes.Contains(CardFilterService.NormalizeSiteType(cleanReq)))
            {
                return CardFilterService.MatchesSiteType(site.Site, cleanReq);
            }

            return CardFilterService.EqualsWithDiacriticFallback(site.Name, cleanReq) ||
                   string.Equals(site.Name.Replace("-", " "), cleanReq.Replace("-", " "), StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesAnySiteRequirement(List<Card> sites, string requirement, CardFilterService filterService)
        {
            if (sites == null || sites.Count == 0 || string.IsNullOrWhiteSpace(requirement)) return false;

            string cleanReq = requirement.Trim();
            if (cleanReq.Contains("&&"))
            {
                var andTerms = cleanReq.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return andTerms.Length > 0 && andTerms.All(term => MatchesAnySiteRequirement(sites, term, filterService));
            }

            return sites.Any(s => SiteMatchesSingleRequirement(s, cleanReq, filterService));
        }

        private static bool IsHeroPlayableAlignment(string alignment)
        {
            if (string.IsNullOrWhiteSpace(alignment)) return true;
            return string.Equals(alignment, "Hero", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Neutral", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Dual", StringComparison.OrdinalIgnoreCase);
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
                    MessageBox.Show(this, "Please select at least one Role under Role Focus on the Resource Events tab.", "Resource Events", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var eligiblePool = allCards
                .Where(c =>
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    IsEligibleBaseResourceEvent(c))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null && PassesResourceEventGuards(c))
                .ToList();

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

            string statusMsg =
                $"Drafted {resAll.Count} event(s) into Resources and {sideAll.Count} into Sideboard:\n" +
                $"• Resources: {resText}\n" +
                $"• Sideboard: {sideText}";

            if (!string.IsNullOrWhiteSpace(resRoleSummary))
            {
                statusMsg += $"\n• Resources Role Allocation: {resRoleSummary}";
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

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblResourceEventsStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblResourceEventsStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblResourceEventsStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblResourceEventsStatus.Text = statusMsg;
            return true;
        }

        private (List<Card> Drafted, string RoleSummary) DraftEventsProportionately(
            List<Card> eligiblePool,
            HashSet<string> checkedRoles,
            List<Card> targetSection,
            int slotsToFill,
            Dictionary<string, int> usedCountsByName)
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

            // Pick narrowest buckets first so specialized roles claim a card before broad multi-role pools
            foreach (var (Role, Cards, Quota) in buckets.Where(b => b.Quota > 0).OrderBy(b => b.Cards.Count).ThenBy(_ => Random.Shared.Next()))
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
                    countsPerRole[Role] = countsPerRole.GetValueOrDefault(Role, 0) + 1;
                }
            }

            // Fallback top-up if any narrow bucket hit copy-limit exhaustion
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
                _lblCreaturesStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblCreaturesStatus.Text = "No creatures or Nazgûl configured (0 drafted).";
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
                    MessageBox.Show(this, "Please select at least one Species / Race under Species / Race Focus on the Creatures tab.", "Creatures", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            // Build catalog lookup maps for Unknown Opponent mode (named Region -> terrains; named Site -> hold type)
            var catalogRegionTypesByName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in allCards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name)))
            {
                string rName = regCard.Name.Trim();
                if (!catalogRegionTypesByName.TryGetValue(rName, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    catalogRegionTypesByName[rName] = set;
                }
                foreach (var rt in DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService))
                {
                    if (!string.IsNullOrWhiteSpace(rt)) set.Add(rt.Trim());
                }
            }

            var catalogSiteHoldByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var siteCard in allCards.Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Site)))
            {
                catalogSiteHoldByName.TryAdd(siteCard.Name.Trim(), siteCard.Site.Trim());
            }

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

            bool PassesUnknownOpponentKeying(Card card)
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

                return tailorToDeck ? IsKeyableToTargetDeck(c) : PassesUnknownOpponentKeying(c);
            }

            var standardPool = heroDistinctHazards
                .Where(c => IsStandardCreatureCard(c) && PassesCreatureFilters(c))
                .ToList();

            var dualPool = heroDistinctHazards
                .Where(c => IsDualModeNonNazgulCreature(c) && PassesCreatureFilters(c))
                .ToList();

            var (draftedHazStd, hazStdSummary) = DraftCreaturesProportionately(
                standardPool, checkedSpecies, _deck.Hazards, Math.Min(reqHazStd, hazCapacity), usedCountsByName);
            hazCapacity -= draftedHazStd.Count;

            var (draftedSideStd, _) = DraftCreaturesProportionately(
                standardPool, checkedSpecies, _deck.Sideboard, Math.Min(reqSideStd, sideCapacity), usedCountsByName);
            sideCapacity -= draftedSideStd.Count;

            var (draftedHazDual, hazDualSummary) = DraftCreaturesProportionately(
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

            string hazText = hazAll.Count > 0 ? FormatCardCountList(hazAll) : "(None)";
            string sideText = sideAll.Count > 0 ? FormatCardCountList(sideAll) : "(None)";

            string statusMsg =
                $"Drafted {hazAll.Count} card(s) into Hazards (Creature Equiv: {hazEquiv}/12 min) and {sideAll.Count} into Sideboard:\n" +
                $"• Hazards: {hazText}\n" +
                $"• Sideboard: {sideText}";

            if (!string.IsNullOrWhiteSpace(hazStdSummary))
            {
                statusMsg += $"\n• Standard Species Mix: {hazStdSummary}";
            }
            if (!string.IsNullOrWhiteSpace(hazDualSummary))
            {
                statusMsg += $"\n• Dual-Mode Species Mix: {hazDualSummary}";
            }

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

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblCreaturesStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblCreaturesStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblCreaturesStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblCreaturesStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblCreaturesStatus.Text = statusMsg;
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

        private static bool IsNazgulCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            HasRaceToken(card, "Nazgûl");

        private bool IsNazgulSupportCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            !HasRaceToken(card, "Nazgûl") &&
            card.Secondary != null &&
            card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase) &&
            _filterService.GetCardTagsForCategory(card.Id, "Target Species")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, "Nazgûl"));

        private static bool IsStandardCreatureCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(card.Secondary, "Creature", StringComparison.OrdinalIgnoreCase);

        private static bool IsDualModeNonNazgulCreature(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            !HasRaceToken(card, "Nazgûl") &&
            card.Secondary != null &&
            card.Secondary.Contains("Creature", StringComparison.OrdinalIgnoreCase) &&
            card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase);

        private bool IsCreaturesTabCard(Card card) =>
            IsNazgulCard(card) ||
            IsNazgulSupportCard(card) ||
            IsStandardCreatureCard(card) ||
            IsDualModeNonNazgulCreature(card);

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

            int fewestWarnings = int.MaxValue;
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

                    if (warningTabs.Count <= fewestWarnings)
                    {
                        fewestWarnings = warningTabs.Count;
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
            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = "Running diagnostic pass...";
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

        private static (int Base, int Conditional) ParseCardMPs(string mpsStr)
        {
            if (string.IsNullOrWhiteSpace(mpsStr)) return (0, 0);

            string trimmed = mpsStr.Trim();
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)) return (0, 0);

            int openParen = trimmed.IndexOf('(');
            int closeParen = openParen >= 0 ? trimmed.IndexOf(')', openParen + 1) : -1;

            // Purely parenthesized e.g. "(2)" -> 0 base, 2 conditional
            if (openParen == 0 && closeParen > 0)
            {
                string inner = trimmed[1..closeParen].Trim();
                return int.TryParse(inner, out int condOnly) ? (0, condOnly) : (0, 0);
            }

            // Base + conditional e.g. "4(6)" -> 4 base, 6 conditional
            if (openParen > 0 && closeParen > openParen)
            {
                string basePart = trimmed[..openParen].Trim();
                string condPart = trimmed[(openParen + 1)..closeParen].Trim();

                int baseVal = int.TryParse(basePart, out int b) ? b : 0;
                int condVal = int.TryParse(condPart, out int c) ? c : baseVal;
                return (baseVal, condVal);
            }

            // Plain integer e.g. "3", "-2" -> (3, 3)
            return int.TryParse(trimmed, out int plain) ? (plain, plain) : (0, 0);
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
        public int DecksBeforeCouncil { get; set; } = 1;
        public bool TailorHazardsToCurrentDeck { get; set; }

        // Tab 2: Regions & Sites
        public string SeedRegion { get; set; } = "Random";
        public int RegionCount { get; set; } = 5;
        public int MaxSiteCount { get; set; } = 12;
        public List<string> SelectedRegionTypes { get; set; } = [];
        public List<string> SelectedSiteTypes { get; set; } = [];

        // Tab 3: Starting Company
        public List<CharacterRowSettings> StarterRows { get; set; } = [];

        // Tab 4: Wizards & Recruits
        public string PrimaryWizard { get; set; } = "Random";
        public int PrimaryWizardCopies { get; set; } = 2;
        public string SecondaryWizard { get; set; } = "Random";
        public int SecondaryWizardCopies { get; set; } = 1;
        public string SecondaryWizardDestination { get; set; } = "Sideboard";

        public List<CharacterRowSettings> RecruitRows { get; set; } = [];
        public List<CharacterRowSettings> ReserveRows { get; set; } = [];

        // Tab 5: Factions
        public int ResourceFactionCount { get; set; } = 1;
        public int SideboardFactionCount { get; set; } = 0;

        // Tab 6: Allies
        public int ResourceAllyCount { get; set; } = 1;
        public int SideboardAllyCount { get; set; } = 0;

        // Tab 7: Items
        public int PoolMinorItemCount { get; set; } = 2;
        public int ResourceMinorItemCount { get; set; } = 1;
        public int SideboardMinorItemCount { get; set; } = 0;
        public int ResourceMajorItemCount { get; set; } = 1;
        public int SideboardMajorItemCount { get; set; } = 1;
        public int ResourceGreaterItemCount { get; set; } = 1;
        public int SideboardGreaterItemCount { get; set; } = 0;
        public int ResourceSpecialItemCount { get; set; } = 0;
        public int SideboardSpecialItemCount { get; set; } = 0;
        public List<string> ExcludedItemRoles { get; set; } = [];

        // Tab 8: Rings
        public int ResourceGoldRingCount { get; set; } = 0;
        public int SideboardGoldRingCount { get; set; } = 0;
        public int ResourceAllowRingTestCount { get; set; } = 0;
        public int SideboardAllowRingTestCount { get; set; } = 0;
        public int ResourceAidRingTestCount { get; set; } = 0;
        public int SideboardAidRingTestCount { get; set; } = 0;
        public int ResourceOneRingCount { get; set; } = 0;
        public int SideboardOneRingCount { get; set; } = 0;
        public int ResourceDwarvenRingCount { get; set; } = 0;
        public int SideboardDwarvenRingCount { get; set; } = 0;
        public int ResourceMagicRingCount { get; set; } = 0;
        public int SideboardMagicRingCount { get; set; } = 0;
        public int ResourceLesserRingCount { get; set; } = 0;
        public int SideboardLesserRingCount { get; set; } = 0;
        public int ResourceWizardsRingCount { get; set; } = 0;
        public int SideboardWizardsRingCount { get; set; } = 0;
        public List<string> ExcludedRingRoles { get; set; } = [];

        // Tab 9: Palantiri
        public int ResourcePalantirCount { get; set; } = 0;
        public int SideboardPalantirCount { get; set; } = 0;
        public int ResourceAllowPalantirUseCount { get; set; } = 0;
        public int SideboardAllowPalantirUseCount { get; set; } = 0;
        public List<string> ExcludedPalantirRoles { get; set; } = [];

        // Tab 10: Quests
        public int ResourceQuestCount { get; set; } = 0;
        public int SideboardQuestCount { get; set; } = 0;
        public bool AutoDraftOneRingPackage { get; set; } = true;
        public List<string> ExcludedQuestRoles { get; set; } = [];

        // Tab 11: Resource Events
        public int ResourceGatesOfMorningCount { get; set; } = 0;
        public int SideboardGatesOfMorningCount { get; set; } = 0;
        public int ResourceTwilightCount { get; set; } = 0;
        public int SideboardTwilightCount { get; set; } = 0;
        public int SideboardResourceEventCount { get; set; } = 4;
        public List<string> ExcludedResourceEventRoles { get; set; } = [];

        // Tab 12: Creatures
        public int HazardNazgulCount { get; set; } = 0;
        public int SideboardNazgulCount { get; set; } = 0;
        public int HazardNazgulSupportCount { get; set; } = 0;
        public int SideboardNazgulSupportCount { get; set; } = 0;
        public int HazardStandardCreatureCount { get; set; } = 12;
        public int SideboardStandardCreatureCount { get; set; } = 2;
        public int HazardDualCreatureCount { get; set; } = 0;
        public int SideboardDualCreatureCount { get; set; } = 0;
        public List<string> ExcludedCreatureSpecies { get; set; } = [];
        public List<string> SelectedCreatureRegionTypes { get; set; }
        public List<string> SelectedCreatureSiteTypes { get; set; }
    }
}