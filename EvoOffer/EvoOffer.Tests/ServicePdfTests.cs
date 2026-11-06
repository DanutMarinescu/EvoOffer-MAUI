using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;

public static class ServicePdfTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckSnapshot(check);
        var options = new OfferPdfOptions();
        foreach (var template in OfferPdfTemplates.All)
        foreach (var language in new[] { AppSettings.Romanian, AppSettings.English })
        foreach (var includeProduct in new[] { false, true })
        {
            var service = new ServiceDefinition
            {
                Name = "Montaj parchet", UnitOfMeasurement = "m²", PriceExcludingVat = 61.20m
            };
            var serviceLine = new OfferLineItem(1, service, 2.75m, 21m);
            var lines = new List<OfferLineItem> { serviceLine };
            if (includeProduct)
                lines.Add(new OfferLineItem(2, new CatalogItem("Parchet", "Oak boards", 100m), 1m, 21m));
            var offer = new OfferPdfData("Service customer", string.Empty, lines,
                new AppSettings { PdfTemplateId = template.Id, Language = language });
            var text = VisibleText(offer, options);
            var culture = LocalizationService.CultureFor(language);
            var label = language == AppSettings.English ? "Unit of measurement:" : "Unitate de măsură:";
            var context = $"{template.Id}, {language}, {(includeProduct ? "mixed" : "services only")}";

            check(text.Contains(service.Name, StringComparison.Ordinal)
                && text.Contains(label, StringComparison.Ordinal)
                && text.Contains(service.UnitOfMeasurement, StringComparison.Ordinal)
                && text.Contains(serviceLine.Quantity.ToString("0.############################", culture), StringComparison.Ordinal),
                $"Service name, saved unit and edited quantity are visible in the PDF ({context})");
            check(new[] { serviceLine.UnitPrice, serviceLine.VatAmount, serviceLine.Total,
                    offer.Subtotal, offer.VatTotal, offer.GrandTotal }
                .All(amount => text.Contains(Money(amount, culture), StringComparison.Ordinal)),
                $"Service net price, VAT and all totals are visible in the PDF ({context})");
            check(text.Contains("Oak boards", StringComparison.Ordinal) == includeProduct,
                $"Services can be exported alone or together with catalog products ({context})");
        }
    }

    private static void CheckSnapshot(Action<bool, string> check)
    {
        var service = new ServiceDefinition
        {
            Name = "Installation", UnitOfMeasurement = "hour", PriceExcludingVat = 61.20m
        };
        var line = new OfferLineItem(1, service, 2.75m, 21m);
        var snapshot = new OfferPdfData("Customer", null, [line], new AppSettings());
        service.Name = "Changed service";
        service.UnitOfMeasurement = "trip";
        service.PriceExcludingVat = 1m;
        line.Quantity = 4m;
        line.VatRate = 9m;
        var saved = snapshot.Items.Single();
        check(saved.Name == "Installation" && saved.UnitOfMeasurement == "hour"
            && saved.UnitPrice == 61.20m && saved.Quantity == 2.75m && saved.VatRate == 21m
            && saved.NetTotal == 168.30m && saved.VatAmount == 35.34m && saved.Total == 203.64m,
            "A PDF keeps the service's name, unit, net price, quantity and amounts after later settings and offer edits");

        var product = new OfferLineItem(1, new CatalogItem("Flooring", "Oak", 100m), 1m, 21m);
        var productSnapshot = new OfferPdfData("Customer", null, [product], new AppSettings());
        check(productSnapshot.Items.Single().UnitOfMeasurement == string.Empty,
            "Catalog products without a unit retain their existing PDF details");
    }

    private static string Money(decimal amount, CultureInfo culture) => amount.ToString("N2", culture) + " RON";

    private static string VisibleText(OfferPdfData offer, OfferPdfOptions options)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        return Regex.Replace(string.Join(' ', new OfferPdfTemplate(offer, options, null).GenerateSvg()
            .SelectMany(page => XDocument.Parse(page).Descendants(svg + "text").Select(element => element.Value.Trim()))),
            @"\s+", " ");
    }
}
