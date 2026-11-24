using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NaturalSort.Extension;

namespace MECCG_Deck_Builder
{
    public sealed partial class DeckAssemblyMatrixDialog
    {
        private static readonly HashSet<string> CanonicalHoldTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Haven", "Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Wizardhaven"
        };

        private static readonly HashSet<string> HeroTargetSpecies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dúnadan", "Dwarf", "Elf", "Hobbit", "Man", "Wizard"
        };

        private sealed class DeckCycleSiteTracker
        {
            private sealed record OverlapRecord(
                Card Tapper,
                List<int> TapIndices,
                List<Card> BottleneckCards,
                List<int> BottleneckSiteIndices,
                bool UsedExactBottleneck);

            private readonly DeckAssemblyMatrixDialog _owner;
            private readonly int _capacityPerSite;
            private readonly List<Card> _allSitesInDeck;
            private readonly List<Card> _nonHavenSites;
            private readonly List<Card> _occupyingCards = [];
            private readonly List<Card> _matchedCards = [];
            private readonly List<OverlapRecord> _overlaps = [];
            private int _matchedCount;

            public DeckCycleSiteTracker(
                DeckAssemblyMatrixDialog owner,
                int cyclesBeforeCouncil,
                IEnumerable<Card> existingResourceCards,
                List<Card> allSitesInDeck)
            {
                _owner = owner;
                _capacityPerSite = Math.Max(1, cyclesBeforeCouncil);
                _allSitesInDeck = allSitesInDeck ?? [];
                _nonHavenSites = [.. _allSitesInDeck.Where(s => !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase))];

                if (existingResourceCards == null) return;

                foreach (var card in existingResourceCards)
                {
                    if (!_owner.NeedsDistinctNonHavenSite(card, _allSitesInDeck)) continue;

                    _occupyingCards.Add(card);

                    var testMatched = new List<Card>(_matchedCards.Count + 1);
                    testMatched.AddRange(_matchedCards);
                    testMatched.Add(card);

                    int newMatched = _owner.MaxNonHavenSiteAssignments(testMatched, _allSitesInDeck, _nonHavenSites, _capacityPerSite);
                    if (newMatched > _matchedCount)
                    {
                        _matchedCards.Add(card);
                        _matchedCount = newMatched;
                    }
                }
            }

            public bool CanAddWithinCapacity(IEnumerable<Card> newCards)
            {
                var tappers = newCards.Where(c => _owner.NeedsDistinctNonHavenSite(c, _allSitesInDeck)).ToList();
                if (tappers.Count == 0) return true;

                var testList = new List<Card>(_matchedCards.Count + tappers.Count);
                testList.AddRange(_matchedCards);
                testList.AddRange(tappers);

                int newMatched = _owner.MaxNonHavenSiteAssignments(testList, _allSitesInDeck, _nonHavenSites, _capacityPerSite);
                return newMatched >= _matchedCount + tappers.Count;
            }

            public void Commit(IEnumerable<Card> addedCards, List<string> overlapsOut = null)
            {
                _ = overlapsOut;
                var tappers = addedCards.Where(c => _owner.NeedsDistinctNonHavenSite(c, _allSitesInDeck)).ToList();
                if (tappers.Count == 0) return;

                foreach (var tapper in tappers)
                {
                    var testMatched = new List<Card>(_matchedCards.Count + 1);
                    testMatched.AddRange(_matchedCards);
                    testMatched.Add(tapper);

                    int newMatched = _owner.MaxNonHavenSiteAssignments(testMatched, _allSitesInDeck, _nonHavenSites, _capacityPerSite);
                    if (newMatched > _matchedCount)
                    {
                        _matchedCards.Add(tapper);
                        _matchedCount = newMatched;
                    }
                    else
                    {
                        _overlaps.Add(CaptureOverlap(tapper));
                    }
                    _occupyingCards.Add(tapper);
                }
            }

            public List<string> BuildOverlapMessages()
            {
                var messages = new List<string>();
                if (_overlaps.Count == 0) return messages;

                var groups = _overlaps.GroupBy(o => (
                    BottleneckSiteKey: string.Join("|", o.BottleneckSiteIndices),
                    BottleneckCardsText: o.BottleneckCards.Count > 0 ? FormatCardCountList(o.BottleneckCards) : string.Empty,
                    o.UsedExactBottleneck));

                foreach (var group in groups)
                {
                    var first = group.First();
                    string cardsSummary = FormatCardCountList(group.Select(g => g.Tapper));

                    if (first.TapIndices.Count == 0)
                    {
                        string verb = group.Count() > 1 ? "have" : "has";
                        messages.Add($"Site capacity exceeded in Resources: {cardsSummary} {verb} no matching non-haven site in Sites.");
                        continue;
                    }

                    var distinctTapKeys = group
                        .Select(g => string.Join("|", g.TapIndices))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    string tapSitesSummary = distinctTapKeys.Count == 1
                        ? FormatSiteIndices(first.TapIndices, 3)
                        : null;

                    string bottleneckSitesSummary = FormatSiteIndices(first.BottleneckSiteIndices, 4);
                    int slotCount = first.UsedExactBottleneck
                        ? first.BottleneckCards.Count
                        : first.BottleneckSiteIndices.Count * _capacityPerSite;

                    string slotWord = slotCount == 1 ? "slot" : "slots";
                    string filledBy = !string.IsNullOrEmpty(group.Key.BottleneckCardsText)
                        ? $" already filled by {group.Key.BottleneckCardsText}"
                        : string.Empty;

                    if (distinctTapKeys.Count == 1 &&
                        string.Equals(distinctTapKeys[0], group.Key.BottleneckSiteKey, StringComparison.Ordinal))
                    {
                        messages.Add(
                            $"Site capacity exceeded in Resources ({_capacityPerSite} tap/site per deck): " +
                            $"{cardsSummary} ({tapSitesSummary}) — {slotCount}-slot capacity{filledBy}.");
                    }
                    else if (tapSitesSummary != null)
                    {
                        messages.Add(
                            $"Site capacity exceeded in Resources ({_capacityPerSite} tap/site per deck): " +
                            $"{cardsSummary} (playable at {tapSitesSummary})\n" +
                            $"   Shared site pool ({bottleneckSitesSummary} — {slotCount} {slotWord}){filledBy}.");
                    }
                    else
                    {
                        messages.Add(
                            $"Site capacity exceeded in Resources ({_capacityPerSite} tap/site per deck): " +
                            $"{cardsSummary}\n" +
                            $"   Shared site pool ({bottleneckSitesSummary} — {slotCount} {slotWord}){filledBy}.");
                    }
                }

                return messages;
            }

            private OverlapRecord CaptureOverlap(Card tapper)
            {
                var tapIndices = _owner.GetNonHavenTapSiteIndices(tapper, _allSitesInDeck, _nonHavenSites);
                if (tapIndices.Count == 0)
                {
                    return new OverlapRecord(tapper, tapIndices, [], [], false);
                }

                var bottleneckCards = new List<Card>();
                for (int i = 0; i < _matchedCards.Count; i++)
                {
                    var withoutI = new List<Card>(_matchedCards.Count);
                    for (int j = 0; j < _matchedCards.Count; j++)
                    {
                        if (j != i) withoutI.Add(_matchedCards[j]);
                    }
                    withoutI.Add(tapper);

                    if (_owner.MaxNonHavenSiteAssignments(withoutI, _allSitesInDeck, _nonHavenSites, _capacityPerSite) >= _matchedCount)
                    {
                        bottleneckCards.Add(_matchedCards[i]);
                    }
                }

                bool usedExactBottleneck = bottleneckCards.Count > 0;
                if (!usedExactBottleneck)
                {
                    var tapIndexSet = tapIndices.ToHashSet();
                    bottleneckCards = [.. _occupyingCards
                        .Where(other => _owner.GetNonHavenTapSiteIndices(other, _allSitesInDeck, _nonHavenSites).Any(idx => tapIndexSet.Contains(idx)))];
                }

                var siteSet = new HashSet<int>(tapIndices);
                foreach (var bc in bottleneckCards)
                {
                    foreach (int idx in _owner.GetNonHavenTapSiteIndices(bc, _allSitesInDeck, _nonHavenSites))
                    {
                        siteSet.Add(idx);
                    }
                }

                return new OverlapRecord(
                    tapper,
                    tapIndices,
                    bottleneckCards,
                    [.. siteSet.OrderBy(i => i)],
                    usedExactBottleneck);
            }

            private string FormatSiteIndices(IReadOnlyList<int> indices, int maxDisplay)
            {
                var names = indices.Select(i => _nonHavenSites[i].Name).ToList();
                return names.Count <= maxDisplay
                    ? string.Join(", ", names)
                    : $"{string.Join(", ", names.Take(maxDisplay))}, ... ({names.Count} sites)";
            }
        }

        private bool IsSiteTappingResourceCard(Card card)
        {
            if (card == null) return false;
            if (!string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(card.Secondary, "Faction", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Secondary, "Ally", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return IsItemCard(card) && !IsSpecialRingItem(card);
        }

        private bool NeedsDistinctNonHavenSite(Card card, List<Card> allSitesInDeck)
        {
            if (!IsSiteTappingResourceCard(card) || allSitesInDeck == null || allSitesInDeck.Count == 0)
            {
                return false;
            }
            return !HasFreeHavenOption(card, allSitesInDeck);
        }

        private bool HasFreeHavenOption(Card card, List<Card> allSitesInDeck)
        {
            var havensInDeck = allSitesInDeck
                .Where(s => string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (havensInDeck.Count == 0) return false;

            var siteReqs = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
            if (siteReqs.Count > 0)
            {
                return siteReqs.Any(req => MatchesAnySiteRequirement(havensInDeck, req, _filterService));
            }
            return havensInDeck.Any(h => MatchesCardFlagPlayabilityAtSite(card, h));
        }

        private List<int> GetNonHavenTapSiteIndices(Card card, List<Card> allSitesInDeck, List<Card> nonHavenSites)
        {
            var indices = new List<int>();
            if (card == null || nonHavenSites == null || nonHavenSites.Count == 0) return indices;

            var siteReqTags = _filterService.GetCardTagsForCategory(card.Id, "Site Requirement");
            if (siteReqTags.Count > 0)
            {
                var matchedIndices = new HashSet<int>();
                foreach (var rawReq in siteReqTags)
                {
                    string cleanReq = rawReq.Trim();
                    if (cleanReq.Contains("&&"))
                    {
                        if (!MatchesAnySiteRequirement(allSitesInDeck, cleanReq, _filterService)) continue;

                        var terms = cleanReq.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var term in terms)
                        {
                            for (int i = 0; i < nonHavenSites.Count; i++)
                            {                                if (SiteMatchesSingleRequirement(nonHavenSites[i], term, _filterService))
                                {
                                    matchedIndices.Add(i);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < nonHavenSites.Count; i++)
                        {
                            if (SiteMatchesSingleRequirement(nonHavenSites[i], cleanReq, _filterService))
                            {
                                matchedIndices.Add(i);
                            }
                        }
                    }
                }
                indices.AddRange(matchedIndices.OrderBy(i => i));
                return indices;
            }

            for (int i = 0; i < nonHavenSites.Count; i++)
            {
                if (MatchesCardFlagPlayabilityAtSite(card, nonHavenSites[i]))
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        private static bool MatchesCardFlagPlayabilityAtSite(Card card, Card site)
        {
            if (card == null || site == null) return false;

            bool needsHoard = string.Equals(card.Hoard, "yes", StringComparison.OrdinalIgnoreCase);
            if (needsHoard && !string.Equals(site.Hoard, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(card.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(site.GoldRing, "yes", StringComparison.OrdinalIgnoreCase);
            }

            if (IsPalantirItem(card))
            {
                return string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(site.Palantiri, "yes", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(card.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(site.MinorItem, "yes", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(card.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(site.MajorItem, "yes", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(card.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                       (HasRaceToken(card, "Scroll") && string.Equals(site.Scroll, "yes", StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private int MaxNonHavenSiteAssignments(
            List<Card> cards,
            List<Card> allSitesInDeck,
            List<Card> nonHavenSites,
            int capacityPerSite)
        {
            if (cards == null || cards.Count == 0 || nonHavenSites == null || nonHavenSites.Count == 0 || capacityPerSite <= 0)
            {
                return 0;
            }

            var adj = new List<int>[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                adj[i] = GetNonHavenTapSiteIndices(cards[i], allSitesInDeck, nonHavenSites);
            }

            var siteOccupants = new List<int>[nonHavenSites.Count];
            for (int i = 0; i < nonHavenSites.Count; i++)
            {
                siteOccupants[i] = [];
            }

            int matched = 0;
            for (int u = 0; u < cards.Count; u++)
            {
                bool[] visited = new bool[nonHavenSites.Count];
                if (TryMatchSite(u, adj, siteOccupants, capacityPerSite, visited))
                {
                    matched++;
                }
            }

            return matched;
        }

        private static bool TryMatchSite(
            int u,
            List<int>[] adj,
            List<int>[] siteOccupants,
            int capacityPerSite,
            bool[] visited)
        {
            foreach (int siteIdx in adj[u])
            {
                if (visited[siteIdx]) continue;
                visited[siteIdx] = true;

                if (siteOccupants[siteIdx].Count < capacityPerSite)
                {
                    siteOccupants[siteIdx].Add(u);
                    return true;
                }

                for (int k = 0; k < siteOccupants[siteIdx].Count; k++)
                {
                    int existingCardIdx = siteOccupants[siteIdx][k];
                    if (TryMatchSite(existingCardIdx, adj, siteOccupants, capacityPerSite, visited))
                    {
                        siteOccupants[siteIdx][k] = u;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsItemCard(Card card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Secondary)) return false;
            return card.Secondary.Contains("Item", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasRaceToken(Card card, string token)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Race) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
            var parts = card.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Any(p => CardFilterService.EqualsWithDiacriticFallback(p, token));
        }

        private bool HasSpecialAbility(Card card, string abilityTag) =>
            card != null &&
            _filterService.GetCardTagsForCategory(card.Id, "Special Ability")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, abilityTag));

        private bool HasRoleTag(Card card, string roleTag) =>
            card != null &&
            _filterService.GetCardTagsForCategory(card.Id, "Role")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, roleTag));

        private bool IsQuestPackageCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
            IsHeroPlayableAlignment(card.Alignment) &&
            HasRoleTag(card, "Quest");

        private static bool IsResourceEventPackageCard(Card card) =>
            card != null &&
            ((string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
              card.Secondary != null &&
              card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase)) ||
             CardFilterService.EqualsWithDiacriticFallback(card.Name, "Twilight"));

        private bool IsGeneralResourceEvent(Card card) =>
            IsResourceEventPackageCard(card) &&
            !IsRingPackageCard(card) &&
            !IsPalantirPackageCard(card) &&
            !IsQuestPackageCard(card);

        private bool IsGeneralHazardEvent(Card card) =>
            card != null &&
            !IsCreaturesTabCard(card) &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            card.Secondary != null &&
            card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase);

        private static bool IsNazgulCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            HasRaceToken(card, "Nazgûl");

        private bool IsNazgulSupportCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            !HasRaceToken(card, "Nazgûl") &&
            card.Secondary != null &&
            card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase) &&
            _filterService.GetCardTagsForCategory(card.Id, "Target Species")
                .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, "Nazgûl"));

        private static bool IsStandardCreatureCard(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(card.Secondary, "Creature", StringComparison.OrdinalIgnoreCase);

        private static bool IsDualModeNonNazgulCreature(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Hazard", StringComparison.OrdinalIgnoreCase) &&
            !HasRaceToken(card, "Nazgûl") &&
            card.Secondary != null &&
            card.Secondary.Contains("Creature", StringComparison.OrdinalIgnoreCase) &&
            card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase);

        private bool IsCreaturesTabCard(Card card) =>
            IsNazgulCard(card) ||
            IsNazgulSupportCard(card) ||
            IsStandardCreatureCard(card) ||
            IsDualModeNonNazgulCreature(card);

        private static int CountEffectiveSpecialAbility(IEnumerable<Card> cards)
        {
            int count = 0;
            var seenUnique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in cards)
            {
                if (string.Equals(c.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenUnique.Add(c.Name))
                    {
                        count++;
                    }
                }
                else
                {
                    count++;
                }
            }
            return count;
        }

        private static string FormatInnateAbilitySummary(List<Card> cards, int effectiveCount)
        {
            string raw = FormatCardCountList(cards);
            return effectiveCount < cards.Count
                ? $"{raw} (counts as {effectiveCount} unique)"
                : raw;
        }

        private static bool MatchesSkillOrRaceRequirements(Card card, List<Card> allDraftedChars)
        {
            if (string.IsNullOrWhiteSpace(card.Skill)) return true;
            var tokens = card.Skill.Split([' ', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tokens.Any(tok => allDraftedChars.Any(ch =>
                CardFilterService.MatchesCardnumCriterion(ch, "Skill", tok) ||
                CardFilterService.MatchesCardnumCriterion(ch, "Race", tok)));
        }

        private static bool IsPalantirItem(Card card) =>
            card != null &&
            string.Equals(card.Primary, "Resource", StringComparison.OrdinalIgnoreCase) &&
            IsItemCard(card) &&
            HasRaceToken(card, "Palantír");

        private bool IsPalantirPackageCard(Card card)
        {
            if (card == null) return false;
            if (IsPalantirItem(card)) return true;
            if (card.Secondary != null && card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                return HasSpecialAbility(card, "Palantír: Allow Use");
            }
            return false;
        }

        private static bool IsStandardTierItem(Card card)
        {
            if (card == null) return false;
            return string.Equals(card.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRingPackageCard(Card card)
        {
            if (card == null) return false;
            if (string.Equals(card.Secondary, "Gold Ring Item", StringComparison.OrdinalIgnoreCase)) return true;
            if (IsSpecialRingItem(card)) return true;
            if (card.Secondary != null && card.Secondary.Contains("event", StringComparison.OrdinalIgnoreCase))
            {
                return HasSpecialAbility(card, "Gold Ring: Allow Test") ||
                       HasSpecialAbility(card, "Gold Ring: Aid Test");
            }
            return false;
        }

        private bool IsSpecialRingItem(Card card)
        {
            if (card == null || !string.Equals(card.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return IsOneRingCard(card) ||
                   IsDwarvenRingCard(card) ||
                   IsMagicRingCard(card) ||
                   IsLesserRingCard(card) ||
                   IsWizardsRingCard(card);
        }

        private static bool IsSpecialItemCard(Card c) =>
            c != null && string.Equals(c.Secondary, "Special Item", StringComparison.OrdinalIgnoreCase);

        private static bool IsOneRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "The One Ring") ||
             HasRaceToken(c, "One Ring") ||
             CardFilterService.EqualsWithDiacriticFallback(c.Name, "The One Ring"));

        private static bool IsDwarvenRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Dwarven Ring") ||
             c.Name.StartsWith("Dwarven Ring", StringComparison.OrdinalIgnoreCase));

        private static bool IsMagicRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Magic Ring") ||
             c.Name.StartsWith("Magic Ring", StringComparison.OrdinalIgnoreCase));

        private static bool IsLesserRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            (HasRaceToken(c, "Lesser Ring") ||
             CardFilterService.EqualsWithDiacriticFallback(c.Name, "Lesser Ring"));

        private bool IsWizardsRingCard(Card c) =>
            IsSpecialItemCard(c) &&
            HasRaceToken(c, "Ring") &&
            (CardFilterService.MatchesCardnumCriterion(c, "Skill", "Wizard") ||
             _filterService.GetCardTagsForCategory(c.Id, "Target Species")
                 .Any(t => CardFilterService.EqualsWithDiacriticFallback(t, "Wizard")));

        private static (GroupBox Box, RichTextBox Status) CreateStatusGroupBox(
            int x,
            int y,
            int width,
            int height,
            string initialText = null,
            string title = "Drafting Status && Results")
        {
            var grp = new GroupBox
            {
                Text = title,
                Location = new Point(x, y),
                Size = new Size(width, height)
            };

            var rtb = CreateStatusRichTextBox(16, 26, width - 32, height - 36, initialText);
            grp.Controls.Add(rtb);
            return (grp, rtb);
        }

        private static RichTextBox CreateStatusRichTextBox(int x, int y, int width, int height, string initialText = null)
        {
            var rtb = new RichTextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f),
                ReadOnly = true,
                TabStop = false,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            if (!string.IsNullOrEmpty(initialText))
            {
                SetRichPlainStatus(rtb, initialText, Color.FromArgb(74, 85, 104), FontStyle.Italic);
            }

            return rtb;
        }

        private static void SetRichPlainStatus(RichTextBox rtb, string text, Color color, FontStyle style)
        {
            if (rtb == null) return;

            rtb.SuspendLayout();
            rtb.Clear();
            rtb.ForeColor = color;
            using var font = new Font("Segoe UI", 9.0f, style);
            rtb.SelectionStart = 0;
            rtb.SelectionLength = 0;
            rtb.SelectionIndent = 0;
            rtb.SelectionHangingIndent = 0;
            rtb.SelectionColor = color;
            rtb.SelectionFont = font;
            rtb.AppendText(text ?? string.Empty);
            rtb.SelectionStart = 0;
            rtb.ScrollToCaret();
            rtb.ResumeLayout();
        }

        private static void SetRichBulletedStatus(
            RichTextBox rtb,
            string headerLine,
            IReadOnlyList<IReadOnlyList<(string Label, string Value)>> bulletGroups,
            IReadOnlyList<string> warnings = null)
        {
            if (rtb == null) return;

            Color headerColor = Color.FromArgb(74, 85, 104);
            Color labelColor = Color.FromArgb(26, 54, 93);
            Color valueColor = Color.FromArgb(45, 55, 72);
            Color noneColor = Color.FromArgb(113, 128, 150);
            Color warnColor = Color.FromArgb(180, 83, 9);

            using var headerFont = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            using var labelFont = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            using var valueFont = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            using var noneFont = new Font("Segoe UI", 8.75f, FontStyle.Italic);
            using var warnFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var bulletGapFont = new Font("Segoe UI", 3.0f, FontStyle.Regular);
            using var groupGapFont = new Font("Segoe UI", 5.5f, FontStyle.Regular);

            void AppendSegment(string text, Color color, Font font)
            {
                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionColor = color;
                rtb.SelectionFont = font;
                rtb.AppendText(text);
            }

            void StartParagraph(int hangingIndent)
            {
                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionIndent = 0;
                rtb.SelectionHangingIndent = hangingIndent;
            }

            void EndParagraphAndAddGap(Font closingFont, Font gapFont)
            {
                AppendSegment("\n", headerColor, closingFont);
                StartParagraph(0);
                AppendSegment("\n", headerColor, gapFont);
            }

            rtb.SuspendLayout();
            rtb.Clear();
            rtb.ForeColor = headerColor;

            Font lastClosingFont = null;

            if (!string.IsNullOrWhiteSpace(headerLine))
            {
                StartParagraph(0);
                AppendSegment(headerLine, headerColor, headerFont);
                lastClosingFont = headerFont;
            }

            if (bulletGroups != null)
            {
                foreach (var group in bulletGroups)
                {
                    if (group == null || group.Count == 0) continue;

                    for (int i = 0; i < group.Count; i++)
                    {
                        var (label, value) = group[i];
                        if (lastClosingFont != null)
                        {
                            EndParagraphAndAddGap(lastClosingFont, i == 0 ? groupGapFont : bulletGapFont);
                        }

                        StartParagraph(0);
                        AppendSegment($"{label} ", labelColor, labelFont);
                        bool isNone = string.Equals(value, "(None)", StringComparison.Ordinal);
                        Font curValFont = isNone ? noneFont : valueFont;
                        AppendSegment(value ?? string.Empty, isNone ? noneColor : valueColor, curValFont);
                        lastClosingFont = curValFont;
                    }
                }
            }

            if (warnings != null)
            {
                for (int i = 0; i < warnings.Count; i++)
                {
                    if (lastClosingFont != null)
                    {
                        EndParagraphAndAddGap(lastClosingFont, i == 0 ? groupGapFont : bulletGapFont);
                    }
                    StartParagraph(0);
                    AppendSegment($"⚠ {warnings[i]}", warnColor, warnFont);
                    lastClosingFont = warnFont;
                }
            }

            rtb.SelectionStart = 0;
            rtb.ScrollToCaret();
            rtb.ResumeLayout();
        }

        private static string FormatCardCountList(IEnumerable<Card> cards) =>
            string.Join(", ", cards
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.First().Name, StringComparer.CurrentCultureIgnoreCase.WithNaturalSort())
                .Select(g => g.Count() > 1 ? $"{g.Count()}x {g.First().Name}" : g.First().Name));

        private bool MatchesItemSitePlayability(Card item, List<Card> allSitesInDeck)
        {
            if (allSitesInDeck == null || allSitesInDeck.Count == 0) return false;

            var siteReqs = _filterService.GetCardTagsForCategory(item.Id, "Site Requirement");
            if (siteReqs.Count > 0)
            {
                return siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService));
            }

            bool needsHoard = string.Equals(item.Hoard, "yes", StringComparison.OrdinalIgnoreCase);
            return allSitesInDeck.Any(site =>
            {
                if (needsHoard && !string.Equals(site.Hoard, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (string.Equals(item.Secondary, "Minor Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.MinorItem, "yes", StringComparison.OrdinalIgnoreCase);
                }
                if (string.Equals(item.Secondary, "Major Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.MajorItem, "yes", StringComparison.OrdinalIgnoreCase);
                }
                if (string.Equals(item.Secondary, "Greater Item", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(site.GreaterItem, "yes", StringComparison.OrdinalIgnoreCase) ||
                           (HasRaceToken(item, "Scroll") && string.Equals(site.Scroll, "yes", StringComparison.OrdinalIgnoreCase));
                }
                return false;
            });
        }

        private bool MatchesItemRegionRequirement(
            Card item,
            HashSet<string> draftedRegionNames,
            HashSet<string> draftedRegionTypes)
        {
            var regReqs = _filterService.GetCardTagsForCategory(item.Id, "Region Requirement");
            if (regReqs.Count == 0) return true;

            return regReqs.Any(req =>
                draftedRegionNames.Any(rn => CardFilterService.EqualsWithDiacriticFallback(rn, req)) ||
                draftedRegionTypes.Any(rt => CardFilterService.EqualsWithDiacriticFallback(rt, req)));
        }

        private bool MatchesItemTargetSpecies(Card item, List<Card> allDraftedChars)
        {
            var speciesTags = _filterService.GetCardTagsForCategory(item.Id, "Target Species");
            if (speciesTags.Count == 0) return true;

            var requiredHeroRaces = speciesTags
                .Where(tag => HeroTargetSpecies.Any(hr => CardFilterService.EqualsWithDiacriticFallback(tag, hr)))
                .ToList();

            // Ignore Creature-only Target Species on Items
            if (requiredHeroRaces.Count == 0) return true;

            foreach (var ch in allDraftedChars)
            {
                if (string.IsNullOrWhiteSpace(ch.Race)) continue;
                var charRaces = ch.Race.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (charRaces.Any(cr => requiredHeroRaces.Any(req => CardFilterService.EqualsWithDiacriticFallback(cr, req))))
                {
                    return true;
                }
            }
            return false;
        }

        private bool SatisfiesExistingPrerequisites(Card card, List<Card> existingDeckCards)
        {
            var prereqTags = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite");
            if (prereqTags.Count == 0) return true;

            foreach (var orBranch in prereqTags)
            {
                var andTerms = orBranch.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (andTerms.Length == 0) continue;

                bool allPresent = andTerms.All(term =>
                    existingDeckCards.Any(c => CardFilterService.EqualsWithDiacriticFallback(c.Name, term)));
                if (allPresent) return true;
            }
            return false;
        }

        private bool TryResolvePrerequisiteChain(
            Card card,
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            List<Card> allSitesInDeck,
            List<Card> allDraftedChars,
            List<Card> resolvedPrereqs,
            HashSet<string> visitedNames)
        {
            if (card == null || !visitedNames.Add(card.Name)) return true;

            var prereqTags = _filterService.GetCardTagsForCategory(card.Id, "Prerequisite");
            if (prereqTags.Count == 0) return true;

            // Semicolon-separated entries in Prerequisite are OR branches; "&&" inside an entry requires all terms (AND).
            foreach (var orBranch in prereqTags)
            {
                var andTerms = orBranch.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (andTerms.Length == 0) continue;

                var branchResolved = new List<Card>();
                var branchVisited = new HashSet<string>(visitedNames, StringComparer.OrdinalIgnoreCase);
                bool branchSucceeded = true;

                foreach (var prereqName in andTerms)
                {
                    var prereqCard = allCards
                        .Where(c =>
                            (activeSets == null || activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                            CardFilterService.EqualsWithDiacriticFallback(c.Name, prereqName) &&
                            IsHeroPlayableAlignment(c.Alignment))
                        .OrderBy(c => string.Equals(c.Alignment, "Hero", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .FirstOrDefault();

                    if (prereqCard == null)
                    {
                        branchSucceeded = false;
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(prereqCard.Specific) &&
                        !allDraftedChars.Any(ch => CardFilterService.EqualsWithDiacriticFallback(ch.Name, prereqCard.Specific)))
                    {
                        branchSucceeded = false;
                        break;
                    }

                    var prereqSiteReqs = _filterService.GetCardTagsForCategory(prereqCard.Id, "Site Requirement");
                    if (prereqSiteReqs.Count > 0 &&
                        !prereqSiteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService)))
                    {
                        branchSucceeded = false;
                        break;
                    }

                    if (!resolvedPrereqs.Any(p => CardFilterService.EqualsWithDiacriticFallback(p.Name, prereqCard.Name)) &&
                        !branchResolved.Any(p => CardFilterService.EqualsWithDiacriticFallback(p.Name, prereqCard.Name)))
                    {
                        branchResolved.Add(prereqCard);
                    }

                    if (!TryResolvePrerequisiteChain(
                        prereqCard,
                        allCards,
                        activeSets,
                        allSitesInDeck,
                        allDraftedChars,
                        branchResolved,
                        branchVisited))
                    {
                        branchSucceeded = false;
                        break;
                    }
                }

                if (branchSucceeded)
                {
                    foreach (var p in branchResolved)
                    {
                        if (!resolvedPrereqs.Any(existing => CardFilterService.EqualsWithDiacriticFallback(existing.Name, p.Name)))
                        {
                            resolvedPrereqs.Add(p);
                        }
                    }
                    foreach (var v in branchVisited)
                    {
                        visitedNames.Add(v);
                    }
                    return true;
                }
            }

            return false;
        }

        private static bool SiteMatchesSingleRequirement(Card site, string cleanReq, CardFilterService filterService)
        {
            if (site == null || string.IsNullOrWhiteSpace(cleanReq)) return false;

            if (CardFilterService.TryParsePredicate(cleanReq, out _, out _, out _))
            {
                return CardFilterService.MatchesPredicate(site, cleanReq, filterService);
            }

            if (CanonicalHoldTypes.Contains(CardFilterService.NormalizeSiteType(cleanReq)))
            {
                return CardFilterService.MatchesSiteType(site.Site, cleanReq);
            }

            return CardFilterService.EqualsWithDiacriticFallback(site.Name, cleanReq) ||
                   string.Equals(site.Name.Replace("-", " "), cleanReq.Replace("-", " "), StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesAnySiteRequirement(List<Card> sites, string requirement, CardFilterService filterService)
        {
            if (sites == null || sites.Count == 0 || string.IsNullOrWhiteSpace(requirement)) return false;

            string cleanReq = requirement.Trim();
            if (cleanReq.Contains("&&"))
            {
                var andTerms = cleanReq.Split("&&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return andTerms.Length > 0 && andTerms.All(term => MatchesAnySiteRequirement(sites, term, filterService));
            }

            return sites.Any(s => SiteMatchesSingleRequirement(s, cleanReq, filterService));
        }

        private static bool IsHeroPlayableAlignment(string alignment)
        {
            if (string.IsNullOrWhiteSpace(alignment)) return true;
            return string.Equals(alignment, "Hero", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Neutral", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alignment, "Dual", StringComparison.OrdinalIgnoreCase);
        }

        private static Card FindHeroPreferredCard(
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            string primary,
            string name)
        {
            if (allCards == null || string.IsNullOrWhiteSpace(name)) return null;

            var inActiveSets = allCards
                .Where(c =>
                    (activeSets == null || activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, name))
                .ToList();

            if (inActiveSets.Count > 0)
            {
                return PickHeroPreferredFromGroup(inActiveSets);
            }

            var anywhere = allCards
                .Where(c =>
                    string.Equals(c.Primary, primary, StringComparison.OrdinalIgnoreCase) &&
                    CardFilterService.EqualsWithDiacriticFallback(c.Name, name))
                .ToList();

            return PickHeroPreferredFromGroup(anywhere);
        }

        private static Card PickHeroPreferredFromGroup(IEnumerable<Card> group)
        {
            var list = group?.ToList();
            if (list == null || list.Count == 0) return null;
            return list.FirstOrDefault(c => string.Equals(c.Alignment, "Hero", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(c => string.Equals(c.Alignment, "Neutral", StringComparison.OrdinalIgnoreCase))
                ?? list[0];
        }

        private static bool IsAvatar(Card card)
        {
            if (card == null) return false;
            return string.Equals(card.Secondary, "Avatar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(card.Race, "Ringwraith", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseCardMind(string mindStr)
        {
            if (string.IsNullOrWhiteSpace(mindStr)) return 0;
            string clean = mindStr.Trim('(', ')').Trim();
            return int.TryParse(clean, out int val) ? val : 0;
        }

        private static (int Base, int Conditional) ParseCardMPs(string mpsStr)
        {
            if (string.IsNullOrWhiteSpace(mpsStr)) return (0, 0);
            string trimmed = mpsStr.Trim();
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)) return (0, 0);

            int openParen = trimmed.IndexOf('(');
            int closeParen = openParen >= 0 ? trimmed.IndexOf(')', openParen + 1) : -1;

            // Purely parenthesized e.g. "(2)" -> 0 base, 2 conditional
            if (openParen == 0 && closeParen > 0)
            {
                string inner = trimmed[1..closeParen].Trim();
                return int.TryParse(inner, out int condOnly) ? (0, condOnly) : (0, 0);
            }

            // Base + conditional e.g. "4(6)" -> 4 base, 6 conditional
            if (openParen > 0 && closeParen > openParen)
            {
                string basePart = trimmed[..openParen].Trim();
                string condPart = trimmed[(openParen + 1)..closeParen].Trim();
                int baseVal = int.TryParse(basePart, out int b) ? b : 0;
                int condVal = int.TryParse(condPart, out int c) ? c : baseVal;
                return (baseVal, condVal);
            }

            // Plain integer e.g. "3", "-2" -> (3, 3)
            return int.TryParse(trimmed, out int plain) ? (plain, plain) : (0, 0);
        }

        private NumericUpDown CreateItemSpinner(int x, int y, int min, int max, int defVal)
        {
            var nud = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(64, 23),
                Minimum = min,
                Maximum = max,
                Value = defVal
            };
            nud.ValueChanged += (s, e) => ValidateWorkbenchReadiness();
            return nud;
        }

        private static void SetComboSelection(ComboBox cb, string value)
        {
            if (cb == null || string.IsNullOrWhiteSpace(value)) return;
            int idx = cb.FindStringExact(value);
            if (idx >= 0) cb.SelectedIndex = idx;
        }

        private static void SetAllChecked(CheckedListBox clb, bool isChecked)
        {
            if (clb == null) return;
            for (int i = 0; i < clb.Items.Count; i++)
            {
                clb.SetItemChecked(i, isChecked);
            }
        }

        private static void SetCheckedItems(CheckedListBox clb, List<string> selectedItems)
        {
            if (clb == null) return;
            var selectedSet = new HashSet<string>(selectedItems ?? [], StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < clb.Items.Count; i++)
            {
                string itemStr = clb.Items[i]?.ToString() ?? "";
                clb.SetItemChecked(i, selectedSet.Contains(itemStr));
            }
        }

        private static void ApplyRowSettings(CharacterRowControls row, CharacterRowSettings settings)
        {
            if (row == null || settings == null) return;
            SetComboSelection(row.CmbSkill, settings.Skill);
            SetComboSelection(row.CmbSpecies, settings.Species);
            row.TxtMind.Text = settings.Mind ?? string.Empty;
            row.TxtDI.Text = settings.DI ?? string.Empty;
        }
    }

    internal sealed class CharacterRowControls
    {
        public ComboBox CmbSkill { get; set; }
        public ComboBox CmbSpecies { get; set; }
        public TextBox TxtMind { get; set; }
        public TextBox TxtDI { get; set; }
        public Button BtnClear { get; set; }

        public bool IsActive =>
            CmbSkill.SelectedIndex > 0 ||
            CmbSpecies.SelectedIndex > 0 ||
            !string.IsNullOrWhiteSpace(TxtMind.Text) ||
            !string.IsNullOrWhiteSpace(TxtDI.Text);

        public void Clear()
        {
            CmbSkill.SelectedIndex = 0;
            CmbSpecies.SelectedIndex = 0;
            TxtMind.Text = string.Empty;
            TxtDI.Text = string.Empty;
        }
    }

    public sealed class CharacterRowSettings
    {
        public string Skill { get; set; } = "Any";
        public string Species { get; set; } = "Any";
        public string Mind { get; set; } = string.Empty;
        public string DI { get; set; } = string.Empty;
    }

    public sealed class DeckAssemblySettings
    {
        public string Version { get; set; } = "1.0";
        public string DeckTitle { get; set; } = "New Deck";
        public int PlayDeckSize { get; set; } = 30;
        public int SideboardSize { get; set; } = 20;
        public int DecksBeforeCouncil { get; set; } = 1;
        public bool TailorHazardsToCurrentDeck { get; set; }

        // Tab 2: Regions & Sites
        public string SeedRegion { get; set; } = "Random";
        public int RegionCount { get; set; } = 5;
        public int MaxSiteCount { get; set; } = 12;
        public bool RequireHavenInRegionGroup { get; set; }
        public List<string> SelectedRegionTypes { get; set; } = [];
        public List<string> SelectedSiteTypes { get; set; } = [];

        // Tab 3: Starting Company
        public List<CharacterRowSettings> StarterRows { get; set; } = [];

        // Tab 4: Wizards & Recruits
        public string PrimaryWizard { get; set; } = "Random";
        public int PrimaryWizardCopies { get; set; } = 2;
        public string SecondaryWizard { get; set; } = "Random";
        public int SecondaryWizardCopies { get; set; } = 1;
        public string SecondaryWizardDestination { get; set; } = "Sideboard";
        public List<CharacterRowSettings> RecruitRows { get; set; } = [];
        public List<CharacterRowSettings> ReserveRows { get; set; } = [];

        // Tab 5: Factions
        public int ResourceFactionCount { get; set; } = 1;
        public int SideboardFactionCount { get; set; } = 0;

        // Tab 6: Allies
        public int ResourceAllyCount { get; set; } = 1;
        public int SideboardAllyCount { get; set; } = 0;

        // Tab 7: Items
        public int PoolMinorItemCount { get; set; } = 2;
        public int ResourceMinorItemCount { get; set; } = 1;
        public int SideboardMinorItemCount { get; set; } = 0;
        public int ResourceMajorItemCount { get; set; } = 1;
        public int SideboardMajorItemCount { get; set; } = 1;
        public int ResourceGreaterItemCount { get; set; } = 1;
        public int SideboardGreaterItemCount { get; set; } = 0;
        public int ResourceSpecialItemCount { get; set; } = 0;
        public int SideboardSpecialItemCount { get; set; } = 0;
        public List<string> ExcludedItemRoles { get; set; } = [];

        // Tab 8: Rings
        public int ResourceGoldRingCount { get; set; } = 0;
        public int SideboardGoldRingCount { get; set; } = 0;
        public int ResourceAllowRingTestCount { get; set; } = 0;
        public int SideboardAllowRingTestCount { get; set; } = 0;
        public int ResourceAidRingTestCount { get; set; } = 0;
        public int SideboardAidRingTestCount { get; set; } = 0;
        public int ResourceOneRingCount { get; set; } = 0;
        public int SideboardOneRingCount { get; set; } = 0;
        public int ResourceDwarvenRingCount { get; set; } = 0;
        public int SideboardDwarvenRingCount { get; set; } = 0;
        public int ResourceMagicRingCount { get; set; } = 0;
        public int SideboardMagicRingCount { get; set; } = 0;
        public int ResourceLesserRingCount { get; set; } = 0;
        public int SideboardLesserRingCount { get; set; } = 0;
        public int ResourceWizardsRingCount { get; set; } = 0;
        public int SideboardWizardsRingCount { get; set; } = 0;
        public List<string> ExcludedRingRoles { get; set; } = [];

        // Tab 9: Palantiri
        public int ResourcePalantirCount { get; set; } = 0;
        public int SideboardPalantirCount { get; set; } = 0;
        public int ResourceAllowPalantirUseCount { get; set; } = 0;
        public int SideboardAllowPalantirUseCount { get; set; } = 0;
        public List<string> ExcludedPalantirRoles { get; set; } = [];

        // Tab 10: Quests
        public int ResourceQuestCount { get; set; } = 0;
        public int SideboardQuestCount { get; set; } = 0;
        public bool AutoDraftOneRingPackage { get; set; } = true;
        public List<string> ExcludedQuestRoles { get; set; } = [];

        // Tab 11: Resource Events
        public int ResourceGatesOfMorningCount { get; set; } = 0;
        public int SideboardGatesOfMorningCount { get; set; } = 0;
        public int ResourceTwilightCount { get; set; } = 0;
        public int SideboardTwilightCount { get; set; } = 0;
        public int SideboardResourceEventCount { get; set; } = 4;
        public List<string> ExcludedResourceEventRoles { get; set; } = [];

        // Tab 12: Creatures
        public int HazardNazgulCount { get; set; } = 0;
        public int SideboardNazgulCount { get; set; } = 0;
        public int HazardNazgulSupportCount { get; set; } = 0;
        public int SideboardNazgulSupportCount { get; set; } = 0;
        public int HazardStandardCreatureCount { get; set; } = 12;
        public int SideboardStandardCreatureCount { get; set; } = 2;
        public int HazardDualCreatureCount { get; set; } = 0;
        public int SideboardDualCreatureCount { get; set; } = 0;
        public List<string> ExcludedCreatureSpecies { get; set; } = [];
        public List<string> SelectedCreatureRegionTypes { get; set; }
        public List<string> SelectedCreatureSiteTypes { get; set; }

        // Tab 13: Hazard Events
        public int HazardDoorsOfNightCount { get; set; } = 0;
        public int SideboardDoorsOfNightCount { get; set; } = 0;
        public int HazardTwilightCount { get; set; } = 0;
        public int SideboardHazardTwilightCount { get; set; } = 0;
        public int SideboardHazardEventCount { get; set; } = 4;
        public List<string> ExcludedHazardEventRoles { get; set; } = [];
        public List<string> SelectedHazardEventRegionTypes { get; set; }
        public List<string> SelectedHazardEventSiteTypes { get; set; }
    }
}

