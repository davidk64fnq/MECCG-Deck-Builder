using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    internal sealed class OpenCloseDeck
    {
        public string CurrentDeckTitle { get; set; } = "New Deck";
        public List<string[]> PoolList { get; set; } = [];
        public List<string[]> ResourceList { get; set; } = [];
        public List<string[]> HazardList { get; set; } = [];
        public List<string[]> SideboardList { get; set; } = [];
        public List<string[]> SiteList { get; set; } = [];
        public List<string> SetList { get; set; } = [];

        public static OpenCloseDeck FromDeck(Deck deck)
        {
            var dto = new OpenCloseDeck
            {
                CurrentDeckTitle = deck.Title,
                SetList = [.. deck.IncludedSets]
            };

            PopulateSectionDto(deck.Pool, dto.PoolList);
            PopulateSectionDto(deck.Resources, dto.ResourceList);
            PopulateSectionDto(deck.Hazards, dto.HazardList);
            PopulateSectionDto(deck.Sideboard, dto.SideboardList);
            PopulateSectionDto(deck.Sites, dto.SiteList);

            return dto;
        }

        public Deck ToDeck(CardCatalogService catalog)
        {
            var deck = new Deck
            {
                Title = CurrentDeckTitle
            };

            foreach (var set in SetList)
            {
                deck.IncludedSets.Add(set);
            }

            ResolveSectionCards(PoolList, deck.Pool, catalog);
            ResolveSectionCards(ResourceList, deck.Resources, catalog);
            ResolveSectionCards(HazardList, deck.Hazards, catalog);
            ResolveSectionCards(SideboardList, deck.Sideboard, catalog);
            ResolveSectionCards(SiteList, deck.Sites, catalog);

            return deck;
        }

        private static void PopulateSectionDto(IEnumerable<Card> cards, List<string[]> targetList)
        {
            foreach (var card in cards)
            {
                targetList.Add([card.Name, card.ImageName, card.Set, card.Id]);
            }
        }

        private static void ResolveSectionCards(List<string[]> dtoList, List<Card> targetSection, CardCatalogService catalog)
        {
            foreach (var item in dtoList)
            {
                if (item.Length >= 4)
                {
                    string cardId = item[(int)CardListField.id];
                    var card = catalog.GetCardById(cardId);
                    if (card != null)
                    {
                        targetSection.Add(card);
                    }
                }
            }
        }
    }
}