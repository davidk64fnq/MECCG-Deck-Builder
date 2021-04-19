
using System;
using System.Collections.Generic;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    class KeyValue
    {
        static private readonly List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        static private readonly List<List<string>> filters = new List<List<string>>();

        /// <summary>
        /// Set key names as index 0 position in filter member key value lists
        /// </summary>
        static internal void SetKeyName(string keyName)
        {
            List<string> newKey = new List<string>
            {
                keyName
            };
            filters.Add(newKey);
            filters.Sort(Cards.CompareListsByFirstValue);
        }

        static internal void SetCardKeyValue(string keyName, string keyValue, string cardId)
        {
            bool cardFound = false;
            for (int cardIndex = 0; cardIndex < cards.Count && !cardFound; cardIndex++)
            {
                if (cards[cardIndex]["id"] == cardId)
                {
                    SortedDictionary<string, string> card = cards[cardIndex];
                    card.Add(keyName, keyValue);
                    cardFound = true;
                }
            }
            if (!cardFound)
            {
                SortedDictionary<string, string> card = new SortedDictionary<string, string>
                {
                    { "id", cardId },
                    { keyName, keyValue }
                };
                cards.Add(card);
            }
        }

        static internal List<string[]> GetCardFilterPairs(string cardId)
        {
            List<string[]> filterPairs = new List<string[]>();

            bool cardFound = false;
            for (int cardIndex = 0; cardIndex < cards.Count && !cardFound; cardIndex++)
            {
                if (cards[cardIndex]["id"] == cardId)
                {
                    cardFound = true;
                    foreach (var pair in cards[cardIndex])
                    {
                        if (pair.Key != "id")
                        {
                            string[] keyNameValue = { pair.Key, pair.Value };
                            filterPairs.Add(keyNameValue);
                        }
                    }
                }
            }

            return filterPairs;
        }

        #region GET_FILTER_INFO

        static internal List<string> GetKeyNameList()
        {
            List<string> keyNames = new List<string>();
            for (int index = 0; index < filters.Count; index++)
            {
                keyNames.Add(filters[index][0]);
            }
            return keyNames;
        }

        static internal List<string> GetCardKeyNameList(string cardId)
        {
            List<string> cardKeyNameList = new List<string>();

            for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
            {
                if (cards[cardIndex]["id"] == cardId)
                {
                    foreach (var pair in cards[cardIndex])
                    {
                        if (pair.Key != "id")
                        {
                            cardKeyNameList.Add(pair.Key);
                        }
                    }
                }
            }

            return cardKeyNameList;
        }

        /// <summary>
        /// Adds new key value and sorts values [1..] in list
        /// </summary>
        /// <param name="key">The key name being updated [0]</param>
        /// <param name="value">The additional key value [1..]</param>
        static internal void SetKeyValue(string key, string value)
        {
            if (value == "")
            {
                return;
            }

            // Get index of key values in filters
            int filtersIndex = filters.FindIndex(keyList => keyList[0] == key);

            // Check whether key value is in key values and add if it is not
            if (filters[filtersIndex].IndexOf(value) == -1)
            {
                filters[filtersIndex].Add(value);
                filters[filtersIndex].Sort(1, filters[filtersIndex].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
            }
        }

        static internal List<string> GetKeyValueList(string keyName)
        {
            List<string> keyValues = new List<string>
            {
                ""
            };
            int keyNameIndex = filters.FindIndex(keyList => keyList[0] == keyName);
            for (int index = 1; index < filters[keyNameIndex].Count; index++)
            {
                keyValues.Add(filters[keyNameIndex][index]);
            }
            return keyValues;
        }

        #endregion
    }
}
