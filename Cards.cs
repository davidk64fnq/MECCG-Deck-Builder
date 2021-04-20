using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Net;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        private readonly List<Dictionary<string, string>> sets = new List<Dictionary<string, string>>();
        private readonly List<List<string>> filters = new List<List<string>>(); // each filter is keyName at index 0 and keyValue(s) from 1..Count
        private List<CardnumCard> CardnumCards;
        private List<CardnumSet> CardnumSets;

        internal string[] filterKeys = { "Primary", "Alignment", "Artist", "Skill", "MPs", "Mind", "Direct", "Prowess", "Body",
            "Corruption", "Home", "Unique", "Secondary", "Race", "Site", "Region", "Playable", "GoldRing", "GreaterItem",
            "MajorItem", "MinorItem", "Information", "Palantiri", "Scroll", "Hoard", "Haven", "Strikes", "Specific"};

        internal Cards()
        {
            ImportCardnumCardInfo();
            ImportCardnumSetInfo();
        }

        #region GET_SET_INFO

        internal int GetSetCount()
        {
            return sets.Count;
        }

        internal string GetSetValue(int setIndex, string setKey)
        {
            return $"{sets[setIndex][setKey]}";
        }

        #endregion

        #region GET_CARD_INFO

        private int GetCardIndex(string cardKey, string cardValue)
        {
            return cards.FindIndex(card => card[cardKey] == cardValue);
        }

        internal List<string[]> GetCardList(List<string> selectedSets, List<string[]> keyValuePairs)
        {
            List<string[]> cardList = new List<string[]>();

            foreach (string set in selectedSets)
            {
                foreach (var card in cards)
                {
                    if (card["set"] == set)
                    {
                        if (CardMatchesFilters(card, keyValuePairs))
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
            }
            cardList.Sort(CompareCardsByName);

            return cardList;
        }

        #endregion

        #region GET_FILTER_INFO

        internal List<string> GetKeyNameList()
        {
            List<string> keyNames = new List<string>();
            foreach (List<string> filter in filters)
            {
                keyNames.Add(filter[0]);
            }
            return keyNames;
        }
        
        internal List<string> GetKeyValueList(string keyName)
        {
            List<string> keyValues = new List<string>
            {
                ""
            };
            int keyNameIndex = filters.FindIndex(filter => filter[0] == keyName);
            for (int index = 1; index < filters[keyNameIndex].Count; index++)
            {
                keyValues.Add(filters[keyNameIndex][index]);
            }
            return keyValues;
        }

        internal List<string[]> GetCardFilterPairs(string cardId)
        {
            List<string[]> filterPairs = new List<string[]>();

            for (int index = 0; index < filters.Count; index++)
            {
                if (cards[Convert.ToInt32(cardId)][filters[index][0]] != "")
                {
                    string[] pair = { filters[index][0], cards[Convert.ToInt32(cardId)][filters[index][0]] };
                    filterPairs.Add(pair);
                }
            }
            return filterPairs;
        }

        #endregion

        #region UTILITIES

        private static int CompareCardsByName(string[] x, string[] y)
        {
            return $"{x[(int)CardListField.name]}".CompareTo($"{y[(int)CardListField.name]}");
        }

        internal static int CompareListsByFirstValue(List<string> x, List<string> y)
        {
            return $"{x[0]}".CompareTo($"{y[0]}");
        }

        private static bool CardMatchesFilters(SortedDictionary<string, string> card, List<string[]> keyValuePairs)
        {
            bool cardMatch = true;
            for (int index = 0; index < keyValuePairs.Count; index++)
            {
                if (keyValuePairs[0][0] != null && !card[keyValuePairs[index][0]].Contains(keyValuePairs[index][1]))
                {
                    cardMatch = false;
                }
            }
            return cardMatch;
        }

        #endregion

        #region EXPORT

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

        #endregion

        #region READ_CARDNUM_CARD_INFO

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

            // Initialise key names in filters list
            SetKeyNames();
            
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
                        { "text", $"{item.Text}" },
                        { "imageName", $"{item.ImageName}" }
                    };
                    for (int keyIndex = 0; keyIndex < filterKeys.Length; keyIndex++)
                    {
                        card.Add(filterKeys[keyIndex], Convert.ToString(item[filterKeys[keyIndex]]));
                    }
                    if (item.Ice_errata == true)
                    {
                        card["imageName"] = "ice-" + item.ImageName;
                    }
                    if (card["set"] == Constants.METW.ToUpper() || card["set"] == "MEUL")
                    {
                        for (int keyIndex = 0; keyIndex < filterKeys.Length; keyIndex++)
                        {
                            SetKeyValues(filterKeys[keyIndex], Convert.ToString(item[filterKeys[keyIndex]]));
                        }
                    }
                    cards.Add(card);
                }
            }
        }

        /// <summary>
        /// Adds new key value and sorts values [1..] in list
        /// </summary>
        /// <param name="key">The key name being updated [0]</param>
        /// <param name="value">The additional key value [1..]</param>
        private void SetKeyValues(string key, string value)
        {
            if (value == "")
            {
                return;
            }

            // Get index of key values in filters
            int filtersIndex = filters.FindIndex(keyList => keyList[0] == key);
            if (key != "Skill")
            {
                // Check whether key value is in key values and add if it is not
                if (filters[filtersIndex].IndexOf(value) == -1)
                {
                    filters[filtersIndex].Add(value);
                    filters[filtersIndex].Sort(1, filters[filtersIndex].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                }
            }
            else
            {
                string[] words = value.Split(null);
                foreach (string word in words)
                {
                    // Check whether key value is in key values and add if it is not
                    if (filters[filtersIndex].IndexOf(word) == -1)
                    {
                        filters[filtersIndex].Add(word);
                        filters[filtersIndex].Sort(1, filters[filtersIndex].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                    }
                }
            }

        }

        /// <summary>
        /// Set key names as index 0 position in filter member key value lists
        /// </summary>
        private void SetKeyNames()
        {
            for (int keyIndex = 0; keyIndex < filterKeys.Length; keyIndex++)
            {
                List<string> newKey = new List<string>
                {
                    filterKeys[keyIndex]
                };
                filters.Add(newKey);
                filters.Sort(CompareListsByFirstValue);
            }
        }

        #endregion

        #region READ_CARDNUM_SET_INFO

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

        #endregion
    }
}
