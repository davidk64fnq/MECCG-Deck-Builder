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
                case "ImportCardnumCardInfo1":
                    return $"ImportCardnumCardInfo: Unable to retrieve card information from Cardnum site using this URL:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumCardsURL}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Using local copy \"{Constants.CardnumCardsFile}\" instead which may not be up-todate.";
                case "ImportCardnumCardInfo2":
                    return $"ImportCardnumCardInfo: Unable to retrieve card information from Cardnum site using this URL:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumCardsURL}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Unable to retrieve card information from local copy:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumCardsFile}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Using Middle Earth The Wizards \"Adrazar\" card information only.";
                case "ImportCardnumSetInfo1":
                    return $"ImportCardnumSetInfo: Unable to retrieve set information from Cardnum site using this URL:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumSetsURL}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Using local copy \"{Constants.CardnumSetsFile}\" instead which may not be up-todate.";
                case "ImportCardnumSetInfo2":
                    return $"ImportCardnumSetInfo: Unable to retrieve set information from Cardnum site using this URL:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumSetsURL}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Unable to retrieve set information from local copy:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumSetsFile}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Using Middle Earth The Wizards set information only.";
                default:
                    break;
            }
            return $"GetMsgBoxText: Unable to find message box text for \"{searchString}\", something has gone astray!";
        }
    }
}
