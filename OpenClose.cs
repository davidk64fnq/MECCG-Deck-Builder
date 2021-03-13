using System;
using System.Collections.Generic;
using System.Text;

namespace MECCG_Deck_Builder
{
    class OpenClose
    {
        public string CurrentDeckTitle { get; set; }
        public List<int> masterList = new List<int>();
        public List<int> poolList = new List<int>();
        public List<int> resourceList = new List<int>();
        public List<int> hazardList = new List<int>();
        public List<int> sideboardList = new List<int>();
        public List<int> siteList = new List<int>();
        public List<string> setList = new List<string>();
    }
}
