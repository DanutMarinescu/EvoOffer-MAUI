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
    Check(defaults.LogoPath is null && defaults.SaveDirectory == AppSettings.DefaultSaveDirectory
        && Path.IsPathFullyQualified(defaults.SaveDirectory),
        "First launch has no logo and a usable absolute PDF output directory");
    Check(File.Exists(store.FilePath), "First launch creates the config file");

    var selectedLogoPath = Path.Combine(settingsDirectory, "Logo assets", " logo final.png ");
    var selectedSaveDirectory = Path.Combine(settingsDirectory, "Oferte PDF ");
    store.Save(new AppSettings
    {
        IssuerName = "Ștefan & Asociații",
        LogoPath = selectedLogoPath,
        SaveDirectory = selectedSaveDirectory,
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
    Check(restored.LogoPath == selectedLogoPath && restored.SaveDirectory == selectedSaveDirectory,
        "Selected logo and PDF directory paths survive reload without altering valid path whitespace");
    restored.Language = AppSettings.Romanian;
    restored.LogoPath = null;
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
    Check(restored.LogoPath is null && restored.SaveDirectory == selectedSaveDirectory,
        "Clearing the optional logo is persisted without resetting the selected PDF directory");

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
    Check(restored.LogoPath is null && restored.SaveDirectory == AppSettings.DefaultSaveDirectory,
        "Settings from older versions receive the optional logo and default PDF directory");
    File.WriteAllText(store.FilePath, "{\"IssuerName\":null,\"Email\":null,\"PhoneNumber\":null,\"AddressLine1\":null,\"AddressLine2\":null,\"VatNumber\":null,\"Language\":\"unsupported\",\"VatRate\":101,\"DefaultMessage\":null}");
    restored = store.Load();
    Check(restored.Language == AppSettings.Romanian && restored.VatRate == VatRateValue.Default
        && restored.IssuerName == string.Empty && restored.DefaultMessage == MainViewModel.DefaultCustomText,
        "Invalid config values fall back to valid defaults");
    Check(restored.Email == string.Empty && restored.PhoneNumber == string.Empty
        && restored.AddressLine1 == string.Empty && restored.AddressLine2 == string.Empty && restored.VatNumber == string.Empty,
        "Null contact fields normalize to empty values");

    foreach (var config in new[]
        { "{\"LogoPath\":null,\"SaveDirectory\":null}", "{\"LogoPath\":\"  \",\"SaveDirectory\":\"  \"}" })
    {
        File.WriteAllText(store.FilePath, config);
        restored = store.Load();
        Check(restored.LogoPath is null && restored.SaveDirectory == AppSettings.DefaultSaveDirectory,
            "Missing or blank file settings restore an absent logo and the default PDF directory");
    }

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

var logoDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-logo-tests-" + Guid.NewGuid());
try
{
    Directory.CreateDirectory(logoDirectory);
    var logoPath = Path.Combine(logoDirectory, "company logo.png");
    // A solid 2 × 2 RGB PNG keeps this test independent of external artwork.
    File.WriteAllBytes(logoPath, Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGPQ8LIAIgYIBQASZgKp0V582AAAAABJRU5ErkJggg=="));
    var logoSettings = new AppSettings { IssuerName = "Logo issuer", LogoPath = logoPath };
    var logoOffer = new OfferPdfData("Logo client", null, new[] { line }, logoSettings);
    logoSettings.LogoPath = null;
    Check(logoOffer.LogoPath == logoPath,
        "An offer keeps its selected logo after subsequent settings changes");
    var logoPdfText = Encoding.Latin1.GetString(pdfService.Generate(logoOffer));
    Check(Regex.IsMatch(logoPdfText, @"/Subtype\s*/Image\b")
        && logoPdfText.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal),
        "A selected PNG logo is embedded in the completed offer PDF");

    File.Delete(logoPath);
    using var missingLogoOutput = new MemoryStream();
    var missingLogoReported = false;
    try { pdfService.Generate(logoOffer, missingLogoOutput); }
    catch (FileNotFoundException) { missingLogoReported = true; }
    Check(missingLogoReported && missingLogoOutput.Length == 0,
        "A removed logo is reported before writing an incomplete PDF");
}
finally
{
    if (Directory.Exists(logoDirectory))
        Directory.Delete(logoDirectory, recursive: true);
}

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

var previewCacheDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-preview-tests-" + Guid.NewGuid());
var savedPdfDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-saved-pdf-tests-" + Guid.NewGuid());
try
{
    var unsupportedService = new PreviewPdfServiceStub { IsSupported = false };
    var unsupportedRejected = false;
    try { using var preview = await OfferPdfPreviewFile.CreateAsync(unsupportedService, pdfOffer, previewCacheDirectory); }
    catch (PlatformNotSupportedException) { unsupportedRejected = true; }
    Check(unsupportedRejected && unsupportedService.GenerateCalls == 0 && !Directory.Exists(previewCacheDirectory),
        "Unsupported preview platforms are rejected before rendering or creating cache files");

    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        var unusedService = new PreviewPdfServiceStub();
        var cancellationObserved = false;
        try { using var preview = await OfferPdfPreviewFile.CreateAsync(unusedService, pdfOffer, previewCacheDirectory, canceled.Token); }
        catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved && unusedService.GenerateCalls == 0 && !Directory.Exists(previewCacheDirectory),
            "Canceled preview requests do not render or create cache files");
    }

    var failure = new InvalidOperationException("PDF rendering failed after writing a partial file.");
    var failingService = new PreviewPdfServiceStub
    {
        WritePdf = output =>
        {
            output.Write(Encoding.ASCII.GetBytes("%PDF-partial"));
            throw failure;
        }
    };
    var renderingFailureObserved = false;
    try { using var preview = await OfferPdfPreviewFile.CreateAsync(failingService, pdfOffer, previewCacheDirectory); }
    catch (InvalidOperationException ex) when (ReferenceEquals(ex, failure)) { renderingFailureObserved = true; }
    Check(renderingFailureObserved && !Directory.EnumerateFiles(previewCacheDirectory, "*", SearchOption.AllDirectories).Any(),
        "Preview failures preserve the rendering error and remove partial PDF files");

    using (var canceled = new CancellationTokenSource())
    using (var finishRendering = new ManualResetEventSlim())
    {
        var renderingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderingUsedWorker = false;
        var pendingService = new PreviewPdfServiceStub
        {
            WritePdf = output =>
            {
                renderingUsedWorker = Thread.CurrentThread.IsThreadPoolThread;
                output.Write(Encoding.ASCII.GetBytes("%PDF-partial"));
                renderingStarted.SetResult();
                if (!finishRendering.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The test did not release PDF rendering.");
                output.Write(Encoding.ASCII.GetBytes("\n%%EOF"));
            }
        };
        var pendingPreview = OfferPdfPreviewFile.CreateAsync(pendingService, pdfOffer, previewCacheDirectory, canceled.Token);
        try
        {
            await renderingStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Check(renderingUsedWorker && !pendingPreview.IsCompleted,
                "Preview rendering runs on a worker and does not expose the PDF before it is complete");
            canceled.Cancel();
        }
        finally { finishRendering.Set(); }

        var cancellationObserved = false;
        try { using var preview = await pendingPreview; }
        catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved && !Directory.EnumerateFiles(previewCacheDirectory, "*", SearchOption.AllDirectories).Any(),
            "Canceling during synchronous PDF rendering waits for the writer and removes its file");
    }

    using var firstPreview = await OfferPdfPreviewFile.CreateAsync(pdfService, pdfOffer, previewCacheDirectory);
    using var secondPreview = await OfferPdfPreviewFile.CreateAsync(pdfService, longOffer, previewCacheDirectory);
    var firstPreviewText = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(firstPreview.FilePath));
    var secondPreviewText = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(secondPreview.FilePath));
    Check(firstPreview.FilePath != secondPreview.FilePath && Path.GetExtension(firstPreview.FilePath) == ".pdf"
        && Path.GetDirectoryName(firstPreview.FilePath) != previewCacheDirectory,
        "Each open preview owns a unique PDF in a dedicated cache subdirectory");
    Check(firstPreviewText.StartsWith("%PDF-", StringComparison.Ordinal)
        && firstPreviewText.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal)
        && Regex.Matches(secondPreviewText, @"/Type\s*/Page\b").Count > 1
        && secondPreviewText.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal),
        "Preview files contain complete, readable QuestPDF documents, including multiple pages");

    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        var canceledDirectory = Path.Combine(savedPdfDirectory, "Canceled save");
        var cancellationObserved = false;
        try { await firstPreview.SaveCopyAsync(canceledDirectory, canceled.Token); }
        catch (OperationCanceledException) { cancellationObserved = true; }
        Check(cancellationObserved && !Directory.Exists(canceledDirectory),
            "A canceled PDF save does not create a directory or partial document");
    }

    var savedPaths = await Task.WhenAll(firstPreview.SaveCopyAsync(savedPdfDirectory),
        firstPreview.SaveCopyAsync(savedPdfDirectory));
    Check(savedPaths.Distinct().Count() == 2 && savedPaths.All(path =>
        Path.GetDirectoryName(path) == savedPdfDirectory && Path.GetExtension(path) == ".pdf")
        && Directory.EnumerateFiles(savedPdfDirectory).Count() == 2,
        "Simultaneous PDF saves create distinct completed files in the selected directory");
    foreach (var savedPath in savedPaths)
        Check(Encoding.Latin1.GetString(await File.ReadAllBytesAsync(savedPath)) == firstPreviewText,
            "A saved PDF contains the complete preview without overwriting an earlier save");

    var blockedDirectory = Path.Combine(savedPdfDirectory, "Existing document.txt");
    await File.WriteAllTextAsync(blockedDirectory, "Keep this document");
    var invalidDestinationReported = false;
    try { await firstPreview.SaveCopyAsync(blockedDirectory); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { invalidDestinationReported = true; }
    Check(invalidDestinationReported && await File.ReadAllTextAsync(blockedDirectory) == "Keep this document"
        && Directory.EnumerateFiles(savedPdfDirectory).Count() == 3,
        "A failed PDF save preserves an existing destination file and leaves no partial output");
    File.Delete(blockedDirectory);

    firstPreview.Dispose();
    firstPreview.Dispose();
    Check(!File.Exists(firstPreview.FilePath) && File.Exists(secondPreview.FilePath),
        "Closing a preview removes only its own file and can be repeated safely");
    var failedSaveDirectory = Path.Combine(savedPdfDirectory, "Unavailable preview");
    var unavailablePreviewReported = false;
    try { await firstPreview.SaveCopyAsync(failedSaveDirectory); }
    catch (FileNotFoundException) { unavailablePreviewReported = true; }
    Check(unavailablePreviewReported && (!Directory.Exists(failedSaveDirectory)
        || !Directory.EnumerateFiles(failedSaveDirectory).Any()),
        "Saving an unavailable preview reports the error and cleans up its partial output");
    secondPreview.Dispose();
    Check(!Directory.EnumerateFiles(previewCacheDirectory, "*", SearchOption.AllDirectories).Any(),
        "Closing all previews removes all generated PDF files");
    Check(savedPaths.All(File.Exists), "Saved PDFs remain available after all previews are closed");
}
finally
{
    if (Directory.Exists(previewCacheDirectory))
        Directory.Delete(previewCacheDirectory, recursive: true);
    if (Directory.Exists(savedPdfDirectory))
        Directory.Delete(savedPdfDirectory, recursive: true);
}

Console.WriteLine($"Passed {checks} regression checks.");

sealed class PreviewPdfServiceStub : IOfferPdfService
{
    public bool IsSupported { get; init; } = true;
    public int GenerateCalls { get; private set; }
    public Action<Stream> WritePdf { get; init; } = _ => { };

    public byte[] Generate(OfferPdfData offer, OfferPdfOptions? options = null)
    {
        using var output = new MemoryStream();
        Generate(offer, output, options);
        return output.ToArray();
    }

    public void Generate(OfferPdfData offer, Stream output, OfferPdfOptions? options = null)
    {
        GenerateCalls++;
        WritePdf(output);
    }
}
