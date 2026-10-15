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
            @"\{([a-zA-Z0-9_]+)\.(?:Tag:([^}]+)|([a-zA-Z0-9_]+))\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
#pragma warning restore SYSLIB1045

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

            // 1. Resolve runtime variable placeholders in Attributes & Tags
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

            var resolvedTags = new Dictionary<string, (string RawTemplate, string ResolvedValue)>(StringComparer.OrdinalIgnoreCase);
            if (rule.Tags != null)
            {
                foreach (var (tagKey, rawVal) in rule.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tagKey)) continue;
                    string resolved = ResolvePlaceholders(rawVal, contextVariables, filterService);
                    resolvedTags[tagKey] = (rawVal, resolved);
                }
            }

            // 2. Filter candidates
            var candidates = allCards.Where(c =>
            {
                if (ruleSets != null && ruleSets.Count > 0 && !ruleSets.Contains(c.Set))
                {
                    return false;
                }

                // Attribute evaluation
                foreach (var (attrKey, (rawTemplate, resolvedVal)) in resolvedAttributes)
                {
                    // If placeholder was specified (e.g. {Leader.Tag:Faction Bonus}) but resolved empty,
                    // the dependency was not met: do not match any card.
                    if (!string.IsNullOrWhiteSpace(rawTemplate) && string.IsNullOrWhiteSpace(resolvedVal))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(resolvedVal))
                    {
                        var allowedValues = resolvedVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!allowedValues.Any(v => CardFilterService.MatchesCardnumCriterion(c, attrKey, v)))
                        {
                            return false;
                        }
                    }
                }

                // Tag evaluation
                if (filterService != null)
                {
                    foreach (var (tagKey, (rawTemplate, resolvedVal)) in resolvedTags)
                    {
                        if (!string.IsNullOrWhiteSpace(rawTemplate) && string.IsNullOrWhiteSpace(resolvedVal))
                        {
                            return false;
                        }

                        if (!string.IsNullOrWhiteSpace(resolvedVal))
                        {
                            var allowedTags = resolvedVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!allowedTags.Any(t => filterService.MatchesCustomCriterion(c.Id, tagKey, t)))
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }).ToList();

            // 3. Sample candidates with tiered variety-first distribution
            int addedForThisRule = 0;
            var targetSection = deck.GetSection(sectionType);
            var newlyAdded = new List<Card>();

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

                int countBefore = targetSection.Count;
                ExecuteRule(rule.Fallback, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables);
                int addedByFallback = targetSection.Count - countBefore;

                // The fallback's own ExecuteRule already logged its results accurately,
                // so return to avoid duplicate/conflicting log entries.
                return;
            }
        }

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
                    // Custom taxonomy tag lookup: {Variable.Tag:Category Name}
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
                    // Official Cardnum attribute lookup: {Variable.AttributeName}
                    string attrName = match.Groups[3].Value.Trim();
                    foreach (var card in cards)
                    {
                        string val = card.GetAttribute(attrName);
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            // Also split multi-values (e.g., compound skills or dual homes)
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
    }
}