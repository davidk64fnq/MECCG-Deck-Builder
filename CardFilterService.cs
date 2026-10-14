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
            var names = new List<string> { "" }; // Index 0 is blank
            var sortedNames = _customFilters.Select(f => f[0]).ToList();
            sortedNames.Sort(StringComparer.Ordinal);
            names.AddRange(sortedNames);
            return names;
        }

        public List<string> GetCustomKeyValues(string keyName)
        {
            var values = new List<string> { "" };
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return values;
            }

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
            if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index == -1)
            {
                AddCustomKeyName(keyName);
                index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            }

            if (index != -1 && !_customFilters[index].Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                _customFilters[index].Add(value);
                if (_customFilters[index].Count > 2)
                {
                    _customFilters[index].Sort(1, _customFilters[index].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                }
            }
        }

        public void RenameCustomKeyName(string oldKeyName, string newKeyName)
        {
            if (string.IsNullOrWhiteSpace(oldKeyName) || string.IsNullOrWhiteSpace(newKeyName) ||
                string.Equals(oldKeyName, newKeyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int index = _customFilters.FindIndex(f => string.Equals(f[0], oldKeyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _customFilters[index][0] = newKeyName;
                _customFilters.Sort((a, b) => string.Compare(a[0], b[0], StringComparison.Ordinal));
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                if (tags.TryGetValue(oldKeyName, out string val))
                {
                    tags.Remove(oldKeyName);
                    tags[newKeyName] = val;
                }
            }
        }

        public void RenameCustomKeyValue(string keyName, string oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(oldValue) ||
                string.IsNullOrWhiteSpace(newValue) || string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                for (int i = 1; i < _customFilters[index].Count; i++)
                {
                    if (string.Equals(_customFilters[index][i], oldValue, StringComparison.OrdinalIgnoreCase))
                    {
                        _customFilters[index][i] = newValue;
                        break;
                    }
                }

                if (_customFilters[index].Count > 2)
                {
                    _customFilters[index].Sort(1, _customFilters[index].Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                }
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                if (tags.TryGetValue(keyName, out string existingVal) &&
                    string.Equals(existingVal, oldValue, StringComparison.OrdinalIgnoreCase))
                {
                    tags[keyName] = newValue;
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

            if (!string.IsNullOrWhiteSpace(value))
            {
                AddCustomKeyValue(keyName, value);
            }
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

        public void LoadCustomFilters(
            List<List<string>> filters,
            List<SortedDictionary<string, string>> cardMappings,
            CardCatalogService catalog = null)
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

            if (cardMappings == null) return;

            foreach (var dict in cardMappings)
            {
                string resolvedCardId = null;

                // 1. Try matching by fullCode
                if (catalog != null && dict.TryGetValue("fullCode", out string fullCode) && !string.IsNullOrWhiteSpace(fullCode))
                {
                    var card = catalog.Cards.FirstOrDefault(c => string.Equals(c.FullCode, fullCode.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (card != null) resolvedCardId = card.Id;
                }

                // 2. Try matching by name and set
                if (resolvedCardId == null && catalog != null &&
                    dict.TryGetValue("name", out string name) && !string.IsNullOrWhiteSpace(name) &&
                    dict.TryGetValue("set", out string set) && !string.IsNullOrWhiteSpace(set))
                {
                    var card = catalog.Cards.FirstOrDefault(c =>
                        string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Set, set.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (card != null) resolvedCardId = card.Id;
                }

                // 3. Backwards-compatibility fallback: legacy runtime 'id'
                if (resolvedCardId == null && dict.TryGetValue("id", out string legacyId))
                {
                    resolvedCardId = legacyId;
                }

                if (string.IsNullOrEmpty(resolvedCardId)) continue;

                var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in dict)
                {
                    // Exclude metadata keys so they don't become custom tags
                    if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kvp.Key, "name", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kvp.Key, "set", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kvp.Key, "fullCode", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    tags[kvp.Key] = kvp.Value;
                }

                _cardCustomTags[resolvedCardId] = tags;
            }
        }

        public void AppendCardCustomTag(string cardId, string keyName, string value)
        {
            if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(keyName) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cardCustomTags[cardId] = tags;
            }

            if (tags.TryGetValue(keyName, out string existing) && !string.IsNullOrWhiteSpace(existing))
            {
                var existingList = existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (!existingList.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    existingList.Add(value.Trim());
                    tags[keyName] = string.Join("; ", existingList);
                }
            }
            else
            {
                tags[keyName] = value.Trim();
            }

            AddCustomKeyValue(keyName, value.Trim());
        }

        public List<string> GetCardTagsForCategory(string cardId, string keyName)
        {
            if (_cardCustomTags.TryGetValue(cardId, out var tags) &&
                tags.TryGetValue(keyName, out string val) &&
                !string.IsNullOrWhiteSpace(val))
            {
                return [.. val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            }
            return [];
        }

        public void RemoveCardCustomTag(string cardId, string keyName, string tagValue)
        {
            if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(keyName) || string.IsNullOrWhiteSpace(tagValue))
            {
                return;
            }

            if (_cardCustomTags.TryGetValue(cardId, out var tags) &&
                tags.TryGetValue(keyName, out string existing) &&
                !string.IsNullOrWhiteSpace(existing))
            {
                var list = existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (list.RemoveAll(t => string.Equals(t, tagValue.Trim(), StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    if (list.Count > 0)
                    {
                        tags[keyName] = string.Join("; ", list);
                    }
                    else
                    {
                        tags.Remove(keyName);
                        if (tags.Count == 0)
                        {
                            _cardCustomTags.Remove(cardId);
                        }
                    }
                }
            }
        }

        public void ToggleCardCustomTag(string cardId, string keyName, string tagValue)
        {
            if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(keyName) || string.IsNullOrWhiteSpace(tagValue))
            {
                return;
            }

            var currentTags = GetCardTagsForCategory(cardId, keyName);
            if (currentTags.Contains(tagValue.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                RemoveCardCustomTag(cardId, keyName, tagValue);
            }
            else
            {
                AppendCardCustomTag(cardId, keyName, tagValue);
            }
        }

        #endregion

        #region FILTER_AND_FACET_EVALUATION

        public static bool MatchesCardnumCriterion(Card card, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string cardValue = card.GetAttribute(key);
            if (string.IsNullOrEmpty(cardValue))
            {
                return false;
            }

            if (string.Equals(key, "Skill", StringComparison.OrdinalIgnoreCase))
            {
                string[] skills = cardValue.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in skills)
                {
                    if (string.Equals(s, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }

            return string.Equals(cardValue.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool MatchesCustomCriterion(string cardId, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                return false;
            }

            if (!tags.TryGetValue(key, out string tagVal) || string.IsNullOrEmpty(tagVal))
            {
                return false;
            }

            if (string.Equals(tagVal.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (tagVal.Contains(';'))
            {
                var split = tagVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return split.Any(s => string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        public bool MatchesCriteria(Card card, IReadOnlyList<FilterCriterion> criteria)
        {
            if (criteria == null || criteria.Count == 0)
            {
                return true;
            }

            foreach (var c in criteria)
            {
                if (!c.IsActive)
                {
                    continue;
                }

                if (c.Type == FilterType.Cardnum)
                {
                    if (!MatchesCardnumCriterion(card, c.Key, c.Value))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!MatchesCustomCriterion(card.Id, c.Key, c.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public List<Card> Filter(
            IEnumerable<Card> sourceCards,
            ISet<string> selectedSets,
            IReadOnlyList<FilterCriterion> criteria)
        {
            var result = new List<Card>();

            foreach (var card in sourceCards)
            {
                if (selectedSets != null && !selectedSets.Contains(card.Set))
                {
                    continue;
                }

                if (!MatchesCriteria(card, criteria))
                {
                    continue;
                }

                result.Add(card);
            }

            result.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.WithNaturalSort().Compare(a.Name, b.Name));
            return result;
        }

        public List<string> GetAvailableValues(
    IEnumerable<Card> sourceCards,
    ISet<string> selectedSets,
    IReadOnlyList<FilterCriterion> otherCriteria,
    FilterType targetType,
    string targetKey)
        {
            var result = new List<string> { "" };

            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return result;
            }

            var distinctValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Scan only cards matching active sets and criteria
            foreach (var card in sourceCards)
            {
                if (selectedSets != null && !selectedSets.Contains(card.Set))
                {
                    continue;
                }

                if (!MatchesCriteria(card, otherCriteria))
                {
                    continue;
                }

                if (targetType == FilterType.Cardnum)
                {
                    string attr = card.GetAttribute(targetKey);
                    if (string.IsNullOrWhiteSpace(attr))
                    {
                        continue;
                    }

                    if (string.Equals(targetKey, "Skill", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] words = attr.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
                        foreach (string word in words)
                        {
                            distinctValues.Add(word.Trim());
                        }
                    }
                    else
                    {
                        distinctValues.Add(attr.Trim());
                    }
                }
                else
                {
                    if (_cardCustomTags.TryGetValue(card.Id, out var tags) &&
                        tags.TryGetValue(targetKey, out string tagVal) &&
                        !string.IsNullOrWhiteSpace(tagVal))
                    {
                        if (tagVal.Contains(';'))
                        {
                            foreach (var part in tagVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                distinctValues.Add(part);
                            }
                        }
                        else
                        {
                            distinctValues.Add(tagVal.Trim());
                        }
                    }
                }
            }

            var sortedList = distinctValues.ToList();
            sortedList.Sort(StringComparison.OrdinalIgnoreCase.WithNaturalSort());
            result.AddRange(sortedList);
            return result;
        }

        #endregion
    }
}