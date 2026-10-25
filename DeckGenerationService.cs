using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public static class DeckGenerationService
    {
#pragma warning disable SYSLIB1045 // Use 'GeneratedRegexAttribute'
        private static readonly Regex PlaceholderRegex = new(
            @"\{([a-zA-Z0-9_]+)\.(?:(?:Category|Tag):([^}]+)|([a-zA-Z0-9_]+))\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
#pragma warning restore SYSLIB1045

        private static readonly HashSet<string> NumericAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prowess", "Body", "Mind", "Direct", "Corruption", "MPs", "Strikes"
        };

        public static DeckGenerationRuleSet LoadRuleSetFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<DeckGenerationRuleSet>(json);
        }

        public static string SaveRuleSetToJson(DeckGenerationRuleSet ruleSet)
        {
            return JsonConvert.SerializeObject(ruleSet, Formatting.Indented);
        }

        public static DeckGenerationResult GenerateDeck(
            IEnumerable<Card> catalogCards,
            ISet<string> defaultSets,
            CardFilterService filterService,
            DeckGenerationRuleSet ruleSet)
        {
            if (ruleSet == null || ruleSet.Rules == null || ruleSet.Rules.Count == 0)
            {
                return new DeckGenerationResult
                {
                    Success = false,
                    ErrorMessage = "The rule set contains no rules."
                };
            }

            var deck = new Deck
            {
                Title = !string.IsNullOrWhiteSpace(ruleSet.DeckTitle) ? ruleSet.DeckTitle : "Generated Deck"
            };

            var cardCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var contextVariables = new Dictionary<string, List<Card>>(StringComparer.OrdinalIgnoreCase);
            var logs = new List<DeckRuleExecutionLog>();
            var allCards = catalogCards?.ToList() ?? [];

            foreach (var rule in ruleSet.Rules)
            {
                ExecuteRule(rule, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables);
            }

            deck.SortSections();

            return new DeckGenerationResult
            {
                Deck = deck,
                Logs = logs,
                Success = true
            };
        }

        private static void ExecuteRule(
            DeckGenerationRule rule,
            Deck deck,
            Dictionary<string, int> cardCountsByName,
            List<DeckRuleExecutionLog> logs,
            IReadOnlyList<Card> allCards,
            ISet<string> defaultSets,
            CardFilterService filterService,
            Dictionary<string, List<Card>> contextVariables = null)
        {
            if (rule == null) return;

            contextVariables ??= new Dictionary<string, List<Card>>(StringComparer.OrdinalIgnoreCase);

            // Handle RandomChoice / ChoiceGroup ("OR" branches)
            if (IsChoiceRule(rule))
            {
                var validChoices = rule.Choices?.Where(c => c != null && c.Rules != null && c.Rules.Count > 0).ToList() ?? [];
                if (validChoices.Count == 0) return;

                int totalWeight = validChoices.Sum(c => Math.Max(1, c.Weight));
                int roll = Random.Shared.Next(totalWeight);
                int cumulative = 0;
                DeckGenerationChoice chosen = validChoices[0];

                foreach (var c in validChoices)
                {
                    cumulative += Math.Max(1, c.Weight);
                    if (roll < cumulative)
                    {
                        chosen = c;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(chosen.DeckTitle))
                {
                    deck.Title = chosen.DeckTitle;
                }

                logs.Add(new DeckRuleExecutionLog
                {
                    RuleName = $"[Branch Selected] {rule.Name} -> '{chosen.Name}'",
                    Section = "-",
                    RequestedCount = 0,
                    AddedCount = 0
                });

                foreach (var subRule in chosen.Rules)
                {
                    ExecuteRule(subRule, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables);
                }
                return;
            }

            // Standard FilterQuery Rule
            if (rule.Count <= 0) return;

            if (!Enum.TryParse<DeckSectionType>(rule.Section, true, out var sectionType))
            {
                sectionType = DeckSectionType.Resources;
            }

            ISet<string> ruleSets = (rule.Sets != null && rule.Sets.Count > 0)
                ? new HashSet<string>(rule.Sets, StringComparer.OrdinalIgnoreCase)
                : defaultSets;

            // 1. Resolve runtime variable placeholders in Attributes & Categories
            var resolvedAttributes = new Dictionary<string, (string RawTemplate, string ResolvedValue)>(StringComparer.OrdinalIgnoreCase);
            if (rule.Attributes != null)
            {
                foreach (var (attrKey, rawVal) in rule.Attributes)
                {
                    if (string.IsNullOrWhiteSpace(attrKey)) continue;
                    string resolved = ResolvePlaceholders(rawVal, contextVariables, filterService);
                    resolvedAttributes[attrKey] = (rawVal, resolved);
                }
            }

            var resolvedCategories = new Dictionary<string, (string RawTemplate, string ResolvedValue)>(StringComparer.OrdinalIgnoreCase);
            if (rule.Categories != null)
            {
                foreach (var (catKey, rawVal) in rule.Categories)
                {
                    if (string.IsNullOrWhiteSpace(catKey)) continue;
                    string resolved = ResolvePlaceholders(rawVal, contextVariables, filterService);
                    resolvedCategories[catKey] = (rawVal, resolved);
                }
            }

            // 2. Filter candidate cards
            var candidates = allCards.Where(c =>
            {
                if (ruleSets != null && ruleSets.Count > 0 && !ruleSets.Contains(c.Set))
                {
                    return false;
                }

                // Attribute evaluation with numeric comparison and blank sentinel support
                foreach (var (attrKey, (rawTemplate, resolvedVal)) in resolvedAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(rawTemplate) && string.IsNullOrWhiteSpace(resolvedVal))
                    {
                        return false;
                    }

                    if (!EvaluateAttributeCriterion(c, attrKey, resolvedVal))
                    {
                        return false;
                    }
                }

                // Category & Tag evaluation with blank sentinel support
                if (filterService != null)
                {
                    foreach (var (catKey, (rawTemplate, resolvedVal)) in resolvedCategories)
                    {
                        if (!string.IsNullOrWhiteSpace(rawTemplate) && string.IsNullOrWhiteSpace(resolvedVal))
                        {
                            return false;
                        }

                        if (!string.IsNullOrWhiteSpace(resolvedVal))
                        {
                            var allowedTags = resolvedVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!allowedTags.Any(t =>
                                t == "*" ||
                                string.Equals(t, "All", StringComparison.OrdinalIgnoreCase) ||
                                filterService.MatchesCustomCriterion(c.Id, catKey, t)))
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }).ToList();

            // 3. Sample candidates according to configurable Distribution strategy
            int addedForThisRule = 0;
            var targetSection = deck.GetSection(sectionType);
            var newlyAdded = new List<Card>();

            string distribution = rule.Distribution?.Trim() ?? "Variety";

            if (string.Equals(distribution, "Focused", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(distribution, "Playset", StringComparison.OrdinalIgnoreCase))
            {
                // FOCUSED: Greedily add maximum legal copies of a sampled title before picking next
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName)).ToList();
                    if (available.Count == 0) break;

                    var titleGroups = available.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    if (titleGroups.Count == 0) break;

                    var chosenGroup = titleGroups[Random.Shared.Next(titleGroups.Count)].ToList();
                    var sampleCard = chosenGroup[0];

                    int currentCount = cardCountsByName.GetValueOrDefault(sampleCard.Name, 0);
                    int maxAllowed = IsUniqueCard(sampleCard) ? 1 : 3;
                    int canAdd = maxAllowed - currentCount;
                    int toAdd = Math.Min(canAdd, rule.Count - addedForThisRule);

                    for (int i = 0; i < toAdd; i++)
                    {
                        var selectedCard = chosenGroup[Random.Shared.Next(chosenGroup.Count)];
                        targetSection.Add(selectedCard);
                        deck.IncludedSets.Add(selectedCard.Set);
                        newlyAdded.Add(selectedCard);

                        currentCount++;
                        cardCountsByName[selectedCard.Name] = currentCount;
                        addedForThisRule++;
                    }
                }
            }
            else if (string.Equals(distribution, "Uniform", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(distribution, "Random", StringComparison.OrdinalIgnoreCase))
            {
                // UNIFORM: Pure lottery draw across all valid candidates with replacement up to limits
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName)).ToList();
                    if (available.Count == 0) break;

                    var titleGroups = available.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    if (titleGroups.Count == 0) break;

                    var chosenGroup = titleGroups[Random.Shared.Next(titleGroups.Count)].ToList();
                    var selectedCard = chosenGroup[Random.Shared.Next(chosenGroup.Count)];

                    targetSection.Add(selectedCard);
                    deck.IncludedSets.Add(selectedCard.Set);
                    newlyAdded.Add(selectedCard);

                    int currentCount = cardCountsByName.GetValueOrDefault(selectedCard.Name, 0) + 1;
                    cardCountsByName[selectedCard.Name] = currentCount;
                    addedForThisRule++;
                }
            }
            else
            {
                // VARIETY (Default): Prioritize distinct titles before assigning duplicates
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName)).ToList();
                    if (available.Count == 0) break;

                    int minCount = available.Min(c => cardCountsByName.GetValueOrDefault(c.Name, 0));

                    var tierGroups = available
                        .Where(c => cardCountsByName.GetValueOrDefault(c.Name, 0) == minCount)
                        .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (tierGroups.Count == 0) break;

                    int groupIndex = Random.Shared.Next(tierGroups.Count);
                    var matchingReprints = tierGroups[groupIndex].ToList();
                    var selectedCard = matchingReprints[Random.Shared.Next(matchingReprints.Count)];

                    targetSection.Add(selectedCard);
                    deck.IncludedSets.Add(selectedCard.Set);
                    newlyAdded.Add(selectedCard);

                    int currentCount = cardCountsByName.GetValueOrDefault(selectedCard.Name, 0) + 1;
                    cardCountsByName[selectedCard.Name] = currentCount;
                    addedForThisRule++;
                }
            }

            logs.Add(new DeckRuleExecutionLog
            {
                RuleName = !string.IsNullOrWhiteSpace(rule.Name) ? rule.Name : "(Unnamed Rule)",
                Section = sectionType.ToString(),
                RequestedCount = rule.Count,
                AddedCount = addedForThisRule
            });

            // 4. Capture selected cards to variable context if SaveAs is specified
            if (!string.IsNullOrWhiteSpace(rule.SaveAs) && newlyAdded.Count > 0)
            {
                contextVariables[rule.SaveAs] = newlyAdded;
            }

            // 5. If shortfall occurred and a Fallback rule is defined, execute it for remaining count
            if (addedForThisRule < rule.Count && rule.Fallback != null)
            {
                int remainingNeeded = rule.Count - addedForThisRule;
                rule.Fallback.Count = remainingNeeded;

                if (string.IsNullOrWhiteSpace(rule.Fallback.Section))
                {
                    rule.Fallback.Section = sectionType.ToString();
                }

                ExecuteRule(rule.Fallback, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables);
            }
        }

        #region ATTRIBUTE_EVALUATION_AND_NUMERIC_OPERATORS

        private static bool EvaluateAttributeCriterion(Card card, string attrKey, string resolvedVal)
        {
            if (string.IsNullOrWhiteSpace(resolvedVal)) return true;

            if (NumericAttributes.Contains(attrKey) && HasComparisonOperator(resolvedVal))
            {
                return EvaluateNumericAttributeComparison(card, attrKey, resolvedVal);
            }

            var allowedValues = resolvedVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return allowedValues.Any(v =>
                v == "*" ||
                string.Equals(v, "All", StringComparison.OrdinalIgnoreCase) ||
                CardFilterService.MatchesCardnumCriterion(card, attrKey, v));
        }

        private static bool HasComparisonOperator(string val) =>
            val.Contains('>') || val.Contains('<') || val.Contains("!=") || val.Contains('=');

        private static bool EvaluateNumericAttributeComparison(Card card, string attrKey, string query)
        {
            string rawCardVal = card.GetAttribute(attrKey);

            if (!TryParseCardStat(rawCardVal, out int cardStat))
            {
                // Fallback to literal matching for non-numeric/empty values
                var fallbackTokens = query.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return fallbackTokens.Any(v =>
                    v == "*" ||
                    string.Equals(v, "All", StringComparison.OrdinalIgnoreCase) ||
                    CardFilterService.MatchesCardnumCriterion(card, attrKey, v));
            }

            var tokens = query.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parsedConditions = new List<(string Op, int TargetVal, bool IsWildcard, bool IsBlank)>();

            foreach (var token in tokens)
            {
                if (token == "*" || string.Equals(token, "All", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (CardFilterService.IsBlankSentinel(token))
                {
                    if (string.IsNullOrWhiteSpace(rawCardVal)) return true;
                    continue;
                }

                string trimmed = token.Trim();
                if (trimmed.StartsWith(">="))
                {
                    if (int.TryParse(trimmed[2..].Trim(), out int t)) parsedConditions.Add((">=", t, false, false));
                }
                else if (trimmed.StartsWith("<="))
                {
                    if (int.TryParse(trimmed[2..].Trim(), out int t)) parsedConditions.Add(("<=", t, false, false));
                }
                else if (trimmed.StartsWith("!="))
                {
                    if (int.TryParse(trimmed[2..].Trim(), out int t)) parsedConditions.Add(("!=", t, false, false));
                }
                else if (trimmed.StartsWith('>'))
                {
                    if (int.TryParse(trimmed[1..].Trim(), out int t)) parsedConditions.Add((">", t, false, false));
                }
                else if (trimmed.StartsWith('<'))
                {
                    if (int.TryParse(trimmed[1..].Trim(), out int t)) parsedConditions.Add(("<", t, false, false));
                }
                else if (trimmed.StartsWith('='))
                {
                    if (int.TryParse(trimmed[1..].Trim(), out int t)) parsedConditions.Add(("==", t, false, false));
                }
                else if (int.TryParse(trimmed, out int exact))
                {
                    parsedConditions.Add(("==", exact, false, false));
                }
            }

            if (parsedConditions.Count == 0) return false;

            // Detect range bounding (e.g. >= 3 and <= 5)
            var lowerBounds = parsedConditions.Where(c => c.Op is ">=" or ">").ToList();
            var upperBounds = parsedConditions.Where(c => c.Op is "<=" or "<").ToList();

            if (lowerBounds.Count == 1 && upperBounds.Count == 1)
            {
                var lb = lowerBounds[0];
                var ub = upperBounds[0];

                if (lb.TargetVal <= ub.TargetVal)
                {
                    // Bound interval AND condition
                    bool satisfiesLower = lb.Op == ">=" ? cardStat >= lb.TargetVal : cardStat > lb.TargetVal;
                    bool satisfiesUpper = ub.Op == "<=" ? cardStat <= ub.TargetVal : cardStat < ub.TargetVal;

                    if (satisfiesLower && satisfiesUpper) return true;

                    // Evaluate any additional discrete conditions
                    var others = parsedConditions.Where(c => c != lb && c != ub).ToList();
                    return others.Any(c => EvaluateSingleCondition(cardStat, c.Op, c.TargetVal));
                }
            }

            // Standard OR evaluation across conditions
            return parsedConditions.Any(c => EvaluateSingleCondition(cardStat, c.Op, c.TargetVal));
        }

        private static bool EvaluateSingleCondition(int stat, string op, int target) => op switch
        {
            ">=" => stat >= target,
            "<=" => stat <= target,
            ">" => stat > target,
            "<" => stat < target,
            "!=" => stat != target,
            "==" or "=" => stat == target,
            _ => false
        };

        private static bool TryParseCardStat(string cardValue, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(cardValue)) return false;

            string trimmed = cardValue.Trim();

            // Strip parentheses (e.g., (-1) -> -1, (2) -> 2)
            if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
            {
                trimmed = trimmed[1..^1].Trim();
            }

            // Strip suffix parentheses (e.g. 1(2) -> 1)
            int parenIdx = trimmed.IndexOf('(');
            if (parenIdx > 0)
            {
                trimmed = trimmed[..parenIdx].Trim();
            }

            // Treat MPs "None" as 0
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            {
                result = 0;
                return true;
            }

            return int.TryParse(trimmed, out result);
        }

        #endregion

        #region PLACEHOLDER_RESOLUTION_AND_HELPERS

        private static string ResolvePlaceholders(
            string template,
            Dictionary<string, List<Card>> contextVariables,
            CardFilterService filterService)
        {
            if (string.IsNullOrWhiteSpace(template) || !template.Contains('{'))
            {
                return template ?? string.Empty;
            }

            return PlaceholderRegex.Replace(template, match =>
            {
                string varName = match.Groups[1].Value;
                if (!contextVariables.TryGetValue(varName, out var cards) || cards.Count == 0)
                {
                    return string.Empty;
                }

                var resolvedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    // Custom taxonomy tag lookup: {Variable.Category:Category Name} or {Variable.Tag:Category Name}
                    string category = match.Groups[2].Value.Trim();
                    if (filterService != null)
                    {
                        foreach (var card in cards)
                        {
                            var tags = filterService.GetCardTagsForCategory(card.Id, category);
                            foreach (var tag in tags)
                            {
                                if (!string.IsNullOrWhiteSpace(tag))
                                {
                                    resolvedValues.Add(tag.Trim());
                                }
                            }
                        }
                    }
                }
                else if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                {
                    // Official rules attribute lookup: {Variable.AttributeName}
                    string attrName = match.Groups[3].Value.Trim();
                    foreach (var card in cards)
                    {
                        string val = card.GetAttribute(attrName);
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            var parts = val.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var part in parts)
                            {
                                resolvedValues.Add(part);
                            }
                        }
                    }
                }

                return resolvedValues.Count > 0 ? string.Join("; ", resolvedValues) : string.Empty;
            }).Trim();
        }

        private static bool IsChoiceRule(DeckGenerationRule rule)
        {
            if (rule == null) return false;
            return string.Equals(rule.RuleType, "RandomChoice", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "Choice", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "OneOf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "Or", StringComparison.OrdinalIgnoreCase) ||
                   (rule.Choices != null && rule.Choices.Count > 0);
        }

        private static bool CanAddCard(Card card, Dictionary<string, int> cardCountsByName)
        {
            int currentCount = cardCountsByName.GetValueOrDefault(card.Name, 0);
            int maxAllowed = IsUniqueCard(card) ? 1 : 3;
            return currentCount < maxAllowed;
        }

        private static bool IsUniqueCard(Card card)
        {
            return string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}