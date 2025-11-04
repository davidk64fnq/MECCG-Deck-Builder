using NaturalSort.Extension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly List<SortedDictionary<string, string>> cards = [];
        private readonly List<Dictionary<string, string>> sets = [];
        private readonly List<List<string>> filters = []; // each filter is keyName at index 0 and keyValue(s) from 1..Count
        private List<CardnumCard> CardnumCards;
        private List<CardnumSet> CardnumSets;

        internal string[] filterKeys = [ "Primary", "Alignment", "Artist", "Skill", "MPs", "Mind", "Direct", "Prowess", "Body",
            "Corruption", "Home", "Unique", "Secondary", "Race", "Site", "Region", "Playable", "GoldRing", "GreaterItem",
            "MajorItem", "MinorItem", "Information", "Palantiri", "Scroll", "Hoard", "Haven", "Strikes", "Specific"];

        internal Cards()
        {
            ImportCardnumCardInfo();
            ImportCardnumSetInfo();
        }

        // HttpClient should be a shared, static, or long-lived instance
        private static readonly HttpClient s_httpClient = new();

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
            List<string[]> cardList = [];

            foreach (string set in selectedSets)
            {
                foreach (var card in cards)
                {
                    if (card["set"] == set)
                    {
                        if (CardMatchesFilters(card, keyValuePairs))
                        {
                            string[] cardItems =
                            [
                                $"{card["cardname"]}",
                                $"{card["imageName"]}",
                                $"{card["set"]}",
                                $"{card["id"]}",
                            ];
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
            List<string> keyNames = [];
            foreach (List<string> filter in filters)
            {
                keyNames.Add(filter[0]);
            }
            return keyNames;
        }
        
        internal List<string> GetKeyValueList(string keyName)
        {
            List<string> keyValues =
            [
                ""
            ];
            int keyNameIndex = filters.FindIndex(filter => filter[0] == keyName);
            for (int index = 1; index < filters[keyNameIndex].Count; index++)
            {
                keyValues.Add(filters[keyNameIndex][index]);
            }
            return keyValues;
        }

        internal List<string[]> GetCardFilterPairs(string cardId)
        {
            List<string[]> filterPairs = [];

            for (int index = 0; index < filters.Count; index++)
            {
                if (cards[Convert.ToInt32(cardId)][filters[index][0]] != "")
                {
                    string[] pair = [filters[index][0], cards[Convert.ToInt32(cardId)][filters[index][0]]];
                    filterPairs.Add(pair);
                }
            }
            return filterPairs;
        }

        /// <summary>
        /// Safely retrieves the value of a filter key from a card dictionary, 
        /// truncating the value if the key is "Secondary" and contains a slash.
        /// </summary>
        private static string GetCardFilterValue(SortedDictionary<string, string> card, string key)
        {
            if (card.TryGetValue(key, out string value))
            {
                // 1. Check if the key is "Secondary" and if the value contains a slash
                if (key.Equals("Secondary", StringComparison.OrdinalIgnoreCase) && value.Contains('/'))
                {
                    // 2. Find the index of the first slash
                    int slashIndex = value.IndexOf('/');

                    // 3. Return the substring to the left of the slash
                    return value[..slashIndex].Trim();
                }

                // Return the value as is for other keys or for "Secondary" without a slash
                return value;
            }

            // Return empty string if the key doesn't exist
            return string.Empty;
        }

        #endregion

        #region UTILITIES

        internal static int CompareCardsByName(string[] x, string[] y)
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

        internal static void Export_TextFile(List<List<string[]>> deckTabLists, string filePathOutput)
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

        internal void Export_ArchiveFile(List<List<string[]>> deckTabLists, string filePathOutput)
        {
            string[] archiveCategories = ["Minor Item", "Major Item", "Greater Item", "Gold Ring Item", "Special Item",
                "Resource Short", "Resource Long", "Resource Permanent", "Ally", "Faction",
                "Creature Unique", "Creature", "Hazard Short", "Hazard Long", "Hazard Permanent",
                "Dunadan", "Dwarf", "Elf", "Hobbit", "Man (Wose)", "Wizard",
                "Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Region"];

            int numberOfCardsArchived = 0;

            // NEW STRUCTURE: Outer key is Category, Inner key is Set Name, Value is the List of cards
            Dictionary<string, Dictionary<string, List<string>>> cardDataBySet = [];

            // 1. Initialize the outer dictionary with Categories
            foreach (string title in archiveCategories)
            {
                // Each category starts with an empty inner dictionary
                cardDataBySet.Add(title, []);
            }

            // 2. Populate the dictionary (assuming the logic goes here)
            for (int tabIndex = 0; tabIndex < deckTabLists.Count; tabIndex++)
            {
                for (int index = 0; index < deckTabLists[tabIndex].Count; index++)
                {
                    // Get card data from the class member 'cards' using the ID from the deck list
                    string cardId = deckTabLists[tabIndex][index][(int)CardListField.id];

                    // NOTE: The 'cards' field is a List<SortedDictionary<string, string>>.
                    // We use the ID to get the full card dictionary.
                    if (!int.TryParse(cardId, out int cardIndex))
                    {
                        // Handle bad ID if necessary
                        continue;
                    }

                    // Retrieve the full card data dictionary
                    var card = this.cards[cardIndex];

                    // Extract the required keys
                    string primaryFilter = GetCardFilterValue(card, "Primary");
                    string secondaryFilter = GetCardFilterValue(card, "Secondary");
                    string uniqueFilter = GetCardFilterValue(card, "Unique");
                    string raceFilter = GetCardFilterValue(card, "Race");
                    string siteFilter = GetCardFilterValue(card, "Site");
                    string categoryKey = GetArchiveCategoryKey(primaryFilter, secondaryFilter, uniqueFilter, raceFilter, siteFilter);

                    // Only proceed if a valid category was found (i.e., it's one of the archiveCategories)
                    if (string.IsNullOrEmpty(categoryKey) || !cardDataBySet.ContainsKey(categoryKey))
                    {
                        // Card does not match any required archive category, skip it.
                        continue;
                    }

                    // Extract the data for the nested dictionary and final output
                    string setKey = card["set"]; // deckTabLists[tabIndex][index][(int)CardListField.set]
                    string cardString = card["cardname"]; // deckTabLists[tabIndex][index][(int)CardListField.name]

                    // Example of how to add a card string for a specific Category and Set:
                    if (!cardDataBySet[categoryKey].TryGetValue(setKey, out List<string> value))
                    {
                        value = [];
                        // If this Set hasn't been seen yet for this Category, create its List
                        cardDataBySet[categoryKey].Add(setKey, value);
                    }

                    value.Add(cardString);
                    numberOfCardsArchived++;
                }
            }

            // 3. Write to File
            using StreamWriter writer = new(filePathOutput);

            writer.WriteLine($"--- {filePathOutput} ({numberOfCardsArchived} cards) ---");
            writer.WriteLine(); // Add an empty line for separation

            // Iterate through the Categories
            foreach (var categoryEntry in cardDataBySet)
            {
                string categoryTitle = categoryEntry.Key;
                var setsInThisCategory = categoryEntry.Value;

                writer.WriteLine($"--- CATEGORY: {categoryTitle} ---");

                // Iterate through the Sets within this Category
                foreach (var setEntry in setsInThisCategory)
                {
                    string setTitle = setEntry.Key;
                    List<string> cardsInThisSet = setEntry.Value;

                    if (cardsInThisSet.Count == 0)
                    {
                        continue; // Skip empty sets
                    }

                    // Write the Set title
                    writer.WriteLine($"\tSET: {setTitle} ({cardsInThisSet.Count})");

                    // Write each associated card on a new line
                    foreach (string card in cardsInThisSet)
                    {
                        writer.WriteLine($"\t\t{card}"); // Use tabs for indentation
                    }
                }
                writer.WriteLine(); // Add an empty line for separation
            }
        }

        /// <summary>
        /// Determines the correct Archive Category based on Primary/Secondary/Unique/Race/Site filter values.
        /// </summary>
        private static string GetArchiveCategoryKey(string primary, string secondary, string unique, string race, string site)
        {
            // 1. Resources
            if (primary.Equals("Resource", StringComparison.OrdinalIgnoreCase))
            {
                return secondary switch
                {
                    "Minor Item" => "Minor Item",
                    "Major Item" => "Major Item",
                    "Greater Item" => "Greater Item",
                    "Gold Ring Item" => "Gold Ring Item",
                    "Special Item" => "Special Item",
                    "Short-event" => "Resource Short",
                    "Long-event" => "Resource Long",
                    "Permanent-event" => "Resource Permanent",
                    "Ally" => "Ally",
                    "Faction" => "Faction",
                    _ => string.Empty, // Some other non-archived resource type
                };
            }

            // 2. Creatures
            if (secondary.Equals("Creature", StringComparison.OrdinalIgnoreCase))
            {
                return unique switch
                {
                    "unique" => "Creature Unique",
                    _ => "Creature", // A creature without "Unique" as first word in card text has Unique value of ""
                };
            }

            // 3. Hazards
            if (primary.Equals("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                return secondary switch
                {
                    "Short-event" => "Hazard Short",
                    "Long-event" => "Hazard Long",
                    "Permanent-event" => "Hazard Permanent",
                    _ => string.Empty, // Some other non-archived hazard type (note creatures handled above
                };
            }

            // 4. CHARACTERS 
            if (primary.Equals("Character", StringComparison.OrdinalIgnoreCase))
            {
                if (race.Equals("Dúnadan", StringComparison.OrdinalIgnoreCase))
                {
                    return "Dunadan";
                }

                return race switch
                {
                    "Dwarf" => "Dwarf",
                    "Elf" => "Elf",
                    "Hobbit" => "Hobbit",
                    "Man" => "Man (Wose)",
                    "Wizard" => "Wizard",
                    _ => string.Empty, // Some other non-archived character race
                };
            }

            // 5. SITES (Free-hold, Border-hold, Ruins & Lairs, Shadow-hold, Dark-hold)
            if (primary.Equals("Site", StringComparison.OrdinalIgnoreCase))
            {
                // Your logic for sites needs to be inferred from the Site filter value itself
                return site switch
                {
                    "Free-hold" => "Free-hold",
                    "Border-hold" => "Border-hold",
                    "Ruins & Lairs" => "Ruins & Lairs",
                    "Shadow-hold" => "Shadow-hold",
                    "Dark-hold" => "Dark-hold",
                    "Region" => "Region",
                    _ => string.Empty, // Not an archived site type
                };
            }

            // 6. Regions
            if (primary.Equals("Region", StringComparison.OrdinalIgnoreCase))
            {
                return "Region";
            }

            return string.Empty; // Default if no category match is found
        }

        internal static void Export_PlayMECCGfile(List<List<string[]>> deckTabLists, string filePathOutput)
        {
            string textOutput = "";
            
            System.Text.Encoding ansiEncoding = System.Text.Encoding.GetEncoding(1252);

            // Pool
            textOutput += "####" + Environment.NewLine;
            textOutput += Constants.TabList[0] + Environment.NewLine;
            textOutput += "####" + Environment.NewLine + Environment.NewLine;
            for (int index = 0; index < deckTabLists[0].Count; index++)
            {
                textOutput += GetPlayMECCGCardname(deckTabLists[0][index][(int)CardListField.name]) + Environment.NewLine;
            }
            textOutput += Environment.NewLine;

            // Resources and hazards tabs combined
            textOutput += "####" + Environment.NewLine;
            textOutput += "Deck" + Environment.NewLine;
            textOutput += "####" + Environment.NewLine + Environment.NewLine;
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

            // Sites
            textOutput += "####" + Environment.NewLine;
            textOutput += "Sites" + Environment.NewLine;
            textOutput += "####" + Environment.NewLine + Environment.NewLine;
            for (int index = 0; index < deckTabLists[4].Count; index++)
            {
                textOutput += Cards.GetPlayMECCGCardname(deckTabLists[4][index][(int)CardListField.name]) + Environment.NewLine;
            }
            textOutput += Environment.NewLine;

            File.WriteAllText(filePathOutput, textOutput, ansiEncoding);
        }

        internal static string GetPlayMECCGCardname(string cardName)
        {
            return cardName switch
            {
                "Mûmak - Oliphant" => "Mûmak (Oliphant)",
                "Olog-hai - Trolls" => "Olog-hai (Trolls)",
                _ => cardName // The underscore (_) acts as the 'default' case, returning the original input.
            };
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

        private static string SetTTStransform(int noTabs)
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
            string json = null;
            bool downloadSuccess = false;

            // --- Attempt to Download from URL (Network operation) ---
            try
            {
                // Replace wc.DownloadString(url) with s_httpClient.GetStringAsync(url).Result
                // The .Result causes the current thread to block until the download is complete.
                json = s_httpClient.GetStringAsync(Constants.CardnumCardsURL).Result;

                // 1. Deserialize the downloaded JSON
                CardnumCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);

                downloadSuccess = true;

                // 2. Save the successfully downloaded JSON to a local file
                try
                {
                    // Use the JSON string that was just downloaded
                    using StreamWriter w = new(Constants.CardnumCardsFile);
                    w.Write(json);
                }
                catch (Exception)
                {
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumCardInfo) + "3"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (AggregateException)
            {
                // Catch exceptions from the blocking .Result call (e.g., HttpRequestException)
                // If you need to log, you can inspect ae.InnerException
                downloadSuccess = false;
            }
            catch (Exception)
            {
                // Catch other exceptions (e.g., Json deserialization error)
                downloadSuccess = false;
            }

            // --- If Download Failed, Attempt to Load from File (Fallback) ---
            if (!downloadSuccess)
            {
                try
                {
                    using StreamReader r = new(Constants.CardnumCardsFile);
                    json = r.ReadToEnd();
                    CardnumCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);

                    // Inform user that fallback to local file was necessary
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumCardInfo) + "1"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception)
                {
                    // --- If File Load Fails, Initialize Default Data ---
                    SortedDictionary<string, string> card = new()
                    {
                        { "id", "0" },
                        { "set", "METW" },
                        { "fullCode", "Adrazar (TW)" },
                        { "cardname", "Adrazar" },
                        { "alignment", "Hero" },
                        { "imageName", "metw_adrazar.jpg" }
            };
                    cards.Add(card); // Assuming 'cards' is accessible here
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
                    SortedDictionary<string, string> card = new()
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
                        // The value returned by the indexer. It could be string, int?, or bool.
                        object value = item[filterKeys[keyIndex]];

                        // Safely convert any type to string. 
                        // This will call the appropriate .ToString() method (e.g., Int32.ToString(), String.ToString(), etc.)
                        // For null values, it safely returns string.Empty.
                        string stringValue = value?.ToString() ?? string.Empty;

                        card.Add(filterKeys[keyIndex], stringValue);
                    }
                    if (item.Ice_errata == true)
                    {
                        card["imageName"] = "ice-" + item.ImageName;
                    }
                    if (card["set"].Equals(Constants.METW, StringComparison.CurrentCultureIgnoreCase) || card["set"] == "MEUL")
                    {
                        for (int keyIndex = 0; keyIndex < filterKeys.Length; keyIndex++)
                        {
                            object value = item[filterKeys[keyIndex]];
                            string stringValue = value?.ToString() ?? string.Empty;

                            SetKeyValues(filterKeys[keyIndex], stringValue);
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
                List<string> newKey =
                [
                    filterKeys[keyIndex]
                ];
                filters.Add(newKey);
                filters.Sort(CompareListsByFirstValue);
            }
        }

        #endregion

        #region READ_CARDNUM_SET_INFO

        private void ImportCardnumSetInfo()
        {
            string json = string.Empty; // Initialize json to allow it to be used in the first try-block scope.

            // -------------------------------------------------------------------
            // 1. PRIMARY ATTEMPT: Download from URL (Sync via GetAwaiter().GetResult())
            // -------------------------------------------------------------------
            try
            {
                // Use HttpClient, but force it to behave synchronously by blocking on the result.
                // NOTE: This can freeze the UI thread in desktop applications.
                using HttpClient client = new();

                // This blocks the calling thread until the download is complete.
                json = client.GetStringAsync(Constants.CardnumSetsURL).GetAwaiter().GetResult();

                CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);

                // Attempt to save the data to a local file
                try
                {
                    // Use synchronous file writing
                    File.WriteAllText(Constants.CardnumSetsFile, json);
                }
                catch (Exception)
                {
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "3"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            // -------------------------------------------------------------------
            // 2. FALLBACK ATTEMPT: Read from local file (Sync)
            // -------------------------------------------------------------------
            catch (Exception)
            {
                try
                {
                    // Use synchronous file reading
                    json = File.ReadAllText(Constants.CardnumSetsFile);
                    CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "1"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                // -------------------------------------------------------------------
                // 3. FINAL FALLBACK: Hardcode default set
                // -------------------------------------------------------------------
                catch (Exception)
                {
                    Dictionary<string, string> set = new()
                    {
                        { "id", "0" },
                        { "code", "METW" },
                        { "format", "General" },
                        { "name", "The Wizards" },
                        { "position", "1" },
                        { "dreamcards", "false" },
                        { "released", "true" }
                    };
                    // Assuming 'sets' is a field/property available in this scope
                    // sets.Clear(); // Uncomment if needed
                    // NOTE: The original code used 'sets.Add(set)' before CardnumSets was defined.
                    // If CardnumSets is null at this point, the following foreach will throw
                    // unless 'sets' is a global list intended for this fallback.
                    sets.Add(set);

                    MessageBox.Show(Messages.GetMsgBoxText(nameof(ImportCardnumSetInfo) + "2"), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // -------------------------------------------------------------------
            // 4. PROCESS THE DESERIALIZED DATA
            // -------------------------------------------------------------------
            // Clear the existing data if the download/fallback was successful before populating
            // sets.Clear(); 

            int index = 0;
            // NOTE: If the second catch block executed the FINAL FALLBACK and returned, 
            // we never reach this section. This is consistent with the original logic.

            // An optional null check for safety after deserialization:
            if (CardnumSets == null) return;

            foreach (var item in CardnumSets)
            {
                Dictionary<string, string> set = new()
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
