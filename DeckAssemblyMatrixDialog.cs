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

        private readonly Deck _deck;
        private readonly CardCatalogService _catalog;
        private readonly CardFilterService _filterService;
        private readonly Func<ISet<string>> _getActiveSets; 
        private readonly Action<DeckSectionType?> _notifyDeckCallback;

        private void NotifyDeckChanged(DeckSectionType? section = null)
        {
            _notifyDeckCallback?.Invoke(section);
        }

        // Session memory snapshots
        private List<Card> _savedPool;
        private List<Card> _savedNonStarters;
        private List<Card> _savedSideboardCharacters;
        private List<Card> _savedSites;

        // Tab Controls
        private readonly TabControl _tabControl;

        // --- TAB 1: GENERAL CONTROLS ---
        private readonly TextBox _txtDeckTitle;
        private readonly NumericUpDown _numPlayDeckSize;
        private readonly NumericUpDown _numSideboardSize;
        private readonly Label _lblDeckSummary;

        // --- TAB 1: EXECUTION PIPELINE CONTROLS ---
        private readonly List<ComboBox> _pipelineStepCombos = [];
        private readonly List<string> _previousStepSelections = [];
        private sealed record RunnableTabInfo(Func<bool, bool> Runner, Label StatusLabel);
        private readonly Dictionary<string, RunnableTabInfo> _tabInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly Panel _pnlPipelineSteps;
        private readonly Label _lblPipelineStatus;
        private bool _isUpdatingPipelineCombos;

        // --- TAB 2: STARTING COMPANY CONTROLS ---
        private readonly List<CharacterRowControls> _starterRows = [];
        private readonly ComboBox _cmbStarterHomeRegionPolicy;
        private readonly Label _lblStarterStatus;

        // --- TAB 3: NON-STARTING CHARACTERS, AVATARS & SIDEBOARD CONTROLS ---
        private readonly ComboBox _cmbWizard1;
        private readonly ComboBox _cmbWizard1Copies;
        private readonly ComboBox _cmbWizard2;
        private readonly ComboBox _cmbWizard2Copies;
        private readonly ComboBox _cmbWizard2Dest;
        private bool _isUpdatingWizardControls;

        private readonly List<CharacterRowControls> _recruitRows = [];
        private readonly List<CharacterRowControls> _reserveRows = [];
        private readonly ComboBox _cmbHomeSitePolicy;
        private readonly Button _btnRunRecruits;
        private readonly Button _btnSaveRecruits;
        private readonly Label _lblRecruitStatus;

        // --- TAB 4: GEOGRAPHIC THEATER CONTROLS ---
        private readonly ComboBox _cmbMode;
        private readonly ComboBox _cmbHaven;
        private readonly NumericUpDown _numRegionCount;
        private readonly NumericUpDown _numSiteCount;
        private readonly CheckedListBox _clbRegionTypes;
        private readonly CheckedListBox _clbSiteTypes;
        private readonly Label _lblTheaterStatus;
        private readonly ComboBox _cmbSeedRegion;
        private bool _isUpdatingTheaterCombos;
        private string _savedSeedRegion;

        // Tab 4: Non-Starter Home Site Policy & Tooltips
        private readonly ComboBox _cmbTheaterHomeSitePolicy;
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
            _numPlayDeckSize.ValueChanged += (s, e) => UpdateDeckSummary();

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
                ValidateRecruitTabBudget();
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
                Text = "Save or load modular drafting profiles (.workbench.json) to reuse tab configurations and execution order.",
                Location = new Point(390, 127),
                Size = new Size(410, 36),
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
                Text = "Tab Execution Order && Full Pipeline Run",
                Location = new Point(16, 192),
                Size = new Size(810, 170)
            };

            _pnlPipelineSteps = new Panel
            {
                Location = new Point(12, 20),
                Size = new Size(460, 96),
                AutoScroll = true
            };

            var btnRunPipeline = new Button
            {
                Text = "Run Pipeline",
                Location = new Point(488, 22),
                Size = new Size(130, 30),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnRunPipeline.Click += (s, e) => RunPipelineDraft();

            var btnResetOrder = new Button
            {
                Text = "Reset Order",
                Location = new Point(628, 22),
                Size = new Size(110, 30)
            };
            btnResetOrder.Click += (s, e) => ResetPipelineOrder();

            var lblPipelineHint = new Label
            {
                Text = "Runs selected tabs in order from Step 1 downward. Stops if any tab encounters a hard lock. Choose (None) to omit a tab.",
                Location = new Point(488, 60),
                Size = new Size(305, 52),
                ForeColor = Color.DimGray
            };

            _lblPipelineStatus = new Label
            {
                Location = new Point(16, 122),
                Size = new Size(778, 40),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            grpPipeline.Controls.AddRange([
                _pnlPipelineSteps,
                btnRunPipeline,
                btnResetOrder,
                lblPipelineHint,
                _lblPipelineStatus
            ]);

            var grpSummary = new GroupBox
            {
                Text = "Live Deck Composition Status",
                Location = new Point(16, 370),
                Size = new Size(810, 236)
            };

            _lblDeckSummary = new Label
            {
                Location = new Point(20, 24),
                Size = new Size(770, 204),
                Font = new Font("Segoe UI", 10.0f),
                ForeColor = Color.FromArgb(26, 54, 93),
                UseMnemonic = false
            };
            grpSummary.Controls.Add(_lblDeckSummary);

            tabGeneral.Controls.AddRange([grpGeneral, grpPipeline, grpSummary]);

            // ==========================================
            // TAB 2: STARTING COMPANY (5 ROWS - OPTION A)
            // ==========================================
            var tabStarter = new TabPage("Starting Company (Pool)");
            BuildCharacterGrid(tabStarter, _starterRows, 5, 42, 28, null);

            var lblStarterHomePolicy = new Label
            {
                Text = "Home Region Policy:",
                Location = new Point(16, 196),
                Size = new Size(130, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            _cmbStarterHomeRegionPolicy = new ComboBox
            {
                Location = new Point(155, 193),
                Size = new Size(330, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbStarterHomeRegionPolicy.Items.AddRange([
                "Best Effort (Prioritize theater regions, else open)",
                "Strict (Require theater regions)",
                "Open / Anywhere (Ignore theater)"
            ]);
            _cmbStarterHomeRegionPolicy.SelectedIndex = 0;

            var btnRunStarter = new Button
            {
                Text = "Run",
                Location = new Point(16, 230),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnRunStarter.Click += (s, e) => RunStartingCompanyDraft();

            var btnClearStarter = new Button
            {
                Text = "Clear",
                Location = new Point(112, 230),
                Size = new Size(80, 28)
            };
            btnClearStarter.Click += (s, e) =>
            {
                _deck.Pool.Clear();
                NotifyDeckChanged(DeckSectionType.Pool);
                RefreshHavenDropdown();
                _lblStarterStatus.Text = "Starting Pool cleared.";
            };

            var btnSaveStarter = new Button
            {
                Text = "Save",
                Location = new Point(198, 230),
                Size = new Size(80, 28)
            };
            btnSaveStarter.Click += (s, e) =>
            {
                _savedPool = [.. _deck.Pool];
                _lblStarterStatus.Text = $"Session Snapshot Saved ({_savedPool.Count} cards).";
            };

            var btnRecallStarter = new Button
            {
                Text = "Recall",
                Location = new Point(284, 230),
                Size = new Size(80, 28)
            };
            btnRecallStarter.Click += (s, e) =>
            {
                if (_savedPool == null)
                {
                    MessageBox.Show(this, "No saved session snapshot for Starting Company.", "Recall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _deck.Pool.Clear();
                _deck.Pool.AddRange(_savedPool);
                _deck.SortSections();
                NotifyDeckChanged(DeckSectionType.Pool);
                RefreshHavenDropdown();
                _lblStarterStatus.Text = $"Restored from Snapshot ({_savedPool.Count} cards).";
            };

            _lblStarterStatus = new Label
            {
                Location = new Point(16, 268),
                Size = new Size(790, 50),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false
            };

            tabStarter.Controls.AddRange([
                lblStarterHomePolicy, _cmbStarterHomeRegionPolicy,
                btnRunStarter, btnClearStarter, btnSaveStarter, btnRecallStarter,
                _lblStarterStatus
            ]);

            // ==========================================
            // TAB 3: NON-STARTING CHARACTERS, AVATARS & SIDEBOARD
            // ==========================================
            var tabRecruits = new TabPage("Non-Starting Characters & Sideboard");

            var grpWizards = new GroupBox
            {
                Text = "Avatar / Wizard Selection (Primary: Resources | Secondary: Sideboard or Resources)",
                Location = new Point(16, 8),
                Size = new Size(810, 82)
            };

            // Row 1: Primary Wizard
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

            // Row 2: Secondary Wizard
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
                Text = "Play Deck Companions (Resources - Up to 10)",
                Location = new Point(16, 98),
                Size = new Size(400, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblRecruitsHdr);

            // 10 Companion Rows for Resources (starts Y: 138, headers at Y: 118)
            BuildCharacterGrid(tabRecruits, _recruitRows, 10, 138, 24, ValidateRecruitTabBudget);

            var lblReservesHdr = new Label
            {
                Text = "Sideboard Contingency Characters (Sideboard - Up to 3)",
                Location = new Point(16, 390),
                Size = new Size(450, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblReservesHdr);

            // 3 Reserve Rows for Sideboard (starts Y: 430, headers at Y: 410)
            BuildCharacterGrid(tabRecruits, _reserveRows, 3, 430, 24, ValidateRecruitTabBudget);

            var lblHomePolicy = new Label
            {
                Text = "Home Site Policy:",
                Location = new Point(16, 513),
                Size = new Size(110, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            _cmbHomeSitePolicy = new ComboBox
            {
                Location = new Point(130, 510),
                Size = new Size(450, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHomeSitePolicy.Items.AddRange([
                "Best Effort (Prefer sites in Site deck, else anywhere)",
                "Strict (Require home site in Site deck)",
                "Open / Anywhere (Ignore Site deck)"
            ]);
            _cmbHomeSitePolicy.SelectedIndex = 0;

            _btnRunRecruits = new Button
            {
                Text = "Run",
                Location = new Point(16, 542),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunRecruits.Click += (s, e) => RunNonStartingCharactersDraft();

            var btnClearRecruits = new Button
            {
                Text = "Clear",
                Location = new Point(112, 542),
                Size = new Size(80, 28)
            };
            btnClearRecruits.Click += (s, e) =>
            {
                _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                NotifyDeckChanged(DeckSectionType.Resources);
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text = "Non-starting characters cleared from Resources and Sideboard.";
            };

            _btnSaveRecruits = new Button
            {
                Text = "Save",
                Location = new Point(198, 542),
                Size = new Size(80, 28)
            };
            _btnSaveRecruits.Click += (s, e) =>
            {
                _savedNonStarters = [.. _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))];
                _savedSideboardCharacters = [.. _deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))];
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text = $"Session Snapshot Saved ({_savedNonStarters.Count} in Resources, {_savedSideboardCharacters.Count} in Sideboard).";
            };

            var btnRecallRecruits = new Button
            {
                Text = "Recall",
                Location = new Point(284, 542),
                Size = new Size(80, 28)
            };
            btnRecallRecruits.Click += (s, e) =>
            {
                if (_savedNonStarters == null && _savedSideboardCharacters == null)
                {
                    MessageBox.Show(this, "No saved session snapshot for Non-Starting Characters.", "Recall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                if (_savedNonStarters != null) _deck.Resources.AddRange(_savedNonStarters);

                _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                if (_savedSideboardCharacters != null) _deck.Sideboard.AddRange(_savedSideboardCharacters);

                _deck.SortSections();
                NotifyDeckChanged(DeckSectionType.Resources);
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text = $"Restored from Snapshot ({_savedNonStarters?.Count ?? 0} in Resources, {_savedSideboardCharacters?.Count ?? 0} in Sideboard).";
            };

            _lblRecruitStatus = new Label
            {
                Location = new Point(16, 574),
                Size = new Size(810, 46),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };

            tabRecruits.Controls.AddRange([
                lblHomePolicy, _cmbHomeSitePolicy,
                _btnRunRecruits, btnClearRecruits, _btnSaveRecruits, btnRecallRecruits,
                _lblRecruitStatus
            ]);

            // ==========================================
            // TAB 4: GEOGRAPHIC THEATER
            // ==========================================
            var tabTheater = new TabPage("Geographic Theater (Sites)");

            var grpTheaterOpts = new GroupBox
            {
                Text = "Movement Mode && Havens",
                Location = new Point(16, 16),
                Size = new Size(415, 235)
            };

            // Row 1: Movement Mode (Y = 24)
            var lblMode = new Label { Text = "Movement Mode:", Location = new Point(12, 27), Size = new Size(144, 20) };
            _cmbMode = new ComboBox
            {
                Location = new Point(160, 24),
                Size = new Size(242, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbMode.Items.AddRange(["Starter + Region (Roaming)", "Starter Only (Haven Hub)"]);
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += (s, e) =>
            {
                bool isStarterOnly = _cmbMode.SelectedIndex == 1;
                _numRegionCount.Enabled = !isStarterOnly;
                _cmbSeedRegion.Enabled = !isStarterOnly;

                // 1. Rebuild Haven dropdown (strips "Nearest Haven" in Starter Only mode)
                RefreshHavenDropdown();

                // 2. Manage Seed Region placeholder
                if (isStarterOnly)
                {
                    _numRegionCount.Value = 0;

                    string curSeed = _cmbSeedRegion.SelectedItem?.ToString();
                    if (!string.IsNullOrWhiteSpace(curSeed) && !curSeed.StartsWith("(N/A", StringComparison.OrdinalIgnoreCase))
                    {
                        _savedSeedRegion = curSeed;
                    }

                    _cmbSeedRegion.BeginUpdate();
                    _cmbSeedRegion.Items.Clear();
                    _cmbSeedRegion.Items.Add("(N/A - Haven Hub)");
                    _cmbSeedRegion.SelectedIndex = 0;
                    _cmbSeedRegion.EndUpdate();
                }
                else
                {
                    if (_numRegionCount.Value == 0)
                    {
                        _numRegionCount.Value = 4;
                    }

                    PopulateSeedRegionDropdown(_savedSeedRegion);
                }
            };

            // Row 2: Haven Anchor (Y = 58)
            var lblHaven = new Label { Text = "Haven Anchor:", Location = new Point(12, 61), Size = new Size(144, 20) };
            _cmbHaven = new ComboBox
            {
                Location = new Point(160, 58),
                Size = new Size(242, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Row 3: Seed Region (Y = 92)
            var lblSeed = new Label { Text = "Seed Region:", Location = new Point(12, 95), Size = new Size(144, 20) };
            _cmbSeedRegion = new ComboBox
            {
                Location = new Point(160, 92),
                Size = new Size(242, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Row 4: Region Count (Y = 126)
            var lblRegCount = new Label { Text = "Region Count:", Location = new Point(12, 129), Size = new Size(144, 20) };
            _numRegionCount = new NumericUpDown
            {
                Location = new Point(160, 126),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 12,
                Value = 4
            };

            // Row 5: Site Count (Y = 160)
            var lblSiteCount = new Label { Text = "Site Count:", Location = new Point(12, 163), Size = new Size(144, 20) };
            _numSiteCount = new NumericUpDown
            {
                Location = new Point(160, 160),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 30,
                Value = 8
            };

            // Row 6: Non-Starter Sites/Regions Policy (Y = 194)
            var lblTheaterHomePolicy = new Label { Text = "Non-Starter Sites/Regs:", Location = new Point(12, 197), Size = new Size(144, 20) };
            _cmbTheaterHomeSitePolicy = new ComboBox
            {
                Location = new Point(160, 194),
                Size = new Size(242, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbTheaterHomeSitePolicy.Items.AddRange([
                "Ignore (Deploy at Havens)",
                "Best Effort (Fit in budget)",
                "Strict (Require all; abort)"
            ]);
            _cmbTheaterHomeSitePolicy.SelectedIndex = 0;

            void UpdateHavenTooltip()
            {
                string sel = _cmbHaven.SelectedItem?.ToString() ?? "";
                string tip = sel switch
                {
                    "Company Haven (Majority Vote)" => "Selects the Hero Haven serving the majority of your drafted company (falls back to a random Hero Haven if empty).",
                    "Nearest Haven (to Theater)" => "Automatically selects the Hero Haven closest (fewest region hops) to the drafted Theater cluster.",
                    "Random" => "Picks a random Hero Haven (Rivendell, Lórien, Grey Havens, or Edhellond).",
                    _ => string.Empty // No tooltip on named Havens
                };
                _toolTip.SetToolTip(_cmbHaven, tip);
            }

            void UpdateSeedTooltip()
            {
                string sel = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
                string tip = sel switch
                {
                    "Match Haven Anchor" => "Starts the region cluster in whatever region the Haven Anchor resolves to.",
                    "Random" => "Picks a random seed region anywhere across Middle-earth (respecting terrain filters). Under Strict, constrained to cover required non-starter home regions.",
                    "(N/A - Haven Hub)" => "Seed Region is unused in Starter Only (Haven Hub) mode.",
                    _ => string.Empty // No tooltip on named Regions
                };
                _toolTip.SetToolTip(_cmbSeedRegion, tip);
            }

            void UpdateTheaterHomePolicyTooltip()
            {
                string tip = _cmbTheaterHomeSitePolicy.SelectedIndex switch
                {
                    0 => "Ignore (Deploy at Havens): Drafts a pure Geographic Theater without pulling non-starter home sites or regions into the deck.",
                    1 => "Best Effort (Fit in budget): Includes non-starter Haven sites plus any non-Haven home sites/regions that fit within the Theater and budget; omits extras with a notice.",
                    _ => "Strict (Require all; abort): Requires all non-starter home sites and a contiguous region span covering all their non-Haven regions within Site/Region Count, aborting on shortfall."
                };
                _toolTip.SetToolTip(_cmbTheaterHomeSitePolicy, tip);
            }

            _cmbHaven.SelectedIndexChanged += (s, e) =>
            {
                UpdateHavenTooltip();
                if (_isUpdatingTheaterCombos) return;

                string havenChoice = _cmbHaven.SelectedItem?.ToString() ?? "";
                if (havenChoice.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
                {
                    string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
                    if (string.Equals(seedChoice, "Match Haven Anchor", StringComparison.OrdinalIgnoreCase))
                    {
                        _isUpdatingTheaterCombos = true;
                        try
                        {
                            int rndIdx = _cmbSeedRegion.FindStringExact("Random");
                            if (rndIdx >= 0) _cmbSeedRegion.SelectedIndex = rndIdx;
                        }
                        finally
                        {
                            _isUpdatingTheaterCombos = false;
                        }
                        UpdateSeedTooltip();
                    }
                }
            };

            _cmbSeedRegion.SelectedIndexChanged += (s, e) =>
            {
                UpdateSeedTooltip();
                if (_isUpdatingTheaterCombos) return;

                string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
                if (string.Equals(seedChoice, "Match Haven Anchor", StringComparison.OrdinalIgnoreCase))
                {
                    string havenChoice = _cmbHaven.SelectedItem?.ToString() ?? "";
                    if (havenChoice.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
                    {
                        _isUpdatingTheaterCombos = true;
                        try
                        {
                            int compIdx = _cmbHaven.FindStringExact("Company Haven (Majority Vote)");
                            if (compIdx >= 0) _cmbHaven.SelectedIndex = compIdx;
                            else _cmbHaven.SelectedIndex = 0;
                        }
                        finally
                        {
                            _isUpdatingTheaterCombos = false;
                        }
                        UpdateHavenTooltip();
                    }
                }
            };

            _cmbTheaterHomeSitePolicy.SelectedIndexChanged += (s, e) => UpdateTheaterHomePolicyTooltip();
            UpdateTheaterHomePolicyTooltip();

            // Inside DeckAssemblyMatrixDialog constructor:
            _cmbHaven.SelectedIndexChanged += (s, e) =>
            {
                if (_isUpdatingTheaterCombos) return;

                string havenChoice = _cmbHaven.SelectedItem?.ToString() ?? "";
                if (havenChoice.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
                {
                    // Haven is following Theater -> Theater cannot follow Haven!
                    string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
                    if (seedChoice.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
                    {
                        _isUpdatingTheaterCombos = true;
                        try
                        {
                            int rndIdx = _cmbSeedRegion.FindStringExact("Random (Across Middle-earth)");
                            if (rndIdx >= 0) _cmbSeedRegion.SelectedIndex = rndIdx;
                        }
                        finally
                        {
                            _isUpdatingTheaterCombos = false;
                        }
                    }
                }
            };

            _cmbSeedRegion.SelectedIndexChanged += (s, e) =>
            {
                if (_isUpdatingTheaterCombos) return;

                string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
                if (seedChoice.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
                {
                    // Theater is following Haven -> Haven cannot follow Theater!
                    string havenChoice = _cmbHaven.SelectedItem?.ToString() ?? "";
                    if (havenChoice.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
                    {
                        _isUpdatingTheaterCombos = true;
                        try
                        {
                            int compIdx = _cmbHaven.FindStringExact("Company Haven (Majority Vote)");
                            if (compIdx >= 0) _cmbHaven.SelectedIndex = compIdx;
                            else _cmbHaven.SelectedIndex = 0;
                        }
                        finally
                        {
                            _isUpdatingTheaterCombos = false;
                        }
                    }
                }
            };

            grpTheaterOpts.Controls.AddRange([
                lblMode, _cmbMode,
                lblHaven, _cmbHaven,
                lblSeed, _cmbSeedRegion,
                lblRegCount, _numRegionCount,
                lblSiteCount, _numSiteCount,
                lblTheaterHomePolicy, _cmbTheaterHomeSitePolicy
            ]);

            var grpFilters = new GroupBox
            {
                Text = "Terrain && Hold Classifications",
                Location = new Point(445, 16),
                Size = new Size(380, 235)
            };

            var lblRegTypes = new Label { Text = "Region Terrains (None = Any):", Location = new Point(12, 24), Size = new Size(170, 16) };
            _clbRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 42),
                Size = new Size(170, 178),
                CheckOnClick = true
            };
            _clbRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);

            var lblSiteTypes = new Label { Text = "Site Holds (None = Any):", Location = new Point(194, 24), Size = new Size(170, 16) };
            _clbSiteTypes = new CheckedListBox
            {
                Location = new Point(194, 42),
                Size = new Size(170, 178),
                CheckOnClick = true
            };
            _clbSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Haven"]);

            grpFilters.Controls.AddRange([lblRegTypes, _clbRegionTypes, lblSiteTypes, _clbSiteTypes]);

            var btnRunTheater = new Button
            {
                Text = "Run",
                Location = new Point(16, 264),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnRunTheater.Click += (s, e) => RunGeographicTheaterDraft();

            var btnClearTheater = new Button
            {
                Text = "Clear",
                Location = new Point(112, 264),
                Size = new Size(80, 28)
            };
            btnClearTheater.Click += (s, e) =>
            {
                _deck.Sites.Clear();
                NotifyDeckChanged(DeckSectionType.Sites);
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblTheaterStatus.Text = "Site deck cleared.";
            };

            var btnSaveTheater = new Button
            {
                Text = "Save",
                Location = new Point(198, 264),
                Size = new Size(80, 28)
            };
            btnSaveTheater.Click += (s, e) =>
            {
                _savedSites = [.. _deck.Sites];
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblTheaterStatus.Text = $"Session Snapshot Saved ({_savedSites.Count} cards).";
            };

            var btnRecallTheater = new Button
            {
                Text = "Recall",
                Location = new Point(284, 264),
                Size = new Size(80, 28)
            };
            btnRecallTheater.Click += (s, e) =>
            {
                if (_savedSites == null)
                {
                    MessageBox.Show(this, "No saved session snapshot for Geographic Theater.", "Recall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _deck.Sites.Clear();
                _deck.Sites.AddRange(_savedSites);
                _deck.SortSections();
                NotifyDeckChanged(DeckSectionType.Sites);
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblTheaterStatus.Text = $"Restored from Snapshot ({_savedSites.Count} cards).";
            };

            _lblTheaterStatus = new Label
            {
                Location = new Point(16, 302),
                Size = new Size(805, 60),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false
            };

            tabTheater.Controls.AddRange([
                grpTheaterOpts, grpFilters,
                btnRunTheater, btnClearTheater, btnSaveTheater, btnRecallTheater,
                _lblTheaterStatus
            ]);

            _tabControl.TabPages.AddRange([tabGeneral, tabStarter, tabRecruits, tabTheater]);
            _tabInfo[tabStarter.Text] = new(silent => RunStartingCompanyDraft(silent), _lblStarterStatus);
            _tabInfo[tabRecruits.Text] = new(silent => RunNonStartingCharactersDraft(silent), _lblRecruitStatus);
            _tabInfo[tabTheater.Text] = new(silent => RunGeographicTheaterDraft(silent), _lblTheaterStatus);
            BuildPipelineStepRows();
            Controls.Add(_tabControl);

            RefreshHavenDropdown();
            UpdateHavenTooltip();
            UpdateSeedTooltip();
            PopulateSeedRegionDropdown();
            UpdateDeckSummary();
            ValidateRecruitTabBudget();
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

        public void UpdateDeckSummary()
        {
            if (IsDisposed) return;

            RefreshHavenDropdown();

            // Refresh recruit status label if deck characters were cleared externally
            int totalResChars = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            int totalSideChars = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            if (totalResChars == 0 && totalSideChars == 0 && !_lblRecruitStatus.Text.StartsWith("Configured:", StringComparison.OrdinalIgnoreCase))
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                ValidateRecruitTabBudget();
            }

            int poolChars = _deck.Pool.Count;
            if (poolChars == 0 && !_lblStarterStatus.Text.StartsWith("Pool cleared", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_lblStarterStatus.Text))
            {
                _lblStarterStatus.Text = string.Empty;
            }

            int poolMind = _deck.Pool.Sum(c => ParseCardMind(c.Mind));
            int nonAvatarCharsInRes = _deck.Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInRes = _deck.Resources.Count(IsAvatar);
            int nonAvatarCharsInSideboard = _deck.Sideboard.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c));
            int avatarsInSideboard = _deck.Sideboard.Count(IsAvatar);
            int regionsInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int sitesInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase));

            string avatarResNote = avatarsInRes > 0 ? $" + {avatarsInRes} Avatar(s)" : "";
            string sideDetailNote = (nonAvatarCharsInSideboard > 0 || avatarsInSideboard > 0)
                ? $" ({nonAvatarCharsInSideboard} companions{(avatarsInSideboard > 0 ? $" + {avatarsInSideboard} Avatar" : "")})"
                : "";

            _lblDeckSummary.Text =
                $"Deck Title:       \"{_deck.Title}\"\n\n" +
                $"Pool:             {_deck.Pool.Count} characters ({poolMind} Mind)  [Official limit: <= 20 Mind]\n" +
                $"Resources:        {_deck.Resources.Count} cards ({nonAvatarCharsInRes} characters{avatarResNote})  [Official limits: <= 10 non-Avatar characters, <= 3 Avatars]\n" +
                $"Hazards:          {_deck.Hazards.Count} cards\n" +
                $"Sideboard:        {_deck.Sideboard.Count} cards{sideDetailNote}  [Target: {_numSideboardSize.Value}]\n" +
                $"Sites:            {_deck.Sites.Count} cards ({regionsInSites} Regions, {sitesInSites} Sites)\n\n" +
                $"Play Deck Status: {_deck.Resources.Count} Resources / {_deck.Hazards.Count} Hazards (Target: {_numPlayDeckSize.Value}/{_numPlayDeckSize.Value})\n" +
                $"Sideboard Status: {_deck.Sideboard.Count} / {_numSideboardSize.Value}";
        }

        private void RefreshHavenDropdown()
        {
            string currentSelection = _cmbHaven.SelectedItem?.ToString() ?? "";
            bool isStarterOnly = _cmbMode != null && _cmbMode.SelectedIndex == 1;

            _cmbHaven.BeginUpdate();
            _cmbHaven.Items.Clear();

            _cmbHaven.Items.Add("Company Haven (Majority Vote)");

            // Only available in Starter + Region roaming mode
            if (!isStarterOnly)
            {
                _cmbHaven.Items.Add("Nearest Haven (to Theater)");
            }

            _cmbHaven.Items.AddRange(["Rivendell", "Lórien", "Grey Havens", "Edhellond", "Random"]);

            int idx = _cmbHaven.FindStringExact(currentSelection);
            if (idx >= 0)
            {
                _cmbHaven.SelectedIndex = idx;
            }
            else
            {
                _cmbHaven.SelectedIndex = 0; // Default to Company Haven if "Nearest Haven" was removed
            }

            _cmbHaven.EndUpdate();
        }

        private void PopulateSeedRegionDropdown(string targetSelection = null)
        {
            string prev = targetSelection
                ?? _cmbSeedRegion.SelectedItem?.ToString()
                ?? "Match Haven Anchor";

            if (prev.StartsWith("(N/A", StringComparison.OrdinalIgnoreCase))
            {
                prev = "Match Haven Anchor";
            }

            _cmbSeedRegion.BeginUpdate();
            _cmbSeedRegion.Items.Clear();

            _cmbSeedRegion.Items.Add("Match Haven Anchor");
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

        private static string FindNearestHavenToCluster(
            Dictionary<string, HashSet<string>> graph,
            List<string> activeRegions)
        {
            if (activeRegions == null || activeRegions.Count == 0) return "Rivendell";

            // Haven home territories
            var havenCapitals = new (string Haven, string Region)[]
            {
                ("Rivendell", "Rhudaur"),
                ("Lórien", "Lórien"),
                ("Grey Havens", "Lindon"),
                ("Edhellond", "Anfalas")
            };

            string bestHaven = "Rivendell";
            int shortestHops = int.MaxValue;

            foreach (var (haven, capital) in havenCapitals)
            {
                if (activeRegions.Contains(capital, StringComparer.OrdinalIgnoreCase))
                {
                    return haven; // 0 hops: theater directly overlaps the haven's home territory
                }

                var path = DeckGenerationService.FindShortestRegionPath(graph, capital, activeRegions);
                int hops = path.Count > 0 ? path.Count : int.MaxValue;
                if (hops < shortestHops)
                {
                    shortestHops = hops;
                    bestHaven = haven;
                }
            }

            return bestHaven;
        }

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

                StarterHomeRegionPolicy = _cmbStarterHomeRegionPolicy.SelectedIndex,
                StarterRows = _starterRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                }).ToList(),

                PrimaryWizard = _cmbWizard1.SelectedItem?.ToString() ?? "(None)",
                PrimaryWizardCopies = int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1) ? c1 : 1,
                SecondaryWizard = _cmbWizard2.SelectedItem?.ToString() ?? "(None)",
                SecondaryWizardCopies = int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int c2) ? c2 : 1,
                SecondaryWizardDestination = _cmbWizard2Dest.SelectedItem?.ToString() ?? "Sideboard",
                TheaterHomeSitePolicy = _cmbTheaterHomeSitePolicy.SelectedIndex,

                RecruitRows = _recruitRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                }).ToList(),

                ReserveRows = _reserveRows.Select(r => new CharacterRowSettings
                {
                    Skill = r.CmbSkill.SelectedItem?.ToString() ?? "Any",
                    Species = r.CmbSpecies.SelectedItem?.ToString() ?? "Any",
                    Mind = r.TxtMind.Text,
                    DI = r.TxtDI.Text
                }).ToList(),

                HomeSitePolicy = _cmbHomeSitePolicy.SelectedIndex,

                MovementMode = _cmbMode.SelectedIndex,
                HavenAnchor = _cmbHaven.SelectedItem?.ToString() ?? "Rivendell",
                RegionCount = (int)_numRegionCount.Value,
                SiteCount = (int)_numSiteCount.Value,
                SelectedRegionTypes = _clbRegionTypes.CheckedItems.Cast<string>().ToList(),
                SelectedSiteTypes = _clbSiteTypes.CheckedItems.Cast<string>().ToList(),

                ExecutionOrder = _pipelineStepCombos
                    .Select(c => c.SelectedItem?.ToString() ?? "(None)")
                    .ToList(),
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

            if (config.StarterHomeRegionPolicy >= 0 && config.StarterHomeRegionPolicy < _cmbStarterHomeRegionPolicy.Items.Count)
            {
                _cmbStarterHomeRegionPolicy.SelectedIndex = config.StarterHomeRegionPolicy;
            }

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
            if (config.TheaterHomeSitePolicy >= 0 && config.TheaterHomeSitePolicy < _cmbTheaterHomeSitePolicy.Items.Count)
            {
                _cmbTheaterHomeSitePolicy.SelectedIndex = config.TheaterHomeSitePolicy;
            }
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

            if (config.HomeSitePolicy >= 0 && config.HomeSitePolicy < _cmbHomeSitePolicy.Items.Count)
            {
                _cmbHomeSitePolicy.SelectedIndex = config.HomeSitePolicy;
            }

            if (config.MovementMode >= 0 && config.MovementMode < _cmbMode.Items.Count)
            {
                _cmbMode.SelectedIndex = config.MovementMode;
            }

            RefreshHavenDropdown();
            SetComboSelection(_cmbHaven, config.HavenAnchor);

            if (config.RegionCount >= _numRegionCount.Minimum && config.RegionCount <= _numRegionCount.Maximum)
            {
                _numRegionCount.Value = config.RegionCount;
            }
            if (config.SiteCount >= _numSiteCount.Minimum && config.SiteCount <= _numSiteCount.Maximum)
            {
                _numSiteCount.Value = config.SiteCount;
            }

            if (config.ExecutionOrder != null && config.ExecutionOrder.Count > 0)
            {
                _isUpdatingPipelineCombos = true;
                try
                {
                    var usedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < _pipelineStepCombos.Count; i++)
                    {
                        string savedStep = i < config.ExecutionOrder.Count ? config.ExecutionOrder[i] : "(None)";
                        int idx = _pipelineStepCombos[i].FindStringExact(savedStep);

                        if (idx > 0 && usedTabs.Add(savedStep))
                        {
                            _pipelineStepCombos[i].SelectedIndex = idx;
                        }
                        else
                        {
                            _pipelineStepCombos[i].SelectedIndex = 0; // "(None)"
                        }

                        _previousStepSelections[i] = _pipelineStepCombos[i].SelectedItem?.ToString() ?? "(None)";
                    }
                }
                finally
                {
                    _isUpdatingPipelineCombos = false;
                }
            }
            else
            {
                ResetPipelineOrder();
            }

            SetCheckedItems(_clbRegionTypes, config.SelectedRegionTypes);
            SetCheckedItems(_clbSiteTypes, config.SelectedSiteTypes);

            UpdateDeckSummary();
            ValidateRecruitTabBudget();
        }

        private static void SetComboSelection(ComboBox cb, string value)
        {
            if (cb == null || string.IsNullOrWhiteSpace(value)) return;
            int idx = cb.FindStringExact(value);
            if (idx >= 0) cb.SelectedIndex = idx;
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

            ValidateRecruitTabBudget();
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

        private void ValidateRecruitTabBudget()
        {
            if (_btnRunRecruits == null || _lblRecruitStatus == null) return;

            int wizInRes = GetSelectedWizard1Count() + (IsWizard2InSideboard() ? 0 : GetSelectedWizard2Count());
            int wizInSide = IsWizard2InSideboard() ? GetSelectedWizard2Count() : 0;

            int companionCount = _recruitRows.Count(r => r.IsActive);
            int reserveCount = _reserveRows.Count(r => r.IsActive);

            if (companionCount > 10)
            {
                _btnRunRecruits.Enabled = false;
                _btnSaveRecruits.Enabled = false;
                _lblRecruitStatus.ForeColor = Color.Firebrick;
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"⚠ Selection exceeds the 10-companion limit for Resources: {companionCount}/10 companions configured.\n" +
                    $"Clear {companionCount - 10} companion row(s) to proceed.";
                return;
            }

            int totalSideboardChars = reserveCount + wizInSide;
            if (totalSideboardChars > (int)_numSideboardSize.Value)
            {
                _btnRunRecruits.Enabled = false;
                _btnSaveRecruits.Enabled = false;
                _lblRecruitStatus.ForeColor = Color.Firebrick;
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"⚠ Sideboard characters ({totalSideboardChars}: {reserveCount} reserves{(wizInSide > 0 ? $" + {wizInSide} Wizard" : "")}) exceed the Sideboard Size limit ({(int)_numSideboardSize.Value}) set in General Settings.\n" +
                    $"Please increase Sideboard Size, change the Secondary Wizard destination, or clear a reserve row.";
                return;
            }

            _btnRunRecruits.Enabled = true;
            _btnSaveRecruits.Enabled = true;
            _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);

            string wizResText = wizInRes > 0 ? $", {wizInRes} Wizard(s)" : "";
            string wizSideText = wizInSide > 0 ? $" + {wizInSide} Wizard" : "";
            _lblRecruitStatus.Text =
                $"Configured: {companionCount}/10 Resource companions{wizResText}  |  {reserveCount}/3 Sideboard reserves{wizSideText}.";
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

        private bool RunStartingCompanyDraft(bool silent = false)
        {
            var activeRows = _starterRows.Where(r => r.IsActive).ToList();
            if (activeRows.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No starter rows are filled in. Please select criteria for at least 1 companion.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var primaryTheaterRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var havenRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int starterPolicy = _cmbStarterHomeRegionPolicy.SelectedIndex; // 0 = Best Effort, 1 = Strict, 2 = Open / Anywhere

            if (starterPolicy != 2)
            {
                foreach (var c in _deck.Sites)
                {
                    if (string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(c.Name))
                        {
                            primaryTheaterRegions.Add(c.Name.Trim());
                        }
                    }
                    else if (string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Region))
                    {
                        if (string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                        {
                            havenRegions.Add(c.Region.Trim());
                        }
                        else
                        {
                            primaryTheaterRegions.Add(c.Region.Trim());
                        }
                    }
                }

                // Keep Haven fallback regions strictly separate from primary theater regions
                havenRegions.ExceptWith(primaryTheaterRegions);
            }

            if (starterPolicy == 1 && primaryTheaterRegions.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this,
                        "Strict Home Region Policy requires active Theater regions (Region cards or non-Haven adventure sites) in the Site deck.\n\nPlease configure and run Geographic Theater first, or set Home Region Policy to Best Effort / Open.",
                        "Strict Home Region Policy",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var tier1PerRow = new List<List<Card>>();
            var tier2PerRow = new List<List<Card>>();
            var tier3PerRow = new List<List<Card>>();

            for (int i = 0; i < activeRows.Count; i++)
            {
                var row = activeRows[i];

                if (starterPolicy == 1) // Strict
                {
                    var strictCandidates = GetCandidatesForCharacterRow(row, allCards, activeSets, primaryTheaterRegions);
                    if (strictCandidates.Count == 0)
                    {
                        if (!silent)
                        {
                            MessageBox.Show(this,
                                $"No matching cards found for Starter Row {i + 1} within the active Theater regions ({string.Join(", ", primaryTheaterRegions)}) in your active expansions.\n\nSwitch Home Region Policy to Best Effort or broaden row criteria.",
                                "Strict Home Region Policy",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                        return false;
                    }
                    tier1PerRow.Add(strictCandidates);
                    tier2PerRow.Add([]);
                    tier3PerRow.Add(strictCandidates);
                }
                else if (starterPolicy == 0) // Best Effort: Theater -> Haven fallback -> Open fallback
                {
                    var t1 = primaryTheaterRegions.Count > 0
                        ? GetCandidatesForCharacterRow(row, allCards, activeSets, primaryTheaterRegions)
                        : [];
                    var t2 = havenRegions.Count > 0
                        ? GetCandidatesForCharacterRow(row, allCards, activeSets, havenRegions)
                        : [];
                    var t3 = GetCandidatesForCharacterRow(row, allCards, activeSets, null);

                    if (t3.Count == 0)
                    {
                        if (!silent)
                        {
                            MessageBox.Show(this, $"No matching cards found for Starter Row {i + 1} with the selected criteria in your active expansions.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        return false;
                    }

                    tier1PerRow.Add(t1);
                    tier2PerRow.Add(t2);
                    tier3PerRow.Add(t3);
                }
                else // Open / Anywhere
                {
                    var openCandidates = GetCandidatesForCharacterRow(row, allCards, activeSets, null);
                    if (openCandidates.Count == 0)
                    {
                        if (!silent)
                        {
                            MessageBox.Show(this, $"No matching cards found for Starter Row {i + 1} with the selected criteria in your active expansions.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        return false;
                    }
                    tier1PerRow.Add([]);
                    tier2PerRow.Add([]);
                    tier3PerRow.Add(openCandidates);
                }
            }

            var minRowMind = tier3PerRow.Select(list => list.Min(c => ParseCardMind(c.Mind))).ToArray();
            int minPossibleMindSum = minRowMind.Sum();

            if (minPossibleMindSum > 20)
            {
                if (!silent)
                {
                    MessageBox.Show(this,
                        $"Cannot draft starting company: The minimum possible Mind sum for your selected rows is {minPossibleMindSum}, which exceeds the official 20-Mind starting company limit.\n\nPlease lower your Mind thresholds or clear one of the rows.",
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

                    Card chosen = null;

                    if (starterPolicy == 1)
                    {
                        var eligible = tier1PerRow[rowIdx]
                            .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                            .ToList();

                        if (eligible.Count > 0)
                        {
                            chosen = eligible[Random.Shared.Next(eligible.Count)];
                            t1Count++;
                        }
                    }
                    else
                    {
                        var eligibleT1 = tier1PerRow[rowIdx]
                            .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                            .ToList();

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

                if (validPermutation && currentMind <= 20)
                {
                    int score = (t1Count * 1000) + (t2Count * 100);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCompany = picked;
                        bestT1Count = t1Count;
                        bestT2Count = t2Count;
                        bestT3Count = t3Count;

                        if (starterPolicy != 0 || bestT1Count == activeRows.Count)
                        {
                            break;
                        }
                    }
                }
            }

            if (bestCompany == null)
            {
                if (!silent)
                {
                    string strictMsg = starterPolicy == 1
                        ? $"\nActive Theater regions: {string.Join(", ", primaryTheaterRegions)}\nConsider switching Home Region Policy to Best Effort."
                        : string.Empty;

                    MessageBox.Show(this,
                        $"Unable to find a valid starting company combination totaling 20 Mind or less with current criteria (after 100 attempts).{strictMsg}\n\nTry broadening skills, allowing lower-mind companions (Mind 1–3), or clearing a row.",
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
                RefreshHavenDropdown();
            }

            int finalMind = bestCompany.Sum(c => ParseCardMind(c.Mind));
            string companyNames = string.Join(", ", bestCompany.Select(c => $"{c.Name} ({c.Mind} Mind)"));

            string tierSummary = string.Empty;
            if (starterPolicy == 0 && (primaryTheaterRegions.Count > 0 || havenRegions.Count > 0))
            {
                if (bestT2Count > 0 || bestT3Count > 0)
                {
                    var parts = new List<string>();
                    if (bestT1Count > 0) parts.Add($"{bestT1Count} Theater");
                    if (bestT2Count > 0) parts.Add($"{bestT2Count} Haven fallback");
                    if (bestT3Count > 0) parts.Add($"{bestT3Count} Open fallback");
                    tierSummary = $" [{string.Join(", ", parts)}]";
                }
                else if (bestT1Count > 0)
                {
                    tierSummary = " [All from Theater regions]";
                }
            }
            else if (starterPolicy == 1)
            {
                tierSummary = " [Strict: Theater regions]";
            }

            _lblStarterStatus.Text = $"Drafted {bestCompany.Count} starting characters (Total Mind: {finalMind}/20){tierSummary}:\n{companyNames}";

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
                if (!silent)
                {
                    MessageBox.Show(this, "No Wizards, Resource companions, or Sideboard reserves are configured. Please select at least 1 character.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (activeRecruitRows.Count > 10)
            {
                ValidateRecruitTabBudget();
                if (!silent)
                {
                    MessageBox.Show(this,
                        $"Selection exceeds the 10-companion limit for Resources ({activeRecruitRows.Count}/10).",
                        "Non-Starting Characters",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            int wizInSide = IsWizard2InSideboard() ? wiz2Count : 0;
            int totalSideboardChars = activeReserveRows.Count + wizInSide;
            if (totalSideboardChars > (int)_numSideboardSize.Value)
            {
                ValidateRecruitTabBudget();
                if (!silent)
                {
                    MessageBox.Show(this,
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
                    MessageBox.Show(this, $"Could not find Primary Wizard '{wiz1Choice}' in your active expansion sets.", "Wizard Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (wiz1Count > 0 && isWiz1Random && allWizards.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No Wizards found in your active expansion sets.", "Wizard Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            // Precompute candidates per row (separating non-Haven Theater sites from Havens)
            int homeSitePolicy = _cmbHomeSitePolicy.SelectedIndex; // 0 = Best Effort, 1 = Strict, 2 = Open / Anywhere
            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var theaterAdventureSites = allSitesInDeck
                .Where(c => !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var havenSitesInDeck = allSitesInDeck
                .Where(c => string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (homeSitePolicy == 1 && allSitesInDeck.Count > 0 && theaterAdventureSites.Count == 0 &&
                (activeRecruitRows.Count > 0 || activeReserveRows.Count > 0))
            {
                if (!silent)
                {
                    MessageBox.Show(this,
                        "Strict Home Site Policy requires non-Haven adventure sites in the Site deck (found only Havens).\n\nAdd adventure sites in Geographic Theater or switch Home Site Policy to Best Effort / Open.",
                        "Strict Home Site Policy",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            static bool MatchesSiteList(Card c, List<Card> siteList, CardFilterService filterService)
            {
                if (siteList.Count == 0 || string.IsNullOrWhiteSpace(c.Home)) return false;
                string cleanHome = c.Home.Trim();
                return siteList.Any(s =>
                    CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                    string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                    CardFilterService.MatchesPredicate(s, cleanHome, filterService));
            }

            var rowSpecs = new List<(bool IsReserve, int RowNum, List<Card> AllCandidates, List<Card> SiteCandidates, List<Card> HavenCandidates)>();

            for (int i = 0; i < activeRecruitRows.Count; i++)
            {
                var cands = GetCandidatesForCharacterRow(activeRecruitRows[i], allCards, activeSets, null);
                var siteCands = (allSitesInDeck.Count > 0 && homeSitePolicy != 2)
                    ? cands.Where(c => MatchesSiteList(c, theaterAdventureSites, _filterService)).ToList()
                    : [];
                var havenCands = (allSitesInDeck.Count > 0 && homeSitePolicy == 0)
                    ? cands.Where(c => MatchesSiteList(c, havenSitesInDeck, _filterService)).ToList()
                    : [];

                if (cands.Count == 0 || (homeSitePolicy == 1 && theaterAdventureSites.Count > 0 && siteCands.Count == 0))
                {
                    if (!silent)
                    {
                        string msg = (homeSitePolicy == 1 && theaterAdventureSites.Count > 0)
                            ? $"Strict Home Site Policy: No matching candidate found for Play Deck Companion Row {i + 1} whose home site is a non-Haven adventure site in the active Site deck.\n\nBroaden row criteria, add the site to the Site deck, or set Home Site Policy to Best Effort / Open."
                            : $"No matching cards found for Play Deck Companion Row {i + 1}. Try broadening your criteria.";
                        MessageBox.Show(this, msg, "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return false;
                }
                rowSpecs.Add((false, i + 1, cands, siteCands, havenCands));
            }

            for (int i = 0; i < activeReserveRows.Count; i++)
            {
                var cands = GetCandidatesForCharacterRow(activeReserveRows[i], allCards, activeSets, null);
                var siteCands = (allSitesInDeck.Count > 0 && homeSitePolicy != 2)
                    ? cands.Where(c => MatchesSiteList(c, theaterAdventureSites, _filterService)).ToList()
                    : [];
                var havenCands = (allSitesInDeck.Count > 0 && homeSitePolicy == 0)
                    ? cands.Where(c => MatchesSiteList(c, havenSitesInDeck, _filterService)).ToList()
                    : [];

                if (cands.Count == 0 || (homeSitePolicy == 1 && theaterAdventureSites.Count > 0 && siteCands.Count == 0))
                {
                    if (!silent)
                    {
                        string msg = (homeSitePolicy == 1 && theaterAdventureSites.Count > 0)
                            ? $"Strict Home Site Policy: No matching candidate found for Sideboard Reserve Row {i + 1} whose home site is a non-Haven adventure site in the active Site deck.\n\nBroaden row criteria, add the site to the Site deck, or set Home Site Policy to Best Effort / Open."
                            : $"No matching cards found for Sideboard Reserve Row {i + 1}. Try broadening your criteria.";
                        MessageBox.Show(this, msg, "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return false;
                }
                rowSpecs.Add((true, i + 1, cands, siteCands, havenCands));
            }

            // Attempt up to 25 times (Attempt 0 processes tightest rows first; subsequent attempts shuffle)
            List<Card> finalRecruits = null;
            List<Card> finalReserves = null;
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
                    ? rowSpecs.OrderBy(r => (homeSitePolicy == 1 && theaterAdventureSites.Count > 0) ? r.SiteCandidates.Count : r.AllCandidates.Count).ToList()
                    : [.. rowSpecs.OrderBy(_ => Random.Shared.Next())];

                bool failed = false;
                foreach (var (IsReserve, RowNum, AllCandidates, SiteCandidates, HavenCandidates) in orderedSpecs)
                {
                    List<Card> available;
                    if (theaterAdventureSites.Count > 0 && homeSitePolicy == 1) // Strict
                    {
                        available = SiteCandidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                    }
                    else if (allSitesInDeck.Count > 0 && homeSitePolicy == 0) // Best Effort: Adventure Sites -> Havens -> Open
                    {
                        var siteAvail = SiteCandidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                        if (siteAvail.Count > 0)
                        {
                            available = siteAvail;
                        }
                        else
                        {
                            var havenAvail = HavenCandidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                            available = havenAvail.Count > 0
                                ? havenAvail
                                : AllCandidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                        }
                    }
                    else // Open / empty Site deck
                    {
                        available = AllCandidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                    }

                    if (available.Count == 0)
                    {
                        string label = IsReserve ? $"Sideboard Reserve Row {RowNum}" : $"Play Deck Companion Row {RowNum}";
                        lastFailureReason = (homeSitePolicy == 1 && theaterAdventureSites.Count > 0)
                            ? $"Strict Home Site Policy: Not enough distinct characters matching non-Haven sites in the active Site deck to fill {label}."
                            : $"Not enough distinct matching characters to fill {label}.";
                        failed = true;
                        break;
                    }

                    var chosen = available[Random.Shared.Next(available.Count)];
                    if (IsReserve) attemptReserves.Add(chosen);
                    else attemptRecruits.Add(chosen);

                    if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                    {
                        usedTitles.Add(chosen.Name);
                    }
                }

                if (!failed)
                {
                    finalRecruits = attemptRecruits;
                    finalReserves = attemptReserves;
                    break;
                }
            }

            if (finalRecruits == null || finalReserves == null)
            {
                if (!silent)
                {
                    MessageBox.Show(this,
                        lastFailureReason ?? "Unable to draft non-starting characters with current criteria.",
                        "Non-Starting Characters",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            // Commit to Deck partitions
            _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Resources.AddRange(finalRecruits);

            _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Sideboard.AddRange(finalReserves);

            _deck.SortSections();
            if (!silent)
            {
                NotifyDeckChanged(finalRecruits.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            // Update Status & Warn if Saruman / Radagast lack their land home site in current Site deck
            string recruitNames = finalRecruits.Count > 0 ? string.Join(", ", finalRecruits.Select(c => c.Name)) : "(None)";
            string reserveNames = finalReserves.Count > 0 ? string.Join(", ", finalReserves.Select(c => c.Name)) : "(None)";

            var wizardWarnings = new List<string>();
            if (_deck.Sites.Count > 0)
            {
                var draftedWizards = finalRecruits.Concat(finalReserves).Where(IsAvatar).Select(w => w.Name).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var wizName in draftedWizards)
                {
                    string landSite = null;
                    if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Saruman")) landSite = "Isengard";
                    else if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Radagast")) landSite = "Rhosgobel";

                    if (landSite != null && !_deck.Sites.Any(s => string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(s.Name, landSite)))
                    {
                        wizardWarnings.Add($"{wizName}'s home site ({landSite}) is not in the Site deck (must enter at a Haven unless Theater is revised).");
                    }
                }
            }

            if (wizardWarnings.Count > 0)
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard:\n" +
                    $"• Resources: {recruitNames}  |  Sideboard: {reserveNames}\n" +
                    $"⚠ {string.Join(" ", wizardWarnings)}";
            }
            else
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard:\n" +
                    $"• Resources: {recruitNames}\n" +
                    $"• Sideboard: {reserveNames}";
            }

            return true;
        }

        private bool RunGeographicTheaterDraft(bool silent = false)
        {
            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            bool isStarterPlusRegion = _cmbMode.SelectedIndex == 0;
            int maxRegions = isStarterPlusRegion ? (int)_numRegionCount.Value : 0;
            int siteCount = (int)_numSiteCount.Value;
            int theaterHomePolicy = _cmbTheaterHomeSitePolicy.SelectedIndex; // 0 = Ignore, 1 = Best Effort, 2 = Strict

            // =========================================================================
            // 1. GATHER NON-STARTER CHARACTER HOME SITES (Resources + Sideboard)
            // =========================================================================
            var nonStarterCharacters = _deck.Resources.Concat(_deck.Sideboard)
                .Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c))
                .ToList();

            var candidateHomeEntries = new List<(string Owner, Card SiteCard)>();
            if (theaterHomePolicy != 0)
            {
                foreach (var charCard in nonStarterCharacters)
                {
                    if (string.IsNullOrWhiteSpace(charCard.Home)) continue;
                    string cleanHome = charCard.Home.Trim();

                    var homeMatch = allCards.FirstOrDefault(s =>
                        string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        (CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                         string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                         CardFilterService.MatchesPredicate(s, cleanHome, _filterService)));

                    if (homeMatch != null)
                    {
                        string sectionTag = _deck.Sideboard.Contains(charCard) ? " (Sideboard)" : "";
                        candidateHomeEntries.Add(($"{charCard.Name}{sectionTag}", homeMatch));
                    }
                }
            }

            // =========================================================================
            // 2. BUILD GRAPH & TERRAIN FILTER EARLY (NEEDED FOR STRICT SPANNING CHECK)
            // =========================================================================
            var graph = _filterService.BuildRegionAdjacencyGraph(allCards);

            var checkedTerrains = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            string terrainFilter = checkedTerrains.Count > 0 ? string.Join("; ", checkedTerrains) : null;

            var checkedHolds = _clbSiteTypes.CheckedItems.Cast<string>().ToList();
            string holdFilter = checkedHolds.Count > 0 ? string.Join("; ", checkedHolds) : null;

            HashSet<string> allowedRegions = null;
            if (!string.IsNullOrWhiteSpace(terrainFilter))
            {
                var matching = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var regCard in allCards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
                {
                    var rTypes = DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService);
                    if (DeckGenerationService.MatchesRegionTypeFilter(rTypes, terrainFilter))
                    {
                        matching.Add(regCard.Name.Trim());
                    }
                }

                if (matching.Count > 0)
                {
                    allowedRegions = matching;
                }
            }

            string seedChoice = _cmbSeedRegion?.SelectedItem?.ToString() ?? "Match Haven Anchor";
            bool isRandomSeed = string.Equals(seedChoice, "Random", StringComparison.OrdinalIgnoreCase);
            bool isMatchHavenSeed = string.Equals(seedChoice, "Match Haven Anchor", StringComparison.OrdinalIgnoreCase);
            bool isExplicitNamedSeed = !isRandomSeed && !isMatchHavenSeed && !seedChoice.StartsWith("(N/A", StringComparison.OrdinalIgnoreCase);

            // =========================================================================
            // 3. VALIDATE STRICT NON-STARTER SITES & CONTIGUOUS REGIONS
            // =========================================================================
            var acceptedHomeSites = new List<Card>();
            var acceptedHomeRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shortfallList = new List<(string Owner, string SiteName, string Region)>();
            var strictRequiredRegions = new List<string>();

            if (theaterHomePolicy == 2) // Strict
            {
                var distinctCandidateSites = candidateHomeEntries
                    .Select(e => e.SiteCard)
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                int requiredAdventureCount = distinctCandidateSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));
                var requiredHomeRegions = distinctCandidateSites
                    .Where(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.Region))
                    .Select(s => s.Region.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                bool siteDeficit = requiredAdventureCount > siteCount;

                if (isStarterPlusRegion)
                {
                    if (isExplicitNamedSeed)
                    {
                        strictRequiredRegions.Add(seedChoice);
                    }
                    foreach (var hr in requiredHomeRegions)
                    {
                        if (!strictRequiredRegions.Contains(hr, StringComparer.OrdinalIgnoreCase))
                        {
                            strictRequiredRegions.Add(hr);
                        }
                    }

                    var blockedByTerrain = allowedRegions != null
                        ? strictRequiredRegions.Where(r => !allowedRegions.Contains(r)).ToList()
                        : [];

                    List<string> testBackbone = null;
                    if (blockedByTerrain.Count == 0 && strictRequiredRegions.Count > 0)
                    {
                        testBackbone = FindContiguousSpanningBackbone(graph, strictRequiredRegions, allowedRegions);
                    }

                    bool unreachable = blockedByTerrain.Count == 0 && strictRequiredRegions.Count > 0 && testBackbone == null;
                    bool spanDeficit = testBackbone != null && testBackbone.Count > maxRegions;

                    if (siteDeficit || blockedByTerrain.Count > 0 || unreachable || spanDeficit)
                    {
                        var siteBulletList = string.Join("\n", distinctCandidateSites.Select(s => $"  • {s.Name} ({s.Region})"));
                        var reasons = new List<string>();
                        if (siteDeficit)
                            reasons.Add($"• Site Count needed: at least {requiredAdventureCount} (current: {siteCount})");
                        if (blockedByTerrain.Count > 0)
                            reasons.Add($"• Excluded by Region Terrains filter: {string.Join(", ", blockedByTerrain)}");
                        if (unreachable)
                            reasons.Add($"• Required regions cannot connect contiguously under current terrain filters: {string.Join(", ", strictRequiredRegions)}");
                        if (spanDeficit)
                            reasons.Add($"• Region Count needed to connect [{string.Join(", ", strictRequiredRegions)}]: at least {testBackbone.Count} contiguous regions (current: {maxRegions})");

                        if (!silent)
                        {
                            MessageBox.Show(this,
                            $"Cannot assemble Geographic Theater (Strict Non-Starter Sites/Regs Policy):\n\n" +
                            $"Required non-starter home sites:\n{siteBulletList}\n\n" +
                            $"Shortfalls:\n{string.Join("\n", reasons)}\n\n" +
                            $"No changes were made to your Site deck.",
                            "Geographic Theater Budget Shortfall",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        }

                        _lblTheaterStatus.ForeColor = Color.Firebrick;
                        _lblTheaterStatus.Text = "Draft aborted: Non-starter home sites/regions cannot fit contiguously within Theater constraints (Strict).";
                        return false;
                    }
                }
                else if (siteDeficit)
                {
                    var siteBulletList = string.Join("\n", distinctCandidateSites.Select(s => $"  • {s.Name} ({s.Region})"));
                    if (!silent)
                    {
                        MessageBox.Show(this,
                        $"Cannot assemble Geographic Theater (Strict Non-Starter Sites/Regs Policy):\n\n" +
                        $"Required non-starter home sites:\n{siteBulletList}\n\n" +
                        $"• Site Count needed: at least {requiredAdventureCount} (current: {siteCount})\n\n" +
                        $"No changes were made to your Site deck.",
                        "Geographic Theater Budget Shortfall",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    }

                    _lblTheaterStatus.ForeColor = Color.Firebrick;
                    _lblTheaterStatus.Text = "Draft aborted: Insufficient Site Count for non-starter home sites (Strict).";
                    return false;
                }

                foreach (var (_, siteCard) in candidateHomeEntries)
                {
                    if (!acceptedHomeSites.Any(s => string.Equals(s.Name, siteCard.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        acceptedHomeSites.Add(siteCard);
                        if (!string.Equals(siteCard.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(siteCard.Region))
                        {
                            acceptedHomeRegions.Add(siteCard.Region.Trim());
                        }
                    }
                }
            }

            // =========================================================================
            // 4. RESOLVE HAVEN ANCHOR
            // =========================================================================
            string rawHaven = _cmbHaven.SelectedItem?.ToString() ?? "Company Haven (Majority Vote)";
            string selectedHaven = "Rivendell";

            if (rawHaven.StartsWith("Company Haven", StringComparison.OrdinalIgnoreCase) ||
                rawHaven.StartsWith("Starting Company", StringComparison.OrdinalIgnoreCase))
            {
                var votes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var charactersToVote = _deck.Pool.Count > 0
                    ? _deck.Pool
                    : _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c)).ToList();

                foreach (var starter in charactersToVote)
                {
                    if (string.IsNullOrWhiteSpace(starter.Home)) continue;
                    string cleanHome = starter.Home.Trim();

                    var homeSite = allCards.FirstOrDefault(s =>
                        string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        (CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                         string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                         CardFilterService.MatchesPredicate(s, cleanHome, _filterService)));

                    if (homeSite != null && !string.IsNullOrWhiteSpace(homeSite.Haven))
                    {
                        string hName = homeSite.Haven.Trim();
                        var canonical = HeroHavens.FirstOrDefault(h => CardFilterService.EqualsWithDiacriticFallback(h, hName)) ?? hName;
                        votes[canonical] = votes.GetValueOrDefault(canonical, 0) + 1;
                    }
                }

                if (votes.Count > 0)
                {
                    int maxVotes = votes.Values.Max();
                    var top = votes.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();
                    selectedHaven = top[Random.Shared.Next(top.Count)];
                }
                else
                {
                    selectedHaven = (!isStarterPlusRegion && siteCount > 0)
                        ? PickBestHavenForStarterHub()
                        : HeroHavens[Random.Shared.Next(HeroHavens.Length)];
                }
            }
            else if (rawHaven.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = (!isStarterPlusRegion && siteCount > 0)
                    ? PickBestHavenForStarterHub()
                    : HeroHavens[Random.Shared.Next(HeroHavens.Length)];
            }
            else if (!rawHaven.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = rawHaven;
            }

            string PickBestHavenForStarterHub()
            {
                var shuffledHavens = HeroHavens.OrderBy(_ => Random.Shared.Next()).ToList();
                string bestHaven = shuffledHavens[0];
                int bestCount = -1;

                foreach (var h in shuffledHavens)
                {
                    int count = allCards.Count(c =>
                        string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                        (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                        CardFilterService.EqualsWithDiacriticFallback(c.Haven, h) &&
                        (string.IsNullOrWhiteSpace(holdFilter) || DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter)) &&
                        (string.IsNullOrWhiteSpace(terrainFilter) || DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter)));

                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestHaven = h;
                    }

                    if (count >= siteCount)
                    {
                        bestHaven = h;
                        break;
                    }
                }

                return bestHaven;
            }

            // =========================================================================
            // 5. EXPAND CONTIGUOUS REGION CLUSTER (MULTI-ATTEMPT FOR RANDOM / SITES)
            // =========================================================================
            var candidateSeeds = allCards
                .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                            (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                            (allowedRegions == null || allowedRegions.Contains(c.Name.Trim())))
                .ToList();

            string seedRegion = "Rhudaur";
            var activeRegions = new List<string>();

            int maxAttempts = (isRandomSeed || siteCount > 0 || strictRequiredRegions.Count > 1) ? 100 : 1;
            string bestSeed = null;
            List<string> bestCluster = [];
            int bestFulfillmentScore = -1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                string trialSeed;
                var trialCluster = new List<string>();

                if (isStarterPlusRegion)
                {
                    if (theaterHomePolicy == 2 && strictRequiredRegions.Count > 0)
                    {
                        // Strict with required non-starter regions: build contiguous backbone covering all required regions
                        trialSeed = isExplicitNamedSeed ? seedChoice : strictRequiredRegions[0];
                        var backbone = FindContiguousSpanningBackbone(graph, strictRequiredRegions, allowedRegions);
                        if (backbone == null || backbone.Count > maxRegions)
                        {
                            continue;
                        }

                        trialCluster.AddRange(backbone);
                        ExpandClusterToTarget(graph, trialCluster, maxRegions, allowedRegions);
                    }
                    else
                    {
                        if (isRandomSeed && candidateSeeds.Count > 0)
                        {
                            trialSeed = candidateSeeds[Random.Shared.Next(candidateSeeds.Count)].Name.Trim();
                        }
                        else if (isExplicitNamedSeed)
                        {
                            trialSeed = seedChoice;
                        }
                        else
                        {
                            var hCard = allCards.FirstOrDefault(c =>
                                string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                                CardFilterService.EqualsWithDiacriticFallback(c.Name, selectedHaven));
                            trialSeed = (hCard != null && !string.IsNullOrWhiteSpace(hCard.Region)) ? hCard.Region.Trim() : "Rhudaur";
                        }

                        if (allowedRegions != null && !allowedRegions.Contains(trialSeed))
                        {
                            var pathToAllowed = DeckGenerationService.FindShortestRegionPath(graph, trialSeed, allowedRegions);
                            if (pathToAllowed.Count > 0)
                            {
                                trialSeed = pathToAllowed[^1];
                            }
                        }

                        if (allowedRegions == null || allowedRegions.Contains(trialSeed))
                        {
                            trialCluster.Add(trialSeed);
                        }

                        // Best Effort (when not using Random): bridge any reachable non-Haven home regions that fit within maxRegions
                        if (theaterHomePolicy == 1 && !isRandomSeed && trialCluster.Count > 0)
                        {
                            var desiredHomeRegions = candidateHomeEntries
                                .Where(e => !string.Equals(e.SiteCard.Site, "Haven", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(e.SiteCard.Region))
                                .Select(e => e.SiteCard.Region.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            foreach (var hr in desiredHomeRegions)
                            {
                                if (trialCluster.Contains(hr, StringComparer.OrdinalIgnoreCase)) continue;
                                if (allowedRegions != null && !allowedRegions.Contains(hr)) continue;

                                var candidateSpan = new List<string>(trialCluster) { hr };
                                var bridged = FindContiguousSpanningBackbone(graph, candidateSpan, allowedRegions);
                                if (bridged != null && bridged.Count <= maxRegions)
                                {
                                    trialCluster = bridged;
                                }
                            }
                        }

                        if (trialCluster.Count > 0)
                        {
                            ExpandClusterToTarget(graph, trialCluster, maxRegions, allowedRegions);
                        }
                    }
                }
                else
                {
                    trialSeed = selectedHaven;
                }

                int matchingSitesInCluster = 0;
                if (siteCount > 0)
                {
                    matchingSitesInCluster = allCards.Count(c =>
                        string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                        (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                        (!isStarterPlusRegion || trialCluster.Contains(c.Region, StringComparer.OrdinalIgnoreCase)) &&
                        (isStarterPlusRegion || CardFilterService.EqualsWithDiacriticFallback(c.Haven, selectedHaven)) &&
                        (string.IsNullOrWhiteSpace(holdFilter) || DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter)) &&
                        (string.IsNullOrWhiteSpace(terrainFilter) || DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter)));
                }

                int currentScore = (trialCluster.Count * 10) + Math.Min(matchingSitesInCluster, siteCount);

                if (currentScore > bestFulfillmentScore)
                {
                    bestFulfillmentScore = currentScore;
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                }

                bool regionsSatisfied = !isStarterPlusRegion || trialCluster.Count >= maxRegions;
                bool sitesSatisfied = siteCount == 0 || matchingSitesInCluster >= siteCount;

                if (regionsSatisfied && sitesSatisfied)
                {
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                    break;
                }
            }

            seedRegion = bestSeed ?? "Rhudaur";
            activeRegions = bestCluster;

            // Snap Nearest Haven to the winning theater
            if (rawHaven.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = FindNearestHavenToCluster(graph, activeRegions);
            }

            // =========================================================================
            // 6. FINALIZE BEST EFFORT NON-STARTER HOME SITES AGAINST CHOSEN THEATER
            // =========================================================================
            if (theaterHomePolicy == 1) // Best Effort
            {
                foreach (var (owner, siteCard) in candidateHomeEntries)
                {
                    if (acceptedHomeSites.Any(s => string.Equals(s.Name, siteCard.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    bool isAdvSite = !string.Equals(siteCard.Site, "Haven", StringComparison.OrdinalIgnoreCase);
                    int currentAdvCount = acceptedHomeSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));

                    bool siteFits = !isAdvSite || (currentAdvCount < siteCount);
                    bool regionFits = !isAdvSite || !isStarterPlusRegion ||
                        (!string.IsNullOrWhiteSpace(siteCard.Region) &&
                         activeRegions.Contains(siteCard.Region.Trim(), StringComparer.OrdinalIgnoreCase));

                    if (siteFits && regionFits)
                    {
                        acceptedHomeSites.Add(siteCard);
                    }
                    else
                    {
                        shortfallList.Add((owner, siteCard.Name, siteCard.Region ?? "Unknown"));
                    }
                }
            }

            // =========================================================================
            // 7. ASSEMBLE SITES & REGIONS
            // =========================================================================
            var havenCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, selectedHaven));
            var draftedSitesAndRegions = new List<Card>();

            var rivendell = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Name, "Rivendell", StringComparison.OrdinalIgnoreCase));
            if (rivendell != null) draftedSitesAndRegions.Add(rivendell);

            if (!CardFilterService.EqualsWithDiacriticFallback(selectedHaven, "Rivendell") && havenCard != null)
            {
                if (CardFilterService.EqualsWithDiacriticFallback(selectedHaven, "Edhellond"))
                {
                    var step = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Name, "Lórien", StringComparison.OrdinalIgnoreCase))
                            ?? allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Name, "Grey Havens", StringComparison.OrdinalIgnoreCase));
                    if (step != null && !draftedSitesAndRegions.Any(s => string.Equals(s.Name, step.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        draftedSitesAndRegions.Add(step);
                    }
                }

                if (!draftedSitesAndRegions.Any(s => string.Equals(s.Name, havenCard.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    draftedSitesAndRegions.Add(havenCard);
                }
            }

            if (isStarterPlusRegion)
            {
                foreach (var reg in activeRegions)
                {
                    var regCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, reg));
                    if (regCard != null && !draftedSitesAndRegions.Any(s => string.Equals(s.Name, regCard.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        draftedSitesAndRegions.Add(regCard);
                    }
                }
            }

            foreach (var hs in acceptedHomeSites)
            {
                if (!draftedSitesAndRegions.Any(s => string.Equals(s.Name, hs.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    draftedSitesAndRegions.Add(hs);
                }
            }

            int currentDraftedAdvSites = acceptedHomeSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int remainingSiteBudget = Math.Max(0, siteCount - currentDraftedAdvSites);

            if (remainingSiteBudget > 0)
            {
                var candidateAdventureSites = allCards.Where(c =>
                {
                    if (!string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase)) return false;
                    if (string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase)) return false;
                    if (activeSets.Count > 0 && !activeSets.Contains(c.Set)) return false;

                    if (isStarterPlusRegion)
                    {
                        if (!activeRegions.Contains(c.Region, StringComparer.OrdinalIgnoreCase)) return false;
                    }
                    else
                    {
                        if (!CardFilterService.EqualsWithDiacriticFallback(c.Haven, selectedHaven)) return false;
                    }

                    if (!string.IsNullOrWhiteSpace(holdFilter) && !DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter))
                        return false;

                    if (!string.IsNullOrWhiteSpace(terrainFilter) && !DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                        return false;

                    return !draftedSitesAndRegions.Any(s => string.Equals(s.Name, c.Name, StringComparison.OrdinalIgnoreCase));
                }).ToList();

                int added = 0;
                while (added < remainingSiteBudget && candidateAdventureSites.Count > 0)
                {
                    var sample = candidateAdventureSites[Random.Shared.Next(candidateAdventureSites.Count)];
                    draftedSitesAndRegions.Add(sample);
                    candidateAdventureSites.Remove(sample);
                    added++;
                }
            }

            // =========================================================================
            // 8. UPDATE DECK, SORT, AND REFRESH UI
            // =========================================================================
            _deck.Sites.Clear();
            _deck.Sites.AddRange(draftedSitesAndRegions);
            _deck.SortSections();
            if (!silent)
                NotifyDeckChanged(DeckSectionType.Sites);

            int finalRegions = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int finalSites = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase));
            int adventureSitesCount = draftedSitesAndRegions.Count(s =>
                string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string hubNote = isStarterPlusRegion
                ? (theaterHomePolicy == 2 && strictRequiredRegions.Count > 0 && isRandomSeed
                    ? $"(Seed: {seedRegion} [from Non-Starter Sites/Regs])"
                    : $"(Seed: {seedRegion})")
                : $"(Starter Hub: {selectedHaven})";

            string statusMsg = $"Drafted {draftedSitesAndRegions.Count} cards ({finalRegions} Regions, {finalSites} Sites). Haven: {selectedHaven} {hubNote}";

            var warnings = new List<string>();

            if (isStarterPlusRegion && activeRegions.Count < maxRegions)
            {
                string terrainNote = !string.IsNullOrWhiteSpace(terrainFilter) ? $" matching '{terrainFilter}'" : "";
                warnings.Add($"Region Shortfall: Found only {activeRegions.Count} of {maxRegions} contiguous regions{terrainNote}.");
            }

            if (siteCount > 0 && adventureSitesCount < siteCount)
            {
                warnings.Add($"Site Shortfall: Found only {adventureSitesCount} of {siteCount} requested adventure sites.");
            }

            if (shortfallList.Count > 0)
            {
                var omittedBrief = string.Join(", ", shortfallList.Select(s => $"{s.Owner} -> {s.SiteName}"));
                warnings.Add($"Omitted {shortfallList.Count} home site(s) outside theater/budget (deploy at Haven): {omittedBrief}");
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

        #endregion

        #region HELPERS

        private static List<string> FindContiguousSpanningBackbone(
            Dictionary<string, HashSet<string>> graph,
            List<string> requiredRegions,
            HashSet<string> allowedRegions)
        {
            if (requiredRegions == null || requiredRegions.Count == 0)
            {
                return [];
            }

            foreach (var req in requiredRegions)
            {
                if (allowedRegions != null && !allowedRegions.Contains(req))
                {
                    return null;
                }
            }

            var backbone = new List<string> { requiredRegions[0] };
            var backboneSet = new HashSet<string>(backbone, StringComparer.OrdinalIgnoreCase);
            var remaining = new List<string>();

            for (int i = 1; i < requiredRegions.Count; i++)
            {
                if (!backboneSet.Contains(requiredRegions[i]))
                {
                    remaining.Add(requiredRegions[i]);
                }
            }

            while (remaining.Count > 0)
            {
                var queue = new Queue<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var remainingSet = new HashSet<string>(remaining, StringComparer.OrdinalIgnoreCase);

                foreach (var b in backbone.OrderBy(_ => Random.Shared.Next()))
                {
                    queue.Enqueue(b);
                    visited.Add(b);
                }

                string foundTarget = null;

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    if (remainingSet.Contains(current))
                    {
                        foundTarget = current;
                        break;
                    }

                    if (graph.TryGetValue(current, out var neighbors))
                    {
                        foreach (var neighbor in neighbors.OrderBy(_ => Random.Shared.Next()))
                        {
                            if ((allowedRegions == null || allowedRegions.Contains(neighbor)) &&
                                visited.Add(neighbor))
                            {
                                parent[neighbor] = current;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                if (foundTarget == null)
                {
                    return null;
                }

                var pathSegment = new List<string>();
                string curr = foundTarget;
                while (!backboneSet.Contains(curr))
                {
                    pathSegment.Add(curr);
                    if (!parent.TryGetValue(curr, out curr))
                    {
                        break;
                    }
                }
                pathSegment.Reverse();

                foreach (var node in pathSegment)
                {
                    if (backboneSet.Add(node))
                    {
                        backbone.Add(node);
                    }
                }

                remaining.RemoveAll(r => backboneSet.Contains(r));
            }

            return backbone;
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

        private List<Card> GetCandidatesForCharacterRow(
            CharacterRowControls row,
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            HashSet<string> theaterRegions)
        {
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

                if (theaterRegions != null && theaterRegions.Count > 0)
                {
                    bool inTheater = false;

                    var homeRegionTags = _filterService.GetCardTagsForCategory(c.Id, "Home Region");
                    if (homeRegionTags.Any(t => theaterRegions.Any(tr => CardFilterService.EqualsWithDiacriticFallback(t.Trim(), tr))))
                    {
                        inTheater = true;
                    }

                    if (!inTheater && !string.IsNullOrWhiteSpace(c.Home))
                    {
                        string cleanHome = c.Home.Trim();
                        var homeSite = allCards.FirstOrDefault(s =>
                            string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                            (CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                             string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase)));

                        if (homeSite != null && !string.IsNullOrWhiteSpace(homeSite.Region))
                        {
                            if (theaterRegions.Any(tr => CardFilterService.EqualsWithDiacriticFallback(homeSite.Region.Trim(), tr)))
                            {
                                inTheater = true;
                            }
                        }
                    }

                    if (!inTheater) return false;
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

        private static int ParseCardMind(string mindStr)
        {
            if (string.IsNullOrWhiteSpace(mindStr)) return 0;
            string clean = mindStr.Trim('(', ')').Trim();
            return int.TryParse(clean, out int val) ? val : 0;
        }

        #endregion

        #region EXECUTION_PIPELINE

        private List<string> GetRunnableTabNames()
        {
            var names = new List<string>();
            foreach (TabPage page in _tabControl.TabPages)
            {
                if (_tabInfo.ContainsKey(page.Text))
                {
                    names.Add(page.Text);
                }
            }
            return names;
        }

        private void BuildPipelineStepRows()
        {
            _pnlPipelineSteps.Controls.Clear();
            _pipelineStepCombos.Clear();
            _previousStepSelections.Clear();

            var runnableTabs = GetRunnableTabNames();

            for (int i = 0; i < runnableTabs.Count; i++)
            {
                int y = i * 30;

                var lblStep = new Label
                {
                    Text = $"Step {i + 1}:",
                    Location = new Point(4, y + 4),
                    Size = new Size(56, 18),
                    Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
                };

                var cmbStep = new ComboBox
                {
                    Location = new Point(64, y + 1),
                    Size = new Size(370, 23),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Tag = i
                };

                cmbStep.Items.Add("(None)");
                foreach (var tabName in runnableTabs)
                {
                    cmbStep.Items.Add(tabName);
                }

                cmbStep.SelectedIndex = i + 1;
                _previousStepSelections.Add(runnableTabs[i]);
                cmbStep.SelectedIndexChanged += PipelineStepCombo_SelectedIndexChanged;

                _pipelineStepCombos.Add(cmbStep);
                _pnlPipelineSteps.Controls.AddRange([lblStep, cmbStep]);
            }
        }

        private void PipelineStepCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingPipelineCombos || sender is not ComboBox changedCombo) return;
            if (changedCombo.Tag is not int changedIndex) return;

            string newValue = changedCombo.SelectedItem?.ToString() ?? "(None)";
            string oldValue = _previousStepSelections[changedIndex];

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase)) return;

            _isUpdatingPipelineCombos = true;
            try
            {
                // Auto-swap if another step already holds the chosen tab (ignore "(None)")
                if (!string.Equals(newValue, "(None)", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < _pipelineStepCombos.Count; i++)
                    {
                        if (i == changedIndex) continue;

                        string otherValue = _pipelineStepCombos[i].SelectedItem?.ToString() ?? "(None)";
                        if (string.Equals(otherValue, newValue, StringComparison.OrdinalIgnoreCase))
                        {
                            int swapIdx = _pipelineStepCombos[i].FindStringExact(oldValue);
                            _pipelineStepCombos[i].SelectedIndex = swapIdx >= 0 ? swapIdx : 0;
                            _previousStepSelections[i] = _pipelineStepCombos[i].SelectedItem?.ToString() ?? "(None)";
                            break;
                        }
                    }
                }

                _previousStepSelections[changedIndex] = newValue;
            }
            finally
            {
                _isUpdatingPipelineCombos = false;
            }
        }

        private void ResetPipelineOrder()
        {
            _isUpdatingPipelineCombos = true;
            try
            {
                var runnableTabs = GetRunnableTabNames();
                for (int i = 0; i < _pipelineStepCombos.Count; i++)
                {
                    string target = i < runnableTabs.Count ? runnableTabs[i] : "(None)";
                    int idx = _pipelineStepCombos[i].FindStringExact(target);
                    _pipelineStepCombos[i].SelectedIndex = idx >= 0 ? idx : 0;
                    _previousStepSelections[i] = _pipelineStepCombos[i].SelectedItem?.ToString() ?? "(None)";
                }

                _lblPipelineStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblPipelineStatus.Text = "Execution order reset to default tab sequence.";
            }
            finally
            {
                _isUpdatingPipelineCombos = false;
            }
        }

        private void RunPipelineDraft()
        {
            var stepsToRun = new List<(int StepNumber, string TabName, RunnableTabInfo Info)>();
            for (int i = 0; i < _pipelineStepCombos.Count; i++)
            {
                string selected = _pipelineStepCombos[i].SelectedItem?.ToString() ?? "(None)";
                if (!string.Equals(selected, "(None)", StringComparison.OrdinalIgnoreCase) &&
                    _tabInfo.TryGetValue(selected, out var info))
                {
                    stepsToRun.Add((i + 1, selected, info));
                }
            }

            if (stepsToRun.Count == 0)
            {
                MessageBox.Show(this,
                    "All pipeline steps are currently set to (None). Select at least one tab to run.",
                    "Run Pipeline",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int existingCardCount =
                _deck.Pool.Count +
                _deck.Resources.Count +
                _deck.Hazards.Count +
                _deck.Sideboard.Count +
                _deck.Sites.Count;

            if (existingCardCount > 0)
            {
                var res = MessageBox.Show(this,
                    $"The current deck already contains cards:\n" +
                    $"• Pool: {_deck.Pool.Count}\n" +
                    $"• Resources: {_deck.Resources.Count}\n" +
                    $"• Hazards: {_deck.Hazards.Count}\n" +
                    $"• Sideboard: {_deck.Sideboard.Count}\n" +
                    $"• Sites: {_deck.Sites.Count}\n\n" +
                    "Proceeding will clear these deck lists before running Step 1.\n\nDo you want to proceed?",
                    "Clear Existing Deck & Run Pipeline",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes)
                {
                    return;
                }
            }

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
                        RefreshHavenDropdown();
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
                    // Step 1 itself failed on attempt 1 (tabs already run internal retries); stop immediately
                    break;
                }
            }

            // If at least one valid (but warning-bearing) run succeeded, restore the highest-scoring snapshot
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
                RefreshHavenDropdown();
                UpdateDeckSummary();

                _lblPipelineStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblPipelineStatus.Text =
                    $"Pipeline complete with warnings (best from attempt {bestAttempt}/{maxPipelineAttempts}): {string.Join(" -> ", bestCompletedSteps)}\n" +
                    $"⚠ See notice(s) on: {string.Join(", ", bestWarningTabs)}";
                return;
            }

            // No valid run succeeded at all: run one diagnostic pass with silent = false to display the hard-lock reason
            ResetAttemptState();
            NotifyDeckChanged(null);
            RefreshHavenDropdown();

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
            RefreshHavenDropdown();
            UpdateDeckSummary();

            _lblPipelineStatus.ForeColor = Color.FromArgb(26, 86, 54);
            _lblPipelineStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _lblPipelineStatus.Text = $"Pipeline complete: {string.Join(" -> ", finalCompleted)}";
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

        // Tab 1: Execution Pipeline Order
        public List<string> ExecutionOrder { get; set; } = [];

        // Tab 2: Starting Company
        public List<CharacterRowSettings> StarterRows { get; set; } = [];
        public int StarterHomeRegionPolicy { get; set; } = 0;

        // Tab 3: Non-Starting Characters & Avatars
        public string PrimaryWizard { get; set; } = "(None)";
        public int PrimaryWizardCopies { get; set; } = 1;
        public string SecondaryWizard { get; set; } = "(None)";
        public int SecondaryWizardCopies { get; set; } = 1;
        public string SecondaryWizardDestination { get; set; } = "Sideboard";

        public List<CharacterRowSettings> RecruitRows { get; set; } = [];
        public List<CharacterRowSettings> ReserveRows { get; set; } = [];
        public int HomeSitePolicy { get; set; } = 0;

        // Tab 4: Geographic Theater
        public int MovementMode { get; set; } = 0;
        public string HavenAnchor { get; set; } = "Rivendell";
        public int RegionCount { get; set; } = 4;
        public int SiteCount { get; set; } = 8;
        public List<string> SelectedRegionTypes { get; set; } = [];
        public List<string> SelectedSiteTypes { get; set; } = [];
        public int TheaterHomeSitePolicy { get; set; } = 0;
    }


}