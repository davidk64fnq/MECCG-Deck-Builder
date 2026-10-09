using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    internal sealed class OpenCloseFilter
    {
        public List<SortedDictionary<string, string>> Cards { get; set; } = [];
        public List<List<string>> Filters { get; set; } = [];

        public static OpenCloseFilter FromFilterService(CardFilterService filterService)
        {
            var dto = new OpenCloseFilter();

            foreach (var filterList in filterService.CustomFilters)
            {
                dto.Filters.Add([.. filterList]);
            }

            foreach (var (cardId, tags) in filterService.CardCustomTags)
            {
                var dict = new SortedDictionary<string, string>
                {
                    ["id"] = cardId
                };
                foreach (var (k, v) in tags)
                {
                    dict[k] = v;
                }
                dto.Cards.Add(dict);
            }

            return dto;
        }
    }
}