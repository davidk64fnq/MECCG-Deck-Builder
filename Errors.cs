using System;
using System.Collections.Generic;
using System.Text;

namespace MECCG_Deck_Builder
{
    static class Errors
    {
        internal static string GetMsgBoxText(string searchString)
        {
            switch (searchString)
            {
                case "ImportCardnumSetInfo":
                    return "ImportCardnumSetInfo: Unable to retrieve set information from Cardnum site. Using local copy instead which may not be up-todate.";
                default:
                    break;
            }
            return "GetMsgBoxText: Unable to find message box text";
        }
    }
}
