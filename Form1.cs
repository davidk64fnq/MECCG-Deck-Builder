using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace MECCG_Deck_Builder
{

    internal partial class Form1 : Form
    {
        // There is a master listbox of all cards in selected sets on the left from which
        // cards can be copied to the tabs on the right. On the right are five tabs each with 
        // a listbox for pool/resource/hazard/sideboard/site, cards can be copied/moved between 
        // tabs and deleted from a tab. Each listbox has an associated list with the card set
        // and id. "meccgCards" is where all information about each card is stored.

        private List<string[]> masterList = new List<string[]>();
        private readonly List<string[]> poolList = new List<string[]>();
        private readonly List<string[]> resourceList = new List<string[]>();
        private readonly List<string[]> hazardList = new List<string[]>();
        private readonly List<string[]> sideboardList = new List<string[]>();
        private readonly List<string[]> siteList = new List<string[]>();
        private readonly List<string> setList = new List<string>();
        private readonly Cards meccgCards = new Cards();
        private ListBox callingListbox;
        private string currentDeckTitle = "New Deck";

        internal Form1()
        {
            InitializeComponent();
            ((ToolStripMenuItem)ToolStripMenuTW).Checked = true;
            UpdateFormTitle();

        }

        // User can drag cards from master list to current tab on right with left mouse click hold
        // and use context menu with right click to copy selected card to a tab on the right
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
            if (e.Clicks == 2)
            {
                ListBoxCardList_MouseDoubleClick(sender, e);
            }
            return;
        }
        private void ToolStripMenuMaster_Click(object sender, EventArgs e)
        {
            ListBox sourceListbox = ListBoxMaster;
            List<string[]> sourceList = masterList;
            int index = sourceListbox.SelectedIndex;
            ListBox destListbox = GetListBox(((ToolStripMenuItem)sender).Name);
            List<string[]> destList = GetList(destListbox);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
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
                PictureBoxCardImage.Image = Image.FromFile($"{currentList[currentIndex][(int)CardListField.set]}\\{currentListBox.SelectedItem}.png");
            }
        }

        #endregion

        // User can choose which sets to include in the master list on left from "Set" menu
        #region SET_MANAGEMENT

        private void ToolStripMenuSet_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasterList(((ToolStripMenuItem)sender).Tag.ToString());
        }

        private void UpdateMasterList(string setName)
        {
            // Update list of user selected sets
            if (setList.Contains(setName))
            {
                setList.Remove(setName);
            }
            else
            {
                setList.Add(setName);
            }

            // Store current card selected before resetting master list
            string curCardId = "";
            if (ListBoxMaster.SelectedIndex >= 0)
            {
                curCardId = masterList[ListBoxMaster.SelectedIndex][(int)CardListField.id];
            }

            // Retrieve and set new list of cards
            ListBoxMaster.Items.Clear();
            masterList = meccgCards.GetCardList(setList);
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

        // Add/Remove a card from a tab listbox and associated list, compare cards in lists for sorting
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

        // Save and read from file a deck in TTS format
        #region OPEN_CLOSE

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Tabletop Simulator (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != DialogResult.Cancel)
            {
                string savePrefix = saveFileDialog.FileName.Substring(0, saveFileDialog.FileName.IndexOf("."));
                meccgCards.SaveMETW_TTSfile(poolList, savePrefix + Constants.poolFileSuffix);
                meccgCards.SaveMETW_TTSfile(resourceList, savePrefix + Constants.resourceFileSuffix);
                meccgCards.SaveMETW_TTSfile(hazardList, savePrefix + Constants.hazardFileSuffix);
                meccgCards.SaveMETW_TTSfile(sideboardList, savePrefix + Constants.sideboardFileSuffix);
                meccgCards.SaveMETW_TTSfile(siteList, savePrefix + Constants.siteFileSuffix);
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string curPath = "";
            string curFilename = "";
            string curSuffix = "";

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = this.Text,

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "json",
                Filter = "Tabletop Simulator (*.json)|*.json",
                FilterIndex = 1,
                RestoreDirectory = false,
                Multiselect = true,
                AutoUpgradeEnabled = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                for (int index = 0; index < openFileDialog.FileNames.Length; index++)
                {
                    curPath = openFileDialog.FileNames[index];
                    curFilename = Path.GetFileName(curPath);
                    if (curFilename.LastIndexOf("_") >= 1)
                    {
                        curSuffix = curFilename[curFilename.LastIndexOf("_")..^0];
                    }
                    if (SuffixValid(curSuffix))
                    {
                        ListBox currentListBox = GetListBox(curSuffix);
                        List<string[]> currentList = GetList(currentListBox);
                        meccgCards.OpenMETW_TTSfile(curPath, currentList);
                        currentListBox.Items.Clear();
                        foreach (var card in currentList)
                        {
                            currentListBox.Items.Add(card[(int)CardListField.name]);
                        }
                        currentDeckTitle = curFilename[0..(curFilename.LastIndexOf("_") - 1)];
                    }
                    else
                    {
                        string message = $"Unable to open \"{curFilename}\"\n\nExpecting filename to end in one of:\n";
                        message += $"\t\"{Constants.poolFileSuffix}\", \n\t\"{Constants.resourceFileSuffix}\", \n\t\"{Constants.hazardFileSuffix}\",\n";
                        message += $"\t\"{Constants.sideboardFileSuffix}\", \n\t\"{Constants.siteFileSuffix}\"";
                        MessageBox.Show(message, "Open File Error");
                    }

                }
                UpdateFormTitle(); // To last valid filename opened
            }
        }

        private Boolean SuffixValid(string curSuffix)
        {
            switch (curSuffix)
            {
                case Constants.poolFileSuffix:
                    return true;
                case Constants.resourceFileSuffix:
                    return true;
                case Constants.hazardFileSuffix:
                    return true;
                case Constants.sideboardFileSuffix:
                    return true;
                case Constants.siteFileSuffix:
                    return true;
                default:
                    break;
            }
            return false;
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
            int index = sourceListbox.SelectedIndex;
            if (GetOperation(sender) == Constants.Delete)
            {
                RemoveCard(sourceListbox, sourceList, index);
                return;
            }
            ListBox destListbox = GetListBox(((ToolStripMenuItem)sender).Name);
            List<string[]> destList = GetList(destListbox);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
            if (GetOperation(sender) == Constants.Move)
            {
                RemoveCard(sourceListbox, sourceList, index);
            }
        }

        private void ContextMenuStripTabs_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (((ListBox)((ContextMenuStrip)sender).SourceControl).SelectedIndex == ListBox.NoMatches)
            {
                e.Cancel = true;
            }
            else
            {
                callingListbox = (ListBox)((ContextMenuStrip)sender).SourceControl;
            }
        }

        #endregion

        // Utility methods for determining correct listbox, list, tab or operation
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

        private string GetOperation(object sender)
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
    }
}
