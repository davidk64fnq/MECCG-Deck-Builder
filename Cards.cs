using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Reflection;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly SortedDictionary<string, string> card = new SortedDictionary<string, string>();
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        private TTScard TTSitems;
        private List<CardnumCard> CardnumItems;
        private readonly Utilities utilities = new Utilities();

        internal Cards()
        {
            utilities.StripDCcardnumData();

            LoadImages();
            SetMETW_TTSdata();
            SetCardnumData();
        }
        private string GetCardValue(int cardIndex, string cardKey)
        {
            return $"{cards[cardIndex][cardKey]}";
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

        private void LoadImages()
        {
            string[] sets = { "metw", "metd", "medm", "mele", "meas", "mewh", "meba" };

            int index = 0;
            foreach (string set in sets)
            {
                var imageFiles = from file in Directory.EnumerateFiles(set)
                                 select file;

                foreach (var file in imageFiles)
                {
                    SortedDictionary<string, string> card = new SortedDictionary<string, string>
                    {
                        { "imageName", $"{file[(set.Length + 1)..^4]}" },
                        { "set", $"{set}" },
                        { "id", $"{index++}" }
                    };
                    // Four image files in Against the Shadow are minion versions and identified by ".1" in filename
                    // to distinguish from hero versions of same name in same set
                    if (card["imageName"].Contains(".1"))
                    {
                        card.Add("cardname", card["imageName"][0..^2]);
                        card.Add("cardnumAlignment", "Minion");
                    }
                    else
                    {
                        card.Add("cardname", card["imageName"]);
                        card.Add("cardnumAlignment", "Hero"); // Will be corrected when cardnum data imported
                    }
                    cards.Add(card);
                }
            }
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

        internal void Save_TextFile(List<string[]> cardList, string filePathOutput)
        {
            string textOutput = "";

            for (int index = 0; index < cardList.Count; index++)
            {
                textOutput += cardList[index][(int)CardListField.name] + Environment.NewLine;
            }
            File.WriteAllText(filePathOutput, textOutput);
        }

        internal void Save_CardnumFile(List<string[]> cardList, string filePathOutput)
        {
            int cardIndex;
            string cardnumOutput = "";

            for (int index = 0; index < cardList.Count; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                cardnumOutput += $"1 {cards[cardIndex]["cardnumFullCode"]}{Environment.NewLine}";
            }
            File.WriteAllText(filePathOutput, cardnumOutput);
        }

        internal void SaveMETW_TTSfile(List<string[]> cardList, string filePathOutput)
        {
            int cardIndex;

            // Check that the list only contains METW cards
            if (!METWonly(cardList))
            {
                string message = $"Unable to save \"{Path.GetFileName(filePathOutput)}\" in TTS format. ";
                message += $"Application only supports the saving of Middle Earth the Wizards cards at present";
                MessageBox.Show(message, Constants.AppTitle);
                return;
            }

            // Make a copy of TTSitems object called saveTTSitems as starting point then modify
            // before serialising to output file
            string copyTTSitems = JsonConvert.SerializeObject(TTSitems);
            TTScard saveTTSitems = JsonConvert.DeserializeObject<TTScard>(copyTTSitems); 

            // Update DeckIDs list
            saveTTSitems.ObjectStates[0].DeckIDs.Clear();
            for (int index = 0; index < cardList.Count; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                saveTTSitems.ObjectStates[0].DeckIDs.Add(Convert.ToInt32(cards[cardIndex]["TTScardID"]));
            }

            // Update deck transform values
            saveTTSitems.ObjectStates[0].Transform.RotX = 0;
            saveTTSitems.ObjectStates[0].Transform.RotY = 180;
            saveTTSitems.ObjectStates[0].Transform.RotZ = 180;

            // Update ContainedObject list
            string temp = JsonConvert.SerializeObject(TTSitems.ObjectStates[0].ContainedObjects[0]);
            saveTTSitems.ObjectStates[0].ContainedObjects.Clear();
            for (int index = 0; index < cardList.Count; index++)
            {
                ContainedObject containedObject = JsonConvert.DeserializeObject<ContainedObject>(temp);
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                containedObject.Transform.RotX = 0;
                containedObject.Transform.RotY = 180;
                containedObject.Transform.RotZ = 180;
                containedObject.Nickname = cards[cardIndex]["TTSnickname"];
                containedObject.Description = cards[cardIndex]["TTSdescription"];
                containedObject.CardID = Convert.ToInt32(cards[cardIndex]["TTScardID"]);
                SetTTScustomDeck(containedObject, cards[cardIndex]["TTScustomDeck"]);
                containedObject.GUID = cards[cardIndex]["TTSguid"];
                saveTTSitems.ObjectStates[0].ContainedObjects.Add(containedObject);
            }

            // Serialize
            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            string indentedJsonString = JsonConvert.SerializeObject(saveTTSitems, Formatting.Indented, jsonSettings);
            indentedJsonString = indentedJsonString.Replace("\"CD", "\"");
            File.WriteAllText(filePathOutput, indentedJsonString);
        }

        internal Boolean METWonly(List<string[]> cardList)
        {
            for (int index = 0; index < cardList.Count; index++)
            {
                if (cardList[index][(int)CardListField.set] != Constants.METW)
                    return false;
            }
            return true;
        }

        internal void SetTTScustomDeck(ContainedObject containedObject, string deckID)
        {
            CustomDeck deckInstance = TTSitems.ObjectStates[0].CustomDeck;
            foreach (PropertyInfo deckProperty in typeof(CustomDeck).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object deckValue = deckProperty.GetValue(deckInstance);
                if (deckProperty.Name == deckID)
                {
                    CustomDeck cardInstance = containedObject.CustomDeck;
                    foreach (PropertyInfo cardProperty in typeof(CustomDeck).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (cardProperty.Name == deckID)
                        {
                            containedObject.CustomDeck = new CustomDeck();
                            cardProperty.SetValue(containedObject.CustomDeck, deckValue);
                            return;
                        }
                    }

                }
            }
        }

        internal void OpenMETW_TTSfile(string filePathName, List<string[]> cardList)
        {
            using StreamReader r = new StreamReader(filePathName);
            string json = r.ReadToEnd();
            var Items = JsonConvert.DeserializeObject<TTScard>(json);

            for (int index = 0; index < Items.ObjectStates[0].ContainedObjects.Count; index++)
            {
                int TTScardID = Items.ObjectStates[0].ContainedObjects[index].CardID;
                int cardIndex = GetCardIndex("TTScardID", TTScardID.ToString());
                string[] cardItems = new string[4];
                cardItems[(int)CardListField.name] = $"{cards[cardIndex]["cardname"]}";
                cardItems[(int)CardListField.image] = $"{cards[cardIndex]["imageName"]}";
                cardItems[(int)CardListField.set] = $"{cards[cardIndex]["set"]}";
                cardItems[(int)CardListField.id] = $"{cards[cardIndex]["id"]}";
                cardList.Add(cardItems);
            }
            cardList.Sort(CompareCardsByName);
        }

        private void SetMETW_TTSdata()
        {
            using StreamReader r = new StreamReader("METW_TTS.json");
            string json = r.ReadToEnd();
            TTSitems = JsonConvert.DeserializeObject<TTScard>(json);

            foreach (var card in cards)
            {
                if (card["set"] == Constants.METW)
                {
                    Fastenshtein.Levenshtein lev = new Fastenshtein.Levenshtein($"{card["cardname"]}");
                    int minDistance = 100;
                    int minIndex = 0;
                    int index = 0;
                    foreach (var item in TTSitems.ObjectStates[0].ContainedObjects)
                    {
                        int levenshteinDistance = lev.DistanceFrom(item.Nickname);
                        if (levenshteinDistance < minDistance)
                        {
                            minDistance = levenshteinDistance;
                            minIndex = index;
                        }
                        index++;
                    }

                    card.Add("TTScardID", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].CardID}");
                    card.Add("TTScustomDeck", GetTTScustomDeck(TTSitems.ObjectStates[0].ContainedObjects[minIndex].CustomDeck));
                    card.Add("TTSdescription", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Description}");
                    card.Add("TTSguid", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].GUID}");
                    card.Add("TTSnickname", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Nickname}");

                }
            }
        }
        private void SetCardnumData()
        {
            using StreamReader r = new StreamReader("Cardnum.json");
            string json = r.ReadToEnd();
            CardnumItems = JsonConvert.DeserializeObject<List<CardnumCard>>(json);

            foreach (var card in cards)
            {
                Fastenshtein.Levenshtein lev = new Fastenshtein.Levenshtein($"{card["cardname"]}");
                int minDistance = 100;
                int minIndex = 0;
                int index = 0;
                foreach (var item in CardnumItems)
                {
                    int levenshteinDistance = lev.DistanceFrom(item.NameEN);
                    if (levenshteinDistance < minDistance)
                    {
                        minDistance = levenshteinDistance;
                        minIndex = index;
                    }
                    else if ((levenshteinDistance == minDistance) && (card["set"] == item.Set.ToLower()) && (card["cardnumAlignment"] == item.Alignment))
                    {
                        minDistance = levenshteinDistance;
                        minIndex = index;
                    }
                    index++;
                }

                card.Add("cardnumFullCode", $"{CardnumItems[minIndex].FullCode}");
            }
        }

        internal string GetTTScustomDeck(CustomDeck cardInstance)
        {
            foreach (PropertyInfo cardProperty in typeof(CustomDeck).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = cardProperty.GetValue(cardInstance);
                if (value != null)
                {
                    return cardProperty.Name;
                }
            }
            return "No custom deck found";
        }
    }
}
