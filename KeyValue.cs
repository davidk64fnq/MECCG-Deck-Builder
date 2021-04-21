
using System;
using System.Collections.Generic;
using System.Linq;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    class KeyValue
    {
        internal List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        internal List<List<string>> filters = new List<List<string>>(); // each filter is keyName at index 0 and keyValue(s) from 1..Count


        private static bool CardMatchesFilters(SortedDictionary<string, string> card, List<string[]> keyValuePairs)
        {
            bool cardMatch = true;
            for (int index = 0; index < keyValuePairs.Count; index++)
            {
                if (card.ContainsKey(keyValuePairs[index][0]))
                {
                    if (!card[keyValuePairs[index][0]].Contains(keyValuePairs[index][1]))
                    {
                        cardMatch = false;
                    }
                }
                else
                {
                    cardMatch = false;
                }
            }
            return cardMatch;
        }

        /// <summary>
        /// Delete key name-value pair (and card) if it's the last pair
        /// </summary>
        internal void DeleteCardKeyValue(string keyName, string cardId)
        {
            int cardIndex = cards.FindIndex(card => card["id"] == cardId);
            if (cardIndex == -1)
            {
                return;
            }
            cards[cardIndex].Remove(keyName);
            if (cards[cardIndex].Count == 1)
            {
                cards.RemoveAt(cardIndex);
            }
        }

        internal void DeleteCardsKeyName(string keyName)
        {
            foreach (SortedDictionary<string, string> card in cards)
            {
                if (card.ContainsKey(keyName))
                {
                    card.Remove(keyName);
                }
            }
            cards.RemoveAll(card => card.Count == 1);
        }

        internal void DeleteCardsKeyValue(string keyName, string keyValue)
        {
            foreach (SortedDictionary<string, string> card in cards)
            {
                if (card.ContainsKey(keyName) && card[keyName] == keyValue)
                {
                    card.Remove(keyName);
                }
            }
            cards.RemoveAll(card => card.Count == 1);
        }

        internal void DeleteKeyName(string keyName)
        {
            int keyNameIndex = filters.FindIndex(filter => filter[0] == keyName);
            filters.RemoveAt(keyNameIndex);
        }

        internal void DeleteKeyValue(string keyName, string keyValue)
        {
            int keyNameIndex = filters.FindIndex(filter => filter[0] == keyName);
            filters[keyNameIndex].Remove(keyValue);
        }

        internal List<string[]> GetCardFilterPairs(string cardId)
        {
            List<string[]> filterPairs = new List<string[]>();

            int cardIndex = cards.FindIndex(card => card["id"] == cardId);
            if (cardIndex == -1)
            {
                return filterPairs;
            }
            foreach (var pair in cards[cardIndex])
            {
                if (pair.Key != "id")
                {
                    string[] keyNameValue = { pair.Key, pair.Value };
                    filterPairs.Add(keyNameValue);
                }
            }

            return filterPairs;
        }

        internal List<string> GetCardKeyNameList(string cardId)
        {
            List<string> cardKeyNameList = new List<string>();

            int cardIndex = cards.FindIndex(card => card["id"] == cardId);
            if (cardIndex == -1)
            {
                return cardKeyNameList;
            }
            foreach (var pair in cards[cardIndex])
            {
                if (pair.Key != "id")
                {
                    cardKeyNameList.Add(pair.Key);
                }
            }

            return cardKeyNameList;
        }

        internal List<string[]> GetCardList(List<string[]> cardList, List<string[]> keyValuePairs)
        {
            List<string[]> returnCardList = new List<string[]>();

            foreach (string[] master in cardList)
            {
                // Is card from masterList in list of custom cards
                int masterIndex = cards.FindIndex(card => card["id"] == master[(int)CardListField.id]);

                // Check card matches current custom filter values
                if (masterIndex != -1)
                {
                    if (CardMatchesFilters(cards[masterIndex], keyValuePairs))
                    {
                        returnCardList.Add(master);
                    }
                }
                else if (keyValuePairs.Count == 0)
                {
                    returnCardList.Add(master);
                }
            }

            return returnCardList;
        }

        internal List<string> GetKeyNameList()
        {
            List<string> keyNames = new List<string>();
            foreach (var pair in filters)
            {
                keyNames.Add(pair[0]);
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
            if (keyNameIndex == -1)
            {
                return keyValues;
            }
            for (int index = 1; index < filters[keyNameIndex].Count; index++)
            {
                keyValues.Add(filters[keyNameIndex][index]);
            }
            return keyValues;
        }

        internal void SetKeyName(string keyName)
        {
            List<string> newKey = new List<string>
            {
                keyName
            };
            filters.Add(newKey);
            filters.Sort(MECCG_Deck_Builder.Cards.CompareListsByFirstValue);
        }

        internal void SetCardKeyValue(string keyName, string keyValue, string cardId)
        {
            int cardIndex = cards.FindIndex(card => card["id"] == cardId);
            if (cardIndex >= 0)
            {
                cards[cardIndex].Add(keyName, keyValue);
            }
            else
            {
                SortedDictionary<string, string> card = new SortedDictionary<string, string>
                {
                    { "id", cardId },
                    { keyName, keyValue }
                };
                cards.Add(card);
            }
        }
        
        /// <summary>
        /// Adds new key value and sorts values [1..] in list
        /// </summary>
        /// <param name="key">The key name being updated [0]</param>
        /// <param name="value">The additional key value [1..]</param>
        internal void SetKeyValue(string key, string value)
        {
            // Get index of key values in filters
            int filtersIndex = filters.FindIndex(filter => filter[0] == key);

            // Check whether key value is in key values and add if it is not
            if (filters[filtersIndex].IndexOf(value) == -1)
            {
                filters[filtersIndex].Add(value);
                filters[filtersIndex].Sort(1, filters[filtersIndex].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
            }
        }
    }
}
