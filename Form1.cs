using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{

    internal partial class Form1 : Form
    {
        // There is a master listbox of all cards in selected sets on the left from which
        // cards can be copied to the tabs on the right. On the right are five tabs each with 
        // a listbox for pool/resource/hazard/sideboard/site, cards can be copied/moved between 
        // tabs and deleted from a tab. Each listbox has an associated list with the card set
        // and id. "meccgCards" is where all information about each card is stored.

        private List<string[]> masterList = [];
        private readonly List<string[]> poolList = [];
        private readonly List<string[]> resourceList = [];
        private readonly List<string[]> hazardList = [];
        private readonly List<string[]> sideboardList = [];
        private readonly List<string[]> siteList = [];
        private readonly List<string> setList = [];
        private readonly Cards meccgCards = new();
        private readonly KeyValue userKeyValues = new();
        private ListBox callingListbox;
        private int selectedIndex;
        private string currentDeckTitle = "New Deck";
        private readonly CardImageCache cardImageCache = new();

        internal Form1()
        {
            InitializeComponent();
            CreateMenus();
            UpdateFormTitle();
        }

        /// <summary>
        /// Dynamic build of set menu list
        /// </summary>
        private void CreateMenus()
        {
            // Set menus
            for (int index = 0; index < meccgCards.GetSetCount(); index++)
            {
                ToolStripMenuItem MenuSetItem = new(meccgCards.GetSetValue(index, "name"))
                {
                    Tag = meccgCards.GetSetValue(index, "code"),
                    CheckOnClick = true,
                };
                MenuSetItem.CheckedChanged += new EventHandler(ToolStripMenuSet_CheckedChanged);
                if (index == 0)
                {
                    MenuSetItem.Checked = true;
                }
                ToolStripMenuSet.DropDownItems.Add(MenuSetItem);
            }

            // Filter menus
            System.ComponentModel.ComponentResourceManager resources = new(typeof(Form1));
            ToolStripMenuFilterOpen.Image = (Image)resources.GetObject("OpenToolStripMenuItem.Image");
            ToolStripMenuFilterSave.Image = (Image)resources.GetObject("ExportToolStripMenuItem.Image");

            // Filter key name lists
            SetKeyNameList(ComboBoxKey1);
            SetKeyNameList(ComboBoxKey2);
            SetKeyNameList(ComboBoxKey3);
            SetKeyNameList(ComboBoxKey4);
        }

        #region MASTER_SINGLE_CLICK

        private void ListBoxMasterList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Find the item under the mouse.
                int index = ListBoxMaster.IndexFromPoint(e.Location);
                if (index < 0)
                {
                    return;
                }
                ListBoxMaster.SelectedIndex = index;
                ListBox_SelectedIndexChanged(sender, e);

                // Drag the item.
                string cardText = ListBoxMaster.SelectedItem.ToString();
                ListBoxMaster.DoDragDrop(cardText, DragDropEffects.Copy);
            }
            if (e.Button == MouseButtons.Right)
            {
                // Find the item under the mouse.
                selectedIndex = ListBoxMaster.IndexFromPoint(e.Location);
                if (selectedIndex < 0)
                {
                    return;
                }
                ToolStripMenuMasterCardname.Text = masterList[selectedIndex][(int)CardListField.name];
                SetToolStripMenuMasterCardnumFilters();
                SetToolStripMenuMasterCustomFilters();
                SetToolStripMenuMasterAddKeyValue();
                SetToolStripMenuMasterDeleteKeyValue();
            }
            if (e.Clicks == 2)
            {
                ListBoxCardList_MouseDoubleClick(sender, e);
            }
            return;
        }

        private void SetToolStripMenuMasterCardnumFilters()
        {
            ToolStripMenuMasterCardnumFilters.DropDownItems.Clear();

            // 1. Get the data pairs
            List<string[]> filterPairs = meccgCards.GetCardFilterPairs(masterList[selectedIndex][(int)CardListField.id]);

            // 2. Calculate max length based on the key (filter name)
            int maxLength = filterPairs.Max(ot => ot[0].Length);

            for (int index = 0; index < filterPairs.Count; index++)
            {
                // Get the key value string (e.g., "Ruins & Lairs")
                string filterValue = filterPairs[index][1];

                // --- FIX: ESCAPE THE AMPERSAND ---
                // Replace all single '&' characters with '&&' for display purposes.
                string escapedFilterValue = filterValue.Replace("&", "&&");

                // Construct the padded pair string using the escaped value
                string pair = $"{filterPairs[index][0].PadRight(maxLength + 3)}{escapedFilterValue}";

                ToolStripMenuMasterCardnumFilters.DropDownItems.Add(pair);

                // The rest of the code is fine, though using Consolas at 8.0f for alignment is smart.
                ToolStripMenuMasterCardnumFilters.DropDownItems[index].Font = new Font("Consolas", 8.0f);
            }
        }

        private void SetToolStripMenuMasterCustomFilters()
        {
            int maxLength = 0;
            ToolStripMenuItem customFilters = new("Custom Filters") { Name = "Custom Filters" };
            List<string[]> filterPairs = userKeyValues.GetCardFilterPairs(masterList[selectedIndex][(int)CardListField.id]);
            if (filterPairs.Count > 0)
            {
                maxLength = filterPairs.Max(ot => ot[0].Length);
            }
            for (int index = 0; index < filterPairs.Count; index++)
            {
                string pair = $"{filterPairs[index][0].PadRight(maxLength + 3)}{filterPairs[index][1]}";
                ToolStripMenuItem newKeyPair = new(pair);
                customFilters.DropDownItems.Add(newKeyPair);
                customFilters.DropDownItems[index].Font = new Font("Consolas", 8.0f);
            }
            ContextMenuStripMaster.Items.RemoveByKey("Custom Filters");
            if (customFilters.HasDropDownItems)
            {
                ContextMenuStripMaster.Items.Add(customFilters);
            }
        }

        private void ToolStripMenuMaster_Click(object sender, EventArgs e)
        {
            ListBox sourceListbox = ListBoxMaster;
            List<string[]> sourceList = masterList;
            ListBox destListbox = GetListBox(((ToolStripMenuItem)sender).Name);
            List<string[]> destList = GetList(destListbox);
            AddCard(sourceListbox, destListbox, sourceList, destList, selectedIndex);
            TabControlDeck.SelectTab(GetTabIndex(destListbox));
        }

        private void ListBoxTab_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxTab_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);
                ListBox currentTabListBox = GetListBox(((ListBox)sender).Parent.Name);
                currentTabListBox.Items.Add(str);
                List<string[]> currentTabList = GetList(currentTabListBox);
                currentTabList.Add(masterList[ListBoxMaster.SelectedIndex]);
                currentTabList.Sort(CompareCardsByName);
            }
        }

        private void SetToolStripMenuMasterAddKeyValue()
        {
            List<string> keyNameList = userKeyValues.GetKeyNameList();
            List<string> cardKeyNameList = userKeyValues.GetCardKeyNameList(masterList[selectedIndex][(int)CardListField.id]);
            List<string> keyValueList;

            ContextMenuStripMaster.Items.RemoveByKey("Add Key Value");
            ToolStripMenuItem addKeyValue = new("Add Key Value") { Name = "Add Key Value" };
            for (int keyIndex = 0; keyIndex < keyNameList.Count; keyIndex++)
            {
                if (!cardKeyNameList.Contains(keyNameList[keyIndex]))
                {
                    ToolStripMenuItem newKeyName = new(keyNameList[keyIndex]);
                    keyValueList = userKeyValues.GetKeyValueList(keyNameList[keyIndex]);
                    for (int valueIndex = 1; valueIndex < keyValueList.Count; valueIndex++)
                    {
                        ToolStripMenuItem newKeyValue = new(keyValueList[valueIndex]);
                        newKeyValue.Click += SetCardNewKeyValue;
                        newKeyName.DropDownItems.Add(newKeyValue);
                    }
                    addKeyValue.DropDownItems.Add(newKeyName);
                }
            }
            if (addKeyValue.HasDropDownItems)
            {
                ContextMenuStripMaster.Items.Add(addKeyValue);
            }
        }

        private void SetToolStripMenuMasterDeleteKeyValue()
        {
            List<string[]> filterPairs = userKeyValues.GetCardFilterPairs(masterList[selectedIndex][(int)CardListField.id]);

            ContextMenuStripMaster.Items.RemoveByKey("Delete Key Value");
            ToolStripMenuItem delKeyNameValue = new("Delete Key Value") { Name = "Delete Key Value" };
            for (int keyIndex = 0; keyIndex < filterPairs.Count; keyIndex++)
            {
                ToolStripMenuItem delKeyName = new(filterPairs[keyIndex][0]);
                ToolStripMenuItem delKeyValue = new(filterPairs[keyIndex][1]);
                delKeyValue.Click += DeleteCardKeyValue;
                delKeyName.DropDownItems.Add(delKeyValue);
                delKeyNameValue.DropDownItems.Add(delKeyName);
            }
            if (delKeyNameValue.HasDropDownItems)
            {
                ContextMenuStripMaster.Items.Add(delKeyNameValue);
            }
        }

        private void SetCardNewKeyValue(object sender, EventArgs e)
        {
            string newKeyValue = ((ToolStripMenuItem)sender).Text;
            string newKeyName = ((ToolStripMenuItem)sender).OwnerItem.Text;
            userKeyValues.SetCardKeyValue(newKeyName, newKeyValue, masterList[selectedIndex][(int)CardListField.id]);
        }

        private void DeleteCardKeyValue(object sender, EventArgs e)
        {
            string delKeyName = ((ToolStripMenuItem)sender).OwnerItem.Text;
            userKeyValues.DeleteCardKeyValue(delKeyName, masterList[selectedIndex][(int)CardListField.id]);
            UpdateMasterList("");
        }

        #endregion

        // Display image of selected card
        #region SELECTED_INDEX

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox currentListBox = GetListBox(((ListBox)sender).Parent.Name);
            if (currentListBox != null && currentListBox.SelectedIndex >= 0)
            {
                int currentIndex = currentListBox.SelectedIndex;

                // Deselect other listboxes
                if (currentListBox.Name != ListBoxMaster.Name)
                    ListBoxMaster.ClearSelected();
                if (currentListBox.Name != ListBoxPool.Name)
                    ListBoxPool.ClearSelected();
                if (currentListBox.Name != ListBoxResources.Name)
                    ListBoxResources.ClearSelected();
                if (currentListBox.Name != ListBoxHazards.Name)
                    ListBoxHazards.ClearSelected();
                if (currentListBox.Name != ListBoxSideboard.Name)
                    ListBoxSideboard.ClearSelected();
                if (currentListBox.Name != ListBoxSites.Name)
                    ListBoxSites.ClearSelected();

                // Display card image from correct set
                List<string[]> currentList = GetList(currentListBox);
                string setFolder = currentList[currentIndex][(int)CardListField.set];
                string imageName = currentList[currentIndex][(int)CardListField.image];
                PictureBoxCardImage.Image = cardImageCache.GetOrCreate($"https://cardnum.net/img/cards/{setFolder}/{imageName}", setFolder, imageName);
            }
        }

        #endregion

        // User can choose which sets to include in the master list on left from "Set" menu
        #region SET_MANAGEMENT

        private void ToolStripMenuSet_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasterList(((ToolStripMenuItem)sender).Tag.ToString());
        }

        private void ToolStripMenuSetClearAll_Click(object sender, EventArgs e)
        {
            // Ignore first two items which are select all and clear all
            for (int setIndex = 2; setIndex < ToolStripMenuSet.DropDownItems.Count; setIndex++)
            {
                if (((ToolStripMenuItem)ToolStripMenuSet.DropDownItems[setIndex]).Checked)
                {
                    ((ToolStripMenuItem)ToolStripMenuSet.DropDownItems[setIndex]).PerformClick();
                }
            }
        }

        private void ToolStripMenuSetSelectAll_Click(object sender, EventArgs e)
        {
            // Ignore first two items which are select all and clear all
            for (int setIndex = 2; setIndex < ToolStripMenuSet.DropDownItems.Count; setIndex++)
            {
                if (!((ToolStripMenuItem)ToolStripMenuSet.DropDownItems[setIndex]).Checked)
                {
                    ((ToolStripMenuItem)ToolStripMenuSet.DropDownItems[setIndex]).PerformClick();
                }
            }
        }

        private void UpdateMasterList(string setName)
        {
            // Update list of user selected sets
            if (setName != "")
            {
                if (!setList.Remove(setName))
                {
                    setList.Add(setName);
                }
            }

            // Store current card selected before resetting master list
            string curCardId = "";
            if (ListBoxMaster.SelectedIndex >= 0)
            {
                curCardId = masterList[ListBoxMaster.SelectedIndex][(int)CardListField.id];
            }

            // Retrieve and set new list of cards
            ListBoxMaster.Items.Clear();
            List<string[]> keyValuePairs = GetCardnumKeyValuePairs();
            masterList = meccgCards.GetCardList(setList, keyValuePairs);
            keyValuePairs = GetCustomKeyValuePairs();
            masterList = userKeyValues.GetCardList(masterList, keyValuePairs);
            foreach (var card in masterList)
            {
                ListBoxMaster.Items.Add(card[(int)CardListField.name]);
            }

            // Set currently selected card
            SetCardFocusAfterSetChange(curCardId);
        }

        private void SetCardFocusAfterSetChange(string curCardId)
        {
            // Find new location of selected card if it's still in master list
            int index = 0;
            foreach (var card in masterList)
            {
                if (card[(int)CardListField.id] == curCardId)
                {
                    ListBoxMaster.SelectedIndex = index;
                    return;
                }
                index++;
            }

            // Default choice
            if (ListBoxMaster.Items.Count > 0)
            {
                ListBoxMaster.SelectedIndex = 0;
            }
        }

        #endregion

        // Double click on card in master list copies it to current tab on right, double click
        // on card in current tab on right duplicates it in that tab
        #region DOUBLE_CLICK

        private void ListBoxCardList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = ListBoxMaster;
            ListBox destListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            List<string[]> sourceList = masterList;
            List<string[]> destList = GetList(destListbox);
            int index = ListBoxMaster.IndexFromPoint(e.X, e.Y);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
        }

        private void ListBoxTab_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            ListBox destListbox = sourceListbox;
            List<string[]> sourceList = GetList(destListbox);
            List<string[]> destList = sourceList;
            int index = sourceListbox.IndexFromPoint(e.X, e.Y);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
        }

        #endregion

        // Add/Remove a card from a tab listbox and associated list, compare cards in lists for sorting,
        // update application window title
        #region CARD_OPS

        private void AddCard(ListBox sourceListbox, ListBox destListbox, List<string[]> sourceList, List<string[]> destList, int index)
        {
            if (index == ListBox.NoMatches)
            {
                return;
            }
            destListbox.Items.Add(sourceListbox.Items[index]);
            destList.Add(sourceList[index]);
            destList.Sort(CompareCardsByName);
            UpdateFormTitle();
        }

        private void RemoveCard(ListBox listBox, List<string[]> cardList, int index)
        {
            listBox.Items.Remove(listBox.Items[index]);
            cardList.Remove(cardList[index]);
            if (listBox.Items.Count > 0)
            {
                listBox.SelectedIndex = 0;
            }
            cardList.Sort(CompareCardsByName);
            UpdateFormTitle();
        }

        private void UpdateFormTitle()
        {
            string newFormTitle;

            newFormTitle = "MECCG Deck Builder - \"" + currentDeckTitle + "\" (";
            for (int tabIndex = 0; tabIndex < TabControlDeck.TabCount; tabIndex++)
            {
                newFormTitle += $"{((ListBox)TabControlDeck.Controls[tabIndex].Controls[0]).Items.Count}";
                if (((ListBox)TabControlDeck.Controls[tabIndex].Controls[0]).Name.Contains(Constants.Resource))
                {
                    int noCharacters = 0;
                    List<string[]> keyValuePairs = [];
                    foreach (string[] card in resourceList)
                    {
                        keyValuePairs = meccgCards.GetCardFilterPairs(card[(int)CardListField.id]);
                        if (keyValuePairs.Exists(pair => pair[0] == "Primary" && pair[1] == "Character"))
                        {
                            noCharacters++;
                        }
                    }
                    newFormTitle += $"[{noCharacters}]";
                }
                if (tabIndex != TabControlDeck.TabCount - 1)
                {
                    newFormTitle += "/";
                }
            }
            newFormTitle += ")";
            Text = newFormTitle;
        }

        private int CompareCardsByName(string[] x, string[] y)
        {
            return x[(int)CardListField.name].CompareTo(y[(int)CardListField.name]);
        }

        #endregion

        #region OPEN_CLOSE_DELETE_FILTER

        private void SetFilterMenuDeleteKeyNameValue_Click(object sender, EventArgs e)
        {
            List<string> keyNames = userKeyValues.GetKeyNameList();
            List<string> keyValues;

            ToolStripMenuItem delKeyNameValue = new("Delete Key") { Name = "Delete Key" };
            if (keyNames.Count == 0)
            {
                delKeyNameValue.Enabled = false;
            }
            foreach (string keyName in keyNames)
            {
                ToolStripMenuItem delKeyName = new(keyName);
                delKeyName.Click += DeleteKeyName;
                keyValues = userKeyValues.GetKeyValueList(keyName);
                foreach (string keyValue in keyValues)
                {
                    if (keyValue != "")
                    {
                        ToolStripMenuItem delKeyValue = new(keyValue);
                        delKeyValue.Click += DeleteKeyValue;
                        delKeyName.DropDownItems.Add(delKeyValue);
                    }
                }
                delKeyNameValue.DropDownItems.Add(delKeyName);
            }
            ToolStripMenuFilter.DropDownItems.RemoveByKey("Delete Key");
            ToolStripMenuFilter.DropDownItems.Insert(0, delKeyNameValue);
        }

        private void OpenFilterMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Custom Filters (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = false,
                AutoUpgradeEnabled = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                using StreamReader r = new(openFileDialog.FileName);
                string json = r.ReadToEnd();
                OpenCloseFilter OpenCloseItems = JsonConvert.DeserializeObject<OpenCloseFilter>(json);
                userKeyValues.cards = OpenCloseItems.cards;
                userKeyValues.filters = OpenCloseItems.filters;
                SetKeyNameList(ComboBoxKey3);
                SetKeyNameList(ComboBoxKey4);
                SetKeyValueList(ComboBoxKey3);
                SetKeyValueList(ComboBoxKey4);
                UpdateMasterList("");
            }
        }

        private void SaveFilterMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Custom Filters (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = false,
                AutoUpgradeEnabled = true
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                OpenCloseFilter OpenCloseItems = new()
                {
                    cards = userKeyValues.cards,
                    filters = userKeyValues.filters
                };
                string indentedJsonString = JsonConvert.SerializeObject(OpenCloseItems, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, indentedJsonString);
            }
        }

        #endregion

        // Open/Save a deck, export deck as TTS/Cardnum/Text format
        #region OPEN_CLOSE_EXPORT_DECK

        private void NewToolStripMenu_Click(object sender, EventArgs e)
        {
            var selectedOption = MessageBox.Show("Do you want to save the current deck?", Constants.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (selectedOption == DialogResult.No)
            {
                currentDeckTitle = "New Deck";
                for (int index = 0; index < Constants.TabList.Length; index++)
                {
                    ListBox currentListBox = GetListBox(Constants.TabList[index]);
                    currentListBox.Items.Clear();
                    List<string[]> currentList = GetList(currentListBox);
                    currentList.Clear();
                }
                UpdateFormTitle();
            }
        }

        private void ExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using SaveFileDialog saveFileDialog = new()
            {
                Title = Constants.AppTitle,
                Filter = "Tabletop Simulator (*.json)|*.json|Play MECCG (*.play)|*play|Cardnum (*.cnum)|*.cnum|Archive (*.archive)|*.txt|Text (*.txt)|*.txt",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != DialogResult.Cancel)
            {
                string savePrefix = Path.GetDirectoryName(saveFileDialog.FileName) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                List<List<string[]>> deckTabLists =
                [
                    poolList,
                    resourceList,
                    hazardList,
                    sideboardList,
                    siteList
                ];
                if (saveFileDialog.FilterIndex == (int)SaveType.TTS)
                {
                    meccgCards.Export_TTSfile(poolList, savePrefix + Constants.poolFileSuffix + ".json");
                    meccgCards.Export_TTSfile(resourceList, savePrefix + Constants.resourceFileSuffix + ".json");
                    meccgCards.Export_TTSfile(hazardList, savePrefix + Constants.hazardFileSuffix + ".json");
                    meccgCards.Export_TTSfile(sideboardList, savePrefix + Constants.sideboardFileSuffix + ".json");
                    meccgCards.Export_TTSfile(siteList, savePrefix + Constants.siteFileSuffix + ".json");
                }
                else if (saveFileDialog.FilterIndex == (int)SaveType.PlayMECCG)
                {
                    Cards.Export_PlayMECCGfile(deckTabLists, savePrefix + ".play");
                }
                else if (saveFileDialog.FilterIndex == (int)SaveType.Cardnum)
                {
                    meccgCards.Export_CardnumFile(deckTabLists, savePrefix + ".cnum");
                }
                else if (saveFileDialog.FilterIndex == (int)SaveType.Archive)
                {
                    meccgCards.Export_ArchiveFile(deckTabLists, savePrefix + ".archive");
                }
                else
                {
                    Cards.Export_TextFile(deckTabLists, savePrefix + ".txt");
                }
            }
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Deck (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = false,
                AutoUpgradeEnabled = true
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                currentDeckTitle = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                OpenCloseDeck OpenCloseItems = new()
                {
                    CurrentDeckTitle = currentDeckTitle,
                    setList = setList,
                    poolList = poolList,
                    resourceList = resourceList,
                    hazardList = hazardList,
                    sideboardList = sideboardList,
                    siteList = siteList
                };
                string indentedJsonString = JsonConvert.SerializeObject(OpenCloseItems, Formatting.Indented);
                File.WriteAllText(saveFileDialog.FileName, indentedJsonString);
                UpdateFormTitle();
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Title = Constants.AppTitle,
                CheckPathExists = true,
                DefaultExt = "json",
                Filter = "MECCG Deck Builder Deck (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = false,
                Multiselect = true,
                AutoUpgradeEnabled = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                using StreamReader r = new(openFileDialog.FileName);
                string json = r.ReadToEnd();
                OpenCloseDeck OpenCloseItems = JsonConvert.DeserializeObject<OpenCloseDeck>(json);
                currentDeckTitle = OpenCloseItems.CurrentDeckTitle;
                foreach (ToolStripMenuItem item in ToolStripMenuSet.DropDownItems)
                {
                    if (item.Checked == true)
                    {
                        item.Checked = false;
                    }
                    if (OpenCloseItems.setList.Contains(item.Tag))
                    {
                        item.Checked = true;
                    }
                }
                for (int index = 0; index < Constants.TabList.Length; index++)
                {
                    ListBox currentListBox = GetListBox(Constants.TabList[index]);
                    List<string[]> savedList = GetList(OpenCloseItems, currentListBox);
                    List<string[]> currentList = GetList(currentListBox);
                    currentListBox.Items.Clear();
                    currentList.Clear();
                    foreach (var card in savedList)
                    {
                        currentListBox.Items.Add(card[(int)CardListField.name]);
                        currentList.Add(card);
                    }
                }
                UpdateFormTitle();
            }
        }

        #endregion

        // Right click on current tab with a card selected brings up context menu allowing three operations:
        // 1. Copy card to same or other tab
        // 2. Move card to another tab
        // 3. Delete card from current tab
        #region TABS_CONTEXT_MENU

        private void ToolStripMenuTab_Click(object sender, EventArgs e)
        {
            ListBox sourceListbox = callingListbox;
            List<string[]> sourceList = GetList(sourceListbox);
            if (GetOperation(sender) == Constants.Delete)
            {
                RemoveCard(sourceListbox, sourceList, selectedIndex);
                return;
            }
            ListBox destListbox = GetListBox(((ToolStripMenuItem)sender).Name);
            List<string[]> destList = GetList(destListbox);
            AddCard(sourceListbox, destListbox, sourceList, destList, selectedIndex);
            if (GetOperation(sender) == Constants.Move)
            {
                RemoveCard(sourceListbox, sourceList, selectedIndex);
            }
        }

        private void ListBoxTab_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                callingListbox = GetListBox(((ListBox)sender).Name);
                // Find the item under the mouse.
                selectedIndex = callingListbox.IndexFromPoint(e.Location);
                if (selectedIndex < 0)
                {
                    return;
                }
                List<string[]> callingList = GetList(callingListbox);
                ToolStripMenuTabCardname.Text = callingList[selectedIndex][(int)CardListField.name];
                SetToolStripMenuTabCardnumFilters(callingList[selectedIndex][(int)CardListField.id]);
                SetToolStripMenuTabCustomFilters(callingList[selectedIndex][(int)CardListField.id]);
                ContextMenuStripTabs.Show(Cursor.Position);
            }
        }

        private void SetToolStripMenuTabCardnumFilters(string cardId)
        {
            ToolStripMenuTabCardnumFilters.DropDownItems.Clear();
            List<string[]> filterPairs = meccgCards.GetCardFilterPairs(cardId);
            int maxLength = filterPairs.Max(ot => ot[0].Length);
            Font curFont = ToolStripMenuTabCardnumFilters.Font;
            for (int index = 0; index < filterPairs.Count; index++)
            {
                string pair = $"{filterPairs[index][0].PadRight(maxLength + 3)}{filterPairs[index][1]}";
                ToolStripMenuTabCardnumFilters.DropDownItems.Add(pair);
                ToolStripMenuTabCardnumFilters.DropDownItems[index].Font = new Font("Consolas", 8.0f);
            }
        }
        private void SetToolStripMenuTabCustomFilters(string cardId)
        {
            int maxLength = 0;
            ToolStripMenuItem customFilters = new("Custom Filters") { Name = "Custom Filters" };
            List<string[]> filterPairs = userKeyValues.GetCardFilterPairs(cardId);
            if (filterPairs.Count > 0)
            {
                maxLength = filterPairs.Max(ot => ot[0].Length);
            }
            for (int index = 0; index < filterPairs.Count; index++)
            {
                string pair = $"{filterPairs[index][0].PadRight(maxLength + 3)}{filterPairs[index][1]}";
                ToolStripMenuItem newKeyPair = new(pair);
                customFilters.DropDownItems.Add(newKeyPair);
                customFilters.DropDownItems[index].Font = new Font("Consolas", 8.0f);
            }
            ContextMenuStripTabs.Items.RemoveByKey("Custom Filters");
            if (customFilters.HasDropDownItems)
            {
                ContextMenuStripTabs.Items.Add(customFilters);
            }
        }

        #endregion

        // Utility methods for determining correct listbox, list, tab or operation. Validate filename
        // suffix when opening file
        #region LOOKUPS

        private List<string[]> GetList(ListBox listbox)
        {
            if (listbox == null)
            {
                return null;
            }
            switch (listbox.Name)
            {
                case "ListBoxCardList":
                    return masterList;
                case "ListBoxPool":
                    return poolList;
                case "ListBoxResources":
                    return resourceList;
                case "ListBoxHazards":
                    return hazardList;
                case "ListBoxSideboard":
                    return sideboardList;
                case "ListBoxSites":
                    return siteList;
                default:
                    break;
            }
            return masterList;
        }

        private static List<string[]> GetList(OpenCloseDeck OpenCloseItems, ListBox listbox)
        {
            if (listbox == null)
            {
                return null;
            }
            switch (listbox.Name)
            {
                case "ListBoxPool":
                    return OpenCloseItems.poolList;
                case "ListBoxResources":
                    return OpenCloseItems.resourceList;
                case "ListBoxHazards":
                    return OpenCloseItems.hazardList;
                case "ListBoxSideboard":
                    return OpenCloseItems.sideboardList;
                case "ListBoxSites":
                    return OpenCloseItems.siteList;
                default:
                    break;
            }
            return null;
        }

        private ListBox GetListBox(string stringToSearch)
        {
            StringComparison comp = StringComparison.OrdinalIgnoreCase;
            if (stringToSearch.Contains(Constants.Form, comp))
            {
                return ListBoxMaster;
            }
            else if (stringToSearch.Contains(Constants.Pool, comp))
            {
                return ListBoxPool;
            }
            else if (stringToSearch.Contains(Constants.Resource, comp))
            {
                return ListBoxResources;
            }
            else if (stringToSearch.Contains(Constants.Hazard, comp))
            {
                return ListBoxHazards;
            }
            else if (stringToSearch.Contains(Constants.Sideboard, comp))
            {
                return ListBoxSideboard;
            }
            else if (stringToSearch.Contains(Constants.Site, comp))
            {
                return ListBoxSites;
            }
            return null;
        }

        private static string GetOperation(object sender)
        {
            string operation;
            if (((ToolStripMenuItem)sender).Name.Contains(Constants.Copy))
            {
                operation = Constants.Copy;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Move))
            {
                operation = Constants.Move;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Delete))
            {
                operation = Constants.Delete;
            }
            else
            {
                operation = "";
            }
            return operation;
        }

        private int GetTabIndex(ListBox listbox)
        {
            for (int tabIndex = 0; tabIndex < TabControlDeck.TabCount; tabIndex++)
            {
                if (TabControlDeck.Controls[tabIndex].Controls[0].Name == listbox.Name)
                {
                    return tabIndex;
                }
            }
            return -1;
        }

        #endregion

        // Handles two sets of key name-value filter sets, one read in from Cardnum, the other user maintained
        // Routines to display available filters, edit filters, assign/remove filters from individual cards
        #region FILTER

        private void ComboBoxKeyNameHandleTextEntry(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            string newKeyName = comboBox.Text;
            if (newKeyName != "" && !comboBox.Items.Contains(newKeyName))
            {
                userKeyValues.SetKeyName(newKeyName);
                string curText = ComboBoxKey3.Text;
                SetKeyNameList(ComboBoxKey3);
                if (ComboBoxKey3.Text != curText)
                {
                    ComboBoxKey3.Text = curText;
                }
                curText = ComboBoxKey4.Text;
                SetKeyNameList(ComboBoxKey4);
                if (ComboBoxKey4.Text != curText)
                {
                    ComboBoxKey4.Text = curText;
                }
                comboBox.SelectedItem = newKeyName;
                MessageBox.Show($"\"{newKeyName}\" added to user key name list", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ComboBoxKeyValueHandleTextEntry(object sender, EventArgs e)
        {
            ComboBox keyValueComboBox = (ComboBox)sender;
            string newKeyValue = keyValueComboBox.Text;
            if (newKeyValue == "")
            {
                return;
            }
            ComboBox keyNameComboBox;
            string keyName;
            if (keyValueComboBox.Name.Contains("Value3"))
            {
                keyNameComboBox = ComboBoxKey3;
                keyName = ComboBoxKey3.SelectedItem?.ToString();
            }
            else
            {
                keyNameComboBox = ComboBoxKey4;
                keyName = ComboBoxKey4.SelectedItem?.ToString();
            }
            if (keyName == "")
            {
                return;
            }
            if (!keyValueComboBox.Items.Contains(newKeyValue))
            {
                userKeyValues.SetKeyValue(keyName, newKeyValue);
                keyValueComboBox.DataSource = SetKeyValueList(keyNameComboBox);
                keyValueComboBox.SelectedItem = newKeyValue;
                MessageBox.Show($"\"{newKeyValue}\" added to user key \"{keyName}\" value list", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private List<string[]> GetCardnumKeyValuePairs()
        {
            List<string[]> keyValuePairs = [];
            if (ComboBoxValue1.SelectedIndex >= 1)
            {
                string[] keyValuePair = [ComboBoxKey1.SelectedItem.ToString(), ComboBoxValue1.SelectedItem.ToString()];
                keyValuePairs.Add(keyValuePair);
            }
            if (ComboBoxValue2.SelectedIndex >= 1)
            {
                string[] keyValuePair = [ComboBoxKey2.SelectedItem.ToString(), ComboBoxValue2.SelectedItem.ToString()];
                keyValuePairs.Add(keyValuePair);
            }
            return keyValuePairs;
        }

        private void DeleteKeyName(object sender, EventArgs e)
        {
            string delKeyName = ((ToolStripMenuItem)sender).Text;

            DialogResult dialogResult = MessageBox.Show($"Delete \"{delKeyName}\" key name?", Constants.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                userKeyValues.DeleteKeyName(delKeyName);
                userKeyValues.DeleteCardsKeyName(delKeyName);
                SetKeyNameList(ComboBoxKey3);
                SetKeyNameList(ComboBoxKey4);
                UpdateMasterList("");
                MessageBox.Show($"\"{delKeyName}\" key name deleted.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DeleteKeyValue(object sender, EventArgs e)
        {
            string delKeyValue = ((ToolStripMenuItem)sender).Text;
            string delKeyName = ((ToolStripMenuItem)sender).OwnerItem.Text;

            DialogResult dialogResult = MessageBox.Show($"Delete \"{delKeyValue}\" value from \"{delKeyName}\" key list?", Constants.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                userKeyValues.DeleteKeyValue(delKeyName, delKeyValue);
                userKeyValues.DeleteCardsKeyValue(delKeyName, delKeyValue);
                if (ComboBoxKey3.Text == delKeyName)
                {
                    ComboBoxValue3.DataSource = null;
                    ComboBoxValue3.DataSource = SetKeyValueList(ComboBoxKey3);
                }
                if (ComboBoxKey4.Text == delKeyName)
                {
                    ComboBoxValue4.DataSource = null;
                    ComboBoxValue4.DataSource = SetKeyValueList(ComboBoxKey4);
                }
                UpdateMasterList("");
                MessageBox.Show($"\"{delKeyValue}\" value deleted from \"{delKeyName}\" key list.", Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private List<string[]> GetCustomKeyValuePairs()
        {
            List<string[]> keyValuePairs = [];
            if (ComboBoxValue3.SelectedIndex >= 1)
            {
                string[] keyValuePair = [ComboBoxKey3.SelectedItem.ToString(), ComboBoxValue3.SelectedItem.ToString()];
                keyValuePairs.Add(keyValuePair);
            }
            if (ComboBoxValue4.SelectedIndex >= 1)
            {
                string[] keyValuePair = [ComboBoxKey4.SelectedItem.ToString(), ComboBoxValue4.SelectedItem.ToString()];
                keyValuePairs.Add(keyValuePair);
            }
            return keyValuePairs;
        }

        private void KeyName_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((ComboBox)sender).Name.Contains("Key1"))
            {
                ComboBoxValue1.DataSource = null;
                ComboBoxValue1.DataSource = SetKeyValueList(ComboBoxKey1);
            }
            else if (((ComboBox)sender).Name.Contains("Key2"))
            {
                ComboBoxValue2.DataSource = null;
                ComboBoxValue2.DataSource = SetKeyValueList(ComboBoxKey2);
            }
            else if (((ComboBox)sender).Name.Contains("Key3"))
            {
                ComboBoxValue3.DataSource = null;
                ComboBoxValue3.DataSource = SetKeyValueList(ComboBoxKey3);
            }
            else if (((ComboBox)sender).Name.Contains("Key4"))
            {
                ComboBoxValue4.DataSource = null;
                ComboBoxValue4.DataSource = SetKeyValueList(ComboBoxKey4);
            }
        }

        private void KeyValue_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMasterList("");
        }

        private void SetKeyNameList(ComboBox comboBox)
        {
            if (int.Parse(comboBox.Name[^1].ToString()) <= 2)
            {
                comboBox.DataSource = null;
                comboBox.DataSource = meccgCards.GetKeyNameList();
            }
            else
            {
                comboBox.DataSource = null;
                comboBox.DataSource = userKeyValues.GetKeyNameList();
            }
        }

        private List<string> SetKeyValueList(ComboBox keyNameComboBox)
        {
            if (int.Parse(keyNameComboBox.Name[^1].ToString()) <= 2)
            {
                return meccgCards.GetKeyValueList((string)keyNameComboBox.SelectedItem);
            }
            else
            {
                return userKeyValues.GetKeyValueList((string)keyNameComboBox.SelectedItem);
            }
        }

        private void AdjustWidthComboBox_DropDown(object sender, System.EventArgs e)
        {
            ComboBox senderComboBox = (ComboBox)sender;
            int width = senderComboBox.DropDownWidth;
            Graphics g = senderComboBox.CreateGraphics();
            Font font = senderComboBox.Font;
            int vertScrollBarWidth =
                (senderComboBox.Items.Count > senderComboBox.MaxDropDownItems)
                ? SystemInformation.VerticalScrollBarWidth : 0;

            int newWidth;
            foreach (string s in ((ComboBox)sender).Items)
            {
                newWidth = (int)g.MeasureString(s, font).Width
                    + vertScrollBarWidth;
                if (width < newWidth)
                {
                    width = newWidth;
                }
            }
            senderComboBox.DropDownWidth = width;
        }

        #endregion

        #region TOOLS

        private void ToolStripMenuToolsGetImages_Click(object sender, EventArgs e)
        {
            for (int cardIndex = 0; cardIndex < masterList.Count; cardIndex++)
            {
                string imageName = masterList[cardIndex][(int)CardListField.image];
                string setFolder = masterList[cardIndex][(int)CardListField.set];
                Bitmap cardImage;
                cardImage = CardImageCache.CreateItem($"https://cardnum.net/img/cards/{setFolder}/{imageName}");
                if (cardImage != null)
                {
                    if (!Directory.Exists(setFolder))
                    {
                        Directory.CreateDirectory(setFolder);
                    }
                    cardImage.Save($"{Path.Combine(setFolder, imageName)}");
                }
            }
        }

        private void CreatePBEMFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using SaveFileDialog saveFileDialog = new()
            {
                Title = Constants.AppTitle,
                Filter = "Play MECCG PBEM (*.playPBEM)|*playPBEM",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != DialogResult.Cancel)
            {
                string savePrefix = Path.GetDirectoryName(saveFileDialog.FileName) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                List<List<string[]>> deckTabLists =
                [
                    poolList,
                    resourceList,
                    hazardList,
                    sideboardList,
                    siteList
                ];
                string textOutput = "";
                string filePathOutput = savePrefix + ".playPBEM";

                System.Text.Encoding ansiEncoding = System.Text.Encoding.GetEncoding(1252);

                // Store a complete list of all the cards in the currently selected sets as Pool, excluding region and site cards
                textOutput += "####" + Environment.NewLine;
                textOutput += "Pool" + Environment.NewLine;
                textOutput += "####" + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < masterList.Count; index++)
                {
                    // get card id and cardnum filter pairs, then skip cards whose Primary == "Region" or "Site"
                    string cardId = masterList[index][(int)CardListField.id];
                    List<string[]> filterPairs = meccgCards.GetCardFilterPairs(cardId);

                    bool primaryIsRegionOrSet = filterPairs.Exists(p => p[0] == "Primary" && (p[1] == "Region" || p[1] == "Site"));
                    if (!primaryIsRegionOrSet)
                    {
                        textOutput += Cards.GetPlayMECCGCardname(masterList[index][(int)CardListField.name]) + Environment.NewLine;
                    }
                }
                textOutput += Environment.NewLine;

                // Pool, Resources, Hazards tabs combined
                textOutput += "####" + Environment.NewLine;
                textOutput += "Deck" + Environment.NewLine;
                textOutput += "####" + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < deckTabLists[0].Count; index++)
                {
                    textOutput += Cards.GetPlayMECCGCardname(deckTabLists[0][index][(int)CardListField.name]) + Environment.NewLine;
                }
                for (int index = 0; index < deckTabLists[1].Count; index++)
                {
                    textOutput += Cards.GetPlayMECCGCardname(deckTabLists[1][index][(int)CardListField.name]) + Environment.NewLine;
                }
                for (int index = 0; index < deckTabLists[2].Count; index++)
                {
                    textOutput += Cards.GetPlayMECCGCardname(deckTabLists[2][index][(int)CardListField.name]) + Environment.NewLine;
                }
                textOutput += Environment.NewLine;

                // Sideboard
                textOutput += "####" + Environment.NewLine;
                textOutput += "Sideboard" + Environment.NewLine;
                textOutput += "####" + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < deckTabLists[3].Count; index++)
                {
                    textOutput += Cards.GetPlayMECCGCardname(deckTabLists[3][index][(int)CardListField.name]) + Environment.NewLine;
                }
                textOutput += Environment.NewLine;

                // Sites and regions
                textOutput += "####" + Environment.NewLine;
                textOutput += "Sites" + Environment.NewLine;
                textOutput += "####" + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < masterList.Count; index++)
                {
                    // get card id and cardnum filter pairs, then include cards whose Primary == "Region" or "Site"
                    string cardId = masterList[index][(int)CardListField.id];
                    List<string[]> filterPairs = meccgCards.GetCardFilterPairs(cardId);

                    bool primaryIsRegionOrSet = filterPairs.Exists(p => p[0] == "Primary" && (p[1] == "Region" || p[1] == "Site"));
                    if (primaryIsRegionOrSet)
                    {
                        textOutput += Cards.GetPlayMECCGCardname(masterList[index][(int)CardListField.name]) + Environment.NewLine;
                    }
                }
                textOutput += Environment.NewLine;

                File.WriteAllText(filePathOutput, textOutput, ansiEncoding);

            }
        }

        #endregion
    }
}
