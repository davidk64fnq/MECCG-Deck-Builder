
namespace MECCG_Deck_Builder
{
    internal enum CardListField
    {
        name,
        image,
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

        public static readonly string[] TabList = {
            "Pool",
            "Resources",
            "Hazards",
            "Sideboard",
            "Sites"
        };
        public const string METW = "metw";
        public const string AppTitle = "MECCG Deck Builder";
        public const string CardnumCardsFile = "cards-dc.json";
        public const string CardnumCardsURL = "https://github.com/rezwits/cardnum/blob/master/fdata/cards-dc.json?raw=true";
        public const string CardnumSetsFile = "sets-dc.json";
        public const string CardnumSetsURL = "https://github.com/rezwits/cardnum/blob/master/fdata/sets-dc.json?raw=true";

        public const string poolFileSuffix = "_pool";
        public const string resourceFileSuffix = "_resource";
        public const string hazardFileSuffix = "_hazard";
        public const string sideboardFileSuffix = "_sideboard";
        public const string siteFileSuffix = "_site";

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
