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

        internal static readonly HashSet<string> NumericAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prowess", "Body", "Mind", "Direct", "Corruption", "MPs", "Strikes"
        };

        private static readonly string[] HeroHavens = ["Rivendell", "Lórien", "Grey Havens", "Edhellond"];

        private static readonly Dictionary<string, string> HavenHomeRegions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rivendell"] = "Rhudaur",
            ["Lórien"] = "Lórien",
            ["Lorien"] = "Lórien",
            ["Grey Havens"] = "Lindon",
            ["Edhellond"] = "Belfalas"
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
            Dictionary<string, List<Card>> contextVariables = null,
            bool isFallback = false)
        {
            if (rule == null) return;

            contextVariables ??= new Dictionary<string, List<Card>>(StringComparer.OrdinalIgnoreCase);

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
                    ExecuteRule(subRule, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables, false);
                }
                return;
            }

            if (IsGeographicTheaterRule(rule))
            {
                ExecuteGeographicTheaterRule(rule, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables);
                return;
            }

            if (rule.Count <= 0) return;

            if (!Enum.TryParse<DeckSectionType>(rule.Section, true, out var sectionType))
            {
                sectionType = DeckSectionType.Resources;
            }

            ISet<string> ruleSets = (rule.Sets != null && rule.Sets.Count > 0)
                ? new HashSet<string>(rule.Sets, StringComparer.OrdinalIgnoreCase)
                : defaultSets;

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

            var candidates = allCards.Where(c =>
            {
                if (ruleSets != null && ruleSets.Count > 0 && !ruleSets.Contains(c.Set))
                {
                    return false;
                }

                // Automatic MECCG rules guard: Avatars (Wizards / Ringwraiths) can never be in the starting Pool
                if (sectionType == DeckSectionType.Pool &&
                    (string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(c.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                // In MECCG site decks, duplicate sites or regions are never permitted
                if (sectionType == DeckSectionType.Sites &&
                    deck.Sites.Any(s => string.Equals(s.Name, c.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                foreach (var (attrKey, (rawTemplate, resolvedVal)) in resolvedAttributes)
                {
                    if (!string.IsNullOrWhiteSpace(rawTemplate) && string.IsNullOrWhiteSpace(resolvedVal))
                    {
                        return false;
                    }

                    if (!EvaluateAttributeCriterion(c, attrKey, resolvedVal, filterService))
                    {
                        return false;
                    }
                }

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
                            bool matchesCategory = EvaluateTokensWithExclusion(resolvedVal, token =>
                            {
                                if (CardFilterService.TryParsePredicate(token, out _, out _, out _))
                                {
                                    return CardFilterService.MatchesPredicate(c, token, filterService);
                                }

                                return filterService.MatchesCustomCriterion(c.Id, catKey, token);
                            });

                            if (!matchesCategory)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }).ToList();

            int addedForThisRule = 0;
            var targetSection = deck.GetSection(sectionType);
            var newlyAdded = new List<Card>();

            string distribution = rule.Distribution?.Trim() ?? "Variety";

            if (string.Equals(distribution, "Focused", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(distribution, "Playset", StringComparison.OrdinalIgnoreCase))
            {
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName, deck, sectionType)).ToList();
                    if (available.Count == 0) break;

                    var titleGroups = available.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    if (titleGroups.Count == 0) break;

                    var chosenGroup = titleGroups[Random.Shared.Next(titleGroups.Count)].ToList();
                    var sampleCard = chosenGroup[0];

                    int currentCount = cardCountsByName.GetValueOrDefault(sampleCard.Name, 0);
                    int maxAllowed = GetMaxAllowedForCard(sampleCard, deck, sectionType);
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
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName, deck, sectionType)).ToList();
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
                while (addedForThisRule < rule.Count)
                {
                    var available = candidates.Where(c => CanAddCard(c, cardCountsByName, deck, sectionType)).ToList();
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
                AddedCount = addedForThisRule,
                IsFallback = isFallback,
                AddedCards = [.. newlyAdded.Select(c => $"{c.Name} ({c.Set})")]
            });

            if (!string.IsNullOrWhiteSpace(rule.SaveAs) && newlyAdded.Count > 0)
            {
                contextVariables[rule.SaveAs] = newlyAdded;
            }

            if (addedForThisRule < rule.Count && rule.Fallback != null)
            {
                int remainingNeeded = rule.Count - addedForThisRule;
                rule.Fallback.Count = remainingNeeded;

                // 1. Fallback MUST inherit parent's Section unless explicitly specified in Fallback JSON
                rule.Fallback.Section = rule.Section;

                // 2. If parent was supposed to save a variable (e.g. "Leader"), delegate SaveAs to the Fallback
                if (!string.IsNullOrWhiteSpace(rule.SaveAs) && string.IsNullOrWhiteSpace(rule.Fallback.SaveAs))
                {
                    rule.Fallback.SaveAs = rule.SaveAs;
                }

                // Recurse into fallback with isFallback = true
                ExecuteRule(rule.Fallback, deck, cardCountsByName, logs, allCards, defaultSets, filterService, contextVariables, true);
            }
        }

        #region GEOGRAPHIC_THEATER_ENGINE

        private static bool IsGeographicTheaterRule(DeckGenerationRule rule)
        {
            if (rule == null) return false;
            return string.Equals(rule.RuleType, "GeographicTheater", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "Theater", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "Geographic", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rule.RuleType, "RegionTheater", StringComparison.OrdinalIgnoreCase) ||
                   !string.IsNullOrWhiteSpace(rule.Mode) ||
                   rule.Attributes.ContainsKey("Mode");
        }

        private static void ExecuteGeographicTheaterRule(
            DeckGenerationRule rule,
            Deck deck,
            Dictionary<string, int> cardCountsByName,
            List<DeckRuleExecutionLog> logs,
            IReadOnlyList<Card> allCards,
            ISet<string> defaultSets,
            CardFilterService filterService,
            Dictionary<string, List<Card>> contextVariables)
        {
            ISet<string> ruleSets = (rule.Sets != null && rule.Sets.Count > 0)
                ? new HashSet<string>(rule.Sets, StringComparer.OrdinalIgnoreCase)
                : defaultSets;

            // 1. Determine Movement Mode ("Starter Only" vs. "Starter + Region")
            string rawMode = !string.IsNullOrWhiteSpace(rule.Mode)
                ? rule.Mode
                : (rule.Attributes.TryGetValue("Mode", out string m) ? m : string.Empty);

            bool isStarterPlusRegion = rawMode.Contains("Region", StringComparison.OrdinalIgnoreCase) ||
                                       rawMode.Contains("Roaming", StringComparison.OrdinalIgnoreCase);

            string modeDisplayName = isStarterPlusRegion ? "Starter + Region" : "Starter Only";

            // 2. Determine RegionType and SiteType Constraints
            string regionTypeFilter = !string.IsNullOrWhiteSpace(rule.RegionType)
                ? rule.RegionType
                : (rule.Attributes.TryGetValue("RegionType", out string rtA) ? rtA
                : (rule.Attributes.TryGetValue("Region Type", out string rtA2) ? rtA2
                : (rule.Categories.TryGetValue("Region Type", out string rtC) ? rtC
                : (rule.Categories.TryGetValue("RegionType", out string rtC2) ? rtC2 : string.Empty))));

            string siteTypeFilter = !string.IsNullOrWhiteSpace(rule.SiteType)
                ? rule.SiteType
                : (rule.Attributes.TryGetValue("SiteType", out string stA) ? stA
                : (rule.Attributes.TryGetValue("Site Type", out string stA2) ? stA2
                : (rule.Attributes.TryGetValue("Site", out string stA3) ? stA3
                : (rule.Categories.TryGetValue("SiteType", out string stC) ? stC
                : (rule.Categories.TryGetValue("Site Type", out string stC2) ? stC2 : string.Empty)))));

            // 3. Determine Selected Anchor Haven with Validation & Snapping
            string rawHaven = !string.IsNullOrWhiteSpace(rule.Haven)
                ? rule.Haven
                : (rule.Attributes.TryGetValue("Haven", out string h) ? h : string.Empty);

            // Parameter / Placeholder resolution
            rawHaven = ResolvePlaceholders(rawHaven, contextVariables, filterService);

            string selectedHaven;
            string havenValidationNote = null;

            // Handle "Auto": if Leader is in context, snap to leader's home/haven; otherwise Random
            if (string.Equals(rawHaven, "Auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawHaven, "From Leader", StringComparison.OrdinalIgnoreCase))
            {
                if (contextVariables != null && contextVariables.TryGetValue("Leader", out var leaders) && leaders.Count > 0)
                {
                    var leader = leaders[0];
                    string leaderHome = !string.IsNullOrWhiteSpace(leader.Home) ? leader.Home.Trim() : "";
                    rawHaven = !string.IsNullOrEmpty(leaderHome) ? leaderHome : "Random";
                }
                else
                {
                    rawHaven = "Random";
                }
            }

            if (string.IsNullOrWhiteSpace(rawHaven) || string.Equals(rawHaven, "Random", StringComparison.OrdinalIgnoreCase))
            {
                selectedHaven = HeroHavens[Random.Shared.Next(HeroHavens.Length)];
            }
            else
            {
                var canonicalHero = HeroHavens.FirstOrDefault(hv => CardFilterService.EqualsWithDiacriticFallback(hv, rawHaven));
                if (canonicalHero != null)
                {
                    selectedHaven = canonicalHero;
                }
                else
                {
                    var siteCard = FindCard(allCards, ruleSets, "Site", rawHaven);
                    if (siteCard != null)
                    {
                        if (string.Equals(CardFilterService.NormalizeSiteType(siteCard.Site), "Haven", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedHaven = siteCard.Name;
                        }
                        else
                        {
                            string nearest = !string.IsNullOrWhiteSpace(siteCard.Haven) ? siteCard.Haven.Trim() : "Rivendell";
                            selectedHaven = HeroHavens.FirstOrDefault(hv => CardFilterService.EqualsWithDiacriticFallback(hv, nearest)) ?? nearest;
                            havenValidationNote = $"Leader site '{siteCard.Name}' is not a Haven; snapped to nearest Haven '{selectedHaven}'.";
                        }
                    }
                    else
                    {
                        selectedHaven = "Rivendell";
                        havenValidationNote = $"Unrecognized Haven '{rawHaven}'; falling back to 'Rivendell'.";
                    }
                }
            }

            // 4. Determine Region Limit
            int maxRegions = 4;
            if (rule.RegionCount.HasValue && rule.RegionCount.Value > 0)
            {
                maxRegions = rule.RegionCount.Value;
            }
            else if (rule.Attributes.TryGetValue("RegionCount", out string rcStr) && int.TryParse(rcStr, out int parsedRc) && parsedRc > 0)
            {
                maxRegions = parsedRc;
            }
            else if (isStarterPlusRegion && rule.Count > 0)
            {
                maxRegions = rule.Count;
            }

            maxRegions = Math.Max(1, maxRegions);

            // 5. Determine Transit / Stepping Stone Options
            bool includeOverland = rule.IncludeOverlandPath ?? false;
            if (!includeOverland && rule.Attributes.TryGetValue("IncludeOverlandPath", out string ovStr) && bool.TryParse(ovStr, out bool parsedOverland))
            {
                includeOverland = parsedOverland;
            }

            bool includeIntermediateHavens = rule.IncludeIntermediateHavens ?? true;
            if (rule.Attributes.TryGetValue("IncludeIntermediateHavens", out string ihStr) && bool.TryParse(ihStr, out bool parsedHavens))
            {
                includeIntermediateHavens = parsedHavens;
            }

            // 6. Determine Adventure Site Draft Count
            int siteCount = 0;
            if (rule.SiteCount.HasValue && rule.SiteCount.Value > 0)
            {
                siteCount = rule.SiteCount.Value;
            }
            else if (rule.Attributes.TryGetValue("SiteCount", out string scStr) && int.TryParse(scStr, out int parsedSc) && parsedSc > 0)
            {
                siteCount = parsedSc;
            }

            // 7. Build Middle-earth Region Adjacency Graph from custom tags & attributes
            var graph = filterService.BuildRegionAdjacencyGraph(allCards);

            // 8. Resolve Haven Home Region
            string homeRegion = "Rhudaur";
            if (HavenHomeRegions.TryGetValue(selectedHaven, out string foundHome))
            {
                homeRegion = foundHome;
            }

            var havenCard = FindCard(allCards, ruleSets, "Site", selectedHaven);
            if (havenCard != null && !string.IsNullOrWhiteSpace(havenCard.Region))
            {
                homeRegion = havenCard.Region.Trim();
            }

            // 9. Build Allowed Region Set if RegionType is Constrained
            HashSet<string> allowedRegions = null;
            if (!string.IsNullOrWhiteSpace(regionTypeFilter))
            {
                var matchingRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var regCard in allCards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
                {
                    var rTypes = GetRegionTypesForCard(regCard, allCards, filterService);
                    if (MatchesRegionTypeFilter(rTypes, regionTypeFilter))
                    {
                        matchingRegions.Add(regCard.Name.Trim());
                    }
                }

                if (matchingRegions.Count > 0)
                {
                    allowedRegions = matchingRegions;
                }
                else
                {
                    string warn = $"No regions matched RegionType '{regionTypeFilter}'; filter ignored.";
                    havenValidationNote = (havenValidationNote != null ? havenValidationNote + " | " : "") + warn;
                }
            }

            List<string> activeRegions;
            var theaterAddedCardNames = new List<string>();

            if (isStarterPlusRegion)
            {
                // Starter + Region: Seed from haven home territory (or closest allowed territory) and expand contiguous cluster
                string seedRegion = homeRegion;
                if (allowedRegions != null && !allowedRegions.Contains(homeRegion))
                {
                    var pathToAllowed = FindShortestRegionPath(graph, homeRegion, allowedRegions);
                    if (pathToAllowed.Count > 0)
                    {
                        seedRegion = pathToAllowed[^1];
                    }
                }

                activeRegions = ExpandContiguousRegionCluster(graph, seedRegion, maxRegions, allowedRegions);

                // Add the X <Region> cards to the Sites deck partition
                foreach (var reg in activeRegions)
                {
                    var regionCard = FindCard(allCards, ruleSets, "Region", reg);
                    if (regionCard != null && AddSiteOrRegionCard(deck, regionCard, cardCountsByName))
                    {
                        theaterAddedCardNames.Add($"{regionCard.Name} (Region)");
                    }
                }

                // If overland hiking requested, compute BFS shortest path from Rhudaur to theater
                if (includeOverland && !activeRegions.Contains("Rhudaur", StringComparer.OrdinalIgnoreCase))
                {
                    var shortestPath = FindShortestRegionPath(graph, "Rhudaur", activeRegions);
                    foreach (var transitReg in shortestPath)
                    {
                        if (!activeRegions.Contains(transitReg, StringComparer.OrdinalIgnoreCase))
                        {
                            var transitCard = FindCard(allCards, ruleSets, "Region", transitReg);
                            if (transitCard != null && AddSiteOrRegionCard(deck, transitCard, cardCountsByName))
                            {
                                theaterAddedCardNames.Add($"{transitCard.Name} (Overland Transit Region)");
                            }
                        }
                    }
                }
            }
            else
            {
                // Starter Only: Pure Haven-tethered hub, 0 region cards added to site deck
                var candidateSites = allCards.Where(c =>
                    string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (ruleSets == null || ruleSets.Count == 0 || ruleSets.Contains(c.Set)) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Haven, selectedHaven) &&
                    !string.IsNullOrWhiteSpace(c.Region) &&
                    (string.IsNullOrWhiteSpace(siteTypeFilter) || MatchesSiteTypeFilter(c, siteTypeFilter)) &&
                    (string.IsNullOrWhiteSpace(regionTypeFilter) || MatchesRegionTypeFilter(GetRegionTypesForCard(c, allCards, filterService), regionTypeFilter))).ToList();

                var candidateRegions = candidateSites
                    .Select(c => c.Region.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(homeRegion) && (allowedRegions == null || allowedRegions.Contains(homeRegion)))
                {
                    candidateRegions.Add(homeRegion);
                }

                activeRegions = [];

                if (!string.IsNullOrWhiteSpace(homeRegion) && (allowedRegions == null || allowedRegions.Contains(homeRegion)))
                {
                    activeRegions.Add(homeRegion);
                }

                if (candidateRegions.Count > maxRegions)
                {
                    var cluster = ExpandContiguousRegionCluster(graph, homeRegion, maxRegions, candidateRegions);
                    foreach (var cr in cluster)
                    {
                        if (!activeRegions.Contains(cr, StringComparer.OrdinalIgnoreCase))
                        {
                            activeRegions.Add(cr);
                        }
                    }

                    if (activeRegions.Count < maxRegions)
                    {
                        foreach (var cr in candidateRegions)
                        {
                            if (activeRegions.Count >= maxRegions) break;
                            if (!activeRegions.Contains(cr, StringComparer.OrdinalIgnoreCase))
                            {
                                activeRegions.Add(cr);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var cr in candidateRegions)
                    {
                        if (!activeRegions.Contains(cr, StringComparer.OrdinalIgnoreCase))
                        {
                            activeRegions.Add(cr);
                        }
                    }
                }
            }

            // 10. Starting Haven Resolution & Stepping-Stone Transit
            var rivendellCard = FindCard(allCards, ruleSets, "Site", "Rivendell");
            if (rivendellCard != null && AddSiteOrRegionCard(deck, rivendellCard, cardCountsByName))
            {
                theaterAddedCardNames.Add("Rivendell (Haven - Starting Site)");
            }

            if (!CardFilterService.EqualsWithDiacriticFallback(selectedHaven, "Rivendell"))
            {
                if (includeIntermediateHavens && CardFilterService.EqualsWithDiacriticFallback(selectedHaven, "Edhellond"))
                {
                    var intermediateCard = FindCard(allCards, ruleSets, "Site", "Lórien") ??
                                           FindCard(allCards, ruleSets, "Site", "Grey Havens");
                    if (intermediateCard != null && AddSiteOrRegionCard(deck, intermediateCard, cardCountsByName))
                    {
                        theaterAddedCardNames.Add($"{intermediateCard.Name} (Haven - Stepping Stone)");
                    }
                }

                if (havenCard != null && AddSiteOrRegionCard(deck, havenCard, cardCountsByName))
                {
                    theaterAddedCardNames.Add($"{havenCard.Name} (Haven - Anchor Destination)");
                }
            }

            // 11. Draft Adventure Sites (Filtering by active regions, SiteType, and RegionType)
            var draftedSiteNames = new List<string>();
            int sitesAddedCount = 0;
            if (siteCount > 0)
            {
                var candidateSites = allCards.Where(c =>
                {
                    if (!string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase)) return false;
                    if (string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase)) return false;
                    if (ruleSets != null && ruleSets.Count > 0 && !ruleSets.Contains(c.Set)) return false;
                    if (!activeRegions.Contains(c.Region, StringComparer.OrdinalIgnoreCase)) return false;

                    if (!isStarterPlusRegion && !CardFilterService.EqualsWithDiacriticFallback(c.Haven, selectedHaven))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(siteTypeFilter) && !MatchesSiteTypeFilter(c, siteTypeFilter))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(regionTypeFilter) && !MatchesRegionTypeFilter(GetRegionTypesForCard(c, allCards, filterService), regionTypeFilter))
                    {
                        return false;
                    }

                    return CanAddCard(c, cardCountsByName, deck, DeckSectionType.Sites);
                }).ToList();

                while (sitesAddedCount < siteCount && candidateSites.Count > 0)
                {
                    var chosenSite = candidateSites[Random.Shared.Next(candidateSites.Count)];
                    if (AddSiteOrRegionCard(deck, chosenSite, cardCountsByName))
                    {
                        sitesAddedCount++;
                        draftedSiteNames.Add($"{chosenSite.Name} ({chosenSite.Region})");
                    }
                    candidateSites.Remove(chosenSite);
                }
            }

            // 12. Unified Downstream Context Contract: {Theater.Haven} & {Theater.Region}
            var contextCards = new List<Card>();
            string allRegionsJoined = string.Join("; ", activeRegions);

            foreach (var reg in activeRegions)
            {
                var regCard = FindCard(allCards, ruleSets, "Region", reg) ?? new Card
                {
                    Name = reg,
                    Primary = "Region",
                    Region = reg
                };

                regCard.SetAttribute("Haven", selectedHaven);
                regCard.SetAttribute("Region", reg);
                contextCards.Add(regCard);
            }

            string saveAsKey = !string.IsNullOrWhiteSpace(rule.SaveAs) ? rule.SaveAs.Trim() : "Theater";
            contextVariables[saveAsKey] = contextCards;

            if (!string.Equals(saveAsKey, "Theater", StringComparison.OrdinalIgnoreCase))
            {
                contextVariables["Theater"] = contextCards;
            }

            string logNote = $"Haven='{selectedHaven}' | Home='{homeRegion}'";
            if (!string.IsNullOrWhiteSpace(regionTypeFilter)) logNote += $" | RegionType='{regionTypeFilter}'";
            if (!string.IsNullOrWhiteSpace(siteTypeFilter)) logNote += $" | SiteType='{siteTypeFilter}'";
            logNote += $" | {{Theater.Region}}=[{allRegionsJoined}]";

            if (!string.IsNullOrEmpty(havenValidationNote))
            {
                logNote = $"{havenValidationNote} | {logNote}";
            }

            logs.Add(new DeckRuleExecutionLog
            {
                RuleName = $"[Geographic Theater: {modeDisplayName}]",
                Section = "Sites",
                RequestedCount = isStarterPlusRegion ? maxRegions : 0,
                AddedCount = isStarterPlusRegion ? activeRegions.Count : 0,
                Note = logNote,
                AddedCards = theaterAddedCardNames
            });

            if (siteCount > 0)
            {
                logs.Add(new DeckRuleExecutionLog
                {
                    RuleName = $"[Theater Adventure Sites] {rule.Name}",
                    Section = "Sites",
                    RequestedCount = siteCount,
                    AddedCount = sitesAddedCount,
                    AddedCards = draftedSiteNames
                });
            }
        }

        internal static bool MatchesSiteTypeFilter(Card card, string siteTypeQuery)
        {
            if (card == null || string.IsNullOrWhiteSpace(siteTypeQuery)) return true;
            return EvaluateTokensWithExclusion(siteTypeQuery, target => CardFilterService.MatchesSiteType(card.Site, target));
        }

        internal static bool MatchesRegionTypeFilter(
            IEnumerable<string> cardRegionTypes,
            string regionTypeQuery)
        {
            if (string.IsNullOrWhiteSpace(regionTypeQuery)) return true;
            var types = (cardRegionTypes ?? []).ToList();
            return EvaluateTokensWithExclusion(regionTypeQuery, target => types.Any(t => CardFilterService.EqualsWithDiacriticFallback(t, target)));
        }

        internal static List<string> GetRegionTypesForCard(
            Card card,
            IReadOnlyList<Card> allCards,
            CardFilterService filterService)
        {
            var types = new List<string>();
            if (card == null) return types;

            if (filterService != null)
            {
                var directTags = filterService.GetCardTagsForCategory(card.Id, "Region Type");
                if (directTags != null && directTags.Count > 0)
                {
                    types.AddRange(directTags);
                }
            }

            string attrVal = card.GetAttribute("Region Type");
            if (!string.IsNullOrWhiteSpace(attrVal))
            {
                foreach (var part in attrVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!types.Contains(part, StringComparer.OrdinalIgnoreCase))
                    {
                        types.Add(part);
                    }
                }
            }

            if (types.Count == 0 && string.Equals(card.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(card.Region))
            {
                var regionCard = allCards.FirstOrDefault(c =>
                    string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, card.Region));

                if (regionCard != null)
                {
                    if (filterService != null)
                    {
                        var regTags = filterService.GetCardTagsForCategory(regionCard.Id, "Region Type");
                        if (regTags != null && regTags.Count > 0)
                        {
                            types.AddRange(regTags);
                        }
                    }

                    string regAttr = regionCard.GetAttribute("Region Type");
                    if (!string.IsNullOrWhiteSpace(regAttr))
                    {
                        foreach (var part in regAttr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (!types.Contains(part, StringComparer.OrdinalIgnoreCase))
                            {
                                types.Add(part);
                            }
                        }
                    }
                }
            }

            return types;
        }

        internal static List<string> ExpandContiguousRegionCluster(
    Dictionary<string, HashSet<string>> graph,
    string seedRegion,
    int targetCount,
    HashSet<string> allowedRegions = null)
        {
            var cluster = new List<string>();
            if (string.IsNullOrWhiteSpace(seedRegion)) return cluster;

            cluster.Add(seedRegion);
            int countNeeded = Math.Max(1, targetCount);

            while (cluster.Count < countNeeded)
            {
                var candidateNeighbors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var reg in cluster)
                {
                    if (graph.TryGetValue(reg, out var neighbors))
                    {
                        foreach (var n in neighbors)
                        {
                            if (!cluster.Contains(n, StringComparer.OrdinalIgnoreCase))
                            {
                                if (allowedRegions == null || allowedRegions.Contains(n))
                                {
                                    candidateNeighbors.Add(n);
                                }
                            }
                        }
                    }
                }

                if (candidateNeighbors.Count == 0)
                {
                    break;
                }

                var candidatesList = candidateNeighbors.ToList();
                string chosen = candidatesList[Random.Shared.Next(candidatesList.Count)];
                cluster.Add(chosen);
            }

            return cluster;
        }

        internal static List<string> FindShortestRegionPath(
            Dictionary<string, HashSet<string>> graph,
            string startRegion,
            IEnumerable<string> targetRegions)
        {
            var targetSet = new HashSet<string>(targetRegions ?? [], StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(startRegion) || targetSet.Count == 0)
            {
                return [];
            }

            if (targetSet.Contains(startRegion))
            {
                return [startRegion];
            }

            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            queue.Enqueue(startRegion);
            visited.Add(startRegion);

            string destination = null;

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();

                if (targetSet.Contains(current))
                {
                    destination = current;
                    break;
                }

                if (graph.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (visited.Add(neighbor))
                        {
                            parent[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (destination == null)
            {
                return [];
            }

            var path = new List<string>();
            string curr = destination;
            path.Add(curr);

            while (parent.TryGetValue(curr, out var p))
            {
                path.Add(p);
                curr = p;
                if (string.Equals(curr, startRegion, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            path.Reverse();
            return path;
        }

        private static Card FindCard(
            IReadOnlyList<Card> allCards,
            ISet<string> ruleSets,
            string primary,
            string name)
        {
            if (allCards == null || string.IsNullOrWhiteSpace(name)) return null;

            if (ruleSets != null && ruleSets.Count > 0)
            {
                var match = allCards.FirstOrDefault(c =>
                    ruleSets.Contains(c.Set) &&
                    string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, name));
                if (match != null) return match;
            }

            return allCards.FirstOrDefault(c =>
                string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                CardFilterService.EqualsWithDiacriticFallback(c.Name, name));
        }

        private static bool AddSiteOrRegionCard(
            Deck deck,
            Card card,
            Dictionary<string, int> cardCountsByName)
        {
            if (card == null) return false;

            // In MECCG site decks, sites (including Havens) and region cards are unique by title
            if (deck.Sites.Any(c => string.Equals(c.Name, card.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            deck.Sites.Add(card);
            deck.IncludedSets.Add(card.Set);
            cardCountsByName[card.Name] = cardCountsByName.GetValueOrDefault(card.Name, 0) + 1;
            return true;
        }

        #endregion

        #region ATTRIBUTE_EVALUATION_AND_NUMERIC_OPERATORS

        /// <summary>
        /// Evaluates a semicolon-delimited query string supporting positive inclusions (OR), 
        /// negative exclusions prefixed with '!=' (AND), wildcards ('*', 'All'), and blank sentinels ('[Blank]').
        /// </summary>
        internal static bool EvaluateTokensWithExclusion(
            string query,
            Func<string, bool> matchesSingleTarget)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;

            var tokens = query.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0) return true;

            var positiveTargets = new List<string>();
            var negativeTargets = new List<string>();

            foreach (var token in tokens)
            {
                if (token == "*" || string.Equals(token, "All", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (token.StartsWith("!=") && token.Length > 2)
                {
                    negativeTargets.Add(token[2..].Trim());
                }
                else
                {
                    positiveTargets.Add(token);
                }
            }

            // 1. Negative Exclusions (AND logic): any match immediately rejects the card
            foreach (var neg in negativeTargets)
            {
                if (matchesSingleTarget(neg))
                {
                    return false;
                }
            }

            // 2. Positive Inclusions (OR logic): if specified, at least one must match
            if (positiveTargets.Count > 0)
            {
                return positiveTargets.Any(pos => matchesSingleTarget(pos));
            }

            // 3. If only negative exclusions were specified and none matched, the card passes
            return true;
        }

        private static bool EvaluateAttributeCriterion(Card card, string attrKey, string resolvedVal, CardFilterService filterService)
        {
            if (string.IsNullOrWhiteSpace(resolvedVal))
            {
                if (CardFilterService.TryParsePredicate(attrKey, out _, out _, out _))
                {
                    return CardFilterService.MatchesPredicate(card, attrKey, filterService);
                }
                return true;
            }

            if (CardFilterService.TryParsePredicate(attrKey, out _, out _, out _))
            {
                return CardFilterService.MatchesPredicate(card, attrKey, filterService);
            }

            if (NumericAttributes.Contains(attrKey) && HasComparisonOperator(resolvedVal))
            {
                return EvaluateNumericAttributeComparison(card, attrKey, resolvedVal);
            }

            return EvaluateTokensWithExclusion(resolvedVal, token =>
            {
                if (CardFilterService.TryParsePredicate(token, out _, out _, out _))
                {
                    return CardFilterService.MatchesPredicate(card, token, filterService);
                }

                return CardFilterService.MatchesCardnumCriterion(card, attrKey, token);
            });
        }

        private static bool HasComparisonOperator(string val) =>
            val.Contains('>') || val.Contains('<') || val.Contains("!=") || val.Contains('=');

#pragma warning disable SYSLIB1045 // Use 'GeneratedRegexAttribute'
        /// <summary>
        /// Normalizes natural language numeric expressions (e.g. ">= 3 and <= 5", "3-5", "2 to 4", "1, 2")
        /// into standard semicolon-delimited comparison tokens.
        /// </summary>
        public static string NormalizeNumericQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string text = input.Trim();

            // 1. Replace natural conjunctions ("and", "&&", "&", commas) with semicolons
            text = Regex.Replace(text, @"\s+(?:and|&&|&)\s+", "; ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @",\s*", "; ");

            // 2. Handle ranges like "3-5", "3 - 5", "3 to 5" (ensuring we don't misinterpret negative numbers like "-2")
            var rangeMatch = Regex.Match(text, @"^(\d+)\s*(?:-|–|—|\bto\b)\s*(\d+)$", RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                return $">= {rangeMatch.Groups[1].Value}; <= {rangeMatch.Groups[2].Value}";
            }

            return text;
        }
#pragma warning restore SYSLIB1045

        internal static bool EvaluateNumericAttributeComparison(Card card, string attrKey, string query)
        {
            string rawCardVal = card.GetAttribute(attrKey);
            string normalizedQuery = NormalizeNumericQuery(query);

            if (!TryParseCardStat(rawCardVal, out int cardStat))
            {
                var fallbackTokens = normalizedQuery.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return fallbackTokens.Any(v =>
                    v == "*" ||
                    string.Equals(v, "All", StringComparison.OrdinalIgnoreCase) ||
                    CardFilterService.MatchesCardnumCriterion(card, attrKey, v));
            }

            var tokens = normalizedQuery.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

            var lowerBounds = parsedConditions.Where(c => c.Op is ">=" or ">").ToList();
            var upperBounds = parsedConditions.Where(c => c.Op is "<=" or "<").ToList();

            if (lowerBounds.Count == 1 && upperBounds.Count == 1)
            {
                var lb = lowerBounds[0];
                var ub = upperBounds[0];

                if (lb.TargetVal <= ub.TargetVal)
                {
                    bool satisfiesLower = lb.Op == ">=" ? cardStat >= lb.TargetVal : cardStat > lb.TargetVal;
                    bool satisfiesUpper = ub.Op == "<=" ? cardStat <= ub.TargetVal : cardStat < ub.TargetVal;

                    if (satisfiesLower && satisfiesUpper) return true;

                    var others = parsedConditions.Where(c => c != lb && c != ub).ToList();
                    return others.Any(c => EvaluateSingleCondition(cardStat, c.Op, c.TargetVal));
                }
            }

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

            if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
            {
                trimmed = trimmed[1..^1].Trim();
            }

            int parenIdx = trimmed.IndexOf('(');
            if (parenIdx > 0)
            {
                trimmed = trimmed[..parenIdx].Trim();
            }

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

                    if (resolvedValues.Count == 0)
                    {
                        foreach (var card in cards)
                        {
                            string val = card.GetAttribute(category);
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
                }
                else if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                {
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
                        else if (filterService != null)
                        {
                            var tags = filterService.GetCardTagsForCategory(card.Id, attrName);
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

        private static bool IsAvatar(Card card)
        {
            if (card == null) return false;
            return string.Equals(card.Secondary, "Avatar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetMaxAllowedForCard(Card card, Deck deck, DeckSectionType sectionType)
        {
            if (card == null) return 0;

            // Special MECCG Avatar Allowance (3 of one, or 2 of one + 1 of another; max 3 total in Resources)
            if (IsAvatar(card) && sectionType == DeckSectionType.Resources && deck != null)
            {
                var existingAvatars = deck.Resources.Where(IsAvatar).ToList();
                int totalAvatars = existingAvatars.Count;

                if (totalAvatars >= 3) return 0;

                var distinctAvatars = existingAvatars.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                bool alreadyInDeck = distinctAvatars.Contains(card.Name, StringComparer.OrdinalIgnoreCase);

                // 1. If this is a new Avatar not yet in the deck:
                if (!alreadyInDeck)
                {
                    // Cannot have 3 different Avatars in play deck
                    if (distinctAvatars.Count >= 2) return 0;

                    // First Avatar chosen for the deck: can take all 3 copies!
                    if (distinctAvatars.Count == 0) return 3;

                    // Second distinct Avatar: can take whatever slots remain (e.g. 3 - 1 = 2; or 3 - 2 = 1)
                    return 3 - totalAvatars;
                }

                // 2. If this Avatar is already in the deck:
                // If there is another distinct Avatar already present, neither can exceed 2 (completing 2+1)
                if (distinctAvatars.Count == 2)
                {
                    return 2;
                }

                // Only Avatar in deck so far: can reach up to 3 copies
                return 3;
            }

            return IsUniqueCard(card) ? 1 : 3;
        }

        private static bool CanAddCard(Card card, Dictionary<string, int> cardCountsByName, Deck deck = null, DeckSectionType sectionType = DeckSectionType.Resources)
        {
            if (card == null) return false;

            int currentCount = cardCountsByName.GetValueOrDefault(card.Name, 0);
            int maxAllowed = GetMaxAllowedForCard(card, deck, sectionType);
            return currentCount < maxAllowed;
        }

        private static bool IsUniqueCard(Card card)
        {
            if (card == null) return false;

            // In MECCG, all Site cards and Region cards are strictly unique in deck construction (max 1 copy per site deck)
            if (string.Equals(card.Primary, "Site", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Primary, "Region", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(card.Unique, "Unique", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}