
using System;

namespace MECCG_Deck_Builder
{
    static class Messages
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
                case "ImportCardnumCardInfo3":
                    return $"ImportCardnumCardInfo: Unable to store card information in local copy:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumCardsFile}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Perhaps a permissions issue in \"{Environment.CurrentDirectory}\"";
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
                case "ImportCardnumSetInfo3":
                    return $"ImportCardnumCardInfo: Unable to store set information in local copy:{Environment.NewLine}{Environment.NewLine}" +
                        $"\"{Constants.CardnumSetsFile}\"{Environment.NewLine}{Environment.NewLine}" +
                        $"Perhaps a permissions issue in \"{Environment.CurrentDirectory}\"";
                case "SetCardKeyInfo1":
                    return $"SetCardKeyInfo: A card on Cardnum has no value for \"Secondary\" field.";
                case "SetCardKeyInfo2":
                    return $"SetCardKeyInfo: A card on Cardnum has an unexpected value for \"Secondary\" field.";
                default:
                    break;
            }
            return $"GetMsgBoxText: Unable to find message box text for \"{searchString}\", something has gone astray!";
        }
    }
}
