using System.Globalization;
using EvoOffer.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Color = QuestPDF.Infrastructure.Color;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace EvoOffer.Services;

/// <summary>Offer layouts shared by the app's export and PDF preview.</summary>
internal sealed class OfferPdfTemplate(OfferPdfData offer, OfferPdfOptions options, byte[]? logo) : IDocument
{
    private readonly Color Primary = options.AccentColor ?? Color.FromHex(offer.PdfPrimaryColor);
    private readonly Color Ink = options.TextColor ?? Color.FromHex(offer.PdfTextColor);
    private readonly Color Muted = options.SecondaryColor ?? Color.FromHex(offer.PdfSecondaryColor);
    private Color Rule => Tint(Muted, 0.67);
    private Color PaperTint => Tint(Muted, 0.93);
    private Color OnPrimary => ContrastingInk(Primary);
    private string TemplateId => OfferPdfTemplates.Normalize(options.TemplateId ?? offer.PdfTemplateId);
    private bool Modern => OfferPdfTemplates.GetBaseTemplateId(TemplateId) == OfferPdfTemplates.Modern;
    private bool Minimal => OfferPdfTemplates.GetBaseTemplateId(TemplateId) == OfferPdfTemplates.Minimal;
    private bool HideIssuerName => OfferPdfTemplates.HidesIssuerName(TemplateId);
    private bool ShowIssuerName => !HideIssuerName && !string.IsNullOrWhiteSpace(offer.IssuerName);
    private string BrandTitle => ShowIssuerName ? offer.IssuerName : Title;
    private bool Romanian => offer.Language != AppSettings.English;
    private CultureInfo Culture => CultureInfo.GetCultureInfo(Romanian ? "ro-RO" : "en-GB");
    private string Localize(string romanian, string english) => Romanian ? romanian : english;
    private string Title => options.Title ?? Localize("Ofertă comercială", "Commercial offer");
    private string Money(decimal value) => $"{value.ToString("N2", Culture)} RON";
    private string Rate(decimal value) => value.ToString("0.##", Culture);

    private static Color Tint(Color color, double whiteAmount)
    {
        // Compose transparency over the white page before producing an opaque tint.
        byte Blend(byte component) => (byte)Math.Round(255 - (255 - component)
            * (color.Alpha / 255d) * (1 - whiteAmount));
        return Color.FromRGB(Blend(color.Red), Blend(color.Green), Blend(color.Blue));
    }

