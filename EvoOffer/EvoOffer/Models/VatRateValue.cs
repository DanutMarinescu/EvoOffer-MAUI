using System.Globalization;

namespace EvoOffer.Models;

public static class VatRateValue
{
    public const decimal Default = 0m;
    public const string ValidationMessage = "Enter a VAT percentage from 0 to 100, with up to two decimal places.";

    public static bool IsValid(decimal value) =>
        value >= 0m && value <= 100m && decimal.Round(value, 2) == value;

    public static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out decimal value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        value = 0m;
        if (normalized is null)
            return false;
        var separator = normalized.IndexOf('.');
        if (separator >= 0 && normalized.Length - separator - 1 > 2)
            return false;
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value) && IsValid(value);
    }
}
