using System;
using System.Collections.Generic;
using System.Linq;

namespace MECCG_Deck_Builder
{
    public sealed class Deck
    {
        public string Title { get; set; } = "New Deck";
        public HashSet<string> IncludedSets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<Card> Pool { get; } = [];
        public List<Card> Resources { get; } = [];
        public List<Card> Hazards { get; } = [];
        public List<Card> Sideboard { get; } = [];
        public List<Card> Sites { get; } = [];

        public List<Card> GetSection(DeckSectionType sectionType) => sectionType switch
        {
            DeckSectionType.Pool => Pool,
            DeckSectionType.Resources => Resources,
            DeckSectionType.Hazards => Hazards,
            DeckSectionType.Sideboard => Sideboard,
            DeckSectionType.Sites => Sites,
            _ => throw new ArgumentOutOfRangeException(nameof(sectionType))
        };

        public int CountCharactersInResources()
        {
            return Resources.Count(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase));
        }

        public void Clear()
        {
            Title = "New Deck";
            IncludedSets.Clear();
            Pool.Clear();
            Resources.Clear();
            Hazards.Clear();
            Sideboard.Clear();
            Sites.Clear();
        }

        public void SortSections()
        {
            static int CompareCards(Card a, Card b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal);

            Pool.Sort(CompareCards);
            Resources.Sort(CompareCards);
            Hazards.Sort(CompareCards);
            Sideboard.Sort(CompareCards);
            Sites.Sort(CompareCards);
        }
    }
}