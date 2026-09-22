using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Run with: dotnet run --project EvoOffer.Tests
// Exercises the production model and view model without a native MAUI runtime.
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException(description);
    checks++;
}

foreach (var culture in new[] { "en-US", "ro-RO" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    foreach (var (text, expected) in new (string, decimal)[]
        { ("0", 0m), ("100", 100m), (" 21 ", 21m), ("9.5", 9.5m), ("9,5", 9.5m), ("19.25", 19.25m) })
    {
        Check(VatRateValue.TryParse(text, out var rate) && rate == expected, $"Parse {text} in {culture}");
        Check(VatRateValue.TryParse(VatRateValue.Format(expected), out var restored) && restored == expected,
            "Saved VAT rate round trip");
    }
    foreach (var text in new[] { null, "", " ", "abc", "-1", "100.01", "1.234", "1,000", "21%", "1e1", "1,2.3" })
        Check(!VatRateValue.TryParse(text, out _), $"Reject invalid VAT rate: {text}");
}

foreach (var email in new[] { null, "", " ", "office@example.com", "  billing+offers@sub.example.ro  ", "first.last@example.co.uk" })
    Check(ContactDataValue.IsValidEmail(email), $"Accept optional or valid e-mail: {email}");
foreach (var email in new[] { "office", "office@", "@example.com", "office@example", "office@example.",
    "office name@example.com", "office@@example.com", "Name <office@example.com>",
    "one@example.com,two@example.com", "office\n@example.com" })
    Check(!ContactDataValue.IsValidEmail(email), $"Reject malformed e-mail: {email}");
foreach (var (input, expected) in new (string?, string)[]
    { (null, ""), ("", ""), ("00123456789012345678901234567890", "00123456789012345678901234567890"),
      ("+40 (0722) 123-456", "400722123456"), ("RO00123456", "00123456"),
      ("12abc34", "1234"), ("a. -+\n", ""), ("١٢３4", "4") })
    Check(ContactDataValue.DigitsOnly(input) == expected, "Phone and VAT input keeps only ASCII digits and preserves leading zeros");

var line = new OfferLineItem(1, new CatalogItem("Test", "Item", 180m), 10m, 21m);
Check(line.UnitPrice == 180m && line.NetTotal == 1800m && line.VatAmount == 378m && line.Total == 2178m,
    "VAT is calculated on the whole line while unit price stays VAT-exclusive");
var notifications = new HashSet<string?>();
line.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
line.Quantity = 2.5m;
Check(line.NetTotal == 450m && line.VatAmount == 94.5m && line.Total == 544.5m, "Fractional quantities");
Check(new[] { "Quantity", "NetTotal", "VatAmount", "VatAmountText", "Total", "TotalText" }.All(notifications.Contains),
    "Quantity changes update all displayed amounts");
notifications.Clear();
line.VatRate = 9.5m;
Check(line.VatAmount == 42.75m && line.Total == 492.75m, "Rate changes recalculate VAT");
Check(new[] { "VatRate", "VatAmount", "VatAmountText", "Total", "TotalText" }.All(notifications.Contains),
    "Rate changes update displayed amounts");
line.QuantityText = "invalid";
Check(line.HasQuantityError && line.Total == 492.75m, "Invalid quantities preserve last valid amounts");
line.Quantity = 2m;
Check(!line.HasQuantityError && line.Total == 394.2m, "Corrected quantity recalculates");
line.VatRate = 0m;
Check(line.VatAmount == 0m && line.Total == line.NetTotal, "Zero VAT");
line.VatRate = 100m;
Check(line.Total == 2m * line.NetTotal, "Upper VAT boundary");
var rounded = new OfferLineItem(1, new CatalogItem("Test", "Rounding", 0.05m), 1m, 10m);
Check(rounded.VatAmount == 0.01m && rounded.Total == 0.06m, "VAT rounds half cents away from zero");
rounded.Quantity = 1.1m;
Check(rounded.NetTotal == 0.06m && rounded.VatAmount == 0.01m && rounded.Total == 0.07m,
    "Rounded net and VAT add up to the displayed total");

var vm = new MainViewModel(21m);
Check(vm.Items.All(item => item.VatRate == 21m) && vm.GrandTotal == 4416.5m, "Initial offer uses saved rate");
var totalChanges = 0;
vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.GrandTotalText)) totalChanges++; };
vm.VatRate = 9.5m;
Check(vm.Items.All(item => item.VatRate == 9.5m) && vm.GrandTotal == 3996.75m && totalChanges > 0,
    "Changing settings recalculates existing items and the footer");
