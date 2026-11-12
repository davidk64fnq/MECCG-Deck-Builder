using System;
using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    public sealed class DeckGenerationRuleSet
    {
        public string DeckTitle { get; set; } = "Generated Deck";
        public string RuleSetType { get; set; } = "SequentialRules";
        public string Version { get; set; } = "1.0";
        public string ComponentType { get; set; } = string.Empty; // "Company", "Theater", "Strategy", "Hazards"
        public List<DeckGenerationRule> Rules { get; set; } = [];
    }

    public sealed class DeckGenerationChoice
    {
        public string Name { get; set; } = string.Empty;
        public string DeckTitle { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public List<DeckGenerationRule> Rules { get; set; } = [];
    }

    public sealed class DeckGenerationRule
    {
        public string RuleType { get; set; } = "FilterQuery";
        public string Name { get; set; } = string.Empty;
        public string SaveAs { get; set; } = string.Empty;
        public string Section { get; set; } = "Resources";
        public int Count { get; set; } = 1;

        /// <summary>
        /// Sampling strategy: "Variety" (default), "Focused" (or "Playset"), or "Uniform" (or "Random").
        /// </summary>
        public string Distribution { get; set; } = "Variety";

        public List<string> Sets { get; set; } = [];
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DeckGenerationRule Fallback { get; set; }
        public List<DeckGenerationChoice> Choices { get; set; } = [];

        // Geographic Theater Properties
        /// <summary>
        /// Geographic theater movement mode: "Starter Only" (pure Haven hub, 0 regions) or "Starter + Region" (contiguous roaming cluster &lt;= 4).
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Specific Haven anchor ("Rivendell", "Lórien", "Grey Havens", "Edhellond") or "Random".
        /// </summary>
        public string Haven { get; set; } = string.Empty;

        /// <summary>
        /// Optional Middle-earth terrain filter constraining the geographic cluster and adventure sites (e.g. "Wilderness; Border-land", "!=Dark-domain").
        /// </summary>
        public string RegionType { get; set; } = string.Empty;

        /// <summary>
        /// Optional site hold classification filter constraining adventure sites (e.g. "Free-hold; Border-hold", "!=Dark-hold; !=Shadow-hold").
        /// </summary>
        public string SiteType { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of regions in the contiguous cluster (defaults to 4 for single-turn transit, but can be higher for larger multi-turn theaters).
        /// </summary>
        public int? RegionCount { get; set; }

        /// <summary>
        /// Number of adventure sites to draft in the theater.
        /// </summary>
        public int? SiteCount { get; set; }

        /// <summary>
        /// In "Starter + Region" mode, uses BFS pathfinding on Middle-earth to include overland transit region cards from Rhudaur (Rivendell).
        /// </summary>
        public bool? IncludeOverlandPath { get; set; }

        /// <summary>
        /// If true, automatically adds intermediate stepping-stone havens for Haven-to-Haven starter movement (e.g. Lórien for Edhellond). Default is true.
        /// </summary>
        public bool? IncludeIntermediateHavens { get; set; }
    }

    public sealed class DeckRuleExecutionLog
    {
        public string RuleName { get; init; } = string.Empty;
        public string Section { get; init; } = string.Empty;
        public int RequestedCount { get; init; }
        public int AddedCount { get; init; }
        public bool IsShortfall => AddedCount < RequestedCount;
        public bool IsFallback { get; init; }
        public List<string> AddedCards { get; init; } = [];
        public string Note { get; init; } = string.Empty;
    }

    public sealed class DeckGenerationResult
    {
        public Deck Deck { get; init; } = new();
        public List<DeckRuleExecutionLog> Logs { get; init; } = [];
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Formats a human-readable execution trace breaking down rule-by-rule card drafting and fallback routing.
        /// </summary>
        public string GenerateExecutionTrace(string ruleSetTitle = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("MECCG DECK BUILDER - PROCEDURAL GENERATION EXECUTION TRACE");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Deck Title: \"{Deck.Title}\"");
            if (!string.IsNullOrWhiteSpace(ruleSetTitle))
            {
                sb.AppendLine($"Rule Set:   \"{ruleSetTitle}\"");
            }
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            sb.AppendLine("--- EXECUTION LOG BY RULE ---");
            sb.AppendLine();

            int step = 1;
            for (int i = 0; i < Logs.Count; i++)
            {
                var log = Logs[i];

                if (log.RuleName.StartsWith("[Branch Selected]"))
                {
                    sb.AppendLine($"[Step {step++}] {log.RuleName}");
                    sb.AppendLine();
                    continue;
                }

                string prefix = log.IsFallback ? "  └── [Fallback] " : $"[Step {step++}] ";
                sb.AppendLine($"{prefix}{log.RuleName}");
                sb.AppendLine($"      Target Section:  {log.Section}");
                sb.AppendLine($"      Requested Count: {log.RequestedCount}");
                sb.AppendLine($"      Added Count:     {log.AddedCount}");

                if (!string.IsNullOrWhiteSpace(log.Note))
                {
                    sb.AppendLine($"      Details:         {log.Note}");
                }

                if (log.AddedCards != null && log.AddedCards.Count > 0)
                {
                    sb.AppendLine($"      Drafted Cards ({log.AddedCards.Count}):");
                    foreach (var card in log.AddedCards)
                    {
                        sb.AppendLine($"        • {card}");
                    }
                }
                else
                {
                    sb.AppendLine("      Drafted Cards:   (None)");
                }

                if (log.IsShortfall && !log.IsFallback)
                {
                    sb.AppendLine($"      ! SHORTFALL DETECTED: Needed {log.RequestedCount}, only obtained {log.AddedCount}.");
                }

                sb.AppendLine();
            }

            sb.AppendLine("--- FINAL DECK TOTALS ---");
            sb.AppendLine($"Pool:      {Deck.Pool.Count}");
            sb.AppendLine($"Resources: {Deck.Resources.Count} ({Deck.CountCharactersInResources()} Characters)");
            sb.AppendLine($"Hazards:   {Deck.Hazards.Count}");
            sb.AppendLine($"Sideboard: {Deck.Sideboard.Count}");
            sb.AppendLine($"Sites:     {Deck.Sites.Count}");
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }
    }
}