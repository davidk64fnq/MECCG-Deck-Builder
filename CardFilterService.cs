using System;
using System.Collections.Generic;
using System.Linq;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    public sealed class CardFilterService
    {
        // Custom user filters
        private readonly List<List<string>> _customFilters = []; // [0] = keyName, [1..] = values
        private readonly Dictionary<string, Dictionary<string, string>> _cardCustomTags = new(StringComparer.OrdinalIgnoreCase); // cardId -> (tagKey -> tagValue)

        public IReadOnlyList<List<string>> CustomFilters => _customFilters;
        public IReadOnlyDictionary<string, Dictionary<string, string>> CardCustomTags => _cardCustomTags;

        #region CUSTOM_FILTER_MANAGEMENT

        public List<string> GetCustomKeyNames()
        {
            var names = new List<string>(_customFilters.Select(f => f[0]));
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public List<string> GetCustomKeyValues(string keyName)
        {
            var values = new List<string> { "" };
            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                for (int i = 1; i < _customFilters[index].Count; i++)
                {
                    values.Add(_customFilters[index][i]);
                }
            }
            return values;
        }

        public void AddCustomKeyName(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName) || _customFilters.Exists(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _customFilters.Add([keyName]);
            _customFilters.Sort((a, b) => string.Compare(a[0], b[0], StringComparison.Ordinal));
        }

        public void AddCustomKeyValue(string keyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                return;
            }

            if (!_customFilters[index].Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                _customFilters[index].Add(value);
                if (_customFilters[index].Count > 2)
                {
                    _customFilters[index].Sort(1, _customFilters[index].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                }
            }
        }

        public void DeleteCustomKeyName(string keyName)
        {
            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _customFilters.RemoveAt(index);
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                tags.Remove(keyName);
            }
        }

        public void DeleteCustomKeyValue(string keyName, string value)
        {
            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _customFilters[index].RemoveAll(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                if (tags.TryGetValue(keyName, out string existing) && string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Remove(keyName);
                }
            }
        }

        public void SetCardCustomTag(string cardId, string keyName, string value)
        {
            if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(keyName))
            {
                return;
            }

            if (!_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cardCustomTags[cardId] = tags;
            }

            tags[keyName] = value ?? string.Empty;
        }

        public void DeleteCardCustomTag(string cardId, string keyName)
        {
            if (_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                tags.Remove(keyName);
                if (tags.Count == 0)
                {
                    _cardCustomTags.Remove(cardId);
                }
            }
        }

        public List<KeyValuePair<string, string>> GetCardCustomFilterPairs(string cardId)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            if (_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                foreach (var kvp in tags)
                {
                    pairs.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value));
                }
            }
            return pairs;
        }

        public List<string> GetCardCustomKeyNames(string cardId)
        {
            if (_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                return [.. tags.Keys];
            }
            return [];
        }

        public void LoadCustomFilters(List<List<string>> filters, List<SortedDictionary<string, string>> cardMappings)
        {
            _customFilters.Clear();
            _cardCustomTags.Clear();

            if (filters != null)
            {
                foreach (var f in filters)
                {
                    _customFilters.Add([.. f]);
                }
            }

            if (cardMappings != null)
            {
                foreach (var dict in cardMappings)
                {
                    if (dict.TryGetValue("id", out string cardId))
                    {
                        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in dict)
                        {
                            if (!string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase))
                            {
                                tags[kvp.Key] = kvp.Value;
                            }
                        }
                        _cardCustomTags[cardId] = tags;
                    }
                }
            }
        }

        #endregion

        #region FILTER_EVALUATION

        public List<Card> Filter(
            IEnumerable<Card> sourceCards,
            ISet<string> selectedSets,
            IReadOnlyList<KeyValuePair<string, string>> cardnumFilters,
            IReadOnlyList<KeyValuePair<string, string>> customFilters)
        {
            var result = new List<Card>();

            foreach (var card in sourceCards)
            {
                if (selectedSets != null && selectedSets.Count > 0 && !selectedSets.Contains(card.Set))
                {
                    continue;
                }

                if (!MatchesCardnumFilters(card, cardnumFilters))
                {
                    continue;
                }

                if (!MatchesCustomFilters(card.Id, customFilters))
                {
                    continue;
                }

                result.Add(card);
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return result;
        }

        private static bool MatchesCardnumFilters(Card card, IReadOnlyList<KeyValuePair<string, string>> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true;
            }

            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Key) || string.IsNullOrEmpty(filter.Value))
                {
                    continue;
                }

                string cardValue = card.GetAttribute(filter.Key);
                if (string.IsNullOrEmpty(cardValue) || !cardValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private bool MatchesCustomFilters(string cardId, IReadOnlyList<KeyValuePair<string, string>> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true;
            }

            if (!_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                return false;
            }

            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Key) || string.IsNullOrEmpty(filter.Value))
                {
                    continue;
                }

                if (!tags.TryGetValue(filter.Key, out string tagVal) || !tagVal.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}