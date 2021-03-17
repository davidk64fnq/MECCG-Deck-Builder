using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Reflection;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using Octokit;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MECCG_Deck_Builder
{
    internal class Cards
    {
        private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        private readonly List<Dictionary<string, string>> sets = new List<Dictionary<string, string>>();
        private TTScard TTSitems;
        private List<CardnumCard> CardnumItems;
        private List<CardnumSet> CardnumSets;

        internal Cards()
        {
            ImportCardnumData();
            ImportCardnumSetInfo();
            ImportTTSdata();
        }
        internal int GetSetCount()
        {
            return CardnumSets.Count;
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

        internal void Export_TextFile(List<string[]> cardList, string filePathOutput)
        {
            string textOutput = "";

            for (int index = 0; index < cardList.Count; index++)
            {
                textOutput += cardList[index][(int)CardListField.name] + Environment.NewLine;
            }
            File.WriteAllText(filePathOutput, textOutput);
        }

        internal void Export_CardnumFile(List<string[]> cardList, string filePathOutput)
        {
            int cardIndex;
            string cardnumOutput = "";

            for (int index = 0; index < cardList.Count; index++)
            {
                cardIndex = Convert.ToInt32(cardList[index][(int)CardListField.id]);
                cardnumOutput += $"1 {cards[cardIndex]["fullCode"]}{Environment.NewLine}";
            }
            File.WriteAllText(filePathOutput, cardnumOutput);
        }

        internal void Export_METW_TTSfile(List<string[]> cardList, string filePathOutput)
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

        private void ImportTTSdata()
        {
            using StreamReader r = new StreamReader("TTSReleasedCards1670.json");
            string json = r.ReadToEnd();
            TTSitems = JsonConvert.DeserializeObject<TTScard>(json);

            foreach (var card in cards)
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
            //    card.Add("TTScustomDeck", GetTTScustomDeck(TTSitems.ObjectStates[0].ContainedObjects[minIndex].CustomDeck));
                card.Add("TTSdescription", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Description}");
                card.Add("TTSguid", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].GUID}");
                card.Add("TTSnickname", $"{TTSitems.ObjectStates[0].ContainedObjects[minIndex].Nickname}");
            }
        }
        private void ImportCardnumData()
        {
            using StreamReader r = new StreamReader("Cardnum.json");
            string json = r.ReadToEnd();
            CardnumItems = JsonConvert.DeserializeObject<List<CardnumCard>>(json);

            int index = 0;
            foreach (var item in CardnumItems)
            {
                if (item.Dreamcard != false || item.Released != false)
                {
                    SortedDictionary<string, string> card = new SortedDictionary<string, string>
                    {
                        { "id", $"{index++}" },
                        { "set", $"{item.Set}" },
                        { "fullCode", $"{item.FullCode}" },
                        { "cardname", $"{item.NameEN}" },
                        { "alignment", $"{item.Alignment}" },
                        { "imageName", $"{item.ImageName}" }
                    };
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
                json = wc.DownloadString("https://github.com/rezwitsX/cardnum/blob/master/fdata/sets-dc.json?raw=true");
                CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
            }
            catch (Exception)
            {
                try
                {
                    using StreamReader r = new StreamReader("CardnumSets.json");
                    json = r.ReadToEnd();
                    CardnumSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                    MessageBox.Show(Errors.GetMsgBoxText(nameof(ImportCardnumSetInfo)), Constants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception)
                {

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
