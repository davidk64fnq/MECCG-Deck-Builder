using System;
using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    public sealed class Card
    {
        public string Id { get; init; } = string.Empty;
        public string Set { get; init; } = string.Empty;
        public string FullCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string ImageName { get; init; } = string.Empty;

        // Cardnum attributes
        public string Primary { get; init; } = string.Empty;
        public string Alignment { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string Skill { get; init; } = string.Empty;
        public string MPs { get; init; } = string.Empty;
        public string Mind { get; init; } = string.Empty;
        public string Direct { get; init; } = string.Empty;
        public string Prowess { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Corruption { get; init; } = string.Empty;
        public string Home { get; init; } = string.Empty;
        public string Unique { get; init; } = string.Empty;
        public string Secondary { get; init; } = string.Empty;
        public string Race { get; init; } = string.Empty;
        public string Site { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Playable { get; init; } = string.Empty;
        public string GoldRing { get; init; } = string.Empty;
        public string GreaterItem { get; init; } = string.Empty;
        public string MajorItem { get; init; } = string.Empty;
        public string MinorItem { get; init; } = string.Empty;
        public string Information { get; init; } = string.Empty;
        public string Palantiri { get; init; } = string.Empty;
        public string Scroll { get; init; } = string.Empty;
        public string Hoard { get; init; } = string.Empty;
        public string Haven { get; init; } = string.Empty;
        public string Strikes { get; init; } = string.Empty;
        public string Specific { get; init; } = string.Empty;

        // Additional metadata
        public bool Dreamcard { get; init; }
        public bool Released { get; init; }
        public bool IceErrata { get; init; }

        // Dynamic attribute map for filter lookup
        private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

        public Card() { }

        public void SetAttribute(string key, string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _attributes[key] = value ?? string.Empty;
            }
        }

        public string GetAttribute(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (_attributes.TryGetValue(key, out string value))
            {
                return value;
            }

            return key.ToLowerInvariant() switch
            {
                "id" => Id,
                "set" => Set,
                "fullcode" => FullCode,
                "cardname" or "name" => Name,
                "text" => Text,
                "imagename" => ImageName,
                "primary" => Primary,
                "alignment" => Alignment,
                "artist" => Artist,
                "skill" => Skill,
                "mps" => MPs,
                "mind" => Mind,
                "direct" => Direct,
                "prowess" => Prowess,
                "body" => Body,
                "corruption" => Corruption,
                "home" => Home,
                "unique" => Unique,
                "secondary" => Secondary,
                "race" => Race,
                "site" => Site,
                "region" => Region,
                "playable" => Playable,
                "goldring" => GoldRing,
                "greateritem" => GreaterItem,
                "majoritem" => MajorItem,
                "minoritem" => MinorItem,
                "information" => Information,
                "palantiri" => Palantiri,
                "scroll" => Scroll,
                "hoard" => Hoard,
                "haven" => Haven,
                "strikes" => Strikes,
                "specific" => Specific,
                _ => string.Empty
            };
        }

        public bool TryGetAttribute(string key, out string value)
        {
            value = GetAttribute(key);
            return !string.IsNullOrEmpty(value);
        }

        public override string ToString() => $"{Name} ({Set})";
    }
}