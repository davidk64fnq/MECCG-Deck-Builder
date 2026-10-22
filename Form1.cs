using NaturalSort.Extension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    internal partial class Form1 : Form
    {
        private sealed class FilterSlot(int index, FilterType type, ComboBox keyBox, ComboBox valueBox)
        {
            public int Index { get; } = index;
            public FilterType Type { get; } = type;
            public ComboBox KeyBox { get; } = keyBox;
            public ComboBox ValueBox { get; } = valueBox;

            public string CurrentKey => KeyBox.Text.Trim();
            public string CurrentValue => ValueBox.Text.Trim();

            public FilterCriterion ToCriterion() => new(Type, CurrentKey, CurrentValue);
        }

        private readonly CardCatalogService _catalogService = new();
        private readonly CardFilterService _filterService = new();
        private readonly Deck _currentDeck = new();
        private readonly CardImageCache _cardImageCache = new();

        private readonly FilterSlot[] _filterSlots;
        private bool _isUpdatingFilters;

        private List<Card> _masterCards = [];
        private Card _inspectedCard;
        private ListBox _callingListBox;
        private int _selectedMasterIndex;
        private int _selectedTabIndex;
        private CancellationTokenSource _imageLoadCts;
        private Rectangle _dragBoxFromMouseDown = Rectangle.Empty;
        private string _lastDeckRulesFilePath;

        internal Form1()
        {
            InitializeComponent();

            _filterSlots = [
                new FilterSlot(0, FilterType.Cardnum, ComboBoxKey1, ComboBoxValue1),
                new FilterSlot(1, FilterType.Cardnum, ComboBoxKey2, ComboBoxValue2),
                new FilterSlot(2, FilterType.Custom, ComboBoxKey3, ComboBoxValue3),
                new FilterSlot(3, FilterType.Custom, ComboBoxKey4, ComboBoxValue4)
            ];

            InitializeFilterClearing();
            InitializeTaxonomyInspector();

            _catalogService.WarningOccurred += (s, msg) =>
                MessageBox.Show(msg, Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Shown += Form1_Shown;
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            Enabled = false;
            Text = $"{Constants.AppTitle} - Initializing Card Catalog...";

            try
            {
                await _catalogService.InitializeAsync();
                LoadBaselineRulesTags(); // Auto-load Rules_Support_Tags.json if present
                CreateMenus();
                RefreshFilterKeyDropdowns();
                PopulateAssignCategories();
                UpdateFacetedFilters();
                UpdateFormTitle();
                SetRightPaneMode(false); // Default to Deck Builder tab view
            }
            finally
            {
                Enabled = true;
                UpdateFormTitle();
            }
        }

        private void LoadBaselineRulesTags()
        {
            try
            {
                string path = StoragePathService.ResolveDataFilePath("Rules_Support_Tags.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var dto = JsonConvert.DeserializeObject<OpenCloseFilter>(json);
                    if (dto != null)
                    {
                        _filterService.LoadCustomFilters(dto.Filters, dto.Cards, _catalogService);
                    }
                }
            }
            catch
            {
                // Baseline load fails gracefully without blocking catalog startup
            }
        }

        private void CreateMenus()
        {
            // Keep Catalog dropdown open while ticking expansion sets
            ToolStripMenuCatalog.DropDown.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                {
                    e.Cancel = true;
                }
            };

            var resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            ToolStripMenuFilterOpen.Image = (Image)resources.GetObject("OpenToolStripMenuItem.Image");
            ToolStripMenuFilterSave.Image = (Image)resources.GetObject("ExportToolStripMenuItem.Image");

            PopulateSetMenuItems();
        }

        private void PopulateSetMenuItems()
        {
            _isUpdatingFilters = true;
            try
            {
                var existingSetItems = ToolStripMenuCatalog.DropDownItems
                    .OfType<ToolStripMenuItem>()
                    .Where(item => item.Tag is string)
                    .ToList();

                foreach (var item in existingSetItems)
                {
                    ToolStripMenuCatalog.DropDownItems.Remove(item);
                }

                var savedSets = new HashSet<string>(UserSettingsService.Current.SelectedSets ?? [], StringComparer.OrdinalIgnoreCase);
                int insertIndex = ToolStripMenuCatalog.DropDownItems.Count - 2;

                for (int i = 0; i < _catalogService.Sets.Count; i++)
                {
                    var set = _catalogService.Sets[i];
                    var menuItem = new ToolStripMenuItem(set.Name)
                    {
                        Tag = set.Code,
                        CheckOnClick = true
                    };
                    menuItem.CheckedChanged += ToolStripMenuSet_CheckedChanged;

                    bool shouldCheck = savedSets.Count > 0
                        ? savedSets.Contains(set.Code)
                        : (i == 0);

                    if (shouldCheck)
                    {
                        menuItem.Checked = true;
                        _currentDeck.IncludedSets.Add(set.Code);
                    }

                    ToolStripMenuCatalog.DropDownItems.Insert(insertIndex++, menuItem);
                }

                if (_currentDeck.IncludedSets.Count == 0 && _catalogService.Sets.Count > 0)
                {
                    var firstSet = _catalogService.Sets[0];
                    if (ToolStripMenuCatalog.DropDownItems.Count > 3 && ToolStripMenuCatalog.DropDownItems[3] is ToolStripMenuItem firstItem)
                    {
                        firstItem.Checked = true;
                    }
                    _currentDeck.IncludedSets.Add(firstSet.Code);
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private void PersistSelectedSets()
        {
            UserSettingsService.Current.SelectedSets = [.. _currentDeck.IncludedSets];
            UserSettingsService.SaveSettings();
        }

        private void RefreshFilterKeyDropdowns()
        {
            _isUpdatingFilters = true;
            try
            {
                SetKeyDropdownDataSource(ComboBoxKey1, _catalogService.GetFilterKeys());
                SetKeyDropdownDataSource(ComboBoxKey2, _catalogService.GetFilterKeys());
                SetKeyDropdownDataSource(ComboBoxKey3, _filterService.GetCustomKeyNames());
                SetKeyDropdownDataSource(ComboBoxKey4, _filterService.GetCustomKeyNames());
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private static void SetKeyDropdownDataSource(ComboBox cb, IReadOnlyList<string> items)
        {
            string current = cb.Text.Trim();
            cb.DataSource = null;
            cb.DataSource = items;
            int idx = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0)
            {
                cb.SelectedIndex = idx;
            }
            else if (items.Count > 0)
            {
                cb.SelectedIndex = 0;
            }
            else
            {
                cb.SelectedIndex = -1;
            }
        }

        #region RIGHT_PANE_MODE_AND_INSPECTOR

        private void InitializeTaxonomyInspector()
        {
            BtnModeDeck.Click += (s, e) => SetRightPaneMode(false);
            BtnModeTaxonomy.Click += (s, e) => SetRightPaneMode(true);

            CmbAssignCategory.SelectedIndexChanged += (s, e) => PopulateAssignTagsForCategory();
            CmbAssignCategory.TextChanged += (s, e) => PopulateAssignTagsForCategory();

            // Handle <Enter> key for rapid keyboard assignment
            CmbAssignCategory.KeyDown += CmbAssign_KeyDown;
            CmbAssignTag.KeyDown += CmbAssign_KeyDown;

            BtnAssignTag.Click += BtnAssignTag_Click;
            BtnRemoveTag.Click += BtnRemoveTag_Click;
            BtnAuthorTag.Click += BtnAuthorTag_Click;
        }

        private void CmbAssign_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // Silences the Windows ding sound

                if (sender == CmbAssignCategory && string.IsNullOrWhiteSpace(CmbAssignTag.Text))
                {
                    // If hitting Enter on an empty Category box, jump straight to Tag
                    CmbAssignTag.Focus();
                }
                else
                {
                    // Trigger the "+ Assign" button directly
                    BtnAssignTag.PerformClick();
                }
            }
        }

        private void SetRightPaneMode(bool isTaxonomy)
        {
            if (isTaxonomy)
            {
                BtnModeTaxonomy.BackColor = SystemColors.Window;
                BtnModeTaxonomy.Font = new Font(Font, FontStyle.Bold);
                BtnModeDeck.BackColor = SystemColors.Control;
                BtnModeDeck.Font = new Font(Font, FontStyle.Regular);

                TabControlDeck.Visible = false;
                PanelTaxonomy.Visible = true;

                PopulateAssignCategories();
                SyncAssignCategoryWithFilterSlots();
                UpdateTaxonomyView(_inspectedCard);
            }
            else
            {
                BtnModeDeck.BackColor = SystemColors.Window;
                BtnModeDeck.Font = new Font(Font, FontStyle.Bold);
                BtnModeTaxonomy.BackColor = SystemColors.Control;
                BtnModeTaxonomy.Font = new Font(Font, FontStyle.Regular);

                PanelTaxonomy.Visible = false;
                TabControlDeck.Visible = true;
            }
        }

        private void SyncAssignCategoryWithFilterSlots()
        {
            string slot3Cat = ComboBoxKey3.Text.Trim();
            string slot3Val = ComboBoxValue3.Text.Trim();
            string slot4Cat = ComboBoxKey4.Text.Trim();
            string slot4Val = ComboBoxValue4.Text.Trim();

            if (!string.IsNullOrWhiteSpace(slot3Cat))
            {
                CmbAssignCategory.Text = slot3Cat;
                PopulateAssignTagsForCategory(!string.IsNullOrWhiteSpace(slot3Val) ? slot3Val : null);
            }
            else if (!string.IsNullOrWhiteSpace(slot4Cat))
            {
                CmbAssignCategory.Text = slot4Cat;
                PopulateAssignTagsForCategory(!string.IsNullOrWhiteSpace(slot4Val) ? slot4Val : null);
            }
        }

        private void PopulateAssignCategories()
        {
            string prevCategory = CmbAssignCategory.Text.Trim();
            CmbAssignCategory.Items.Clear();

            var keys = _filterService.GetCustomKeyNames().Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            foreach (var k in keys)
            {
                CmbAssignCategory.Items.Add(k);
            }

            // 1. Only preserve previous category if it still exists in the taxonomy!
            var survivingCategory = keys.FirstOrDefault(k => string.Equals(k, prevCategory, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(survivingCategory))
            {
                CmbAssignCategory.Text = survivingCategory;
            }
            else if (keys.Count > 0)
            {
                // Fall back to Slot 3 (if valid), then Slot 4 (if valid), or index 0
                string slot3 = ComboBoxKey3.Text.Trim();
                string slot4 = ComboBoxKey4.Text.Trim();

                var matchSlot3 = keys.FirstOrDefault(k => string.Equals(k, slot3, StringComparison.OrdinalIgnoreCase));
                var matchSlot4 = keys.FirstOrDefault(k => string.Equals(k, slot4, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchSlot3))
                {
                    CmbAssignCategory.Text = matchSlot3;
                }
                else if (!string.IsNullOrEmpty(matchSlot4))
                {
                    CmbAssignCategory.Text = matchSlot4;
                }
                else
                {
                    CmbAssignCategory.SelectedIndex = 0;
                }
            }
            else
            {
                CmbAssignCategory.SelectedIndex = -1;
                CmbAssignCategory.Text = string.Empty;
            }

            PopulateAssignTagsForCategory();
        }

        private void PopulateAssignTagsForCategory(string tagToSelect = null)
        {
            string category = CmbAssignCategory.Text.Trim();
            string targetTag = !string.IsNullOrWhiteSpace(tagToSelect)
                ? tagToSelect.Trim().TrimStart('✓', ' ')
                : CmbAssignTag.Text.Trim().TrimStart('✓', ' ');

            CmbAssignTag.Items.Clear();

            if (string.IsNullOrWhiteSpace(category))
            {
                CmbAssignTag.SelectedIndex = -1;
                CmbAssignTag.Text = string.Empty;
                return;
            }

            var tags = _filterService.GetCustomKeyValues(category).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var cardTags = (_inspectedCard != null)
                ? _filterService.GetCardTagsForCategory(_inspectedCard.Id, category)
                : [];

            foreach (var t in tags)
            {
                bool isAssigned = cardTags.Contains(t, StringComparer.OrdinalIgnoreCase);
                string displayTag = isAssigned ? $"✓ {t}" : t;
                CmbAssignTag.Items.Add(displayTag);
            }

            // 1. Prioritize target tag ONLY if it exists in the surviving tags list
            if (!string.IsNullOrEmpty(targetTag))
            {
                int matchIdx = -1;
                for (int i = 0; i < CmbAssignTag.Items.Count; i++)
                {
                    string raw = CmbAssignTag.Items[i].ToString().TrimStart('✓', ' ');
                    if (string.Equals(raw, targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        matchIdx = i;
                        break;
                    }
                }
                if (matchIdx >= 0)
                {
                    CmbAssignTag.SelectedIndex = matchIdx;
                    return;
                }
            }

            // 2. Otherwise default to the card's first assigned tag if any
            if (cardTags.Count > 0)
            {
                int matchIdx = -1;
                for (int i = 0; i < CmbAssignTag.Items.Count; i++)
                {
                    string raw = CmbAssignTag.Items[i].ToString().TrimStart('✓', ' ');
                    if (string.Equals(raw, cardTags[0], StringComparison.OrdinalIgnoreCase))
                    {
                        matchIdx = i;
                        break;
                    }
                }
                if (matchIdx >= 0)
                {
                    CmbAssignTag.SelectedIndex = matchIdx;
                    return;
                }
            }

            // 3. Fall back to index 0 if tags exist, otherwise cleanly clear the box
            if (CmbAssignTag.Items.Count > 0)
            {
                CmbAssignTag.SelectedIndex = 0;
            }
            else
            {
                CmbAssignTag.SelectedIndex = -1;
                CmbAssignTag.Text = string.Empty;
            }
        }

        private void UpdateTaxonomyView(Card card)
        {
            if (card == null)
            {
                LblTaxonomyCardTitle.Text = "(No card selected)";
                RichTextBoxTaxonomy.Clear();
                return;
            }

            LblTaxonomyCardTitle.Text = $"{card.Name} ({card.Set})";

            RichTextBoxTaxonomy.SuspendLayout();
            RichTextBoxTaxonomy.Clear();

            Color headerColor = Color.FromArgb(26, 54, 93);    // Slate Navy #1A365D
            Color keyColor = Color.FromArgb(74, 85, 104);       // Muted Gray #4A5568
            Color valColor = Color.FromArgb(26, 32, 44);        // Crisp Dark #1A202C
            Color emptyColor = Color.FromArgb(160, 174, 192);   // Italic Muted Gray #A0AEC0
            Color tagColor = Color.FromArgb(19, 78, 74);        // Deep Teal #134E4A

            using var headerFont = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            using var monoRegularFont = new Font("Consolas", 8.25f, FontStyle.Regular);
            using var monoBoldFont = new Font("Consolas", 8.25f, FontStyle.Bold);
            using var italicFont = new Font("Consolas", 8.25f, FontStyle.Italic);

            void AppendHeader(string text)
            {
                RichTextBoxTaxonomy.SelectionStart = RichTextBoxTaxonomy.TextLength;
                RichTextBoxTaxonomy.SelectionLength = 0;
                RichTextBoxTaxonomy.SelectionColor = headerColor;
                RichTextBoxTaxonomy.SelectionFont = headerFont;
                RichTextBoxTaxonomy.AppendText(text);
            }

            // --- 1. OFFICIAL ATTRIBUTES ---
            AppendHeader("=== OFFICIAL ATTRIBUTES ===" + Environment.NewLine);

            // Suppress legacy placeholder Secondary when it merely duplicates Primary (e.g., site/Site, region/Region)
            string displaySecondary = string.Equals(card.Primary, card.Secondary, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : card.Secondary;

            // Suppress composite unparsed Playable string for official published sets to save vertical space;
            // preserve it for Dreamcards that may use custom text strings.
            string displayPlayable = card.Dreamcard
                ? card.Playable
                : string.Empty;

            var attrPairs = new List<(string Key, string Value)>
    {
        ("Primary", card.Primary),
        ("Secondary", displaySecondary),
        ("Alignment", card.Alignment),
        ("Race", card.Race),
        ("Subrace", card.Subrace),
        ("Skill", card.Skill),
        ("Mind", card.Mind),
        ("Direct", card.Direct),
        ("Prowess", card.Prowess),
        ("Body", card.Body),
        ("Corruption", card.Corruption),
        ("Strikes", card.Strikes),
        ("Home", card.Home),
        ("Site", card.Site),
        ("Region", card.Region),
        ("Haven", card.Haven),
        ("MPs", card.MPs),
        ("Unique", card.Unique),
        ("Playable", displayPlayable),
        ("GoldRing", card.GoldRing),
        ("GreaterItem", card.GreaterItem),
        ("MajorItem", card.MajorItem),
        ("MinorItem", card.MinorItem),
        ("Information", card.Information),
        ("Palantiri", card.Palantiri),
        ("Scroll", card.Scroll),
        ("Hoard", card.Hoard),
        ("Specific", card.Specific),
        ("Artist", card.Artist)
    };

            var nonBlankAttrs = attrPairs.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToList();
            if (nonBlankAttrs.Count > 0)
            {
                int maxKeyLen = nonBlankAttrs.Max(p => p.Key.Length);
                foreach (var (k, v) in nonBlankAttrs)
                {
                    AppendWrappedMonospaceLine(k, v, maxKeyLen, keyColor, valColor, monoRegularFont, monoBoldFont);
                }
            }
            else
            {
                AppendSingleLine("  (None)" + Environment.NewLine, emptyColor, italicFont);
            }

            AppendSingleLine(Environment.NewLine, keyColor, monoRegularFont);

            // --- 2. CUSTOM TAXONOMY TAGS ---
            AppendHeader("=== ASSIGNED CUSTOM TAGS ===" + Environment.NewLine);

            var customPairs = _filterService.GetCardCustomFilterPairs(card.Id);
            if (customPairs.Count > 0)
            {
                int maxCatLen = Math.Min(16, customPairs.Max(p => p.Key.Length));
                foreach (var kvp in customPairs)
                {
                    AppendWrappedMonospaceLine(kvp.Key, kvp.Value, maxCatLen, keyColor, tagColor, monoRegularFont, monoBoldFont);
                }
            }
            else
            {
                AppendSingleLine("  (No custom tags assigned)" + Environment.NewLine, emptyColor, italicFont);
            }

            RichTextBoxTaxonomy.SelectionStart = 0;
            RichTextBoxTaxonomy.ScrollToCaret();
            RichTextBoxTaxonomy.ResumeLayout();
        }

        private void AppendSingleLine(string text, Color color, Font font)
        {
            RichTextBoxTaxonomy.SelectionStart = RichTextBoxTaxonomy.TextLength;
            RichTextBoxTaxonomy.SelectionLength = 0;
            RichTextBoxTaxonomy.SelectionColor = color;
            RichTextBoxTaxonomy.SelectionFont = font;
            RichTextBoxTaxonomy.AppendText(text);
        }

        private void AppendWrappedMonospaceLine(
            string key,
            string value,
            int maxKeyLen,
            Color keyColor,
            Color valColor,
            Font regularFont,
            Font boldFont)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            string paddedKey = key.PadRight(maxKeyLen);
            string prefix = $"  {paddedKey} : ";
            string indent = new(' ', prefix.Length);

            // 38 characters max per line prevents horizontal clipping on 306px pane width
            int availWidth = Math.Max(12, 38 - prefix.Length);
            var lines = WrapTokensToLines(value, availWidth);

            for (int i = 0; i < lines.Count; i++)
            {
                AppendSingleLine(i == 0 ? prefix : indent, keyColor, regularFont);
                AppendSingleLine(lines[i] + Environment.NewLine, valColor, boldFont);
            }
        }

        private static List<string> WrapTokensToLines(string text, int availWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return lines;

            // Semicolon-delimited lists (custom tags, resolved sites, skills)
            if (text.Contains(';'))
            {
                var tokens = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var cur = new System.Text.StringBuilder();

                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i];
                    bool isLast = (i == tokens.Length - 1);
                    string tokenWithSep = isLast ? token : token + ";";

                    if (cur.Length == 0)
                    {
                        cur.Append(tokenWithSep);
                    }
                    else if (cur.Length + 1 + tokenWithSep.Length <= availWidth)
                    {
                        cur.Append(' ').Append(tokenWithSep);
                    }
                    else
                    {
                        lines.Add(cur.ToString());
                        cur.Clear();
                        cur.Append(tokenWithSep);
                    }
                }

                if (cur.Length > 0)
                {
                    lines.Add(cur.ToString());
                }

                return lines;
            }

            // Word-wrap long unbroken strings (e.g. Home site, Skill descriptions)
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordCur = new System.Text.StringBuilder();

            foreach (var w in words)
            {
                if (wordCur.Length == 0)
                {
                    wordCur.Append(w);
                }
                else if (wordCur.Length + 1 + w.Length <= availWidth)
                {
                    wordCur.Append(' ').Append(w);
                }
                else
                {
                    lines.Add(wordCur.ToString());
                    wordCur.Clear();
                    wordCur.Append(w);
                }
            }

            if (wordCur.Length > 0)
            {
                lines.Add(wordCur.ToString());
            }

            return lines;
        }

        private void BtnAssignTag_Click(object sender, EventArgs e)
        {
            if (_inspectedCard == null)
            {
                MessageBox.Show(this, "Please select a card first.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rawCategory = CmbAssignCategory.Text.Trim();
            string tagInput = CmbAssignTag.Text.Trim().TrimStart('✓', ' ');

            if (string.IsNullOrWhiteSpace(rawCategory))
            {
                MessageBox.Show(this, "Please enter or select a category.", "Assign Tag", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CmbAssignCategory.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(tagInput))
            {
                MessageBox.Show(this, "Please enter or select a tag.", "Assign Tag", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CmbAssignTag.Focus();
                return;
            }

            // 1. Canonicalize Category
            var allCategories = _filterService.GetCustomKeyNames().Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            string category = allCategories.FirstOrDefault(c => string.Equals(c, rawCategory, StringComparison.OrdinalIgnoreCase)) ?? rawCategory;

            if (!allCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                var res = MessageBox.Show(this,
                    $"Category '{category}' does not exist in your taxonomy.\n\nDo you want to create this new category?",
                    "New Category Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes) return;
            }

            // 2. Canonicalize each tag and guard against accidental typos
            var knownCategoryTags = _filterService.GetCustomKeyValues(category).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var rawTagParts = tagInput.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tagsToAssign = new List<string>();

            foreach (var rawTag in rawTagParts)
            {
                var canonicalMatch = knownCategoryTags.FirstOrDefault(t => string.Equals(t, rawTag, StringComparison.OrdinalIgnoreCase));
                if (canonicalMatch != null)
                {
                    // Existing tag: snaps to official catalog casing automatically
                    tagsToAssign.Add(canonicalMatch);
                }
                else
                {
                    // Unrecognized tag: prompt user to confirm it's intentional (not a typo)
                    var res = MessageBox.Show(this,
                        $"Tag '{rawTag}' is not currently registered under category '{category}'.\n\nDo you want to create and assign this new tag?",
                        "New Tag Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (res == DialogResult.Yes)
                    {
                        tagsToAssign.Add(rawTag);
                    }
                }
            }

            if (tagsToAssign.Count == 0) return;

            // 3. Assign tags
            foreach (var tag in tagsToAssign)
            {
                _filterService.AddCustomKeyName(category);
                _filterService.AddCustomKeyValue(category, tag);
                _filterService.AppendCardCustomTag(_inspectedCard.Id, category, tag);
            }

            RefreshFilterKeyDropdowns();
            UpdateFacetedFilters();
            UpdateTaxonomyView(_inspectedCard);
            PopulateAssignCategories();
            CmbAssignCategory.Text = category;

            // Select the first assigned tag with correct checkmark and catalog casing
            PopulateAssignTagsForCategory(tagsToAssign[0]);
        }

        private void BtnRemoveTag_Click(object sender, EventArgs e)
        {
            if (_inspectedCard == null) return;

            string category = CmbAssignCategory.Text.Trim();
            string tag = CmbAssignTag.Text.Trim().TrimStart('✓', ' ');

            var cardTags = _filterService.GetCardTagsForCategory(_inspectedCard.Id, category);

            if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(tag) && cardTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                _filterService.RemoveCardCustomTag(_inspectedCard.Id, category, tag);
                UpdateFacetedFilters();
                UpdateTaxonomyView(_inspectedCard);

                // Re-populate and select the tag so it displays WITHOUT '✓ '
                PopulateAssignTagsForCategory(tag);
                return;
            }

            // Fallback context picker listing all tags currently on the card
            var allPairs = _filterService.GetCardCustomFilterPairs(_inspectedCard.Id);
            if (allPairs.Count == 0)
            {
                MessageBox.Show(this, $"No custom tags are currently assigned to '{_inspectedCard.Name}'.", "Remove Tag", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var removeMenu = new ContextMenuStrip();
            foreach (var (cat, val) in allPairs)
            {
                var catMenu = new ToolStripMenuItem(cat.Replace("&", "&&"));
                var tagList = val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var t in tagList)
                {
                    var item = new ToolStripMenuItem(t.Replace("&", "&&")) { Tag = (cat, t) };
                    item.Click += (s, ev) =>
                    {
                        if (((ToolStripMenuItem)s).Tag is (string c, string v))
                        {
                            _filterService.RemoveCardCustomTag(_inspectedCard.Id, c, v);
                            UpdateFacetedFilters();
                            UpdateTaxonomyView(_inspectedCard);
                            PopulateAssignTagsForCategory();
                        }
                    };
                    catMenu.DropDownItems.Add(item);
                }
                removeMenu.Items.Add(catMenu);
            }

            removeMenu.Show(BtnRemoveTag, new Point(0, BtnRemoveTag.Height));
        }

        private void BtnAuthorTag_Click(object sender, EventArgs e)
        {
            if (_inspectedCard == null)
            {
                MessageBox.Show(this, "Please select a card first.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Pass the currently active category into the dialog
            string initialCategory = CmbAssignCategory.Text.Trim();
            var result = AddTagDialog.Show(this, _inspectedCard.Name, _filterService, initialCategory);
            if (result.HasValue)
            {
                _filterService.AddCustomKeyName(result.Value.Key);
                _filterService.AddCustomKeyValue(result.Value.Key, result.Value.Value);
                _filterService.AppendCardCustomTag(_inspectedCard.Id, result.Value.Key, result.Value.Value);

                RefreshFilterKeyDropdowns();
                UpdateFacetedFilters();
                UpdateTaxonomyView(_inspectedCard);
                PopulateAssignCategories();
                CmbAssignCategory.Text = result.Value.Key;

                // Re-populate and select the new tag with '✓ '
                PopulateAssignTagsForCategory(result.Value.Value);
            }
        }

        #endregion

        #region MASTER_LIST_INTERACTION

        private void ListBoxMasterList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = ListBoxMaster.IndexFromPoint(e.Location);
                if (index < 0) return;

                ListBoxMaster.SelectedIndex = index;
                ListBox_SelectedIndexChanged(sender, e);

                Size dragSize = SystemInformation.DragSize;
                _dragBoxFromMouseDown = new Rectangle(
                    new Point(e.X - (dragSize.Width / 2), e.Y - (dragSize.Height / 2)),
                    dragSize);
            }
            else if (e.Button == MouseButtons.Right)
            {
                _dragBoxFromMouseDown = Rectangle.Empty;
                _selectedMasterIndex = ListBoxMaster.IndexFromPoint(e.Location);
                if (_selectedMasterIndex < 0 || _selectedMasterIndex >= _masterCards.Count) return;

                var card = _masterCards[_selectedMasterIndex];
                ToolStripMenuMasterCardname.Text = card.Name;

                SetToolStripMenuMasterCardnumFilters(card);
                SetToolStripMenuMasterCustomFilters(card);
                SetToolStripMenuMasterAddKeyValue(card);
                SetToolStripMenuMasterDeleteKeyValue(card);
            }
        }

        private void ListBoxMaster_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (_dragBoxFromMouseDown != Rectangle.Empty && !_dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    _dragBoxFromMouseDown = Rectangle.Empty;
                    if (ListBoxMaster.SelectedItem != null)
                    {
                        ListBoxMaster.DoDragDrop(ListBoxMaster.SelectedItem.ToString(), DragDropEffects.Copy);
                    }
                }
            }
        }

        private void SetToolStripMenuMasterCardnumFilters(Card card)
        {
            ToolStripMenuMasterCardnumFilters.DropDownItems.Clear();
            var pairs = new List<(string Key, string Value)>
            {
                ("Name", card.Name),
                ("Set", card.Set),
                ("FullCode", card.FullCode)
            };

            foreach (string key in CardCatalogService.FilterKeys)
            {
                string val = card.GetAttribute(key);
                if (!string.IsNullOrEmpty(val))
                {
                    pairs.Add((key, val));
                }
            }

            if (pairs.Count == 0) return;
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string escapedVal = pairs[i].Value.Replace("&", "&&");
                string line = $"{pairs[i].Key.PadRight(maxLen + 3)}{escapedVal}";
                var item = new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) };
                ToolStripMenuMasterCardnumFilters.DropDownItems.Add(item);
            }
        }

        private void SetToolStripMenuMasterCustomFilters(Card card)
        {
            var pairs = _filterService.GetCardCustomFilterPairs(card.Id);
            ContextMenuStripMaster.Items.RemoveByKey("Assigned Tags");

            if (pairs.Count == 0) return;

            var customMenu = new ToolStripMenuItem("Assigned Tags") { Name = "Assigned Tags" };
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string escapedKey = pairs[i].Key.Replace("&", "&&");
                string escapedVal = pairs[i].Value.Replace("&", "&&");
                string line = $"{escapedKey.PadRight(maxLen + 3)}{escapedVal}";
                customMenu.DropDownItems.Add(new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) });
            }

            ContextMenuStripMaster.Items.Add(customMenu);
        }

        private void SetToolStripMenuMasterAddKeyValue(Card card)
        {
            ContextMenuStripMaster.Items.RemoveByKey("Assign Tag");
            var keyNames = _filterService.GetCustomKeyNames();

            var addMenu = new ToolStripMenuItem("Assign Tag") { Name = "Assign Tag" };

            var addNewTagItem = new ToolStripMenuItem("+ Author New Tag...") { Font = new Font(Font, FontStyle.Bold) };
            addNewTagItem.Click += (s, ev) =>
            {
                var result = AddTagDialog.Show(this, card.Name, _filterService);
                if (result.HasValue)
                {
                    _filterService.AddCustomKeyName(result.Value.Key);
                    _filterService.AddCustomKeyValue(result.Value.Key, result.Value.Value);
                    _filterService.AppendCardCustomTag(card.Id, result.Value.Key, result.Value.Value);
                    RefreshFilterKeyDropdowns();
                    UpdateFacetedFilters();
                    UpdateTaxonomyView(_inspectedCard);
                    PopulateAssignCategories();
                }
            };
            addMenu.DropDownItems.Add(addNewTagItem);
            addMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (string keyName in keyNames)
            {
                if (string.IsNullOrWhiteSpace(keyName)) continue;

                var cardTags = _filterService.GetCardTagsForCategory(card.Id, keyName);
                var keyItem = new ToolStripMenuItem(keyName);

                var addNewValueItem = new ToolStripMenuItem("+ Add Tag...");
                addNewValueItem.Click += (s, ev) =>
                {
                    string val = PromptDialog.Show(this, $"Add Tag to '{keyName}'", $"Enter new tag for category '{keyName}':");
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        _filterService.AddCustomKeyValue(keyName, val);
                        _filterService.AppendCardCustomTag(card.Id, keyName, val);
                        RefreshFilterKeyDropdowns();
                        UpdateFacetedFilters();
                        UpdateTaxonomyView(_inspectedCard);
                        PopulateAssignCategories();
                    }
                };
                keyItem.DropDownItems.Add(addNewValueItem);
                keyItem.DropDownItems.Add(new ToolStripSeparator());

                var values = _filterService.GetCustomKeyValues(keyName);
                var definedValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                if (definedValues.Count > 0)
                {
                    foreach (string val in definedValues)
                    {
                        bool isChecked = cardTags.Contains(val, StringComparer.OrdinalIgnoreCase);
                        var valItem = new ToolStripMenuItem(val.Replace("&", "&&"))
                        {
                            Checked = isChecked,
                            Tag = val
                        };
                        valItem.Click += (s, ev) =>
                        {
                            string tagToToggle = ((ToolStripMenuItem)s).Tag?.ToString() ?? val;
                            _filterService.ToggleCardCustomTag(card.Id, keyName, tagToToggle);
                            UpdateFacetedFilters();
                            UpdateTaxonomyView(_inspectedCard);
                            PopulateAssignTagsForCategory();
                        };
                        keyItem.DropDownItems.Add(valItem);
                    }
                }

                addMenu.DropDownItems.Add(keyItem);
            }

            if (addMenu.HasDropDownItems)
            {
                ContextMenuStripMaster.Items.Add(addMenu);
            }
        }

        private void SetToolStripMenuMasterDeleteKeyValue(Card card)
        {
            ContextMenuStripMaster.Items.RemoveByKey("Remove Tag");
            var pairs = _filterService.GetCardCustomFilterPairs(card.Id);
            if (pairs.Count == 0) return;

            var delMenu = new ToolStripMenuItem("Remove Tag") { Name = "Remove Tag" };
            foreach (var (key, value) in pairs)
            {
                var keyItem = new ToolStripMenuItem(key.Replace("&", "&&"));
                var tags = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var tag in tags)
                {
                    var valItem = new ToolStripMenuItem(tag.Replace("&", "&&")) { Tag = tag };
                    valItem.Click += (s, ev) =>
                    {
                        string tagToRemove = ((ToolStripMenuItem)s).Tag?.ToString() ?? tag;
                        _filterService.RemoveCardCustomTag(card.Id, key, tagToRemove);
                        UpdateFacetedFilters();
                        UpdateTaxonomyView(_inspectedCard);
                        PopulateAssignTagsForCategory();
                    };
                    keyItem.DropDownItems.Add(valItem);
                }
                delMenu.DropDownItems.Add(keyItem);
            }

            ContextMenuStripMaster.Items.Add(delMenu);
        }

        private void ToolStripMenuMaster_Click(object sender, EventArgs e)
        {
            if (_selectedMasterIndex < 0 || _selectedMasterIndex >= _masterCards.Count) return;

            var sectionType = GetSectionTypeFromMenuName(((ToolStripMenuItem)sender).Name);
            AddCardToSection(_masterCards[_selectedMasterIndex], sectionType);
            TabControlDeck.SelectedIndex = (int)sectionType;
        }

        #endregion

        #region IMAGE_PREVIEW_AND_SELECTION

        private async void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is not ListBox listBox) return;

            if (listBox.SelectedIndex < 0)
            {
                if (listBox == ListBoxMaster)
                {
                    _imageLoadCts?.Cancel();
                    PictureBoxCardImage.Image = null;
                    _inspectedCard = null;
                    UpdateTaxonomyView(null);
                }
                return;
            }

            Card selectedCard = null;

            if (listBox == ListBoxMaster)
            {
                if (listBox.SelectedIndex < _masterCards.Count)
                {
                    selectedCard = _masterCards[listBox.SelectedIndex];
                }
            }
            else
            {
                var sectionType = GetSectionTypeFromListBox(listBox);
                var sectionCards = _currentDeck.GetSection(sectionType);
                if (listBox.SelectedIndex < sectionCards.Count)
                {
                    selectedCard = sectionCards[listBox.SelectedIndex];
                }
            }

            if (selectedCard == null) return;

            _inspectedCard = selectedCard;
            UpdateTaxonomyView(selectedCard);
            PopulateAssignTagsForCategory();

            if (listBox != ListBoxMaster) ListBoxMaster.ClearSelected();
            if (listBox != ListBoxPool) ListBoxPool.ClearSelected();
            if (listBox != ListBoxResources) ListBoxResources.ClearSelected();
            if (listBox != ListBoxHazards) ListBoxHazards.ClearSelected();
            if (listBox != ListBoxSideboard) ListBoxSideboard.ClearSelected();
            if (listBox != ListBoxSites) ListBoxSites.ClearSelected();

            string imageUrl = $"https://cardnum.net/img/cards/{selectedCard.Set}/{selectedCard.ImageName}";

            _imageLoadCts?.Cancel();
            _imageLoadCts?.Dispose();
            _imageLoadCts = new CancellationTokenSource();
            var token = _imageLoadCts.Token;

            try
            {
                var bitmap = await _cardImageCache.GetOrCreateAsync(imageUrl, selectedCard.Set, selectedCard.ImageName, token);
                if (!token.IsCancellationRequested && bitmap != null)
                {
                    PictureBoxCardImage.Image = bitmap;
                }
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        #region SET_MANAGEMENT

        private void ToolStripMenuSet_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            var item = (ToolStripMenuItem)sender;
            string setCode = item.Tag?.ToString();
            if (string.IsNullOrEmpty(setCode)) return;

            if (item.Checked)
            {
                _currentDeck.IncludedSets.Add(setCode);
            }
            else
            {
                _currentDeck.IncludedSets.Remove(setCode);
            }

            PersistSelectedSets();
            UpdateFacetedFilters();
        }

        private void ToolStripMenuSetClearAll_Click(object sender, EventArgs e)
        {
            _isUpdatingFilters = true;
            try
            {
                foreach (ToolStripItem item in ToolStripMenuCatalog.DropDownItems)
                {
                    if (item is ToolStripMenuItem setItem && setItem.Tag is string code && setItem.Checked)
                    {
                        setItem.Checked = false;
                        _currentDeck.IncludedSets.Remove(code);
                    }
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            PersistSelectedSets();
            UpdateFacetedFilters();
        }

        private void ToolStripMenuSetSelectAll_Click(object sender, EventArgs e)
        {
            _isUpdatingFilters = true;
            try
            {
                foreach (ToolStripItem item in ToolStripMenuCatalog.DropDownItems)
                {
                    if (item is ToolStripMenuItem setItem && setItem.Tag is string code && !setItem.Checked)
                    {
                        setItem.Checked = true;
                        _currentDeck.IncludedSets.Add(code);
                    }
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            PersistSelectedSets();
            UpdateFacetedFilters();
        }

        private void UpdateMasterList()
        {
            string curCardId = (ListBoxMaster.SelectedIndex >= 0 && ListBoxMaster.SelectedIndex < _masterCards.Count)
                ? _masterCards[ListBoxMaster.SelectedIndex].Id
                : string.Empty;

            var activeCriteria = _filterSlots
                .Where(s => !string.IsNullOrWhiteSpace(s.CurrentKey) && !string.IsNullOrWhiteSpace(s.CurrentValue))
                .Select(s => s.ToCriterion())
                .ToList();

            _masterCards = _filterService.Filter(_catalogService.Cards, _currentDeck.IncludedSets, activeCriteria);

            ListBoxMaster.BeginUpdate();
            ListBoxMaster.Items.Clear();
            foreach (var card in _masterCards)
            {
                ListBoxMaster.Items.Add(card.Name);
            }
            ListBoxMaster.EndUpdate();

            if (_masterCards.Count == 0)
            {
                _imageLoadCts?.Cancel();
                PictureBoxCardImage.Image = null;
                _inspectedCard = null;
                UpdateTaxonomyView(null);
                return;
            }

            // Restore focus
            int foundIndex = -1;
            if (!string.IsNullOrEmpty(curCardId))
            {
                foundIndex = _masterCards.FindIndex(c => c.Id == curCardId);
            }

            if (foundIndex >= 0)
            {
                ListBoxMaster.SelectedIndex = foundIndex;
                _inspectedCard = _masterCards[foundIndex];
            }
            else
            {
                ListBoxMaster.SelectedIndex = 0;
                _inspectedCard = _masterCards[0];
            }

            UpdateTaxonomyView(_inspectedCard);
            PopulateAssignTagsForCategory();
        }

        #endregion

        #region CARD_OPERATIONS & DRAG_DROP

        private void AddCardToSection(Card card, DeckSectionType sectionType)
        {
            var section = _currentDeck.GetSection(sectionType);
            section.Add(card);
            section.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.WithNaturalSort().Compare(a.Name, b.Name));

            RefreshSectionListBox(sectionType);
            UpdateFormTitle();
        }

        private void RemoveCardFromSection(DeckSectionType sectionType, int index)
        {
            var section = _currentDeck.GetSection(sectionType);
            if (index >= 0 && index < section.Count)
            {
                section.RemoveAt(index);
                RefreshSectionListBox(sectionType);
                UpdateFormTitle();
            }
        }

        private void RefreshSectionListBox(DeckSectionType sectionType)
        {
            var listBox = GetListBoxFromSectionType(sectionType);
            var section = _currentDeck.GetSection(sectionType);

            listBox.BeginUpdate();
            listBox.Items.Clear();
            foreach (var card in section)
            {
                listBox.Items.Add(card.Name);
            }
            listBox.EndUpdate();
        }

        private void ListBoxCardList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = ListBoxMaster.IndexFromPoint(e.Location);
            if (index >= 0 && index < _masterCards.Count)
            {
                // Always add to the active deck section, even if inspecting card taxonomy
                int tabIndex = Math.Clamp(TabControlDeck.SelectedIndex, 0, 4);
                var currentSection = (DeckSectionType)tabIndex;
                AddCardToSection(_masterCards[index], currentSection);
            }
        }

        private void ListBoxTab_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var sectionType = (DeckSectionType)TabControlDeck.SelectedIndex;
            var listBox = GetListBoxFromSectionType(sectionType);
            int index = listBox.IndexFromPoint(e.Location);
            var section = _currentDeck.GetSection(sectionType);

            if (index >= 0 && index < section.Count)
            {
                AddCardToSection(section[index], sectionType);
            }
        }

        private void ListBoxTab_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxTab_DragDrop(object sender, DragEventArgs e)
        {
            if (ListBoxMaster.SelectedIndex >= 0 && ListBoxMaster.SelectedIndex < _masterCards.Count)
            {
                var targetListBox = (ListBox)sender;
                var sectionType = GetSectionTypeFromListBox(targetListBox);
                AddCardToSection(_masterCards[ListBoxMaster.SelectedIndex], sectionType);
            }
        }

        private void UpdateFormTitle()
        {
            int totalDeckCards = _currentDeck.Pool.Count +
                                 _currentDeck.Resources.Count +
                                 _currentDeck.Hazards.Count +
                                 _currentDeck.Sideboard.Count +
                                 _currentDeck.Sites.Count;

            BtnModeDeck.Text = $"Deck ({totalDeckCards})";

            Text = $"{Constants.AppTitle} - \"{_currentDeck.Title}\" " +
                   $"({_currentDeck.Pool.Count}/" +
                   $"{_currentDeck.Resources.Count}[{_currentDeck.CountCharactersInResources()}]/" +
                   $"{_currentDeck.Hazards.Count}/" +
                   $"{_currentDeck.Sideboard.Count}/" +
                   $"{_currentDeck.Sites.Count})";
        }

        #endregion

        #region TAB_CONTEXT_MENU

        private void ListBoxTab_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            _callingListBox = (ListBox)sender;
            _selectedTabIndex = _callingListBox.IndexFromPoint(e.Location);
            if (_selectedTabIndex < 0) return;

            var sectionType = GetSectionTypeFromListBox(_callingListBox);
            var sectionCards = _currentDeck.GetSection(sectionType);
            if (_selectedTabIndex >= sectionCards.Count) return;

            var card = sectionCards[_selectedTabIndex];
            ToolStripMenuTabCardname.Text = card.Name;

            SetToolStripMenuTabCardnumFilters(card);
            SetToolStripMenuTabCustomFilters(card);
            SetToolStripMenuTabAddKeyValue(card);
            SetToolStripMenuTabDeleteKeyValue(card);
            ContextMenuStripTabs.Show(Cursor.Position);
        }

        private void SetToolStripMenuTabCardnumFilters(Card card)
        {
            ToolStripMenuTabCardnumFilters.DropDownItems.Clear();
            var pairs = new List<(string Key, string Value)>
            {
                ("Name", card.Name),
                ("Set", card.Set),
                ("FullCode", card.FullCode)
            };

            foreach (string key in CardCatalogService.FilterKeys)
            {
                string val = card.GetAttribute(key);
                if (!string.IsNullOrEmpty(val))
                {
                    pairs.Add((key, val));
                }
            }

            if (pairs.Count == 0) return;
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string line = $"{pairs[i].Key.PadRight(maxLen + 3)}{pairs[i].Value.Replace("&", "&&")}";
                ToolStripMenuTabCardnumFilters.DropDownItems.Add(new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) });
            }
        }

        private void SetToolStripMenuTabCustomFilters(Card card)
        {
            var pairs = _filterService.GetCardCustomFilterPairs(card.Id);
            ContextMenuStripTabs.Items.RemoveByKey("Assigned Tags");

            if (pairs.Count == 0) return;

            var customMenu = new ToolStripMenuItem("Assigned Tags") { Name = "Assigned Tags" };
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string escapedKey = pairs[i].Key.Replace("&", "&&");
                string escapedVal = pairs[i].Value.Replace("&", "&&");
                string line = $"{escapedKey.PadRight(maxLen + 3)}{escapedVal}";
                customMenu.DropDownItems.Add(new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) });
            }

            ContextMenuStripTabs.Items.Add(customMenu);
        }

        private void SetToolStripMenuTabAddKeyValue(Card card)
        {
            ContextMenuStripTabs.Items.RemoveByKey("Assign Tag");
            var keyNames = _filterService.GetCustomKeyNames();

            var addMenu = new ToolStripMenuItem("Assign Tag") { Name = "Assign Tag" };

            var addNewTagItem = new ToolStripMenuItem("+ Author New Tag...") { Font = new Font(Font, FontStyle.Bold) };
            addNewTagItem.Click += (s, ev) =>
            {
                var result = AddTagDialog.Show(this, card.Name, _filterService);
                if (result.HasValue)
                {
                    _filterService.AddCustomKeyName(result.Value.Key);
                    _filterService.AddCustomKeyValue(result.Value.Key, result.Value.Value);
                    _filterService.AppendCardCustomTag(card.Id, result.Value.Key, result.Value.Value);
                    RefreshFilterKeyDropdowns();
                    UpdateFacetedFilters();
                    UpdateTaxonomyView(_inspectedCard);
                    PopulateAssignCategories();
                }
            };
            addMenu.DropDownItems.Add(addNewTagItem);
            addMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (string keyName in keyNames)
            {
                if (string.IsNullOrWhiteSpace(keyName)) continue;

                var cardTags = _filterService.GetCardTagsForCategory(card.Id, keyName);
                var keyItem = new ToolStripMenuItem(keyName);

                var addNewValueItem = new ToolStripMenuItem("+ Add Tag...");
                addNewValueItem.Click += (s, ev) =>
                {
                    string val = PromptDialog.Show(this, $"Add Tag to '{keyName}'", $"Enter new tag for category '{keyName}':");
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        _filterService.AddCustomKeyValue(keyName, val);
                        _filterService.AppendCardCustomTag(card.Id, keyName, val);
                        RefreshFilterKeyDropdowns();
                        UpdateFacetedFilters();
                        UpdateTaxonomyView(_inspectedCard);
                        PopulateAssignCategories();
                    }
                };
                keyItem.DropDownItems.Add(addNewValueItem);
                keyItem.DropDownItems.Add(new ToolStripSeparator());

                var values = _filterService.GetCustomKeyValues(keyName);
                var definedValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                if (definedValues.Count > 0)
                {
                    foreach (string val in definedValues)
                    {
                        bool isChecked = cardTags.Contains(val, StringComparer.OrdinalIgnoreCase);
                        var valItem = new ToolStripMenuItem(val.Replace("&", "&&"))
                        {
                            Checked = isChecked,
                            Tag = val
                        };
                        valItem.Click += (s, ev) =>
                        {
                            string tagToToggle = ((ToolStripMenuItem)s).Tag?.ToString() ?? val;
                            _filterService.ToggleCardCustomTag(card.Id, keyName, tagToToggle);
                            UpdateFacetedFilters();
                            UpdateTaxonomyView(_inspectedCard);
                            PopulateAssignTagsForCategory();
                        };
                        keyItem.DropDownItems.Add(valItem);
                    }
                }

                addMenu.DropDownItems.Add(keyItem);
            }

            if (addMenu.HasDropDownItems)
            {
                ContextMenuStripTabs.Items.Add(addMenu);
            }
        }

        private void SetToolStripMenuTabDeleteKeyValue(Card card)
        {
            ContextMenuStripTabs.Items.RemoveByKey("Remove Tag");
            var pairs = _filterService.GetCardCustomFilterPairs(card.Id);
            if (pairs.Count == 0) return;

            var delMenu = new ToolStripMenuItem("Remove Tag") { Name = "Remove Tag" };
            foreach (var (key, value) in pairs)
            {
                var keyItem = new ToolStripMenuItem(key.Replace("&", "&&"));
                var tags = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var tag in tags)
                {
                    var valItem = new ToolStripMenuItem(tag.Replace("&", "&&")) { Tag = tag };
                    valItem.Click += (s, ev) =>
                    {
                        string tagToRemove = ((ToolStripMenuItem)s).Tag?.ToString() ?? tag;
                        _filterService.RemoveCardCustomTag(card.Id, key, tagToRemove);
                        UpdateFacetedFilters();
                        UpdateTaxonomyView(_inspectedCard);
                        PopulateAssignTagsForCategory();
                    };
                    keyItem.DropDownItems.Add(valItem);
                }
                delMenu.DropDownItems.Add(keyItem);
            }

            ContextMenuStripTabs.Items.Add(delMenu);
        }

        private void ToolStripMenuTab_Click(object sender, EventArgs e)
        {
            if (_callingListBox == null || _selectedTabIndex < 0) return;

            var sourceSectionType = GetSectionTypeFromListBox(_callingListBox);
            var sourceSection = _currentDeck.GetSection(sourceSectionType);
            if (_selectedTabIndex >= sourceSection.Count) return;

            var card = sourceSection[_selectedTabIndex];
            string menuItemName = ((ToolStripMenuItem)sender).Name;

            if (menuItemName.Contains(Constants.Delete))
            {
                RemoveCardFromSection(sourceSectionType, _selectedTabIndex);
                return;
            }

            var destSectionType = GetSectionTypeFromMenuName(menuItemName);
            AddCardToSection(card, destSectionType);

            if (menuItemName.Contains(Constants.Move))
            {
                RemoveCardFromSection(sourceSectionType, _selectedTabIndex);
            }
        }

        #endregion

        #region FILE_MENU_OPEN_SAVE_EXPORT

        private void NewToolStripMenu_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Do you want to clear the current deck?", Constants.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (result == DialogResult.Yes)
            {
                _currentDeck.Clear();
                foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
                {
                    RefreshSectionListBox(sectionType);
                }
                UpdateFormTitle();
            }
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Deck (*.json)|*.json",
                FilterIndex = 1,
                FileName = GetSafeDeckFileName()
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                _currentDeck.Title = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                var dto = OpenCloseDeck.FromDeck(_currentDeck);
                string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, json);
                UpdateFormTitle();
            }
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Title = $"{Constants.AppTitle} - Save Deck As",
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Deck (*.json)|*.json",
                FilterIndex = 1,
                FileName = GetSafeDeckFileName()
            };

            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                _currentDeck.Title = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                var dto = OpenCloseDeck.FromDeck(_currentDeck);
                string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, json);
                UpdateFormTitle();
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Deck (*.json)|*.json",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var dto = JsonConvert.DeserializeObject<OpenCloseDeck>(json);
                if (dto == null) return;

                var loadedDeck = dto.ToDeck(_catalogService);
                _currentDeck.Clear();
                _currentDeck.Title = loadedDeck.Title;

                foreach (var s in loadedDeck.IncludedSets)
                {
                    _currentDeck.IncludedSets.Add(s);
                }

                _currentDeck.Pool.AddRange(loadedDeck.Pool);
                _currentDeck.Resources.AddRange(loadedDeck.Resources);
                _currentDeck.Hazards.AddRange(loadedDeck.Hazards);
                _currentDeck.Sideboard.AddRange(loadedDeck.Sideboard);
                _currentDeck.Sites.AddRange(loadedDeck.Sites);

                _isUpdatingFilters = true;
                try
                {
                    foreach (ToolStripItem item in ToolStripMenuCatalog.DropDownItems)
                    {
                        if (item is ToolStripMenuItem setItem && setItem.Tag is string tag)
                        {
                            setItem.Checked = _currentDeck.IncludedSets.Contains(tag);
                        }
                    }
                }
                finally
                {
                    _isUpdatingFilters = false;
                }

                foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
                {
                    RefreshSectionListBox(sectionType);
                }

                PersistSelectedSets();
                UpdateFacetedFilters();
                UpdateFormTitle();
            }
        }

        private void ExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Title = Constants.AppTitle,
                Filter = "Tabletop Simulator (*.json)|*.json|Play MECCG (*.play)|*.play|Cardnum (*.cnum)|*.cnum|Archive (*.archive)|*.txt|Text (*.txt)|*.txt",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = GetSafeDeckFileName()
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            string basePrefix = Path.Combine(
                Path.GetDirectoryName(saveFileDialog.FileName) ?? "",
                Path.GetFileNameWithoutExtension(saveFileDialog.FileName));

            switch ((SaveType)saveFileDialog.FilterIndex)
            {
                case SaveType.TTS:
                    DeckExportService.ExportToTts(_currentDeck.Pool, $"{basePrefix}{Constants.poolFileSuffix}.json");
                    DeckExportService.ExportToTts(_currentDeck.Resources, $"{basePrefix}{Constants.resourceFileSuffix}.json");
                    DeckExportService.ExportToTts(_currentDeck.Hazards, $"{basePrefix}{Constants.hazardFileSuffix}.json");
                    DeckExportService.ExportToTts(_currentDeck.Sideboard, $"{basePrefix}{Constants.sideboardFileSuffix}.json");
                    DeckExportService.ExportToTts(_currentDeck.Sites, $"{basePrefix}{Constants.siteFileSuffix}.json");
                    break;
                case SaveType.PlayMECCG:
                    DeckExportService.ExportToPlayMeccg(_currentDeck, $"{basePrefix}.play");
                    break;
                case SaveType.Cardnum:
                    DeckExportService.ExportToCardnum(_currentDeck, $"{basePrefix}.cnum");
                    break;
                case SaveType.Archive:
                    DeckExportService.ExportToArchive(_currentDeck, $"{basePrefix}.archive");
                    break;
                default:
                    DeckExportService.ExportToText(_currentDeck, $"{basePrefix}.txt");
                    break;
            }

            MessageBox.Show(this, "Deck exported successfully.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region CUSTOM_FILTERS_OPEN_SAVE

        private void ToolStripMenuFilterManage_Click(object sender, EventArgs e)
        {
            using var dlg = new ManageCustomTagsForm(_filterService);
            dlg.ShowDialog(this);
            RefreshFilterKeyDropdowns();
            PopulateAssignCategories();
            UpdateFacetedFilters();
            UpdateTaxonomyView(_inspectedCard); // Immediately redraws inspector
        }

        private void OpenFilterMenuItem_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = $"{Constants.AppTitle} - Load Custom Tag Set",
                DefaultExt = "json",
                Filter = "MECCG Custom Tag Set (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var dto = JsonConvert.DeserializeObject<OpenCloseFilter>(json);
                if (dto != null)
                {
                    _filterService.LoadCustomFilters(dto.Filters, dto.Cards, _catalogService);
                    RefreshFilterKeyDropdowns();
                    PopulateAssignCategories();
                    UpdateFacetedFilters();
                }
            }
        }

        private void SaveFilterMenuItem_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Title = $"{Constants.AppTitle} - Save Custom Tag Set",
                DefaultExt = "json",
                Filter = "MECCG Custom Tag Set (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                var dto = OpenCloseFilter.FromFilterService(_filterService, _catalogService);
                string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, json);
            }
        }

        private void ToolStripMenuFilterMerge_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = $"{Constants.AppTitle} - Select Source Tag Set to Merge into Session",
                DefaultExt = "json",
                Filter = "MECCG Custom Tag Set (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var dto = JsonConvert.DeserializeObject<OpenCloseFilter>(json);
                    if (dto != null)
                    {
                        _filterService.MergeCustomFilters(dto.Filters, dto.Cards, _catalogService);
                        RefreshFilterKeyDropdowns();
                        PopulateAssignCategories();
                        UpdateFacetedFilters();
                        MessageBox.Show(this, "Tag set merged successfully into active taxonomy.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to merge tag set: {ex.Message}", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region FACETED_FILTER_ENGINE

        private void UpdateFacetedFilters()
        {
            if (_isUpdatingFilters) return;
            _isUpdatingFilters = true;

            try
            {
                // Pass 1: Validate active slots against selected sets
                for (int i = 0; i < _filterSlots.Length; i++)
                {
                    var slot = _filterSlots[i];
                    if (string.IsNullOrWhiteSpace(slot.CurrentKey) || string.IsNullOrWhiteSpace(slot.CurrentValue))
                    {
                        continue;
                    }

                    var baseValues = _filterService.GetAvailableValues(
                        _catalogService.Cards,
                        _currentDeck.IncludedSets,
                        [],
                        slot.Type,
                        slot.CurrentKey);

                    if (!baseValues.Exists(v => string.Equals(v, slot.CurrentValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        slot.ValueBox.DataSource = null;
                        if (baseValues.Count > 0)
                        {
                            slot.ValueBox.DataSource = baseValues;
                            slot.ValueBox.SelectedIndex = 0;
                        }
                        else
                        {
                            slot.ValueBox.Text = "";
                        }
                    }
                }

                // Pass 2: Mutual constraint check across surviving peer criteria
                bool changed;
                int maxIterations = 4;
                do
                {
                    changed = false;
                    for (int i = 0; i < _filterSlots.Length; i++)
                    {
                        var slot = _filterSlots[i];
                        if (string.IsNullOrWhiteSpace(slot.CurrentKey) || string.IsNullOrWhiteSpace(slot.CurrentValue))
                        {
                            continue;
                        }

                        var otherCriteria = _filterSlots
                            .Where(s => s.Index != i && !string.IsNullOrWhiteSpace(s.CurrentKey) && !string.IsNullOrWhiteSpace(s.CurrentValue))
                            .Select(s => s.ToCriterion())
                            .ToList();

                        var availableValues = _filterService.GetAvailableValues(
                            _catalogService.Cards,
                            _currentDeck.IncludedSets,
                            otherCriteria,
                            slot.Type,
                            slot.CurrentKey);

                        if (!availableValues.Exists(v => string.Equals(v, slot.CurrentValue, StringComparison.OrdinalIgnoreCase)))
                        {
                            slot.ValueBox.DataSource = null;
                            if (availableValues.Count > 0)
                            {
                                slot.ValueBox.DataSource = availableValues;
                                slot.ValueBox.SelectedIndex = 0;
                            }
                            else
                            {
                                slot.ValueBox.Text = "";
                            }
                            changed = true;
                        }
                    }
                } while (changed && --maxIterations > 0);

                // Pass 3: Context-sensitive pruning
                for (int i = 0; i < _filterSlots.Length; i++)
                {
                    var slot = _filterSlots[i];
                    string key = slot.CurrentKey;
                    string prevVal = slot.CurrentValue;

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        slot.ValueBox.DataSource = null;
                        slot.ValueBox.DataSource = new List<string> { "" };
                        slot.ValueBox.SelectedIndex = 0;
                        continue;
                    }

                    var otherCriteria = _filterSlots
                        .Where(s => s.Index != i && !string.IsNullOrWhiteSpace(s.CurrentKey) && !string.IsNullOrWhiteSpace(s.CurrentValue))
                        .Select(s => s.ToCriterion())
                        .ToList();

                    var availableValues = _filterService.GetAvailableValues(
                        _catalogService.Cards,
                        _currentDeck.IncludedSets,
                        otherCriteria,
                        slot.Type,
                        key);

                    slot.ValueBox.DataSource = null;
                    slot.ValueBox.DataSource = availableValues;

                    int matchIdx = availableValues.FindIndex(v => string.Equals(v, prevVal, StringComparison.OrdinalIgnoreCase));
                    slot.ValueBox.SelectedIndex = matchIdx >= 0 ? matchIdx : 0;
                }

                UpdateMasterList();
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private void KeyName_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            int slotIndex = GetSlotIndexForControl((Control)sender);
            if (slotIndex >= 0)
            {
                _isUpdatingFilters = true;
                try
                {
                    var valueBox = _filterSlots[slotIndex].ValueBox;
                    valueBox.DataSource = null;
                    valueBox.Items.Clear();
                }
                finally
                {
                    _isUpdatingFilters = false;
                }

                // Live-sync Quick Assign to whichever custom slot you just changed
                if (sender == ComboBoxKey3 && !string.IsNullOrWhiteSpace(ComboBoxKey3.Text))
                {
                    CmbAssignCategory.Text = ComboBoxKey3.Text.Trim();
                    PopulateAssignTagsForCategory();
                }
                else if (sender == ComboBoxKey4 && !string.IsNullOrWhiteSpace(ComboBoxKey4.Text))
                {
                    CmbAssignCategory.Text = ComboBoxKey4.Text.Trim();
                    PopulateAssignTagsForCategory();
                }

                UpdateFacetedFilters();
            }
        }

        private void KeyValue_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            // Live-sync Quick Assign when changing custom tag values in Slot 3 or 4
            if (sender == ComboBoxValue3 && !string.IsNullOrWhiteSpace(ComboBoxKey3.Text))
            {
                string tag = ComboBoxValue3.Text.Trim();
                CmbAssignCategory.Text = ComboBoxKey3.Text.Trim();
                PopulateAssignTagsForCategory(!string.IsNullOrWhiteSpace(tag) ? tag : null);
            }
            else if (sender == ComboBoxValue4 && !string.IsNullOrWhiteSpace(ComboBoxKey4.Text))
            {
                string tag = ComboBoxValue4.Text.Trim();
                CmbAssignCategory.Text = ComboBoxKey4.Text.Trim();
                PopulateAssignTagsForCategory(!string.IsNullOrWhiteSpace(tag) ? tag : null);
            }

            UpdateFacetedFilters();
        }

        private void AdjustWidthComboBox_DropDown(object sender, EventArgs e)
        {
            var cb = (ComboBox)sender;
            int width = cb.DropDownWidth;
            using var g = cb.CreateGraphics();
            int vertWidth = cb.Items.Count > cb.MaxDropDownItems ? SystemInformation.VerticalScrollBarWidth : 0;

            foreach (var item in cb.Items)
            {
                if (item != null)
                {
                    int itemWidth = (int)g.MeasureString(item.ToString(), cb.Font).Width + vertWidth;
                    if (itemWidth > width) width = itemWidth;
                }
            }
            cb.DropDownWidth = width;
        }

        private void InitializeFilterClearing()
        {
            var filterContextMenu = new ContextMenuStrip();

            var clearSlotItem = new ToolStripMenuItem("Clear This Filter");
            clearSlotItem.Click += (s, e) =>
            {
                if (filterContextMenu.SourceControl is ComboBox cb)
                {
                    ClearSlotForControl(cb);
                }
            };

            var clearAllItem = new ToolStripMenuItem("Clear All Filters") { ShortcutKeyDisplayString = "Ctrl+R" };
            clearAllItem.Click += (s, e) => ResetAllFilters();

            filterContextMenu.Items.Add(clearSlotItem);
            filterContextMenu.Items.Add(new ToolStripSeparator());
            filterContextMenu.Items.Add(clearAllItem);

            ComboBox[] filterBoxes = [
                ComboBoxKey1, ComboBoxValue1,
                ComboBoxKey2, ComboBoxValue2,
                ComboBoxKey3, ComboBoxValue3,
                ComboBoxKey4, ComboBoxValue4
            ];

            foreach (var cb in filterBoxes)
            {
                cb.ContextMenuStrip = filterContextMenu;
                cb.KeyDown += FilterComboBox_KeyDown;
            }
        }

        private void FilterComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && sender is ComboBox cb)
            {
                cb.SelectedIndex = 0;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ClearSlotForControl(ComboBox cb)
        {
            int slotIndex = GetSlotIndexForControl(cb);
            if (slotIndex >= 0)
            {
                _isUpdatingFilters = true;
                try
                {
                    var slot = _filterSlots[slotIndex];
                    if (slot.KeyBox.Items.Count > 0) slot.KeyBox.SelectedIndex = 0;
                    else { slot.KeyBox.SelectedIndex = -1; slot.KeyBox.Text = ""; }

                    if (slot.ValueBox.Items.Count > 0) slot.ValueBox.SelectedIndex = 0;
                    else { slot.ValueBox.SelectedIndex = -1; slot.ValueBox.Text = ""; }
                }
                finally
                {
                    _isUpdatingFilters = false;
                }

                UpdateFacetedFilters();
            }
        }

        public void ResetAllFilters()
        {
            if (_isUpdatingFilters) return;
            _isUpdatingFilters = true;

            try
            {
                foreach (var slot in _filterSlots)
                {
                    if (slot.KeyBox.Items.Count > 0) slot.KeyBox.SelectedIndex = 0;
                    else slot.KeyBox.Text = "";

                    if (slot.ValueBox.Items.Count > 0) slot.ValueBox.SelectedIndex = 0;
                    else slot.ValueBox.Text = "";
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            UpdateFacetedFilters();
        }

        private int GetSlotIndexForControl(Control control)
        {
            if (control == ComboBoxKey1 || control == ComboBoxValue1) return 0;
            if (control == ComboBoxKey2 || control == ComboBoxValue2) return 1;
            if (control == ComboBoxKey3 || control == ComboBoxValue3) return 2;
            if (control == ComboBoxKey4 || control == ComboBoxValue4) return 3;
            return -1;
        }

        #endregion

        #region HELPERS_AND_LOOKUPS

        private static DeckSectionType GetSectionTypeFromListBox(ListBox lb) => lb.Name switch
        {
            nameof(ListBoxPool) => DeckSectionType.Pool,
            nameof(ListBoxResources) => DeckSectionType.Resources,
            nameof(ListBoxHazards) => DeckSectionType.Hazards,
            nameof(ListBoxSideboard) => DeckSectionType.Sideboard,
            nameof(ListBoxSites) => DeckSectionType.Sites,
            _ => DeckSectionType.Pool
        };

        private ListBox GetListBoxFromSectionType(DeckSectionType type) => type switch
        {
            DeckSectionType.Pool => ListBoxPool,
            DeckSectionType.Resources => ListBoxResources,
            DeckSectionType.Hazards => ListBoxHazards,
            DeckSectionType.Sideboard => ListBoxSideboard,
            DeckSectionType.Sites => ListBoxSites,
            _ => ListBoxPool
        };

        private static DeckSectionType GetSectionTypeFromMenuName(string menuName)
        {
            if (menuName.Contains(Constants.Pool)) return DeckSectionType.Pool;
            if (menuName.Contains(Constants.Resource)) return DeckSectionType.Resources;
            if (menuName.Contains(Constants.Hazard)) return DeckSectionType.Hazards;
            if (menuName.Contains(Constants.Sideboard)) return DeckSectionType.Sideboard;
            if (menuName.Contains(Constants.Site)) return DeckSectionType.Sites;
            return DeckSectionType.Pool;
        }

        #endregion

        #region TOOLS_AND_HELP

        private async void ToolStripMenuToolsGetImages_Click(object sender, EventArgs e)
        {
            if (_masterCards.Count == 0)
            {
                MessageBox.Show("No cards available in the master list to download.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ToolStripMenuToolsGetImages.Enabled = false;
            Cursor = Cursors.WaitCursor;

            int downloadedCount = 0;
            int skippedCount = 0;
            var cardsToProcess = _masterCards.ToList();
            int total = cardsToProcess.Count;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    var card = cardsToProcess[i];
                    Text = $"{Constants.AppTitle} - Downloading Images ({i + 1}/{total}): {card.Name}";

                    if (string.IsNullOrEmpty(card.ImageName) || string.IsNullOrEmpty(card.Set)) continue;

                    string readPath = StoragePathService.ResolveImageReadPath(card.Set, card.ImageName);
                    if (File.Exists(readPath))
                    {
                        skippedCount++;
                        continue;
                    }

                    string targetPath = StoragePathService.GetCardImagePath(card.Set, card.ImageName);
                    string setDir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(setDir)) Directory.CreateDirectory(setDir);

                    using var bmp = await CardImageCache.CreateItemAsync($"https://cardnum.net/img/cards/{card.Set}/{card.ImageName}");
                    if (bmp != null)
                    {
                        try
                        {
                            bmp.Save(targetPath);
                            downloadedCount++;
                        }
                        catch { }
                    }
                }

                MessageBox.Show($"Image download complete.\n\nDownloaded: {downloadedCount}\nAlready cached: {skippedCount}",
                    Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                ToolStripMenuToolsGetImages.Enabled = true;
                Cursor = Cursors.Default;
                UpdateFormTitle();
            }
        }

        private void ContentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string helpFile = Path.Combine(Application.StartupPath, "Resources", "Help", "MECCG_Deck_Builder.chm");
            if (File.Exists(helpFile))
            {
                Help.ShowHelp(this, helpFile, HelpNavigator.TableOfContents);
            }
            else
            {
                MessageBox.Show($"Help file not found at:\n{helpFile}", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                $"{Constants.AppTitle}\nVersion 0.5.0\n\n" +
                "A desktop deck builder and collection manager for the Middle-earth Collectible Card Game (MECCG).\n\n" +
                "Card data and artwork courtesy of the Cardnum Project (https://cardnum.net).\n" +
                "MECCG is © Iron Crown Enterprises and Tolkien Enterprises.",
                $"About {Constants.AppTitle}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ToolStripMenuToolsGenerateDeck_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = $"{Constants.AppTitle} - Select Deck Rules File",
                DefaultExt = "json",
                Filter = "MECCG Deck Rules (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                RunDeckGeneration(ofd.FileName);
            }
        }

        private void ToolStripMenuToolsRerunDeckGen_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastDeckRulesFilePath) && File.Exists(_lastDeckRulesFilePath))
            {
                RunDeckGeneration(_lastDeckRulesFilePath);
            }
            else
            {
                ToolStripMenuToolsGenerateDeck_Click(sender, e);
            }
        }

        private void RunDeckGeneration(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(this, "The specified deck rules file could not be found.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DeckGenerationRuleSet ruleSet;
            try
            {
                string json = File.ReadAllText(filePath);
                ruleSet = DeckGenerationService.LoadRuleSetFromJson(json);

                if (ruleSet == null || ruleSet.Rules == null || ruleSet.Rules.Count == 0)
                {
                    MessageBox.Show(this, "The selected rules file contains no rules.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load deck rules file:\n{ex.Message}", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = DeckGenerationService.GenerateDeck(
                _catalogService.Cards,
                _currentDeck.IncludedSets,
                _filterService,
                ruleSet);

            if (!result.Success)
            {
                MessageBox.Show(this, $"Deck generation failed: {result.ErrorMessage}", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _lastDeckRulesFilePath = filePath;
            ToolStripMenuToolsRerunDeckGen.Enabled = true;
            ToolStripMenuToolsRerunDeckGen.Text = $"Re-run Rules ({Path.GetFileName(filePath)})";

            _currentDeck.Clear();
            _currentDeck.Title = result.Deck.Title;

            foreach (var s in result.Deck.IncludedSets)
            {
                _currentDeck.IncludedSets.Add(s);
            }

            _currentDeck.Pool.AddRange(result.Deck.Pool);
            _currentDeck.Resources.AddRange(result.Deck.Resources);
            _currentDeck.Hazards.AddRange(result.Deck.Hazards);
            _currentDeck.Sideboard.AddRange(result.Deck.Sideboard);
            _currentDeck.Sites.AddRange(result.Deck.Sites);

            _isUpdatingFilters = true;
            try
            {
                foreach (ToolStripItem item in ToolStripMenuCatalog.DropDownItems)
                {
                    if (item is ToolStripMenuItem setItem && setItem.Tag is string tag)
                    {
                        setItem.Checked = _currentDeck.IncludedSets.Contains(tag);
                    }
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }
            PersistSelectedSets();

            ResetAllFilters();

            foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
            {
                RefreshSectionListBox(sectionType);
            }
            UpdateFormTitle();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Deck \"{_currentDeck.Title}\" generated successfully!\n");
            sb.AppendLine($"Pool: {_currentDeck.Pool.Count}");
            sb.AppendLine($"Resources: {_currentDeck.Resources.Count} ({_currentDeck.CountCharactersInResources()} Characters)");
            sb.AppendLine($"Hazards: {_currentDeck.Hazards.Count}");
            sb.AppendLine($"Sideboard: {_currentDeck.Sideboard.Count}");
            sb.AppendLine($"Sites: {_currentDeck.Sites.Count}");

            var branches = result.Logs.Where(l => l.RuleName.StartsWith("[Branch Selected]")).ToList();
            if (branches.Count > 0)
            {
                sb.AppendLine("\nRandom Choices:");
                foreach (var b in branches)
                {
                    sb.AppendLine($" • {b.RuleName.Replace("[Branch Selected] ", "")}");
                }
            }

            var shortfalls = result.Logs.Where(l => l.IsShortfall).ToList();
            if (shortfalls.Count > 0)
            {
                sb.AppendLine("\nNotice - Insufficient matching cards:");
                foreach (var sf in shortfalls)
                {
                    sb.AppendLine($" • {sf.RuleName} ({sf.Section}): added {sf.AddedCount} of {sf.RequestedCount}");
                }
            }

            MessageBox.Show(this, sb.ToString(), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GetSafeDeckFileName()
        {
            if (string.IsNullOrWhiteSpace(_currentDeck.Title))
            {
                return "Deck";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeChars = _currentDeck.Title
                .Where(c => !invalidChars.Contains(c))
                .ToArray();

            string safeName = new string(safeChars).Trim();
            return !string.IsNullOrWhiteSpace(safeName) ? safeName : "Deck";
        }

        #endregion

        #region TAXONOMY_TOOLS

        private void ToolStripMenuToolsRunRules_Click(object sender, EventArgs e)
        {
            if (_masterCards.Count == 0)
            {
                MessageBox.Show(this,
                    "No cards are currently displayed in the Master List to evaluate.\n\nPlease select at least one expansion set or adjust your active filters.",
                    Constants.AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dlg = new RunTaxonomyRulesDialog(_masterCards, _currentDeck.IncludedSets, _catalogService, _filterService);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RefreshFilterKeyDropdowns();
                PopulateAssignCategories();
                UpdateFacetedFilters();
                UpdateTaxonomyView(_inspectedCard);
            }
        }

        private void ToolStripMenuToolsExportCsv_Click(object sender, EventArgs e)
        {
            if (_masterCards.Count == 0)
            {
                MessageBox.Show(this,
                    "No cards are currently displayed in the Master List to export.\n\nPlease select at least one expansion set or adjust your active filters.",
                    Constants.AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = $"{Constants.AppTitle} - Export Taxonomy Draft (CSV)",
                DefaultExt = "csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                FileName = "Taxonomy_Draft.csv"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                var activeAttributes = new List<string>();
                if (!string.IsNullOrWhiteSpace(_filterSlots[0].CurrentKey)) activeAttributes.Add(_filterSlots[0].CurrentKey);
                if (!string.IsNullOrWhiteSpace(_filterSlots[1].CurrentKey)) activeAttributes.Add(_filterSlots[1].CurrentKey);

                var activeCategories = new List<string>();
                if (!string.IsNullOrWhiteSpace(_filterSlots[2].CurrentKey)) activeCategories.Add(_filterSlots[2].CurrentKey);
                if (!string.IsNullOrWhiteSpace(_filterSlots[3].CurrentKey)) activeCategories.Add(_filterSlots[3].CurrentKey);

                TaxonomyExtractionService.ExportTemplateDraftToCsv(
                    _masterCards,
                    _filterService,
                    sfd.FileName,
                    activeAttributes,
                    activeCategories);

                MessageBox.Show(this,
                    $"Taxonomy draft exported successfully to:\n{sfd.FileName}\n\n" +
                    $"Cards exported: {_masterCards.Count}\n" +
                    $"Official attribute columns: {(activeAttributes.Count > 0 ? string.Join(", ", activeAttributes) : "Primary (default)")}\n" +
                    $"Custom category columns: {(activeCategories.Count > 0 ? string.Join(", ", activeCategories) : "[Insert Category Name] (blank draft)")}\n\n" +
                    "Workflow in Google Sheets or Excel:\n" +
                    "1. Review and edit tags (separate multiple tags with ';').\n" +
                    "2. Add new category columns as needed.\n" +
                    "3. Import changes back via Taxonomy > Import Taxonomy from CSV...",
                    Constants.AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ToolStripMenuToolsImportCsv_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = $"{Constants.AppTitle} - Import Taxonomy from CSV",
                DefaultExt = "csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var (cardsMatched, categoriesImported, tagsAssigned) =
                        TaxonomyExtractionService.ImportFromCsv(ofd.FileName, _catalogService, _filterService);

                    RefreshFilterKeyDropdowns();
                    PopulateAssignCategories();
                    UpdateFacetedFilters();
                    UpdateTaxonomyView(_inspectedCard);

                    MessageBox.Show(this,
                        $"Taxonomy imported successfully.\n\nCards matched: {cardsMatched}\nCategories updated: {categoriesImported}\nTags assigned: {tagsAssigned}",
                        Constants.AppTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to import CSV taxonomy: {ex.Message}", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion
    }
}