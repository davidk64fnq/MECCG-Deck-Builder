using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class DeckAssemblyMatrixDialog : Form
    {
        private readonly Deck _deck;
        private readonly CardCatalogService _catalog;
        private readonly CardFilterService _filterService;
        private readonly Func<ISet<string>> _getActiveSets;
        private readonly Action _notifyDeckChanged;

        // Session memory snapshots
        private List<Card> _savedPool;
        private List<Card> _savedNonStarters;
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
        private readonly CheckBox _chkConstrainStarterToTheater;
        private readonly Label _lblStarterStatus;

        // --- TAB 3: NON-STARTING CHARACTERS & AVATARS CONTROLS ---
        private readonly ComboBox _cmbWizard1;
        private readonly ComboBox _cmbWizard1Copies;
        private readonly ComboBox _cmbWizard2;
        private readonly ComboBox _cmbWizard2Copies;
        private bool _isUpdatingWizardControls;

        private readonly List<CharacterRowControls> _recruitRows = [];
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
            ClientSize = new Size(820, 560);
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
                Text = "Target Deck Sizing & Nomenclature",
                Location = new Point(16, 16),
                Size = new Size(770, 150)
            };

            var lblTitle = new Label { Text = "Deck Title:", Location = new Point(16, 30), Size = new Size(110, 20) };
            _txtDeckTitle = new TextBox
            {
                Text = _deck.Title,
                Location = new Point(130, 27),
                Size = new Size(400, 23)
            };
            _txtDeckTitle.TextChanged += (s, e) =>
            {
                _deck.Title = _txtDeckTitle.Text;
                _notifyDeckChanged();
            };

            var lblPlaySize = new Label { Text = "Play Deck Size:", Location = new Point(16, 68), Size = new Size(110, 20) };
            _numPlayDeckSize = new NumericUpDown
            {
                Location = new Point(130, 65),
                Size = new Size(60, 23),
                Minimum = 20,
                Maximum = 60,
                Value = 30
            };
            _numPlayDeckSize.ValueChanged += (s, e) => UpdateDeckSummary();

            var lblPlaySizeHint = new Label
            {
                Text = "cards in Resources and equal cards in Hazards (Official tournament standard is 30/30).",
                Location = new Point(200, 68),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            var lblSideSize = new Label { Text = "Sideboard Size:", Location = new Point(16, 104), Size = new Size(110, 20) };
            _numSideboardSize = new NumericUpDown
            {
                Location = new Point(130, 101),
                Size = new Size(60, 23),
                Minimum = 0,
                Maximum = 30,
                Value = 20
            };
            _numSideboardSize.ValueChanged += (s, e) => UpdateDeckSummary();

            var lblSideSizeHint = new Label
            {
                Text = "cards in reserve (Official tournament standard is up to 20 or 25).",
                Location = new Point(200, 104),
                Size = new Size(540, 20),
                ForeColor = Color.DimGray
            };

            grpGeneral.Controls.AddRange([
                lblTitle, _txtDeckTitle,
                lblPlaySize, _numPlayDeckSize, lblPlaySizeHint,
                lblSideSize, _numSideboardSize, lblSideSizeHint
            ]);

            var grpSummary = new GroupBox
            {
                Text = "Live Deck Composition Status",
                Location = new Point(16, 180),
                Size = new Size(770, 310)
            };

            _lblDeckSummary = new Label
            {
                Location = new Point(20, 35),
                Size = new Size(730, 250),
                Font = new Font("Segoe UI", 10.0f),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            grpSummary.Controls.Add(_lblDeckSummary);

            tabGeneral.Controls.AddRange([grpGeneral, grpSummary]);

            // ==========================================
            // TAB 2: STARTING COMPANY (5 ROWS)
            // ==========================================
            var tabStarter = new TabPage("Starting Company (Pool)");
            BuildCharacterGrid(tabStarter, _starterRows, 5, "Starter", 32, 34, null);

            _chkConstrainStarterToTheater = new CheckBox
            {
                Text = "Prioritize / constrain starting companions to active Theater regions",
                Location = new Point(16, 215),
                Size = new Size(500, 22),
                Checked = false
            };

            var btnRunStarter = new Button
            {
                Text = "Run",
                Location = new Point(16, 248),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnRunStarter.Click += (s, e) => RunStartingCompanyDraft();

            var btnClearStarter = new Button
            {
                Text = "Clear",
                Location = new Point(112, 248),
                Size = new Size(80, 28)
            };
            btnClearStarter.Click += (s, e) =>
            {
                _deck.Pool.Clear();
                _notifyDeckChanged();
                _lblStarterStatus.Text = "Starting Pool cleared.";
            };

            var btnSaveStarter = new Button
            {
                Text = "Save",
                Location = new Point(198, 248),
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
                Location = new Point(284, 248),
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
                _lblStarterStatus.Text = $"Restored from Snapshot ({_savedPool.Count} cards).";
            };

            _lblStarterStatus = new Label
            {
                Location = new Point(16, 288),
                Size = new Size(760, 45),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic)
            };

            tabStarter.Controls.AddRange([
                _chkConstrainStarterToTheater,
                btnRunStarter, btnClearStarter, btnSaveStarter, btnRecallStarter,
                _lblStarterStatus
            ]);

            // ==========================================
            // TAB 3: NON-STARTING CHARACTERS (RESOURCES)
            // ==========================================
            var tabRecruits = new TabPage("Non-Starting Characters (Resources)");

            var grpWizards = new GroupBox
            {
                Text = "Avatar / Wizard Selection (Max 3 copies total; 3× of one or 2+1 split)",
                Location = new Point(16, 8),
                Size = new Size(770, 52)
            };

            var lblWiz1 = new Label { Text = "Primary Wizard:", Location = new Point(12, 23), Size = new Size(95, 18) };
            _cmbWizard1 = new ComboBox
            {
                Location = new Point(108, 20),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbWizard1.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard1.SelectedIndex = 0;

            var lblWiz1Cop = new Label { Text = "Copies:", Location = new Point(250, 23), Size = new Size(48, 18) };
            _cmbWizard1Copies = new ComboBox
            {
                Location = new Point(300, 20),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard1Copies.Items.AddRange(["1", "2", "3"]);
            _cmbWizard1Copies.SelectedIndex = 0;

            var lblWiz2 = new Label { Text = "Secondary Wizard:", Location = new Point(385, 23), Size = new Size(110, 18) };
            _cmbWizard2 = new ComboBox
            {
                Location = new Point(498, 20),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard2.SelectedIndex = 0;

            var lblWiz2Cop = new Label { Text = "Copies:", Location = new Point(640, 23), Size = new Size(48, 18) };
            _cmbWizard2Copies = new ComboBox
            {
                Location = new Point(690, 20),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Copies.Items.AddRange(["1", "2"]);
            _cmbWizard2Copies.SelectedIndex = 0;

            _cmbWizard1.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard1Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Copies.SelectedIndexChanged += WizardSelection_Changed;

            grpWizards.Controls.AddRange([
                lblWiz1, _cmbWizard1, lblWiz1Cop, _cmbWizard1Copies,
                lblWiz2, _cmbWizard2, lblWiz2Cop, _cmbWizard2Copies
            ]);
            tabRecruits.Controls.Add(grpWizards);

            // 10 Companion Rows below Wizard box
            BuildCharacterGrid(tabRecruits, _recruitRows, 10, "Recruit", 86, 32, ValidateRecruitTabBudget);

            _btnRunRecruits = new Button
            {
                Text = "Run",
                Location = new Point(16, 412),
                Size = new Size(90, 28),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnRunRecruits.Click += (s, e) => RunNonStartingCharactersDraft();

            var btnClearRecruits = new Button
            {
                Text = "Clear",
                Location = new Point(112, 412),
                Size = new Size(80, 28)
            };
            btnClearRecruits.Click += (s, e) =>
            {
                _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                _notifyDeckChanged();
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = "Non-starting characters cleared from Resources.";
            };

            _btnSaveRecruits = new Button
            {
                Text = "Save",
                Location = new Point(198, 412),
                Size = new Size(80, 28)
            };
            _btnSaveRecruits.Click += (s, e) =>
            {
                _savedNonStarters = [.. _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase))];
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = $"Session Snapshot Saved ({_savedNonStarters.Count} cards).";
            };

            var btnRecallRecruits = new Button
            {
                Text = "Recall",
                Location = new Point(284, 412),
                Size = new Size(80, 28)
            };
            btnRecallRecruits.Click += (s, e) =>
            {
                if (_savedNonStarters == null)
                {
                    MessageBox.Show(this, "No saved session snapshot for Non-Starting Characters.", "Recall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
                _deck.Resources.AddRange(_savedNonStarters);
                _deck.SortSections();
                _notifyDeckChanged();
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Text = $"Restored from Snapshot ({_savedNonStarters.Count} cards).";
            };

            _lblRecruitStatus = new Label
            {
                Location = new Point(16, 446),
                Size = new Size(770, 48),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic)
            };

            tabRecruits.Controls.AddRange([
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
                Size = new Size(370, 230)
            };

            var lblMode = new Label { Text = "Movement Mode:", Location = new Point(12, 28), Size = new Size(110, 20) };
            _cmbMode = new ComboBox
            {
                Location = new Point(130, 25),
                Size = new Size(225, 23),
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
                Size = new Size(225, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHaven.Items.AddRange(["Auto (Nearest Haven)", "Rivendell", "Lórien", "Grey Havens", "Edhellond", "Random"]);
            _cmbHaven.SelectedIndex = 0;

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

            var lblThNotes = new Label
            {
                Text = "Note: BFS overland highway from Rivendell and intermediate stepping-stone havens are automatically included in Starter + Region mode.",
                Location = new Point(12, 170),
                Size = new Size(345, 50),
                ForeColor = Color.DimGray
            };

            grpTheaterOpts.Controls.AddRange([
                lblMode, _cmbMode,
                lblHaven, _cmbHaven,
                lblRegCount, _numRegionCount,
                lblSiteCount, _numSiteCount,
                lblThNotes
            ]);

            var grpFilters = new GroupBox
            {
                Text = "Terrain & Hold Classifications",
                Location = new Point(400, 16),
                Size = new Size(385, 230)
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
                Size = new Size(760, 50),
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

            int poolMind = _deck.Pool.Sum(c => ParseCardMind(c.Mind));
            int charsInResources = _deck.CountCharactersInResources();
            int regionsInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int sitesInSites = _deck.Sites.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase));

            _lblDeckSummary.Text =
                $"Deck Title:       \"{_deck.Title}\"\n\n" +
                $"Pool:             {_deck.Pool.Count} characters ({poolMind} Mind)  [Official limit: <= 20 Mind]\n" +
                $"Resources:        {_deck.Resources.Count} cards ({charsInResources} characters)  [Official limit: <= 10 characters]\n" +
                $"Hazards:          {_deck.Hazards.Count} cards\n" +
                $"Sideboard:        {_deck.Sideboard.Count} cards\n" +
                $"Sites:            {_deck.Sites.Count} cards ({regionsInSites} Regions, {sitesInSites} Sites)\n\n" +
                $"Play Deck Status: {_deck.Resources.Count} Resources / {_deck.Hazards.Count} Hazards (Target: {_numPlayDeckSize.Value}/{_numPlayDeckSize.Value})\n" +
                $"Sideboard Status: {_deck.Sideboard.Count} / {_numSideboardSize.Value}";
        }

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

                // Secondary Wizard is only available if Primary is active and has < 3 copies
                bool canHaveWiz2 = hasWiz1 && wiz1Copies < 3;
                _cmbWizard2.Enabled = canHaveWiz2;

                if (!canHaveWiz2)
                {
                    _cmbWizard2.SelectedIndex = 0;
                    _cmbWizard2Copies.Enabled = false;
                }
                else
                {
                    // Prevent picking the exact same specific Wizard in both slots
                    string wiz1Name = _cmbWizard1.SelectedItem?.ToString() ?? "";
                    string wiz2Name = _cmbWizard2.SelectedItem?.ToString() ?? "";
                    if (_cmbWizard1.SelectedIndex > 1 && string.Equals(wiz1Name, wiz2Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _cmbWizard2.SelectedIndex = 0;
                    }

                    bool hasWiz2 = _cmbWizard2.SelectedIndex > 0;
                    _cmbWizard2Copies.Enabled = hasWiz2;

                    if (hasWiz2)
                    {
                        // If Primary has 2 copies, Secondary can only have 1 copy (2+1 rule)
                        // If Primary has 1 copy, Secondary can have 1 or 2 copies
                        int maxWiz2Copies = Math.Min(2, 3 - wiz1Copies);
                        string prevWiz2Cop = _cmbWizard2Copies.SelectedItem?.ToString() ?? "1";

                        _cmbWizard2Copies.Items.Clear();
                        for (int i = 1; i <= maxWiz2Copies; i++)
                        {
                            _cmbWizard2Copies.Items.Add(i.ToString());
                        }

                        int idx = _cmbWizard2Copies.FindStringExact(prevWiz2Cop);
                        _cmbWizard2Copies.SelectedIndex = idx >= 0 ? idx : 0;
                    }
                }
            }
            finally
            {
                _isUpdatingWizardControls = false;
            }

            ValidateRecruitTabBudget();
        }

        private int GetSelectedWizardCount()
        {
            int total = 0;
            if (_cmbWizard1.SelectedIndex > 0 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1))
            {
                total += c1;
            }
            if (_cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 && int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int c2))
            {
                total += c2;
            }
            return total;
        }

        private void ValidateRecruitTabBudget()
        {
            if (_btnRunRecruits == null || _lblRecruitStatus == null) return;

            int wizardCount = GetSelectedWizardCount();
            int companionCount = _recruitRows.Count(r => r.IsActive);
            int totalSelected = wizardCount + companionCount;

            if (totalSelected > 10)
            {
                _btnRunRecruits.Enabled = false;
                _btnSaveRecruits.Enabled = false;
                _lblRecruitStatus.ForeColor = Color.Firebrick;
                _lblRecruitStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"⚠ Selection exceeds the 10-character limit: {totalSelected}/10 selected ({wizardCount} Wizard(s) + {companionCount} Companion(s)).\n" +
                    $"Clear {totalSelected - 10} companion row(s) or reduce Wizard copies to enable Run and Save.";
            }
            else
            {
                _btnRunRecruits.Enabled = true;
                _btnSaveRecruits.Enabled = true;
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblRecruitStatus.Text =
                    $"Configured: {totalSelected}/10 Resource characters ({wizardCount} Wizard(s) + {companionCount} Companion(s)).";
            }
        }

        #endregion

        #region GRID_BUILDER

        private static void BuildCharacterGrid(
            TabPage page,
            List<CharacterRowControls> rowList,
            int count,
            string prefix,
            int startY,
            int rowSpacing,
            Action onRowChanged)
        {
            var lblHdrSlot = new Label { Text = "#", Location = new Point(16, startY - 19), Size = new Size(70, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrSkill = new Label { Text = "Skill Focus", Location = new Point(95, startY - 19), Size = new Size(130, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrRace = new Label { Text = "Species / Race", Location = new Point(235, startY - 19), Size = new Size(130, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrMind = new Label { Text = "Mind (e.g. <=3)", Location = new Point(375, startY - 19), Size = new Size(110, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrDI = new Label { Text = "DI (e.g. >=2)", Location = new Point(495, startY - 19), Size = new Size(110, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            page.Controls.AddRange([lblHdrSlot, lblHdrSkill, lblHdrRace, lblHdrMind, lblHdrDI]);

            for (int i = 0; i < count; i++)
            {
                int y = startY + (i * rowSpacing);
                var row = new CharacterRowControls
                {
                    LblSlot = new Label { Text = $"{prefix} {i + 1}:", Location = new Point(16, y + 3), Size = new Size(75, 20) },
                    CmbSkill = new ComboBox { Location = new Point(95, y), Size = new Size(130, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    CmbSpecies = new ComboBox { Location = new Point(235, y), Size = new Size(130, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    TxtMind = new TextBox { Location = new Point(375, y), Size = new Size(110, 23) },
                    TxtDI = new TextBox { Location = new Point(495, y), Size = new Size(110, 23) },
                    BtnClear = new Button { Text = "✕", Location = new Point(615, y - 1), Size = new Size(28, 25), ForeColor = Color.DimGray }
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
                page.Controls.AddRange([row.LblSlot, row.CmbSkill, row.CmbSpecies, row.TxtMind, row.TxtDI, row.BtnClear]);
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
            if (_chkConstrainStarterToTheater.Checked)
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

            var candidatesPerRow = new List<List<Card>>();
            for (int i = 0; i < activeRows.Count; i++)
            {
                var row = activeRows[i];
                var candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, theaterRegions);
                if (candidates.Count == 0)
                {
                    MessageBox.Show(this, $"No matching cards found for Starter Row {i + 1} with the selected criteria in your active expansions.", "Starting Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name),
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

            int finalMind = bestCompany.Sum(c => ParseCardMind(c.Mind));
            string companyNames = string.Join(", ", bestCompany.Select(c => $"{c.Name} ({c.Mind} Mind)"));
            _lblStarterStatus.Text = $"Drafted {bestCompany.Count} starting characters (Total Mind: {finalMind}/20):\n{companyNames}";
        }

        private void RunNonStartingCharactersDraft()
        {
            int wizardCount = GetSelectedWizardCount();
            var activeRows = _recruitRows.Where(r => r.IsActive).ToList();

            if (wizardCount == 0 && activeRows.Count == 0)
            {
                MessageBox.Show(this, "No Wizards or companion rows are selected. Please configure at least 1 character.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (wizardCount + activeRows.Count > 10)
            {
                ValidateRecruitTabBudget();
                return;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;
            var pickedRecruits = new List<Card>();

            // 1. Draft Selected Wizards (Primary & Secondary)
            var allWizards = allCards.Where(c =>
                string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) &&
                (activeSets.Count == 0 || activeSets.Contains(c.Set))).ToList();

            string chosenPrimaryWizardName = null;

            if (_cmbWizard1.SelectedIndex > 0 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int copies1) && copies1 > 0)
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
                for (int i = 0; i < copies1; i++)
                {
                    pickedRecruits.Add(wiz1Card);
                }
            }

            if (_cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 && int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int copies2) && copies2 > 0)
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

                for (int i = 0; i < copies2; i++)
                {
                    pickedRecruits.Add(wiz2Card);
                }
            }

            // 2. Draft Non-Wizard Companions from Active Rows
            var currentSites = _deck.Sites.Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase)).ToList();
            bool hasTheaterSites = currentSites.Count > 0;
            var currentSiteNames = new HashSet<string>(currentSites.Select(s => s.Name.Trim()), StringComparer.OrdinalIgnoreCase);

            var candidatesPerRow = new List<List<Card>>();
            for (int i = 0; i < activeRows.Count; i++)
            {
                var row = activeRows[i];
                var candidates = GetCandidatesForCharacterRow(row, allCards, activeSets, null);

                if (hasTheaterSites)
                {
                    candidates = [.. candidates.Where(c =>
                    {
                        if (string.IsNullOrWhiteSpace(c.Home)) return false;
                        string cleanHome = c.Home.Trim();
                        if (currentSiteNames.Contains(cleanHome)) return true;
                        return currentSites.Any(s =>
                            CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                            string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                            CardFilterService.MatchesPredicate(s, cleanHome, _filterService));
                    })];
                }

                if (candidates.Count == 0)
                {
                    string hint = hasTheaterSites ? " (constrained by current Theater sites)" : "";
                    MessageBox.Show(this, $"No matching cards found for Recruit Row {i + 1}{hint}. Clear or adjust Theater sites, or change row criteria.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                candidatesPerRow.Add(candidates);
            }

            var usedTitles = new HashSet<string>(_deck.Pool.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidatesPerRow.Count; i++)
            {
                var candidates = candidatesPerRow[i];
                var eligible = candidates.Where(c => !usedTitles.Contains(c.Name)).ToList();

                if (eligible.Count == 0)
                {
                    MessageBox.Show(this, $"Unable to draft unique companion for Row {i + 1} without duplicating an existing character. Try broadening your criteria.", "Non-Starting Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var chosen = eligible[Random.Shared.Next(eligible.Count)];
                pickedRecruits.Add(chosen);
                if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    usedTitles.Add(chosen.Name);
                }
            }

            _deck.Resources.RemoveAll(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
            _deck.Resources.AddRange(pickedRecruits);
            _deck.SortSections();
            _notifyDeckChanged();

            string recruitNames = string.Join(", ", pickedRecruits.Select(c => c.Name));
            _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblRecruitStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblRecruitStatus.Text = $"Drafted {pickedRecruits.Count} characters into Resources:\n{recruitNames}";
        }

        private void RunGeographicTheaterDraft()
        {
            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            bool isStarterPlusRegion = _cmbMode.SelectedIndex == 0;
            int maxRegions = isStarterPlusRegion ? (int)_numRegionCount.Value : 0;
            int siteCount = (int)_numSiteCount.Value;

            // 1. Gather required home sites for non-starter companions (excluding Avatars, who can enter at Rivendell)
            var nonStarters = _deck.Resources
                .Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) && !IsAvatar(c))
                .ToList();

            var requiredHomeSites = new List<Card>();
            var requiredHomeRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var charCard in nonStarters)
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
                    if (!requiredHomeSites.Any(s => string.Equals(s.Name, homeMatch.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        requiredHomeSites.Add(homeMatch);
                    }
                    if (!string.IsNullOrWhiteSpace(homeMatch.Region))
                    {
                        requiredHomeRegions.Add(homeMatch.Region.Trim());
                    }
                }
            }

            // 2. Atomic Pre-Check on Site and Region Budgets
            int requiredAdventureSitesCount = requiredHomeSites.Count(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            bool siteBudgetDeficit = requiredAdventureSitesCount > siteCount;
            bool regionBudgetDeficit = isStarterPlusRegion && requiredHomeRegions.Count > maxRegions;

            if (siteBudgetDeficit || regionBudgetDeficit)
            {
                var siteBulletList = string.Join("\n", requiredHomeSites.Select(s => $"  • {s.Name} ({s.Region})"));

                string siteMsg = siteBudgetDeficit
                    ? $"• Site Count needed: at least {requiredAdventureSitesCount} (current setting: {siteCount})\n"
                    : "";
                string regMsg = regionBudgetDeficit
                    ? $"• Region Count needed: at least {requiredHomeRegions.Count} (current setting: {maxRegions})\n"
                    : "";

                MessageBox.Show(this,
                    $"Cannot assemble Geographic Theater:\n\n" +
                    $"Your drafted non-starting characters require home sites that exceed your current Theater settings:\n" +
                    $"{siteBulletList}\n\n" +
                    $"Budget shortfalls:\n" +
                    $"{siteMsg}{regMsg}\n" +
                    $"No changes were made to your Site deck.\n\n" +
                    $"To proceed, increase the counts as indicated or re-run/adjust Non-Starting Characters.",
                    "Geographic Theater Budget Shortfall",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _lblTheaterStatus.Text = "Draft aborted: Insufficient Site or Region budget for non-starting character home sites.";
                return;
            }

            // 3. Resolve Haven Anchor & Graph
            string rawHaven = _cmbHaven.SelectedItem?.ToString() ?? "Rivendell";
            string selectedHaven = "Rivendell";

            if (rawHaven.StartsWith("Auto", StringComparison.OrdinalIgnoreCase))
            {
                var nonStarterWithHome = nonStarters.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Home));
                if (nonStarterWithHome != null)
                {
                    var homeSite = allCards.FirstOrDefault(s => string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(s.Name, nonStarterWithHome.Home));
                    if (homeSite != null && !string.IsNullOrWhiteSpace(homeSite.Haven))
                    {
                        selectedHaven = homeSite.Haven.Trim();
                    }
                }
            }
            else if (rawHaven.Equals("Random", StringComparison.OrdinalIgnoreCase))
            {
                string[] havens = ["Rivendell", "Lórien", "Grey Havens", "Edhellond"];
                selectedHaven = havens[Random.Shared.Next(havens.Length)];
            }
            else
            {
                selectedHaven = rawHaven;
            }

            var graph = _filterService.BuildRegionAdjacencyGraph(allCards);

            string homeRegion = "Rhudaur";
            var havenCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, selectedHaven));
            if (havenCard != null && !string.IsNullOrWhiteSpace(havenCard.Region))
            {
                homeRegion = havenCard.Region.Trim();
            }

            var checkedTerrains = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            string terrainFilter = checkedTerrains.Count > 0 ? string.Join("; ", checkedTerrains) : null;

            var checkedHolds = _clbSiteTypes.CheckedItems.Cast<string>().ToList();
            string holdFilter = checkedHolds.Count > 0 ? string.Join("; ", checkedHolds) : null;

            // 4. Expand Regional Cluster
            var activeRegions = new List<string>();
            if (isStarterPlusRegion)
            {
                var seedRegions = new List<string> { homeRegion };
                foreach (var hr in requiredHomeRegions)
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
                    var cluster = DeckGenerationService.ExpandContiguousRegionCluster(graph, homeRegion, maxRegions);
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

            var draftedSitesAndRegions = new List<Card>();

            // 5. Assemble Sites & Regions
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

                if (!activeRegions.Contains("Rhudaur", StringComparer.OrdinalIgnoreCase))
                {
                    var path = DeckGenerationService.FindShortestRegionPath(graph, "Rhudaur", activeRegions);
                    foreach (var p in path)
                    {
                        var transitCard = allCards.FirstOrDefault(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && CardFilterService.EqualsWithDiacriticFallback(c.Name, p));
                        if (transitCard != null && !draftedSitesAndRegions.Any(s => string.Equals(s.Name, transitCard.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            draftedSitesAndRegions.Add(transitCard);
                        }
                    }
                }
            }

            foreach (var hs in requiredHomeSites)
            {
                if (!draftedSitesAndRegions.Any(s => string.Equals(s.Name, hs.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    draftedSitesAndRegions.Add(hs);
                }
            }

            int remainingSiteBudget = Math.Max(0, siteCount - requiredAdventureSitesCount);
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

            _lblTheaterStatus.Text = $"Drafted {draftedSitesAndRegions.Count} cards ({finalRegions} Regions, {finalSites} Sites). Haven: {selectedHaven}";
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

                // Companion rows never draft Avatars (Wizards / Ringwraiths are handled in the Avatar box)
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
        public Label LblSlot { get; set; }
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
}