Check(vm.VatHeaderText.Contains("9.5%"), "Header displays active rate");
vm.AddCommand.Execute(null);
Check(vm.Items.Last().VatRate == 9.5m && vm.Items.Last().Total == 197.1m, "Added items use current rate");
vm.Items.Last().Quantity = 2m;
Check(vm.GrandTotal == 4390.95m, "Editing quantity updates grand total");
vm.DeleteCommand.Execute(vm.Items.Last());
Check(vm.GrandTotal == 3996.75m, "Deleting item updates grand total");
vm.ResetCommand.Execute(null);
Check(vm.GrandTotal == 0m && vm.VatRate == 9.5m, "Reset preserves VAT settings");
vm.AddCommand.Execute(null);
Check(vm.Items.Single().Total == 197.1m, "Offer after reset uses saved rate");
vm.ClientName = "Client";
vm.Items.Single().QuantityText = "invalid";
Check(!vm.TryValidateOffer(out _), "Invalid quantity blocks preview");
foreach (var invalid in new[] { -1m, 101m, 2.345m })
{
    var rejected = false;
    try { vm.VatRate = invalid; }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected && vm.VatRate == 9.5m, "Invalid rates cannot enter the model");
}

var settingsDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-settings-tests-" + Guid.NewGuid());
try
{
    var store = new SettingsStore(settingsDirectory);
    var defaults = store.Load();
    Check(defaults.Language == "Română" && defaults.IssuerName == string.Empty,
        "First launch defaults to Romanian and an empty issuer");
    Check(File.Exists(store.FilePath), "First launch creates the config file");

    store.Save(new AppSettings
    {
        IssuerName = "Ștefan & Asociații",
        Email = "  office+offers@example.ro  ",
        PhoneNumber = "00722123456",
        AddressLine1 = "Strada Ștefan cel Mare 12",
        AddressLine2 = "Etaj 2, București, 010101",
        VatNumber = "00123456789012345678901234567890",
        Language = AppSettings.English,
        VatRate = 9.5m,
        DefaultMessage = "Ofertă personalizată\nThank you!"
    });
    var restored = new SettingsStore(settingsDirectory).Load();
    Check(restored.IssuerName == "Ștefan & Asociații" && restored.Language == AppSettings.English
        && restored.VatRate == 9.5m && restored.DefaultMessage == "Ofertă personalizată\nThank you!",
        "A fresh store restores all saved settings, including Unicode and multiline text");
    Check(restored.Email == "office+offers@example.ro" && restored.PhoneNumber == "00722123456"
        && restored.AddressLine1 == "Strada Ștefan cel Mare 12" && restored.AddressLine2 == "Etaj 2, București, 010101"
        && restored.VatNumber == "00123456789012345678901234567890",
        "Contact data survives reload, trims e-mail whitespace, and preserves long identifiers and leading zeros");
    restored.Language = AppSettings.Romanian;
    restored.IssuerName = string.Empty;
    restored.DefaultMessage = string.Empty;
    restored.Email = string.Empty;
    restored.PhoneNumber = string.Empty;
    restored.AddressLine1 = string.Empty;
    restored.AddressLine2 = string.Empty;
    restored.VatNumber = string.Empty;
    store.Save(restored);
    restored = new SettingsStore(settingsDirectory).Load();
    Check(restored.Language == AppSettings.Romanian && restored.IssuerName == string.Empty
        && restored.DefaultMessage == string.Empty, "Saving again replaces the config and preserves intentional empty values");
    Check(restored.Email == string.Empty && restored.PhoneNumber == string.Empty
        && restored.AddressLine1 == string.Empty && restored.AddressLine2 == string.Empty && restored.VatNumber == string.Empty,
        "Clearing contact data is persisted");

    // A failed write must leave the previous config usable.
    Directory.CreateDirectory(store.FilePath + ".tmp");
    var writeFailed = false;
    try { store.Save(new AppSettings { IssuerName = "Unsaved" }); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { writeFailed = true; }
    Check(writeFailed && new SettingsStore(settingsDirectory).Load().IssuerName == string.Empty,
        "Failed writes preserve the last saved settings");
    Directory.Delete(store.FilePath + ".tmp");

    File.WriteAllText(store.FilePath, "{\"IssuerName\":\"Existing issuer\"}");
    restored = store.Load();
    Check(restored.IssuerName == "Existing issuer" && restored.Language == AppSettings.Romanian,
        "Missing fields receive defaults");
    Check(restored.Email == string.Empty && restored.PhoneNumber == string.Empty
        && restored.AddressLine1 == string.Empty && restored.AddressLine2 == string.Empty && restored.VatNumber == string.Empty,
        "Existing settings files load with empty contact fields");
    File.WriteAllText(store.FilePath, "{\"IssuerName\":null,\"Email\":null,\"PhoneNumber\":null,\"AddressLine1\":null,\"AddressLine2\":null,\"VatNumber\":null,\"Language\":\"unsupported\",\"VatRate\":101,\"DefaultMessage\":null}");
    restored = store.Load();
    Check(restored.Language == AppSettings.Romanian && restored.VatRate == VatRateValue.Default
        && restored.IssuerName == string.Empty && restored.DefaultMessage == MainViewModel.DefaultCustomText,
        "Invalid config values fall back to valid defaults");
    Check(restored.Email == string.Empty && restored.PhoneNumber == string.Empty
        && restored.AddressLine1 == string.Empty && restored.AddressLine2 == string.Empty && restored.VatNumber == string.Empty,
        "Null contact fields normalize to empty values");

    File.WriteAllText(store.FilePath, "{broken");
    var readFailed = false;
    try { store.Load(); }
    catch (JsonException) { readFailed = true; }
    Check(readFailed && File.ReadAllText(store.FilePath) == "{broken",
        "Malformed config is reported without silently overwriting the file");

    File.Delete(store.FilePath);
    store.Load(new AppSettings { VatRate = 21m, DefaultMessage = "Previous message" });
    restored = new SettingsStore(settingsDirectory).Load(new AppSettings { VatRate = 5m });
    Check(restored.VatRate == 21m && restored.DefaultMessage == "Previous message"
        && restored.Language == AppSettings.Romanian,
        "Legacy settings migrate once; the config takes precedence on later launches");
}
finally
{
    if (Directory.Exists(settingsDirectory))
        Directory.Delete(settingsDirectory, recursive: true);
}

var pdfIssuer = new AppSettings
{
    IssuerName = "Ștefan & Asociații",
    Email = "office@example.ro",
    PhoneNumber = "00722123456",
    AddressLine1 = "Strada Ștefan cel Mare 12",
    AddressLine2 = "București",
    VatNumber = "00123456"
};
var pdfItems = new List<OfferLineItem>
{
    new(1, new CatalogItem("Instalații", "Țeavă și îmbinări", 180m), 2.5m, 9.5m),
    new(2, new CatalogItem("Materiale", "Șurub", 0.05m), 1.1m, 10m)
};
var pdfOffer = new OfferPdfData("  Client român  ", "Vă mulțumim!\nOfertă valabilă 30 de zile.", pdfItems, pdfIssuer);
pdfItems[0].Number = 99;
pdfItems[0].Quantity = 100m;
pdfItems[0].VatRate = 21m;
pdfItems.Clear();
pdfIssuer.IssuerName = "Changed issuer";
pdfIssuer.Email = pdfIssuer.PhoneNumber = pdfIssuer.AddressLine1 = pdfIssuer.AddressLine2 = pdfIssuer.VatNumber = string.Empty;
Check(pdfOffer.ClientName == "Client român" && pdfOffer.Message.Contains("Vă mulțumim!")
    && pdfOffer.IssuerName == "Ștefan & Asociații"
    && pdfOffer.IssuerContactLines.SequenceEqual(new[]
        { "Strada Ștefan cel Mare 12", "București", "office@example.ro", "00722123456", "VAT number: 00123456" }),
    "PDF snapshot preserves issuer contact settings and Romanian client/message text after edits");
Check(pdfOffer.Items.Count == 2 && pdfOffer.Items[0].Number == 1 && pdfOffer.Items[0].Quantity == 2.5m
    && pdfOffer.Items[0].VatRate == 9.5m && pdfOffer.Items[0].Name == "Țeavă și îmbinări"
    && pdfOffer.Subtotal == 450.06m && pdfOffer.VatTotal == 42.76m && pdfOffer.GrandTotal == 492.82m,
    "PDF snapshot preserves items and rounded totals after quantity/rate changes and collection reset");

void CheckArgumentRejected(Action action, string description)
{
    var rejected = false;
    try { action(); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, description);
}

var invalidPdfItem = new OfferLineItem(1, new CatalogItem("Test", "Invalid quantity", 1m), 1m)
    { QuantityText = "invalid" };
CheckArgumentRejected(() => new OfferPdfData("Client", null, new[] { invalidPdfItem }, pdfIssuer),
    "PDF generation cannot snapshot a stale amount from an invalid quantity");
CheckArgumentRejected(() => new OfferPdfData("Client", null, Array.Empty<OfferLineItem>(), pdfIssuer),
    "Empty offers cannot become PDF snapshots");
CheckArgumentRejected(() => new OfferPdfData(" ", null, new[] { line }, pdfIssuer),
    "PDF snapshots require a client");

var pdfDefaults = new OfferPdfOptions();
foreach (var invalidOptions in new[]
    {
        pdfDefaults with { Margin = -1 }, pdfDefaults with { Margin = 1000 },
        pdfDefaults with { FontSize = 0 }, pdfDefaults with { FontSize = float.NaN },
        pdfDefaults with { FontFamily = " " }, pdfDefaults with { Title = "" },
        pdfDefaults with { PageSize = new PageSize(0, 600) }
    })
    CheckArgumentRejected(() => new OfferPdfService(invalidOptions), "Invalid PDF defaults are rejected before rendering");

var pdfService = new OfferPdfService(pdfDefaults);
using (var readOnlyOutput = new MemoryStream(new byte[1], writable: false))
    CheckArgumentRejected(() => pdfService.Generate(pdfOffer, readOnlyOutput), "Read-only PDF output streams are rejected");
using (var invalidOutput = new MemoryStream())
{
    CheckArgumentRejected(() => pdfService.Generate(pdfOffer, invalidOutput, pdfDefaults with { Margin = float.NaN }),
        "Invalid per-document PDF configuration is rejected");
    Check(invalidOutput.Length == 0, "Invalid PDF configuration leaves the output stream untouched");
}

QuestPDF.Settings.License = LicenseType.Evaluation;
Check(pdfService.IsSupported, "The desktop regression host supports the QuestPDF renderer");
var pdfBytes = pdfService.Generate(pdfOffer);
var pdfText = Encoding.Latin1.GetString(pdfBytes);
Check(pdfText.StartsWith("%PDF-", StringComparison.Ordinal) && pdfText.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal)
    && Regex.IsMatch(pdfText, @"/Type\s*/Page\b"), "Romanian text and rounded offer amounts produce a complete PDF");

