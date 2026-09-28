using System.Globalization;
using EvoOffer.Services;

namespace EvoOffer.Models;

public static class VatRateValue
{
    public const decimal Default = 0m;
    public static string ValidationMessage => LocalizationService.Get("VatRateValidation");

    public static bool IsValid(decimal value) =>
        value >= 0m && value <= 100m && decimal.Round(value, 2) == value;

    public static string Format(decimal value) => value.ToString("0.##", LocalizationService.Culture);

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