    private static Color ContrastingInk(Color background)
    {
        double Linear(byte component)
        {
            var value = (255 - (255 - component) * (background.Alpha / 255d)) / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Linear(background.Red) + 0.7152 * Linear(background.Green)
            + 0.0722 * Linear(background.Blue);
        var blackContrast = (luminance + 0.05) / 0.05;
        var whiteContrast = 1.05 / (luminance + 0.05);
        return Color.FromHex(blackContrast >= whiteContrast ? "#000000" : "#FFFFFF");
    }

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
            page.Header().PaddingBottom(Minimal ? 16 : Modern ? 20 : 24).Column(header =>
            {
                header.Item().ShowOnce().Element(Modern ? ComposeModernLetterhead
                    : Minimal ? ComposeMinimalLetterhead : ComposeLetterhead);
                header.Item().SkipOnce().BorderBottom(0.7f).BorderColor(Rule).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Text(BrandTitle)
                        .SemiBold().FontColor(Primary);
                    row.RelativeItem().AlignRight().Text(HideIssuerName ? offer.ClientName : $"{Title} / {offer.ClientName}")
                        .FontSize(options.FontSize * 0.85f).FontColor(Muted);
                });
            });

            page.Content().Column(content =>
            {
                content.Item().Element(Modern ? ComposeModernIntroduction
                    : Minimal ? ComposeMinimalIntroduction : ComposeIntroduction);
                content.Item().EnsureSpace(options.FontSize * 8).Element(Modern ? ComposeProductCards : ComposeTable);
                var closing = options.FooterText ?? Localize(
                    "Vă mulțumim pentru încredere.\nSuntem la dispoziția dvs. pentru orice detalii suplimentare sau clarificări.",
                    "Thank you for your trust.\nPlease contact us for any further details or clarification.");
                content.Item().PreventPageBreak().Column(summary =>
                {
                    summary.Item().ShowEntire().Element(Modern ? ComposeModernTotals
                        : Minimal ? ComposeMinimalTotals : ComposeTotals);
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
                {
                    IContainer logoContainer = HideIssuerName
                        ? row.ConstantItem(180).MaxHeight(126)
                        : row.ConstantItem(62).Height(66);
                    logoContainer.AlignMiddle().Image(logo).FitArea();
                }
                row.RelativeItem().AlignMiddle().Column(brand =>
                {
                    brand.Spacing(6);
                    brand.Item().Text(BrandTitle)
                        .FontSize(options.FontSize * 2.5f).SemiBold().FontColor(Primary);
                    var subtitle = string.IsNullOrWhiteSpace(options.Tagline)
                        ? (ShowIssuerName ? Title.ToUpper(Culture) : string.Empty)
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
                    .SemiBold().FontSize(options.FontSize * 1.3f).FontColor(Primary);
                row.RelativeItem().BorderBottom(0.8f).BorderColor(Primary).PaddingBottom(6)
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

    private void ComposeModernLetterhead(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(Primary).Padding(18).Row(row =>
            {
                row.Spacing(16);
                if (logo is not null)
                {
                    IContainer logoContainer = HideIssuerName
                        ? row.ConstantItem(180).MaxHeight(126)
                        : row.ConstantItem(64).Height(64);
                    logoContainer = logoContainer.Background("#FFFFFF").Padding(5);
                    if (HideIssuerName)
                        logoContainer = logoContainer.AlignMiddle();
                    logoContainer.Image(logo).FitArea();
                }
                IContainer brandContainer = row.RelativeItem();
                if (HideIssuerName)
                    brandContainer = brandContainer.AlignMiddle();
                brandContainer.Column(brand =>
                {
                    brand.Spacing(6);
                    if (ShowIssuerName)
                        brand.Item().Text(offer.IssuerName).FontSize(options.FontSize * 1.8f)
                            .SemiBold().FontColor(OnPrimary);
                    brand.Item().Text(Title).FontSize(options.FontSize * (HideIssuerName ? 1.8f : 1.25f)).FontColor(OnPrimary);
                    if (!string.IsNullOrWhiteSpace(options.Tagline))
                        brand.Item().Text(options.Tagline).FontSize(options.FontSize * 0.85f).FontColor(OnPrimary);
                });
            });
            column.Item().PaddingTop(12).Row(row =>
            {
                row.Spacing(20);
                row.RelativeItem().Column(address =>
                {
                    AddContact(address, offer.AddressLine1);
                    AddContact(address, offer.AddressLine2);
                });
                row.RelativeItem().Column(contact =>
                {
                    AddContact(contact, offer.Email);
                    AddContact(contact, offer.PhoneNumber);
                    AddContact(contact, VatContact);
                });
            });
        });
    }

    private void ComposeMinimalLetterhead(IContainer container)
    {
        container.BorderBottom(1.2f).BorderColor(Primary).PaddingBottom(12).Column(column =>
        {
            column.Spacing(8);
            column.Item().Row(row =>
            {
                row.Spacing(12);
                if (logo is not null)
                {
                    IContainer logoContainer = HideIssuerName
                        ? row.ConstantItem(150).MaxHeight(96)
                        : row.ConstantItem(42).Height(42);
                    if (HideIssuerName)
                        logoContainer = logoContainer.AlignMiddle();
                    logoContainer.Image(logo).FitArea();
                }
                IContainer brandContainer = row.RelativeItem(1.6f);
                if (HideIssuerName)
                    brandContainer = brandContainer.AlignMiddle();
                brandContainer.Column(brand =>
                {
                    brand.Spacing(4);
                    brand.Item().Text(BrandTitle)
                        .SemiBold().FontSize(options.FontSize * 1.4f).FontColor(Primary);
                    if (!string.IsNullOrWhiteSpace(options.Tagline))
                        brand.Item().Text(options.Tagline).FontSize(options.FontSize * 0.8f).FontColor(Muted);
                });
                if (ShowIssuerName)
                    row.RelativeItem().AlignRight().Text(Title).FontSize(options.FontSize * 1.15f).FontColor(Muted);
            });
            var address = string.Join(" · ", new[] { offer.AddressLine1, offer.AddressLine2 }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var contacts = string.Join(" · ", new[] { offer.Email, offer.PhoneNumber, VatContact }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            AddContact(column, address);
            AddContact(column, contacts);
        });
    }

    private string VatContact => string.IsNullOrWhiteSpace(offer.VatNumber)
        ? string.Empty : $"{Localize("Cod TVA", "VAT number")}: {offer.VatNumber}";

    private void AddContact(ColumnDescriptor column, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            column.Item().Text(value).FontSize(options.FontSize * 0.85f).FontColor(Muted);
    }

    private void ComposeModernIntroduction(IContainer container)
    {
        container.PaddingBottom(16).Column(column =>
        {
            column.Spacing(12);
            column.Item().EnsureSpace(options.FontSize * 6).Background(PaperTint)
                .BorderLeft(3).BorderColor(Primary).PaddingHorizontal(14).PaddingVertical(12).Column(client =>
                {
                    client.Spacing(4);
                    client.Item().Text(Localize("PREGĂTITĂ PENTRU", "PREPARED FOR"))
                        .FontSize(options.FontSize * 0.8f).SemiBold().FontColor(Muted);
                    client.Item().Text(offer.ClientName).FontSize(options.FontSize * 1.35f).SemiBold();
                });
            ComposeMessage(column);
        });
    }

    private void ComposeMinimalIntroduction(IContainer container)
    {
        container.PaddingBottom(18).Column(column =>
        {
            column.Spacing(12);
            column.Item().EnsureSpace(options.FontSize * 4).Text(text =>
            {
                text.Span(Localize("Către: ", "To: ")).FontColor(Muted);
                text.Span(offer.ClientName).SemiBold();
            });
            ComposeMessage(column);
        });
    }

    private void ComposeMessage(ColumnDescriptor column)
    {
        column.Item().Text(Localize("Stimată Doamnă / Stimate Domn,", "Dear Sir / Madam,")).SemiBold();
        if (!string.IsNullOrWhiteSpace(offer.Message))
            column.Item().Text(offer.Message).LineHeight(1.4f);
        if (options.ValidUntil is { } validUntil)
            column.Item().Text(text =>
            {
                text.Span(Localize("Prezenta ofertă este valabilă până la data de: ", "This offer is valid until: "));
                text.Span(validUntil.ToString("d MMMM yyyy", Culture)).SemiBold();
                text.Span(".");
            });
    }

    private void ComposeProductCards(IContainer container)
    {
        // A one-column table repeats the section heading while allowing unusually long cards to split.
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => columns.RelativeColumn());
            table.Header(header => header.Cell().PaddingBottom(10).Text(Localize("Produse și servicii", "Products and services"))
                .FontSize(options.FontSize * 1.2f).SemiBold().FontColor(Primary));
            var mixedRates = offer.Items.Select(item => item.VatRate).Distinct().Skip(1).Any();
            foreach (var item in offer.Items)
            {
                table.Cell().PaddingBottom(8).PreventPageBreak().Border(0.7f).BorderColor(Rule).Column(card =>
                {
                    card.Item().Background(PaperTint).PaddingHorizontal(10).PaddingVertical(7).Row(row =>
                    {
                        row.Spacing(12);
                        var numberWidth = Math.Max(26, item.Number.ToString(CultureInfo.InvariantCulture).Length
                            * options.FontSize * 0.65f);
                        row.ConstantItem(numberWidth).Text(item.Number.ToString(Culture))
                            .SemiBold().FontColor(Primary);
                        row.RelativeItem().Element(product => ComposeProductDetails(product, item, emphasizeName: true));
                    });
                    card.Item().PaddingHorizontal(10).PaddingVertical(7).Row(row =>
                    {
                        row.Spacing(12);
                        PriceMetric(row.RelativeItem(), Localize("Preț unitar (fără T.V.A.)", "Unit price (excl. VAT)"), Money(item.UnitPrice));
                        PriceMetric(row.RelativeItem(0.75f), Localize("Cantitate", "Quantity"),
                            item.Quantity.ToString("0.############################", Culture));
                        PriceMetric(row.RelativeItem(), Localize("TVA calculat", "VAT amount")
                            + (mixedRates ? $" ({Rate(item.VatRate)}%)" : string.Empty), Money(item.VatAmount));
                        PriceMetric(row.RelativeItem(1.2f), Localize("Preț total", "Total price"), Money(item.Total), emphasize: true);
                    });
                });
            }
        });
    }

    private void PriceMetric(IContainer container, string label, string value, bool emphasize = false)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text(label).FontSize(options.FontSize * 0.8f).FontColor(Muted);
            var amount = column.Item().Text(value).FontSize(options.FontSize * 0.95f);
            if (emphasize)
                amount.SemiBold().FontColor(Primary);
        });
    }

    private void ComposeProductDetails(IContainer container, OfferPdfLine item, bool emphasizeName = false)
    {
        container.Column(product =>
        {
            product.Spacing(3);
            var name = product.Item().Text(item.Name);
            if (emphasizeName)
                name.SemiBold();
            foreach (var (label, value) in new[]
            {
                (Localize("Mărime", "Size"), item.Size),
                (Localize("Culoare", "Color"), item.Color)
            })
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                product.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(options.FontSize * 0.85f));
                    text.Span($"{label}: ").SemiBold();
                    text.Span(value);
                });
            }
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
                    var cell = Minimal
                        ? header.Cell().BorderBottom(1).BorderColor(Primary)
                        : header.Cell().Background(Primary);
                    cell = cell.PaddingHorizontal(5).PaddingVertical(Minimal ? 8 : 10).AlignMiddle();
                    if (index != 1)
                        cell = cell.AlignCenter();
                    cell.Text(labels[index]).FontSize(options.FontSize * 0.82f).SemiBold()
                        .FontColor(Minimal ? Primary : OnPrimary);
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
                    // Keep product text unconstrained, including words wider than the column.
                    Cell(row.RelativeItem(2.9f)).Element(product => ComposeProductDetails(product, item));
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
            column.Item().Background(Primary).PaddingHorizontal(10).PaddingVertical(10).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(Localize("TOTAL CU T.V.A.", "TOTAL INCL. VAT"))
                    .SemiBold().FontSize(options.FontSize * 1.25f).FontColor(OnPrimary);
                row.RelativeItem().AlignRight().AlignMiddle().Text(Money(offer.GrandTotal))
                    .Bold().FontSize(options.FontSize * 1.4f).FontColor(OnPrimary);
            });

            void SummaryRow(string label, decimal amount)
            {
                column.Item().BorderBottom(0.6f).BorderColor(Rule).PaddingHorizontal(10).PaddingVertical(9).Row(row =>
                {
                    row.RelativeItem().Text(label);
                    row.RelativeItem().AlignRight().Text(Money(amount)).SemiBold().FontColor(Primary);
                });
            }
        });
    }

    private void ComposeModernTotals(IContainer container)
    {
        container.PaddingTop(4).Background(PaperTint).Padding(12).Column(column =>
        {
            ComposePlainSummary(column);
            column.Item().PaddingTop(10).BorderTop(1).BorderColor(Primary).PaddingTop(10).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(Localize("TOTAL CU T.V.A.", "TOTAL INCL. VAT"))
                    .SemiBold().FontColor(Primary);
                row.RelativeItem().AlignRight().Text(Money(offer.GrandTotal))
                    .Bold().FontSize(options.FontSize * 1.6f).FontColor(Primary);
            });
        });
    }

    private void ComposeMinimalTotals(IContainer container)
    {
        container.PaddingTop(8).Column(column =>
        {
            ComposePlainSummary(column);
            column.Item().PaddingTop(8).BorderTop(1.2f).BorderColor(Primary).PaddingTop(10).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(Localize("TOTAL CU T.V.A.", "TOTAL INCL. VAT")).SemiBold();
                row.RelativeItem().AlignRight().Text(Money(offer.GrandTotal)).Bold().FontSize(options.FontSize * 1.3f);
            });
        });
    }

    private void ComposePlainSummary(ColumnDescriptor column)
    {
        SummaryRow(Localize("Subtotal fără T.V.A.", "Subtotal excl. VAT"), offer.Subtotal);
        var rates = offer.Items.Select(item => item.VatRate).Distinct().ToArray();
        var vatLabel = Localize("Valoare T.V.A.", "VAT amount");
        if (rates.Length == 1)
            vatLabel += $" ({Rate(rates[0])}%)";
        SummaryRow(vatLabel, offer.VatTotal);

        void SummaryRow(string label, decimal amount)
        {
            column.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Muted);
                row.RelativeItem().AlignRight().Text(Money(amount)).SemiBold();
            });
        }
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
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='26' viewBox='0 0 24 26' fill='none' stroke='{Primary}' stroke-width='1.4' stroke-linecap='round' stroke-linejoin='round'>{paths}</svg>";
    }
}
