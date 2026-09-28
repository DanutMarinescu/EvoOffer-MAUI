using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

public static class LocalizationTests
{
    public static void Run(Action<bool, string> check)
    {
        var originalLanguage = LocalizationService.Language;
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalCatalog = CatalogStore.Current;
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "EvoOffer-localization-tests-" + Guid.NewGuid());
        var languageChanges = 0;
        EventHandler languageChanged = (_, _) => languageChanges++;
        LocalizationService.LanguageChanged += languageChanged;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            check(LocalizationService.Language == AppSettings.Romanian
                && LocalizationService.Get("StatusReady") == "Gata"
                && LocalizationService.Culture.Name == "ro-RO",
                "A fresh application uses Romanian independently of the operating system culture");
            CheckResourceParity(check);

            var catalog = CatalogCsvImporter.Import(new StringReader("""
                Denumire Produs;Pret;Categorie / Categorii;Atribute: Culoare (lista);Atribute: Cantitate_11 (lista)
                Produs românesc;1234.50;Produse;Alb;5L
                Produs românesc;2469;Produse;Negru;10L
                Etichete;100;Produse;;
                Etichete;200;Produse;—;—
                Etichete;300;Produse;— (not specified);— (not specified)
                Etichete;400;Produse;— (nespecificat);— (nespecificat)
                """), 19.5m);
            var vm = new MainViewModel(19.5m, catalog) { NewQuantity = 1.5m };
            vm.AddCommand.Execute(null);
            var line = vm.Items.Single();
            var variant = line.SelectedVariant;
            var total = line.Total;
            check(vm.Status == "S-a adăugat Produs românesc."
                && vm.DefaultMessage.StartsWith("Vă mulțumim", StringComparison.Ordinal)
                && vm.CustomText == vm.DefaultMessage
                && vm.VatHeaderText == "TVA 19,5%\n(RON)"
                && vm.GrandTotalSummary == "Total cu TVA: 1.851,75 RON"
                && vm.NewQuantityText == "1,5" && line.QuantityText == "1,5",
                "Romanian offers localize status, default message, headers, totals and fractional quantities");

            var vmNotifications = new HashSet<string?>();
            var lineNotifications = new HashSet<string?>();
            vm.PropertyChanged += (_, e) => vmNotifications.Add(e.PropertyName);
            line.PropertyChanged += (_, e) => lineNotifications.Add(e.PropertyName);
            LocalizationService.SetLanguage(AppSettings.English);
            vm.RefreshLocalization();
            check(languageChanges == 1 && vm.Status == "Added Produs românesc."
                && vm.VatHeaderText == "VAT 19.5%\n(RON)"
                && vm.GrandTotalSummary == "Total incl. VAT: 1,851.75 RON"
                && vm.NewQuantityText == "1.5" && line.QuantityText == "1.5"
                && vm.CustomText.StartsWith("Thank you", StringComparison.Ordinal)
                && line.RemoveDescription == "Remove Produs românesc",
                "Switching to English immediately refreshes existing offer text and number formats");
            check(new[] { "Status", "VatHeaderText", "GrandTotalText", "GrandTotalSummary", "NewQuantityText",
                    "NewQuantityValidationMessage", "CustomText", "DefaultMessage" }.All(vmNotifications.Contains)
                && new[] { "QuantityText", "QuantityValidationMessage", "UnitPriceText", "VatAmountText",
                    "TotalText", "RemoveDescription", "AvailableSizes", "AvailableColors", "SelectedSize", "SelectedColor" }
                    .All(lineNotifications.Contains),
                "Language refresh notifies every bound offer amount, validation, status and translated line label");
            check(ReferenceEquals(line.SelectedVariant, variant) && line.Total == total
                && line.Name == "Produs românesc" && line.SelectedColor == "Alb" && line.SelectedSize == "5L"
                && ReferenceEquals(vm.SelectedItem, catalog.Items[0]),
                "Language changes preserve imported product data, selected variants and calculated amounts");

