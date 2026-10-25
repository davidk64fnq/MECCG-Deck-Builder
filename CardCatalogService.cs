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

                    var (canonicalRace, subrace) = NormalizeRace(raw.Race);

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
        /// Normalizes raw Cardnum race strings into clean, canonical singular terms.
        /// Retains Tolkien lore sub-races for Elf and Dwarf while mapping creature splits and composite strings.
        /// </summary>
        private static (string CanonicalRace, string Subrace) NormalizeRace(string rawRace)
        {
            if (string.IsNullOrWhiteSpace(rawRace))
            {
                return (string.Empty, string.Empty);
            }

            string trimmed = rawRace.Trim();
            string lower = trimmed.ToLowerInvariant();

            // 1. Character Lore Doublets & Sub-races
            if (lower is "elf" or "elf elves") return ("Elf", string.Empty);
            if (trimmed.EndsWith("Elf", StringComparison.OrdinalIgnoreCase))
            {
                return ("Elf", trimmed);
            }

            if (lower is "dwarf" or "dwarf dwarves") return ("Dwarf", string.Empty);
            if (trimmed.EndsWith("Dwarf", StringComparison.OrdinalIgnoreCase))
            {
                return ("Dwarf", trimmed);
            }

            if (lower is "dúnadan" or "dúnadan dúnedain" or "dunadan" or "dunadan dunedain")
            {
                return ("Dúnadan", string.Empty);
            }

            // --- MULTI-SPECIES CREATURES (Must go before single species checks!) ---
            if (lower is "animals man men bears") return ("Animal; Man; Bear", "Beorning");
            if (lower is "orcs man men") return ("Orc; Man", "Half-orc");
            if (lower is "half-trolls" or "trolls men") return ("Troll; Man", "Half-troll");

            // 2. Creature Species Normalization (Strict matching to prevent substring collisions)
            if (lower is "drake" or "drakes" || lower.StartsWith("drake ") || lower.EndsWith("-drake"))
            {
                string sub = trimmed.Replace("Drake", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("-drake", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();
                return ("Drake", !string.IsNullOrEmpty(sub) ? sub : string.Empty);
            }

            if (lower is "troll" or "trolls" || lower.EndsWith(" troll") || lower.EndsWith(" trolls"))
            {
                string sub = trimmed.Replace("Trolls", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("Troll", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();
                return ("Troll", !string.IsNullOrEmpty(sub) ? sub : string.Empty);
            }

            if (lower is "orc" or "orcs" || lower.EndsWith(" orc") || lower.EndsWith(" orcs"))
            {
                string sub = trimmed.Replace("Orcs", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("Orc", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();
                return ("Orc", !string.IsNullOrEmpty(sub) ? sub : string.Empty);
            }

            if (lower is "spider" or "spiders" or "spawn spider" or "spider spawn")
            {
                return ("Spider", string.Empty);
            }

            if (lower is "wolf" or "wolves" or "wolf wolves")
            {
                return ("Wolf", string.Empty);
            }

            if (lower is "animal" or "animals" or "animals vermin" || lower.StartsWith("animal "))
            {
                string sub = trimmed.Replace("Animals", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("Animal", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();
                return ("Animal", !string.IsNullOrEmpty(sub) ? sub : string.Empty);
            }

            if (lower.StartsWith("dragon"))
            {
                string sub = trimmed.Replace("Dragon", "", StringComparison.OrdinalIgnoreCase).Trim();
                return ("Dragon", !string.IsNullOrEmpty(sub) ? sub : string.Empty);
            }

            if (lower is "undead" or "special undead") return ("Undead", string.Empty);
            if (lower is "balrog" or "balrog spawn") return ("Balrog", string.Empty);
            if (lower is "giant" or "giants") return ("Giant", string.Empty);

            // Ent (Characters/Allies/Factions) vs Awakened Plant (Hazard Creatures)
            if (lower is "awakened plant" or "awakened plant ent")
            {
                return ("Awakened Plant", "Ent");
            }

            if (lower is "ent" or "ents")
            {
                return ("Ent", string.Empty);
            }

            if (lower is "nazgûl" or "nazgûl female" or "nazgul" or "nazgul female") return ("Nazgûl", string.Empty);
            if (lower is "ringwraith" or "ringwraith female") return ("Ringwraith", string.Empty);
            if (lower is "pûkel-creature" or "pukel-creature") return ("Pûkel-creature", string.Empty);
            if (lower is "man" or "man men") return ("Man", string.Empty);

            // 3. Unmodified non-creatures (Environment, Dark Enchantment, Armor, Weapon, etc.)
            return (trimmed, string.Empty);
        }

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

            if (key == "Skill")
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
            else if (value.Contains(';'))
            {
                // Split multi-values like "Animal; Man; Bear" so each appears individually in the dropdown
                string[] parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string part in parts)
                {
                    if (!list.Contains(part))
                    {
                        list.Add(part);
                        if (list.Count > 2)
                        {
                            list.Sort(1, list.Count - 1, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
                        }
                    }
                }
            }
            else
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
        }
    }
}