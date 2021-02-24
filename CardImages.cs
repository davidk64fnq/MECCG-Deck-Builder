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
    internal class CardImages
    {
        private readonly SortedDictionary<string, string> card = new SortedDictionary<string, string>();
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();


        internal CardImages()
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
            ArrayList indices = new ArrayList();

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

        internal List<string[]> GetCardList(List<string> checkedItems)
        {
            List<string[]> cardList = new List<string[]>();

            foreach (string itemChecked in checkedItems)
            {
                foreach (var card in cards)
                {
                    if (card["set"] == itemChecked)
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

        internal void SaveMETW_TTSfile(List<string[]> cardList, string filePathOutput)
        {
            string filePathHeader = @"DeckOutputHeader.txt";
            string outputText = File.ReadAllText(filePathHeader);

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
            count = 0;
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

        internal List<string[]> OpenMETW_TTSfile(string filePathName)
        {
            List<string[]> cardList = new List<string[]>();

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

            return cardList;
        }

        private void ReadMETW_TTSfile()
        {
            using StreamReader r = new StreamReader("METW.json");
            string json = r.ReadToEnd();
            var Items = JsonConvert.DeserializeObject<Root>(json);

            foreach (var card in cards)
            {
                if (card["set"] == Constants.METW)
                {
                    Fastenshtein.Levenshtein lev = new Fastenshtein.Levenshtein($"{card["cardname"]}");
                    int minDistance = 100;
                    int minIndex = 0;
                    int index = 0;
                    foreach (var item in Items.ObjectStates[0].ContainedObjects)
                    {
                        int levenshteinDistance = lev.DistanceFrom(item.Nickname);
                        if (levenshteinDistance < minDistance)
                        {
                            minDistance = levenshteinDistance;
                            minIndex = index;
                        }
                        index++;
                    }

                    card.Add("TTScardID", $"{Items.ObjectStates[0].ContainedObjects[minIndex].CardID}");
                    card.Add("TTScustomDeck", GetProperties(Items.ObjectStates[0].ContainedObjects[minIndex].CustomDeck));
                    card.Add("TTSdescription", $"{Items.ObjectStates[0].ContainedObjects[minIndex].Description}");
                    card.Add("TTSguid", $"{Items.ObjectStates[0].ContainedObjects[minIndex].GUID}");
                    card.Add("TTSnickname", $"{Items.ObjectStates[0].ContainedObjects[minIndex].Nickname}");

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
