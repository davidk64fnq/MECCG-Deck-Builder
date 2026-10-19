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
            "Corruption", "Home", "Unique", "Secondary", "Race", "Subrace", "Site", "Region", "Playable", "GoldRing", "GreaterItem",
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
            var keys = new List<string> { "" };
            var sortedKeys = new List<string>(_filterOptions.Keys);
            sortedKeys.Sort(StringComparer.Ordinal);
            keys.AddRange(sortedKeys);
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
            string filePath = StoragePathService.ResolveDataFilePath(Constants.CardnumSetsFile);
            List<CardnumSet> rawSets = null;

            if (File.Exists(filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    rawSets = JsonConvert.DeserializeObject<List<CardnumSet>>(json);
                }
                catch (Exception)
                {
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumSetInfo1"));
                }
            }

            if (rawSets == null || rawSets.Count == 0)
            {
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

            _sets.Clear();
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

        private async Task LoadCardsAsync(CancellationToken cancellationToken)
        {
            string filePath = StoragePathService.ResolveDataFilePath(Constants.CardnumCardsFile);
            List<CardnumCard> rawCards = null;

            if (File.Exists(filePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    rawCards = JsonConvert.DeserializeObject<List<CardnumCard>>(json);
                }
                catch (Exception)
                {
                    WarningOccurred?.Invoke(this, Messages.GetMsgBoxText("ImportCardnumCardInfo1"));
                }
            }

            if (rawCards == null || rawCards.Count == 0)
            {
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

            PopulateCards(rawCards);
        }

        private void PopulateCards(List<CardnumCard> rawCards)
        {
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

                    string rawRace = raw.Race ?? string.Empty;
                    string canonicalRace = rawRace;
                    string subrace = string.Empty;

                    if (rawRace.EndsWith("Elf", StringComparison.OrdinalIgnoreCase))
                    {
                        canonicalRace = "Elf";
                        if (!string.Equals(rawRace, "Elf", StringComparison.OrdinalIgnoreCase))
                        {
                            subrace = rawRace;
                        }
                    }
                    else if (rawRace.EndsWith("Dwarf", StringComparison.OrdinalIgnoreCase))
                    {
                        canonicalRace = "Dwarf";
                        if (!string.Equals(rawRace, "Dwarf", StringComparison.OrdinalIgnoreCase))
                        {
                            subrace = rawRace;
                        }
                    }

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
                        Race = canonicalRace,
                        Subrace = subrace,
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

        /// <summary>
        /// Explicit, user-initiated database update that downloads the latest Cardnum datasets
        /// from GitHub and saves them to the writable LocalAppData folder.
        /// </summary>
        public async Task<(bool Success, string Message)> UpdateDatabaseFromCardnumAsync(CancellationToken cancellationToken = default)
        {
            string setsJson;
            string cardsJson;

            try
            {
                setsJson = await s_httpClient.GetStringAsync(Constants.CardnumSetsURL, cancellationToken).ConfigureAwait(false);
                var testSets = JsonConvert.DeserializeObject<List<CardnumSet>>(setsJson);
                if (testSets == null || testSets.Count == 0)
                {
                    return (false, "Retrieved set data was empty or invalid.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to download sets from Cardnum:\n{ex.Message}");
            }

            try
            {
                cardsJson = await s_httpClient.GetStringAsync(Constants.CardnumCardsURL, cancellationToken).ConfigureAwait(false);
                var testCards = JsonConvert.DeserializeObject<List<CardnumCard>>(cardsJson);
                if (testCards == null || testCards.Count == 0)
                {
                    return (false, "Retrieved card data was empty or invalid.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to download cards from Cardnum:\n{ex.Message}");
            }

            string setsPath = StoragePathService.GetWritableDataFilePath(Constants.CardnumSetsFile);
            string cardsPath = StoragePathService.GetWritableDataFilePath(Constants.CardnumCardsFile);

            try
            {
                await File.WriteAllTextAsync(setsPath, setsJson, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(cardsPath, cardsJson, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save database files to local storage:\n{ex.Message}");
            }

            await LoadSetsAsync(cancellationToken).ConfigureAwait(false);
            await LoadCardsAsync(cancellationToken).ConfigureAwait(false);

            return (true, $"Database successfully updated and reloaded!\n\nSets: {_sets.Count}\nCards: {_cards.Count}");
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