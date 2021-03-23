using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Net;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        private readonly List<Dictionary<string, string>> sets = new List<Dictionary<string, string>>();
        private List<CardnumCard> CardnumCards;
        private List<CardnumSet> CardnumSets;

        internal Cards()
        {
            ImportCardnumCardInfo();
            ImportCardnumSetInfo();
        }

        internal int GetSetCount()
        {
            return sets.Count;
        }

        internal string GetSetValue(int setIndex, string setKey)
        {
            return $"{sets[setIndex][setKey]}";
        }

        private int GetCardIndex(string cardKey, string cardValue)
        {
            int index = 0;
            foreach (var card in cards)
            {
                if ($"{card[cardKey]}" == cardValue)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        internal List<string[]> GetCardList(List<string> selectedSets)
        {
            List<string[]> cardList = new List<string[]>();

            foreach (string set in selectedSets)
            {
                foreach (var card in cards)
                {
                    if (card["set"] == set)
                    {
                        string[] cardItems = new string[4];
                        cardItems[(int)CardListField.name] = $"{card["cardname"]}";
                        cardItems[(int)CardListField.image] = $"{card["imageName"]}";
                        cardItems[(int)CardListField.set] = $"{card["set"]}";
                        cardItems[(int)CardListField.id] = $"{card["id"]}";
                        cardList.Add(cardItems);
                    }
                }
            }
            cardList.Sort(CompareCardsByName);

            return cardList;
        }

        private static int CompareCardsByName(string[] x, string[] y)
        {
            return $"{x[(int)CardListField.name]}".CompareTo($"{y[(int)CardListField.name]}");
        }

        private static int CompareCardsByImageName(SortedDictionary<string, string> x, SortedDictionary<string, string> y)
        {
            return $"{x["imageName"]}".CompareTo($"{y["imageName"]}");
        }

        internal void Export_TextFile(List<List<string[]>> deckTabLists, string filePathOutput)
        {
            string textOutput = "";

            for (int tabIndex = 0; tabIndex < deckTabLists.Count; tabIndex++) 
            {
                textOutput += Constants.TabList[tabIndex] + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < deckTabLists[tabIndex].Count; index++)
                {
                    textOutput += deckTabLists[tabIndex][index][(int)CardListField.name] + Environment.NewLine;
                }
                textOutput += Environment.NewLine;
            }

            File.WriteAllText(filePathOutput, textOutput);
        }

        internal void Export_CardnumFile(List<List<string[]>> deckTabLists, string filePathOutput)
        {
            int cardIndex;
            string cardnumOutput = "";

            for (int tabIndex = 0; tabIndex < deckTabLists.Count; tabIndex++)
            {
                cardnumOutput += Constants.TabList[tabIndex] + Environment.NewLine + Environment.NewLine;
                for (int index = 0; index < deckTabLists[tabIndex].Count; index++)
                {
                    cardIndex = Convert.ToInt32(deckTabLists[tabIndex][index][(int)CardListField.id]);
                    cardnumOutput += $"1 {cards[cardIndex]["fullCode"]}{Environment.NewLine}";
                }
                cardnumOutput += Environment.NewLine;
            }

            File.WriteAllText(filePathOutput, cardnumOutput);
        }

        internal void Export_TTSfile(List<string[]> cardList, string filePathOutput)
        {
            int cardIndex;
            int noTabs;

            string jsonOutput = "";
            jsonOutput += "{\n";
            jsonOutput += "\t\"ObjectStates\": [\n";
            jsonOutput += "\t\t{\n";
            jsonOutput += "\t\t\t\"Name\": \"Deck\",\n";
            noTabs = 3;
            jsonOutput += SetTTStransform(noTabs);
            jsonOutput += "\t\t\t\"DeckIDs\": [\n";
            for (int index = 0; index < cardList.Count; index++)
            {
                jsonOutput += $"\t\t\t\t{(index + 1) * 100}";
                if (index != cardList.Count - 1)
                {
                    jsonOutput += ",";
                }
                jsonOutput += "\n";
            }
            jsonOutput += "\t\t\t],\n";
            jsonOutput += "\t\t\t\"CustomDeck\": {\n";
            for (int index = 0; index < cardList.Count; index++)
            {
                noTabs = 4;
                jsonOutput += SetTTScustomDeck(cardList, index, noTabs);
                if (index != cardList.Count - 1)
                {
                    jsonOutput += ",";
                }
                jsonOutput += "\n";
            }
            jsonOutput += "\t\t\t},\n";
            jsonOutput += "\t\t\t\"ContainedObjects\": [\n";
            for (int index = 0; index < cardList.Count; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                jsonOutput += "\t\t\t\t{\n";
                jsonOutput += "\t\t\t\t\t\"Name\": \"Card\",\n";
                noTabs = 5;
                jsonOutput += SetTTStransform(noTabs);
                jsonOutput += "\t\t\t\t\t\"CustomDeck\": {\n";
                noTabs = 6;
                jsonOutput += SetTTScustomDeck(cardList, index, noTabs);
                jsonOutput += "\n\t\t\t\t\t},\n";
                jsonOutput += $"\t\t\t\t\t\"Nickname\": \"{cards[cardIndex]["cardname"].Replace("\"", "\\\"")}\",\n";
                jsonOutput += $"\t\t\t\t\t\"CardID\": \"{(index + 1) * 100}\"\n";
                jsonOutput += "\t\t\t\t}";
                if (index != cardList.Count - 1)
                {
                    jsonOutput += ",";
                }
                jsonOutput += "\n";
            }
            jsonOutput += "\t\t\t],\n";
            jsonOutput += "\t\t}\n";
            jsonOutput += "\t]\n";
            jsonOutput += "}\n";
            File.WriteAllText(filePathOutput, jsonOutput);
        }

        private string SetTTStransform(int noTabs)
        {
            string transform;
            string tabString = "";
            for (int tabNo = 0; tabNo < noTabs; tabNo++)
            {
                tabString += "\t";
            }
            transform = $"{tabString}\"Transform\": {{\n";
            transform += $"{tabString}\t\"RotY\": 180.0,\n";
            transform += $"{tabString}\t\"RotZ\": 180.0,\n";
            transform += $"{tabString}\t\"ScaleX\": 1.0,\n";
            transform += $"{tabString}\t\"ScaleY\": 1.0,\n";
            transform += $"{tabString}\t\"ScaleZ\": 1.0\n";
            transform += $"{tabString}}},\n";
            return transform;
        }

        private string SetTTScustomDeck(List<string[]> cardList, int index, int noTabs)
        {
            string customDeck;
            int cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
            string setFolder = cards[cardIndex]["set"];
            string imageName = cards[cardIndex]["imageName"];
            string tabString = "";
            for (int tabNo = 0; tabNo < noTabs; tabNo++)
            {
                tabString += "\t";
            }
            customDeck = $"{tabString}\"{index + 1}\": {{\n";
            customDeck += $"{tabString}\t\"FaceURL\": \"https://cardnum.net/img/cards/{setFolder}/{imageName}\",\n";
            customDeck += $"{tabString}\t\"BackURL\": \"https://i.imgur.com/gUPyTI4.jpg\",\n";
            customDeck += $"{tabString}\t\"BackIsHidden\": \"true\",\n";
            customDeck += $"{tabString}\t\"NumWidth\": 1,\n";
            customDeck += $"{tabString}\t\"NumHeight\": 1\n";
            customDeck += $"{tabString}}}";
            return customDeck;
        }

        private void ImportCardnumCardInfo()
        {
            string json;
            try
            {
                WebClient wc = new WebClient();
                json = wc.DownloadString(Constants.CardnumCardsURL);
                CardnumCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);
                try
                {
                    using StreamWriter w = new StreamWriter(Constants.CardnumCardsFile);
                    w.Write(json);
                }
                catch (Exception)
                {
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumCardInfo) + "3"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception)
            {
                try
                {
                    using StreamReader r = new StreamReader(Constants.CardnumCardsFile);
                    json = r.ReadToEnd();
                    CardnumCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumCardInfo) + "1"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception)
                {
                    SortedDictionary<string, string> card = new SortedDictionary<string, string>
                    {
                        { "id", "0" },
                        { "set", "METW" },
                        { "fullCode", "Adrazar (TW)" },
                        { "cardname", "Adrazar" },
                        { "alignment", "Hero" },
                        { "imageName", "metw_adrazar.jpg" }
                    };
                    cards.Add(card);
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumCardInfo) + "2"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int index = 0;
            foreach (var item in CardnumCards)
            {
                if (item.Dreamcard == true || item.Released == true)
                {
                    SortedDictionary<string, string> card = new SortedDictionary<string, string>
                    {
                        { "id", $"{index++}" },
                        { "set", $"{item.Set}" },
                        { "fullCode", $"{item.FullCode}" },
                        { "cardname", $"{item.NameEN}" },
                        { "alignment", $"{item.Alignment}" },
                        { "text", $"{item.Text}" },
                        { "imageName", $"{item.ImageName}" }
                    };
                    if (item.Ice_errata == true)
                    {
                        card["imageName"] = "ice-" + item.ImageName;
                    }
                    cards.Add(card);
                }
            }
        }

        private void ImportCardnumSetInfo()
        {
            string json;
            try
            {
                WebClient wc = new WebClient();
                json = wc.DownloadString(Constants.CardnumSetsURL);
                CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                try
                {
                    using StreamWriter w = new StreamWriter(Constants.CardnumSetsFile);
                    w.Write(json);
                }
                catch (Exception)
                {
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "3"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception)
            {
                try
                {
                    using StreamReader r = new StreamReader(Constants.CardnumSetsFile);
                    json = r.ReadToEnd();
                    CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "1"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception)
                {
                    Dictionary<string, string> set = new Dictionary<string, string>
                    {
                        { "id", "0" },
                        { "code", "METW" },
                        { "format", "General" },
                        { "name", "The Wizards" },
                        { "position", "1" },
                        { "dreamcards", "false" },
                        { "released", "true" }
                    };
                    sets.Add(set);
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "2"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int index = 0;
            foreach (var item in CardnumSets)
            {
                Dictionary<string, string> set = new Dictionary<string, string>
                {
                    { "id", $"{index++}" },
                    { "code", $"{item.Code}" },
                    { "format", $"{item.Format}" },
                    { "name", $"{item.Name}" },
                    { "position", $"{item.Position}" },
                    { "dreamcards", $"{item.Dreamcards}" },
                    { "released", $"{item.Released}" }
                };
                sets.Add(set);
            }
        }
    }
}
