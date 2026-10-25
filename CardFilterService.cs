using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    public sealed class CardFilterService
    {
        public const string BlankSentinel = "[Blank]";
        public const string NoneSentinel = "[None]";

        public static bool IsBlankSentinel(string value) =>
            string.Equals(value, BlankSentinel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, NoneSentinel, StringComparison.OrdinalIgnoreCase);

        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static bool EqualsWithDiacriticFallback(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(RemoveDiacritics(a), RemoveDiacritics(b), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeSiteType(string raw, bool allowSingleLetterShorthand = false)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string trimmed = raw.Trim().ToLowerInvariant();

            // Only allow single-letter codes when parsing legacy card database attributes
            if (allowSingleLetterShorthand)
            {
                switch (trimmed)
                {
                    case "h": return "Haven";
                    case "f": return "Free-hold";
                    case "b": return "Border-hold";
                    case "r": return "Ruins & Lairs";
                    case "s": return "Shadow-hold";
                    case "d": return "Dark-hold";
                    case "w": return "Wizardhaven";
                }
            }

            return trimmed switch
            {
                "haven" => "Haven",
                "free-hold" or "freehold" or "free hold" => "Free-hold",
                "border-hold" or "borderhold" or "border hold" => "Border-hold",
                "ruins & lairs" or "ruins and lairs" or "ruins & lair" or "ruins and lair" => "Ruins & Lairs",
                "shadow-hold" or "shadowhold" or "shadow hold" => "Shadow-hold",
                "dark-hold" or "darkhold" or "dark hold" => "Dark-hold",
                "wizardhaven" or "wizard-haven" => "Wizardhaven",
                _ => raw.Trim()
            };
        }

        public static List<string> SplitSiteTypes(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            var parts = text.Split([';', '/', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length > 1 && tokens.All(t => t.Length == 1))
                {
                    foreach (var tok in tokens)
                    {
                        list.Add(tok);
                    }
                }
                else
                {
                    list.Add(part);
                }
            }
            return list;
        }

        public static bool MatchesSiteType(string cardSite, string targetSite)
        {
            if (string.IsNullOrWhiteSpace(cardSite) || string.IsNullOrWhiteSpace(targetSite)) return false;

            // Card database entries may contain legacy shorthand (e.g. "B" or "B F")
            var cardTypes = SplitSiteTypes(cardSite)
                .Select(t => NormalizeSiteType(t, allowSingleLetterShorthand: true))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // User queries in deck rules and tags require full canonical names
            var targetTypes = SplitSiteTypes(targetSite)
                .Select(t => NormalizeSiteType(t, allowSingleLetterShorthand: false))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return targetTypes.Overlaps(cardTypes);
        }

        public static bool TryParsePredicate(string input, out string attrName, out string op, out string attrVal)
        {
            attrName = null;
            op = null;
            attrVal = null;

            if (string.IsNullOrWhiteSpace(input)) return false;

            string trimmed = input.Trim();

            string[] ops = [">=", "<=", "!=", "==", "=", ">", "<"];
            foreach (var candidateOp in ops)
            {
                int idx = trimmed.IndexOf(candidateOp, StringComparison.Ordinal);
                if (idx > 0)
                {
                    string left = trimmed[..idx].Trim();
                    string right = trimmed[(idx + candidateOp.Length)..].Trim();

                    if (!string.IsNullOrEmpty(left) && left.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' '))
                    {
                        attrName = left;
                        op = candidateOp;
                        attrVal = right;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool MatchesPredicate(Card card, string predicate, CardFilterService filterService)
        {
            if (card == null || string.IsNullOrWhiteSpace(predicate)) return false;

            // Support compound AND predicates (e.g., "Region=Under-deeps && Site!=Dark-hold")
            if (predicate.Contains("&&"))
            {
                var subPredicates = predicate.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return subPredicates.All(sub => MatchesPredicate(card, sub, filterService));
            }

            if (!TryParsePredicate(predicate, out string attrName, out string op, out string attrVal))
            {
                return false;
            }

            // --- UPDATED BLANK CHECK ---
            if (IsBlankSentinel(attrVal) || string.IsNullOrWhiteSpace(attrVal))
            {
                string curVal = card.GetAttribute(attrName);
                bool isBlank = string.IsNullOrWhiteSpace(curVal);

                // If it's blank in official attributes, check custom tags!
                if (isBlank && filterService != null)
                {
                    var customTags = filterService.GetCardTagsForCategory(card.Id, attrName);
                    isBlank = customTags.Count == 0;
                }

                return op == "!=" ? !isBlank : isBlank;
            }

            if (attrVal == "*" || string.Equals(attrVal, "All", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (DeckGenerationService.NumericAttributes.Contains(attrName) ||
                (int.TryParse(attrVal, out _) && (op is ">=" or "<=" or ">" or "<" or "!=" or "==" or "=")))
            {
                string numericQuery = (op is "==" or "=") ? attrVal : op + " " + attrVal;
                return DeckGenerationService.EvaluateNumericAttributeComparison(card, attrName, numericQuery);
            }

            if (string.Equals(attrName, "Site", StringComparison.OrdinalIgnoreCase))
            {
                bool siteMatch = MatchesSiteType(card.Site, attrVal);
                return op == "!=" ? !siteMatch : siteMatch;
            }

            string cardVal = card.GetAttribute(attrName);
            bool matched = false;

            if (!string.IsNullOrEmpty(cardVal))
            {
                matched = MatchesCardnumCriterion(card, attrName, attrVal);
            }
            else if (filterService != null)
            {
                matched = filterService.MatchesCustomCriterion(card.Id, attrName, attrVal);
            }

            return op == "!=" ? !matched : matched;
        }

        // Custom user filters
        private readonly List<List<string>> _customFilters = [];
        private readonly Dictionary<string, Dictionary<string, string>> _cardCustomTags = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<List<string>> CustomFilters => _customFilters;
        public IReadOnlyDictionary<string, Dictionary<string, string>> CardCustomTags => _cardCustomTags;

        #region CUSTOM_FILTER_MANAGEMENT

        public List<string> GetCustomKeyNames()
        {
            var names = new List<string> { "" };
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

            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                for (int i = 1; i < _customFilters[index].Count; i++)
                {
                    string val = _customFilters[index][i];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        if (val.Contains(';'))
                        {
                            foreach (var part in val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                distinct.Add(part);
                            }
                        }
                        else
                        {
                            distinct.Add(val.Trim());
                        }
                    }
                }
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                if (tags.TryGetValue(keyName, out string tagVal) && !string.IsNullOrWhiteSpace(tagVal))
                {
                    if (tagVal.Contains(';'))
                    {
                        foreach (var part in tagVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            distinct.Add(part);
                        }
                    }
                    else
                    {
                        distinct.Add(tagVal.Trim());
                    }
                }
            }

            var sorted = distinct.ToList();
            sorted.Sort(StringComparison.OrdinalIgnoreCase.WithNaturalSort());
            values.AddRange(sorted);
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

            if (index != -1)
            {
                // Ensure incoming strings are split so dropdowns don't get populated with giant literal strings
                var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool added = false;

                foreach (var part in parts)
                {
                    if (!_customFilters[index].Contains(part, StringComparer.OrdinalIgnoreCase))
                    {
                        _customFilters[index].Add(part);
                        added = true;
                    }
                }

                if (added && _customFilters[index].Count > 2)
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
                if (tags.TryGetValue(keyName, out string existingVal) && !string.IsNullOrWhiteSpace(existingVal))
                {
                    if (existingVal.Contains(';'))
                    {
                        var parts = existingVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        bool modified = false;
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (string.Equals(parts[i], oldValue, StringComparison.OrdinalIgnoreCase))
                            {
                                parts[i] = newValue;
                                modified = true;
                            }
                        }
                        if (modified)
                        {
                            tags[keyName] = string.Join("; ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
                        }
                    }
                    else if (string.Equals(existingVal.Trim(), oldValue, StringComparison.OrdinalIgnoreCase))
                    {
                        tags[keyName] = newValue;
                    }
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
            if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int index = _customFilters.FindIndex(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _customFilters[index].RemoveAll(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var tags in _cardCustomTags.Values)
            {
                if (tags.TryGetValue(keyName, out string existing) && !string.IsNullOrWhiteSpace(existing))
                {
                    if (existing.Contains(';'))
                    {
                        var parts = existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        if (parts.RemoveAll(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) > 0)
                        {
                            if (parts.Count > 0)
                            {
                                tags[keyName] = string.Join("; ", parts);
                            }
                            else
                            {
                                tags.Remove(keyName);
                            }
                        }
                    }
                    else if (string.Equals(existing.Trim(), value, StringComparison.OrdinalIgnoreCase))
                    {
                        tags.Remove(keyName);
                    }
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

            if (!string.IsNullOrWhiteSpace(value) && value.Contains(';'))
            {
                // Split, deduplicate, and natural sort
                var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();
                parts.Sort(StringComparison.CurrentCultureIgnoreCase.WithNaturalSort());
                tags[keyName] = string.Join("; ", parts);
            }
            else
            {
                tags[keyName] = value ?? string.Empty;
            }

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
                var sortedCategories = tags.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort());
                foreach (var cat in sortedCategories)
                {
                    string val = tags[cat];

                    if (!string.IsNullOrWhiteSpace(val) && val.Contains(';'))
                    {
                        var parts = val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        parts.Sort(StringComparison.CurrentCultureIgnoreCase.WithNaturalSort());
                        val = string.Join("; ", parts);
                    }

                    pairs.Add(new KeyValuePair<string, string>(cat, val));
                }
            }
            return pairs;
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

                if (catalog != null && dict.TryGetValue("fullCode", out string fullCode) && !string.IsNullOrWhiteSpace(fullCode))
                {
                    var card = catalog.Cards.FirstOrDefault(c => string.Equals(c.FullCode, fullCode.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (card != null) resolvedCardId = card.Id;
                }

                if (resolvedCardId == null && catalog != null &&
                    dict.TryGetValue("name", out string name) && !string.IsNullOrWhiteSpace(name) &&
                    dict.TryGetValue("set", out string set) && !string.IsNullOrWhiteSpace(set))
                {
                    var card = catalog.Cards.FirstOrDefault(c =>
                        string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Set, set.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (card != null) resolvedCardId = card.Id;
                }

                if (resolvedCardId == null && dict.TryGetValue("id", out string legacyId))
                {
                    resolvedCardId = legacyId;
                }

                if (string.IsNullOrEmpty(resolvedCardId)) continue;

                var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in dict)
                {
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

            string resolvedKey = _customFilters.FirstOrDefault(f => string.Equals(f[0], keyName, StringComparison.OrdinalIgnoreCase))?[0] ?? keyName.Trim();

            if (!_cardCustomTags.TryGetValue(cardId, out var tags))
            {
                tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cardCustomTags[cardId] = tags;
            }

            var incomingTags = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var existingList = new List<string>();

            if (tags.TryGetValue(resolvedKey, out string existing) && !string.IsNullOrWhiteSpace(existing))
            {
                existingList = [.. existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            }

            foreach (var incomingTag in incomingTags)
            {
                var knownValues = GetCustomKeyValues(resolvedKey);
                string canonicalVal = knownValues.FirstOrDefault(v => string.Equals(v, incomingTag, StringComparison.OrdinalIgnoreCase));
                string resolvedValue = !string.IsNullOrEmpty(canonicalVal) ? canonicalVal : incomingTag;

                if (!existingList.Contains(resolvedValue, StringComparer.OrdinalIgnoreCase))
                {
                    existingList.Add(resolvedValue);
                    AddCustomKeyValue(resolvedKey, resolvedValue);
                }
            }

            existingList.Sort(StringComparison.CurrentCultureIgnoreCase.WithNaturalSort());
            tags[resolvedKey] = string.Join("; ", existingList);
        }

        public List<string> GetCardTagsForCategory(string cardId, string keyName)
        {
            if (_cardCustomTags.TryGetValue(cardId, out var tags) &&
                tags.TryGetValue(keyName, out string val) &&
                !string.IsNullOrWhiteSpace(val))
            {
                var list = val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                list.Sort(StringComparison.CurrentCultureIgnoreCase.WithNaturalSort());
                return list;
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

        public void MergeCustomFilters(
            List<List<string>> filters,
            List<SortedDictionary<string, string>> cardMappings,
            CardCatalogService catalog = null)
        {
            if (filters != null)
            {
                foreach (var f in filters)
                {
                    if (f == null || f.Count == 0) continue;
                    string keyName = f[0];
                    AddCustomKeyName(keyName);
                    for (int i = 1; i < f.Count; i++)
                    {
                        AddCustomKeyValue(keyName, f[i]);
                    }
                }
            }

            if (cardMappings == null) return;

            foreach (var dict in cardMappings)
            {
                string resolvedCardId = ResolveCardIdFromDictionary(dict, catalog);
                if (string.IsNullOrEmpty(resolvedCardId)) continue;

                foreach (var kvp in dict)
                {
                    if (IsMetadataKey(kvp.Key)) continue;

                    AppendCardCustomTag(resolvedCardId, kvp.Key, kvp.Value);
                }
            }
        }

        private static string ResolveCardIdFromDictionary(SortedDictionary<string, string> dict, CardCatalogService catalog)
        {
            if (catalog != null && dict.TryGetValue("fullCode", out string fullCode) && !string.IsNullOrWhiteSpace(fullCode))
            {
                var card = catalog.Cards.FirstOrDefault(c => string.Equals(c.FullCode, fullCode.Trim(), StringComparison.OrdinalIgnoreCase));
                if (card != null) return card.Id;
            }

            if (catalog != null &&
                dict.TryGetValue("name", out string name) && !string.IsNullOrWhiteSpace(name) &&
                dict.TryGetValue("set", out string set) && !string.IsNullOrWhiteSpace(set))
            {
                var card = catalog.Cards.FirstOrDefault(c =>
                    string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Set, set.Trim(), StringComparison.OrdinalIgnoreCase));
                if (card != null) return card.Id;
            }

            if (dict.TryGetValue("id", out string legacyId))
            {
                return legacyId;
            }

            return null;
        }

        private static bool IsMetadataKey(string key) =>
            string.Equals(key, "id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "set", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "fullCode", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region FILTER_AND_FACET_EVALUATION

        public static bool MatchesCardnumCriterion(Card card, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string cardValue = card.GetAttribute(key);

            if (IsBlankSentinel(value))
            {
                return string.IsNullOrWhiteSpace(cardValue);
            }

            if (string.IsNullOrEmpty(cardValue))
            {
                return false;
            }

            if (string.Equals(key, "Skill", StringComparison.OrdinalIgnoreCase))
            {
                string[] skills = cardValue.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in skills)
                {
                    if (EqualsWithDiacriticFallback(s, value))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (string.Equals(key, "Site", StringComparison.OrdinalIgnoreCase))
            {
                return MatchesSiteType(cardValue, value);
            }

            if (cardValue.Contains(';') ||
                (string.Equals(key, "Secondary", StringComparison.OrdinalIgnoreCase) && cardValue.Contains('/')) ||
                (string.Equals(key, "Region", StringComparison.OrdinalIgnoreCase) && cardValue.Contains('/')))
            {
                var parts = cardValue.Split([';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Any(p => EqualsWithDiacriticFallback(p, value.Trim()));
            }

            return EqualsWithDiacriticFallback(cardValue.Trim(), value.Trim());
        }

        public bool MatchesCustomCriterion(string cardId, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (IsBlankSentinel(value))
            {
                if (!_cardCustomTags.TryGetValue(cardId, out var tags))
                {
                    return true;
                }
                return !tags.TryGetValue(key, out string tagVal) || string.IsNullOrWhiteSpace(tagVal);
            }

            if (!_cardCustomTags.TryGetValue(cardId, out var customTags))
            {
                return false;
            }

            if (!customTags.TryGetValue(key, out string val) || string.IsNullOrEmpty(val))
            {
                return false;
            }

            if (EqualsWithDiacriticFallback(val.Trim(), value.Trim()))
            {
                return true;
            }

            if (val.Contains(';'))
            {
                var split = val.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return split.Any(s => EqualsWithDiacriticFallback(s, value.Trim()));
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
            bool hasBlank = false;

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
                        hasBlank = true;
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
                    else if (attr.Contains(';'))
                    {
                        var parts = attr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var part in parts)
                        {
                            distinctValues.Add(part.Trim());
                        }
                    }
                    else if (string.Equals(targetKey, "Site", StringComparison.OrdinalIgnoreCase) && attr.Contains('/'))
                    {
                        var parts = attr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var part in parts)
                        {
                            distinctValues.Add(part.Trim());
                        }
                    }
                    else
                    {
                        distinctValues.Add(attr.Trim());
                    }
                }
                else
                {
                    if (!_cardCustomTags.TryGetValue(card.Id, out var tags) ||
                        !tags.TryGetValue(targetKey, out string tagVal) ||
                        string.IsNullOrWhiteSpace(tagVal))
                    {
                        hasBlank = true;
                        continue;
                    }

                    if (tagVal.Contains(';'))
                    {
                        foreach (var part in tagVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            distinctValues.Add(part.Trim());
                        }
                    }
                    else
                    {
                        distinctValues.Add(tagVal.Trim());
                    }
                }
            }

            if (hasBlank)
            {
                result.Add(BlankSentinel);
            }

            var sortedList = distinctValues.ToList();
            sortedList.Sort(StringComparison.OrdinalIgnoreCase.WithNaturalSort());
            result.AddRange(sortedList);
            return result;
        }

        #endregion
    }
}