var landscapeOptions = pdfDefaults with
{
    PageSize = PageSizes.A4.Landscape(),
    Margin = 24,
    FontSize = 11,
    AccentColor = Color.FromHex("#284A38"),
    Title = "Custom offer",
    FooterText = "Vă mulțumim pentru încredere!",
    ShowPageNumbers = false
};
var landscapeOffer = new OfferPdfData("Landscape client", pdfOffer.Message,
    new[] { new OfferLineItem(1, new CatalogItem("Instalații", "Țeavă și îmbinări", 180m), 2.5m, 9.5m) },
    new AppSettings { IssuerName = pdfOffer.IssuerName });
using (var output = new MemoryStream())
{
    pdfService.Generate(landscapeOffer, output, landscapeOptions);
    Check(output.CanWrite && output.Length > 0, "PDF generation leaves the caller-owned stream open");
    var customizedPdf = Encoding.Latin1.GetString(output.ToArray());
    var mediaBoxes = Regex.Matches(customizedPdf, @"/MediaBox\s*\[\s*0(?:\.0+)?\s+0(?:\.0+)?\s+([0-9.]+)\s+([0-9.]+)\s*\]");
    Check(mediaBoxes.Count > 0 && mediaBoxes.All(box =>
        Math.Abs(float.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture) - landscapeOptions.PageSize.Width) < 1
        && Math.Abs(float.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture) - landscapeOptions.PageSize.Height) < 1),
        "Per-document landscape dimensions are applied to the generated PDF");
    const string expectedTitle = "Custom offer - Landscape client";
    Check(customizedPdf.Contains(expectedTitle, StringComparison.Ordinal)
        || customizedPdf.Contains(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(expectedTitle)), StringComparison.OrdinalIgnoreCase),
        "Custom title and client are included in PDF metadata");
    output.WriteByte(0);
    Check(output.CanWrite, "The caller can continue using its output stream after generation");
}

var longOffer = new OfferPdfData("Client cu ofertă detaliată", "Lucrări și materiale pentru renovarea clădirii.",
    Enumerable.Range(1, 150).Select(number => new OfferLineItem(number,
        new CatalogItem("Instalații și materiale", $"Reper {number}: țeavă, îmbinări și accesorii pentru încălzire", 12.34m), 2.5m, 21m)),
    new AppSettings { IssuerName = "Ștefan & Asociații" });
var longPdfText = Encoding.Latin1.GetString(pdfService.Generate(longOffer,
    pdfDefaults with { FooterText = "Ofertă detaliată — București" }));
Check(Regex.Matches(longPdfText, @"/Type\s*/Page\b").Count > 1
    && longPdfText.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal),
    "Long Romanian offers render across multiple pages with a footer and page numbering");

Console.WriteLine($"Passed {checks} regression checks.");
