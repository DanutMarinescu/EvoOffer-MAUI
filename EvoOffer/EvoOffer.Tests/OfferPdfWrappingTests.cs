using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;

internal static class OfferPdfWrappingTests
{
    private const string Category = "Categorie verificată";
    private const string FollowingItem = "Următorul produs";

    public static void Run(Action<bool, string> check)
    {
        var baseline = Render("Produs scurt");
        const string longName = "Parchet din stejar pentru încăperi comerciale cu trafic intens, "
            + "cu finisaj rezistent la zgârieturi, accesorii pentru instalare și protecție suplimentară "
            + "împotriva umidității, potrivit pentru proiecte de renovare și amenajare interioară";
        var wrapped = Render(longName);
        var wrappedLines = NameLines(wrapped);
        var wrappedCategory = wrapped.Single(text => text.Value == Category);
        var wrappedFollowing = wrapped.Single(text => text.Value == FollowingItem);

        check(wrappedLines.Length > 1 && wrappedLines.Select(text => (text.Page, text.Y)).Distinct().Count() > 1
            && string.Join(' ', wrappedLines.Select(text => text.Value)) == longName,
            "Long Romanian product names wrap onto distinct lines without truncating any text");
        check(wrappedCategory.Y > baseline.Single(text => text.Value == Category).Y
            && wrappedFollowing.Y > baseline.Single(text => text.Value == FollowingItem).Y
            && wrappedCategory.Page == 0 && wrappedFollowing.Page == 0
            && IsAfter(wrappedCategory, wrappedLines[^1]) && IsAfter(wrappedFollowing, wrappedCategory),
            "Wrapping a product name expands its row and moves its category and the following item down without overlap");

        var identifier = string.Concat(Enumerable.Repeat("PRODUS1234567890", 24));
        var identifierLayout = Render(identifier);
        var identifierLines = NameLines(identifierLayout);
        check(identifierLines.Length > 1
            && string.Concat(identifierLines.Select(text => text.Value)) == identifier
            && IsAfter(identifierLayout.Single(text => text.Value == Category), identifierLines[^1]),
            "Unbroken product identifiers wrap across lines and preserve every character before the category");

        var spanningName = string.Join(' ', Enumerable.Repeat(
            "Sistem profesional pentru instalare și întreținere cu documentație tehnică completă", 100));
        var spanning = Render(spanningName);
        var spanningLines = NameLines(spanning);
        var spanningCategory = spanning.Single(text => text.Value == Category);
        var spanningFollowing = spanning.Single(text => text.Value == FollowingItem);
        var total = spanning.Single(text => text.Value == "TOTAL CU T.V.A.");
        check(spanningLines.Select(text => text.Page).Distinct().Count() > 1
            && string.Join(' ', spanningLines.Select(text => text.Value)) == spanningName,
            "An exceptionally long product name continues across pages without losing or duplicating text");
        check(IsAfter(spanningCategory, spanningLines[^1])
            && IsAfter(spanningFollowing, spanningCategory) && IsAfter(total, spanningFollowing),
            "The category, next product and offer total appear after the complete name when a row spans pages");
    }

    private static RenderedText[] Render(string name)
    {
        var offer = new OfferPdfData("Client verificare", null,
            new[]
            {
                new OfferLineItem(1, new CatalogItem(Category, name, 123m), 2m, 21m),
                new OfferLineItem(2, new CatalogItem(string.Empty, FollowingItem, 45m), 1m, 21m)
            }, new AppSettings { IssuerName = "Verificare ofertă", Language = AppSettings.Romanian });
        var options = new OfferPdfOptions { FooterText = string.Empty, ShowPageNumbers = false };

        // SVG uses the production document's layout and exposes text baselines without a PDF parsing dependency.
        XNamespace svg = "http://www.w3.org/2000/svg";
        return new OfferPdfTemplate(offer, options, null).GenerateSvg()
            .SelectMany((page, pageNumber) => XDocument.Parse(page).Descendants(svg + "text")
                .Select(element =>
                {
                    var translation = Regex.Match((string?)element.Attribute("transform") ?? string.Empty,
                        @"translate\(([-+\d.eE]+)[ ,]+([-+\d.eE]+)\)");
                    if (!translation.Success)
                        throw new InvalidOperationException("Expected translated text in the generated offer SVG.");
                    return new RenderedText(pageNumber,
                        Number(translation.Groups[1].Value) + FirstNumber(element, "x"),
                        Number(translation.Groups[2].Value) + FirstNumber(element, "y"),
                        Number((string)element.Attribute("font-size")!), element.Value.Trim());
                }))
            .ToArray();
    }

    private static RenderedText[] NameLines(RenderedText[] layout)
    {
        var following = layout.Single(text => text.Value == FollowingItem);
        return layout.Where(text => Math.Abs(text.X - following.X) < 0.01
                && text.FontSize == following.FontSize && text.Value != FollowingItem)
            .OrderBy(text => text.Page).ThenBy(text => text.Y).ToArray();
    }

    private static bool IsAfter(RenderedText current, RenderedText previous) =>
        current.Page > previous.Page || current.Page == previous.Page && current.Y > previous.Y;

    private static double FirstNumber(XElement element, string attribute) =>
        Number(((string)element.Attribute(attribute)!).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);

    private static double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private sealed record RenderedText(int Page, double X, double Y, double FontSize, string Value);
}
