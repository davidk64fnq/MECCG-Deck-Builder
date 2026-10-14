using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public static class DeckGenerationService
    {
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
            var logs = new List<DeckRuleExecutionLog>();
            var allCards = catalogCards?.ToList() ?? [];

            foreach (var rule in ruleSet.Rules)
            {
                ExecuteRule(rule, deck, cardCountsByName, logs, allCards, defaultSets, filterService);
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
            CardFilterService filterService)
        {
            if (rule == null) return;

            // Handle RandomChoice / ChoiceGroup ("OR" branches)
            if (IsChoiceRule(rule))
            {
                var validChoices = rule.Choices?.Where(c => c != null && c.Rules != null && c.Rules.Count > 0).ToList() ?? [];
                if (validChoices.Count == 0) return;

                // Weighted random selection (default weight is 1)
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

                // If choice provides a specific deck title, apply it
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

                // Execute the chosen branch's sub-rules
                foreach (var subRule in chosen.Rules)
                {
                    ExecuteRule(subRule, deck, cardCountsByName, logs, allCards, defaultSets, filterService);
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

            // 1. Filter candidates
            var candidates = allCards.Where(c =>
            {
                if (ruleSets != null && ruleSets.Count > 0 && !ruleSets.Contains(c.Set))
                {
                    return false;
                }

                if (rule.Attributes != null)
                {
                    foreach (var (attrKey, attrVal) in rule.Attributes)
                    {
                        if (!string.IsNullOrWhiteSpace(attrKey) && !string.IsNullOrWhiteSpace(attrVal))
                        {
                            var allowedValues = attrVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!allowedValues.Any(v => CardFilterService.MatchesCardnumCriterion(c, attrKey, v)))
                            {
                                return false;
                            }
                        }
                    }
                }

                if (rule.Tags != null && filterService != null)
                {
                    foreach (var (tagKey, tagVal) in rule.Tags)
                    {
                        if (!string.IsNullOrWhiteSpace(tagKey) && !string.IsNullOrWhiteSpace(tagVal))
                        {
                            var allowedTags = tagVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!allowedTags.Any(t => filterService.MatchesCustomCriterion(c.Id, tagKey, t)))
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }).ToList();

            // 2. Sample candidates with tiered variety-first duplication
            int addedForThisRule = 0;
            var targetSection = deck.GetSection(sectionType);

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

                int currentCount = cardCountsByName.GetValueOrDefault(selectedCard.Name, 0) + 1;
                cardCountsByName[selectedCard.Name] = currentCount;
                addedForThisRule++;
            }

            logs.Add(new DeckRuleExecutionLog
            {
                RuleName = !string.IsNullOrWhiteSpace(rule.Name) ? rule.Name : $"Rule {logs.Count + 1}",
                Section = sectionType.ToString(),
                RequestedCount = rule.Count,
                AddedCount = addedForThisRule
            });
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