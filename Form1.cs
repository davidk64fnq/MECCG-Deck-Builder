using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{

    internal partial class Form1 : Form
    {
        private List<string[]> cardList = new List<string[]>();
        private List<string[]> poolList = new List<string[]>();
        private List<string[]> resourceList = new List<string[]>();
        private List<string[]> hazardList = new List<string[]>();
        private List<string[]> sideboardList = new List<string[]>();
        private List<string[]> siteList = new List<string[]>();
        private readonly List<string> setList = new List<string>();
        private readonly CardImages meccgImages = new CardImages();
        internal Form1()
        {
            InitializeComponent();

            ((ToolStripMenuItem)ToolStripMenuItemTW).Checked = true;

        }
        private void UpdateCardList(string setName)
        {
            // Maintain list of user selected sets
            if (setList.Contains(setName))
            {
                setList.Remove(setName);
            }
            else
            {
                setList.Add(setName);
            }

            // Store current card selected before resetting card list
            string curCardname = $"{ListBoxCardList.SelectedItem}";
            string curCardSet = "";
            if (ListBoxCardList.SelectedIndex >= 0)
            {
                curCardSet = cardList[ListBoxCardList.SelectedIndex][(int)CardListField.set];
            }
            
            int curSelection = ListBoxCardList.SelectedIndex;

            // Retrieve and set new list of cards
            ListBoxCardList.Items.Clear();
            cardList = meccgImages.GetCardList(setList);
            foreach (var card in cardList)
            {
                ListBoxCardList.Items.Add(card[(int)CardListField.name]);
            }

            // Find new location of selected card if it's still in list
            int locatedIndex = -1;
            int index = -1;
            foreach (var card in cardList)
            {
                index++;
                if (Equals(card[(int)CardListField.name], curCardname) && Equals(card[(int)CardListField.set] == curCardSet))
                {
                    locatedIndex = index;
                    break;
                }
            } 

            // Set currently selected card
            var curListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            if (locatedIndex >= 0)
            {
                // Restore previously selected card in cardList (which could be from another set)
                ListBoxCardList.SelectedIndex = locatedIndex;
            }
            else if (curSelection >= 0 && curListbox.Items.Count > 0)
            {
                // Select first card on active tab
                curListbox.SelectedIndex = 0;
            }
            else if (curListbox.Items.Count == 0)
            {
                // Select first card in cardList
                ListBoxCardList.SelectedIndex = 0;
            }
        }

        private void ListBoxCardList_MouseDown(object sender, MouseEventArgs e)
        {
            ListBoxCardList_SelectedIndexChanged(sender, e);

            if (e.Button == MouseButtons.Left && e.Clicks == 1)
            {
                int index = ListBoxCardList.IndexFromPoint(e.X, e.Y);
                if (index == ListBox.NoMatches)
                {
                    return;
                }
                string cardToMove = ListBoxCardList.Items[index].ToString();
                DoDragDrop(cardToMove, DragDropEffects.Copy);
            }
        }

        private void ListBoxPool_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxResources_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxHazards_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxSideboard_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxSites_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void ListBoxPool_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);

                ListBoxPool.Items.Add(str);
                poolList.Add(cardList[ListBoxCardList.SelectedIndex]);
                poolList.Sort(CompareCardsByName);
            }
        }

        private void ListBoxResources_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);

                ListBoxResources.Items.Add(str);
                resourceList.Add(cardList[ListBoxCardList.SelectedIndex]);
                resourceList.Sort(CompareCardsByName);
            }
        }

        private void ListBoxHazards_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);

                ListBoxHazards.Items.Add(str);
                hazardList.Add(cardList[ListBoxCardList.SelectedIndex]);
                hazardList.Sort(CompareCardsByName);
            }
        }

        private void ListBoxSideboard_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);

                ListBoxSideboard.Items.Add(str);
                sideboardList.Add(cardList[ListBoxCardList.SelectedIndex]);
                sideboardList.Sort(CompareCardsByName);
            }
        }

        private void ListBoxSites_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string str = (string)e.Data.GetData(DataFormats.StringFormat);

                ListBoxSites.Items.Add(str);
                siteList.Add(cardList[ListBoxCardList.SelectedIndex]);
                siteList.Sort(CompareCardsByName);
            }
        }

        private int CompareCardsByName(string[] x, string[] y)
        {
            return x[(int)CardListField.name].CompareTo(y[(int)CardListField.name]);
        }

        private void ListBoxCardList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxCardList.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxPool.ClearSelected();
                ListBoxResources.ClearSelected();
                ListBoxHazards.ClearSelected();
                ListBoxSideboard.ClearSelected();
                ListBoxSites.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{cardList[ListBoxCardList.SelectedIndex][(int)CardListField.set]}\\{ListBoxCardList.SelectedItem}.png");
            }
        }

        private void ListBoxPool_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxPool.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxCardList.ClearSelected();
                ListBoxResources.ClearSelected();
                ListBoxHazards.ClearSelected();
                ListBoxSideboard.ClearSelected();
                ListBoxSites.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{poolList[ListBoxPool.SelectedIndex][(int)CardListField.set]}\\{ListBoxPool.SelectedItem}.png");
            }
        }

        private void ListBoxResources_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxResources.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxCardList.ClearSelected();
                ListBoxPool.ClearSelected();
                ListBoxHazards.ClearSelected();
                ListBoxSideboard.ClearSelected();
                ListBoxSites.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{resourceList[ListBoxResources.SelectedIndex][(int)CardListField.set]}\\{ListBoxResources.SelectedItem}.png");
            }
        }

        private void ListBoxHazards_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxHazards.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxCardList.ClearSelected();
                ListBoxPool.ClearSelected();
                ListBoxResources.ClearSelected();
                ListBoxSideboard.ClearSelected();
                ListBoxSites.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{hazardList[ListBoxHazards.SelectedIndex][(int)CardListField.set]}\\{ListBoxHazards.SelectedItem}.png");
            }
        }

        private void ListBoxSideboard_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxSideboard.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxCardList.ClearSelected();
                ListBoxPool.ClearSelected();
                ListBoxResources.ClearSelected();
                ListBoxHazards.ClearSelected();
                ListBoxSites.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{sideboardList[ListBoxSideboard.SelectedIndex][(int)CardListField.set]}\\{ListBoxSideboard.SelectedItem}.png");
            }
        }

        private void ListBoxSites_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListBoxSites.SelectedIndex >= 0)
            {
                // Deselect other listboxes
                ListBoxCardList.ClearSelected();
                ListBoxPool.ClearSelected();
                ListBoxResources.ClearSelected();
                ListBoxHazards.ClearSelected();
                ListBoxSideboard.ClearSelected();

                // Display card image from correct set
                PictureBoxCardImage.Image = Image.FromFile($"{siteList[ListBoxSites.SelectedIndex][(int)CardListField.set]}\\{ListBoxSites.SelectedItem}.png");
            }
        }

        private void ToolStripMenuItemTW_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.METW);
        }

        private void ToolStripMenuItemTD_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.METD);
        }

        private void ToolStripMenuItemDM_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.MEDM);
        }

        private void ToolStripMenuItemLE_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.MELE);
        }

        private void ToolStripMenuItemAS_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.MEAS);
        }

        private void ToolStripMenuItemWH_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.MEWH);
        }

        private void ToolStripMenuItemBA_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCardList(Constants.MEBA);
        }

        private void ListBoxCardList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = ListBoxCardList;
            ListBox destListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            List<string[]> sourceList = cardList;
            List<string[]> destList = GetDestCardlist(destListbox);
            int index = ListBoxCardList.IndexFromPoint(e.X, e.Y);
            AddCardToListBox(sourceListbox, destListbox, sourceList, destList, index);
        }

        private void ListBoxTab_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBox sourceListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];
            ListBox destListbox = sourceListbox;
            List<string[]> sourceList = GetDestCardlist(destListbox);
            List<string[]> destList = sourceList;
            int index = sourceListbox.IndexFromPoint(e.X, e.Y);
            AddCardToListBox(sourceListbox, destListbox, sourceList, destList, index);
        }

        private List<string[]> GetDestCardlist(ListBox destListbox)
        {
            switch (destListbox.Name)
            {
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
            return cardList;
        }

        private void AddCardToListBox(ListBox sourceListbox, ListBox destListbox, List<string[]> sourceList, List<string[]> destList, int index)
        {
            if (index == ListBox.NoMatches)
            {
                return;
            }
            destListbox.Items.Add(sourceListbox.Items[index]);
            destList.Add(sourceList[index]);
            destList.Sort(CompareCardsByName);
        }

        private void ListBoxTab_MouseDown(object sender, MouseEventArgs e)
        {
            ListBox curListbox = (ListBox)TabControlDeck.SelectedTab.Controls[0];

            if (e.Button == MouseButtons.Right && e.Clicks == 1)
            {
                int index = curListbox.IndexFromPoint(e.X, e.Y);
                if (index == ListBox.NoMatches)
                {
                    return;
                }
                curListbox.Items.Remove(curListbox.Items[index]);
                switch (curListbox.Name)
                {
                    case "ListBoxPool":
                        poolList.Remove(poolList[index]);
                        poolList.Sort(CompareCardsByName);
                        break;
                    case "ListBoxResources":
                        resourceList.Remove(resourceList[index]);
                        resourceList.Sort(CompareCardsByName);
                        break;
                    case "ListBoxHazards":
                        hazardList.Remove(hazardList[index]);
                        hazardList.Sort(CompareCardsByName);
                        break;
                    case "ListBoxSideboard":
                        sideboardList.Remove(sideboardList[index]);
                        sideboardList.Sort(CompareCardsByName);
                        break;
                    case "ListBoxSites":
                        siteList.Remove(siteList[index]);
                        siteList.Sort(CompareCardsByName);
                        break;
                    default:
                        break;
                }
            }

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
                meccgImages.SaveMETW_TTSfile(poolList, savePrefix + Constants.poolFileSuffix);
                meccgImages.SaveMETW_TTSfile(resourceList, savePrefix + Constants.resourceFileSuffix);
                meccgImages.SaveMETW_TTSfile(hazardList, savePrefix + Constants.hazardFileSuffix);
                meccgImages.SaveMETW_TTSfile(sideboardList, savePrefix + Constants.sideboardFileSuffix);
                meccgImages.SaveMETW_TTSfile(siteList, savePrefix + Constants.siteFileSuffix);
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
                    string openPrefix = openFileDialog.FileNames[index].Substring(0, openFileDialog.FileName.IndexOf("_"));
                    string openSuffix = curFileName[curFileName.IndexOf("_")..];
                    switch (openSuffix)
                    {
                        case Constants.poolFileSuffix:
                            poolList = meccgImages.OpenMETW_TTSfile(openPrefix + Constants.poolFileSuffix);
                            ListBoxPool.Items.Clear();
                            foreach (var card in poolList)
                            {
                                ListBoxPool.Items.Add(card[(int)CardListField.name]);
                            }
                            break;
                        case Constants.resourceFileSuffix:
                            resourceList = meccgImages.OpenMETW_TTSfile(openPrefix + Constants.resourceFileSuffix);
                            ListBoxResources.Items.Clear();
                            foreach (var card in resourceList)
                            {
                                ListBoxResources.Items.Add(card[(int)CardListField.name]);
                            }
                            break;
                        case Constants.hazardFileSuffix:
                            hazardList = meccgImages.OpenMETW_TTSfile(openPrefix + Constants.hazardFileSuffix);
                            ListBoxHazards.Items.Clear();
                            foreach (var card in hazardList)
                            {
                                ListBoxHazards.Items.Add(card[(int)CardListField.name]);
                            }
                            break;
                        case Constants.sideboardFileSuffix:
                            sideboardList = meccgImages.OpenMETW_TTSfile(openPrefix + Constants.sideboardFileSuffix);
                            ListBoxSideboard.Items.Clear();
                            foreach (var card in sideboardList)
                            {
                                ListBoxSideboard.Items.Add(card[(int)CardListField.name]);
                            }
                            break;
                        case Constants.siteFileSuffix:
                            siteList = meccgImages.OpenMETW_TTSfile(openPrefix + Constants.siteFileSuffix);
                            ListBoxSites.Items.Clear();
                            foreach (var card in siteList)
                            {
                                ListBoxSites.Items.Add(card[(int)CardListField.name]);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void ToolStripMenuItemPool_Click(object sender, EventArgs e)
        {
            ListBox sourceListbox = ListBoxCardList;
            ListBox destListbox = ListBoxPool;
            List<string[]> sourceList = cardList;
            List<string[]> destList = poolList;
            int index = ListBoxCardList.SelectedIndex;
            AddCardToListBox(sourceListbox, destListbox, sourceList, destList, index);
            TabControlDeck.SelectTab(0);
        }

        private void ToolStripMenuItemResource_Click(object sender, EventArgs e)
        {
            ListBox sourceListbox = ListBoxCardList;
            ListBox destListbox = ListBoxResources;
            List<string[]> sourceList = cardList;
            List<string[]> destList = resourceList;
            int index = ListBoxCardList.SelectedIndex;
            AddCardToListBox(sourceListbox, destListbox, sourceList, destList, index);
            TabControlDeck.SelectTab(1);
        }
    }
}
