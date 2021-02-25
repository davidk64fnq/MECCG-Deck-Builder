
namespace MECCG_Deck_Builder
{
    internal enum CardListField
    {
        name,
        set,
        id
    }

    internal enum DeckTab
    {
        pool,
        resources,
        hazards,
        sideboard,
        sites
    }
    static class Constants
    {
        public const string METW = "metw";
        public const string METD = "metd";
        public const string MEDM = "medm";
        public const string MELE = "mele";
        public const string MEAS = "meas";
        public const string MEWH = "mewh";
        public const string MEBA = "meba";

        public const string poolFileSuffix = "_pool.json";
        public const string resourceFileSuffix = "_resource.json";
        public const string hazardFileSuffix = "_hazard.json";
        public const string sideboardFileSuffix = "_sideboard.json";
        public const string siteFileSuffix = "_site.json";

        public const string Move = "Move";
        public const string Copy = "Copy";
        public const string Delete = "Delete";
        public const string Pool = "Pool";
        public const string Resource = "Resource";
        public const string Hazard = "Hazard";
        public const string Sideboard = "Sideboard";
        public const string Site = "Site";

        public const string senderMaster = "Form1";
        public const string senderPool = "TabPagePool";
        public const string senderResource = "TabPageResources";
        public const string senderHazard = "TabPageHazards";
        public const string senderSideboard = "TabPageSideboard";
        public const string senderSite = "TabPageSites";
    }

}
