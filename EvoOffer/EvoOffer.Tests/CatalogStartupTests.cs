using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

internal static class CatalogStartupTests
{
    private const string Header = "Denumire Produs,Pret,Categorie / Categorii";

    public static async Task RunAsync(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoOffer-catalog-startup-tests-" + Guid.NewGuid());
        var previousCatalog = CatalogStore.Current;
        try
        {
            var csvPath = Path.Combine(directory, "Saved catalog.csv");
            var store = new SettingsStore(directory);
            store.Save(new AppSettings { DataFilePath = csvPath, VatRate = 7m });
            await File.WriteAllTextAsync(csvPath, Header + "\nOriginal,107,First category");

            var settings = new SettingsStore(directory).Load();
            var firstStartup = new MainViewModel(settings.VatRate, ProductCatalog.Empty);
            await firstStartup.ReloadCatalogAsync(settings.DataFilePath);
            check(firstStartup.Catalog.Items.Single().Name == "Original"
                && firstStartup.Catalog.Items[0].UnitPrice == 100m
                && firstStartup.Catalog.Items[0].ImportedVatRate == 7m,
                "Startup reads the CSV path and VAT rate restored from saved settings");
            check(firstStartup.SelectedItem == firstStartup.Catalog.Items[0]
                && firstStartup.SelectedCategory?.Path == "First category"
                && ReferenceEquals(CatalogStore.Current, firstStartup.Catalog),
                "Startup publishes the imported catalog and populates product and category selection");

            await File.WriteAllTextAsync(csvPath, Header + "\nUpdated,214,Changed category\nAdded,321,Changed category");
            settings = new SettingsStore(directory).Load();
            var nextStartup = new MainViewModel(settings.VatRate, ProductCatalog.Empty);
            await nextStartup.ReloadCatalogAsync(settings.DataFilePath);
            check(nextStartup.Catalog.Items.Select(item => item.Name).SequenceEqual(new[] { "Updated", "Added" })
                && nextStartup.Catalog.Items.Select(item => item.UnitPrice).SequenceEqual(new[] { 200m, 300m })
                && nextStartup.SelectedCategory?.Path == "Changed category",
                "Every startup rereads changed CSV contents at the same saved path instead of reusing the previous catalog");
            check(firstStartup.Catalog.Items.Single().Name == "Original"
                && ReferenceEquals(CatalogStore.Current, nextStartup.Catalog),
                "A startup reload replaces the shared snapshot without mutating the previous catalog");

            foreach (var absentPath in new string?[] { null, string.Empty, "  " })
            {
                var noSource = new MainViewModel(settings.VatRate, firstStartup.Catalog);
                await noSource.ReloadCatalogAsync(absentPath);
                check(noSource.Catalog.Items.Count == 0 && noSource.Categories.Count == 0
                    && noSource.SelectedItem is null && noSource.SelectedCategory is null
                    && CatalogStore.Current.Items.Count == 0,
                    "Startup without a CSV path clears products and categories from an existing in-memory catalog");
            }

            await File.WriteAllTextAsync(csvPath, Header + "\nValid first row,107,Category\nInvalid second row,bad,Category");
            foreach (var source in new[] { csvPath, Path.Combine(directory, "Missing.csv") })
            {
                foreach (var initialCatalog in new[] { ProductCatalog.Empty, firstStartup.Catalog })
                {
                    var failedStartup = new MainViewModel(settings.VatRate, initialCatalog);
                    CatalogStore.Replace(failedStartup.Catalog);
                    var catalogBeforeReload = failedStartup.Catalog;
                    var rejected = false;
                    try
                    {
                        await failedStartup.ReloadCatalogAsync(source);
                    }
                    catch (CatalogImportException) when (source == csvPath)
                    {
                        rejected = true;
                    }
                    catch (FileNotFoundException) when (source != csvPath)
                    {
                        rejected = true;
                    }
                    check(rejected && ReferenceEquals(failedStartup.Catalog, catalogBeforeReload)
                        && ReferenceEquals(CatalogStore.Current, catalogBeforeReload),
                        "An invalid or missing startup CSV reports failure without publishing partial or replacement catalog data");
                }
            }
        }
        finally
        {
            CatalogStore.Replace(previousCatalog);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
