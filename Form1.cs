using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

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
        private ListBox _callingListBox;
        private int _selectedMasterIndex;
        private int _selectedTabIndex;
        private CancellationTokenSource _imageLoadCts;
        private Rectangle _dragBoxFromMouseDown = Rectangle.Empty;

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
                CreateMenus();
                RefreshFilterKeyDropdowns();
                UpdateFacetedFilters();
                UpdateFormTitle();
            }
            finally
            {
                Enabled = true;
                UpdateFormTitle();
            }
        }

        private void CreateMenus()
        {
            _isUpdatingFilters = true;
            try
            {
                for (int i = 0; i < _catalogService.Sets.Count; i++)
                {
                    var set = _catalogService.Sets[i];
                    var menuItem = new ToolStripMenuItem(set.Name)
                    {
                        Tag = set.Code,
                        CheckOnClick = true
                    };
                    menuItem.CheckedChanged += ToolStripMenuSet_CheckedChanged;
                    if (i == 0)
                    {
                        menuItem.Checked = true;
                        _currentDeck.IncludedSets.Add(set.Code);
                    }
                    ToolStripMenuSet.DropDownItems.Add(menuItem);
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            var resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            ToolStripMenuFilterOpen.Image = (Image)resources.GetObject("OpenToolStripMenuItem.Image");
            ToolStripMenuFilterSave.Image = (Image)resources.GetObject("ExportToolStripMenuItem.Image");

            var clearMenuItem = new ToolStripMenuItem("Clear All Filters")
            {
                ShortcutKeys = Keys.Control | Keys.R
            };
            clearMenuItem.Click += (s, e) => ResetAllFilters();

            ToolStripMenuFilter.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuFilter.DropDownItems.Add(clearMenuItem);
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
            var pairs = new List<(string Key, string Value)>();

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
            ContextMenuStripMaster.Items.RemoveByKey("Custom Filters");

            if (pairs.Count == 0) return;

            var customMenu = new ToolStripMenuItem("Custom Filters") { Name = "Custom Filters" };
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string line = $"{pairs[i].Key.PadRight(maxLen + 3)}{pairs[i].Value}";
                customMenu.DropDownItems.Add(new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) });
            }

            ContextMenuStripMaster.Items.Add(customMenu);
        }

        private void SetToolStripMenuMasterAddKeyValue(Card card)
        {
            ContextMenuStripMaster.Items.RemoveByKey("Add Key Value");
            var keyNames = _filterService.GetCustomKeyNames();
            var cardKeys = _filterService.GetCardCustomKeyNames(card.Id);

            var addMenu = new ToolStripMenuItem("Add Key Value") { Name = "Add Key Value" };

            foreach (string keyName in keyNames)
            {
                if (string.IsNullOrWhiteSpace(keyName))
                {
                    continue;
                }

                if (!cardKeys.Contains(keyName))
                {
                    var keyItem = new ToolStripMenuItem(keyName);
                    var values = _filterService.GetCustomKeyValues(keyName);
                    var definedValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                    if (definedValues.Count > 0)
                    {
                        foreach (string val in definedValues)
                        {
                            var valItem = new ToolStripMenuItem(val);
                            valItem.Click += (s, ev) =>
                            {
                                _filterService.SetCardCustomTag(card.Id, keyName, valItem.Text);
                                UpdateFacetedFilters();
                            };
                            keyItem.DropDownItems.Add(valItem);
                        }
                    }
                    else
                    {
                        keyItem.DropDownItems.Add(new ToolStripMenuItem("(No values defined)") { Enabled = false });
                    }

                    addMenu.DropDownItems.Add(keyItem);
                }
            }

            if (addMenu.HasDropDownItems)
            {
                ContextMenuStripMaster.Items.Add(addMenu);
            }
        }

        private void SetToolStripMenuMasterDeleteKeyValue(Card card)
        {
            ContextMenuStripMaster.Items.RemoveByKey("Delete Key Value");
            var pairs = _filterService.GetCardCustomFilterPairs(card.Id);
            if (pairs.Count == 0) return;

            var delMenu = new ToolStripMenuItem("Delete Key Value") { Name = "Delete Key Value" };
            foreach (var (key, value) in pairs)
            {
                var keyItem = new ToolStripMenuItem(key);
                var valItem = new ToolStripMenuItem(value);
                valItem.Click += (s, ev) =>
                {
                    _filterService.DeleteCardCustomTag(card.Id, key);
                    UpdateFacetedFilters();
                };
                keyItem.DropDownItems.Add(valItem);
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

        #region IMAGE_PREVIEW

        private async void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is not ListBox listBox) return;

            if (listBox.SelectedIndex < 0)
            {
                if (listBox == ListBoxMaster)
                {
                    _imageLoadCts?.Cancel();
                    PictureBoxCardImage.Image = null;
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

            UpdateFacetedFilters();
        }

        private void ToolStripMenuSetClearAll_Click(object sender, EventArgs e)
        {
            _isUpdatingFilters = true;
            try
            {
                for (int i = 2; i < ToolStripMenuSet.DropDownItems.Count; i++)
                {
                    if (ToolStripMenuSet.DropDownItems[i] is ToolStripMenuItem item && item.Checked)
                    {
                        item.Checked = false;
                        if (item.Tag is string code)
                        {
                            _currentDeck.IncludedSets.Remove(code);
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            UpdateFacetedFilters();
        }

        private void ToolStripMenuSetSelectAll_Click(object sender, EventArgs e)
        {
            _isUpdatingFilters = true;
            try
            {
                for (int i = 2; i < ToolStripMenuSet.DropDownItems.Count; i++)
                {
                    if (ToolStripMenuSet.DropDownItems[i] is ToolStripMenuItem item && !item.Checked)
                    {
                        item.Checked = true;
                        if (item.Tag is string code)
                        {
                            _currentDeck.IncludedSets.Add(code);
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

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
            }
            else
            {
                ListBoxMaster.SelectedIndex = 0;
            }
        }

        #endregion

        #region CARD_OPERATIONS & DRAG_DROP

        private void AddCardToSection(Card card, DeckSectionType sectionType)
        {
            var section = _currentDeck.GetSection(sectionType);
            section.Add(card);
            section.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

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
                var currentSection = (DeckSectionType)TabControlDeck.SelectedIndex;
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
            ContextMenuStripTabs.Show(Cursor.Position);
        }

        private void SetToolStripMenuTabCardnumFilters(Card card)
        {
            ToolStripMenuTabCardnumFilters.DropDownItems.Clear();
            var pairs = new List<(string Key, string Value)>();

            foreach (string key in CardCatalogService.FilterKeys)
            {
                string val = card.GetAttribute(key);
                if (!string.IsNullOrEmpty(val)) pairs.Add((key, val));
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
            ContextMenuStripTabs.Items.RemoveByKey("Custom Filters");

            if (pairs.Count == 0) return;

            var customMenu = new ToolStripMenuItem("Custom Filters") { Name = "Custom Filters" };
            int maxLen = pairs.Max(p => p.Key.Length);

            for (int i = 0; i < pairs.Count; i++)
            {
                string line = $"{pairs[i].Key.PadRight(maxLen + 3)}{pairs[i].Value}";
                customMenu.DropDownItems.Add(new ToolStripMenuItem(line) { Font = new Font("Consolas", 8.0f) });
            }

            ContextMenuStripTabs.Items.Add(customMenu);
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
                FilterIndex = 1
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
                    foreach (ToolStripItem item in ToolStripMenuSet.DropDownItems)
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
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            string basePrefix = Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName) ?? "", Path.GetFileNameWithoutExtension(saveFileDialog.FileName));

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
        }

        #endregion

        #region CUSTOM_FILTERS_OPEN_SAVE

        private void OpenFilterMenuItem_Click(object sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = Constants.AppTitle,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Custom Filters (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var dto = JsonConvert.DeserializeObject<OpenCloseFilter>(json);
                if (dto != null)
                {
                    _filterService.LoadCustomFilters(dto.Filters, dto.Cards);
                    RefreshFilterKeyDropdowns();
                    UpdateFacetedFilters();
                }
            }
        }

        private void SaveFilterMenuItem_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Title = Constants.AppTitle,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Custom Filters (*.json)|*.json"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                var dto = OpenCloseFilter.FromFilterService(_filterService);
                string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, json);
            }
        }

        private void SetFilterMenuDeleteKeyNameValue_Click(object sender, EventArgs e)
        {
            var keyNames = _filterService.GetCustomKeyNames();
            var delMenu = new ToolStripMenuItem("Delete Key") { Name = "Delete Key", Enabled = keyNames.Count > 0 };

            foreach (string keyName in keyNames)
            {
                if (string.IsNullOrWhiteSpace(keyName)) continue;

                var keyItem = new ToolStripMenuItem(keyName);
                keyItem.Click += (s, ev) =>
                {
                    _filterService.DeleteCustomKeyName(keyName);

                    if (string.Equals(ComboBoxKey3.Text.Trim(), keyName, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboBoxKey3.SelectedIndex = 0;
                        ComboBoxValue3.SelectedIndex = 0;
                    }

                    if (string.Equals(ComboBoxKey4.Text.Trim(), keyName, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboBoxKey4.SelectedIndex = 0;
                        ComboBoxValue4.SelectedIndex = 0;
                    }

                    RefreshFilterKeyDropdowns();
                    UpdateFacetedFilters();
                };

                var values = _filterService.GetCustomKeyValues(keyName);
                foreach (string val in values)
                {
                    if (!string.IsNullOrEmpty(val))
                    {
                        var valItem = new ToolStripMenuItem(val);
                        valItem.Click += (s, ev) =>
                        {
                            _filterService.DeleteCustomKeyValue(keyName, val);
                            UpdateFacetedFilters();
                        };
                        keyItem.DropDownItems.Add(valItem);
                    }
                }
                delMenu.DropDownItems.Add(keyItem);
            }

            ToolStripMenuFilter.DropDownItems.RemoveByKey("Delete Key");
            ToolStripMenuFilter.DropDownItems.Insert(0, delMenu);
        }

        #endregion

        #region FACETED_FILTER_ENGINE

        private void UpdateFacetedFilters()
        {
            if (_isUpdatingFilters) return;
            _isUpdatingFilters = true;

            try
            {
                // Invalidation loop: detect dead-ends and reset invalid values to blank
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
                            if (slot.ValueBox.DataSource is List<string> list && list.Count > 0)
                            {
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

                // Context-sensitive pruning: update dropdown contents for all 4 slots
                for (int i = 0; i < _filterSlots.Length; i++)
                {
                    var slot = _filterSlots[i];
                    string key = slot.CurrentKey;
                    string prevVal = slot.CurrentValue;

                    if (string.IsNullOrWhiteSpace(key))
                    {
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
                    if (valueBox.Items.Count > 0)
                    {
                        valueBox.SelectedIndex = 0;
                    }
                    else
                    {
                        valueBox.SelectedIndex = -1;
                        valueBox.Text = string.Empty;
                    }
                }
                finally
                {
                    _isUpdatingFilters = false;
                }

                UpdateFacetedFilters();
            }
        }

        private void KeyValue_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            UpdateFacetedFilters();
        }

        private void ComboBoxKeyNameHandleTextEntry(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            var cb = (ComboBox)sender;
            string keyName = cb.Text.Trim();
            if (!string.IsNullOrEmpty(keyName) && !cb.Items.Contains(keyName))
            {
                _filterService.AddCustomKeyName(keyName);
                RefreshFilterKeyDropdowns();
                cb.SelectedItem = keyName;

                UpdateFacetedFilters();
            }
        }

        private void ComboBoxKeyValueHandleTextEntry(object sender, EventArgs e)
        {
            if (_isUpdatingFilters) return;

            var cb = (ComboBox)sender;
            string val = cb.Text.Trim();
            var keyCb = cb == ComboBoxValue3 ? ComboBoxKey3 : ComboBoxKey4;
            string key = keyCb.Text.Trim();

            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
            {
                _filterService.AddCustomKeyValue(key, val);

                UpdateFacetedFilters();
            }
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
            else if (e.KeyCode == Keys.Enter && sender is ComboBox enterCb)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (enterCb == ComboBoxKey3 || enterCb == ComboBoxKey4)
                {
                    ComboBoxKeyNameHandleTextEntry(enterCb, EventArgs.Empty);
                }
                else if (enterCb == ComboBoxValue3 || enterCb == ComboBoxValue4)
                {
                    ComboBoxKeyValueHandleTextEntry(enterCb, EventArgs.Empty);
                }
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

                    string targetPath = Path.Combine(card.Set, card.ImageName);
                    if (File.Exists(targetPath))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!Directory.Exists(card.Set)) Directory.CreateDirectory(card.Set);

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

        #endregion
    }
}