using EvoOffer.Services;

namespace EvoOffer.Models;

/// <summary>Stable identifiers shared by saved settings, the picker and the PDF renderer.</summary>
public static class OfferPdfTemplates
{
    public const string Classic = "classic";
    public const string Modern = "modern";
    public const string Minimal = "minimal";

    public static IReadOnlyList<OfferPdfTemplateDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new OfferPdfTemplateDefinition(Classic, "Template_ClassicName", "Template_ClassicDescription", "template_classic.png"),
        new OfferPdfTemplateDefinition(Modern, "Template_ModernName", "Template_ModernDescription", "template_modern.png"),
        new OfferPdfTemplateDefinition(Minimal, "Template_MinimalName", "Template_MinimalDescription", "template_minimal.png")
    });

    public static string Normalize(string? id) => id is Classic or Modern or Minimal ? id : Classic;

    public static OfferPdfTemplateDefinition Find(string? id) => All.First(template => template.Id == Normalize(id));
}

public sealed record OfferPdfTemplateDefinition(string Id, string NameResourceKey, string DescriptionResourceKey, string PreviewImage)
{
    public string Name => LocalizationService.Get(NameResourceKey);
    public string Description => LocalizationService.Get(DescriptionResourceKey);

    public override string ToString() => Name;
}
