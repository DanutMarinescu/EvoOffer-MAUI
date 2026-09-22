using System.Globalization;

namespace EvoOffer.Models;

public static class QuantityValue
{
    public const decimal Minimum = 1m;
    public const decimal Maximum = 999999m;
    public const string ValidationMessage = "Enter a quantity between 1 and 999,999.";

    public static bool TryParse(string? text, out decimal quantity)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        var parsed = decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out quantity) ||
                     decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out quantity);
        return parsed && quantity >= Minimum && quantity <= Maximum;
    }

    public static string Format(decimal quantity) =>
        quantity.ToString("0.############################", CultureInfo.CurrentCulture);
}
