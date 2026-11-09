using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    public sealed class ExportTaxonomyDialog : Form
    {
        private readonly CardCatalogService _catalog;
        private readonly CardFilterService _filterService;
        private readonly ISet<string> _activeSets;
        private readonly IReadOnlyList<Card> _currentMasterCards;
        private readonly string _initialSlot1Attr;
        private readonly string _initialSlot2Attr;
        private readonly string _initialSlot3Cat;
        private readonly string _initialSlot4Cat;

        private readonly ComboBox _cmbPartition;
        private readonly Label _lblScopeCount;
        private readonly CheckedListBox _clbAttributes;
        private readonly CheckedListBox _clbCategories;
        private readonly Button _btnExport;
        private readonly Button _btnClose;

        private List<Card> _cardsInScope = [];
        private bool _isUpdatingPreset;

        public ExportTaxonomyDialog(
            CardCatalogService catalog,
            CardFilterService filterService,
            ISet<string> activeSets,
            IReadOnlyList<Card> currentMasterCards,
            string initialSlot1Attr = null,
            string initialSlot2Attr = null,
            string initialSlot3Cat = null,
            string initialSlot4Cat = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _activeSets = activeSets ?? new HashSet<string>();
            _currentMasterCards = currentMasterCards ?? [];
            _initialSlot1Attr = initialSlot1Attr;
            _initialSlot2Attr = initialSlot2Attr;
            _initialSlot3Cat = initialSlot3Cat;
            _initialSlot4Cat = initialSlot4Cat;

            Text = "Export Taxonomy Draft (CSV)";
            ClientSize = new Size(680, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            // ==========================================
            // TOP: PARTITION SELECTOR & SCOPE SUMMARY
            // ==========================================
            var grpPartition = new GroupBox
            {
                Text = "1. Select Card Scope / Partition",
                Location = new Point(14, 12),
                Size = new Size(652, 82)
            };

            var lblPartition = new Label
            {
                Text = "Target Partition:",
                Location = new Point(14, 24),
                Size = new Size(110, 20)
            };

            _cmbPartition = new ComboBox
            {
                Location = new Point(130, 21),
                Size = new Size(508, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbPartition.Items.AddRange([
                "Items & Gear (All 5 Item Types)",
                "Characters (Starting Pool & Recruits)",
                "Avatars (Wizards & Ringwraiths)",
                "Allies (Retinue & Staging)",
                "Factions & Strongholds",
                "Hazard Creatures (Standard & Dual-Mode)",
                "Hazard Events (Short, Long, Permanent)",
                "Resource Events (Short, Long, Permanent)",
                "Sites & Havens",
                "Regions (Terrain & Movement)",
                "Current Master List Filter",
                "All Cards in Active Sets"
            ]);
            _cmbPartition.SelectedIndexChanged += CmbPartition_SelectedIndexChanged;

            _lblScopeCount = new Label
            {
                Location = new Point(130, 51),
                Size = new Size(508, 20),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            grpPartition.Controls.AddRange([lblPartition, _cmbPartition, _lblScopeCount]);

            // ==========================================
            // MIDDLE LEFT: OFFICIAL ATTRIBUTES CHECKLIST
            // ==========================================
            var grpAttrs = new GroupBox
            {
                Text = "2. Official Attributes (Read-Only Reference in CSV)",
                Location = new Point(14, 102),
                Size = new Size(320, 395)
            };

            _clbAttributes = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(296, 325),
                CheckOnClick = true
            };

            var btnSelectAllAttrs = new Button
            {
                Text = "All",
                Location = new Point(12, 356),
                Size = new Size(60, 25)
            };
            btnSelectAllAttrs.Click += (s, e) => SetAllChecked(_clbAttributes, true);

            var btnClearAttrs = new Button
            {
                Text = "Clear",
                Location = new Point(78, 356),
                Size = new Size(60, 25)
            };
            btnClearAttrs.Click += (s, e) => SetAllChecked(_clbAttributes, false);

            grpAttrs.Controls.AddRange([_clbAttributes, btnSelectAllAttrs, btnClearAttrs]);

            // ==========================================
            // MIDDLE RIGHT: CUSTOM CATEGORIES CHECKLIST
            // ==========================================
            var grpCats = new GroupBox
            {
                Text = "3. Custom Categories (Editable Tags in CSV)",
                Location = new Point(346, 102),
                Size = new Size(320, 395)
            };

            _clbCategories = new CheckedListBox
            {
                Location = new Point(12, 24),
                Size = new Size(296, 325),
                CheckOnClick = true
            };

            var btnSelectAllCats = new Button
            {
                Text = "All",
                Location = new Point(12, 356),
                Size = new Size(60, 25)
            };
            btnSelectAllCats.Click += (s, e) => SetAllChecked(_clbCategories, true);

            var btnClearCats = new Button
            {
                Text = "Clear",
                Location = new Point(78, 356),
                Size = new Size(60, 25)
            };
            btnClearCats.Click += (s, e) => SetAllChecked(_clbCategories, false);

            var btnAddCat = new Button
            {
                Text = "+ Add Category...",
                Location = new Point(188, 356),
                Size = new Size(120, 25)
            };
            btnAddCat.Click += BtnAddCat_Click;

            grpCats.Controls.AddRange([_clbCategories, btnSelectAllCats, btnClearCats, btnAddCat]);

            // ==========================================
            // BOTTOM: ACTIONS
            // ==========================================
            _btnExport = new Button
            {
                Text = "Export to CSV...",
                Location = new Point(486, 510),
                Size = new Size(110, 32),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };
            _btnExport.Click += BtnExport_Click;

            _btnClose = new Button
            {
                Text = "Cancel",
                Location = new Point(602, 510),
                Size = new Size(64, 32),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([grpPartition, grpAttrs, grpCats, _btnExport, _btnClose]);
            AcceptButton = _btnExport;
            CancelButton = _btnClose;

            PopulateAvailableColumns();

            // Default to "Items & Gear" preset
            _cmbPartition.SelectedIndex = 0;
        }

        private void PopulateAvailableColumns()
        {
            _clbAttributes.Items.Clear();
            var allAttrs = _catalog.GetFilterKeys()
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var a in allAttrs)
            {
                _clbAttributes.Items.Add(a);
            }

            _clbCategories.Items.Clear();
            var allCats = _filterService.GetCustomKeyNames()
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Ensure baseline categories are always present even if unassigned in active memory
            string[] standardCategories = [
                "Role", "Target Species", "Target Item", "Prerequisite", "Prerequisite For",
                "Site Requirement", "Region Requirement", "Home Region", "Faction Bonus",
                "Combat Bonus", "Environment Stance", "Region Type", "Adjacent Sites"
            ];

            foreach (var std in standardCategories)
            {
                if (!allCats.Contains(std, StringComparer.OrdinalIgnoreCase))
                {
                    allCats.Add(std);
                }
            }

            allCats.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var c in allCats)
            {
                _clbCategories.Items.Add(c);
            }
        }

        private void CmbPartition_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingPreset) return;
            _isUpdatingPreset = true;

            try
            {
                string choice = _cmbPartition.SelectedItem?.ToString() ?? "";
                ResolveCardsInScope(choice);

                // Update scope summary note
                string setsJoined = _activeSets.Count > 0 ? string.Join(", ", _activeSets) : "All Sets";
                _lblScopeCount.Text = $"Cards in scope: {_cardsInScope.Count}  |  Active Expansions: [{setsJoined}]";

                // Auto-configure recommended columns based on selected partition
                ApplyRecommendedColumns(choice);
            }
            finally
            {
                _isUpdatingPreset = false;
            }
        }

        private void ResolveCardsInScope(string choice)
        {
            var basePool = _catalog.Cards.Where(c => _activeSets.Count == 0 || _activeSets.Contains(c.Set));

            _cardsInScope = choice switch
            {
                "Items & Gear (All 5 Item Types)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                                        c.Secondary != null && c.Secondary.Contains("Item", StringComparison.OrdinalIgnoreCase))],

                "Characters (Starting Pool & Recruits)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(c.Secondary, "Avatar", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(c.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase))],

                "Avatars (Wizards & Ringwraiths)" =>
                    [.. basePool.Where(c => string.Equals(c.Secondary, "Avatar", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase))],

                "Allies (Retinue & Staging)" =>
                    [.. basePool.Where(c => string.Equals(c.Secondary, "Ally", StringComparison.OrdinalIgnoreCase))],

                "Factions & Strongholds" =>
                    [.. basePool.Where(c => string.Equals(c.Secondary, "Faction", StringComparison.OrdinalIgnoreCase))],

                "Hazard Creatures (Standard & Dual-Mode)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
                                        c.Secondary != null && c.Secondary.Contains("Creature", StringComparison.OrdinalIgnoreCase))],

                "Hazard Events (Short, Long, Permanent)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
                                        c.Secondary != null && c.Secondary.Contains("Event", StringComparison.OrdinalIgnoreCase))],

                "Resource Events (Short, Long, Permanent)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
                                        c.Secondary != null && c.Secondary.Contains("Event", StringComparison.OrdinalIgnoreCase))],

                "Sites & Havens" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))],

                "Regions (Terrain & Movement)" =>
                    [.. basePool.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase))],

                "Current Master List Filter" =>
                    [.. _currentMasterCards],

                _ => [.. basePool]
            };

            _cardsInScope.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.WithNaturalSort().Compare(a.Name, b.Name));
        }

        private void ApplyRecommendedColumns(string choice)
        {
            SetAllChecked(_clbAttributes, false);
            SetAllChecked(_clbCategories, false);

            (string[] Attrs, string[] Cats) = choice switch
            {
                "Items & Gear (All 5 Item Types)" =>
                    (["Secondary", "Race", "Corruption", "MPs", "Specific"],
                     ["Role", "Target Species", "Prerequisite", "Prerequisite For"]),

                "Characters (Starting Pool & Recruits)" =>
                    (["Race", "Subrace", "Skill", "Mind", "Direct", "Prowess", "Body", "Corruption", "Home"],
                     ["Home Region", "Faction Bonus", "Combat Bonus"]),

                "Avatars (Wizards & Ringwraiths)" =>
                    (["Race", "Mind", "Direct", "Prowess", "Body", "Corruption", "Home", "Skill"],
                     ["Home Region"]),

                "Allies (Retinue & Staging)" =>
                    (["Mind", "Prowess", "Body", "Corruption", "MPs"],
                     ["Site Requirement", "Region Requirement", "Role"]),

                "Factions & Strongholds" =>
                    (["Race", "MPs", "Specific"],
                     ["Site Requirement", "Prerequisite"]),

                "Hazard Creatures (Standard & Dual-Mode)" =>
                    (["Secondary", "Race", "Prowess", "Body", "Strikes"],
                     ["Region Requirement", "Site Requirement", "Environment Stance", "Role", "Target Species"]),

                "Hazard Events (Short, Long, Permanent)" =>
                    (["Secondary", "Specific"],
                     ["Role", "Environment Stance", "Region Requirement", "Site Requirement", "Target Species", "Target Item"]),

                "Resource Events (Short, Long, Permanent)" =>
                    (["Secondary", "Skill", "Specific"],
                     ["Role", "Environment Stance", "Prerequisite", "Prerequisite For", "Site Requirement", "Region Requirement"]),

                "Sites & Havens" =>
                    (["Site", "Region", "Haven", "MinorItem", "MajorItem", "GreaterItem", "GoldRing", "Hoard"],
                     ["Adjacent Sites", "Automatic Attack", "Region Type"]),

                "Regions (Terrain & Movement)" =>
                    (["Region"],
                     ["Region Type", "Adjacent Regions"]),

                "Current Master List Filter" =>
                    (GetMasterListInitialAttributes(),
                     GetMasterListInitialCategories()),

                _ =>
                    (["Primary", "Secondary", "Race"],
                     ["Role"])
            };

            foreach (var a in Attrs) SetItemCheckedByName(_clbAttributes, a, true);
            foreach (var c in Cats) SetItemCheckedByName(_clbCategories, c, true);
        }

        private string[] GetMasterListInitialAttributes()
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(_initialSlot1Attr)) list.Add(_initialSlot1Attr);
            if (!string.IsNullOrWhiteSpace(_initialSlot2Attr) && !list.Contains(_initialSlot2Attr, StringComparer.OrdinalIgnoreCase)) list.Add(_initialSlot2Attr);
            return list.Count > 0 ? [.. list] : ["Primary"];
        }

        private string[] GetMasterListInitialCategories()
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(_initialSlot3Cat)) list.Add(_initialSlot3Cat);
            if (!string.IsNullOrWhiteSpace(_initialSlot4Cat) && !list.Contains(_initialSlot4Cat, StringComparer.OrdinalIgnoreCase)) list.Add(_initialSlot4Cat);
            return list.Count > 0 ? [.. list] : ["Role"];
        }

        private static void SetAllChecked(CheckedListBox clb, bool isChecked)
        {
            for (int i = 0; i < clb.Items.Count; i++)
            {
                clb.SetItemChecked(i, isChecked);
            }
        }

        private static void SetItemCheckedByName(CheckedListBox clb, string name, bool isChecked)
        {
            for (int i = 0; i < clb.Items.Count; i++)
            {
                if (string.Equals(clb.Items[i].ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    clb.SetItemChecked(i, isChecked);
                    break;
                }
            }
        }

        private void BtnAddCat_Click(object sender, EventArgs e)
        {
            string newCat = PromptDialog.Show(this, "Add Custom Category", "Enter category name to register and export:");
            if (string.IsNullOrWhiteSpace(newCat)) return;

            newCat = newCat.Trim();
            _filterService.AddCustomKeyName(newCat);

            if (!_clbCategories.Items.Cast<string>().Contains(newCat, StringComparer.OrdinalIgnoreCase))
            {
                int idx = _clbCategories.Items.Add(newCat);
                _clbCategories.SetItemChecked(idx, true);
            }
            else
            {
                SetItemCheckedByName(_clbCategories, newCat, true);
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_cardsInScope.Count == 0)
            {
                MessageBox.Show(this, "No cards are in scope for the selected partition.", "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var chosenAttrs = _clbAttributes.CheckedItems.Cast<string>().ToList();
            var chosenCats = _clbCategories.CheckedItems.Cast<string>().ToList();

            if (chosenCats.Count == 0)
            {
                var res = MessageBox.Show(this,
                    "No custom categories are selected.\n\nDo you want to proceed and export an empty '[Insert Category Name]' template column?",
                    "No Categories Selected",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes) return;
            }

            string rawChoice = _cmbPartition.SelectedItem?.ToString() ?? "Custom";
            string cleanPartitionName = rawChoice.Split('(')[0].Trim().Replace(" ", "_");

            string setsSuffix = _activeSets.Count == 1 ? $"_{_activeSets.First()}" : "";
            string defaultFileName = $"{cleanPartitionName}{setsSuffix}_Taxonomy.csv";

            using var sfd = new SaveFileDialog
            {
                Title = $"{Constants.AppTitle} - Export Taxonomy Draft (CSV)",
                DefaultExt = "csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                FileName = defaultFileName
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                TaxonomyExtractionService.ExportTemplateDraftToCsv(
                    _cardsInScope,
                    _filterService,
                    sfd.FileName,
                    chosenAttrs,
                    chosenCats);

                MessageBox.Show(this,
                    $"Partition taxonomy draft exported successfully!\n\n" +
                    $"File: {sfd.FileName}\n" +
                    $"Cards Exported: {_cardsInScope.Count}\n" +
                    $"Official Reference Columns ({chosenAttrs.Count}): {(chosenAttrs.Count > 0 ? string.Join(", ", chosenAttrs) : "(None)")}\n" +
                    $"Editable Category Columns ({chosenCats.Count}): {(chosenCats.Count > 0 ? string.Join(", ", chosenCats) : "[Insert Category Name]")}\n\n" +
                    "Review or edit tags in Excel/Sheets, then re-import via:\n" +
                    "Taxonomy > Import Taxonomy (CSV)...",
                    Constants.AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
            }
        }
    }
}