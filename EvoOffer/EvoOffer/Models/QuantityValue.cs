using System.Globalization;
using EvoOffer.Services;

namespace EvoOffer.Models;

public static class QuantityValue
{
    public const decimal Minimum = 1m;
    public const decimal Maximum = 999999m;
    public static string ValidationMessage => LocalizationService.Get("QuantityValidation");

    public static bool TryParse(string? text, out decimal quantity)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        var parsed = decimal.TryParse(text, styles, LocalizationService.Culture, out quantity) ||
                     decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out quantity);
        return parsed && quantity >= Minimum && quantity <= Maximum;
    }

    public static string Format(decimal quantity) =>
        quantity.ToString("0.############################", LocalizationService.Culture);
}
