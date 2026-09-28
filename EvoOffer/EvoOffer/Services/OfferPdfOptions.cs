using EvoOffer.Models;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Color = QuestPDF.Infrastructure.Color;

namespace EvoOffer.Services;

/// <summary>Shared defaults or per-document overrides. Measurements are in points (72 points = 1 inch).</summary>
public sealed record OfferPdfOptions
{
    /// <summary>Null uses the template saved in the offer snapshot.</summary>
    public string? TemplateId { get; init; }
    public PageSize PageSize { get; init; } = PageSizes.A4;
    public float Margin { get; init; } = 30;
    public string FontFamily { get; init; } = "Lato";
    public float FontSize { get; init; } = 10;
    /// <summary>Null uses the primary color saved in the offer snapshot.</summary>
    public Color? AccentColor { get; init; }
    /// <summary>Null uses the secondary color saved in the offer snapshot.</summary>
    public Color? SecondaryColor { get; init; }
    /// <summary>Null uses the text color saved in the offer snapshot.</summary>
    public Color? TextColor { get; init; }
    /// <summary>Null uses the title in the offer's saved language.</summary>
    public string? Title { get; init; }
    public string Tagline { get; init; } = string.Empty;
    /// <summary>Omitted unless the caller explicitly supplies an expiry date.</summary>
    public DateOnly? ValidUntil { get; init; }
    /// <summary>Null uses the localized closing note; empty hides it.</summary>
    public string? FooterText { get; init; }
    public bool ShowPageNumbers { get; init; } = true;

    internal void Validate()
    {
        if (TemplateId is not null && TemplateId is not
            (OfferPdfTemplates.Classic or OfferPdfTemplates.Modern or OfferPdfTemplates.Minimal))
            throw new ArgumentException("Choose a supported PDF template.", nameof(TemplateId));
        ArgumentNullException.ThrowIfNull(PageSize);
        if (!float.IsFinite(PageSize.Width) || !float.IsFinite(PageSize.Height)
            || PageSize.Width <= 0 || PageSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page dimensions must be positive and finite.");
        if (!float.IsFinite(Margin) || Margin < 0 || Margin * 2 >= Math.Min(PageSize.Width, PageSize.Height))
            throw new ArgumentOutOfRangeException(nameof(Margin), "Margins must leave space for the document content.");
        if (!float.IsFinite(FontSize) || FontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(FontSize), "Font size must be positive and finite.");
        ArgumentException.ThrowIfNullOrWhiteSpace(FontFamily);
        if (Title is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(Title);
    }
}
