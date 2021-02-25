using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{

    internal partial class Form1 : Form
    {
        // There is a master listbox of all cards in selected sets on the left, from which
        // cards can be copied to the tabs on the right. On the right are five tabs each with 
        // a listbox for pool/resource/hazard/sideboard/site, cards can be copied/moved between 
        // tabs and deleted from a tab. Each listbox has an associated list with the card set
        // and id. "meccgCards" is where all information about each card is stored.

        private List<string[]> masterList = new List<string[]>();
        private List<string[]> poolList = new List<string[]>();
        private List<string[]> resourceList = new List<string[]>();
        private List<string[]> hazardList = new List<string[]>();
        private List<string[]> sideboardList = new List<string[]>();
        private List<string[]> siteList = new List<string[]>();
        private readonly List<string> setList = new List<string>();
        private readonly Cards meccgCards = new Cards();

        internal Form1()
        {
            InitializeComponent();

            ((ToolStripMenuItem)ToolStripMenuTW).Checked = true;

        }

        private void ListBoxMasterList_MouseDown(object sender, MouseEventArgs e)
        {
            // Only use the right mouse button.
            if (e.Button == MouseButtons.Right)
            {
                // Find the item under the mouse.
                int index = ListBoxMasterList.IndexFromPoint(e.Location);
                if (index < 0)
                {
                    return;
                }
                ListBoxMasterList.SelectedIndex = index;
                ListBox_SelectedIndexChanged(sender, e);

                // Drag the item.
                string cardText = ListBoxMasterList.SelectedItem.ToString();
                ListBoxMasterList.DoDragDrop(cardText, DragDropEffects.Copy);
            }
            return;
        }

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox currentListBox = GetCurrentListBox(((ListBox)sender).Parent.Name);
            if (currentListBox != null && currentListBox.SelectedIndex >= 0)
            {
                int currentIndex = currentListBox.SelectedIndex;

                // Deselect other listboxes
                if (currentListBox.Name != ListBoxMasterList.Name)
                    ListBoxMasterList.ClearSelected();
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
                List<string[]> currentList = GetCardListRef(currentListBox);
                PictureBoxCardImage.Image = Image.FromFile($"{currentList[currentIndex][(int)CardListField.set]}\\{currentListBox.SelectedItem}.png");
            }
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
                ListBox currentTabListBox = GetCurrentTabListBox(TabControlDeck.SelectedIndex);
                currentTabListBox.Items.Add(str);
                List<string[]> currentTabList = GetCardListRef(currentTabListBox);
                currentTabList.Add(masterList[ListBoxMasterList.SelectedIndex]);
                currentTabList.Sort(CompareCardsByName);
            }
        }

        private ListBox GetCurrentTabListBox(int currentTabIndex)
        {
            switch (currentTabIndex)
            {
                case (int)DeckTab.pool:
                    return ListBoxPool;
                case (int)DeckTab.resources:
                    return ListBoxResources;
                case (int)DeckTab.hazards:
                    return ListBoxHazards;
                case (int)DeckTab.sideboard:
                    return ListBoxSideboard;
                case (int)DeckTab.sites:
                    return ListBoxSites;
                default:
                    break;
            }
            return null;
        }

        private List<string[]> GetCardListRef(ListBox listbox)
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

        private int CompareCardsByName(string[] x, string[] y)
        {
            return x[(int)CardListField.name].CompareTo(y[(int)CardListField.name]);
        }

        private ListBox GetCurrentListBox(string senderParent)
        {
            if (senderParent == Constants.senderMaster)
            {
                return ListBoxMasterList;
            }
            else if (senderParent == Constants.senderPool)
            {
                return ListBoxPool;
            }
            else if (senderParent == Constants.senderResource)
            {
                return ListBoxResources;
            }
            else if (senderParent == Constants.senderHazard)
            {
                return ListBoxHazards;
            }
            else if (senderParent == Constants.senderSideboard)
            {
                return ListBoxSideboard;
            }
            else if (senderParent == Constants.senderSite)
            {
                return ListBoxSites;
            }
            return null;
        }

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
            if (ListBoxMasterList.SelectedIndex >= 0)
            {
                curCardId = masterList[ListBoxMasterList.SelectedIndex][(int)CardListField.id];
            }

            // Retrieve and set new list of cards
            ListBoxMasterList.Items.Clear();
            masterList = meccgCards.GetCardList(setList);
            foreach (var card in masterList)
            {
                ListBoxMasterList.Items.Add(card[(int)CardListField.name]);
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
                    ListBoxMasterList.SelectedIndex = index;
                    return;
                }
                index++;
            }

            // Default choice
            if (ListBoxMasterList.Items.Count > 0)
            {
                ListBoxMasterList.SelectedIndex = 0;
            }
        }

        private void ListBoxCardList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = ListBoxMasterList;
            ListBox destListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            List<string[]> sourceList = masterList;
            List<string[]> destList = GetCardListRef(destListbox);
            int index = ListBoxMasterList.IndexFromPoint(e.X, e.Y);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
        }

        private void ListBoxTab_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            ListBox destListbox = sourceListbox;
            List<string[]> sourceList = GetCardListRef(destListbox);
            List<string[]> destList = sourceList;
            int index = sourceListbox.IndexFromPoint(e.X, e.Y);
            AddCard(sourceListbox, destListbox, sourceList, destList, index);
        }

        private void AddCard(ListBox sourceListbox, ListBox destListbox, List<string[]> sourceList, List<string[]> destList, int index)
        {
            if (index == ListBox.NoMatches)
            {
                return;
            }
            destListbox.Items.Add(sourceListbox.Items[index]);
            destList.Add(sourceList[index]);
            destList.Sort(CompareCardsByName);
        }

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
                    string curFileName = openFileDialog.FileNames[index];
                    string openSuffix = curFileName[curFileName.IndexOf("_")..];

                    ListBox currentListBox = GetOpenFileListBox(openSuffix);
                    List<string[]> currentList = GetCardListRef(currentListBox);
                    currentList = meccgCards.OpenMETW_TTSfile(curFileName);
                    currentListBox.Items.Clear();
                    foreach (var card in currentList)
                    {
                        currentListBox.Items.Add(card[(int)CardListField.name]);
                    }
                }
            }
        }

        private ListBox GetOpenFileListBox(string fileSuffix)
        {
            if (fileSuffix == Constants.poolFileSuffix)
            {
                return ListBoxPool;
            }
            else if (fileSuffix == Constants.resourceFileSuffix)
            {
                return ListBoxResources;
            }
            else if (fileSuffix == Constants.hazardFileSuffix)
            {
                return ListBoxHazards;
            }
            else if (fileSuffix == Constants.sideboardFileSuffix)
            {
                return ListBoxSideboard;
            }
            else if (fileSuffix == Constants.siteFileSuffix)
            {
                return ListBoxSites;
            }
            return null;
        }

        private ListBox GetListBoxRef(object sender)
        {
            if (((ToolStripMenuItem)sender).Name.Contains(Constants.Pool))
            {
                return ListBoxPool;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Resource))
            {
                return ListBoxResources;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Hazard))
            {
                return ListBoxHazards;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Sideboard))
            {
                return ListBoxSideboard;
            }
            else if (((ToolStripMenuItem)sender).Name.Contains(Constants.Site))
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

        private void RemoveCard(ListBox listBox, List<string[]> cardList, int index)
        {
            listBox.Items.Remove(listBox.Items[index]);
            cardList.Remove(cardList[index]);
            cardList.Sort(CompareCardsByName);
        }

        private void ToolStripMenuTab_Click(object sender, EventArgs e)
        {
            if (ListBoxMasterList.SelectedIndex >= 0)
            {
                return;
            }
            ListBox sourceListbox = GetCurrentListBox();
            if (sourceListbox != null)
            {
                List<string[]> sourceList = GetCardListRef(sourceListbox);
                int index = sourceListbox.SelectedIndex;
                if (GetOperation(sender) == Constants.Delete)
                {
                    RemoveCard(sourceListbox, sourceList, index);
                    return;
                }
                ListBox destListbox = GetListBoxRef(sender);
                List<string[]> destList = GetCardListRef(destListbox);
                if (index >= 0)
                {
                    AddCard(sourceListbox, destListbox, sourceList, destList, index);
                    if (GetOperation(sender) == Constants.Move)
                    {
                        RemoveCard(sourceListbox, sourceList, index);
                    }
                }
            }
        }

        private ListBox GetCurrentListBox()
        {
            if (ListBoxMasterList.SelectedIndex >= 0)
            {
                return ListBoxMasterList;
            }
            else if (ListBoxPool.SelectedIndex >= 0)
            {
                return ListBoxPool;
            }
            else if (ListBoxResources.SelectedIndex >= 0)
            {
                return ListBoxResources;
            }
            else if (ListBoxHazards.SelectedIndex >= 0)
            {
                return ListBoxHazards;
            }
            else if (ListBoxSideboard.SelectedIndex >= 0)
            {
                return ListBoxSideboard;
            }
            else if (ListBoxSites.SelectedIndex >= 0)
            {
                return ListBoxSites;
            }
            return null;
        }
    }
}
