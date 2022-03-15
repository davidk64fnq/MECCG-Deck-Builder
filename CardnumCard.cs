

namespace MECCG_Deck_Builder
{
    public class CardnumCard
    {
        public object this[string propertyName]
        {
            get { return this.GetType().GetProperty(propertyName).GetValue(this, null); }
            set { this.GetType().GetProperty(propertyName).SetValue(this, value, null); }
        }

        public string Set { get; set; }
        public string Primary { get; set; }
        public string Alignment { get; set; }
        public string MEID { get; set; }
        public string Artist { get; set; }
        public string Rarity { get; set; }
        public string Precise { get; set; }
        public string NameEN { get; set; }
        public string NameDU { get; set; }
        public string NameSP { get; set; }
        public string NameFN { get; set; }
        public string NameFR { get; set; }
        public string NameGR { get; set; }
        public string NameIT { get; set; }
        public string NameJP { get; set; }
        public string ImageName { get; set; }
        public string Text { get; set; }
        public string Skill { get; set; }
        public string MPs { get; set; }
        public string Mind  { get; set; }
        public string Direct { get; set; }
        public int? General { get; set; }
        public int? Prowess { get; set; }
        public string Body { get; set; }
        public string Corruption { get; set; }
        public string Home { get; set; }
        public string Unique { get; set; }
        public string Secondary { get; set; }
        public string Race { get; set; }
        public string RWMPs { get; set; }
        public string Site { get; set; }
        public string Path { get; set; }
        public string Region { get; set; }
        public string RPath { get; set; }
        public string Playable { get; set; }
        public string GoldRing { get; set; }
        public string GreaterItem { get; set; }
        public string MajorItem { get; set; }
        public string MinorItem { get; set; }
        public string Information { get; set; }
        public string Palantiri { get; set; }
        public string Scroll { get; set; }
        public string Hoard { get; set; }
        public string Gear { get; set; }
        public string Non { get; set; }
        public string Haven { get; set; }
        public int? Stage { get; set; }
        public int? Strikes { get; set; }
        public string Code { get; set; }
        public int CodeFR { get; set; }
        public int CodeGR { get; set; }
        public int CodeSP { get; set; }
        public string CodeJP { get; set; }
        public string Specific { get; set; }
        public string FullCode { get; set; }
        public string GccgAlign { get; set; }
        public string GccgSet { get; set; }
        public string Normalizedtitle { get; set; }
        public string DCpath { get; set; }
        public bool Dreamcard { get; set; }
        public bool Released { get; set; }
        public bool? Erratum { get; set; }
        public bool? Ice_errata { get; set; }
        public bool Extras { get; set; }
    }


}
