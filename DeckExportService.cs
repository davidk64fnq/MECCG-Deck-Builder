using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MECCG_Deck_Builder
{
    public static class DeckExportService
    {
        public static void ExportToText(Deck deck, string filePath)
        {
            var sb = new StringBuilder();

            foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
            {
                sb.AppendLine(sectionType.ToString());
                sb.AppendLine();
                foreach (var card in deck.GetSection(sectionType))
                {
                    sb.AppendLine(card.Name);
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportToPlayMeccg(Deck deck, string filePath)
        {
            var sb = new StringBuilder();
            var ansiEncoding = Encoding.GetEncoding(1252);

            // Pool
            sb.AppendLine("####");
            sb.AppendLine("Pool");
            sb.AppendLine("####").AppendLine();
            foreach (var card in deck.Pool)
            {
                sb.AppendLine(FormatPlayMeccgCardName(card.Name));
            }
            sb.AppendLine();

            // Deck (Resources + Hazards combined)
            sb.AppendLine("####");
            sb.AppendLine("Deck");
            sb.AppendLine("####").AppendLine();
            foreach (var card in deck.Resources)
            {
                sb.AppendLine(FormatPlayMeccgCardName(card.Name));
            }
            foreach (var card in deck.Hazards)
            {
                sb.AppendLine(FormatPlayMeccgCardName(card.Name));
            }
            sb.AppendLine();

            // Sideboard
            sb.AppendLine("####");
            sb.AppendLine("Sideboard");
            sb.AppendLine("####").AppendLine();
            foreach (var card in deck.Sideboard)
            {
                sb.AppendLine(FormatPlayMeccgCardName(card.Name));
            }
            sb.AppendLine();

            // Sites
            sb.AppendLine("####");
            sb.AppendLine("Sites");
            sb.AppendLine("####").AppendLine();
            foreach (var card in deck.Sites)
            {
                sb.AppendLine(FormatPlayMeccgCardName(card.Name));
            }
            sb.AppendLine();

            File.WriteAllText(filePath, sb.ToString(), ansiEncoding);
        }

        public static void ExportToCardnum(Deck deck, string filePath)
        {
            var sb = new StringBuilder();

            foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
            {
                sb.AppendLine(sectionType.ToString());
                sb.AppendLine();
                foreach (var card in deck.GetSection(sectionType))
                {
                    if (!string.IsNullOrEmpty(card.FullCode))
                    {
                        sb.AppendLine($"1 {card.FullCode}");
                    }
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportToArchive(Deck deck, string filePath)
        {
            string[] archiveCategories =
            [
                "Minor Item", "Major Item", "Greater Item", "Gold Ring Item", "Special Item",
                "Resource Short", "Resource Long", "Resource Permanent", "Ally", "Faction",
                "Creature Unique", "Creature", "Hazard Short", "Hazard Long", "Hazard Permanent",
                "Dunadan", "Dwarf", "Elf", "Hobbit", "Man (Wose)", "Wizard",
                "Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold", "Region"
            ];

            int totalArchived = 0;
            var cardDataByCategory = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (string cat in archiveCategories)
            {
                cardDataByCategory[cat] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (DeckSectionType sectionType in Enum.GetValues<DeckSectionType>())
            {
                foreach (var card in deck.GetSection(sectionType))
                {
                    string primary = card.Primary;
                    string secondary = TruncateSlash(card.Secondary);
                    string unique = card.Unique;
                    string race = card.Race;
                    string site = card.Site;

                    string categoryKey = GetArchiveCategoryKey(primary, secondary, unique, race, site);
                    if (string.IsNullOrEmpty(categoryKey) || !cardDataByCategory.TryGetValue(categoryKey, out var setsInCategory))
                    {
                        continue;
                    }

                    string setKey = card.Set;
                    if (!setsInCategory.TryGetValue(setKey, out var cardsInSet))
                    {
                        cardsInSet = [];
                        setsInCategory[setKey] = cardsInSet;
                    }

                    cardsInSet.Add(card.Name);
                    totalArchived++;
                }
            }

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine($"--- {filePath} ({totalArchived} cards) ---");
            writer.WriteLine();

            foreach (var categoryEntry in cardDataByCategory)
            {
                string categoryTitle = categoryEntry.Key;
                var setsInThisCategory = categoryEntry.Value;

                writer.WriteLine($"--- CATEGORY: {categoryTitle} ---");

                foreach (var setEntry in setsInThisCategory)
                {
                    string setTitle = setEntry.Key;
                    var cardsInThisSet = setEntry.Value;

                    if (cardsInThisSet.Count == 0)
                    {
                        continue;
                    }

                    writer.WriteLine($"\tSET: {setTitle} ({cardsInThisSet.Count})");
                    foreach (string cardName in cardsInThisSet)
                    {
                        writer.WriteLine($"\t\t{cardName}");
                    }
                }
                writer.WriteLine();
            }
        }

        public static void ExportToTts(IReadOnlyList<Card> cards, string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("{\n\t\"ObjectStates\": [\n\t\t{\n\t\t\t\"Name\": \"Deck\",\n");
            sb.Append(GetTtsTransform(3));
            sb.Append("\t\t\t\"DeckIDs\": [\n");

            for (int i = 0; i < cards.Count; i++)
            {
                sb.Append($"\t\t\t\t{(i + 1) * 100}");
                if (i != cards.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("\t\t\t],\n\t\t\t\"CustomDeck\": {\n");

            for (int i = 0; i < cards.Count; i++)
            {
                sb.Append(GetTtsCustomDeck(cards[i], i + 1, 4));
                if (i != cards.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("\t\t\t},\n\t\t\t\"ContainedObjects\": [\n");

            for (int i = 0; i < cards.Count; i++)
            {
                string escapedName = cards[i].Name.Replace("\"", "\\\"");
                sb.Append("\t\t\t\t{\n\t\t\t\t\t\"Name\": \"Card\",\n");
                sb.Append(GetTtsTransform(5));
                sb.Append("\t\t\t\t\t\"CustomDeck\": {\n");
                sb.Append(GetTtsCustomDeck(cards[i], i + 1, 6));
                sb.Append("\n\t\t\t\t\t},\n");
                sb.Append($"\t\t\t\t\t\"Nickname\": \"{escapedName}\",\n");
                sb.Append($"\t\t\t\t\t\"CardID\": \"{(i + 1) * 100}\"\n");
                sb.Append("\t\t\t\t}");
                if (i != cards.Count - 1) sb.Append(',');
                sb.Append('\n');
            }

            sb.Append("\t\t\t]\n\t\t}\n\t]\n}\n");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        #region HELPERS

        private static string FormatPlayMeccgCardName(string cardName)
        {
            if (cardName.Contains(" - "))
            {
                string cleanName = cardName.Replace("\"", "");
                int dashIndex = cleanName.IndexOf(" - ", StringComparison.Ordinal);
                string mainTitle = cleanName[..dashIndex].Trim();
                string subTitle = cleanName[(dashIndex + 3)..].Trim();
                return $"{mainTitle} ({subTitle})";
            }
            return cardName;
        }

        private static string TruncateSlash(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int slashIndex = value.IndexOf('/');
            return slashIndex >= 0 ? value[..slashIndex].Trim() : value.Trim();
        }

        private static string GetArchiveCategoryKey(string primary, string secondary, string unique, string race, string site)
        {
            if (primary.Equals("Resource", StringComparison.OrdinalIgnoreCase))
            {
                return secondary switch
                {
                    "Minor Item" => "Minor Item",
                    "Major Item" => "Major Item",
                    "Greater Item" => "Greater Item",
                    "Gold Ring Item" => "Gold Ring Item",
                    "Special Item" => "Special Item",
                    "Short-event" => "Resource Short",
                    "Long-event" => "Resource Long",
                    "Permanent-event" => "Resource Permanent",
                    "Ally" => "Ally",
                    "Faction" => "Faction",
                    _ => string.Empty
                };
            }

            if (secondary.Equals("Creature", StringComparison.OrdinalIgnoreCase))
            {
                return unique.Equals("unique", StringComparison.OrdinalIgnoreCase) ? "Creature Unique" : "Creature";
            }

            if (primary.Equals("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                return secondary switch
                {
                    "Short-event" => "Hazard Short",
                    "Long-event" => "Hazard Long",
                    "Permanent-event" => "Hazard Permanent",
                    _ => string.Empty
                };
            }

            if (primary.Equals("Character", StringComparison.OrdinalIgnoreCase))
            {
                if (race.Equals("Dúnadan", StringComparison.OrdinalIgnoreCase)) return "Dunadan";
                return race switch
                {
                    "Dwarf" => "Dwarf",
                    "Elf" => "Elf",
                    "Hobbit" => "Hobbit",
                    "Man" => "Man (Wose)",
                    "Wizard" => "Wizard",
                    _ => string.Empty
                };
            }

            if (primary.Equals("Site", StringComparison.OrdinalIgnoreCase))
            {
                return site switch
                {
                    "Free-hold" => "Free-hold",
                    "Border-hold" => "Border-hold",
                    "Ruins & Lairs" => "Ruins & Lairs",
                    "Shadow-hold" => "Shadow-hold",
                    "Dark-hold" => "Dark-hold",
                    "Region" => "Region",
                    _ => string.Empty
                };
            }

            if (primary.Equals("Region", StringComparison.OrdinalIgnoreCase))
            {
                return "Region";
            }

            return string.Empty;
        }

        private static string GetTtsTransform(int tabCount)
        {
            string tabs = new('\t', tabCount);
            return $"{tabs}\"Transform\": {{\n{tabs}\t\"RotY\": 180.0,\n{tabs}\t\"RotZ\": 180.0,\n{tabs}\t\"ScaleX\": 1.0,\n{tabs}\t\"ScaleY\": 1.0,\n{tabs}\t\"ScaleZ\": 1.0\n{tabs}}},\n";
        }

        private static string GetTtsCustomDeck(Card card, int index, int tabCount)
        {
            string tabs = new('\t', tabCount);
            return $"{tabs}\"{index}\": {{\n" +
                   $"{tabs}\t\"FaceURL\": \"https://cardnum.net/img/cards/{card.Set}/{card.ImageName}\",\n" +
                   $"{tabs}\t\"BackURL\": \"https://i.imgur.com/gUPyTI4.jpg\",\n" +
                   $"{tabs}\t\"BackIsHidden\": \"true\",\n" +
                   $"{tabs}\t\"NumWidth\": 1,\n" +
                   $"{tabs}\t\"NumHeight\": 1\n" +
                   $"{tabs}}}";
        }

        #endregion
    }
}