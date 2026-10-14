using System;
using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    public sealed class DeckGenerationRuleSet
    {
        public string DeckTitle { get; set; } = "Generated Deck";
        public string RuleSetType { get; set; } = "SequentialRules";
        public string Version { get; set; } = "1.0";
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
        public string Section { get; set; } = "Resources";
        public int Count { get; set; } = 1;
        public List<string> Sets { get; set; } = [];
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Sub-choices for RuleType == "RandomChoice" / "Choice" / "OneOf"
        public List<DeckGenerationChoice> Choices { get; set; } = [];
    }

    public sealed class DeckRuleExecutionLog
    {
        public string RuleName { get; init; } = string.Empty;
        public string Section { get; init; } = string.Empty;
        public int RequestedCount { get; init; }
        public int AddedCount { get; init; }
        public bool IsShortfall => AddedCount < RequestedCount;
    }

    public sealed class DeckGenerationResult
    {
        public Deck Deck { get; init; } = new();
        public List<DeckRuleExecutionLog> Logs { get; init; } = [];
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
    }
}