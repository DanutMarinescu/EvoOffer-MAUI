using EvoOffer.Services;

namespace EvoOffer.Models;

/// <summary>Stable identifiers shared by saved settings, the picker and the PDF renderer.</summary>
public static class OfferPdfTemplates
{
    public const string Classic = "classic";
    public const string ClassicNoIssuerName = "classic-no-issuer-name";
    public const string Modern = "modern";
    public const string ModernNoIssuerName = "modern-no-issuer-name";
    public const string Minimal = "minimal";
    public const string MinimalNoIssuerName = "minimal-no-issuer-name";

    public static IReadOnlyList<OfferPdfTemplateDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new OfferPdfTemplateDefinition(Classic, "Template_ClassicName", "Template_ClassicDescription", "template_classic.png"),
        new OfferPdfTemplateDefinition(ClassicNoIssuerName, "Template_ClassicNoIssuerNameName", "Template_ClassicNoIssuerNameDescription", "template_classic_no_issuer_name.png"),
        new OfferPdfTemplateDefinition(Modern, "Template_ModernName", "Template_ModernDescription", "template_modern.png"),
        new OfferPdfTemplateDefinition(ModernNoIssuerName, "Template_ModernNoIssuerNameName", "Template_ModernNoIssuerNameDescription", "template_modern_no_issuer_name.png"),
        new OfferPdfTemplateDefinition(Minimal, "Template_MinimalName", "Template_MinimalDescription", "template_minimal.png"),
        new OfferPdfTemplateDefinition(MinimalNoIssuerName, "Template_MinimalNoIssuerNameName", "Template_MinimalNoIssuerNameDescription", "template_minimal_no_issuer_name.png")
    });

    public static string Normalize(string? id) => id is Classic or ClassicNoIssuerName
        or Modern or ModernNoIssuerName or Minimal or MinimalNoIssuerName ? id : Classic;

    public static string GetBaseTemplateId(string? id) => Normalize(id) switch
    {
        ClassicNoIssuerName => Classic,
        ModernNoIssuerName => Modern,
        MinimalNoIssuerName => Minimal,
        var templateId => templateId
    };

    public static bool HidesIssuerName(string? id) => id is ClassicNoIssuerName or ModernNoIssuerName or MinimalNoIssuerName;

    public static OfferPdfTemplateDefinition Find(string? id) => All.First(template => template.Id == Normalize(id));
}

public sealed record OfferPdfTemplateDefinition(string Id, string NameResourceKey, string DescriptionResourceKey, string PreviewImage)
{
    public string Name => LocalizationService.Get(NameResourceKey);
    public string Description => LocalizationService.Get(DescriptionResourceKey);

    public override string ToString() => Name;
}
