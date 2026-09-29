using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;

internal static class OfferPdfNoIssuerNameTests
{
    private const string Issuer = "Distinctive issuer";
    private static readonly (string Original, string Variant)[] Templates =
    {
        (OfferPdfTemplates.Classic, OfferPdfTemplates.ClassicNoIssuerName),
        (OfferPdfTemplates.Modern, OfferPdfTemplates.ModernNoIssuerName),
        (OfferPdfTemplates.Minimal, OfferPdfTemplates.MinimalNoIssuerName)
    };
    private static readonly (string Name, string Png, double AspectRatio)[] Logos =
    {
        ("square", "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGPQ8LIAIgYIBQASZgKp0V582AAAAABJRU5ErkJggg==", 1),
        ("wide", "iVBORw0KGgoAAAANSUhEUgAAAAgAAAACCAYAAABllJ3tAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAASSURBVBhXY2BgaPiPH2MIoGIA+mgX8bDDIh0AAAAASUVORK5CYII=", 4),
        ("tall", "iVBORw0KGgoAAAANSUhEUgAAAAIAAAAICAYAAADTLS5CAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY2BgaPgPwRQxAEJNF/EGaOu7AAAAAElFTkSuQmCC", 0.25)
    };

    public static void Run(Action<bool, string> check)
    {
        var options = new OfferPdfOptions { Title = "Custom proposal", Tagline = "Company tagline" };
        foreach (var (original, variant) in Templates)
        {
            check(OfferPdfTemplates.GetBaseTemplateId(variant) == original
                && OfferPdfTemplates.HidesIssuerName(variant) && !OfferPdfTemplates.HidesIssuerName(original),
                $"The {variant} selection preserves its base layout and distinct issuer-name behavior");

            var settings = Settings(variant);
            var snapshot = Offer(settings);
            settings.PdfTemplateId = original;
            settings.IssuerName = "Edited after saving";
            check(snapshot.PdfTemplateId == variant && snapshot.IssuerName == Issuer,
                $"The {variant} offer snapshots its variant while retaining the saved issuer details");

            var noLogoPages = Render(snapshot, options);
            var noLogoText = VisibleText(noLogoPages[0]);
            check(noLogoPages.Length == 1 && !noLogoText.Contains(Issuer)
                && new[] { "Custom proposal", "Company tagline", "office@example.com", "Example address", "00123456" }
                    .All(noLogoText.Contains),
                $"The {variant} layout remains useful without a logo and retains the title, tagline and contacts");

            var longOffer = Offer(Settings(original), 150);
            var originalPages = Render(longOffer, options);
            var variantPages = Render(longOffer, options with { TemplateId = variant });
            check(originalPages.Length > 1 && originalPages.All(page => VisibleText(page).Contains(Issuer)),
                $"The original {original} layout retains the issuer on its first and continuation pages");
            check(variantPages.Length > 1 && variantPages.All(page => !VisibleText(page).Contains(Issuer))
                && variantPages.All(page => VisibleText(page).Contains("Custom proposal")),
                $"The {variant} override suppresses the issuer on every page and keeps the offer title");
            check(longOffer.PdfTemplateId == original && longOffer.IssuerName == Issuer,
                $"Rendering the {variant} override preserves the original offer snapshot");

            foreach (var (name, png, aspectRatio) in Logos)
            {
                var logo = Convert.FromBase64String(png);
                var originalBounds = LogoBounds(Render(snapshot, options with { TemplateId = original }, logo)[0]);
                var variantBounds = LogoBounds(Render(snapshot, options, logo)[0]);
                check(variantBounds.Width >= originalBounds.Width * 1.8
                    && variantBounds.Height >= originalBounds.Height * 1.8,
                    $"The {variant} layout renders a materially larger {name} logo than {original}");
                check(Math.Abs(variantBounds.Width / variantBounds.Height - aspectRatio) < 0.01
                    && variantBounds.Left >= options.Margin - 0.1 && variantBounds.Top >= options.Margin - 0.1
                    && variantBounds.Right <= options.PageSize.Width - options.Margin + 0.1
                    && variantBounds.Bottom <= options.PageSize.Height - options.Margin + 0.1,
                    $"The {variant} layout preserves the {name} logo's proportions and keeps it inside the page");
            }

            var missingSettings = Settings(variant);
            missingSettings.LogoPath = Path.Combine(Path.GetTempPath(), "EvoOffer-missing-logo-" + Guid.NewGuid() + ".png");
            using var output = new MemoryStream();
            var missingReported = false;
            try { new OfferPdfService(options).Generate(Offer(missingSettings), output); }
            catch (FileNotFoundException) { missingReported = true; }
            check(missingReported && output.Length == 0,
                $"The {variant} layout reports a removed logo before writing a partial PDF");
        }
    }

