using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Color = QuestPDF.Infrastructure.Color;

namespace EvoOffer.Services;

/// <summary>Shared defaults or per-document overrides. Measurements are in points (72 points = 1 inch).</summary>
public sealed record OfferPdfOptions
{
    public PageSize PageSize { get; init; } = PageSizes.A4;
    public float Margin { get; init; } = 30;
    public string FontFamily { get; init; } = "Lato";
    public float FontSize { get; init; } = 10;
    public Color AccentColor { get; init; } = Color.FromHex("#147EF0");
    public string Title { get; init; } = "Offer";
    public string FooterText { get; init; } = string.Empty;
    public bool ShowPageNumbers { get; init; } = true;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(PageSize);
        if (!float.IsFinite(PageSize.Width) || !float.IsFinite(PageSize.Height)
            || PageSize.Width <= 0 || PageSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page dimensions must be positive and finite.");
        if (!float.IsFinite(Margin) || Margin < 0 || Margin * 2 >= Math.Min(PageSize.Width, PageSize.Height))
            throw new ArgumentOutOfRangeException(nameof(Margin), "Margins must leave space for the document content.");
        if (!float.IsFinite(FontSize) || FontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(FontSize), "Font size must be positive and finite.");
        ArgumentException.ThrowIfNullOrWhiteSpace(FontFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
    }
}
