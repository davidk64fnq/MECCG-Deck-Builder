using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private readonly TextBox _txtHarvestCategory;
        private readonly ComboBox _cmbHarvestAttribute;
        private readonly ComboBox _cmbHarvestValue;

        // Mode 2: Quick Regex
        private readonly RadioButton _rbQuick;
        private readonly TextBox _txtRegexCategory;
        private readonly TextBox _txtRegexTag;
        private readonly TextBox _txtRegexPattern;

        // Mode 3: File
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

            Text = $"Extract Taxonomy with Rules ({_targetCards.Count} cards in Master List)";
            ClientSize = new Size(720, 570);
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
                Size = new Size(696, 210)
            };

            // 1. Dynamic Entity Harvest
            _rbHarvest = new RadioButton
            {
                Text = "Dynamic Entity Harvest (from Catalog):",
                Location = new Point(12, 22),
                Size = new Size(260, 20),
                Checked = true,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbHarvest.CheckedChanged += RuleSource_Changed;

            var lblHCategory = new Label { Text = "Target Category:", Location = new Point(32, 48), Size = new Size(100, 18) };
            _txtHarvestCategory = new TextBox { Text = "Faction Focus", Location = new Point(135, 45), Size = new Size(160, 23) };

            var lblHAttr = new Label { Text = "Attribute:", Location = new Point(305, 48), Size = new Size(60, 18) };
            _cmbHarvestAttribute = new ComboBox
            {
                Location = new Point(365, 45),
                Size = new Size(130, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbHarvestAttribute.SelectedIndexChanged += CmbHarvestAttribute_SelectedIndexChanged;

            var lblHVal = new Label { Text = "Value:", Location = new Point(505, 48), Size = new Size(42, 18) };
            _cmbHarvestValue = new ComboBox
            {
                Location = new Point(547, 45),
                Size = new Size(135, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 2. Custom Regex Expression
            _rbQuick = new RadioButton
            {
                Text = "Custom Regex Expression:",
                Location = new Point(12, 80),
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbQuick.CheckedChanged += RuleSource_Changed;

            var lblQCategory = new Label { Text = "Category:", Location = new Point(32, 105), Size = new Size(65, 18) };
            _txtRegexCategory = new TextBox { Text = "Custom Trait", Location = new Point(97, 102), Size = new Size(150, 23), Enabled = false };

            var lblQTag = new Label { Text = "Tag Name:", Location = new Point(255, 105), Size = new Size(68, 18) };
            _txtRegexTag = new TextBox { Text = "$1", Location = new Point(325, 102), Size = new Size(130, 23), Enabled = false };

            var lblQPattern = new Label { Text = "Pattern:", Location = new Point(465, 105), Size = new Size(50, 18) };
            _txtRegexPattern = new TextBox { Text = @"(?i)Dwarf factions?", Location = new Point(515, 102), Size = new Size(167, 23), Enabled = false };

            // 3. Load File
            _rbFile = new RadioButton
            {
                Text = "Load Rules from File (.json):",
                Location = new Point(12, 137),
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _rbFile.CheckedChanged += RuleSource_Changed;

            _txtFilePath = new TextBox
            {
                Location = new Point(32, 162),
                Size = new Size(550, 23),
                ReadOnly = true,
                Enabled = false
            };

            _btnBrowseFile = new Button
            {
                Text = "Browse...",
                Location = new Point(590, 161),
                Size = new Size(92, 25),
                Enabled = false
            };
            _btnBrowseFile.Click += BtnBrowseFile_Click;

            grpSource.Controls.AddRange([
                _rbHarvest, lblHCategory, _txtHarvestCategory, lblHAttr, _cmbHarvestAttribute, lblHVal, _cmbHarvestValue,
                _rbQuick, lblQCategory, _txtRegexCategory, lblQTag, _txtRegexTag, lblQPattern, _txtRegexPattern,
                _rbFile, _txtFilePath, _btnBrowseFile
            ]);

            // Preview & actions
            _btnTest = new Button
            {
                Text = "Test / Preview Matches",
                Location = new Point(12, 228),
                Size = new Size(160, 28)
            };
            _btnTest.Click += BtnTest_Click;

            var lblScopeHint = new Label
            {
                Text = $"Scanning {_targetCards.Count} card(s) currently visible in the Master List.",
                Location = new Point(180, 233),
                Size = new Size(520, 20),
                ForeColor = Color.DimGray
            };

            _lstPreview = new ListView
            {
                Location = new Point(12, 262),
                Size = new Size(696, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lstPreview.Columns.Add("Card Name", 230);
            _lstPreview.Columns.Add("Set", 60);
            _lstPreview.Columns.Add("Category", 150);
            _lstPreview.Columns.Add("Extracted Tag", 230);

            _btnExportCsv = new Button
            {
                Text = "Export to CSV...",
                Location = new Point(330, 525),
                Size = new Size(130, 30)
            };
            _btnExportCsv.Click += BtnExportCsv_Click;

            _btnApply = new Button
            {
                Text = "Apply Tags to Catalog",
                Location = new Point(470, 525),
                Size = new Size(150, 30)
            };
            _btnApply.Click += BtnApply_Click;

            _btnClose = new Button
            {
                Text = "Close",
                Location = new Point(628, 525),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([grpSource, _btnTest, lblScopeHint, _lstPreview, _btnExportCsv, _btnApply, _btnClose]);
            CancelButton = _btnClose;

            PopulateHarvestAttributes();
        }

        private void PopulateHarvestAttributes()
        {
            _cmbHarvestAttribute.Items.Clear();
            var keys = _catalog.GetFilterKeys();
            foreach (var k in keys)
            {
                if (!string.IsNullOrWhiteSpace(k))
                {
                    _cmbHarvestAttribute.Items.Add(k);
                }
            }

            int defaultIdx = _cmbHarvestAttribute.FindStringExact("Secondary");
            if (defaultIdx >= 0) _cmbHarvestAttribute.SelectedIndex = defaultIdx;
            else if (_cmbHarvestAttribute.Items.Count > 0) _cmbHarvestAttribute.SelectedIndex = 0;
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

            int defaultIdx = _cmbHarvestValue.FindStringExact("Faction");
            if (defaultIdx >= 0) _cmbHarvestValue.SelectedIndex = defaultIdx;
            else if (_cmbHarvestValue.Items.Count > 0) _cmbHarvestValue.SelectedIndex = 0;
        }

        private void RuleSource_Changed(object sender, EventArgs e)
        {
            bool isHarvest = _rbHarvest.Checked;
            _txtHarvestCategory.Enabled = isHarvest;
            _cmbHarvestAttribute.Enabled = isHarvest;
            _cmbHarvestValue.Enabled = isHarvest;

            bool isQuick = _rbQuick.Checked;
            _txtRegexCategory.Enabled = isQuick;
            _txtRegexTag.Enabled = isQuick;
            _txtRegexPattern.Enabled = isQuick;

            bool isFile = _rbFile.Checked;
            _txtFilePath.Enabled = isFile;
            _btnBrowseFile.Enabled = isFile;
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

        private List<TaxonomyRule> GetActiveRules()
        {
            if (_rbHarvest.Checked)
            {
                string attr = _cmbHarvestAttribute.SelectedItem?.ToString() ?? string.Empty;
                string val = _cmbHarvestValue.SelectedItem?.ToString() ?? string.Empty;
                string cat = _txtHarvestCategory.Text.Trim();

                if (string.IsNullOrWhiteSpace(attr) || string.IsNullOrWhiteSpace(val))
                {
                    return [];
                }

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
            var rules = GetActiveRules();
            if (rules.Count == 0)
            {
                MessageBox.Show(this, "No active rules to evaluate.", "Test Rules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _lstPreview.BeginUpdate();
            _lstPreview.Items.Clear();

            var result = TaxonomyExtractionService.ExecuteRules(_targetCards, rules);

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
                MessageBox.Show(this, $"No cards in the current Master List ({_targetCards.Count} cards) matched the rule criteria.", "Test Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            var rules = GetActiveRules();
            if (rules.Count == 0)
            {
                MessageBox.Show(this, "No active rules to evaluate.", "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export Extraction Draft (CSV)",
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = "Entity_Extraction_Draft.csv"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                var result = TaxonomyExtractionService.ExecuteRules(_targetCards, rules);
                TaxonomyExtractionService.ExportDraftToCsv(_targetCards, result, sfd.FileName);
                MessageBox.Show(this, $"Extraction draft exported successfully to:\n{sfd.FileName}\n\nMatched {result.Matches.Count} card(s) out of {_targetCards.Count} in scope.", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            var rules = GetActiveRules();
            if (rules.Count == 0)
            {
                MessageBox.Show(this, "No active rules to evaluate.", "Apply Tags", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var (cardsTagged, tagsAssigned) = TaxonomyExtractionService.ApplyRulesDirectly(_targetCards, rules, _filterService);
            MessageBox.Show(this, $"Applied {tagsAssigned} tag assignment(s) across {cardsTagged} card(s) into your catalog taxonomy.", "Tags Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
    }
}