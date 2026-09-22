using EvoOffer.Models;
using QuestPDF.Fluent;

namespace EvoOffer.Services;

/// <summary>Renders the reusable offer template without native UI or file-picker dependencies.</summary>
public sealed class OfferPdfService : IOfferPdfService
{
    private readonly OfferPdfOptions _defaults;

    public OfferPdfService(OfferPdfOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        defaults.Validate();
        _defaults = defaults;
    }

    public bool IsSupported => !OperatingSystem.IsMacCatalyst()
        && (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();

    public byte[] Generate(OfferPdfData offer, OfferPdfOptions? options = null)
    {
        using var output = new MemoryStream();
        Generate(offer, output, options);
        return output.ToArray();
    }

    public void Generate(OfferPdfData offer, Stream output, OfferPdfOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
            throw new ArgumentException("The PDF output stream must be writable.", nameof(output));

        var configuration = options ?? _defaults;
        configuration.Validate();
        if (!IsSupported)
            throw new PlatformNotSupportedException(
                "QuestPDF does not support iOS, Android or Mac Catalyst. Generate PDFs on Windows or in a desktop/server .NET host.");

        var logo = offer.LogoPath is null ? null : File.ReadAllBytes(offer.LogoPath);
        new OfferPdfTemplate(offer, configuration, logo).GeneratePdf(output);
    }
}
