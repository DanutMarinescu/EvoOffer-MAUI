using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;

internal static class DiscountPdfTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckSnapshot(check);
        var options = new OfferPdfOptions();
        foreach (var template in OfferPdfTemplates.All)
        foreach (var language in new[] { AppSettings.Romanian, AppSettings.English })
        {
            var offer = CreateOffer(template.Id, language);
            var pages = new OfferPdfTemplate(offer, options, null).GenerateSvg().ToArray();
            var text = VisibleText(pages);
            var culture = LocalizationService.CultureFor(language);
            string Money(decimal amount) => amount.ToString("N2", culture) + " RON";
            var description = offer.Items[0].Discount!.Describe(language);
            var context = $"{template.Id}, {language}";
            check(text.Contains(description, StringComparison.Ordinal)
                && text.IndexOf(description, StringComparison.Ordinal) < text.IndexOf("Oak flooring", StringComparison.Ordinal)
                && text.Contains(offer.Items[1].Discount!.Describe(language), StringComparison.Ordinal),
                $"PDF item discount descriptions precede their parent products and services ({context})");
            check(new[] { -24.20m, -20m, -29.78m, 297.80m, 234m, 34.02m, 268.02m }
                .All(amount => text.Contains(Money(amount), StringComparison.Ordinal)),
                $"PDF line reductions, general reduction, original total and final net/VAT/gross totals are visible ({context})");
            check(text.Contains(language == AppSettings.English ? "General discount" : "Reducere generală",
                    StringComparison.Ordinal)
                && pages.Any(page => page.Contains("#B42318", StringComparison.OrdinalIgnoreCase)),
                $"PDF discounts use the saved language and red presentation ({context})");

            var repeated = Enumerable.Range(1, 60).Select(index => new OfferLineItem(index,
                new CatalogItem("Category", $"Discounted item {index}", 100m), 2.5m, 21m)
                { Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 12.5m) });
            var longOffer = new OfferPdfData("Discount pagination customer", string.Empty, repeated,
                new AppSettings { PdfTemplateId = template.Id, Language = language },
                new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Absolute, 50m));
            var longPages = new OfferPdfTemplate(longOffer, options, null).GenerateSvg().ToArray();
            var longText = VisibleText(longPages);
            check(longPages.Length > 1 && Enumerable.Range(1, 60).All(index =>
                    longText.Contains($"Discounted item {index}", StringComparison.Ordinal)),
                $"Discounted offers paginate without losing items ({context})");
        }
    }

    public static OfferPdfData CreateOffer(string template, string language)
    {
        var product = new OfferLineItem(1, new CatalogItem("Flooring", "Oak flooring", 100m), 2m, 21m)
        {
            Discount = new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Absolute, 12.10m)
        };
        var service = new OfferLineItem(2,
            new ServiceDefinition { Name = "Installation", UnitOfMeasurement = "hour", PriceExcludingVat = 50m }, 2m, 0m)
        {
            Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 20m)
        };
        return new OfferPdfData("Discount customer", "Discounted products and services.", [product, service],
            new AppSettings { IssuerName = "Discount issuer", PdfTemplateId = template, Language = language },
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m));
    }

    private static void CheckSnapshot(Action<bool, string> check)
    {
        var product = new OfferLineItem(1, new CatalogItem("Flooring", "Oak flooring", 100m), 2m, 21m)
        {
            Discount = new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Absolute, 12.10m)
        };
        var general = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m);
        var snapshot = new OfferPdfData("Customer", null, [product], new AppSettings(), general);
        product.Discount = null;
        product.Quantity = 5m;
        var line = snapshot.Items.Single();
        check(line.OriginalNetTotal == 200m && line.OriginalVatAmount == 42m && line.OriginalTotal == 242m
            && line.DiscountNetAmount == 20m && line.DiscountVatAmount == 4.20m && line.DiscountAmount == 24.20m
            && line.NetTotal == 180m && line.VatAmount == 37.80m && line.Total == 217.80m
            && line.Discount == new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Absolute, 12.10m),
            "PDF snapshots retain discount definitions and original/reduced values after live offer edits");
        check(snapshot.GeneralDiscount == general && snapshot.TotalBeforeGeneralDiscount == 217.80m
            && snapshot.GeneralDiscountAmount == 21.78m && snapshot.Subtotal == 162m
            && snapshot.VatTotal == 34.02m && snapshot.GrandTotal == 196.02m
            && snapshot.GeneralDiscountNetAmount == 18m && snapshot.GeneralDiscountVatAmount == 3.78m,
            "PDF general discounts apply after line discounts and proportionally reduce net and VAT amounts");

        var zero = new OfferPdfData("Customer", null, [product], new AppSettings(),
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 100m));
        check(zero.Subtotal == 0m && zero.VatTotal == 0m && zero.GrandTotal == 0m,
            "A full general PDF discount zeroes both taxable amount and VAT");
        var capped = new OfferPdfData("Customer", null, [product], new AppSettings(),
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Absolute, 1000m));
        check(capped.GrandTotal == 0m && capped.GeneralDiscountAmount == product.Total,
            "PDF snapshots preserve general discounts capped after subsequent offer edits");
    }

    private static string VisibleText(IEnumerable<string> pages)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        return Regex.Replace(string.Join(' ', pages.SelectMany(page => XDocument.Parse(page)
            .Descendants(svg + "text").Select(element => element.Value.Trim()))), @"\s+", " ");
    }
}