            LocalizationService.SetLanguage(AppSettings.English);
            check(languageChanges == 1, "Saving an unchanged language does not fire a redundant language change");
            vm.ClientName = "Client personalizat";
            vm.DefaultMessage = "Mesaj implicit personalizat";
            vm.CustomText = "Mesaj pentru acest client";
            vm.NewQuantityText = "2..";
            line.QuantityText = "invalid";
            vmNotifications.Clear();
            lineNotifications.Clear();
            LocalizationService.SetLanguage(AppSettings.Romanian);
            vm.RefreshLocalization();
            check(vm.NewQuantityText == "2.." && vm.NewQuantityHasError
                && line.QuantityText == "invalid" && line.HasQuantityError
                && line.Quantity == 1.5m && line.Total == total
                && vm.NewQuantityValidationMessage == "Introduceți o cantitate între 1 și 999.999."
                && line.QuantityValidationMessage == vm.NewQuantityValidationMessage
                && vm.Status == "Articolul 1: Introduceți o cantitate între 1 și 999.999.",
                "Switching language translates active validation without discarding invalid input or the last valid amounts");
            check(vm.DefaultMessage == "Mesaj implicit personalizat" && vm.CustomText == "Mesaj pentru acest client"
                && vm.ClientName == "Client personalizat"
                && vmNotifications.Contains("NewQuantityValidationMessage")
                && lineNotifications.Contains("QuantityValidationMessage"),
                "Custom messages and client data survive language changes while validation bindings refresh");
            check(VatRateValue.ValidationMessage.StartsWith("Introduceți un procent TVA", StringComparison.Ordinal)
                && ContactDataValue.EmailValidationMessage.StartsWith("Introduceți o adresă de e-mail", StringComparison.Ordinal),
                "Settings validation follows the selected Romanian language");

            vm.Status = "Custom status from another source";
            LocalizationService.SetLanguage(AppSettings.English);
            vm.RefreshLocalization();
            check(vm.Status == "Custom status from another source",
                "A custom status is preserved instead of being replaced by a built-in translation");
            check(!vm.TryValidateOffer(out var invalidMessage)
                && invalidMessage == "Item 1: Enter a quantity between 1 and 999,999.",
                "Offer validation uses the currently selected language");

            CheckMissingAttributeLabels(catalog, check);
            CheckSettingsMigration(settingsDirectory, check);

