using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed partial class DeckAssemblyMatrixDialog : Form
    {
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

        // Navigation & Pipeline infrastructure
        private readonly Panel _pnlSidebar;
        private readonly Panel _pnlNavIndicator;
        private readonly Panel _pnlPageHost;
        private readonly Dictionary<string, Panel> _pages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _navButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Font _navRegularFont = new("Segoe UI", 9.0f, FontStyle.Regular);
        private readonly Font _navBoldFont = new("Segoe UI", 9.0f, FontStyle.Bold);
        private string _activePageKey = "General Settings";
        private sealed record RunnableTabInfo(Func<bool, bool> Runner, Control StatusControl);
        private readonly Dictionary<string, RunnableTabInfo> _tabInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _pipelineOrder =
        [
            "Regions & Sites", "Starting Company", "Wizards & Recruits", "Factions",
            "Allies", "Rings", "Palantíri", "Items", "Quests", "Resource Events",
            "Creatures", "Hazard Events"
        ];

        // --- TAB 1: GENERAL SETTINGS & CONTROLS ---
        private TextBox _txtDeckTitle;
        private NumericUpDown _numPlayDeckSize;
        private NumericUpDown _numSideboardSize;
        private Button _btnRunPipeline;
        private Button _btnClearDeck;
        private Button _btnSaveSnapshot;
        private Button _btnRecallSnapshot;
        private Label _lblPipelineStatus;
        private RichTextBox _rtbDeckSummary;
        private NumericUpDown _numDecksBeforeCouncil;
        private CheckBox _chkTailorHazardsToCurrentDeck;

        // --- TAB 2: REGIONS & SITES ---
        private ComboBox _cmbSeedRegion;
        private NumericUpDown _numRegionCount;
        private NumericUpDown _numMaxSiteCount;
        private CheckBox _chkRequireHavenInRegionGroup;
        private CheckedListBox _clbRegionTypes;
        private CheckedListBox _clbSiteTypes;
        private RichTextBox _rtbTheaterStatus;
        private bool _isUpdatingSeedDropdown;

        // --- TAB 3: STARTING COMPANY ---
        private readonly List<CharacterRowControls> _starterRows = [];
        private RichTextBox _rtbStarterStatus;

        // --- TAB 4: WIZARDS & RECRUITS ---
        private ComboBox _cmbWizard1;
        private ComboBox _cmbWizard1Copies;
        private ComboBox _cmbWizard2;
        private ComboBox _cmbWizard2Copies;
        private ComboBox _cmbWizard2Dest;
        private bool _isUpdatingWizardControls;

        private readonly List<CharacterRowControls> _recruitRows = [];
        private readonly List<CharacterRowControls> _reserveRows = [];
        private RichTextBox _rtbRecruitStatus;

        // --- TAB 5: FACTIONS ---
        private NumericUpDown _numResourceFactions;
        private NumericUpDown _numSideboardFactions;
        private NumericUpDown _numResourceFactionSupport;
        private NumericUpDown _numSideboardFactionSupport;
        private RichTextBox _rtbFactionStatus;

        // --- TAB 6: ALLIES ---
        private NumericUpDown _numResourceAllies;
        private NumericUpDown _numSideboardAllies;
        private RichTextBox _rtbAllyStatus;

        // --- TAB 7: ITEMS ---
        private NumericUpDown _numPoolMinorItems;
        private NumericUpDown _numResourceMinorItems;
        private NumericUpDown _numSideboardMinorItems;
        private NumericUpDown _numResourceMajorItems;
        private NumericUpDown _numSideboardMajorItems;
        private NumericUpDown _numResourceGreaterItems;
        private NumericUpDown _numSideboardGreaterItems;
        private NumericUpDown _numResourceSpecialItems;
        private NumericUpDown _numSideboardSpecialItems;
        private CheckedListBox _clbItemRoles;
        private RichTextBox _rtbItemStatus;

        // --- TAB 8: RINGS ---
        private NumericUpDown _numResGoldRings;
        private NumericUpDown _numSideGoldRings;
        private NumericUpDown _numResAllowRingTest;
        private NumericUpDown _numSideAllowRingTest;
        private NumericUpDown _numResAidRingTest;
        private NumericUpDown _numSideAidRingTest;
        private NumericUpDown _numResOneRing;
        private NumericUpDown _numSideOneRing;
        private NumericUpDown _numResDwarvenRings;
        private NumericUpDown _numSideDwarvenRings;
        private NumericUpDown _numResMagicRings;
        private NumericUpDown _numSideMagicRings;
        private NumericUpDown _numResLesserRings;
        private NumericUpDown _numSideLesserRings;
        private NumericUpDown _numResWizardsRing;
        private NumericUpDown _numSideWizardsRing;
        private RichTextBox _rtbRingsStatus;
        private CheckedListBox _clbRingRoles;

        // --- TAB 9: PALANTIRI ---
        private NumericUpDown _numResPalantiri;
        private NumericUpDown _numSidePalantiri;
        private NumericUpDown _numResAllowPalantirUse;
        private NumericUpDown _numSideAllowPalantirUse;
        private CheckedListBox _clbPalantirRoles;
        private RichTextBox _rtbPalantiriStatus;

        // --- TAB 10: QUESTS ---
        private NumericUpDown _numResourceQuests;
        private NumericUpDown _numSideboardQuests;
        private CheckBox _chkAutoDraftOneRingPackage;
        private CheckedListBox _clbQuestRoles;
        private RichTextBox _rtbQuestsStatus;

        // --- TAB 11: RESOURCE EVENTS ---
        private NumericUpDown _numResGatesOfMorning;
        private NumericUpDown _numSideGatesOfMorning;
        private NumericUpDown _numResTwilight;
        private NumericUpDown _numSideTwilight;
        private NumericUpDown _numSideboardResourceEvents;
        private CheckedListBox _clbResourceEventRoles;
        private RichTextBox _rtbResourceEventsStatus;

        // --- TAB 12: CREATURES ---
        private NumericUpDown _numHazNazgul;
        private NumericUpDown _numSideNazgul;
        private NumericUpDown _numHazNazgulSupport;
        private NumericUpDown _numSideNazgulSupport;
        private NumericUpDown _numHazStandardCreatures;
        private NumericUpDown _numSideStandardCreatures;
        private NumericUpDown _numHazDualCreatures;
        private NumericUpDown _numSideDualCreatures;
        private Label _lblCreatureEquivReadout;
        private CheckedListBox _clbCreatureSpecies;
        private CheckedListBox _clbCreatureRegionTypes;
        private CheckedListBox _clbCreatureSiteTypes;
        private Button _btnAllCreatureKeying;
        private Button _btnClearCreatureKeying;
        private Label _lblCreatureKeyingNote;
        private RichTextBox _rtbCreaturesStatus;

        // --- TAB 13: HAZARD EVENTS ---
        private NumericUpDown _numHazDoorsOfNight;
        private NumericUpDown _numSideDoorsOfNight;
        private NumericUpDown _numHazTwilight;
        private NumericUpDown _numSideHazTwilight;
        private NumericUpDown _numSideboardHazardEvents;
        private CheckedListBox _clbHazardEventRoles;
        private CheckedListBox _clbHazardEventRegionTypes;
        private CheckedListBox _clbHazardEventSiteTypes;
        private Button _btnAllHazardEventKeying;
        private Button _btnClearHazardEventKeying;
        private Label _lblHazardEventKeyingNote;
        private RichTextBox _rtbHazardEventsStatus;

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
            ClientSize = new Size(1056, 676);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.0f);

            _pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 208,
                BackColor = Color.FromArgb(242, 245, 249)
            };

            var pnlSidebarBorder = new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = Color.FromArgb(203, 213, 224)
            };
            _pnlSidebar.Controls.Add(pnlSidebarBorder);

            _pnlNavIndicator = new Panel
            {
                Size = new Size(4, 32),
                Location = new Point(0, 12),
                BackColor = Color.FromArgb(43, 108, 176)
            };
            _pnlSidebar.Controls.Add(_pnlNavIndicator);

            _pnlPageHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control
            };

            var tabGeneral = BuildGeneralPage();
            var tabTheater = BuildRegionsAndSitesPage();
            var tabStarter = BuildStartingCompanyPage();
            var tabRecruits = BuildWizardsAndRecruitsPage();
            var tabFactions = BuildFactionsPage();
            var tabAllies = BuildAlliesPage();
            var tabRings = BuildRingsPage();
            var tabPalantiri = BuildPalantiriPage();
            var tabItems = BuildItemsPage();
            var tabQuests = BuildQuestsPage();
            var tabResourceEvents = BuildResourceEventsPage();
            var tabCreatures = BuildCreaturesPage();
            var tabHazardEvents = BuildHazardEventsPage();

            var navEntries = new (string Key, string Label, Panel Page)[]
            {
                ("General Settings", "General Settings", tabGeneral),
                ("Regions & Sites", "1. Regions & Sites", tabTheater),
                ("Starting Company", "2. Starting Company", tabStarter),
                ("Wizards & Recruits", "3. Wizards & Recruits", tabRecruits),
                ("Factions", "4. Factions", tabFactions),
                ("Allies", "5. Allies", tabAllies),
                ("Rings", "6. Rings", tabRings),
                ("Palantíri", "7. Palantíri", tabPalantiri),
                ("Items", "8. Items", tabItems),
                ("Quests", "9. Quests", tabQuests),
                ("Resource Events", "10. Resource Events", tabResourceEvents),
                ("Creatures", "11. Creatures", tabCreatures),
                ("Hazard Events", "12. Hazard Events", tabHazardEvents)
            };

            int navY = 12;
            for (int i = 0; i < navEntries.Length; i++)
            {
                var (key, label, page) = navEntries[i];
                _pages[key] = page;
                _pnlPageHost.Controls.Add(page);

                if (i == 1)
                {
                    var divider = new Panel
                    {
                        Location = new Point(12, 51),
                        Size = new Size(183, 1),
                        BackColor = Color.FromArgb(203, 213, 224)
                    };
                    var lblSteps = new Label
                    {
                        Text = "PIPELINE STEPS",
                        Location = new Point(12, 58),
                        Size = new Size(183, 16),
                        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(113, 128, 150)
                    };
                    _pnlSidebar.Controls.AddRange([divider, lblSteps]);
                    navY = 78;
                }

                var btn = new Button
                {
                    Text = label,
                    Tag = key,
                    Location = new Point(0, navY),
                    Size = new Size(207, 32),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 234, 244);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(214, 226, 240);
                btn.Click += (s, e) => SelectPage(key);

                _navButtons[key] = btn;
                _pnlSidebar.Controls.Add(btn);
                navY += 32;
            }

            _tabInfo["Regions & Sites"] = new(RunRegionsAndSitesDraft, _rtbTheaterStatus);
            _tabInfo["Starting Company"] = new(RunStartingCompanyDraft, _rtbStarterStatus);
            _tabInfo["Wizards & Recruits"] = new(RunNonStartingCharactersDraft, _rtbRecruitStatus);
            _tabInfo["Factions"] = new(RunFactionsDraft, _rtbFactionStatus);
            _tabInfo["Allies"] = new(RunAlliesDraft, _rtbAllyStatus);
            _tabInfo["Items"] = new(RunItemsDraft, _rtbItemStatus);
            _tabInfo["Rings"] = new(RunRingsDraft, _rtbRingsStatus);
            _tabInfo["Palantíri"] = new(RunPalantiriDraft, _rtbPalantiriStatus);
            _tabInfo["Quests"] = new(RunQuestsDraft, _rtbQuestsStatus);
            _tabInfo["Resource Events"] = new(RunResourceEventsDraft, _rtbResourceEventsStatus);
            _tabInfo["Creatures"] = new(RunCreaturesDraft, _rtbCreaturesStatus);
            _tabInfo["Hazard Events"] = new(RunHazardEventsDraft, _rtbHazardEventsStatus);

            var pnlTopBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(203, 213, 224)
            };

            Controls.Add(_pnlPageHost);
            Controls.Add(_pnlSidebar);
            Controls.Add(pnlTopBorder);
            SelectPage("General Settings");

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
                    PopulateHazardEventRolesChecklist();
                    UpdateHazardKeyingEnabledState();
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
            PopulateHazardEventRolesChecklist();
            UpdateHazardKeyingEnabledState();
            UpdateCreatureEquivalentsReadout();
            UpdateSeedTooltip();
            UpdateDeckSummary();
            ValidateWorkbenchReadiness();
        }

        private Panel BuildGeneralPage()
        {
            var tabGeneral = new Panel { Dock = DockStyle.Fill, Visible = false };
            var grpGeneral = new GroupBox
            {
                Text = "Target Deck Sizing && Workbench Profiles",
                Location = new Point(16, 16),
                Size = new Size(810, 220)
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
                "Each non-haven site can be tapped once per deck cycle (1-4) across Resources Factions, Allies, Items, Gold Rings, Palantíri, and Quest Items.\n" +
                "Havens never tap and have unlimited capacity.");

            var lblDecksHint = new Label
            {
                Text = "deck cycle(s) planned (non-haven sites allow 1 Resources tap per cycle; havens never tap).",
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
                UpdateHazardKeyingEnabledState();
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
                Size = new Size(112, 26)
            };
            btnSaveSettings.Click += (s, e) => SaveSettingsToFile();

            var btnLoadSettings = new Button
            {
                Text = "Load Settings...",
                Location = new Point(273, 186),
                Size = new Size(112, 26)
            };
            btnLoadSettings.Click += (s, e) => LoadSettingsFromFile();

            var btnResetDefaults = new Button
            {
                Text = "Reset Defaults",
                Location = new Point(391, 186),
                Size = new Size(112, 26)
            };
            btnResetDefaults.Click += (s, e) => ResetSettingsToDefaults();
            _toolTip.SetToolTip(btnResetDefaults, "Resets all workbench tabs, filters, and counts back to factory defaults.");

            var lblProfileHint = new Label
            {
                Text = "Save, load (.workbench.json), or reset workbench settings to defaults.",
                Location = new Point(513, 183),
                Size = new Size(288, 34),
                ForeColor = Color.DimGray
            };

            grpGeneral.Controls.AddRange([
                lblTitle, _txtDeckTitle,
                lblPlaySize, _numPlayDeckSize, lblPlaySizeHint,
                lblSideSize, _numSideboardSize, lblSideSizeHint,
                lblDecksBeforeCouncil, _numDecksBeforeCouncil, lblDecksHint,
                lblHazardTargeting, _chkTailorHazardsToCurrentDeck,
                lblProfiles, btnSaveSettings, btnLoadSettings, btnResetDefaults, lblProfileHint
            ]);

            var grpPipeline = new GroupBox
            {
                Text = "Deck Assembly Pipeline && Session Snapshots",
                Location = new Point(16, 242),
                Size = new Size(810, 138)
            };

            _btnRunPipeline = new Button
            {
                Text = "Run Pipeline (F5)",
                Location = new Point(16, 26),
                Size = new Size(145, 30),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunPipeline.Click += (s, e) => RunPipelineDraft();
            _toolTip.SetToolTip(
                _btnRunPipeline,
                "Drafts the full deck in sequence (Shortcut: F5 from any workbench tab).\n" +
                "Automatically retries up to 25 times if random region rolls fail downstream constraints.");

            _btnClearDeck = new Button
            {
                Text = "Clear Deck",
                Location = new Point(169, 26),
                Size = new Size(110, 30)
            };
            _btnClearDeck.Click += BtnClearDeck_Click;
            _toolTip.SetToolTip(_btnClearDeck, "Clears all cards across Pool, Resources, Hazards, Sideboard, and Sites.");

            _btnSaveSnapshot = new Button
            {
                Text = "Save Snapshot",
                Location = new Point(300, 26),
                Size = new Size(125, 30)
            };
            _btnSaveSnapshot.Click += BtnSaveSnapshot_Click;
            _toolTip.SetToolTip(_btnSaveSnapshot, "Saves an in-session snapshot of all currently drafted deck lists so you can re-roll and restore.");

            _btnRecallSnapshot = new Button
            {
                Text = "Recall Snapshot",
                Location = new Point(433, 26),
                Size = new Size(125, 30),
                Enabled = false
            };
            _btnRecallSnapshot.Click += BtnRecallSnapshot_Click;
            _toolTip.SetToolTip(_btnRecallSnapshot, "Restores the complete deck from your last saved session snapshot.");

            _lblPipelineStatus = new Label
            {
                Location = new Point(16, 64),
                Size = new Size(778, 66),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            grpPipeline.Controls.AddRange([
                _btnRunPipeline, _btnClearDeck, _btnSaveSnapshot, _btnRecallSnapshot,
                _lblPipelineStatus
            ]);

            var grpSummary = new GroupBox
            {
                Text = "Live Deck Composition Status",
                Location = new Point(16, 386),
                Size = new Size(810, 186)
            };

            _rtbDeckSummary = CreateStatusRichTextBox(16, 30, 778, 146);
            grpSummary.Controls.Add(_rtbDeckSummary);

            tabGeneral.Controls.AddRange([grpGeneral, grpPipeline, grpSummary]);
            return tabGeneral;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ValidateWorkbenchReadiness();
                if (_btnRunPipeline != null && _btnRunPipeline.Enabled)
                {
                    RunPipelineDraft();
                }
                else
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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
                _navRegularFont?.Dispose();
                _navBoldFont?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void SelectPage(string pageKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || !_pages.ContainsKey(pageKey))
            {
                return;
            }

            _activePageKey = pageKey;

            _pnlPageHost.SuspendLayout();
            foreach (var (key, page) in _pages)
            {
                page.Visible = string.Equals(key, pageKey, StringComparison.OrdinalIgnoreCase);
            }
            _pnlPageHost.ResumeLayout();

            UpdateSidebarVisuals();
        }

        private void UpdateSidebarVisuals()
        {
            if (_pnlSidebar == null || _navButtons.Count == 0) return;

            for (int i = 0; i < _pipelineOrder.Length; i++)
            {
                string key = _pipelineOrder[i];
                if (!_navButtons.TryGetValue(key, out var btn) || !_tabInfo.TryGetValue(key, out var info))
                {
                    continue;
                }

                bool hasAlert = info.StatusControl.Text.Contains('⚠');
                btn.Text = hasAlert ? $"{i + 1}. {key}  ⚠" : $"{i + 1}. {key}";
            }

            foreach (var (key, btn) in _navButtons)
            {
                bool isActive = string.Equals(key, _activePageKey, StringComparison.OrdinalIgnoreCase);
                bool hasError = _tabInfo.TryGetValue(key, out var info) && info.StatusControl.ForeColor == Color.Firebrick;
                bool hasWarning = info != null && info.StatusControl.Text.Contains('⚠');

                btn.BackColor = isActive ? Color.White : Color.FromArgb(242, 245, 249);
                btn.Font = isActive ? _navBoldFont : _navRegularFont;

                if (hasError)
                {
                    btn.ForeColor = Color.Firebrick;
                }
                else if (hasWarning)
                {
                    btn.ForeColor = Color.FromArgb(180, 83, 9);
                }
                else
                {
                    btn.ForeColor = isActive
                        ? Color.FromArgb(26, 54, 93)
                        : Color.FromArgb(45, 55, 72);
                }

                if (isActive && _pnlNavIndicator != null)
                {
                    _pnlNavIndicator.Top = btn.Top;
                    _pnlNavIndicator.Height = btn.Height;
                    _pnlNavIndicator.Visible = true;
                    _pnlNavIndicator.BringToFront();
                }
            }
        }

        private void NotifyDeckChanged(DeckSectionType? section = null)
        {
            _notifyDeckCallback?.Invoke(section);
        }

        public void UpdateDeckSummary()
        {
            if (IsDisposed) return;

            if (_deck.Pool.Count == 0 && !string.Equals(_rtbStarterStatus.Text, StarterIdleStatusText, StringComparison.Ordinal))
            {
                SetRichPlainStatus(_rtbStarterStatus, StarterIdleStatusText, Color.FromArgb(74, 85, 104), FontStyle.Italic);
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
            int hazEvents = _deck.Hazards.Count(IsGeneralHazardEvent);
            int sideAllEvents = _deck.Sideboard.Count(c => IsGeneralResourceEvent(c) || IsGeneralHazardEvent(c));
            int hazNazgul = _deck.Hazards.Count(IsNazgulCard);
            int sideNazgul = _deck.Sideboard.Count(IsNazgulCard);
            int hazNazgulSupport = _deck.Hazards.Count(IsNazgulSupportCard);
            int sideNazgulSupport = _deck.Sideboard.Count(IsNazgulSupportCard);
            int hazStandardCreatures = _deck.Hazards.Count(IsStandardCreatureCard);
            int sideStandardCreatures = _deck.Sideboard.Count(IsStandardCreatureCard);
            int hazDualCreatures = _deck.Hazards.Count(IsDualModeNonNazgulCreature);
            int sideDualCreatures = _deck.Sideboard.Count(IsDualModeNonNazgulCreature);

            if (totalResChars == 0 && totalSideChars == 0 &&
                resFactions == 0 && sideFactions == 0 &&
                resAllies == 0 && sideAllies == 0 &&
                poolItems == 0 && resItems == 0 && sideItems == 0 &&
                resRingCards == 0 && sideRingCards == 0 &&
                resPalantirCards == 0 && sidePalantirCards == 0 &&
                resQuests == 0 && sideQuests == 0 &&
                resEvents == 0 && hazEvents == 0 && sideAllEvents == 0 &&
                hazNazgul == 0 && sideNazgul == 0 &&
                hazNazgulSupport == 0 && sideNazgulSupport == 0 &&
                hazStandardCreatures == 0 && sideStandardCreatures == 0 &&
                hazDualCreatures == 0 && sideDualCreatures == 0)
            {
                ValidateWorkbenchReadiness();
            }

            static string Plural(int count, string singular, string plural = null) =>
                count == 1 ? singular : (plural ?? $"{singular}s");

            int poolChars = _deck.Pool.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            int poolMind = _deck.Pool.Sum(c => ParseCardMind(c.Mind));
            int nonAvatarCharsInRes = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInRes = _deck.Resources.Count(IsAvatar);
            int nonAvatarCharsInSideboard = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInSideboard = _deck.Sideboard.Count(IsAvatar);
            int regionsInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int havensInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int advSitesInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string poolItemNote = poolItems > 0 ? $" + {poolItems} {Plural(poolItems, "item")}" : "";
            string avatarResNote = avatarsInRes > 0 ? $" + {avatarsInRes} {Plural(avatarsInRes, "Avatar")}" : "";
            string resFactionNote = resFactions > 0 ? $", {resFactions} {Plural(resFactions, "faction")}" : "";
            string resAllyNote = resAllies > 0 ? $", {resAllies} {Plural(resAllies, "ally", "allies")}" : "";
            string resItemNote = resItems > 0 ? $", {resItems} {Plural(resItems, "item")}" : "";
            string resRingNote = resRingCards > 0 ? $", {resRingCards} {Plural(resRingCards, "ring card")}" : "";
            string resPalantirNote = resPalantirCards > 0 ? $", {resPalantirCards} {Plural(resPalantirCards, "palantír card")}" : "";
            string resQuestNote = resQuests > 0 ? $", {resQuests} {Plural(resQuests, "quest")}" : "";
            string resEventNote = resEvents > 0 ? $", {resEvents} {Plural(resEvents, "event")}" : "";

            var hazParts = new List<string>();
            if (hazStandardCreatures > 0) hazParts.Add($"{hazStandardCreatures} {Plural(hazStandardCreatures, "creature")}");
            if (hazDualCreatures > 0) hazParts.Add($"{hazDualCreatures} {Plural(hazDualCreatures, "dual")}");
            if (hazNazgul > 0) hazParts.Add($"{hazNazgul} Nazgûl");
            if (hazNazgulSupport > 0) hazParts.Add($"{hazNazgulSupport} Nazgûl support");
            if (hazEvents > 0) hazParts.Add($"{hazEvents} {Plural(hazEvents, "event")}");
            string hazDetailNote = hazParts.Count > 0 ? $" ({string.Join(", ", hazParts)})" : "";

            var sideParts = new List<string>();
            if (nonAvatarCharsInSideboard > 0) sideParts.Add($"{nonAvatarCharsInSideboard} {Plural(nonAvatarCharsInSideboard, "character")}");
            if (avatarsInSideboard > 0) sideParts.Add($"{avatarsInSideboard} {Plural(avatarsInSideboard, "Avatar")}");
            if (sideFactions > 0) sideParts.Add($"{sideFactions} {Plural(sideFactions, "faction")}");
            if (sideAllies > 0) sideParts.Add($"{sideAllies} {Plural(sideAllies, "ally", "allies")}");
            if (sideItems > 0) sideParts.Add($"{sideItems} {Plural(sideItems, "item")}");
            if (sideRingCards > 0) sideParts.Add($"{sideRingCards} {Plural(sideRingCards, "ring card")}");
            if (sidePalantirCards > 0) sideParts.Add($"{sidePalantirCards} {Plural(sidePalantirCards, "palantír card")}");
            if (sideQuests > 0) sideParts.Add($"{sideQuests} {Plural(sideQuests, "quest")}");
            if (sideAllEvents > 0) sideParts.Add($"{sideAllEvents} {Plural(sideAllEvents, "event")}");
            if (sideStandardCreatures > 0) sideParts.Add($"{sideStandardCreatures} {Plural(sideStandardCreatures, "creature")}");
            if (sideDualCreatures > 0) sideParts.Add($"{sideDualCreatures} {Plural(sideDualCreatures, "dual")}");
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
            string totalMpUnit = (totalBase == 1 && totalCond == 1) ? "MP" : "MPs";
            string poolMpText = poolCond != poolBase ? $"{poolBase}({poolCond})" : $"{poolBase}";
            string resMpText = $"{resBase}({resCond})";
            string sideMpText = sideCond != sideBase ? $"{sideBase}({sideCond})" : $"{sideBase}";
            string siteMpText = siteCond != siteBase ? $"{siteBase}({siteCond})" : $"{siteBase}";

            var sectionRows = new List<(string Label, string Value)>
            {
                ("Pool:", $"{_deck.Pool.Count} {Plural(_deck.Pool.Count, "card")} ({poolChars} {Plural(poolChars, "character")}, {poolMind} Mind{poolItemNote})  [Limits: <= 20 Mind, <= 2 Minor Items]"),
                ("Resources:", $"{_deck.Resources.Count} {Plural(_deck.Resources.Count, "card")} ({nonAvatarCharsInRes} {Plural(nonAvatarCharsInRes, "character")}{avatarResNote}{resFactionNote}{resAllyNote}{resItemNote}{resRingNote}{resPalantirNote}{resQuestNote}{resEventNote})  [Limits: <= 10 non-Avatar, <= 3 Avatars]"),
                ("Hazards:", $"{_deck.Hazards.Count} {Plural(_deck.Hazards.Count, "card")}{hazDetailNote}  [Creature equivalent >= 12]"),
                ("Sideboard:", $"{_deck.Sideboard.Count} {Plural(_deck.Sideboard.Count, "card")}{sideDetailNote}"),
                ("Sites:", $"{_deck.Sites.Count} {Plural(_deck.Sites.Count, "card")} ({havensInSites} {Plural(havensInSites, "Haven")}, {regionsInSites} {Plural(regionsInSites, "Region")}, {advSitesInSites} {Plural(advSitesInSites, "Site")})")
            };

            var mpRows = new List<(string Label, string Value)>
            {
                ("MP Total:", $"{totalMpText} {totalMpUnit} (Pool: {poolMpText}, Resources: {resMpText}, Sideboard: {sideMpText}{(siteBase != 0 || siteCond != 0 ? $", Sites: {siteMpText}" : "")})")
            };

            SetRichBulletedStatus(_rtbDeckSummary, null, [sectionRows, mpRows]);
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

            Color idleColor = Color.FromArgb(74, 85, 104);
            SetRichPlainStatus(_rtbTheaterStatus, TheaterIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbStarterStatus, StarterIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbRecruitStatus, string.Empty, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbFactionStatus, FactionIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbAllyStatus, AllyIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbItemStatus, ItemIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbRingsStatus, RingsIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbPalantiriStatus, PalantirIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbQuestsStatus, QuestIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbResourceEventsStatus, ResourceEventsIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbCreaturesStatus, CreaturesIdleStatusText, idleColor, FontStyle.Italic);
            SetRichPlainStatus(_rtbHazardEventsStatus, HazardEventsIdleStatusText, idleColor, FontStyle.Italic);

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

        private void ResetSettingsToDefaults()
        {
            var res = MessageBox.Show(
                this,
                "Reset all workbench tabs, filters, and counts back to factory defaults?",
                "Reset Workbench Defaults",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res != DialogResult.Yes) return;

            ApplyLoadedSettings(CreateDefaultSettings());
            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = "Workbench settings reset to factory defaults.";
        }

        private static DeckAssemblySettings CreateDefaultSettings()
        {
            var settings = new DeckAssemblySettings
            {
                SelectedRegionTypes = ["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"],
                SelectedSiteTypes = ["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold"]
            };

            for (int i = 0; i < 5; i++)
            {
                settings.StarterRows.Add(new CharacterRowSettings
                {
                    Skill = "Any",
                    Species = "Any",
                    Mind = ">=1",
                    DI = "<=4"
                });
            }

            for (int i = 0; i < 10; i++)
            {
                settings.RecruitRows.Add(new CharacterRowSettings());
            }

            for (int i = 0; i < 3; i++)
            {
                settings.ReserveRows.Add(new CharacterRowSettings());
            }

            return settings;
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
                RequireHavenInRegionGroup = _chkRequireHavenInRegionGroup.Checked,
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
                ResourceFactionSupportCount = (int)_numResourceFactionSupport.Value,
                SideboardFactionSupportCount = (int)_numSideboardFactionSupport.Value,

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
                SelectedCreatureSiteTypes = [.. _clbCreatureSiteTypes.CheckedItems.Cast<string>()],

                HazardDoorsOfNightCount = (int)_numHazDoorsOfNight.Value,
                SideboardDoorsOfNightCount = (int)_numSideDoorsOfNight.Value,
                HazardTwilightCount = (int)_numHazTwilight.Value,
                SideboardHazardTwilightCount = (int)_numSideHazTwilight.Value,
                SideboardHazardEventCount = (int)_numSideboardHazardEvents.Value,
                ExcludedHazardEventRoles = [.. Enumerable.Range(0, _clbHazardEventRoles.Items.Count)
                    .Where(i => !_clbHazardEventRoles.GetItemChecked(i))
                    .Select(i => _clbHazardEventRoles.Items[i]?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))],
                SelectedHazardEventRegionTypes = [.. _clbHazardEventRegionTypes.CheckedItems.Cast<string>()],
                SelectedHazardEventSiteTypes = [.. _clbHazardEventSiteTypes.CheckedItems.Cast<string>()]
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

            _numRegionCount.Value = Math.Clamp(config.RegionCount, (int)_numRegionCount.Minimum, (int)_numRegionCount.Maximum);

            _numMaxSiteCount.Value = Math.Clamp(config.MaxSiteCount, (int)_numMaxSiteCount.Minimum, (int)_numMaxSiteCount.Maximum);
            _chkRequireHavenInRegionGroup.Checked = config.RequireHavenInRegionGroup;

            SetCheckedItems(_clbRegionTypes, config.SelectedRegionTypes);
            SetCheckedItems(_clbSiteTypes, config.SelectedSiteTypes);
            PopulateSeedRegionDropdown(config.SeedRegion);
            UpdateSeedTooltip();

            foreach (var row in _starterRows) row.Clear();
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

            foreach (var row in _recruitRows) row.Clear();
            if (config.RecruitRows != null)
            {
                for (int i = 0; i < Math.Min(_recruitRows.Count, config.RecruitRows.Count); i++)
                {
                    ApplyRowSettings(_recruitRows[i], config.RecruitRows[i]);
                }
            }

            foreach (var row in _reserveRows) row.Clear();
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
            _numResourceFactionSupport.Value = Math.Clamp(config.ResourceFactionSupportCount, (int)_numResourceFactionSupport.Minimum, (int)_numResourceFactionSupport.Maximum);
            _numSideboardFactionSupport.Value = Math.Clamp(config.SideboardFactionSupportCount, (int)_numSideboardFactionSupport.Minimum, (int)_numSideboardFactionSupport.Maximum);

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

            _numHazDoorsOfNight.Value = Math.Clamp(config.HazardDoorsOfNightCount, (int)_numHazDoorsOfNight.Minimum, (int)_numHazDoorsOfNight.Maximum);
            _numSideDoorsOfNight.Value = Math.Clamp(config.SideboardDoorsOfNightCount, (int)_numSideDoorsOfNight.Minimum, (int)_numSideDoorsOfNight.Maximum);
            _numHazTwilight.Value = Math.Clamp(config.HazardTwilightCount, (int)_numHazTwilight.Minimum, (int)_numHazTwilight.Maximum);
            _numSideHazTwilight.Value = Math.Clamp(config.SideboardHazardTwilightCount, (int)_numSideHazTwilight.Minimum, (int)_numSideHazTwilight.Maximum);
            _numSideboardHazardEvents.Value = Math.Clamp(config.SideboardHazardEventCount, (int)_numSideboardHazardEvents.Minimum, (int)_numSideboardHazardEvents.Maximum);

            PopulateHazardEventRolesChecklist();
            var excludedHazEventRoles = new HashSet<string>(config.ExcludedHazardEventRoles ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _clbHazardEventRoles.Items.Count; i++)
            {
                string roleName = _clbHazardEventRoles.Items[i]?.ToString() ?? string.Empty;
                _clbHazardEventRoles.SetItemChecked(i, !excludedHazEventRoles.Contains(roleName));
            }

            if (config.SelectedHazardEventRegionTypes != null)
            {
                SetCheckedItems(_clbHazardEventRegionTypes, config.SelectedHazardEventRegionTypes);
            }
            else
            {
                SetAllChecked(_clbHazardEventRegionTypes, true);
            }

            if (config.SelectedHazardEventSiteTypes != null)
            {
                SetCheckedItems(_clbHazardEventSiteTypes, config.SelectedHazardEventSiteTypes);
            }
            else
            {
                SetAllChecked(_clbHazardEventSiteTypes, true);
            }

            UpdateHazardKeyingEnabledState();
            UpdateCreatureEquivalentsReadout();

            UpdateDeckSummary();
            ValidateWorkbenchReadiness();
        }

        #endregion

        #region VALIDATION_AND_PIPELINE

        private void ValidateWorkbenchReadiness()
        {
            if (_btnRunPipeline == null || _rtbStarterStatus == null || _rtbRecruitStatus == null || _rtbTheaterStatus == null ||
                _rtbFactionStatus == null || _rtbAllyStatus == null || _rtbItemStatus == null ||
                _rtbRingsStatus == null || _rtbPalantiriStatus == null || _rtbQuestsStatus == null ||
                _rtbResourceEventsStatus == null || _rtbCreaturesStatus == null || _rtbHazardEventsStatus == null)
            {
                return;
            }

            bool hasTheaterError = false;
            bool hasStarterError = false;
            bool hasRecruitError = false;
            bool hasFactionError = false;
            bool hasAllyError = false;
            bool hasItemError = false;
            bool hasRingsError = false;
            bool hasPalantirError = false;
            bool hasQuestsError = false;
            bool hasResourceEventsError = false;
            bool hasCreaturesError = false;
            bool hasHazardEventsError = false;

            int wizInRes = GetSelectedWizard1Count() + (IsWizard2InSideboard() ? 0 : GetSelectedWizard2Count());
            int wizInSide = IsWizard2InSideboard() ? GetSelectedWizard2Count() : 0;

            int companionCount = _recruitRows.Count(r => r.IsActive);
            int reserveCount = _reserveRows.Count(r => r.IsActive);
            int totalSideboardChars = reserveCount + wizInSide;

            int resFactions = (int)_numResourceFactions.Value;
            int sideFactions = (int)_numSideboardFactions.Value;
            int resFactionSupport = (int)_numResourceFactionSupport.Value;
            int sideFactionSupport = (int)_numSideboardFactionSupport.Value;
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

            int hazDonTotal = (int)_numHazDoorsOfNight.Value + (int)_numSideDoorsOfNight.Value;
            int hazTwiTotal = (int)_numHazTwilight.Value + (int)_numSideHazTwilight.Value;
            int hazEventsEnv = (int)_numHazDoorsOfNight.Value + (int)_numHazTwilight.Value;
            int hazEventsSide = (int)_numSideDoorsOfNight.Value + (int)_numSideHazTwilight.Value + (int)_numSideboardHazardEvents.Value;
            int totalConfiguredHaz = hazCreaturesTotal + hazEventsEnv;
            int hazAutoTarget = Math.Max(0, (int)_numPlayDeckSize.Value - totalConfiguredHaz);
            int sideHazAuto = (int)_numSideboardHazardEvents.Value;

            int playDeckLimit = (int)_numPlayDeckSize.Value;
            int sideboardLimit = (int)_numSideboardSize.Value;

            Color idleColor = Color.FromArgb(74, 85, 104);

            // --- Tab 2 Validation ---
            if (_clbRegionTypes.CheckedItems.Count == 0 || _clbSiteTypes.CheckedItems.Count == 0)
            {
                hasTheaterError = true;
                SetRichPlainStatus(
                    _rtbTheaterStatus,
                    "⚠ At least one Region Terrain and one Site Hold must be checked under Terrain & Hold Classifications.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (IsSeedRegionExcludedByTerrains(out string badSeed, out string seedTerrains))
            {
                hasTheaterError = true;
                string terrainNote = !string.IsNullOrEmpty(seedTerrains)
                    ? $" ({seedTerrains})"
                    : " (no Region Type tag)";
                string fixHint = !string.IsNullOrEmpty(seedTerrains)
                    ? $"Check {seedTerrains} in Region Terrains or select another Seed Region."
                    : "Select another Seed Region.";
                SetRichPlainStatus(
                    _rtbTheaterStatus,
                    $"⚠ Seed Region '{badSeed}'{terrainNote} is excluded by Region Terrains.\n{fixHint}",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (_chkRequireHavenInRegionGroup.Checked && IsHavenReachabilityError(out string havenErrorMsg))
            {
                hasTheaterError = true;
                SetRichPlainStatus(
                    _rtbTheaterStatus,
                    havenErrorMsg,
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (_rtbTheaterStatus.ForeColor == Color.Firebrick &&
                     (_rtbTheaterStatus.Text.StartsWith("⚠ At least one Region Terrain", StringComparison.Ordinal) ||
                      _rtbTheaterStatus.Text.StartsWith("⚠ Seed Region '", StringComparison.Ordinal) ||
                      _rtbTheaterStatus.Text.StartsWith("⚠ 'Region group must contain a haven'", StringComparison.Ordinal)))
            {
                SetRichPlainStatus(_rtbTheaterStatus, TheaterIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 3 (Starting Company) Validation ---
            var activeSetsForStarter = _getActiveSets();
            var activeStarterRows = _starterRows
                .Select((Row, Index) => (Row, RowNumber: Index + 1))
                .Where(x => x.Row.IsActive)
                .ToList();

            if (activeStarterRows.Count > 0)
            {
                var zeroMatchRows = new List<int>();
                int minMindSum = 0;

                foreach (var (row, rowNum) in activeStarterRows)
                {
                    var candidates = GetCandidatesForCharacterRow(row, _catalog.Cards, activeSetsForStarter, null, null);
                    if (candidates.Count == 0)
                    {
                        zeroMatchRows.Add(rowNum);
                    }
                    else
                    {
                        minMindSum += candidates.Min(c => ParseCardMind(c.Mind));
                    }
                }

                if (zeroMatchRows.Count > 0)
                {
                    hasStarterError = true;
                    string rowLabel = zeroMatchRows.Count == 1
                        ? $"Row {zeroMatchRows[0]} matches"
                        : $"Row(s) {string.Join(", ", zeroMatchRows)} match";
                    SetRichPlainStatus(
                        _rtbStarterStatus,
                        $"⚠ {rowLabel} 0 non-Avatar characters in your active sets (note: Mind must be >= 1; leave a box blank for Any).\n" +
                        "Adjust the row filter(s) or clear the row.",
                        Color.Firebrick,
                        FontStyle.Bold);
                }
                else if (minMindSum > 20)
                {
                    hasStarterError = true;
                    SetRichPlainStatus(
                        _rtbStarterStatus,
                        $"⚠ Minimum possible Mind sum ({minMindSum}) exceeds the 20-Mind starting pool limit.\n" +
                        "Lower your Mind thresholds or clear one of the rows.",
                        Color.Firebrick,
                        FontStyle.Bold);
                }
                else if (_rtbStarterStatus.ForeColor == Color.Firebrick &&
                         (_rtbStarterStatus.Text.StartsWith("⚠ Row", StringComparison.Ordinal) ||
                          _rtbStarterStatus.Text.StartsWith("⚠ Minimum possible Mind", StringComparison.Ordinal)))
                {
                    SetRichPlainStatus(_rtbStarterStatus, StarterIdleStatusText, idleColor, FontStyle.Italic);
                }
            }
            else if (_rtbStarterStatus.ForeColor == Color.Firebrick &&
                     (_rtbStarterStatus.Text.StartsWith("⚠ Row", StringComparison.Ordinal) ||
                      _rtbStarterStatus.Text.StartsWith("⚠ Minimum possible Mind", StringComparison.Ordinal)))
            {
                SetRichPlainStatus(_rtbStarterStatus, StarterIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 4 Validation ---
            var zeroMatchRecruitRows = _recruitRows
                .Select((Row, Index) => (Row, RowNumber: Index + 1))
                .Where(x => x.Row.IsActive && GetCandidatesForCharacterRow(x.Row, _catalog.Cards, activeSetsForStarter, null, null).Count == 0)
                .Select(x => x.RowNumber)
                .ToList();

            var zeroMatchReserveRows = _reserveRows
                .Select((Row, Index) => (Row, RowNumber: Index + 1))
                .Where(x => x.Row.IsActive && GetCandidatesForCharacterRow(x.Row, _catalog.Cards, activeSetsForStarter, null, null).Count == 0)
                .Select(x => x.RowNumber)
                .ToList();

            if (companionCount > 10)
            {
                hasRecruitError = true;
                SetRichPlainStatus(
                    _rtbRecruitStatus,
                    $"⚠ Selection exceeds the 10-character limit for Play Deck Characters (Resources): {companionCount}/10 configured.\n" +
                    $"Clear {companionCount - 10} row(s) to proceed.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (totalSideboardChars > sideboardLimit)
            {
                hasRecruitError = true;
                SetRichPlainStatus(
                    _rtbRecruitStatus,
                    $"⚠ Sideboard characters ({totalSideboardChars}: {reserveCount} Sideboard row(s){(wizInSide > 0 ? $" + {wizInSide} Wizard" : "")}) exceed the Sideboard Size limit ({sideboardLimit}) set in General Settings.\n" +
                    "Increase Sideboard Size, change Secondary Wizard destination, or clear a Sideboard row.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (zeroMatchRecruitRows.Count > 0 || zeroMatchReserveRows.Count > 0)
            {
                hasRecruitError = true;
                var badParts = new List<string>();
                if (zeroMatchRecruitRows.Count > 0)
                {
                    badParts.Add(zeroMatchRecruitRows.Count == 1
                        ? $"Play Deck Row {zeroMatchRecruitRows[0]}"
                        : $"Play Deck Row(s) {string.Join(", ", zeroMatchRecruitRows)}");
                }
                if (zeroMatchReserveRows.Count > 0)
                {
                    badParts.Add(zeroMatchReserveRows.Count == 1
                        ? $"Sideboard Row {zeroMatchReserveRows[0]}"
                        : $"Sideboard Row(s) {string.Join(", ", zeroMatchReserveRows)}");
                }

                string verb = (zeroMatchRecruitRows.Count + zeroMatchReserveRows.Count) == 1 ? "matches" : "match";
                SetRichPlainStatus(
                    _rtbRecruitStatus,
                    $"⚠ {string.Join(" and ", badParts)} {verb} 0 non-Avatar characters in your active sets (note: Mind must be >= 1; leave a box blank for Any).\n" +
                    "Adjust the row filter(s) or clear the row.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (_rtbRecruitStatus.ForeColor == Color.Firebrick ||
                     string.IsNullOrWhiteSpace(_rtbRecruitStatus.Text) ||
                     _rtbRecruitStatus.Text.StartsWith("Configured:", StringComparison.Ordinal))
            {
                string wizResText = wizInRes > 0 ? $", {wizInRes} Wizard(s)" : "";
                string wizSideText = wizInSide > 0 ? $" + {wizInSide} Wizard" : "";
                SetRichPlainStatus(
                    _rtbRecruitStatus,
                    $"Configured: {companionCount}/10 Resources{wizResText}  |  {reserveCount}/3 Sideboard{wizSideText}.\n" +
                    "Recruits prioritize: 1) local sites (including local havens), 2) any eligible character.",
                    idleColor,
                    FontStyle.Italic);
            }

            // --- Shared Play Deck & Sideboard Budget Validation (Tabs 5-13) ---
            int totalConfiguredRes = companionCount + wizInRes + resFactions + resFactionSupport + resAllies + resItems + resRingCards + resPalCards + resQuests + resEnvEvents;
            int totalConfiguredSide = totalSideboardChars + sideFactions + sideFactionSupport + sideAllies + sideItems + sideRingCards + sidePalCards + sideQuests + sideEnvEvents + sideAutoEvents + sideCreatures + hazEventsSide;
            string resOverMsg =
                $"⚠ Configured Resources cards ({totalConfiguredRes}: {companionCount + wizInRes} characters + {resFactions} factions + {resFactionSupport} faction support + {resAllies} allies + {resItems} items + {resRingCards} ring cards + {resPalCards} palantír cards + {resQuests} quests + {resEnvEvents} env) exceed Play Deck Size ({playDeckLimit}).\n" +
                "Reduce Resources card counts or increase Play Deck Size on General Settings.";

            string sideOverMsg =
                $"⚠ Configured Sideboard cards ({totalConfiguredSide}: {totalSideboardChars} characters + {sideFactions} factions + {sideFactionSupport} faction support + {sideAllies} allies + {sideItems} items + {sideRingCards} ring cards + {sidePalCards} palantír cards + {sideQuests} quests + {sideEnvEvents + sideAutoEvents + hazEventsSide} events + {sideCreatures} creature/Nazgûl) exceed Sideboard Size ({sideboardLimit}).\n" +
                "Reduce Sideboard card counts or increase Sideboard Size on General Settings.";

            // --- Tab 5 (Factions) ---
            if (totalConfiguredRes > playDeckLimit && (resFactions + resFactionSupport) > 0)
            {
                hasFactionError = true;
                SetRichPlainStatus(_rtbFactionStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && (sideFactions + sideFactionSupport) > 0)
            {
                hasFactionError = true;
                SetRichPlainStatus(_rtbFactionStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbFactionStatus.ForeColor == Color.Firebrick &&
                      (_rtbFactionStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbFactionStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase) && !IsFactionSupportEvent(c)) &&
                      _deck.Sideboard.All(c => !string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase) && !IsFactionSupportEvent(c)) &&
                      !_rtbFactionStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbFactionStatus.Text.StartsWith("No factions configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbFactionStatus, FactionIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 6 (Allies) ---
            if (totalConfiguredRes > playDeckLimit && resAllies > 0)
            {
                hasAllyError = true;
                SetRichPlainStatus(_rtbAllyStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sideAllies > 0)
            {
                hasAllyError = true;
                SetRichPlainStatus(_rtbAllyStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbAllyStatus.ForeColor == Color.Firebrick &&
                      (_rtbAllyStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbAllyStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase)) &&
                      _deck.Sideboard.All(c => !string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase)) &&
                      !_rtbAllyStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbAllyStatus.Text.StartsWith("No allies configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbAllyStatus, AllyIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 7 (Items) ---
            int totalRequestedItems = (int)_numPoolMinorItems.Value + resItems + sideItems;
            if (totalRequestedItems > 0 && _clbItemRoles.Items.Count > 0 && _clbItemRoles.CheckedItems.Count == 0)
            {
                hasItemError = true;
                SetRichPlainStatus(_rtbItemStatus, "⚠ At least one Role must be checked under Role when drafting items.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredRes > playDeckLimit && resItems > 0)
            {
                hasItemError = true;
                SetRichPlainStatus(_rtbItemStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sideItems > 0)
            {
                hasItemError = true;
                SetRichPlainStatus(_rtbItemStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbItemStatus.ForeColor == Color.Firebrick &&
                      (_rtbItemStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbItemStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbItemStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Pool.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      _deck.Resources.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsItemCard(c) || IsRingPackageCard(c) || IsPalantirPackageCard(c) || IsQuestPackageCard(c)) &&
                      !_rtbItemStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbItemStatus.Text.StartsWith("No items configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbItemStatus, ItemIdleStatusText, idleColor, FontStyle.Italic);
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
                SetRichPlainStatus(_rtbRingsStatus, "⚠ The One Ring is Unique: combined Resources + Sideboard count cannot exceed 1.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalRequestedSpecialRings > 0 && _clbRingRoles.Items.Count > 0 && _clbRingRoles.CheckedItems.Count == 0)
            {
                hasRingsError = true;
                SetRichPlainStatus(_rtbRingsStatus, "⚠ At least one Role must be checked under Special Ring Role when drafting Special Rings.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredRes > playDeckLimit && resRingCards > 0)
            {
                hasRingsError = true;
                SetRichPlainStatus(_rtbRingsStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sideRingCards > 0)
            {
                hasRingsError = true;
                SetRichPlainStatus(_rtbRingsStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbRingsStatus.ForeColor == Color.Firebrick &&
                      (_rtbRingsStatus.Text.StartsWith("⚠ The One Ring", StringComparison.Ordinal) ||
                       _rtbRingsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbRingsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbRingsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsRingPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsRingPackageCard(c)) &&
                      !_rtbRingsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbRingsStatus.Text.StartsWith("No ring cards configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbRingsStatus, RingsIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 9 (Palantiri) ---
            int totalRequestedPalantiri = (int)_numResPalantiri.Value + (int)_numSidePalantiri.Value;
            if (totalRequestedPalantiri > 0 && _clbPalantirRoles.Items.Count > 0 && _clbPalantirRoles.CheckedItems.Count == 0)
            {
                hasPalantirError = true;
                SetRichPlainStatus(_rtbPalantiriStatus, "⚠ At least one Role must be checked under Role when drafting Palantíri.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredRes > playDeckLimit && resPalCards > 0)
            {
                hasPalantirError = true;
                SetRichPlainStatus(_rtbPalantiriStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sidePalCards > 0)
            {
                hasPalantirError = true;
                SetRichPlainStatus(_rtbPalantiriStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbPalantiriStatus.ForeColor == Color.Firebrick &&
                      (_rtbPalantiriStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbPalantiriStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbPalantiriStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsPalantirPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsPalantirPackageCard(c)) &&
                      !_rtbPalantiriStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbPalantiriStatus.Text.StartsWith("No Palantír cards configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbPalantiriStatus, PalantirIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 10 (Quests) ---
            int totalRequestedQuests = resQuests + sideQuests;
            if (totalRequestedQuests > 0 && _clbQuestRoles.Items.Count > 0 && _clbQuestRoles.CheckedItems.Count == 0)
            {
                hasQuestsError = true;
                SetRichPlainStatus(_rtbQuestsStatus, "⚠ At least one Role must be checked under Role when drafting quests.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredRes > playDeckLimit && resQuests > 0)
            {
                hasQuestsError = true;
                SetRichPlainStatus(_rtbQuestsStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sideQuests > 0)
            {
                hasQuestsError = true;
                SetRichPlainStatus(_rtbQuestsStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbQuestsStatus.ForeColor == Color.Firebrick &&
                      (_rtbQuestsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbQuestsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbQuestsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsQuestPackageCard(c)) &&
                      _deck.Sideboard.All(c => !IsQuestPackageCard(c)) &&
                      !_rtbQuestsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbQuestsStatus.Text.StartsWith("No quests configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbQuestsStatus, QuestIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 11 (Resource Events) ---
            int gomTotal = (int)_numResGatesOfMorning.Value + (int)_numSideGatesOfMorning.Value;
            int twiTotal = (int)_numResTwilight.Value + (int)_numSideTwilight.Value;
            int combinedTwiTotal = twiTotal + hazTwiTotal;
            bool needsAutoEvents = totalConfiguredRes < playDeckLimit || sideAutoEvents > 0;

            if (gomTotal > 3)
            {
                hasResourceEventsError = true;
                SetRichPlainStatus(_rtbResourceEventsStatus, "⚠ Gates of Morning is non-unique: combined Resources + Sideboard count cannot exceed 3.", Color.Firebrick, FontStyle.Bold);
            }
            else if (twiTotal > 3)
            {
                hasResourceEventsError = true;
                SetRichPlainStatus(_rtbResourceEventsStatus, "⚠ Twilight is non-unique: combined Resources + Sideboard count cannot exceed 3.", Color.Firebrick, FontStyle.Bold);
            }
            else if (needsAutoEvents && _clbResourceEventRoles.Items.Count > 0 && _clbResourceEventRoles.CheckedItems.Count == 0)
            {
                hasResourceEventsError = true;
                SetRichPlainStatus(_rtbResourceEventsStatus, "⚠ At least one Role must be checked under Role to auto-fill Resource Events.", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredRes > playDeckLimit && resEnvEvents > 0)
            {
                hasResourceEventsError = true;
                SetRichPlainStatus(_rtbResourceEventsStatus, resOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && (sideEnvEvents + sideAutoEvents) > 0)
            {
                hasResourceEventsError = true;
                SetRichPlainStatus(_rtbResourceEventsStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbResourceEventsStatus.ForeColor == Color.Firebrick &&
                      (_rtbResourceEventsStatus.Text.StartsWith("⚠ Gates of Morning", StringComparison.Ordinal) ||
                       _rtbResourceEventsStatus.Text.StartsWith("⚠ Twilight", StringComparison.Ordinal) ||
                       _rtbResourceEventsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbResourceEventsStatus.Text.StartsWith("⚠ Configured Resources", StringComparison.Ordinal) ||
                       _rtbResourceEventsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Resources.All(c => !IsGeneralResourceEvent(c)) &&
                      _deck.Sideboard.All(c => !IsGeneralResourceEvent(c)) &&
                      !_rtbResourceEventsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbResourceEventsStatus, ResourceEventsIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 12 (Creatures) ---
            int totalNonNazgulRequested = hazStdCr + sideStdCr + hazDualCr + sideDualCr;
            bool tailorToDeck = _chkTailorHazardsToCurrentDeck.Checked;

            if (hazNazgul + sideNazgul > 9)
            {
                hasCreaturesError = true;
                SetRichPlainStatus(
                    _rtbCreaturesStatus,
                    "⚠ Nazgûl are Unique: combined Hazards + Sideboard count cannot exceed 9.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (totalNonNazgulRequested > 0 && _clbCreatureSpecies.Items.Count > 0 && _clbCreatureSpecies.CheckedItems.Count == 0)
            {
                hasCreaturesError = true;
                SetRichPlainStatus(
                    _rtbCreaturesStatus,
                    "⚠ At least one Creature Race must be checked when drafting Standard or Dual-Mode creatures.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (!tailorToDeck && totalNonNazgulRequested > 0 &&
                     _clbCreatureRegionTypes.CheckedItems.Count == 0 &&
                     _clbCreatureSiteTypes.CheckedItems.Count == 0)
            {
                hasCreaturesError = true;
                SetRichPlainStatus(
                    _rtbCreaturesStatus,
                    "⚠ At least one Region Terrain or Site Hold must be checked under Keying Filters (or enable Tailor Hazards to current deck).",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (hazCreaturesTotal > playDeckLimit)
            {
                hasCreaturesError = true;
                SetRichPlainStatus(
                    _rtbCreaturesStatus,
                    $"⚠ Configured Hazards cards ({hazCreaturesTotal}: {hazStdCr} Standard + {hazDualCr} Dual + {hazNazgul} Nazgûl + {hazNazSup} Support) exceed Play Deck Size ({playDeckLimit}).\n" +
                    "Reduce creature/Nazgûl counts or increase Play Deck Size on General Settings.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && sideCreatures > 0)
            {
                hasCreaturesError = true;
                SetRichPlainStatus(_rtbCreaturesStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbCreaturesStatus.ForeColor == Color.Firebrick &&
                      (_rtbCreaturesStatus.Text.StartsWith("⚠ Nazgûl are Unique", StringComparison.Ordinal) ||
                       _rtbCreaturesStatus.Text.StartsWith("⚠ At least one Creature Race", StringComparison.Ordinal) ||
                       _rtbCreaturesStatus.Text.StartsWith("⚠ At least one Region Terrain", StringComparison.Ordinal) ||
                       _rtbCreaturesStatus.Text.StartsWith("⚠ Configured Hazards", StringComparison.Ordinal) ||
                       _rtbCreaturesStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Hazards.All(c => !IsCreaturesTabCard(c)) &&
                      _deck.Sideboard.All(c => !IsCreaturesTabCard(c)) &&
                      !_rtbCreaturesStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase) &&
                      !_rtbCreaturesStatus.Text.StartsWith("No creatures or Nazgûl configured", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbCreaturesStatus, CreaturesIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Tab 13 (Hazard Events) ---
            if (hazDonTotal > 3)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(_rtbHazardEventsStatus, "⚠ Doors of Night is non-unique: combined Hazards + Sideboard count cannot exceed 3.", Color.Firebrick, FontStyle.Bold);
            }
            else if (combinedTwiTotal > 3)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(_rtbHazardEventsStatus, $"⚠ Twilight is non-unique: combined Resources + Hazards + Sideboard count ({combinedTwiTotal}) cannot exceed 3.", Color.Firebrick, FontStyle.Bold);
            }
            else if ((hazAutoTarget > 0 || sideHazAuto > 0) && _clbHazardEventRoles.Items.Count > 0 && _clbHazardEventRoles.CheckedItems.Count == 0)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(_rtbHazardEventsStatus, "⚠ At least one Role must be checked under Role to auto-fill Hazard Events.", Color.Firebrick, FontStyle.Bold);
            }
            else if (!tailorToDeck && (hazAutoTarget > 0 || sideHazAuto > 0) &&
                     _clbHazardEventRegionTypes.CheckedItems.Count == 0 &&
                     _clbHazardEventSiteTypes.CheckedItems.Count == 0)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(_rtbHazardEventsStatus, "⚠ At least one Region Terrain or Site Hold must be checked under Keying Filters (or enable Tailor Hazards to current deck).", Color.Firebrick, FontStyle.Bold);
            }
            else if (totalConfiguredHaz > playDeckLimit && hazEventsEnv > 0)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(
                    _rtbHazardEventsStatus,
                    $"⚠ Configured Hazards cards ({totalConfiguredHaz}: {hazCreaturesTotal} creatures/Nazgûl + {hazEventsEnv} env) exceed Play Deck Size ({playDeckLimit}).\n" +
                    "Reduce Hazard counts or increase Play Deck Size on General Settings.",
                    Color.Firebrick,
                    FontStyle.Bold);
            }
            else if (totalConfiguredSide > sideboardLimit && hazEventsSide > 0)
            {
                hasHazardEventsError = true;
                SetRichPlainStatus(_rtbHazardEventsStatus, sideOverMsg, Color.Firebrick, FontStyle.Bold);
            }
            else if ((_rtbHazardEventsStatus.ForeColor == Color.Firebrick &&
                      (_rtbHazardEventsStatus.Text.StartsWith("⚠ Doors of Night", StringComparison.Ordinal) ||
                       _rtbHazardEventsStatus.Text.StartsWith("⚠ Twilight", StringComparison.Ordinal) ||
                       _rtbHazardEventsStatus.Text.StartsWith("⚠ At least one Role", StringComparison.Ordinal) ||
                       _rtbHazardEventsStatus.Text.StartsWith("⚠ At least one Region Terrain", StringComparison.Ordinal) ||
                       _rtbHazardEventsStatus.Text.StartsWith("⚠ Configured Hazards", StringComparison.Ordinal) ||
                       _rtbHazardEventsStatus.Text.StartsWith("⚠ Configured Sideboard", StringComparison.Ordinal))) ||
                     (_deck.Hazards.All(c => !IsGeneralHazardEvent(c)) &&
                      _deck.Sideboard.All(c => !IsGeneralHazardEvent(c)) &&
                      !_rtbHazardEventsStatus.Text.StartsWith("Drafted ", StringComparison.OrdinalIgnoreCase)))
            {
                SetRichPlainStatus(_rtbHazardEventsStatus, HazardEventsIdleStatusText, idleColor, FontStyle.Italic);
            }

            // --- Unified Pipeline Readiness ---
            bool anyError =
                hasTheaterError || hasStarterError || hasRecruitError || hasFactionError || hasAllyError ||
                hasItemError || hasRingsError || hasPalantirError || hasQuestsError ||
                hasResourceEventsError || hasCreaturesError || hasHazardEventsError;

            _btnRunPipeline.Enabled = !anyError;

            if (anyError)
            {
                var blockedTabs = new List<string>();
                if (hasTheaterError) blockedTabs.Add("Regions & Sites");
                if (hasStarterError) blockedTabs.Add("Starting Company");
                if (hasRecruitError) blockedTabs.Add("Wizards & Recruits");
                if (hasFactionError) blockedTabs.Add("Factions");
                if (hasAllyError) blockedTabs.Add("Allies");
                if (hasItemError) blockedTabs.Add("Items");
                if (hasRingsError) blockedTabs.Add("Rings");
                if (hasPalantirError) blockedTabs.Add("Palantíri");
                if (hasQuestsError) blockedTabs.Add("Quests");
                if (hasResourceEventsError) blockedTabs.Add("Resource Events");
                if (hasCreaturesError) blockedTabs.Add("Creatures");
                if (hasHazardEventsError) blockedTabs.Add("Hazard Events");

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

            UpdateSidebarVisuals();
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

            void ResetAttemptState()
            {
                _deck.Pool.Clear();
                _deck.Resources.Clear();
                _deck.Hazards.Clear();
                _deck.Sideboard.Clear();
                _deck.Sites.Clear();

                foreach (var (_, _, info) in stepsToRun)
                {
                    info.StatusControl.Text = string.Empty;
                }
            }

            const int maxPipelineAttempts = 25;
            const string baseTitle = "Modular Deck Assembly Workbench";

            bool prevRecallEnabled = _btnRecallSnapshot.Enabled;
            UseWaitCursor = true;
            Cursor.Current = Cursors.WaitCursor;
            _btnRunPipeline.Enabled = false;
            _btnRunPipeline.Text = "Running...";
            _btnClearDeck.Enabled = false;
            _btnSaveSnapshot.Enabled = false;
            _btnRecallSnapshot.Enabled = false;
            _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblPipelineStatus.Text = $"Running pipeline (attempt 1/{maxPipelineAttempts})...";
            Text = $"{baseTitle} - Running Pipeline (attempt 1/{maxPipelineAttempts})...";
            _btnRunPipeline.Update();
            _lblPipelineStatus.Update();
            Update();

            try
            {
                int fewestWarnings = int.MaxValue;
                int bestAttempt = 0;
                List<Card> bestPool = null;
                List<Card> bestResources = null;
                List<Card> bestHazards = null;
                List<Card> bestSideboard = null;
                List<Card> bestSites = null;
                List<(Control Control, string Text, string Rtf, Color ForeColor, Font Font)> bestStatusControls = null;
                List<string> bestCompletedSteps = null;
                List<string> bestWarningTabs = null;

                for (int attempt = 1; attempt <= maxPipelineAttempts; attempt++)
                {
                    Text = $"{baseTitle} - Running Pipeline (attempt {attempt}/{maxPipelineAttempts})...";
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
                            .Where(step => step.Info.StatusControl.Text.Contains('⚠'))
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
                            UpdateSidebarVisuals();
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
                            bestStatusControls = [.. stepsToRun.Select(step => (
                                step.Info.StatusControl,
                                step.Info.StatusControl.Text,
                                step.Info.StatusControl is RichTextBox rtb ? rtb.Rtf : null,
                                step.Info.StatusControl.ForeColor,
                                step.Info.StatusControl.Font))];
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

                    foreach (var (ctrl, text, rtf, foreColor, font) in bestStatusControls)
                    {
                        ctrl.ForeColor = foreColor;
                        ctrl.Font = font;
                        if (ctrl is RichTextBox rtb && rtf != null)
                        {
                            rtb.Rtf = rtf;
                        }
                        else
                        {
                            ctrl.Text = text;
                        }
                    }

                    NotifyDeckChanged(null);
                    UpdateDeckSummary();

                    _lblPipelineStatus.ForeColor = Color.FromArgb(180, 83, 9);
                    _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    _lblPipelineStatus.Text =
                        $"Pipeline complete with warnings (best from attempt {bestAttempt}/{maxPipelineAttempts}): {string.Join(" -> ", bestCompletedSteps)}\n" +
                        $"⚠ See notice(s) on: {string.Join(", ", bestWarningTabs)}";
                    UpdateSidebarVisuals();
                    return;
                }

                ResetAttemptState();
                _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblPipelineStatus.Text = "Running diagnostic pass...";
                _lblPipelineStatus.Update();
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
                        UpdateSidebarVisuals();
                        return;
                    }

                    finalCompleted.Add($"{stepNumber}. {tabName} ✓");
                }

                NotifyDeckChanged(null);
                UpdateDeckSummary();

                _lblPipelineStatus.ForeColor = Color.FromArgb(26, 86, 54);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblPipelineStatus.Text = $"Pipeline complete: {string.Join(" -> ", finalCompleted)}";
                UpdateSidebarVisuals();
            }
            finally
            {
                Text = baseTitle;
                UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
                _btnRunPipeline.Text = "Run Pipeline (F5)";
                _btnClearDeck.Enabled = true;
                _btnSaveSnapshot.Enabled = true;
                _btnRecallSnapshot.Enabled = prevRecallEnabled || _savedDeckSnapshot != null;
                ValidateWorkbenchReadiness();
            }
        }

        #endregion
    }
}