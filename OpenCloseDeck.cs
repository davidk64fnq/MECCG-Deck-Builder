
using System.Collections.Generic;

namespace MECCG_Deck_Builder
{
    class OpenCloseDeck
    {
        public string CurrentDeckTitle { get; set; }
        public List<string[]> poolList = new List<string[]>();
        public List<string[]> resourceList = new List<string[]>();
        public List<string[]> hazardList = new List<string[]>();
        public List<string[]> sideboardList = new List<string[]>();
        public List<string[]> siteList = new List<string[]>();
        public List<string> setList = new List<string>();
    }
}