            LocalizationService.SetLanguage("unsupported");
            check(LocalizationService.Language == AppSettings.Romanian
                && LocalizationService.Get("StatusReady") == "Gata"
                && LocalizationService.Get("StatusReady", null) == "Gata",
                "Unknown and missing language preferences consistently fall back to Romanian");
        }
        finally
        {
            LocalizationService.LanguageChanged -= languageChanged;
            LocalizationService.SetLanguage(originalLanguage);
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CatalogStore.Replace(originalCatalog);
            if (Directory.Exists(settingsDirectory))
                Directory.Delete(settingsDirectory, recursive: true);
        }
    }

    private static void CheckMissingAttributeLabels(ProductCatalog catalog, Action<bool, string> check)
    {
        LocalizationService.SetLanguage(AppSettings.English);
        var line = new OfferLineItem(1, catalog.Items.Single(item => item.Name == "Etichete"), 1m, 19.5m);
        var selectedVariant = line.SelectedVariant;
        check(line.SelectedSize == "— (not specified 2)" && line.SelectedColor == "— (not specified 2)"
            && line.AvailableSizes.Count == 4 && line.AvailableColors.Count == 4,
            "An English missing-attribute label remains distinct from literal CSV placeholder labels");

        // A picker can temporarily clear its selection while its ItemsSource is replaced.
        line.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(line.AvailableSizes))
                line.SelectedSize = "—";
        };
        LocalizationService.SetLanguage(AppSettings.Romanian);
        line.RefreshLocalization();
        check(line.SelectedSize == "— (nespecificat 2)" && line.SelectedColor == "— (nespecificat 2)"
            && line.AvailableSizes.Distinct(StringComparer.Ordinal).Count() == 4
            && line.AvailableColors.Distinct(StringComparer.Ordinal).Count() == 4
            && ReferenceEquals(line.SelectedVariant, selectedVariant),
            "Translating placeholders avoids collisions and preserves the chosen variant through picker notifications");
        line.SelectedSize = "— (not specified)";
        check(line.SelectedVariant.Size == "— (not specified)" && line.SelectedVariant.PriceIncludingVat == 300m,
            "Literal English CSV attribute text remains selectable in the Romanian interface");
    }

    private static void CheckSettingsMigration(string directory, Action<bool, string> check)
    {
        LocalizationService.SetLanguage(AppSettings.English);
        var settings = new AppSettings
        {
            Language = AppSettings.Romanian,
            DefaultMessage = MainViewModel.GetDefaultMessage(AppSettings.English)
        };
        settings.Normalize();
        check(settings.DefaultMessage == MainViewModel.GetDefaultMessage(AppSettings.Romanian)
            && LocalizationService.Language == AppSettings.English,
            "An old stock English message migrates using the saved Romanian language without changing the active UI");

        LocalizationService.SetLanguage(AppSettings.Romanian);
        settings.Language = AppSettings.English;
        settings.DefaultMessage = null!;
        settings.Normalize();
        check(settings.DefaultMessage == MainViewModel.GetDefaultMessage(AppSettings.English)
            && MainViewModel.IsDefaultMessage(settings.DefaultMessage),
            "A missing default message adopts the saved language independently of the active UI");
        settings.DefaultMessage = "My saved custom message";
        settings.Normalize();
        check(settings.DefaultMessage == "My saved custom message",
            "Normalizing language settings preserves a saved custom default message");
        settings.DefaultMessage = string.Empty;
        settings.Normalize();
        check(settings.DefaultMessage == string.Empty,
            "Normalizing language settings preserves an intentionally empty default message");

        var store = new SettingsStore(directory);
        settings.DefaultMessage = MainViewModel.GetDefaultMessage(AppSettings.English);
        store.Save(settings);
        var restored = new SettingsStore(directory).Load();
        LocalizationService.SetLanguage(restored.Language);
        var freshVm = new MainViewModel(catalog: ProductCatalog.Empty);
        check(restored.Language == AppSettings.English
            && freshVm.DefaultMessage == restored.DefaultMessage && freshVm.Status == "Ready",
            "Reloading a saved English preference restores English text in a new offer");
    }

    private static void CheckResourceParity(Action<bool, string> check)
    {
        var resources = new ResourceManager("EvoOffer.Resources.Strings.AppResources", typeof(LocalizationService).Assembly);
        var romanian = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
        var english = resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false)!
            .Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
        check(romanian.Count > 0 && romanian.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(english.Keys),
            "Every UI resource has both a Romanian default and an explicit English translation");
        var mismatchedFormats = romanian.Keys.Where(key =>
            CompositeFormat.Parse(romanian[key]).MinimumArgumentCount != CompositeFormat.Parse(english[key]).MinimumArgumentCount
            || !PlaceholderIndexes(romanian[key]).SequenceEqual(PlaceholderIndexes(english[key]))).ToArray();
        check(mismatchedFormats.Length == 0,
            "Romanian and English translations preserve all formatting arguments: " + string.Join(", ", mismatchedFormats));
        check(LocalizationService.GetStrings().Count == romanian.Count,
            "The dynamic UI resource dictionary includes every localized string");
    }

    private static IEnumerable<string> PlaceholderIndexes(string text) =>
        Regex.Matches(text, @"(?<!\{)\{(\d+)[^{}]*\}")
            .Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal);
}
