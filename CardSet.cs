namespace MECCG_Deck_Builder
{
    public sealed class CardSet
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;
        public int Position { get; init; }
        public bool Dreamcards { get; init; }
        public bool Released { get; init; }
    }
}