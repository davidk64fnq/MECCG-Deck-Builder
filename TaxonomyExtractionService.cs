using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed class TaxonomyRule
    {
        public string Category { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string TagTemplate { get; set; } = string.Empty;
        public string FilterPrimary { get; set; } = string.Empty;
        public string FilterSecondary { get; set; } = string.Empty;
        public string FilterSet { get; set; } = string.Empty;
    }

    public sealed class ExtractionResult
    {
        // cardId -> (Category -> Set of unique tags)
        public Dictionary<string, Dictionary<string, HashSet<string>>> Matches { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddTag(string cardId, string category, string tag)
        {
            if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (!Matches.TryGetValue(cardId, out var catDict))
            {
                catDict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                Matches[cardId] = catDict;
            }

            if (!catDict.TryGetValue(category, out var tagSet))
            {
                tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                catDict[category] = tagSet;
            }

            tagSet.Add(tag.Trim());
        }

        public IReadOnlyList<string> GetAllCategories()
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var catDict in Matches.Values)
            {
                foreach (var cat in catDict.Keys)
                {
                    categories.Add(cat);
                }
            }
            var list = categories.ToList();
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public string GetFormattedTags(string cardId, string category)
        {
            if (Matches.TryGetValue(cardId, out var catDict) &&
                catDict.TryGetValue(category, out var tagSet) &&
                tagSet.Count > 0)
            {
                return string.Join("; ", tagSet.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            }
            return string.Empty;
        }
    }

    public static class TaxonomyExtractionService
    {
        #region DYNAMIC_ENTITY_HARVESTING

        /// <summary>
        /// Queries the catalog cards for any attribute = value pair across the selected sets,
        /// returning a distinct list of harvested card titles.
        /// </summary>
        public static List<string> HarvestEntityNames(
            IEnumerable<Card> catalogCards,
            ISet<string> selectedSets,
            string attributeKey,
            string attributeValue)
        {
            if (catalogCards == null || string.IsNullOrWhiteSpace(attributeKey) || string.IsNullOrWhiteSpace(attributeValue))
            {
                return [];
            }

            return [.. catalogCards
                .Where(c => (selectedSets == null || selectedSets.Count == 0 || selectedSets.Contains(c.Set)) &&
                            string.Equals(c.GetAttribute(attributeKey), attributeValue, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Builds a unified Regex rule matching any of the harvested entity names using capture group $1.
        /// </summary>
        public static TaxonomyRule BuildEntityHarvestRule(IEnumerable<string> entityNames, string categoryName)
        {
            var validNames = entityNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
            if (validNames.Count == 0)
            {
                return null;
            }

            string escapedNames = string.Join("|", validNames.Select(Regex.Escape));
            string pattern = $@"(?i)\b({escapedNames})\b";

            return new TaxonomyRule
            {
                Category = string.IsNullOrWhiteSpace(categoryName) ? "Harvested Tags" : categoryName.Trim(),
                Pattern = pattern,
                TagTemplate = "$1"
            };
        }

        #endregion

        #region EXECUTION

        public static ExtractionResult ExecuteRules(IEnumerable<Card> cards, IEnumerable<TaxonomyRule> rules)
        {
            var result = new ExtractionResult();
            if (cards == null || rules == null) return result;

            var compiledRules = new List<(TaxonomyRule Rule, Regex CompiledRegex)>();
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Pattern) || string.IsNullOrWhiteSpace(rule.Category))
                {
                    continue;
                }

                try
                {
                    var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    compiledRules.Add((rule, regex));
                }
                catch
                {
                    // Skip malformed regex gracefully
                }
            }

            foreach (var card in cards)
            {
                if (string.IsNullOrWhiteSpace(card.Text)) continue;

                // Strip flavor quotes so we only evaluate active rules text
                string rulesText = StripFlavorText(card.Text);

                foreach (var (rule, regex) in compiledRules)
                {
                    // (primary/secondary/set checks...)

                    var matches = regex.Matches(rulesText); // Use rulesText instead of card.Text
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            string tag = !string.IsNullOrWhiteSpace(rule.TagTemplate)
                                ? (rule.TagTemplate.Contains('$') ? match.Result(rule.TagTemplate) : rule.TagTemplate)
                                : match.Value;

                            result.AddTag(card.Id, rule.Category, tag);
                        }
                    }
                }
            }

            return result;
        }

        public static (int CardsTagged, int TagsAssigned) ApplyRulesDirectly(
            IEnumerable<Card> cards,
            IEnumerable<TaxonomyRule> rules,
            CardFilterService filterService)
        {
            var result = ExecuteRules(cards, rules);
            int cardsTagged = result.Matches.Count;
            int tagsAssigned = 0;

            foreach (var (cardId, catDict) in result.Matches)
            {
                foreach (var (category, tagSet) in catDict)
                {
                    filterService.AddCustomKeyName(category);
                    foreach (var tag in tagSet)
                    {
                        filterService.AppendCardCustomTag(cardId, category, tag);
                        tagsAssigned++;
                    }
                }
            }

            return (cardsTagged, tagsAssigned);
        }

        #endregion

        #region RELATIONAL_EXTRACTION

        /// <summary>
        /// Cross-references an attribute on target cards (e.g. Character 'Home') 
        /// against matching catalog entities (e.g. Site 'Name') to extract a secondary 
        /// attribute (e.g. Site 'Region') as a custom tag.
        /// </summary>
        public static ExtractionResult ExecuteRelationalExtraction(
            IEnumerable<Card> targetCards,
            IEnumerable<Card> catalogCards,
            string sourceAttribute,
            string lookupAttribute,
            string lookupValue,
            string targetAttribute,
            string categoryName)
        {
            var result = new ExtractionResult();
            if (targetCards == null || catalogCards == null ||
                string.IsNullOrWhiteSpace(sourceAttribute) || string.IsNullOrWhiteSpace(targetAttribute))
            {
                return result;
            }

            string category = string.IsNullOrWhiteSpace(categoryName) ? "Relational Tags" : categoryName.Trim();

            // 1. Build lookup dictionary from catalog entities (e.g. Primary = Site)
            var lookupCards = catalogCards;
            if (!string.IsNullOrWhiteSpace(lookupAttribute) && !string.IsNullOrWhiteSpace(lookupValue))
            {
                lookupCards = lookupCards.Where(c => string.Equals(c.GetAttribute(lookupAttribute), lookupValue.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Map: Entity Name -> Set of extracted values (e.g. "Rivendell" -> ["Rhudaur"])
            var lookupMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in lookupCards)
            {
                string entityName = card.Name?.Trim();
                string targetVal = card.GetAttribute(targetAttribute)?.Trim();

                if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(targetVal))
                {
                    continue;
                }

                if (!lookupMap.TryGetValue(entityName, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    lookupMap[entityName] = set;
                }
                set.Add(targetVal);
            }

            if (lookupMap.Count == 0)
            {
                return result;
            }

            var knownNames = lookupMap.Keys.OrderByDescending(k => k.Length).ToList();

            // 2. Cross-reference target cards
            foreach (var card in targetCards)
            {
                string sourceVal = card.GetAttribute(sourceAttribute);
                if (string.IsNullOrWhiteSpace(sourceVal))
                {
                    continue;
                }

                bool matched = false;

                // Split on common list delimiters (commas, semicolons, slashes)
                var parts = sourceVal.Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    // Also handle " or " and " and "
                    var subParts = part.Split([" or ", " and "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var sub in subParts)
                    {
                        if (lookupMap.TryGetValue(sub, out var targets))
                        {
                            foreach (var t in targets)
                            {
                                result.AddTag(card.Id, category, t);
                                matched = true;
                            }
                        }
                    }
                }

                // If no exact token match, search known names with regex word boundaries (e.g. "Haven: Rivendell")
                if (!matched)
                {
                    foreach (var name in knownNames)
                    {
                        if (Regex.IsMatch(sourceVal, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase))
                        {
                            if (lookupMap.TryGetValue(name, out var targets))
                            {
                                foreach (var t in targets)
                                {
                                    result.AddTag(card.Id, category, t);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region CSV_EXPORT_AND_IMPORT

        public static void ExportTemplateDraftToCsv(IReadOnlyList<Card> cards, string filePath)
        {
            if (cards == null || cards.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("Card Name,Set,Primary,[Insert Category Name],Card Text");

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                string tagHint = (i == 0) ? "[Insert tag (separate multiple tags with semicolon)]" : string.Empty;

                sb.Append(EscapeCsv(card.Name)).Append(',');
                sb.Append(EscapeCsv(card.Set)).Append(',');
                sb.Append(EscapeCsv(card.Primary)).Append(',');
                sb.Append(EscapeCsv(tagHint)).Append(',');
                sb.AppendLine(EscapeCsv(card.Text));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportDraftToCsv(
            IEnumerable<Card> cards,
            ExtractionResult result,
            string filePath,
            IEnumerable<string> categories = null)
        {
            var categoryList = categories?.ToList() ?? [.. result.GetAllCategories()];
            var sb = new StringBuilder();

            sb.Append("Card Name,Set,Primary");
            foreach (var cat in categoryList)
            {
                sb.Append(',').Append(EscapeCsv(cat));
            }
            sb.AppendLine(",Card Text");

            foreach (var card in cards.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                sb.Append(EscapeCsv(card.Name)).Append(',');
                sb.Append(EscapeCsv(card.Set)).Append(',');
                sb.Append(EscapeCsv(card.Primary));

                foreach (var cat in categoryList)
                {
                    string tags = result.GetFormattedTags(card.Id, cat);
                    sb.Append(',').Append(EscapeCsv(tags));
                }

                sb.Append(',').AppendLine(EscapeCsv(card.Text));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static (int CardsMatched, int CategoriesImported, int TagsAssigned) ImportFromCsv(
            string filePath,
            CardCatalogService catalog,
            CardFilterService filterService)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified CSV file was not found.", filePath);
            }

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            var records = ParseCsv(reader);

            if (records.Count < 2)
            {
                return (0, 0, 0);
            }

            var header = records[0];
            int nameIndex = -1;
            int setIndex = -1;
            var categoryColumnIndices = new List<(int Index, string CategoryName)>();

            for (int i = 0; i < header.Count; i++)
            {
                string col = header[i].Trim();
                if (string.Equals(col, "Card Name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(col, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    nameIndex = i;
                }
                else if (string.Equals(col, "Set", StringComparison.OrdinalIgnoreCase))
                {
                    setIndex = i;
                }
                else if (string.Equals(col, "Primary", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(col, "Card Text", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(col, "Text", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(col, "Id", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip fixed/reference columns
                }
                else if (!string.IsNullOrWhiteSpace(col))
                {
                    if (col.StartsWith("[Insert", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip placeholder template header if left untouched
                    }
                    categoryColumnIndices.Add((i, col));
                }
            }

            if (nameIndex == -1 || setIndex == -1)
            {
                throw new InvalidOperationException("CSV must contain 'Card Name' and 'Set' columns.");
            }

            int cardsMatched = 0;
            int tagsAssigned = 0;
            var categoriesImportedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = 1; r < records.Count; r++)
            {
                var row = records[r];
                if (row.Count <= Math.Max(nameIndex, setIndex)) continue;

                string cardName = row[nameIndex].Trim();
                string setCode = row[setIndex].Trim();

                if (string.IsNullOrEmpty(cardName)) continue;

                var card = catalog.Cards.FirstOrDefault(c =>
                    string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Set, setCode, StringComparison.OrdinalIgnoreCase))
                    ?? catalog.Cards.FirstOrDefault(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase));

                if (card == null) continue;

                cardsMatched++;

                foreach (var (colIdx, catName) in categoryColumnIndices)
                {
                    if (colIdx >= row.Count) continue;

                    string cellValue = row[colIdx].Trim();
                    if (string.IsNullOrWhiteSpace(cellValue)) continue;

                    // Skip instructional hint if left untouched
                    if (cellValue.Contains("[Insert", StringComparison.OrdinalIgnoreCase) ||
                        (cellValue.StartsWith('[') && cellValue.EndsWith(']')))
                    {
                        continue;
                    }

                    filterService.AddCustomKeyName(catName);
                    categoriesImportedSet.Add(catName);

                    var tags = cellValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (tag.StartsWith("[Insert", StringComparison.OrdinalIgnoreCase)) continue;

                        filterService.AppendCardCustomTag(card.Id, catName, tag);
                        tagsAssigned++;
                    }
                }
            }

            return (cardsMatched, categoriesImportedSet.Count, tagsAssigned);
        }

        #endregion

        #region HELPERS_AND_PARSER

        public static string SaveRulesToJson(IEnumerable<TaxonomyRule> rules) =>
            JsonConvert.SerializeObject(rules, Formatting.Indented);

        public static List<TaxonomyRule> LoadRulesFromJson(string json) =>
            JsonConvert.DeserializeObject<List<TaxonomyRule>>(json) ?? [];

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        public static List<List<string>> ParseCsv(TextReader reader)
        {
            var records = new List<List<string>>();
            var currentRecord = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            int nextChar;

            while ((nextChar = reader.Read()) != -1)
            {
                char c = (char)nextChar;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            currentField.Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRecord.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (reader.Peek() == '\n')
                        {
                            reader.Read();
                        }
                        currentRecord.Add(currentField.ToString());
                        currentField.Clear();
                        records.Add(currentRecord);
                        currentRecord = [];
                    }
                    else if (c == '\n')
                    {
                        currentRecord.Add(currentField.ToString());
                        currentField.Clear();
                        records.Add(currentRecord);
                        currentRecord = [];
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (currentField.Length > 0 || currentRecord.Count > 0)
            {
                currentRecord.Add(currentField.ToString());
                records.Add(currentRecord);
            }

            return records;
        }

#pragma warning disable SYSLIB1045 // Use 'GeneratedRegexAttribute'
        private static readonly Regex FlavorQuotesRegex = new(@"[""“\u201C][^""”\u201D]*[""”\u201D]", RegexOptions.Compiled);
        private static readonly Regex FlavorAttributionRegex = new(@"[—–-]\s*(?:LotR|Hobbit|The Hobbit|Silmarillion)[^\r\n]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
#pragma warning restore SYSLIB1045

        /// <summary>
        /// Strips Tolkien flavor quotes ("..." or “...”) and book attributions (—LotR) 
        /// so entity harvesting only matches functional gameplay rules text.
        /// </summary>
        public static string StripFlavorText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Remove everything between quotation marks ("..." or “...”)
            string cleaned = FlavorQuotesRegex.Replace(text, " ");

            // Remove trailing book citations like —LotRIV or —Hobbit
            cleaned = FlavorAttributionRegex.Replace(cleaned, " ");

            return cleaned.Trim();
        }

        #endregion
    }
}