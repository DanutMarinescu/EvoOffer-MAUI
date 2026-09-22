using System.Globalization;
using EvoOffer.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace EvoOffer.Services;

/// <summary>Letterhead offer template shared by the app's export and PDF preview.</summary>
internal sealed class OfferPdfTemplate(OfferPdfData offer, OfferPdfOptions options, byte[]? logo) : IDocument
{
    private const string Ink = "#17202B";
    private const string Muted = "#59616A";
    private const string Rule = "#C7CCD2";
    private bool Romanian => offer.Language != AppSettings.English;
    private CultureInfo Culture => CultureInfo.GetCultureInfo(Romanian ? "ro-RO" : "en-GB");
    private string Localize(string romanian, string english) => Romanian ? romanian : english;
    private string Title => options.Title ?? Localize("Ofertă comercială", "Commercial offer");
    private string Money(decimal value) => $"{value.ToString("N2", Culture)} RON";
    private string Rate(decimal value) => value.ToString("0.##", Culture);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{Title} - {offer.ClientName}",
        Author = offer.IssuerName,
        Creator = "EvoOffer"
    };

    public void Compose(IDocumentContainer document)
    {
        document.Page(page =>
        {
            page.Size(options.PageSize);
            page.Margin(options.Margin);
            page.DefaultTextStyle(style => style.FontFamily(options.FontFamily)
                .FontSize(options.FontSize).FontColor(Ink).LineHeight(1.2f));
            page.Header().PaddingBottom(24).Column(header =>
            {
                header.Item().ShowOnce().Element(ComposeLetterhead);
                header.Item().SkipOnce().BorderBottom(0.7f).BorderColor(Rule).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Text(string.IsNullOrWhiteSpace(offer.IssuerName) ? Title : offer.IssuerName)
                        .SemiBold().FontColor(options.AccentColor);
                    row.RelativeItem().AlignRight().Text($"{Title} / {offer.ClientName}")
                        .FontSize(options.FontSize * 0.85f).FontColor(Muted);
                });
            });

            page.Content().Column(content =>
            {
                content.Item().Element(ComposeIntroduction);
                content.Item().EnsureSpace(options.FontSize * 8).Element(ComposeTable);
                var closing = options.FooterText ?? Localize(
                    "Vă mulțumim pentru încredere.\nSuntem la dispoziția dvs. pentru orice detalii suplimentare sau clarificări.",
                    "Thank you for your trust.\nPlease contact us for any further details or clarification.");
                content.Item().PreventPageBreak().Column(summary =>
                {
                    summary.Item().ShowEntire().Element(ComposeTotals);
                    if (!string.IsNullOrWhiteSpace(closing))
                        summary.Item().PaddingTop(18).Text(closing).Italic()
                            .FontSize(options.FontSize * 0.9f).FontColor(Muted);
                });
            });

            if (options.ShowPageNumbers)
                page.Footer().PaddingTop(12).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(options.FontSize * 0.75f).FontColor(Muted));
                    text.Span(Localize("Pagina ", "Page "));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
        });
    }

    private void ComposeLetterhead(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(4).PaddingBottom(24).Row(row =>
            {
                row.Spacing(18);
                if (logo is not null)
                    row.ConstantItem(62).Height(66).AlignMiddle().Image(logo).FitArea();
                row.RelativeItem().AlignMiddle().Column(brand =>
                {
                    brand.Spacing(6);
                    brand.Item().Text(string.IsNullOrWhiteSpace(offer.IssuerName) ? Title : offer.IssuerName)
                        .FontSize(options.FontSize * 2.5f).SemiBold().FontColor(options.AccentColor);
                    var subtitle = string.IsNullOrWhiteSpace(options.Tagline)
                        ? (string.IsNullOrWhiteSpace(offer.IssuerName) ? string.Empty : Title.ToUpper(Culture))
                        : options.Tagline;
                    if (!string.IsNullOrWhiteSpace(subtitle))
                        brand.Item().Text(subtitle).FontSize(options.FontSize * 0.9f).LetterSpacing(0.12f).FontColor(Muted);
                });
            });

            column.Item().LineHorizontal(0.7f).LineColor(Rule);
            var address = string.Join("\n", new[] { offer.AddressLine1, offer.AddressLine2 }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var contacts = new[]
            {
                (Text: address, Icon: "address", Weight: 1.5f),
                (Text: offer.Email, Icon: "email", Weight: 1.2f),
                (Text: offer.PhoneNumber, Icon: "phone", Weight: 1f)
            }.Where(contact => !string.IsNullOrWhiteSpace(contact.Text)).ToArray();

            if (contacts.Length > 0)
                column.Item().PaddingTop(16).Row(row =>
                {
                    for (var index = 0; index < contacts.Length; index++)
                    {
                        var contact = contacts[index];
                        IContainer block = row.RelativeItem(contact.Weight);
                        if (index > 0)
                            block = block.BorderLeft(0.7f).BorderColor(Rule).PaddingLeft(14);
                        if (index < contacts.Length - 1)
                            block = block.PaddingRight(12);
                        block.Row(details =>
                        {
                            details.Spacing(8);
                            details.ConstantItem(19).Height(24).AlignMiddle().Svg(ContactIcon(contact.Icon)).FitArea();
                            details.RelativeItem().AlignMiddle().Text(contact.Text).FontSize(options.FontSize * 0.85f);
                        });
                    }
                });

            if (!string.IsNullOrWhiteSpace(offer.VatNumber))
                column.Item().PaddingTop(9).Text($"{Localize("Cod TVA", "VAT number")}: {offer.VatNumber}")
                    .FontSize(options.FontSize * 0.8f).FontColor(Muted);
        });
    }

    private void ComposeIntroduction(IContainer container)
    {
        container.PaddingTop(8).PaddingBottom(22).Column(column =>
        {
            column.Spacing(16);
            column.Item().EnsureSpace(options.FontSize * 5).Row(row =>
            {
                row.AutoItem().PaddingRight(12).PaddingTop(2).Text(Localize("CĂTRE", "TO"))
                    .SemiBold().FontSize(options.FontSize * 1.3f).FontColor(options.AccentColor);
                row.RelativeItem().BorderBottom(0.8f).BorderColor(options.AccentColor).PaddingBottom(6)
                    .Text(offer.ClientName).SemiBold().FontSize(options.FontSize * 1.15f);
            });
            column.Item().Text(Localize("Stimată Doamnă / Stimate Domn,", "Dear Sir / Madam,")).SemiBold();
            if (!string.IsNullOrWhiteSpace(offer.Message))
                column.Item().Text(offer.Message).LineHeight(1.45f);
            if (options.ValidUntil is { } validUntil)
                column.Item().Text(text =>
                {
                    text.Span(Localize("Prezenta ofertă este valabilă până la data de: ", "This offer is valid until: "));
                    text.Span(validUntil.ToString("d MMMM yyyy", Culture)).SemiBold();
                    text.Span(".");
                });
        });
    }

    private void ComposeTable(IContainer container)
    {
        var numberDigits = offer.Items.Max(item => item.Number.ToString(CultureInfo.InvariantCulture).Length);
        var numberWidth = Math.Max(40, numberDigits * options.FontSize * 0.65f + 10);
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(numberWidth);
                columns.RelativeColumn(2.9f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(0.75f);
                columns.RelativeColumn(1.25f);
                columns.RelativeColumn(1.35f);
            });
            table.Header(header =>
            {
                var labels = new[]
                {
                    Localize("Nr. crt.", "No."), Localize("Produs", "Product"),
                    Localize("Preț unitar\n(fără T.V.A.)", "Unit price\n(excl. VAT)"),
                    Localize("Cantitate", "Quantity"), Localize("TVA calculat", "VAT amount"),
                    Localize("Preț total", "Total price")
                };
                for (var index = 0; index < labels.Length; index++)
                {
                    var cell = header.Cell().Background(options.AccentColor).PaddingHorizontal(5).PaddingVertical(10).AlignMiddle();
                    if (index != 1)
                        cell = cell.AlignCenter();
                    cell.Text(labels[index]).FontSize(options.FontSize * 0.82f).SemiBold().FontColor("#FFFFFF");
                }
            });

            var mixedRates = offer.Items.Select(item => item.VatRate).Distinct().Skip(1).Any();
            foreach (var item in offer.Items)
            {
                // Apply the soft page break to the whole row so names and amounts move together.
                // Unlike ShowEntire, this still lets an exceptionally tall description span pages.
                table.Cell().ColumnSpan(6).PreventPageBreak().BorderBottom(0.6f).BorderColor(Rule)
                    .PaddingVertical(10).Row(row =>
                {
                    Cell(row.ConstantItem(numberWidth)).AlignCenter().Text(item.Number.ToString(Culture));
                    Cell(row.RelativeItem(2.9f)).Column(product =>
                    {
                        product.Spacing(3);
                        product.Item().Text(item.Name);
                        if (!string.IsNullOrWhiteSpace(item.Category))
                            product.Item().Text(item.Category).FontSize(options.FontSize * 0.85f).FontColor(Muted);
                    });
                    Cell(row.RelativeItem(1.3f)).AlignRight().Text(Money(item.UnitPrice)).FontSize(options.FontSize * 0.9f);
                    Cell(row.RelativeItem(0.75f)).AlignCenter().Text(item.Quantity.ToString("0.############################", Culture))
                        .FontSize(options.FontSize * 0.9f);
                    Cell(row.RelativeItem(1.25f)).AlignRight().Column(vat =>
                    {
                        vat.Item().Text(Money(item.VatAmount)).FontSize(options.FontSize * 0.9f);
                        if (mixedRates)
                            vat.Item().AlignRight().Text($"({Rate(item.VatRate)}%)").FontSize(options.FontSize * 0.8f).FontColor(Muted);
                    });
                    Cell(row.RelativeItem(1.35f)).AlignRight().Text(Money(item.Total)).FontSize(options.FontSize * 0.9f);
                });
            }
        });

        static IContainer Cell(IContainer cell) => cell.PaddingHorizontal(5).AlignMiddle();
    }

    private void ComposeTotals(IContainer container)
    {
        container.Column(column =>
        {
            SummaryRow(Localize("Subtotal fără T.V.A.", "Subtotal excl. VAT"), offer.Subtotal);
            var rates = offer.Items.Select(item => item.VatRate).Distinct().ToArray();
            var vatLabel = Localize("Valoare T.V.A.", "VAT amount");
            if (rates.Length == 1)
                vatLabel += $" ({Rate(rates[0])}%)";
            SummaryRow(vatLabel, offer.VatTotal);
            column.Item().Background(options.AccentColor).PaddingHorizontal(10).PaddingVertical(10).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(Localize("TOTAL CU T.V.A.", "TOTAL INCL. VAT"))
                    .SemiBold().FontSize(options.FontSize * 1.25f).FontColor("#FFFFFF");
                row.RelativeItem().AlignRight().AlignMiddle().Text(Money(offer.GrandTotal))
                    .Bold().FontSize(options.FontSize * 1.4f).FontColor("#FFFFFF");
            });

            void SummaryRow(string label, decimal amount)
            {
                column.Item().BorderBottom(0.6f).BorderColor(Rule).PaddingHorizontal(10).PaddingVertical(9).Row(row =>
                {
                    row.RelativeItem().Text(label);
                    row.RelativeItem().AlignRight().Text(Money(amount)).SemiBold().FontColor(options.AccentColor);
                });
            }
        });
    }

    // Small vector contact icons stay sharp at print resolution and do not depend on icon fonts.
    private string ContactIcon(string kind)
    {
        var paths = kind switch
        {
            "address" => "<path d='M20 10c0 6-8 13-8 13S4 16 4 10a8 8 0 1 1 16 0Z'/><circle cx='12' cy='10' r='3'/>",
            "email" => "<rect x='2' y='4' width='20' height='16' rx='1'/><path d='m2 5 10 8L22 5'/>",
            _ => "<path d='m5 2 4 5-3 3c2 4 4 6 8 8l3-3 5 4c-1 4-4 5-8 3C8 19 3 14 1 7 0 4 2 2 5 2Z'/>"
        };
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='26' viewBox='0 0 24 26' fill='none' stroke='{options.AccentColor}' stroke-width='1.4' stroke-linecap='round' stroke-linejoin='round'>{paths}</svg>";
    }
}
