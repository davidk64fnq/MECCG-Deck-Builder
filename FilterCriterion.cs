namespace MECCG_Deck_Builder
{
    public enum FilterType
    {
        Cardnum,
        Custom
    }

    public sealed record FilterCriterion(FilterType Type, string Key, string Value)
    {
        public bool IsActive => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Value);
    }
}