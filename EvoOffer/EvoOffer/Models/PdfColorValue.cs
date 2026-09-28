namespace EvoOffer.Models;

/// <summary>Opaque template colors stored in a consistent, UI-independent format.</summary>
public static class PdfColorValue
{
    public const string DefaultPrimary = "#08254B";
    public const string DefaultSecondary = "#59616A";
    public const string DefaultText = "#17202B";

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var hex = input.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];
        if (hex.Length is not (3 or 6) || !hex.All(Uri.IsHexDigit))
            return false;

        if (hex.Length == 3)
            hex = string.Concat(hex.SelectMany(character => new[] { character, character }));
        normalized = "#" + hex.ToUpperInvariant();
        return true;
    }

    public static string Normalize(string? input, string fallback)
    {
        if (TryNormalize(input, out var normalized))
            return normalized;
        if (TryNormalize(fallback, out normalized))
            return normalized;
        throw new ArgumentException("The fallback must be a three- or six-digit hexadecimal color.", nameof(fallback));
    }
}
