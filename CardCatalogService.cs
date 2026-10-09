using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NaturalSort.Extension;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed class CardCatalogService
    {
        private static readonly HttpClient s_httpClient = new();

        public static readonly string[] FilterKeys =
        [
            "Primary", "Alignment", "Artist", "Skill", "MPs", "Mind", "Direct", "Prowess", "Body",
            "Corruption", "Home", "Unique", "Secondary", "Race", "Site", "Region", "Playable", "GoldRing", "GreaterItem",
            "MajorItem", "MinorItem", "Information", "Palantiri", "Scroll", "Hoard", "Haven", "Strikes", "Specific"
        ];

        private readonly List<Card> _cards = [];
        private readonly Dictionary<string, Card> _cardsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CardSet> _sets = [];
        private readonly Dictionary<string, List<string>> _filterOptions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Card> Cards => _cards;
        public IReadOnlyDictionary<string, Card> CardsById => _cardsById;
        public IReadOnlyList<CardSet> Sets => _sets;

        public event EventHandler<string> WarningOccurred;

        public Card GetCardById(string cardId)
        {
            if (!string.IsNullOrEmpty(cardId) && _cardsById.TryGetValue(cardId, out var card))
            {
                return card;
            }
            return null;
        }

        public IReadOnlyList<string> GetFilterKeys()
        {
            var keys = new List<string>(_filterOptions.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        public IReadOnlyList<string> GetFilterValues(string key)
        {
            if (!string.IsNullOrEmpty(key) && _filterOptions.TryGetValue(key, out var values))
            {
                return values;
            }
            return [""];
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await LoadSetsAsync(cancellationToken).ConfigureAwait(false);
            await LoadCardsAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadSetsAsync(CancellationToken cancellationToken)
        {
            string json = null;
            List<CardnumSet> rawSets = null;

            // 1. Try download from GitHub
            try
            {
                json = await s_httpClient.GetStringAsync(Constants.CardnumSetsURL, cancellationToken).ConfigureAwait(false);
                rawSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                try
                {
                    await File.WriteAllTextAsync(Constants.CardnumSetsFile, json, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumSetInfo3"));
                }
            }
            catch (Exception)
            {
                // 2. Fallback to local file
                try
                {
                    json = await File.ReadAllTextAsync(Constants.CardnumSetsFile, cancellationToken).ConfigureAwait(false);
                    rawSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumSetInfo1"));
                }
                catch (Exception)
                {
                    // 3. Emergency fallback
                    _sets.Clear();
                    _sets.Add(new CardSet
                    {
                        Code = "METW",
                        Name = "The Wizards",
                        Format = "General",
                        Position = 1,
                        Dreamcards = false,
                        Released = true
                    });
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumSetInfo2"));
                    return;
                }
            }

            _sets.Clear();
            if (rawSets != null)
            {
                foreach (var s in rawSets)
                {
                    _sets.Add(new CardSet
                    {
                        Code = s.Code,
                        Name = s.Name,
                        Format = s.Format,
                        Position = s.Position,
                        Dreamcards = s.Dreamcards,
                        Released = s.Released
                    });
                }
            }
        }

        private async Task LoadCardsAsync(CancellationToken cancellationToken)
        {
            string json = null;
            List<CardnumCard> rawCards = null;

            // 1. Try download from GitHub
            try
            {
                json = await s_httpClient.GetStringAsync(Constants.CardnumCardsURL, cancellationToken).ConfigureAwait(false);
                rawCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);
                try
                {
                    await File.WriteAllTextAsync(Constants.CardnumCardsFile, json, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumCardInfo3"));
                }
            }
            catch (Exception)
            {
                // 2. Fallback to local file
                try
                {
                    json = await File.ReadAllTextAsync(Constants.CardnumCardsFile, cancellationToken).ConfigureAwait(false);
                    rawCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumCardInfo1"));
                }
                catch (Exception)
                {
                    // 3. Emergency fallback: Single default card
                    var emergencyCard = new Card
                    {
                        Id = "0",
                        Set = "METW",
                        FullCode = "Adrazar (TW)",
                        Name = "Adrazar",
                        Alignment = "Hero",
                        ImageName = "metw_adrazar.jpg",
                        Released = true
                    };
                    _cards.Clear();
                    _cardsById.Clear();
                    _cards.Add(emergencyCard);
                    _cardsById[emergencyCard.Id] = emergencyCard;
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumCardInfo2"));
                    return;
                }
            }

            InitializeFilterKeys();

            _cards.Clear();
            _cardsById.Clear();

            int idCounter = 0;
            if (rawCards != null)
            {
                foreach (var raw in rawCards)
                {
                    if (!raw.Dreamcard && !raw.Released)
                    {
                        continue;
                    }

                    string imageName = raw.Ice_errata == true ? $"ice-{raw.ImageName}" : raw.ImageName;

                    var card = new Card
                    {
                        Id = (idCounter++).ToString(),
                        Set = raw.Set ?? string.Empty,
                        FullCode = raw.FullCode ?? string.Empty,
                        Name = raw.NameEN ?? string.Empty,
                        Text = raw.Text ?? string.Empty,
                        ImageName = imageName ?? string.Empty,
                        Primary = raw.Primary ?? string.Empty,
                        Alignment = raw.Alignment ?? string.Empty,
                        Artist = raw.Artist ?? string.Empty,
                        Skill = raw.Skill ?? string.Empty,
                        MPs = raw.MPs ?? string.Empty,
                        Mind = raw.Mind ?? string.Empty,
                        Direct = raw.Direct ?? string.Empty,
                        Prowess = raw.Prowess?.ToString() ?? string.Empty,
                        Body = raw.Body ?? string.Empty,
                        Corruption = raw.Corruption ?? string.Empty,
                        Home = raw.Home ?? string.Empty,
                        Unique = raw.Unique ?? string.Empty,
                        Secondary = raw.Secondary ?? string.Empty,
                        Race = raw.Race ?? string.Empty,
                        Site = raw.Site ?? string.Empty,
                        Region = raw.Region ?? string.Empty,
                        Playable = raw.Playable ?? string.Empty,
                        GoldRing = raw.GoldRing ?? string.Empty,
                        GreaterItem = raw.GreaterItem ?? string.Empty,
                        MajorItem = raw.MajorItem ?? string.Empty,
                        MinorItem = raw.MinorItem ?? string.Empty,
                        Information = raw.Information ?? string.Empty,
                        Palantiri = raw.Palantiri ?? string.Empty,
                        Scroll = raw.Scroll ?? string.Empty,
                        Hoard = raw.Hoard ?? string.Empty,
                        Haven = raw.Haven ?? string.Empty,
                        Strikes = raw.Strikes?.ToString() ?? string.Empty,
                        Specific = raw.Specific ?? string.Empty,
                        Dreamcard = raw.Dreamcard,
                        Released = raw.Released,
                        IceErrata = raw.Ice_errata == true
                    };

                    foreach (string key in FilterKeys)
                    {
                        string val = card.GetAttribute(key);
                        card.SetAttribute(key, val);
                        RegisterFilterValue(key, val);
                    }

                    _cards.Add(card);
                    _cardsById[card.Id] = card;
                }
            }
        }

        private void InitializeFilterKeys()
        {
            _filterOptions.Clear();
            foreach (string key in FilterKeys)
            {
                _filterOptions[key] = [""];
            }
        }

        private void RegisterFilterValue(string key, string value)
        {
            if (string.IsNullOrEmpty(value) || !_filterOptions.TryGetValue(key, out var list))
            {
                return;
            }

            if (key != "Skill")
            {
                if (!list.Contains(value))
                {
                    list.Add(value);
                    if (list.Count > 2)
                    {
                        list.Sort(1, list.Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                    }
                }
            }
            else
            {
                string[] words = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    if (!list.Contains(word))
                    {
                        list.Add(word);
                        if (list.Count > 2)
                        {
                            list.Sort(1, list.Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                        }
                    }
                }
            }
        }
    }
}