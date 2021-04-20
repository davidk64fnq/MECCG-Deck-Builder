using System;
using System.Collections.Generic;
using System.Text;

namespace MECCG_Deck_Builder
{
    class OpenCloseFilter
    {
        public List<SortedDictionary<string, string>> cards = new List<SortedDictionary<string, string>>();
        public List<List<string>> filters = new List<List<string>>(); // each filter is keyName at index 0 and keyValue(s) from 1..Count
    }
}
