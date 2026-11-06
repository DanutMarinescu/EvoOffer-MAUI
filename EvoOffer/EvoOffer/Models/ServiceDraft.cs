using System.Globalization;
using EvoOffer.Services;

namespace EvoOffer.Models;

/// <summary>Editable input kept separate from saved services until settings are saved.</summary>
public sealed class ServiceDraft
{
    public ServiceDraft(ServiceDefinition? source = null)
    {
        Name = source?.Name ?? string.Empty;
        UnitOfMeasurement = source?.UnitOfMeasurement ?? string.Empty;
        PriceText = source?.PriceExcludingVat.ToString("0.############################", LocalizationService.Culture)
            ?? string.Empty;
    }

    public string Name { get; set; }
    public string UnitOfMeasurement { get; set; }
    public string PriceText { get; set; }

    public bool HasNameError => string.IsNullOrWhiteSpace(Name);
    public bool HasUnitError => string.IsNullOrWhiteSpace(UnitOfMeasurement);
    public bool HasPriceError => !TryParsePrice(PriceText, out _);

    public bool TryBuild(out ServiceDefinition service)
    {
        service = null!;
        if (HasNameError || HasUnitError || !TryParsePrice(PriceText, out var price))
            return false;

        service = new ServiceDefinition
        {
            Name = Name.Trim(),
            UnitOfMeasurement = UnitOfMeasurement.Trim(),
            PriceExcludingVat = price
        };
        return true;
    }

    public static bool TryParsePrice(string? text, out decimal value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        value = 0m;
        if (string.IsNullOrEmpty(normalized))
            return false;
        var separator = normalized.IndexOf('.');
        if (separator >= 0 && normalized.Length - separator - 1 > 2)
            return false;
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value) || value < 0m)
            return false;

        // Decimal parsing can round very large fractional values. Preserve prices
        // exactly, including when input approaches the decimal precision limit.
        var canonical = normalized.TrimStart('0');
        if (canonical.StartsWith('.'))
            canonical = "0" + canonical;
        if (canonical.Contains('.'))
            canonical = canonical.TrimEnd('0').TrimEnd('.');
        if (canonical.Length == 0)
            canonical = "0";
        return canonical == value.ToString("0.############################", CultureInfo.InvariantCulture);
    }
}
