using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

public static class OfferPdfTemplateTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckSettings(check);
        var defaults = new OfferPdfOptions();
        var service = new OfferPdfService(defaults);
        var issuer = CreateIssuer(OfferPdfTemplates.Modern, AppSettings.English);
        var snapshot = CreateOffer(issuer);
        issuer.PdfTemplateId = OfferPdfTemplates.Minimal;
        check(snapshot.PdfTemplateId == OfferPdfTemplates.Modern,
            "An offer keeps its selected PDF template after subsequent settings edits");
        check(CreateOffer(new AppSettings { PdfTemplateId = "unavailable" }).PdfTemplateId == OfferPdfTemplates.Classic,
            "PDF snapshots normalize unavailable templates to the classic layout");

        foreach (var invalid in new[] { "", " ", "unavailable" })
        {
            var invalidOptions = defaults with { TemplateId = invalid };
            CheckArgumentRejected(() => new OfferPdfService(invalidOptions), check,
                "Invalid default PDF template overrides are rejected: " + invalid);
            using var output = new MemoryStream();
            CheckArgumentRejected(() => service.Generate(snapshot, output, invalidOptions), check,
                "Invalid per-document PDF template overrides are rejected: " + invalid);
            check(output.Length == 0, "A rejected template override leaves the destination untouched");
        }

        var selected = ContentStreams(service.Generate(snapshot));
        var explicitModern = ContentStreams(service.Generate(snapshot, defaults with { TemplateId = OfferPdfTemplates.Modern }));
        var explicitMinimal = ContentStreams(service.Generate(snapshot, defaults with { TemplateId = OfferPdfTemplates.Minimal }));
        check(selected.Length > 0 && selected.SequenceEqual(explicitModern),
            "PDF generation uses the offer snapshot's selected template when no override is supplied");
        check(!selected.SequenceEqual(explicitMinimal)
            && snapshot.PdfTemplateId == OfferPdfTemplates.Modern,
            "A per-document template override changes the rendered layout without changing the saved snapshot");
        var configuredService = new OfferPdfService(defaults with { TemplateId = OfferPdfTemplates.Minimal });
        check(ContentStreams(configuredService.Generate(snapshot)).SequenceEqual(explicitMinimal)
            && ContentStreams(configuredService.Generate(snapshot, defaults)).SequenceEqual(selected),
            "Service defaults can override the template, while per-document null selection uses the offer snapshot");

        var allSamples = OfferPdfSamples.Create(defaults);
        foreach (var sample in allSamples.Where(sample => sample.Name.StartsWith("template-", StringComparison.Ordinal)))
            check(CheckPdf(service.Generate(sample.Offer, sample.Options), check, sample.Name) == 1,
                $"The {sample.Name} settings preview keeps its sample products and totals on one page");
        var samples = allSamples.Where(sample =>
            sample.Name is "sample-multipage" or "sample-stress" or "sample-wrapped-names").ToArray();
        var logoDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-template-tests-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(logoDirectory);
            var logoPath = Path.Combine(logoDirectory, "logo.png");
            File.WriteAllBytes(logoPath, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGPQ8LIAIgYIBQASZgKp0V582AAAAABJRU5ErkJggg=="));
            foreach (var template in OfferPdfTemplates.All)
            {
                foreach (var language in new[] { AppSettings.Romanian, AppSettings.English })
                {
                    var offer = CreateOffer(CreateIssuer(template.Id, language));
                    CheckPdf(service.Generate(offer), check, $"{template.Id} / {language}");
                    CheckVisibleContent(offer, defaults, check);
                }
                foreach (var sample in samples)
                {
                    var bytes = service.Generate(sample.Offer, sample.Options with { TemplateId = template.Id });
                    var pageCount = CheckPdf(bytes, check, $"{template.Id} / {sample.Name}");
                    if (sample.Name is "sample-multipage" or "sample-stress")
                        check(pageCount > 1, $"The {template.Id} template safely paginates {sample.Name}");
                }

                var logoIssuer = CreateIssuer(template.Id, AppSettings.English);
                logoIssuer.LogoPath = logoPath;
                var logoPdf = service.Generate(CreateOffer(logoIssuer));
                CheckPdf(logoPdf, check, template.Id + " / logo");
                check(Regex.IsMatch(Encoding.Latin1.GetString(logoPdf), @"/Subtype\s*/Image\b"),
                    $"The {template.Id} template embeds the selected company logo");

                var customOptions = defaults with
                {
                    TemplateId = template.Id, PageSize = PageSizes.A4.Landscape(),
                    Title = "Custom proposal", FooterText = "Custom closing note",
                    Tagline = "Custom brand tagline", ValidUntil = new DateOnly(2026, 10, 22),
                    ShowPageNumbers = false
                };
                var customPdf = service.Generate(snapshot, customOptions);
                CheckPdf(customPdf, check, template.Id + " / custom options");
                var dimensions = Regex.Matches(Encoding.Latin1.GetString(customPdf),
                    @"/MediaBox\s*\[\s*0(?:\.0+)?\s+0(?:\.0+)?\s+([0-9.]+)\s+([0-9.]+)\s*\]");
                check(dimensions.Count > 0 && dimensions.All(box =>
                    Math.Abs(float.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture) - customOptions.PageSize.Width) < 1
                    && Math.Abs(float.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture) - customOptions.PageSize.Height) < 1),
                    $"The {template.Id} template respects custom landscape page dimensions");
                var customText = VisibleText(snapshot, customOptions);
                check(customText.Contains("Custom proposal") && customText.Contains("Custom closing note")
                    && customText.Contains("Custom brand tagline") && customText.Contains("22 October 2026")
                    && !customText.Contains("Page 1"),
                    $"The {template.Id} template retains title, tagline, validity and closing overrides without page numbering");
            }
        }
        finally
        {
            if (Directory.Exists(logoDirectory))
                Directory.Delete(logoDirectory, recursive: true);
        }

        CheckPreview(service, check);
    }

    private static void CheckSettings(Action<bool, string> check)
    {
        check(OfferPdfTemplates.All.Select(template => template.Id).SequenceEqual(new[]
            { OfferPdfTemplates.Classic, OfferPdfTemplates.Modern, OfferPdfTemplates.Minimal })
            && OfferPdfTemplates.All.All(template => !string.IsNullOrWhiteSpace(template.Name)
                && !string.IsNullOrWhiteSpace(template.Description) && !string.IsNullOrWhiteSpace(template.PreviewImage)),
            "The PDF template catalog exposes stable choices with names, descriptions and previews");
        var directory = Path.Combine(Path.GetTempPath(), "EvoOffer-template-settings-tests-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(directory);
            check(store.Load().PdfTemplateId == OfferPdfTemplates.Classic,
                "First-launch settings preserve the existing classic PDF template");
            foreach (var template in OfferPdfTemplates.All)
            {
                store.Save(new AppSettings { PdfTemplateId = template.Id });
                check(new SettingsStore(directory).Load().PdfTemplateId == template.Id,
                    $"The {template.Id} PDF template selection survives a settings reload");
            }
            File.WriteAllText(store.FilePath, "{\"IssuerName\":\"Legacy issuer\"}");
            var legacy = store.Load();
            check(legacy.IssuerName == "Legacy issuer" && legacy.PdfTemplateId == OfferPdfTemplates.Classic,
                "Settings files from before template selection continue using the classic template");
            foreach (var invalid in new string?[] { null, string.Empty, " ", "removed-template" })
            {
                File.WriteAllText(store.FilePath, JsonSerializer.Serialize(new { PdfTemplateId = invalid }));
                var settings = store.Load();
                check(settings.PdfTemplateId == OfferPdfTemplates.Classic
                    && OfferPdfTemplates.Normalize(invalid) == OfferPdfTemplates.Classic,
                    "Unavailable or empty saved template identifiers safely fall back to classic");
                settings.PdfTemplateId = invalid!;
                store.Save(settings);
                check(new SettingsStore(directory).Load().PdfTemplateId == OfferPdfTemplates.Classic,
                    "Saving an invalid template selection persists a usable fallback");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckPreview(OfferPdfService service, Action<bool, string> check)
    {
        var emptySettingsPreview = OfferPdfTemplatePreview.Create(new AppSettings());
        check(!string.IsNullOrWhiteSpace(emptySettingsPreview.ClientName) && emptySettingsPreview.Items.Count > 0,
            "Settings can preview a sample template before any offer or product catalog exists");
        foreach (var template in OfferPdfTemplates.All)
        {
            var settings = CreateIssuer(template.Id, AppSettings.English);
            var preview = OfferPdfTemplatePreview.Create(settings);
            settings.IssuerName = "Changed after preview";
            settings.PdfTemplateId = OfferPdfTemplates.Classic;
            check(preview.PdfTemplateId == template.Id && preview.Language == AppSettings.English
                && preview.IssuerName == "Template issuer" && preview.Email == "offers@example.com"
                && preview.Items.Count > 0,
                $"The {template.Id} settings preview snapshots the selected layout, language and company details");
            CheckPdf(service.Generate(preview), check, template.Id + " / settings preview");
        }
    }

    private static void CheckVisibleContent(OfferPdfData offer, OfferPdfOptions options, Action<bool, string> check)
    {
        var text = VisibleText(offer, options);
        var culture = CultureInfo.GetCultureInfo(offer.Language == AppSettings.English ? "en-GB" : "ro-RO");
        var expected = new[]
        {
            offer.ClientName, offer.IssuerName, offer.Message, offer.AddressLine1,
            offer.Email, offer.PhoneNumber, offer.VatNumber, "Oak flooring", "Installation",
            "600 x 90 mm", "Natural", offer.Subtotal.ToString("N2", culture) + " RON",
            offer.VatTotal.ToString("N2", culture) + " RON", offer.GrandTotal.ToString("N2", culture) + " RON"
        };
        check(expected.All(value => text.Contains(value, StringComparison.Ordinal)),
            $"The {offer.PdfTemplateId} template retains company, client, message, variants and calculated totals in {offer.Language}");
    }

    private static string VisibleText(OfferPdfData offer, OfferPdfOptions options)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        return Regex.Replace(string.Join(' ', new OfferPdfTemplate(offer, options, null).GenerateSvg()
            .SelectMany(page => XDocument.Parse(page).Descendants(svg + "text").Select(element => element.Value.Trim()))),
            @"\s+", " ");
    }

    private static AppSettings CreateIssuer(string template, string language) => new()
    {
        PdfTemplateId = template, Language = language, IssuerName = "Template issuer",
        AddressLine1 = "23 Example Street", AddressLine2 = "Bucharest",
        Email = "offers@example.com", PhoneNumber = "00722123456", VatNumber = "00123456"
    };

    private static OfferPdfData CreateOffer(AppSettings settings) => new("Template customer", "Personalized offer message",
        new[]
        {
            new OfferLineItem(1, new CatalogItem("Oak flooring",
                new[] { new CatalogVariant("600 x 90 mm", "Natural", 100m, 121m) }, new[] { "Flooring" }), 2.5m, 21m),
            new OfferLineItem(2, new CatalogItem("Services", "Installation", 40m), 2m, 9.5m)
        }, settings);

    private static int CheckPdf(byte[] bytes, Action<bool, string> check, string description)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var pages = Regex.Matches(text, @"/Type\s*/Page\b").Count;
        check(text.StartsWith("%PDF-", StringComparison.Ordinal)
            && text.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal) && pages > 0,
            "A complete PDF is generated for " + description);
        return pages;
    }

    // Compare document content independently of generated timestamps and document identifiers.
    private static string[] ContentStreams(byte[] bytes)
    {
        var pdf = Encoding.Latin1.GetString(bytes);
        return Regex.Matches(pdf, @"<<(?<dictionary>[^<>]*?)>>\s*stream\r?\n")
            .Where(match => match.Groups["dictionary"].Value.Contains("/FlateDecode", StringComparison.Ordinal))
            .Select(match =>
            {
                var length = int.Parse(Regex.Match(match.Groups["dictionary"].Value, @"/Length\s+(\d+)").Groups[1].Value,
                    CultureInfo.InvariantCulture);
                using var source = new MemoryStream(bytes, match.Index + match.Length, length);
                using var decoded = new ZLibStream(source, CompressionMode.Decompress);
                using var output = new MemoryStream();
                decoded.CopyTo(output);
                return Convert.ToBase64String(output.ToArray());
            }).ToArray();
    }

    private static void CheckArgumentRejected(Action action, Action<bool, string> check, string description)
    {
        var rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, description);
    }
}
