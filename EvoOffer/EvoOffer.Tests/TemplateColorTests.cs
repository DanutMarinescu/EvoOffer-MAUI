using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EvoOffer.Models;
using EvoOffer.Services;
using QuestPDF.Fluent;
using Color = QuestPDF.Infrastructure.Color;

public static class TemplateColorTests
{
    private const string Primary = "#713C91";
    private const string Secondary = "#A05E22";
    private const string Text = "#243E35";
    private const string Override = "#236BA1";

    public static void Run(Action<bool, string> check)
    {
        CheckNormalization(check);
        CheckPersistence(check);
        CheckSnapshots(check);
        CheckRendering(check);
    }

    private static void CheckNormalization(Action<bool, string> check)
    {
        foreach (var (input, expected) in new[]
        {
            ("#123456", "#123456"), ("abcdef", "#ABCDEF"),
            ("#aBc", "#AABBCC"), ("  1aF  ", "#11AAFF"),
            ("#000", "#000000"), ("fff", "#FFFFFF")
        })
            check(PdfColorValue.TryNormalize(input, out var result) && result == expected,
                $"Template color {input} normalizes to an opaque six-digit hex value");

        foreach (var input in new string?[] { null, "", " ", "red", "#12", "#1234", "#12345G",
            "#12345678", "##123456", "rgb(1,2,3)", "#12 456", "#１２３４５６" })
            check(!PdfColorValue.TryNormalize(input, out _)
                && PdfColorValue.Normalize(input, Primary) == Primary,
                $"Invalid template color falls back safely: {input}");
    }

    private static void CheckPersistence(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoOffer-palette-tests-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(directory);
            CheckDefaults(store.Load(), check, "First-launch settings retain the original template palette");
            File.WriteAllText(store.FilePath, "{\"IssuerName\":\"Existing company\",\"PdfTemplateId\":\"modern\"}");
            var legacy = store.Load();
            CheckDefaults(legacy, check, "Existing settings files receive the original palette when colors are absent");
            check(legacy.IssuerName == "Existing company" && legacy.PdfTemplateId == OfferPdfTemplates.Modern,
                "Adding template colors preserves previously saved company and template settings");

            store.Save(new AppSettings
            {
                PdfPrimaryColor = "  713c91  ", PdfSecondaryColor = "#a05e22", PdfTextColor = "#243e35"
            });
            var restored = new SettingsStore(directory).Load();
            check(restored.PdfPrimaryColor == Primary && restored.PdfSecondaryColor == Secondary
                && restored.PdfTextColor == Text,
                "All three custom template colors are normalized and restored by a fresh settings store");

            foreach (var invalid in new string?[] { null, "", " ", "not-a-color", "#12345678" })
            {
                File.WriteAllText(store.FilePath, JsonSerializer.Serialize(new
                {
                    PdfPrimaryColor = invalid, PdfSecondaryColor = invalid, PdfTextColor = invalid
                }));
                CheckDefaults(store.Load(), check, "Invalid saved template colors receive their own defaults");
                store.Save(new AppSettings
                {
                    PdfPrimaryColor = invalid!, PdfSecondaryColor = invalid!, PdfTextColor = invalid!
                });
                CheckDefaults(new SettingsStore(directory).Load(), check,
                    "Saving invalid template colors persists a usable palette");
            }

            store.Save(new AppSettings { PdfPrimaryColor = Primary, PdfSecondaryColor = "broken", PdfTextColor = Text });
            restored = store.Load();
            check(restored.PdfPrimaryColor == Primary && restored.PdfSecondaryColor == PdfColorValue.DefaultSecondary
                && restored.PdfTextColor == Text,
                "An invalid color does not reset the other custom template colors");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckSnapshots(Action<bool, string> check)
    {
        var settings = CreateSettings(OfferPdfTemplates.Classic);
        var snapshot = CreateOffer(settings);
        settings.PdfPrimaryColor = settings.PdfSecondaryColor = settings.PdfTextColor = "#FFFFFF";
        check(snapshot.PdfPrimaryColor == Primary && snapshot.PdfSecondaryColor == Secondary
            && snapshot.PdfTextColor == Text,
            "An offer retains its palette after subsequent settings edits");

        var draft = new AppSettings
        {
            PdfPrimaryColor = "713c91", PdfSecondaryColor = "#a05e22", PdfTextColor = "#243e35"
        };
        var preview = OfferPdfTemplatePreview.Create(draft);
        check(preview.PdfPrimaryColor == Primary && preview.PdfSecondaryColor == Secondary
            && preview.PdfTextColor == Text && draft.PdfPrimaryColor == "713c91"
            && draft.PdfSecondaryColor == "#a05e22" && draft.PdfTextColor == "#243e35",
            "Settings preview snapshots draft colors without normalizing or mutating the draft");
        draft.PdfPrimaryColor = "invalid";
        draft.PdfSecondaryColor = null!;
        draft.PdfTextColor = "";
        var invalidSnapshot = CreateOffer(draft);
        check(invalidSnapshot.PdfPrimaryColor == PdfColorValue.DefaultPrimary
            && invalidSnapshot.PdfSecondaryColor == PdfColorValue.DefaultSecondary
            && invalidSnapshot.PdfTextColor == PdfColorValue.DefaultText
            && draft.PdfPrimaryColor == "invalid" && draft.PdfSecondaryColor is null && draft.PdfTextColor == "",
            "Offer snapshots safely resolve invalid draft colors without modifying settings");
    }

    private static void CheckRendering(Action<bool, string> check)
    {
        var defaults = new OfferPdfOptions();
        var service = new OfferPdfService(defaults);
        foreach (var template in OfferPdfTemplates.All)
        {
            var offer = CreateOffer(CreateSettings(template.Id));
            var colors = RenderedColors(offer, defaults);
            check(new[] { Primary, Secondary, Text }.All(colors.Contains),
                $"The {template.Id} template renders all three selected colors without explicit PDF overrides");

            var pdf = Encoding.Latin1.GetString(service.Generate(offer));
            check(pdf.StartsWith("%PDF-", StringComparison.Ordinal)
                && pdf.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal),
                $"The {template.Id} template exports a complete PDF with custom colors");

            foreach (var (options, replaced, retained) in new[]
            {
                (defaults with { AccentColor = Color.FromHex(Override) }, Primary, new[] { Secondary, Text }),
                (defaults with { SecondaryColor = Color.FromHex(Override) }, Secondary, new[] { Primary, Text }),
                (defaults with { TextColor = Color.FromHex(Override) }, Text, new[] { Primary, Secondary })
            })
            {
                var overridden = RenderedColors(offer, options);
                check(overridden.Contains(Override) && !overridden.Contains(replaced)
                    && retained.All(overridden.Contains),
                    $"The {template.Id} template respects an independent color override while retaining the other saved colors");
            }
            check(offer.PdfPrimaryColor == Primary && offer.PdfSecondaryColor == Secondary && offer.PdfTextColor == Text,
                $"Rendering {template.Id} with overrides does not modify the offer palette");

            var baseTemplate = OfferPdfTemplates.GetBaseTemplateId(template.Id);
            if (baseTemplate is OfferPdfTemplates.Classic or OfferPdfTemplates.Modern)
            {
                var label = baseTemplate == OfferPdfTemplates.Classic ? "Product" : "Commercial offer";
                check(RenderedLabelColors(offer, defaults, label).All(color => color == "#FFFFFF"),
                    $"The {template.Id} template uses readable white labels on a dark primary background");
                var light = defaults with { AccentColor = Color.FromHex("#FFF6CC") };
                check(RenderedLabelColors(offer, light, label).All(color => color == "#000000"),
                    $"The {template.Id} template switches labels to black on a light primary background");
            }
        }
    }

