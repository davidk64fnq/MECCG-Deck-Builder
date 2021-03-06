
namespace MECCG_Deck_Builder
{
    internal enum CardListField
    {
        name,
        set,
        id
    }

    internal enum SaveType
    {
        TTS = 1,
        Cardnum,
        Text
    }

    static class Constants
    {
        public const string METW = "metw";
        public const string AppTitle = "MECCG Deck Builder";

        public const string poolFileSuffix = "_pool.json";
        public const string resourceFileSuffix = "_resource.json";
        public const string hazardFileSuffix = "_hazard.json";
        public const string sideboardFileSuffix = "_sideboard.json";
        public const string siteFileSuffix = "_site.json";

        public const string Move = "Move";
        public const string Copy = "Copy";
        public const string Delete = "Delete";
        public const string Form = "Form";
        public const string Pool = "Pool";
        public const string Resource = "Resource";
        public const string Hazard = "Hazard";
        public const string Sideboard = "Sideboard";
        public const string Site = "Site";
    }

}
