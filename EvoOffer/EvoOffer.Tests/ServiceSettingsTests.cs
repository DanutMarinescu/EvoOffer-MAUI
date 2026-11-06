using System.Globalization;
using EvoOffer.Models;
using EvoOffer.Services;

public static class ServiceSettingsTests
{
    public static void Run(Action<bool, string> check)
    {
        CheckDrafts(check);
        CheckPersistence(check);
    }

    private static void CheckDrafts(Action<bool, string> check)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalLanguage = LocalizationService.Language;
        try
        {
            foreach (var language in new[] { AppSettings.Romanian, AppSettings.English })
            {
                LocalizationService.SetLanguage(language);
                // Input follows the selected app language, independently of the operating system.
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language == AppSettings.English ? "ro-RO" : "en-US");
                foreach (var (text, expected) in new[]
                {
                    ("0", 0m), (" 1234.50 ", 1234.5m), ("1234,50", 1234.5m), ("0.01", 0.01m),
                    ("9999999.99", 9999999.99m), ("79228162514264337593543950335", decimal.MaxValue)
                })
                    check(ServiceDraft.TryParsePrice(text, out var value) && value == expected,
                        $"Service prices accept {text} while using {language}");

                foreach (var text in new string?[]
                {
                    null, "", " ", "abc", "-1", "+1", "1.234", "1,000", "1,234.50", "1.234,50",
                    "1 234", "1e2", "RON 1", "1%", "1..2", ".", "79228162514264337593543950336",
                    "79228162514264337593543950335.01"
                })
                    check(!ServiceDraft.TryParsePrice(text, out _), $"Service prices reject invalid input: {text}");

                var source = new ServiceDefinition { Name = "Installation", UnitOfMeasurement = "m²", PriceExcludingVat = 25.5m };
                var draft = new ServiceDraft(source);
                check(draft.PriceText == (language == AppSettings.English ? "25.5" : "25,5")
                    && draft.TryBuild(out var restored) && restored.PriceExcludingVat == 25.5m,
                    "Saved service prices display and round trip in the selected language");
                draft.Name = "  Edited installation  ";
                draft.UnitOfMeasurement = "  hour  ";
                draft.PriceText = "40,25";
                check(source.Name == "Installation" && source.UnitOfMeasurement == "m²" && source.PriceExcludingVat == 25.5m,
                    "Editing a service draft leaves saved data untouched when settings are canceled");
                check(draft.TryBuild(out var snapshot) && snapshot.Name == "Edited installation"
                    && snapshot.UnitOfMeasurement == "hour" && snapshot.PriceExcludingVat == 40.25m
                    && !ReferenceEquals(snapshot, source),
                    "A valid service draft builds an independent trimmed snapshot");
                draft.Name = "Later edit";
                draft.PriceText = "50";
                check(snapshot.Name == "Edited installation" && snapshot.PriceExcludingVat == 40.25m,
                    "Later draft edits cannot modify a built service snapshot");
            }

            var empty = new ServiceDraft();
            check(empty.HasNameError && empty.HasUnitError && empty.HasPriceError && !empty.TryBuild(out _),
                "A new blank service cannot be saved as an incomplete record");
            empty.Name = "Delivery";
            check(!empty.HasNameError && empty.HasUnitError && !empty.TryBuild(out _), "A service requires a unit");
            empty.UnitOfMeasurement = "trip";
            empty.PriceText = "0";
            check(!empty.HasPriceError && empty.TryBuild(out var freeService) && freeService.PriceExcludingVat == 0m,
                "A free service can be saved with an explicit zero price");
            empty.Name = " \t ";
            check(empty.HasNameError && !empty.TryBuild(out _), "Whitespace service names are rejected");
            empty.Name = "Delivery";
            empty.UnitOfMeasurement = " ";
            check(empty.HasUnitError && !empty.TryBuild(out _), "Whitespace service units are rejected");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            LocalizationService.SetLanguage(originalLanguage);
        }
    }

    private static void CheckPersistence(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoOffer-service-tests-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(directory);
            var settings = store.Load();
            check(settings.Services.Count == 0, "A new settings file starts with no services");
            settings.Services.Add(new ServiceDefinition { Name = "  Montaj  ", UnitOfMeasurement = " m² ", PriceExcludingVat = 30.25m });
            settings.Services.Add(new ServiceDefinition { Name = "Delivery", UnitOfMeasurement = "trip", PriceExcludingVat = 0m });
            store.Save(settings);
            var restored = new SettingsStore(directory).Load();
            check(restored.Services.Count == 2 && restored.Services[0].Name == "Montaj"
                && restored.Services[0].UnitOfMeasurement == "m²" && restored.Services[0].PriceExcludingVat == 30.25m
                && restored.Services[1].PriceExcludingVat == 0m,
                "Added services retain their order, names, units and exact VAT-exclusive prices after reload");

            var draft = new ServiceDraft(restored.Services[0]) { Name = "Montaj nou", UnitOfMeasurement = "hour", PriceText = "45.50" };
            check(draft.TryBuild(out var edited), "The persisted service remains editable");
            restored.Services[0] = edited;
            restored.Services.RemoveAt(1);
            restored.VatRate = 21m;
            store.Save(restored);
            restored = new SettingsStore(directory).Load();
            check(restored.Services.Count == 1 && restored.Services[0].Name == "Montaj nou"
                && restored.Services[0].UnitOfMeasurement == "hour" && restored.Services[0].PriceExcludingVat == 45.5m,
                "Service edits and deletions persist while VAT changes leave service net prices unchanged");
            restored.Services.Clear();
            store.Save(restored);
            check(store.Load().Services.Count == 0, "Deleting the last service persists an empty table");

            File.WriteAllText(store.FilePath, """{"IssuerName":"Existing issuer","VatRate":19.5}""");
            var legacy = store.Load();
            check(legacy.Services.Count == 0 && legacy.IssuerName == "Existing issuer" && legacy.VatRate == 19.5m,
                "Legacy settings without services load without changing existing values");
            File.WriteAllText(store.FilePath, """{"Services":null}""");
            check(store.Load().Services.Count == 0, "A null service collection loads as an empty table");
            File.WriteAllText(store.FilePath, """
                {"Services":[null,{"Name":null,"UnitOfMeasurement":null,"PriceExcludingVat":-5.125}]}
                """);
            var malformed = store.Load();
            check(malformed.Services.Count == 1 && malformed.Services[0].Name == string.Empty
                && malformed.Services[0].UnitOfMeasurement == string.Empty && malformed.Services[0].PriceExcludingVat == -5.125m,
                "Normalization removes null records and handles null text without silently changing saved prices");
            var malformedDraft = new ServiceDraft(malformed.Services[0]);
            check(malformedDraft.HasNameError && malformedDraft.HasUnitError && malformedDraft.HasPriceError,
                "Incomplete or invalid saved services remain available for correction in settings");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
