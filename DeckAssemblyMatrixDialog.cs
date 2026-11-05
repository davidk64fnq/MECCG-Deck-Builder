using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class DeckAssemblyMatrixDialog : Form
    {
        // --- COMPANY CONTROLS ---
        private readonly ComboBox _cmbCompany;
        private readonly CheckedListBox _clbCompanySpecies;
        private readonly ComboBox _cmbLeaderSkill;
        private readonly NumericUpDown _numPoolSize;

        // --- THEATER CONTROLS ---
        private readonly ComboBox _cmbTheater;
        private readonly ComboBox _cmbHavenOverride;
        private readonly ComboBox _cmbModeOverride;
        private readonly NumericUpDown _numRegionCount;
        private readonly NumericUpDown _numSiteCount;
        private readonly CheckedListBox _clbRegionTypes;
        private readonly CheckedListBox _clbSiteTypes;
        private readonly CheckBox _chkOverlandPath;
        private readonly CheckBox _chkIntermediateHavens;

        // --- PIPELINE & ACTIONS ---
        private readonly ComboBox _cmbDirection;
        private readonly Label _lblFlowSummary;
        private readonly Button _btnAssemble;
        private readonly Button _btnCancel;

        private readonly List<(string DisplayName, string FilePath, DeckGenerationRuleSet RuleSet)> _companyComponents = [];
        private readonly List<(string DisplayName, string FilePath, DeckGenerationRuleSet RuleSet)> _theaterComponents = [];

        public IReadOnlyList<DeckGenerationRuleSet> SelectedComponents { get; private set; }
        public Dictionary<string, string> ParameterOverrides { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> SelectedComponentFiles { get; private set; }
        public string AssemblySummary { get; private set; }
        private static DeckAssemblyDialogState s_lastState;

        public DeckAssemblyMatrixDialog()
        {
            Text = "Modular Deck Assembly Workbench";
            ClientSize = new Size(740, 620);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            var lblHeader = new Label
            {
                Text = "Modular Deck Assembly Workbench",
                Location = new Point(14, 10),
                Size = new Size(710, 22),
                Font = new Font("Segoe UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };

            // ==========================================
            // LEFT COLUMN: STARTING COMPANY / POOL
            // ==========================================
            var grpCompany = new GroupBox
            {
                Text = "1. Starting Company / Pool Slot",
                Location = new Point(14, 38),
                Size = new Size(350, 420)
            };

            var lblComp = new Label { Text = "Component:", Location = new Point(12, 22), Size = new Size(326, 16) };
            _cmbCompany = new ComboBox
            {
                Location = new Point(12, 40),
                Size = new Size(326, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbCompany.SelectedIndexChanged += (s, e) => UpdateFlowSummary();

            var lblPoolSize = new Label { Text = "Pool Size (Characters):", Location = new Point(12, 72), Size = new Size(160, 20) };
            _numPoolSize = new NumericUpDown
            {
                Location = new Point(180, 70),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 10,
                Value = 5
            };

            var lblLeaderSkill = new Label { Text = "Leader Skill Focus:", Location = new Point(12, 102), Size = new Size(160, 20) };
            _cmbLeaderSkill = new ComboBox
            {
                Location = new Point(180, 99),
                Size = new Size(158, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbLeaderSkill.Items.AddRange(["Any (Archetype Default)", "Diplomat", "Ranger", "Sage", "Scout", "Warrior"]);
            _cmbLeaderSkill.SelectedIndex = 0;

            var lblSpecies = new Label
            {
                Text = "Species / Cultures (Checked = Allowed; None = Any):",
                Location = new Point(12, 132),
                Size = new Size(326, 18)
            };
            _clbCompanySpecies = new CheckedListBox
            {
                Location = new Point(12, 152),
                Size = new Size(326, 95),
                CheckOnClick = true
            };
            _clbCompanySpecies.Items.AddRange(["Dwarf", "Dúnadan", "Elf", "Hobbit", "Man"]);

            var lblCompNote = new Label
            {
                Text = "Note: Leader stats (Mind/Direct) are enforced by the chosen archetype. Checking multiple species permits mixed alliances.",
                Location = new Point(12, 255),
                Size = new Size(326, 150),
                ForeColor = Color.DimGray
            };

            grpCompany.Controls.AddRange([
                lblComp, _cmbCompany,
                lblPoolSize, _numPoolSize,
                lblLeaderSkill, _cmbLeaderSkill,
                lblSpecies, _clbCompanySpecies,
                lblCompNote
            ]);

            // ==========================================
            // RIGHT COLUMN: GEOGRAPHIC THEATER / SITES
            // ==========================================
            var grpTheater = new GroupBox
            {
                Text = "2. Geographic Theater / Sites Slot",
                Location = new Point(374, 38),
                Size = new Size(350, 420)
            };

            var lblTh = new Label { Text = "Component:", Location = new Point(12, 22), Size = new Size(326, 16) };
            _cmbTheater = new ComboBox
            {
                Location = new Point(12, 40),
                Size = new Size(326, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbTheater.SelectedIndexChanged += (s, e) => UpdateFlowSummary();

            var lblHaven = new Label { Text = "Haven Anchor:", Location = new Point(12, 70), Size = new Size(95, 20) };
            _cmbHavenOverride = new ComboBox
            {
                Location = new Point(110, 67),
                Size = new Size(228, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHavenOverride.Items.AddRange(["Auto (From Leader if present)", "Random", "Rivendell", "Lórien", "Grey Havens", "Edhellond"]);
            _cmbHavenOverride.SelectedIndex = 0;

            var lblMode = new Label { Text = "Movement:", Location = new Point(12, 98), Size = new Size(95, 20) };
            _cmbModeOverride = new ComboBox
            {
                Location = new Point(110, 95),
                Size = new Size(228, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbModeOverride.Items.AddRange(["Starter + Region (Roaming)", "Starter Only (Haven Hub)"]);
            _cmbModeOverride.SelectedIndex = 0;
            _cmbModeOverride.SelectedIndexChanged += CmbModeOverride_SelectedIndexChanged;

            var lblRegions = new Label { Text = "Region Count:", Location = new Point(12, 126), Size = new Size(90, 20) };
            _numRegionCount = new NumericUpDown
            {
                Location = new Point(105, 124),
                Size = new Size(50, 23),
                Minimum = 0,
                Maximum = 12,
                Value = 4
            };

            var lblSites = new Label { Text = "Site Count:", Location = new Point(185, 126), Size = new Size(75, 20) };
            _numSiteCount = new NumericUpDown
            {
                Location = new Point(265, 124),
                Size = new Size(50, 23),
                Minimum = 0,
                Maximum = 25,
                Value = 8
            };

            var lblRegTypes = new Label { Text = "Region Terrains (None = Any):", Location = new Point(12, 152), Size = new Size(160, 16) };
            _clbRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 170),
                Size = new Size(158, 120),
                CheckOnClick = true
            };
            _clbRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);

            var lblSiteTypes = new Label { Text = "Site Holds (None = Any):", Location = new Point(180, 152), Size = new Size(158, 16) };
            _clbSiteTypes = new CheckedListBox
            {
                Location = new Point(180, 170),
                Size = new Size(158, 120),
                CheckOnClick = true
            };
            _clbSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Haven"]);

            _chkOverlandPath = new CheckBox
            {
                Text = "Include BFS Overland Route from Rivendell",
                Location = new Point(12, 360),
                Size = new Size(326, 22),
                Checked = false
            };

            _chkIntermediateHavens = new CheckBox
            {
                Text = "Include Stepping-Stone Havens (Lórien / Grey Havens)",
                Location = new Point(12, 385),
                Size = new Size(326, 22),
                Checked = true
            };

            grpTheater.Controls.AddRange([
                lblTh, _cmbTheater,
                lblHaven, _cmbHavenOverride,
                lblMode, _cmbModeOverride,
                lblRegions, _numRegionCount,
                lblSites, _numSiteCount,
                lblRegTypes, _clbRegionTypes,
                lblSiteTypes, _clbSiteTypes,
                _chkOverlandPath, _chkIntermediateHavens
            ]);

            // ==========================================
            // BOTTOM: PIPELINE DRIVER & ACTIONS
            // ==========================================
            var grpFlow = new GroupBox
            {
                Text = "Assembly Pipeline & Primary Driver",
                Location = new Point(14, 464),
                Size = new Size(710, 100)
            };

            var lblDir = new Label { Text = "Assembly Driver:", Location = new Point(12, 24), Size = new Size(105, 20) };
            _cmbDirection = new ComboBox
            {
                Location = new Point(120, 21),
                Size = new Size(575, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbDirection.Items.AddRange([
                "Leader-First  (Company recruits retinue ➔ Theater expands around Leader)",
                "Region-First  (Theater sets boundaries ➔ Company drawn from Theater)"
            ]);
            _cmbDirection.SelectedIndex = 0;
            _cmbDirection.SelectedIndexChanged += (s, e) => UpdateFlowSummary();

            _lblFlowSummary = new Label
            {
                Location = new Point(12, 50),
                Size = new Size(680, 42),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Consolas", 8.5f)
            };
            grpFlow.Controls.AddRange([lblDir, _cmbDirection, _lblFlowSummary]);

            _btnAssemble = new Button
            {
                Text = "Assemble Deck",
                Location = new Point(460, 575),
                Size = new Size(150, 32),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnAssemble.Click += BtnAssemble_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(620, 575),
                Size = new Size(100, 32),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([lblHeader, grpCompany, grpTheater, grpFlow, _btnAssemble, _btnCancel]);
            AcceptButton = _btnAssemble;
            CancelButton = _btnCancel;

            LoadDiscoveredComponents();
            RestoreLastState();
            UpdateFlowSummary();
        }

        private void RestoreLastState()
        {
            if (s_lastState == null) return;

            // 1. Restore Company
            if (!string.IsNullOrEmpty(s_lastState.SelectedCompanyFile))
            {
                int compIdx = _companyComponents.FindIndex(c => string.Equals(c.FilePath, s_lastState.SelectedCompanyFile, StringComparison.OrdinalIgnoreCase));
                if (compIdx >= 0) _cmbCompany.SelectedIndex = compIdx;
            }

            if (s_lastState.PoolSize >= _numPoolSize.Minimum && s_lastState.PoolSize <= _numPoolSize.Maximum)
                _numPoolSize.Value = s_lastState.PoolSize;

            if (s_lastState.LeaderSkillIndex >= 0 && s_lastState.LeaderSkillIndex < _cmbLeaderSkill.Items.Count)
                _cmbLeaderSkill.SelectedIndex = s_lastState.LeaderSkillIndex;

            for (int i = 0; i < _clbCompanySpecies.Items.Count; i++)
            {
                string item = _clbCompanySpecies.Items[i].ToString();
                _clbCompanySpecies.SetItemChecked(i, s_lastState.SelectedSpecies.Contains(item, StringComparer.OrdinalIgnoreCase));
            }

            // 2. Restore Theater
            if (!string.IsNullOrEmpty(s_lastState.SelectedTheaterFile))
            {
                int thIdx = _theaterComponents.FindIndex(t => string.Equals(t.FilePath, s_lastState.SelectedTheaterFile, StringComparison.OrdinalIgnoreCase));
                if (thIdx >= 0) _cmbTheater.SelectedIndex = thIdx;
            }

            if (s_lastState.HavenIndex >= 0 && s_lastState.HavenIndex < _cmbHavenOverride.Items.Count)
                _cmbHavenOverride.SelectedIndex = s_lastState.HavenIndex;

            if (s_lastState.ModeIndex >= 0 && s_lastState.ModeIndex < _cmbModeOverride.Items.Count)
                _cmbModeOverride.SelectedIndex = s_lastState.ModeIndex;

            if (s_lastState.RegionCount >= _numRegionCount.Minimum && s_lastState.RegionCount <= _numRegionCount.Maximum)
                _numRegionCount.Value = s_lastState.RegionCount;

            if (s_lastState.SiteCount >= _numSiteCount.Minimum && s_lastState.SiteCount <= _numSiteCount.Maximum)
                _numSiteCount.Value = s_lastState.SiteCount;

            for (int i = 0; i < _clbRegionTypes.Items.Count; i++)
            {
                string item = _clbRegionTypes.Items[i].ToString();
                _clbRegionTypes.SetItemChecked(i, s_lastState.SelectedRegionTypes.Contains(item, StringComparer.OrdinalIgnoreCase));
            }

            for (int i = 0; i < _clbSiteTypes.Items.Count; i++)
            {
                string item = _clbSiteTypes.Items[i].ToString();
                _clbSiteTypes.SetItemChecked(i, s_lastState.SelectedSiteTypes.Contains(item, StringComparer.OrdinalIgnoreCase));
            }

            _chkOverlandPath.Checked = s_lastState.IncludeOverlandPath;
            _chkIntermediateHavens.Checked = s_lastState.IncludeIntermediateHavens;

            // 3. Restore Direction
            if (s_lastState.DirectionIndex >= 0 && s_lastState.DirectionIndex < _cmbDirection.Items.Count)
                _cmbDirection.SelectedIndex = s_lastState.DirectionIndex;
        }

        private void CmbModeOverride_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isStarterOnly = _cmbModeOverride.SelectedIndex == 1;

            if (isStarterOnly)
            {
                _numRegionCount.Value = 0;
                _numRegionCount.Enabled = false;
                _chkOverlandPath.Checked = false;
                _chkOverlandPath.Enabled = false;
            }
            else
            {
                if (_numRegionCount.Value == 0) _numRegionCount.Value = 4;
                _numRegionCount.Enabled = true;
                _chkOverlandPath.Enabled = true;
            }
        }

        private void LoadDiscoveredComponents()
        {
            string baseDir = StoragePathService.AppBaseDirectory;
            var searchPaths = new[]
            {
                Path.Combine(baseDir, "Components"),
                Path.Combine(baseDir, @"..\..\..\Components"),
                Path.Combine(StoragePathService.GetDataDirectory(), "Components")
            };

            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    LoadFromDirectory(path);
                    break;
                }
            }

            _cmbCompany.Items.Clear();
            foreach (var (displayName, _, _) in _companyComponents) _cmbCompany.Items.Add(displayName);
            if (_cmbCompany.Items.Count > 0) _cmbCompany.SelectedIndex = 0;

            _cmbTheater.Items.Clear();
            foreach (var (displayName, _, _) in _theaterComponents) _cmbTheater.Items.Add(displayName);
            if (_cmbTheater.Items.Count > 0) _cmbTheater.SelectedIndex = 0;
        }

        private void LoadFromDirectory(string rootDir)
        {
            foreach (var file in Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var ruleSet = DeckGenerationService.LoadRuleSetFromJson(json);
                    if (ruleSet == null) continue;

                    string display = !string.IsNullOrWhiteSpace(ruleSet.DeckTitle) ? ruleSet.DeckTitle : Path.GetFileNameWithoutExtension(file);

                    if (string.Equals(ruleSet.ComponentType, "Company", StringComparison.OrdinalIgnoreCase) ||
                        file.Contains("Company", StringComparison.OrdinalIgnoreCase))
                    {
                        _companyComponents.Add((display, file, ruleSet));
                    }
                    else if (string.Equals(ruleSet.ComponentType, "Theater", StringComparison.OrdinalIgnoreCase) ||
                             file.Contains("Theater", StringComparison.OrdinalIgnoreCase))
                    {
                        _theaterComponents.Add((display, file, ruleSet));
                    }
                }
                catch { }
            }
        }

        private void UpdateFlowSummary()
        {
            bool isLeaderFirst = _cmbDirection.SelectedIndex == 0;
            if (isLeaderFirst)
            {
                _lblFlowSummary.Text = "Execution Order:\n[Company] Drafts Leader & Retinue ➔ [Theater] Expands around Leader's Home Territory";
            }
            else
            {
                _lblFlowSummary.Text = "Execution Order:\n[Theater] Establishes Contiguous Regional Cluster ➔ [Company] Recruits Native Companions";
            }
        }

        private void BtnAssemble_Click(object sender, EventArgs e)
        {
            if (_cmbCompany.SelectedIndex < 0 || _cmbTheater.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Please select both a Company and a Theater component.", "Workbench", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var (DisplayName, FilePath, RuleSet) = _companyComponents[_cmbCompany.SelectedIndex];
            var theater = _theaterComponents[_cmbTheater.SelectedIndex];

            bool isLeaderFirst = _cmbDirection.SelectedIndex == 0;
            SelectedComponents = isLeaderFirst ? [RuleSet, theater.RuleSet] : [theater.RuleSet, RuleSet];
            SelectedComponentFiles = isLeaderFirst ? [FilePath, theater.FilePath] : [theater.FilePath, FilePath];
            AssemblySummary = $"{DisplayName} + {theater.DisplayName}";

            ParameterOverrides.Clear();

            // --- COMPANY PARAMETERS ---
            ParameterOverrides["Company.PoolSize"] = _numPoolSize.Value.ToString();

            if (_cmbLeaderSkill.SelectedIndex > 0)
            {
                ParameterOverrides["Company.Skill"] = _cmbLeaderSkill.SelectedItem.ToString();
            }

            var checkedSpecies = _clbCompanySpecies.CheckedItems.Cast<string>().ToList();
            if (checkedSpecies.Count > 0)
            {
                ParameterOverrides["Company.Race"] = string.Join("; ", checkedSpecies);
            }

            // --- THEATER PARAMETERS ---
            if (_cmbHavenOverride.SelectedIndex == 0) ParameterOverrides["Theater.Haven"] = "Auto";
            else ParameterOverrides["Theater.Haven"] = _cmbHavenOverride.SelectedItem.ToString();

            ParameterOverrides["Theater.Mode"] = _cmbModeOverride.SelectedIndex == 0 ? "Starter + Region" : "Starter Only";
            ParameterOverrides["Theater.RegionCount"] = _numRegionCount.Value.ToString();
            ParameterOverrides["Theater.SiteCount"] = _numSiteCount.Value.ToString();

            var checkedRegTypes = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            if (checkedRegTypes.Count > 0)
            {
                ParameterOverrides["Theater.RegionType"] = string.Join("; ", checkedRegTypes);
            }

            var checkedSiteTypes = _clbSiteTypes.CheckedItems.Cast<string>().ToList();
            if (checkedSiteTypes.Count > 0)
            {
                ParameterOverrides["Theater.SiteType"] = string.Join("; ", checkedSiteTypes);
            }

            ParameterOverrides["Theater.IncludeOverlandPath"] = _chkOverlandPath.Checked.ToString();
            ParameterOverrides["Theater.IncludeIntermediateHavens"] = _chkIntermediateHavens.Checked.ToString();

            // Save state for next time the dialog is opened
            s_lastState = new DeckAssemblyDialogState
            {
                SelectedCompanyFile = _companyComponents[_cmbCompany.SelectedIndex].FilePath,
                PoolSize = _numPoolSize.Value,
                LeaderSkillIndex = _cmbLeaderSkill.SelectedIndex,
                SelectedSpecies = [.. checkedSpecies],

                SelectedTheaterFile = _theaterComponents[_cmbTheater.SelectedIndex].FilePath,
                HavenIndex = _cmbHavenOverride.SelectedIndex,
                ModeIndex = _cmbModeOverride.SelectedIndex,
                RegionCount = _numRegionCount.Value,
                SiteCount = _numSiteCount.Value,
                SelectedRegionTypes = [.. checkedRegTypes],
                SelectedSiteTypes = [.. checkedSiteTypes],
                IncludeOverlandPath = _chkOverlandPath.Checked,
                IncludeIntermediateHavens = _chkIntermediateHavens.Checked,

                DirectionIndex = _cmbDirection.SelectedIndex
            };

            DialogResult = DialogResult.OK;
        }
    }

    public sealed class DeckAssemblyDialogState
    {
        public string SelectedCompanyFile { get; set; }
        public decimal PoolSize { get; set; } = 5;
        public int LeaderSkillIndex { get; set; }
        public List<string> SelectedSpecies { get; set; } = [];

        public string SelectedTheaterFile { get; set; }
        public int HavenIndex { get; set; }
        public int ModeIndex { get; set; }
        public decimal RegionCount { get; set; } = 4;
        public decimal SiteCount { get; set; } = 8;
        public List<string> SelectedRegionTypes { get; set; } = [];
        public List<string> SelectedSiteTypes { get; set; } = [];
        public bool IncludeOverlandPath { get; set; }
        public bool IncludeIntermediateHavens { get; set; } = true;

        public int DirectionIndex { get; set; }
    }
}