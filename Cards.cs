using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Reflection;
using System.Collections;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly SortedDictionary<string, string> card = new SortedDictionary<string, string>();
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        private Root TTSitems;

        internal Cards()
        {
            LoadImages();
            ReadMETW_TTSfile();
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
                        { "cardname", $"{file[(set.Length + 1)..^4]}" },
                        { "set", $"{set}" },
                        { "id", $"{index++}" }
                    };
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
                        string[] cardItems = new string[3];
                        cardItems[(int)CardListField.name] = $"{card["cardname"]}";
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

        internal void SaveMETW_TTSfileNew(List<string[]> cardList, string filePathOutput)
        {
            int cardIndex;

            // Make a copy of TTSitems object called saveTTSitems as starting point then modify
            // before serialising to output file
            string copyTTSitems = JsonConvert.SerializeObject(TTSitems);
            Root saveTTSitems = JsonConvert.DeserializeObject<Root>(copyTTSitems); 

            // Update DeckIDs list
            saveTTSitems.ObjectStates[0].DeckIDs.Clear();
            for (int index = 0; index < cardList.Count; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                saveTTSitems.ObjectStates[0].DeckIDs.Add(Convert.ToInt32(cards[cardIndex]["TTScardID"]));
            }

            // Update ContainedObject list
            string temp = JsonConvert.SerializeObject(TTSitems.ObjectStates[0].ContainedObjects[0]);
            saveTTSitems.ObjectStates[0].ContainedObjects.Clear();
            for (int index = 0; index < cardList.Count; index++)
            {
                ContainedObject containedObject = JsonConvert.DeserializeObject<ContainedObject>(temp);
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                containedObject.Nickname = cards[cardIndex]["TTSnickname"];
                containedObject.Description = cards[cardIndex]["TTSdescription"];
                containedObject.CardID = Convert.ToInt32(cards[cardIndex]["TTScardID"]);
                SetTTScustomDeck(containedObject, cards[cardIndex]["TTScustomDeck"]);
                containedObject.GUID = cards[cardIndex]["TTSguid"];
                saveTTSitems.ObjectStates[0].ContainedObjects.Add(containedObject);
            }

            // Serialize
            string indentedJsonString = JsonConvert.SerializeObject(saveTTSitems, Formatting.Indented);
            File.WriteAllText(filePathOutput, indentedJsonString);
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

        internal void SaveMETW_TTSfile(List<string[]> cardList, string filePathOutput)
        {
            string filePathHeader = @"DeckOutputHeader.txt";
            string outputText = File.ReadAllText(filePathHeader);
            int cardIndex;

            SaveMETW_TTSfileNew(cardList, filePathOutput);
            return;

            foreach (var card in cardList)
            {
                if (card[(int)CardListField.set] != Constants.METW)
                {
                    Form1 form1 = new Form1();
                    MessageBox.Show(
                        "Deck contains at least one non Middle Earth The Wizards card. Unable to save in Tabletop Simulator format.",
                        form1.Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }
            }

            /*
            int count = 0;
            foreach (var card in cardList)
            {
                int index = Convert.ToInt32(card[(int)CardListField.id]);
                outputText += "        " + cards[index]["TTScardID"];
                if (++count < cards.Count)
                {
                    outputText += ",";
                }
                outputText += Environment.NewLine;
            }
            */

            for (int index = 0; index < cardList.Count - 2; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                outputText += "        " + cards[cardIndex]["TTScardID"] + "," + Environment.NewLine;
            }
            cardIndex = Convert.ToInt32(cardList[^1][(int)CardListField.id]);
            outputText += "        " + cards[cardIndex]["TTScardID"] + Environment.NewLine;

            string filePathCustomDeck = @"DeckOutputCustomDeck.txt";
            outputText += File.ReadAllText(filePathCustomDeck);

            string filePathCardTransform = @"DeckOutputCardTransform.txt";
            string cardTransform = File.ReadAllText(filePathCardTransform);
            string filePathCardColor = @"DeckOutputCardColor.txt";
            string cardColor = File.ReadAllText(filePathCardColor);
            string filePathCardSideways = @"DeckOutputCardSideways.txt";
            string cardSideways = File.ReadAllText(filePathCardSideways);
            string filePathCardWidth = @"DeckOutputCardWidth.txt";
            string cardWidth = File.ReadAllText(filePathCardWidth);
            int count = 0;
            foreach (var card in cardList)
            {
                int index = Convert.ToInt32(card[(int)CardListField.id]);
                outputText += cardTransform;
                outputText += "          \"Nickname\": \"" + cards[index]["TTSnickname"].Replace("\"", "\\\"") + "\"," + Environment.NewLine;
                outputText += "          \"Description\": \"" + cards[index]["TTSdescription"].Replace("\"", "\\\"") + "\"," + Environment.NewLine;
                outputText += cardColor;
                outputText += "          \"CardID\": " + cards[index]["TTScardID"] + "," + Environment.NewLine;
                outputText += cardSideways;
                outputText += "            \"" + cards[index]["TTScustomDeck"][(2)..] + "\": {" + Environment.NewLine;
                string filePathCardDeck = $"DeckOutputCardDeck{cards[index]["TTScustomDeck"][(2)..]}.txt";
                string cardDeck = File.ReadAllText(filePathCardDeck);
                outputText += cardDeck;
                outputText += cardWidth;
                outputText += "          \"GUID\": \"" + cards[index]["TTSguid"] + "\"" + Environment.NewLine;
                outputText += "        }";
                if (++count < cards.Count)
                {
                    outputText += ",";
                }
                outputText += Environment.NewLine;
            }

            string filePathFooter = @"DeckOutputFooter.txt";
            outputText += File.ReadAllText(filePathFooter);

            File.WriteAllText(filePathOutput, outputText);
        }

        internal void OpenMETW_TTSfile(string filePathName, List<string[]> cardList)
        {
            using StreamReader r = new StreamReader(filePathName);
            string json = r.ReadToEnd();
            var Items = JsonConvert.DeserializeObject<Root>(json);

            for (int index = 0; index < Items.ObjectStates[0].ContainedObjects.Count; index++)
            {
                int TTScardID = Items.ObjectStates[0].ContainedObjects[index].CardID;
                int cardIndex = GetCardIndex("TTScardID", TTScardID.ToString());
                string[] cardItems = new string[3];
                cardItems[(int)CardListField.name] = $"{cards[cardIndex]["cardname"]}";
                cardItems[(int)CardListField.set] = $"{cards[cardIndex]["set"]}";
                cardItems[(int)CardListField.id] = $"{cards[cardIndex]["id"]}";
                cardList.Add(cardItems);
            }
            cardList.Sort(CompareCardsByName);
        }

        private void ReadMETW_TTSfile()
        {
            using StreamReader r = new StreamReader("METW.json");
            string json = r.ReadToEnd();
            TTSitems = JsonConvert.DeserializeObject<Root>(json);

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
                    card.Add("TTScustomDeck", GetProperties(TTSitems.ObjectStates[0].ContainedObjects[minIndex].CustomDeck));
                    card.Add("TTSdescription", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Description}");
                    card.Add("TTSguid", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].GUID}");
                    card.Add("TTSnickname", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Nickname}");

                }
            }
        }

        private string GetProperties<T>(T instance)
        {
            return GetProperties(typeof(T), instance);
        }

        private string GetProperties(Type classType, object instance)
        {
            foreach (PropertyInfo property in classType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = property.GetValue(instance);
                if (value != null)
                {
                    int idx = value.ToString().LastIndexOf('.');
                    return value.ToString()[(idx + 1)..];
                }
            }
            return "";
        }
    }
}