    private static HashSet<string> RenderedColors(OfferPdfData offer, OfferPdfOptions options) =>
        new OfferPdfTemplate(offer, options, null).GenerateSvg()
            .SelectMany(page => XDocument.Parse(page).Descendants().Attributes()
                .Where(attribute => attribute.Name.LocalName is "fill" or "stroke")
                .Select(attribute => attribute.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string[] RenderedLabelColors(OfferPdfData offer, OfferPdfOptions options, string label)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        var labels = new OfferPdfTemplate(offer, options, null).GenerateSvg()
            .SelectMany(page => XDocument.Parse(page).Descendants(svg + "text"))
            .Where(element => element.Value.Trim() == label).ToArray();
        if (labels.Length == 0)
            throw new InvalidOperationException($"The {offer.PdfTemplateId} SVG did not contain the expected label: {label}");
        return labels.Select(element =>
        {
            var fill = element.AncestorsAndSelf().Select(ancestor => (string?)ancestor.Attribute("fill"))
                .FirstOrDefault(value => value is not null)?.ToUpperInvariant();
            // SVG may emit named white/black or omit the default black fill entirely.
            return fill switch
            {
                null or "BLACK" => "#000000",
                "WHITE" => "#FFFFFF",
                _ => PdfColorValue.TryNormalize(fill, out var normalized) ? normalized : fill
            };
        }).ToArray();
    }

    private static void CheckDefaults(AppSettings settings, Action<bool, string> check, string description) =>
        check(settings.PdfPrimaryColor == PdfColorValue.DefaultPrimary
            && settings.PdfSecondaryColor == PdfColorValue.DefaultSecondary
            && settings.PdfTextColor == PdfColorValue.DefaultText, description);

    private static AppSettings CreateSettings(string template) => new()
    {
        PdfTemplateId = template, PdfPrimaryColor = Primary, PdfSecondaryColor = Secondary, PdfTextColor = Text,
        Language = AppSettings.English, IssuerName = "EXAMPLE STUDIO", AddressLine1 = "23 Example Street",
        Email = "hello@example.com", PhoneNumber = "0721234567", VatNumber = "12345678",
        DefaultMessage = "Thank you for your interest in our products and services.\n\nPlease find our proposal below."
    };

    private static OfferPdfData CreateOffer(AppSettings settings) => new("Palette customer", "Personalized offer message",
        new[] { new OfferLineItem(1, new CatalogItem("Flooring", "Natural oak flooring", 120m), 12m, 21m) }, settings);

    internal static IEnumerable<OfferPdfSample> CreateSamples(OfferPdfOptions defaults)
    {
        foreach (var template in OfferPdfTemplates.All)
        {
            var settings = CreateSettings(template.Id);
            yield return new OfferPdfSample("palette-" + template.Id, OfferPdfTemplatePreview.Create(settings), defaults);
            if (OfferPdfTemplates.GetBaseTemplateId(template.Id) is OfferPdfTemplates.Classic or OfferPdfTemplates.Modern)
            {
                settings.PdfPrimaryColor = "#FFF6CC";
                yield return new OfferPdfSample("palette-light-" + template.Id,
                    OfferPdfTemplatePreview.Create(settings), defaults);
            }
        }
    }
}
