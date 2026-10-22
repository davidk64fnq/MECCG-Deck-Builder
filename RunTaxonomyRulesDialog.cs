using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class RunTaxonomyRulesDialog : Form
    {
        private readonly IReadOnlyList<Card> _targetCards;
        private readonly ISet<string> _activeSets;
        private readonly CardCatalogService _catalog;
        private readonly CardFilterService _filterService;

        // Mode 1: Dynamic Entity Harvest
        private readonly RadioButton _rbHarvest;
        private readonly ComboBox _cmbHarvestAttribute;
        private readonly ComboBox _cmbHarvestValue;
        private readonly TextBox _txtHarvestCategory;

        // Mode 2: Relational Attribute Lookup (Cross-Card Join)
        private readonly RadioButton _rbRelational;
        private readonly ComboBox _cmbRelSourceAttr;
        private readonly ComboBox _cmbRelLookupAttr;
        private readonly ComboBox _cmbRelLookupVal;
        private readonly ComboBox _cmbRelTargetAttr;
        private readonly TextBox _txtRelCategory;

        // Mode 3: Custom Regex
        private readonly RadioButton _rbQuick;
        private readonly TextBox _txtRegexPattern;
        private readonly TextBox _txtRegexTag;
        private readonly TextBox _txtRegexCategory;

        // Mode 4: File
        private readonly RadioButton _rbFile;
        private readonly TextBox _txtFilePath;
        private readonly Button _btnBrowseFile;

        // Shared controls
        private readonly ListView _lstPreview;
        private readonly Button _btnTest;
        private readonly Button _btnExportCsv;
        private readonly Button _btnApply;
        private readonly Button _btnClose;
        private List<TaxonomyRule> _loadedRules = [];

        public RunTaxonomyRulesDialog(
            IReadOnlyList<Card> targetCards,
            ISet<string> activeSets,
            CardCatalogService catalog,
            CardFilterService filterService)
        {
            _targetCards = targetCards ?? throw new ArgumentNullException(nameof(targetCards));
            _activeSets = activeSets ?? new HashSet<string>();
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));

            var settings = UserSettingsService.Current;

            Text = $"Extract Taxonomy with Rules ({_targetCards.Count} cards in Master List)";
            ClientSize = new Size(740, 680);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            var grpSource = new GroupBox
            {
                Text = "Extraction Rule Source",
                Location = new Point(12, 12),
                Size = new Size(716, 320)
            };

            // ==========================================
            // MODE 1: SCAN CARD TEXT FOR ENTITIES
            // ==========================================
            _rbHarvest = new RadioButton
            {
                Text = "Scan Card Text for Catalog Entities:",
                Location = new Point(12, 20),
                Size = new Size(320, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbHarvest.CheckedChanged += RuleSource_Changed;

            // Row 1: Source Definition
            var lblHAttr = new Label { Text = "Target Entity:", Location = new Point(32, 44), Size = new Size(90, 18) };
            _cmbHarvestAttribute = new ComboBox
            {
                Location = new Point(125, 41),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHarvestAttribute.SelectedIndexChanged += CmbHarvestAttribute_SelectedIndexChanged;

            var lblHEqual = new Label { Text = "=", Location = new Point(270, 44), Size = new Size(16, 18), TextAlign = ContentAlignment.MiddleCenter };
            _cmbHarvestValue = new ComboBox
            {
                Location = new Point(292, 41),
                Size = new Size(160, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Row 2: Destination Category
            var lblHCategory = new Label { Text = "Save to Category:", Location = new Point(32, 72), Size = new Size(110, 18) };
            _txtHarvestCategory = new TextBox
            {
                Text = settings.LastHarvestCategory ?? "Faction Focus",
                Location = new Point(145, 69),
                Size = new Size(307, 23)
            };

            // ==========================================
            // MODE 2: CROSS-CARD ATTRIBUTE LOOKUP
            // ==========================================
            _rbRelational = new RadioButton
            {
                Text = "Cross-Card Attribute Lookup:",
                Location = new Point(12, 100),
                Size = new Size(320, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbRelational.CheckedChanged += RuleSource_Changed;

            // Row 1: Read Attribute -> Lookup Entity (Attribute = Value)
            var lblRel1 = new Label { Text = "Read Attribute:", Location = new Point(32, 124), Size = new Size(95, 18) };
            _cmbRelSourceAttr = new ComboBox
            {
                Location = new Point(130, 121),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            var lblRel2 = new Label { Text = "Lookup Entity:", Location = new Point(280, 124), Size = new Size(90, 18) };
            _cmbRelLookupAttr = new ComboBox
            {
                Location = new Point(375, 121),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbRelLookupAttr.SelectedIndexChanged += CmbRelLookupAttr_SelectedIndexChanged;

            var lblRelEqual = new Label { Text = "=", Location = new Point(515, 124), Size = new Size(16, 18), TextAlign = ContentAlignment.MiddleCenter };
            _cmbRelLookupVal = new ComboBox
            {
                Location = new Point(535, 121),
                Size = new Size(165, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            // Row 2: Extract Attribute -> Save to Category
            var lblRel3 = new Label { Text = "Extract Attribute:", Location = new Point(32, 152), Size = new Size(95, 18) };
            _cmbRelTargetAttr = new ComboBox
            {
                Location = new Point(130, 149),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            var lblRel4 = new Label { Text = "Save to Category:", Location = new Point(280, 152), Size = new Size(115, 18) };
            _txtRelCategory = new TextBox
            {
                Text = settings.LastRelationalCategory ?? "Home Region",
                Location = new Point(400, 149),
                Size = new Size(300, 23),
                Enabled = false
            };

            // ==========================================
            // MODE 3: CUSTOM PATTERN SEARCH (REGEX)
            // ==========================================
            _rbQuick = new RadioButton
            {
                Text = "Custom Pattern Search (Regex):",
                Location = new Point(12, 180),
                Size = new Size(250, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbQuick.CheckedChanged += RuleSource_Changed;

            // Row 1: Full-width Search Pattern
            var lblQPattern = new Label { Text = "Pattern:", Location = new Point(32, 204), Size = new Size(60, 18) };
            _txtRegexPattern = new TextBox
            {
                Text = settings.LastExtractionPattern ?? @"(?i)Dwarf factions?",
                Location = new Point(95, 201),
                Size = new Size(605, 23),
                Enabled = false
            };

            // Row 2: Assigned Tag -> Save to Category
            var lblQTag = new Label { Text = "Assigned Tag:", Location = new Point(32, 232), Size = new Size(80, 18) };
            _txtRegexTag = new TextBox
            {
                Text = settings.LastExtractionTag ?? "$1",
                Location = new Point(115, 229),
                Size = new Size(275, 23),
                Enabled = false
            };

            var lblQCategory = new Label { Text = "Save to Category:", Location = new Point(405, 232), Size = new Size(110, 18) };
            _txtRegexCategory = new TextBox
            {
                Text = settings.LastExtractionCategory ?? "Custom Trait",
                Location = new Point(520, 229),
                Size = new Size(180, 23),
                Enabled = false
            };

            // ==========================================
            // MODE 4: BATCH RULES FROM FILE
            // ==========================================
            _rbFile = new RadioButton
            {
                Text = "Batch Rules from File (.json):",
                Location = new Point(12, 260),
                Size = new Size(240, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbFile.CheckedChanged += RuleSource_Changed;

            _txtFilePath = new TextBox
            {
                Location = new Point(32, 283),
                Size = new Size(570, 23),
                ReadOnly = true,
                Enabled = false
            };

            _btnBrowseFile = new Button
            {
                Text = "Browse...",
                Location = new Point(610, 282),
                Size = new Size(90, 25),
                Enabled = false
            };
            _btnBrowseFile.Click += BtnBrowseFile_Click;

            grpSource.Controls.AddRange([
                _rbHarvest, lblHAttr, _cmbHarvestAttribute, lblHEqual, _cmbHarvestValue, lblHCategory, _txtHarvestCategory,
                _rbRelational, lblRel1, _cmbRelSourceAttr, lblRel2, _cmbRelLookupAttr, lblRelEqual, _cmbRelLookupVal,
                lblRel3, _cmbRelTargetAttr, lblRel4, _txtRelCategory,
                _rbQuick, lblQPattern, _txtRegexPattern, lblQTag, _txtRegexTag, lblQCategory, _txtRegexCategory,
                _rbFile, _txtFilePath, _btnBrowseFile
            ]);

            // Restore active extraction mode
            switch (settings.LastExtractionMode)
            {
                case 2:
                    _rbRelational.Checked = true;
                    break;
                case 3:
                    _rbQuick.Checked = true;
                    break;
                case 4:
                    _rbFile.Checked = true;
                    break;
                default:
                    _rbHarvest.Checked = true;
                    break;
            }

            // Preview & actions
            _btnTest = new Button
            {
                Text = "Test / Preview Matches",
                Location = new Point(12, 340),
                Size = new Size(160, 28)
            };
            _btnTest.Click += BtnTest_Click;

            var lblScopeHint = new Label
            {
                Text = $"Scanning {_targetCards.Count} card(s) currently visible in the Master List.",
                Location = new Point(180, 345),
                Size = new Size(520, 20),
                ForeColor = Color.DimGray
            };

            _lstPreview = new ListView
            {
                Location = new Point(12, 375),
                Size = new Size(716, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lstPreview.Columns.Add("Card Name", 230);
            _lstPreview.Columns.Add("Set", 60);
            _lstPreview.Columns.Add("Category", 150);
            _lstPreview.Columns.Add("Extracted Tag", 250);

            _btnExportCsv = new Button
            {
                Text = "Export to CSV...",
                Location = new Point(350, 638),
                Size = new Size(130, 30)
            };
            _btnExportCsv.Click += BtnExportCsv_Click;

            _btnApply = new Button
            {
                Text = "Apply Tags to Catalog",
                Location = new Point(490, 638),
                Size = new Size(150, 30)
            };
            _btnApply.Click += BtnApply_Click;

            _btnClose = new Button
            {
                Text = "Close",
                Location = new Point(648, 638),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([grpSource, _btnTest, lblScopeHint, _lstPreview, _btnExportCsv, _btnApply, _btnClose]);
            CancelButton = _btnClose;

            FormClosing += (s, e) => SaveCurrentSettings();

            PopulateDropdowns();
        }

        private void PopulateDropdowns()
        {
            var settings = UserSettingsService.Current;

            _cmbHarvestAttribute.Items.Clear();
            _cmbRelSourceAttr.Items.Clear();
            _cmbRelTargetAttr.Items.Clear();
            _cmbRelLookupAttr.Items.Clear();

            var keys = _catalog.GetFilterKeys().Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            foreach (var k in keys)
            {
                _cmbHarvestAttribute.Items.Add(k);
                _cmbRelSourceAttr.Items.Add(k);
                _cmbRelTargetAttr.Items.Add(k);
                _cmbRelLookupAttr.Items.Add(k);
            }

            string savedSource = settings.LastRelationalSourceAttr ?? "Home";
            int sourceIdx = _cmbRelSourceAttr.FindStringExact(savedSource);
            _cmbRelSourceAttr.SelectedIndex = sourceIdx >= 0 ? sourceIdx : 0;

            string savedTarget = settings.LastRelationalTargetAttr ?? "Region";
            int targetIdx = _cmbRelTargetAttr.FindStringExact(savedTarget);
            _cmbRelTargetAttr.SelectedIndex = targetIdx >= 0 ? targetIdx : 0;

            string savedLookupAttr = settings.LastRelationalLookupAttr ?? "Primary";
            int lookupAttrIdx = _cmbRelLookupAttr.FindStringExact(savedLookupAttr);
            _cmbRelLookupAttr.SelectedIndex = lookupAttrIdx >= 0 ? lookupAttrIdx : 0;

            string savedHarvestAttr = settings.LastHarvestAttribute ?? "Secondary";
            int defaultHarvestIdx = _cmbHarvestAttribute.FindStringExact(savedHarvestAttr);
            _cmbHarvestAttribute.SelectedIndex = defaultHarvestIdx >= 0 ? defaultHarvestIdx : 0;
        }

        private void CmbHarvestAttribute_SelectedIndexChanged(object sender, EventArgs e)
        {
            string attr = _cmbHarvestAttribute.SelectedItem?.ToString() ?? string.Empty;
            _cmbHarvestValue.Items.Clear();
            if (string.IsNullOrWhiteSpace(attr)) return;

            var values = _catalog.GetFilterValues(attr);
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    _cmbHarvestValue.Items.Add(v);
                }
            }

            string savedHarvestVal = UserSettingsService.Current.LastHarvestValue ?? "Faction";
            int defaultIdx = _cmbHarvestValue.FindStringExact(savedHarvestVal);
            if (defaultIdx >= 0) _cmbHarvestValue.SelectedIndex = defaultIdx;
            else if (_cmbHarvestValue.Items.Count > 0) _cmbHarvestValue.SelectedIndex = 0;
        }

        private void CmbRelLookupAttr_SelectedIndexChanged(object sender, EventArgs e)
        {
            string attr = _cmbRelLookupAttr.SelectedItem?.ToString() ?? string.Empty;
            _cmbRelLookupVal.Items.Clear();
            if (string.IsNullOrWhiteSpace(attr)) return;

            var values = _catalog.GetFilterValues(attr);
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    _cmbRelLookupVal.Items.Add(v);
                }
            }

            string savedLookupVal = UserSettingsService.Current.LastRelationalLookupVal ?? "Site";
            int defaultIdx = _cmbRelLookupVal.FindStringExact(savedLookupVal);
            if (defaultIdx >= 0) _cmbRelLookupVal.SelectedIndex = defaultIdx;
            else if (_cmbRelLookupVal.Items.Count > 0) _cmbRelLookupVal.SelectedIndex = 0;
        }

        private void RuleSource_Changed(object sender, EventArgs e)
        {
            bool isHarvest = _rbHarvest.Checked;
            _cmbHarvestAttribute.Enabled = isHarvest;
            _cmbHarvestValue.Enabled = isHarvest;
            _txtHarvestCategory.Enabled = isHarvest;

            bool isRelational = _rbRelational.Checked;
            _cmbRelSourceAttr.Enabled = isRelational;
            _cmbRelLookupAttr.Enabled = isRelational;
            _cmbRelLookupVal.Enabled = isRelational;
            _cmbRelTargetAttr.Enabled = isRelational;
            _txtRelCategory.Enabled = isRelational;

            bool isQuick = _rbQuick.Checked;
            _txtRegexPattern.Enabled = isQuick;
            _txtRegexTag.Enabled = isQuick;
            _txtRegexCategory.Enabled = isQuick;

            bool isFile = _rbFile.Checked;
            _txtFilePath.Enabled = isFile;
            _btnBrowseFile.Enabled = isFile;
        }

        private void SaveCurrentSettings()
        {
            var settings = UserSettingsService.Current;

            // Mode 1 Entity Harvest
            if (_cmbHarvestAttribute.SelectedItem != null)
            {
                settings.LastHarvestAttribute = _cmbHarvestAttribute.SelectedItem.ToString();
            }
            if (_cmbHarvestValue.SelectedItem != null)
            {
                settings.LastHarvestValue = _cmbHarvestValue.SelectedItem.ToString();
            }
            if (!string.IsNullOrWhiteSpace(_txtHarvestCategory.Text))
            {
                settings.LastHarvestCategory = _txtHarvestCategory.Text.Trim();
            }

            // Mode 3 Quick Regex
            if (!string.IsNullOrWhiteSpace(_txtRegexCategory.Text))
            {
                settings.LastExtractionCategory = _txtRegexCategory.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(_txtRegexTag.Text))
            {
                settings.LastExtractionTag = _txtRegexTag.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(_txtRegexPattern.Text))
            {
                settings.LastExtractionPattern = _txtRegexPattern.Text.Trim();
            }

            // Mode 2 Relational Lookup
            if (!string.IsNullOrWhiteSpace(_txtRelCategory.Text))
            {
                settings.LastRelationalCategory = _txtRelCategory.Text.Trim();
            }
            if (_cmbRelSourceAttr.SelectedItem != null)
            {
                settings.LastRelationalSourceAttr = _cmbRelSourceAttr.SelectedItem.ToString();
            }
            if (_cmbRelLookupAttr.SelectedItem != null)
            {
                settings.LastRelationalLookupAttr = _cmbRelLookupAttr.SelectedItem.ToString();
            }
            if (_cmbRelLookupVal.SelectedItem != null)
            {
                settings.LastRelationalLookupVal = _cmbRelLookupVal.SelectedItem.ToString();
            }
            if (_cmbRelTargetAttr.SelectedItem != null)
            {
                settings.LastRelationalTargetAttr = _cmbRelTargetAttr.SelectedItem.ToString();
            }

            // Active mode
            if (_rbQuick.Checked) settings.LastExtractionMode = 3;
            else if (_rbRelational.Checked) settings.LastExtractionMode = 2;
            else if (_rbFile.Checked) settings.LastExtractionMode = 4;
            else settings.LastExtractionMode = 1;

            UserSettingsService.SaveSettings();
        }

        private void BtnBrowseFile_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open Taxonomy Rules File",
                Filter = "JSON Rules (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    _loadedRules = TaxonomyExtractionService.LoadRulesFromJson(json);
                    _txtFilePath.Text = ofd.FileName;
                    MessageBox.Show(this, $"Loaded {_loadedRules.Count} rule(s) successfully.", "Rules Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to load rule file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private ExtractionResult RunExtraction()
        {
            SaveCurrentSettings();

            if (_rbRelational.Checked)
            {
                string sourceAttr = _cmbRelSourceAttr.SelectedItem?.ToString() ?? "Home";
                string lookupAttr = _cmbRelLookupAttr.SelectedItem?.ToString() ?? "Primary";
                string lookupVal = _cmbRelLookupVal.SelectedItem?.ToString() ?? "Site";
                string targetAttr = _cmbRelTargetAttr.SelectedItem?.ToString() ?? "Region";
                string category = _txtRelCategory.Text.Trim();

                return TaxonomyExtractionService.ExecuteRelationalExtraction(
                    _targetCards,
                    _catalog.Cards,
                    sourceAttr,
                    lookupAttr,
                    lookupVal,
                    targetAttr,
                    category);
            }

            var rules = GetActiveRules();
            if (rules.Count == 0) return null;

            return TaxonomyExtractionService.ExecuteRules(_targetCards, rules);
        }

        private List<TaxonomyRule> GetActiveRules()
        {
            if (_rbHarvest.Checked)
            {
                string attr = _cmbHarvestAttribute.SelectedItem?.ToString() ?? string.Empty;
                string val = _cmbHarvestValue.SelectedItem?.ToString() ?? string.Empty;
                string cat = _txtHarvestCategory.Text.Trim();

                if (string.IsNullOrWhiteSpace(attr) || string.IsNullOrWhiteSpace(val)) return [];

                var harvestedNames = TaxonomyExtractionService.HarvestEntityNames(_catalog.Cards, _activeSets, attr, val);
                if (harvestedNames.Count == 0)
                {
                    MessageBox.Show(this, $"No cards in the catalog matched {attr} = '{val}' to harvest.", "Harvest Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return [];
                }

                var rule = TaxonomyExtractionService.BuildEntityHarvestRule(harvestedNames, cat);
                return rule != null ? [rule] : [];
            }

            if (_rbQuick.Checked)
            {
                return [
                    new TaxonomyRule
                    {
                        Category = _txtRegexCategory.Text.Trim(),
                        Pattern = _txtRegexPattern.Text.Trim(),
                        TagTemplate = _txtRegexTag.Text.Trim()
                    }
                ];
            }

            return _loadedRules;
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            var result = RunExtraction();
            if (result == null)
            {
                MessageBox.Show(this, "No active rules to evaluate.", "Test Rules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lstPreview.BeginUpdate();
            _lstPreview.Items.Clear();

            foreach (var (cardId, catDict) in result.Matches)
            {
                var card = _catalog.GetCardById(cardId);
                if (card == null) continue;

                foreach (var (cat, tags) in catDict)
                {
                    var item = new ListViewItem(card.Name);
                    item.SubItems.Add(card.Set);
                    item.SubItems.Add(cat);
                    item.SubItems.Add(string.Join("; ", tags));
                    _lstPreview.Items.Add(item);
                }
            }

            _lstPreview.EndUpdate();

            if (_lstPreview.Items.Count == 0)
            {
                MessageBox.Show(this, $"No cards in the current Master List ({_targetCards.Count} cards) matched the criteria.", "Test Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            var result = RunExtraction();
            if (result == null || result.Matches.Count == 0)
            {
                MessageBox.Show(this, "No matching results to export.", "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export Extraction Draft (CSV)",
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "Relational_Extraction_Draft.csv"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                TaxonomyExtractionService.ExportDraftToCsv(_targetCards, result, sfd.FileName);
                MessageBox.Show(this, $"Extraction draft exported successfully to:\n{sfd.FileName}\n\nMatched {result.Matches.Count} card(s) out of {_targetCards.Count} in scope.", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            var result = RunExtraction();
            if (result == null || result.Matches.Count == 0)
            {
                MessageBox.Show(this, "No matches to apply.", "Apply Tags", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int cardsTagged = result.Matches.Count;
            int tagsAssigned = 0;

            foreach (var (cardId, catDict) in result.Matches)
            {
                foreach (var (category, tagSet) in catDict)
                {
                    _filterService.AddCustomKeyName(category);
                    foreach (var tag in tagSet)
                    {
                        _filterService.AppendCardCustomTag(cardId, category, tag);
                        tagsAssigned++;
                    }
                }
            }

            MessageBox.Show(this, $"Applied {tagsAssigned} tag assignment(s) across {cardsTagged} card(s) into your catalog taxonomy.", "Tags Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
    }
}