    private static AppSettings Settings(string template) => new()
    {
        PdfTemplateId = template, IssuerName = Issuer, Language = AppSettings.English,
        AddressLine1 = "Example address", Email = "office@example.com", VatNumber = "00123456"
    };

    private static OfferPdfData Offer(AppSettings settings, int itemCount = 2) => new("Example customer", null,
        Enumerable.Range(1, itemCount).Select(index => new OfferLineItem(index,
            new CatalogItem("Services", $"Item {index}", 25m), 1m, 21m)), settings);

    private static XDocument[] Render(OfferPdfData offer, OfferPdfOptions options, byte[]? logo = null) =>
        new OfferPdfTemplate(offer, options, logo).GenerateSvg().Select(XDocument.Parse).ToArray();

    private static string VisibleText(XDocument page) => Regex.Replace(string.Join(' ',
        page.Descendants().Where(element => element.Name.LocalName == "text").Select(element => element.Value.Trim())),
        @"\s+", " ");

    // Inspect the actual SVG image geometry produced by the PDF layout, including its enclosing transforms.
    private static Bounds LogoBounds(XDocument page)
    {
        var image = page.Descendants().Single(element => element.Name.LocalName == "image");
        var transform = Matrix3x2.Identity;
        var placement = image.Ancestors().Any(element => element.Name.LocalName == "defs")
            ? page.Descendants().Single(element => element.Name.LocalName == "use"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "href"
                    && attribute.Value == "#" + (string?)image.Attribute("id")))
            : image;
        if (!ReferenceEquals(placement, image))
            transform = Transform((string?)image.Attribute("transform"));
        foreach (var element in placement.AncestorsAndSelf())
            transform *= Transform((string?)element.Attribute("transform"));
        var width = Number((string)image.Attribute("width")!);
        var height = Number((string)image.Attribute("height")!);
        var corners = new[] { new Vector2(0, 0), new Vector2(width, 0), new Vector2(0, height), new Vector2(width, height) }
            .Select(point => Vector2.Transform(point, transform)).ToArray();
        return new Bounds(corners.Min(point => point.X), corners.Min(point => point.Y),
            corners.Max(point => point.X), corners.Max(point => point.Y));
    }

    private static Matrix3x2 Transform(string? value)
    {
        var result = Matrix3x2.Identity;
        foreach (Match match in Regex.Matches(value ?? string.Empty, @"(\w+)\(([^)]+)\)"))
        {
            var values = Regex.Split(match.Groups[2].Value.Trim(), @"[ ,]+").Select(Number).ToArray();
            var operation = match.Groups[1].Value switch
            {
                "matrix" => new Matrix3x2(values[0], values[1], values[2], values[3], values[4], values[5]),
                "translate" => Matrix3x2.CreateTranslation(values[0], values.Length > 1 ? values[1] : 0),
                "scale" => Matrix3x2.CreateScale(values[0], values.Length > 1 ? values[1] : values[0]),
                _ => throw new InvalidOperationException("Unexpected SVG image transform: " + match.Value)
            };
            result = operation * result;
        }
        return result;
    }

    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    private sealed record Bounds(float Left, float Top, float Right, float Bottom)
    {
        public float Width => Right - Left;
        public float Height => Bottom - Top;
    }
}
