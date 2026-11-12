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
        private readonly Action _notifyDeckChanged;

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
        private readonly ComboBox _cmbWizardHomePolicy;
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

        public DeckAssemblyMatrixDialog(
            Deck deck,
            CardCatalogService catalog,
            CardFilterService filterService,
            Func<ISet<string>> getActiveSets,
            Action notifyDeckChanged)
        {
            _deck = deck ?? throw new ArgumentNullException(nameof(deck));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _getActiveSets = getActiveSets ?? (() => new HashSet<string>());
            _notifyDeckChanged = notifyDeckChanged ?? (() => { });

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
                Text = "Target Deck Sizing & Workbench Profiles",
                Location = new Point(16, 16),
                Size = new Size(810, 185)
            };

            var lblTitle = new Label { Text = "Deck Title:", Location = new Point(16, 28), Size = new Size(110, 20) };
            _txtDeckTitle = new TextBox
            {
                Text = _deck.Title,
                Location = new Point(130, 25),
                Size = new Size(400, 23)
            };
            _txtDeckTitle.TextChanged += (s, e) =>
            {
                _deck.Title = _txtDeckTitle.Text;
                _notifyDeckChanged();
            };

            var lblPlaySize = new Label { Text = "Play Deck Size:", Location = new Point(16, 64), Size = new Size(110, 20) };
            _numPlayDeckSize = new NumericUpDown
            {
                Location = new Point(130, 61),
                Size = new Size(60, 23),
                Minimum = 20,
                Maximum = 60,
                Value = 30
            };
            _numPlayDeckSize.ValueChanged += (s, e) => UpdateDeckSummary();

            var lblPlaySizeHint = new Label
            {
                Text = "cards in Resources and equal cards in Hazards (Official tournament standard is 30/30).",
                Location = new Point(200, 64),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            var lblSideSize = new Label { Text = "Sideboard Size:", Location = new Point(16, 100), Size = new Size(110, 20) };
            _numSideboardSize = new NumericUpDown
            {
                Location = new Point(130, 97),
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
                Location = new Point(200, 100),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            var lblProfiles = new Label
            {
                Text = "Profile Settings:",
                Location = new Point(16, 138),
                Size = new Size(110, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            var btnSaveSettings = new Button
            {
                Text = "Save Settings...",
                Location = new Point(130, 135),
                Size = new Size(120, 26)
            };
            btnSaveSettings.Click += (s, e) => SaveSettingsToFile();

            var btnLoadSettings = new Button
            {
                Text = "Load Settings...",
                Location = new Point(258, 135),
                Size = new Size(120, 26)
            };
            btnLoadSettings.Click += (s, e) => LoadSettingsFromFile();

            var lblProfileHint = new Label
            {
                Text = "Save or load modular drafting profiles (.workbench.json) to quickly reuse company/theater configurations.",
                Location = new Point(390, 139),
                Size = new Size(410, 36),
                ForeColor = Color.DimGray
            };

            grpGeneral.Controls.AddRange([
                lblTitle, _txtDeckTitle,
                lblPlaySize, _numPlayDeckSize, lblPlaySizeHint,
                lblSideSize, _numSideboardSize, lblSideSizeHint,
                lblProfiles, btnSaveSettings, btnLoadSettings, lblProfileHint
            ]);

            var grpSummary = new GroupBox
            {
                Text = "Live Deck Composition Status",
                Location = new Point(16, 212),
                Size = new Size(810, 380)
            };

            _lblDeckSummary = new Label
            {
                Location = new Point(20, 30),
                Size = new Size(770, 335),
                Font = new Font("Segoe UI", 10.0f),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            grpSummary.Controls.Add(_lblDeckSummary);

            tabGeneral.Controls.AddRange([grpGeneral, grpSummary]);

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
                _notifyDeckChanged();
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
                _notifyDeckChanged();
                RefreshHavenDropdown();
                _lblStarterStatus.Text = $"Restored from Snapshot ({_savedPool.Count} cards).";
            };

            _lblStarterStatus = new Label
            {
                Location = new Point(16, 268),
                Size = new Size(790, 50),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic)
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
                Size = new Size(810, 112)
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

            // Row 3: Wizard Home Site Policy
            var lblWizHomePolicy = new Label { Text = "Wizard Home Site Policy:", Location = new Point(12, 81), Size = new Size(150, 18) };
            _cmbWizardHomePolicy = new ComboBox
            {
                Location = new Point(170, 78),
                Size = new Size(260, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbWizardHomePolicy.Items.AddRange([
                "None (Rivendell / Havens only)",
                "Include Wizard Home Site(s)"
            ]);
            _cmbWizardHomePolicy.SelectedIndex = 0;

            _cmbWizard1.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard1Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Dest.SelectedIndexChanged += WizardSelection_Changed;

            grpWizards.Controls.AddRange([
                lblWiz1, _cmbWizard1, lblWiz1Cop, _cmbWizard1Copies,
                lblWiz2, _cmbWizard2, lblWiz2Cop, _cmbWizard2Copies,
                lblWiz2Dest, _cmbWizard2Dest,
                lblWizHomePolicy, _cmbWizardHomePolicy
            ]);
            tabRecruits.Controls.Add(grpWizards);

            var lblRecruitsHdr = new Label
            {
                Text = "Play Deck Companions (Resources - Up to 10)",
                Location = new Point(16, 126),
                Size = new Size(400, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblRecruitsHdr);

            // 10 Companion Rows for Resources (Option A: starts Y: 166, headers at Y: 146)
            BuildCharacterGrid(tabRecruits, _recruitRows, 10, 166, 24, ValidateRecruitTabBudget);

            var lblReservesHdr = new Label
            {
                Text = "Sideboard Contingency Characters (Sideboard - Up to 3)",
                Location = new Point(16, 412),
                Size = new Size(450, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblReservesHdr);

            // 3 Reserve Rows for Sideboard (Option A: starts Y: 452, headers at Y: 432)
            BuildCharacterGrid(tabRecruits, _reserveRows, 3, 452, 24, ValidateRecruitTabBudget);

            var lblHomePolicy = new Label
            {
                Text = "Home Site Policy:",
                Location = new Point(16, 530),
                Size = new Size(110, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            _cmbHomeSitePolicy = new ComboBox
            {
                Location = new Point(130, 527),
                Size = new Size(450, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHomeSitePolicy.Items.AddRange([
                "Best Effort (Fit Resources, then Sideboard if budget permits)",
                "Strict (Require all Resource home sites; abort on shortfall)",
                "Havens Only (Do not force home sites)"
            ]);
            _cmbHomeSitePolicy.SelectedIndex = 0;

            _btnRunRecruits = new Button
            {
                Text = "Run",
                Location = new Point(16, 556),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunRecruits.Click += (s, e) => RunNonStartingCharactersDraft();

            var btnClearRecruits = new Button
            {
                Text = "Clear",
                Location = new Point(112, 556),
                Size = new Size(80, 28)
            };
            btnClearRecruits.Click += (s, e) =>
            {
                _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                _notifyDeckChanged();
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = "Non-starting characters cleared from Resources and Sideboard.";
            };

            _btnSaveRecruits = new Button
            {
                Text = "Save",
                Location = new Point(198, 556),
                Size = new Size(80, 28)
            };
            _btnSaveRecruits.Click += (s, e) =>
            {
                _savedNonStarters = [.. _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))];
                _savedSideboardCharacters = [.. _deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))];
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = $"Session Snapshot Saved ({_savedNonStarters.Count} in Resources, {_savedSideboardCharacters.Count} in Sideboard).";
            };

            var btnRecallRecruits = new Button
            {
                Text = "Recall",
                Location = new Point(284, 556),
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
                _notifyDeckChanged();
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = $"Restored from Snapshot ({_savedNonStarters?.Count ?? 0} in Resources, {_savedSideboardCharacters?.Count ?? 0} in Sideboard).";
            };

            _lblRecruitStatus = new Label
            {
                Location = new Point(16, 586),
                Size = new Size(810, 54),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
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
                Text = "Movement Mode & Havens",
                Location = new Point(16, 16),
                Size = new Size(385, 230)
            };

            var lblMode = new Label { Text = "Movement Mode:", Location = new Point(12, 28), Size = new Size(110, 20) };
            _cmbMode = new ComboBox
            {
                Location = new Point(130, 25),
                Size = new Size(235, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbMode.Items.AddRange(["Starter + Region (Roaming)", "Starter Only (Haven Hub)"]);
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += (s, e) =>
            {
                bool isStarterOnly = _cmbMode.SelectedIndex == 1;
                _numRegionCount.Enabled = !isStarterOnly;
                if (isStarterOnly) _numRegionCount.Value = 0;
                else if (_numRegionCount.Value == 0) _numRegionCount.Value = 4;
            };

            var lblHaven = new Label { Text = "Haven Anchor:", Location = new Point(12, 65), Size = new Size(110, 20) };
            _cmbHaven = new ComboBox
            {
                Location = new Point(130, 62),
                Size = new Size(235, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblRegCount = new Label { Text = "Region Count:", Location = new Point(12, 102), Size = new Size(110, 20) };
            _numRegionCount = new NumericUpDown
            {
                Location = new Point(130, 100),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 12,
                Value = 4
            };

            var lblSiteCount = new Label { Text = "Site Count:", Location = new Point(12, 138), Size = new Size(110, 20) };
            _numSiteCount = new NumericUpDown
            {
                Location = new Point(130, 136),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 30,
                Value = 8
            };

            grpTheaterOpts.Controls.AddRange([
                lblMode, _cmbMode,
                lblHaven, _cmbHaven,
                lblRegCount, _numRegionCount,
                lblSiteCount, _numSiteCount
            ]);

            var grpFilters = new GroupBox
            {
                Text = "Terrain & Hold Classifications",
                Location = new Point(415, 16),
                Size = new Size(390, 230)
            };

            var lblRegTypes = new Label { Text = "Region Terrains (None = Any):", Location = new Point(12, 22), Size = new Size(170, 16) };
            _clbRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 40),
                Size = new Size(170, 175),
                CheckOnClick = true
            };
            _clbRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);

            var lblSiteTypes = new Label { Text = "Site Holds (None = Any):", Location = new Point(195, 22), Size = new Size(170, 16) };
            _clbSiteTypes = new CheckedListBox
            {
                Location = new Point(195, 40),
                Size = new Size(170, 175),
                CheckOnClick = true
            };
            _clbSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Haven"]);

            grpFilters.Controls.AddRange([lblRegTypes, _clbRegionTypes, lblSiteTypes, _clbSiteTypes]);

            var btnRunTheater = new Button
            {
                Text = "Run",
                Location = new Point(16, 260),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnRunTheater.Click += (s, e) => RunGeographicTheaterDraft();

            var btnClearTheater = new Button
            {
                Text = "Clear",
                Location = new Point(112, 260),
                Size = new Size(80, 28)
            };
            btnClearTheater.Click += (s, e) =>
            {
                _deck.Sites.Clear();
                _notifyDeckChanged();
                _lblTheaterStatus.Text = "Site deck cleared.";
            };

            var btnSaveTheater = new Button
            {
                Text = "Save",
                Location = new Point(198, 260),
                Size = new Size(80, 28)
            };
            btnSaveTheater.Click += (s, e) =>
            {
                _savedSites = [.. _deck.Sites];
                _lblTheaterStatus.Text = $"Session Snapshot Saved ({_savedSites.Count} cards).";
            };

            var btnRecallTheater = new Button
            {
                Text = "Recall",
                Location = new Point(284, 260),
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
                _notifyDeckChanged();
                _lblTheaterStatus.Text = $"Restored from Snapshot ({_savedSites.Count} cards).";
            };

            _lblTheaterStatus = new Label
            {
                Location = new Point(16, 300),
                Size = new Size(790, 50),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic)
            };

            tabTheater.Controls.AddRange([
                grpTheaterOpts, grpFilters,
                btnRunTheater, btnClearTheater, btnSaveTheater, btnRecallTheater,
                _lblTheaterStatus
            ]);

            _tabControl.TabPages.AddRange([tabGeneral, tabStarter, tabRecruits, tabTheater]);
            Controls.Add(_tabControl);

            RefreshHavenDropdown();
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
            bool hasPool = _deck.Pool.Count > 0;

            _cmbHaven.BeginUpdate();
            _cmbHaven.Items.Clear();

            if (hasPool)
            {
                _cmbHaven.Items.Add("Starting Company (Majority Vote)");
            }

            _cmbHaven.Items.Add("Nearest Haven (to Theater)");
            _cmbHaven.Items.AddRange(["Rivendell", "Lórien", "Grey Havens", "Edhellond", "Random"]);

            int idx = _cmbHaven.FindStringExact(currentSelection);
            _cmbHaven.SelectedIndex = idx >= 0 ? idx : 0;
            _cmbHaven.EndUpdate();
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
                ("Edhellond", "Belfalas")
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
                WizardHomeSitePolicy = _cmbWizardHomePolicy.SelectedIndex,

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
                SelectedSiteTypes = _clbSiteTypes.CheckedItems.Cast<string>().ToList()
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
            if (config.WizardHomeSitePolicy >= 0 && config.WizardHomeSitePolicy < _cmbWizardHomePolicy.Items.Count)
            {
                _cmbWizardHomePolicy.SelectedIndex = config.WizardHomeSitePolicy;
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

        private void RunStartingCompanyDraft()
        {
            var activeRows = _starterRows.Where(r => r.IsActive).ToList();
            if (activeRows.Count == 0)
            {
                MessageBox.Show(this, "No starter rows are filled in. Please select criteria for at least 1 companion.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var theaterRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int starterPolicy = _cmbStarterHomeRegionPolicy.SelectedIndex; // 0 = Best Effort, 1 = Strict, 2 = Open / Anywhere

            if (starterPolicy != 2)
            {
                foreach (var c in _deck.Sites)
                {
                    if (string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase))
                    {
                        theaterRegions.Add(c.Name.Trim());
                    }
                    else if (!string.IsNullOrWhiteSpace(c.Region))
                    {
                        theaterRegions.Add(c.Region.Trim());
                    }
                }
            }

            if (starterPolicy == 1 && theaterRegions.Count == 0)
            {
                MessageBox.Show(this,
                    "Strict Home Region Policy requires active Theater regions in the Site deck.\n\nPlease configure and run Geographic Theater first, or set Home Region Policy to Best Effort / Open.",
                    "Strict Home Region Policy",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var candidatesPerRow = new List<List<Card>>();
            for (int i = 0; i < activeRows.Count; i++)
            {
                var row = activeRows[i];
                var candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, starterPolicy != 2 ? theaterRegions : null);

                // Best Effort fallback: if no candidate lived in the theater, fall back to open recruitment for this row
                if (candidates.Count == 0 && starterPolicy == 0 && theaterRegions.Count > 0)
                {
                    candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, null);
                }

                if (candidates.Count == 0)
                {
                    string policyNote = (starterPolicy == 1) ? " (Strict Home Region Policy applied)" : "";
                    MessageBox.Show(this, $"No matching cards found for Starter Row {i + 1} with the selected criteria{policyNote} in your active expansions.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                candidatesPerRow.Add(candidates);
            }

            int minPossibleMindSum = 0;
            foreach (var list in candidatesPerRow)
            {
                minPossibleMindSum += list.Min(c => ParseCardMind(c.Mind));
            }

            if (minPossibleMindSum > 20)
            {
                MessageBox.Show(this,
                    $"Cannot draft starting company: The minimum possible Mind sum for your selected rows is {minPossibleMindSum}, which exceeds the official 20-Mind starting company limit.\n\nPlease lower your Mind thresholds or clear one of the rows.",
                    "Starting Company (20-Mind Limit)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            List<Card> bestCompany = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var picked = new List<Card>();
                var usedTitles = new HashSet<string>(
                    _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)
                    .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)),
                    StringComparer.OrdinalIgnoreCase);

                bool validPermutation = true;

                foreach (var candidates in candidatesPerRow)
                {
                    var eligible = candidates.Where(c => !usedTitles.Contains(c.Name)).ToList();
                    if (eligible.Count == 0)
                    {
                        validPermutation = false;
                        break;
                    }

                    var chosen = eligible[Random.Shared.Next(eligible.Count)];
                    picked.Add(chosen);
                    usedTitles.Add(chosen.Name);
                }

                if (validPermutation)
                {
                    int totalMind = picked.Sum(c => ParseCardMind(c.Mind));
                    if (totalMind <= 20)
                    {
                        bestCompany = picked;
                        break;
                    }
                }
            }

            if (bestCompany == null)
            {
                MessageBox.Show(this,
                    "Unable to find a valid starting company combination totaling 20 Mind or less with current criteria (after 100 attempts).\n\nTry broadening skills, allowing lower-mind companions (Mind 1–3), or clearing a row.",
                    "Starting Company",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _deck.Pool.Clear();
            _deck.Pool.AddRange(bestCompany);
            _deck.SortSections();
            _notifyDeckChanged();

            RefreshHavenDropdown();

            int finalMind = bestCompany.Sum(c => ParseCardMind(c.Mind));
            string companyNames = string.Join(", ", bestCompany.Select(c => $"{c.Name} ({c.Mind} Mind)"));
            _lblStarterStatus.Text = $"Drafted {bestCompany.Count} starting characters (Total Mind: {finalMind}/20):\n{companyNames}";
        }

        private void RunNonStartingCharactersDraft()
        {
            int wiz1Count = GetSelectedWizard1Count();
            int wiz2Count = GetSelectedWizard2Count();
            var activeRecruitRows = _recruitRows.Where(r => r.IsActive).ToList();
            var activeReserveRows = _reserveRows.Where(r => r.IsActive).ToList();

            if (wiz1Count == 0 && wiz2Count == 0 && activeRecruitRows.Count == 0 && activeReserveRows.Count == 0)
            {
                MessageBox.Show(this, "No Wizards, Resource companions, or Sideboard reserves are configured. Please select at least 1 character.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (activeRecruitRows.Count > 10)
            {
                ValidateRecruitTabBudget();
                return;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;
            var pickedRecruits = new List<Card>();
            var pickedReserves = new List<Card>();

            // 1. Draft Selected Wizards (Primary into Resources; Secondary into Sideboard or Resources)
            var allWizards = allCards.Where(c =>
                string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) &&
                (activeSets.Count == 0 || activeSets.Contains(c.Set))).ToList();

            string chosenPrimaryWizardName = null;

            if (wiz1Count > 0)
            {
                string wiz1Choice = _cmbWizard1.SelectedItem.ToString();
                Card wiz1Card = null;

                if (string.Equals(wiz1Choice, "Random", StringComparison.OrdinalIgnoreCase))
                {
                    if (allWizards.Count > 0)
                    {
                        wiz1Card = allWizards[Random.Shared.Next(allWizards.Count)];
                    }
                }
                else
                {
                    wiz1Card = allWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz1Choice));
                }

                if (wiz1Card == null)
                {
                    MessageBox.Show(this, $"Could not find Primary Wizard '{wiz1Choice}' in your active expansion sets.", "Wizard Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                chosenPrimaryWizardName = wiz1Card.Name;
                for (int i = 0; i < wiz1Count; i++)
                {
                    pickedRecruits.Add(wiz1Card);
                }
            }

            if (wiz2Count > 0)
            {
                string wiz2Choice = _cmbWizard2.SelectedItem.ToString();
                Card wiz2Card = null;

                var remainingWizards = allWizards
                    .Where(w => !string.Equals(w.Name, chosenPrimaryWizardName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (string.Equals(wiz2Choice, "Random", StringComparison.OrdinalIgnoreCase))
                {
                    if (remainingWizards.Count > 0)
                    {
                        wiz2Card = remainingWizards[Random.Shared.Next(remainingWizards.Count)];
                    }
                }
                else
                {
                    wiz2Card = remainingWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz2Choice));
                }

                if (wiz2Card == null)
                {
                    MessageBox.Show(this, $"Could not find distinct Secondary Wizard '{wiz2Choice}' in your active expansion sets.", "Wizard Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool toSideboard = IsWizard2InSideboard();
                for (int i = 0; i < wiz2Count; i++)
                {
                    if (toSideboard)
                    {
                        pickedReserves.Add(wiz2Card);
                    }
                    else
                    {
                        pickedRecruits.Add(wiz2Card);
                    }
                }
            }

            // 2. Draft Non-Wizard Companions into Resources
            int homeSitePolicy = _cmbHomeSitePolicy.SelectedIndex; // 0 = Best Effort, 1 = Strict, 2 = Havens Only
            var currentSites = _deck.Sites.Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase)).ToList();
            bool hasTheaterSites = currentSites.Count > 0;
            var currentSiteNames = new HashSet<string>(currentSites.Select(s => s.Name.Trim()), StringComparer.OrdinalIgnoreCase);

            var usedTitles = new HashSet<string>(_deck.Pool.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var w in pickedRecruits.Concat(pickedReserves))
            {
                if (string.Equals(w.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    usedTitles.Add(w.Name);
                }
            }

            for (int i = 0; i < activeRecruitRows.Count; i++)
            {
                var row = activeRecruitRows[i];
                var candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, null);

                if (hasTheaterSites && homeSitePolicy != 2) // If not "Havens Only"
                {
                    var constrained = candidates.Where(c =>
                    {
                        if (string.IsNullOrWhiteSpace(c.Home)) return false;
                        string cleanHome = c.Home.Trim();
                        if (currentSiteNames.Contains(cleanHome)) return true;
                        return currentSites.Any(s =>
                            CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                            string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                            CardFilterService.MatchesPredicate(s, cleanHome, _filterService));
                    }).ToList();

                    if (constrained.Count > 0)
                    {
                        candidates = constrained;
                    }
                    else if (homeSitePolicy == 1) // Strict mode
                    {
                        MessageBox.Show(this,
                            $"Strict Home Site Policy: No matching candidate found for Recruit Row {i + 1} whose home site is currently in the active Site deck.\n\nBroaden row criteria, add the site to the Site deck, or set Home Site Policy to Best Effort.",
                            "Strict Home Site Shortfall",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

                var eligible = candidates.Where(c => !usedTitles.Contains(c.Name)).ToList();

                if (eligible.Count == 0)
                {
                    string hint = (hasTheaterSites && homeSitePolicy != 2) ? " (constrained by current Theater sites)" : "";
                    MessageBox.Show(this, $"No matching cards found for Recruit Row {i + 1}{hint}. Clear or adjust Theater sites, or change row criteria.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var chosen = eligible[Random.Shared.Next(eligible.Count)];
                pickedRecruits.Add(chosen);
                if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    usedTitles.Add(chosen.Name);
                }
            }

            // 3. Draft Sideboard Reserve Characters into Sideboard
            for (int i = 0; i < activeReserveRows.Count; i++)
            {
                var row = activeReserveRows[i];
                var candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, null);

                var eligible = candidates.Where(c => !usedTitles.Contains(c.Name)).ToList();

                if (eligible.Count == 0)
                {
                    MessageBox.Show(this, $"No matching cards found for Sideboard Reserve Row {i + 1}. Try broadening your criteria.", "Sideboard Reserve", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var chosen = eligible[Random.Shared.Next(eligible.Count)];
                pickedReserves.Add(chosen);
                if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    usedTitles.Add(chosen.Name);
                }
            }

            // 4. Update Deck partitions
            _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Resources.AddRange(pickedRecruits);

            _deck.Sideboard.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Sideboard.AddRange(pickedReserves);

            _deck.SortSections();
            _notifyDeckChanged();

            // 5. Update Status
            string recruitNames = pickedRecruits.Count > 0 ? string.Join(", ", pickedRecruits.Select(c => c.Name)) : "(None)";
            string reserveNames = pickedReserves.Count > 0 ? string.Join(", ", pickedReserves.Select(c => c.Name)) : "(None)";

            _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblRecruitStatus.Text =
                $"Drafted {pickedRecruits.Count} into Resources and {pickedReserves.Count} into Sideboard:\n" +
                $"• Resources: {recruitNames}\n" +
                $"• Sideboard: {reserveNames}";
        }

        private void RunGeographicTheaterDraft()
        {
            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            bool isStarterPlusRegion = _cmbMode.SelectedIndex == 0;
            int maxRegions = isStarterPlusRegion ? (int)_numRegionCount.Value : 0;
            int siteCount = (int)_numSiteCount.Value;

            int homeSitePolicy = _cmbHomeSitePolicy.SelectedIndex; // 0 = Best Effort, 1 = Strict, 2 = Havens Only
            bool includeWizardHomes = _cmbWizardHomePolicy.SelectedIndex == 1; // 0 = None, 1 = Include Wizard Home Site(s)

            // 1. Gather Candidate Characters for Home Sites
            var resourceNonStarters = _deck.Resources
                .Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c))
                .ToList();

            var sideboardNonStarters = _deck.Sideboard
                .Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c))
                .ToList();

            var tier1CandidateEntries = new List<(string Owner, Card SiteCard)>(); // Resources + Wizards (Priority)
            var tier2CandidateEntries = new List<(string Owner, Card SiteCard)>(); // Sideboard (Opportunistic)

            if (homeSitePolicy != 2) // Not "Havens Only"
            {
                // A. Resource Non-Starter Companions
                foreach (var charCard in resourceNonStarters)
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
                        tier1CandidateEntries.Add((charCard.Name, homeMatch));
                    }
                }

                // B. Wizard Land Home Sites (Strictly inspect wizards actually in the deck!)
                if (includeWizardHomes)
                {
                    var wizards = _deck.Resources.Concat(_deck.Sideboard).Where(IsAvatar).Select(w => w.Name).ToList();

                    foreach (var wiz in wizards)
                    {
                        if (CardFilterService.EqualsWithDiacriticFallback(wiz, "Saruman"))
                        {
                            var isen = allCards.FirstOrDefault(s => string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(s.Name, "Isengard"));
                            if (isen != null && !tier1CandidateEntries.Any(e => string.Equals(e.SiteCard.Name, isen.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                tier1CandidateEntries.Add(("Saruman (Wizard)", isen));
                            }
                        }
                        else if (CardFilterService.EqualsWithDiacriticFallback(wiz, "Radagast"))
                        {
                            var rhos = allCards.FirstOrDefault(s => string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(s.Name, "Rhosgobel"));
                            if (rhos != null && !tier1CandidateEntries.Any(e => string.Equals(e.SiteCard.Name, rhos.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                tier1CandidateEntries.Add(("Radagast (Wizard)", rhos));
                            }
                        }
                    }
                }

                // C. Sideboard Non-Starters (Tier 2 Opportunistic)
                if (homeSitePolicy == 0) // Best Effort only
                {
                    foreach (var charCard in sideboardNonStarters)
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
                            tier2CandidateEntries.Add(($"{charCard.Name} (Sideboard)", homeMatch));
                        }
                    }
                }
            }

            // 2. Evaluate Candidate Home Sites Under the Selected Policy
            var acceptedHomeSites = new List<Card>();
            var acceptedHomeRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shortfallList = new List<(string Owner, string SiteName, string Region)>();

            if (homeSitePolicy == 1) // Strict mode (enforces Tier 1 only)
            {
                var distinctCandidateSites = tier1CandidateEntries
                    .Select(e => e.SiteCard)
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                int requiredAdventureCount = distinctCandidateSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));
                var candidateRegions = distinctCandidateSites.Where(s => !string.IsNullOrWhiteSpace(s.Region)).Select(s => s.Region.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                bool siteDeficit = requiredAdventureCount > siteCount;
                bool regionDeficit = isStarterPlusRegion && candidateRegions.Count > maxRegions;

                if (siteDeficit || regionDeficit)
                {
                    var siteBulletList = string.Join("\n", distinctCandidateSites.Select(s => $"  • {s.Name} ({s.Region})"));

                    string siteMsg = siteDeficit
                        ? $"• Site Count needed: at least {requiredAdventureCount} (current setting: {siteCount})\n"
                        : "";
                    string regMsg = regionDeficit
                        ? $"• Region Count needed: at least {candidateRegions.Count} (current setting: {maxRegions})\n"
                        : "";

                    MessageBox.Show(this,
                        $"Cannot assemble Geographic Theater (Strict Home Site Policy):\n\n" +
                        $"Your selected characters require home sites that exceed your current Theater settings:\n" +
                        $"{siteBulletList}\n\n" +
                        $"Budget shortfalls:\n" +
                        $"{siteMsg}{regMsg}\n" +
                        $"No changes were made to your Site deck.\n\n" +
                        $"To proceed, increase Site/Region Count, change Home Site Policy to Best Effort, or re-run/adjust characters.",
                        "Geographic Theater Budget Shortfall",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    _lblTheaterStatus.ForeColor = Color.Firebrick;
                    _lblTheaterStatus.Text = "Draft aborted: Insufficient Site or Region budget for character home sites (Strict policy).";
                    return;
                }

                foreach (var (owner, siteCard) in tier1CandidateEntries)
                {
                    if (!acceptedHomeSites.Any(s => string.Equals(s.Name, siteCard.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        acceptedHomeSites.Add(siteCard);
                        if (!string.IsNullOrWhiteSpace(siteCard.Region))
                        {
                            acceptedHomeRegions.Add(siteCard.Region.Trim());
                        }
                    }
                }
            }
            else if (homeSitePolicy == 0) // Best Effort mode (processes Tier 1, then Tier 2 if capacity allows)
            {
                var combinedEntries = tier1CandidateEntries.Concat(tier2CandidateEntries).ToList();

                foreach (var (owner, siteCard) in combinedEntries)
                {
                    if (acceptedHomeSites.Any(s => string.Equals(s.Name, siteCard.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    bool isAdvSite = !string.Equals(siteCard.Site, "Haven", StringComparison.OrdinalIgnoreCase);
                    int currentAdvCount = acceptedHomeSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));

                    bool siteFits = !isAdvSite || (currentAdvCount < siteCount);
                    bool regionFits = true;
                    if (isStarterPlusRegion && !string.IsNullOrWhiteSpace(siteCard.Region))
                    {
                        string reg = siteCard.Region.Trim();
                        if (!acceptedHomeRegions.Contains(reg))
                        {
                            regionFits = (acceptedHomeRegions.Count + 1) <= maxRegions;
                        }
                    }

                    if (siteFits && regionFits)
                    {
                        acceptedHomeSites.Add(siteCard);
                        if (!string.IsNullOrWhiteSpace(siteCard.Region))
                        {
                            acceptedHomeRegions.Add(siteCard.Region.Trim());
                        }
                    }
                    else
                    {
                        shortfallList.Add((owner, siteCard.Name, siteCard.Region ?? "Unknown"));
                    }
                }
            }

            // 3. Resolve Operational Cluster Seed & Graph
            var graph = _filterService.BuildRegionAdjacencyGraph(allCards);
            string rawHaven = _cmbHaven.SelectedItem?.ToString() ?? "Rivendell";

            var checkedTerrains = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            string terrainFilter = checkedTerrains.Count > 0 ? string.Join("; ", checkedTerrains) : null;

            var checkedHolds = _clbSiteTypes.CheckedItems.Cast<string>().ToList();
            string holdFilter = checkedHolds.Count > 0 ? string.Join("; ", checkedHolds) : null;

            string seedRegion = "Rhudaur";
            if (acceptedHomeRegions.Count > 0)
            {
                seedRegion = acceptedHomeRegions.First();
            }
            else if (rawHaven.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase) ||
                     rawHaven.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                // Decoupled Expeditionary: pick a random seed region across Middle-earth (filtering by terrain if checked)
                var candidateSeeds = allCards
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                                (activeSets.Count == 0 || activeSets.Contains(c.Set)))
                    .ToList();

                if (!string.IsNullOrWhiteSpace(terrainFilter))
                {
                    var filtered = candidateSeeds.Where(c =>
                        DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                        .ToList();

                    if (filtered.Count > 0) candidateSeeds = filtered;
                }

                if (candidateSeeds.Count > 0)
                {
                    seedRegion = candidateSeeds[Random.Shared.Next(candidateSeeds.Count)].Name.Trim();
                }
            }
            else if (!rawHaven.StartsWith("Starting Company", StringComparison.OrdinalIgnoreCase))
            {
                // Specific Haven chosen (Rivendell, Lórien, Grey Havens, Edhellond)
                var hCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, rawHaven));
                if (hCard != null && !string.IsNullOrWhiteSpace(hCard.Region))
                {
                    seedRegion = hCard.Region.Trim();
                }
            }

            // 4. Expand Contiguous Regional Cluster
            var activeRegions = new List<string>();
            if (isStarterPlusRegion)
            {
                var seedRegions = new List<string> { seedRegion };
                foreach (var hr in acceptedHomeRegions)
                {
                    if (!seedRegions.Contains(hr, StringComparer.OrdinalIgnoreCase))
                    {
                        seedRegions.Add(hr);
                    }
                }

                foreach (var sr in seedRegions)
                {
                    if (activeRegions.Count < maxRegions && !activeRegions.Contains(sr, StringComparer.OrdinalIgnoreCase))
                    {
                        activeRegions.Add(sr);
                    }
                }

                if (activeRegions.Count < maxRegions)
                {
                    var cluster = DeckGenerationService.ExpandContiguousRegionCluster(graph, seedRegion, maxRegions);
                    foreach (var cr in cluster)
                    {
                        if (activeRegions.Count >= maxRegions) break;
                        if (!activeRegions.Contains(cr, StringComparer.OrdinalIgnoreCase))
                        {
                            activeRegions.Add(cr);
                        }
                    }
                }
            }

            // 5. Resolve Final Haven Anchor
            string selectedHaven = "Rivendell";

            if (rawHaven.StartsWith("Starting Company", StringComparison.OrdinalIgnoreCase))
            {
                var votes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var starter in _deck.Pool)
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
                    selectedHaven = top.Count == 1 ? top[0] : "Rivendell"; // Plurality wins; tie falls back to Rivendell
                }
            }
            else if (rawHaven.StartsWith("Nearest Haven", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = FindNearestHavenToCluster(graph, activeRegions);
            }
            else if (rawHaven.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = HeroHavens[Random.Shared.Next(HeroHavens.Length)];
            }
            else
            {
                selectedHaven = rawHaven;
            }

            var havenCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, selectedHaven));

            // 6. Assemble Sites & Regions (Overland BFS chain dropped: Region Count strictly respected!)
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

            _deck.Sites.Clear();
            _deck.Sites.AddRange(draftedSitesAndRegions);
            _deck.SortSections();
            _notifyDeckChanged();

            int finalRegions = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int finalSites = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase));

            string statusMsg = $"Drafted {draftedSitesAndRegions.Count} cards ({finalRegions} Regions, {finalSites} Sites). Haven: {selectedHaven}";

            if (shortfallList.Count > 0)
            {
                var omittedBrief = string.Join(", ", shortfallList.Select(s => $"{s.Owner} -> {s.SiteName}"));
                statusMsg += $"\n⚠ Omitted {shortfallList.Count} home site(s) due to budget (deploy at Rivendell): {omittedBrief}";
            }

            _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblTheaterStatus.Text = statusMsg;
        }

        #endregion

        #region HELPERS

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
                    if (homeRegionTags.Any(t => theaterRegions.Contains(t.Trim())))
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
                            if (theaterRegions.Contains(homeSite.Region.Trim()))
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

        // Tab 2: Starting Company
        public List<CharacterRowSettings> StarterRows { get; set; } = [];
        public int StarterHomeRegionPolicy { get; set; } = 0;

        // Tab 3: Non-Starting Characters & Avatars
        public string PrimaryWizard { get; set; } = "(None)";
        public int PrimaryWizardCopies { get; set; } = 1;
        public string SecondaryWizard { get; set; } = "(None)";
        public int SecondaryWizardCopies { get; set; } = 1;
        public string SecondaryWizardDestination { get; set; } = "Sideboard";
        public int WizardHomeSitePolicy { get; set; } = 0;

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
    }
}