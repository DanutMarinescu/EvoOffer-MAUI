using System.Globalization;
using EvoOffer.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace EvoOffer.Services;

/// <summary>Owns the offer PDF layout; no native UI or file-picker dependencies.</summary>
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

        CreateDocument(offer, configuration).GeneratePdf(output);
    }

    private static Document CreateDocument(OfferPdfData offer, OfferPdfOptions options) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(options.PageSize);
            page.Margin(options.Margin);
            page.DefaultTextStyle(style => style.FontFamily(options.FontFamily).FontSize(options.FontSize));
            page.Header().PaddingBottom(16).Text(options.Title).FontSize(options.FontSize + 14)
                .SemiBold().FontColor(options.AccentColor);

            page.Content().Column(column =>
            {
                column.Spacing(14);
                if (!string.IsNullOrWhiteSpace(offer.IssuerName) || offer.IssuerContactLines.Count > 0)
                {
                    column.Item().Column(issuer =>
                    {
                        if (!string.IsNullOrWhiteSpace(offer.IssuerName))
                            issuer.Item().Text(offer.IssuerName).SemiBold();
                        foreach (var line in offer.IssuerContactLines)
                            issuer.Item().Text(line);
                    });
                }

                column.Item().Column(client =>
                {
                    client.Item().Text("PREPARED FOR").FontColor("#50627C");
                    client.Item().Text(offer.ClientName).FontSize(options.FontSize + 6).SemiBold();
                });
                if (!string.IsNullOrWhiteSpace(offer.Message))
                    column.Item().Text(offer.Message);

                column.Item().Element(container => ComposeTable(container, offer, options));
                column.Item().ShowEntire().AlignRight().Column(totals =>
                {
                    totals.Spacing(5);
                    totals.Item().Text($"Subtotal (VAT excluded): {Money(offer.Subtotal)} RON");
                    totals.Item().Text($"VAT: {Money(offer.VatTotal)} RON");
                    totals.Item().Text($"Grand total (VAT included): {Money(offer.GrandTotal)} RON")
                        .SemiBold().FontSize(options.FontSize + 3).FontColor(options.AccentColor);
                });
            });

            if (options.ShowPageNumbers || !string.IsNullOrWhiteSpace(options.FooterText))
            {
                page.Footer().PaddingTop(12).AlignCenter().Column(footer =>
                {
                    if (!string.IsNullOrWhiteSpace(options.FooterText))
                        footer.Item().AlignCenter().Text(options.FooterText);
                    if (options.ShowPageNumbers)
                        footer.Item().AlignCenter().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            }
        });
    }).WithMetadata(new DocumentMetadata
    {
        Title = $"{options.Title} - {offer.ClientName}",
        Author = offer.IssuerName,
        Creator = "EvoOffer"
    });

    private static void ComposeTable(IContainer container, OfferPdfData offer, OfferPdfOptions options)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                var numberDigits = offer.Items.Max(item => item.Number.ToString(CultureInfo.InvariantCulture).Length);
                columns.ConstantColumn(Math.Max(28, numberDigits * options.FontSize * 0.65f + 10));
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(0.9f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.3f);
            });
            table.Header(header =>
            {
                foreach (var label in new[] { "#", "Category", "Item", "Unit price\nRON excl. VAT", "Quantity", "VAT\nRON", "Total\nRON incl. VAT" })
                    header.Cell().Background(options.AccentColor).Padding(5).Text(label).FontColor("#FFFFFF").SemiBold();
            });

            foreach (var item in offer.Items)
            {
                var values = new[]
                {
                    item.Number.ToString(CultureInfo.InvariantCulture), item.Category, item.Name,
                    Money(item.UnitPrice), QuantityValue.Format(item.Quantity),
                    $"{Money(item.VatAmount)}\n({VatRateValue.Format(item.VatRate)}%)", Money(item.Total)
                };
                for (var index = 0; index < values.Length; index++)
                {
                    var cell = table.Cell().BorderBottom(0.5f).BorderColor("#E0E7F0").Padding(5);
                    if (index >= 3)
                        cell = cell.AlignRight();
                    cell.Text(values[index]);
                }
            }
        });
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
