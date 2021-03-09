using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MECCG_Deck_Builder
{
    internal class Utilities
    {
        private List<CardnumCard> CardnumItems;
        public void StripDCcardnumData()
        {
            string[] cardSets = { "metw", "metd", "medm", "mele", "meas", "mewh", "meba" };
            using StreamReader r = new StreamReader("Cardnum.json");
            string jsonInput = r.ReadToEnd();
            CardnumItems = JsonConvert.DeserializeObject<List<CardnumCard>>(jsonInput);
            string jsonOutput = "[\n";
            int index = 0;
            foreach (var item in CardnumItems)
            {
                if (cardSets.Contains(item.Set.ToLower()) && item.MEID != "")
                {
                    jsonOutput += JsonConvert.SerializeObject(item, Formatting.Indented);
                    if (index++ < CardnumItems.Count - 1)
                    {
                        jsonOutput += ",\n";
                    }
                }
            }
            jsonOutput += "\n]\n";
            File.WriteAllText("Cardnum_DCstripped.json", jsonOutput);
        }
    }
}
