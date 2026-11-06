using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

internal static class ServiceOfferTests
{
    public static void Run(Action<bool, string> check)
    {
        var originalLanguage = LocalizationService.Language;
        var originalCatalog = CatalogStore.Current;
        try
        {
            LocalizationService.SetLanguage(AppSettings.English);
            CheckServicesOnly(check);
            CheckMixedOffer(check);
            CheckInvalidInput(check);
        }
        finally
        {
            LocalizationService.SetLanguage(originalLanguage);
            CatalogStore.Replace(originalCatalog);
        }
    }

    private static void CheckServicesOnly(Action<bool, string> check)
    {
        var service = new ServiceDefinition { Name = "  Installation  ", UnitOfMeasurement = " m² ", PriceExcludingVat = 25.5m };
        var vm = new MainViewModel(21m, ProductCatalog.Empty) { ClientName = "Service customer" };
        check(vm.TryAddService(service, "2.5") && vm.Items.Count == 1 && vm.Catalog.Items.Count == 0,
            "A saved service can be added without a product catalog");
        var line = vm.Items.Single();
        check(line.IsService && !line.IsProduct && line.HasUnitOfMeasurement
            && line.Name == "Installation" && line.UnitOfMeasurement == "m²" && line.UnitPrice == 25.5m,
            "Service lines snapshot their trimmed name, saved unit and VAT-exclusive price");
        check(!line.HasSizeOptions && !line.HasColorOptions && line.SelectedVariant.ImportedVatRate is null,
            "Services have no product variants or imported gross price");
        check(line.Quantity == 2.5m && line.NetTotal == 63.75m && line.VatAmount == 13.39m
            && vm.GrandTotal == 77.14m && vm.TryValidateOffer(out _),
            "Service-only offers calculate current VAT on the saved net service price");

        var changes = new HashSet<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        line.QuantityText = "4";
        check(line.Quantity == 4m && line.NetTotal == 102m && line.VatAmount == 21.42m
            && vm.GrandTotal == 123.42m && changes.Contains(nameof(MainViewModel.GrandTotalSummary)),
            "Editing a service quantity refreshes its amounts and the offer total");
        line.QuantityText = "invalid";
        check(line.HasQuantityError && vm.HasValidationErrors && vm.GrandTotal == 123.42m
            && !vm.TryValidateOffer(out _),
            "Invalid service quantities preserve the last valid amount and block generation");
        line.QuantityText = "1.5";
        check(!line.HasQuantityError && !vm.HasValidationErrors && vm.GrandTotal == 46.28m,
            "Correcting a service quantity clears validation and recalculates the total");

        service.Name = "Changed service";
        service.UnitOfMeasurement = "hour";
        service.PriceExcludingVat = 90m;
        check(line.Name == "Installation" && line.UnitOfMeasurement == "m²" && line.UnitPrice == 25.5m,
            "Later saved-service edits cannot alter an existing offer line");
        vm.VatRate = 9.5m;
        check(line.UnitPrice == 25.5m && line.UnitOfMeasurement == "m²" && line.VatAmount == 3.63m
            && vm.GrandTotal == 41.88m,
            "Changing VAT preserves the service net price and recalculates tax");
        check(vm.TryAddService(service, "1") && vm.Items[1].Name == "Changed service"
            && vm.Items[1].UnitOfMeasurement == "hour" && vm.Items[1].UnitPrice == 90m,
            "Adding the service again uses its updated saved details");

        LocalizationService.SetLanguage(AppSettings.Romanian);
        vm.RefreshLocalization();
        check(line.QuantityText == "1,5" && line.UnitPriceText == "25,50"
            && line.Name == "Installation" && line.UnitOfMeasurement == "m²" && line.Total == 41.88m,
            "Language changes localize service amounts without changing service data");
        check(vm.TryAddService(new ServiceDefinition { Name = "Livrare", UnitOfMeasurement = "cursă", PriceExcludingVat = 0m }, "1,5")
            && vm.Items[2].Quantity == 1.5m && vm.Items[2].Total == 0m,
            "Localized fractional quantities and zero-priced services are supported");
        LocalizationService.SetLanguage(AppSettings.English);
        vm.RefreshLocalization();
        vm.DeleteCommand.Execute(vm.Items[1]);
        check(vm.Items.Count == 2 && vm.Items[0].Number == 1 && vm.Items[1].Number == 2 && vm.GrandTotal == 41.88m,
            "Deleting service lines renumbers the remaining lines and updates totals");
        vm.ResetCommand.Execute(null);
        check(vm.Items.Count == 0 && vm.GrandTotal == 0m && vm.VatRate == 9.5m,
            "Reset clears service offer lines and retains the configured VAT rate");
        line.Quantity = 7m;
        check(vm.GrandTotal == 0m, "Detached service lines no longer affect a reset offer");
    }

    private static void CheckMixedOffer(Action<bool, string> check)
    {
        var catalog = CatalogCsvImporter.Import(new StringReader("""
            Denumire Produs,Pret,Categorie / Categorii
            Flooring,121,Materials
            """), 21m);
        var vm = new MainViewModel(21m, catalog);
        vm.AddCommand.Execute(null);
        var product = vm.Items.Single();
        check(product.IsProduct && !product.IsService && !product.HasUnitOfMeasurement && product.UnitOfMeasurement == string.Empty,
            "Imported product lines retain their existing behavior without an invented service unit");
        check(vm.TryAddService(new ServiceDefinition { Name = "Fitting", UnitOfMeasurement = "m²", PriceExcludingVat = 20m }, "3")
            && vm.Items.Count == 2 && vm.GrandTotal == 193.6m,
            "Imported products and net-priced services share the same offer and totals");
        var serviceLine = vm.Items[1];
        vm.ApplyCatalog(ProductCatalog.Empty);
        check(vm.Items.Count == 2 && serviceLine.UnitPrice == 20m && serviceLine.UnitOfMeasurement == "m²",
            "Clearing or replacing the product catalog retains existing service lines");
        vm.VatRate = 0m;
        check(product.UnitPrice == 100m && serviceLine.UnitPrice == 20m && vm.GrandTotal == 160m,
            "VAT changes recalculate mixed offers while preserving both line net prices");
        vm.DeleteCommand.Execute(product);
        check(vm.Items.Single() == serviceLine && serviceLine.Number == 1 && vm.GrandTotal == 60m,
            "Deleting a product correctly renumbers a following service");
    }

    private static void CheckInvalidInput(Action<bool, string> check)
    {
        var vm = new MainViewModel(21m, ProductCatalog.Empty);
        var validService = new ServiceDefinition { Name = "Fitting", UnitOfMeasurement = "hour", PriceExcludingVat = 20m };
        foreach (var quantity in new string?[] { null, "", "0", "-1", "1000000", "invalid" })
            check(!vm.TryAddService(validService, quantity) && vm.Items.Count == 0,
                $"Invalid service quantity {quantity} cannot add a line");
        foreach (var invalid in new ServiceDefinition?[]
        {
            null,
            new() { Name = " ", UnitOfMeasurement = "hour", PriceExcludingVat = 10m },
            new() { Name = "Fitting", UnitOfMeasurement = " ", PriceExcludingVat = 10m },
            new() { Name = "Fitting", UnitOfMeasurement = "hour", PriceExcludingVat = -1m },
            new() { Name = "Fitting", UnitOfMeasurement = "hour", PriceExcludingVat = 1.234m }
        })
            check(!vm.TryAddService(invalid!, "1") && vm.Items.Count == 0
                && vm.Status == LocalizationService.Get("InvalidServiceValidation"),
                "Invalid saved-service data cannot create an offer line");

        var large = new ServiceDefinition { Name = "Very large service", UnitOfMeasurement = "unit", PriceExcludingVat = decimal.MaxValue };
        check(!vm.TryAddService(large, "1") && vm.Items.Count == 0
            && vm.Status == LocalizationService.Get("ServiceAmountTooLarge"),
            "A service whose tax overflows is rejected before changing the offer");
        vm.VatRate = 0m;
        check(!vm.TryAddService(large, "1") && vm.Items.Count == 0,
            "A service that cannot support later legal quantity or VAT edits is rejected");
        vm.Items.Add(new OfferLineItem(1, new CatalogItem("Existing", "Large existing line", decimal.MaxValue), 1m, 0m));
        check(!vm.TryAddService(validService, "1") && vm.Items.Count == 1 && vm.GrandTotal == decimal.MaxValue,
            "A service that would overflow the combined offer total is rejected without mutation");
        var line = new OfferLineItem(1, large, 1m, 0m);
        line.QuantityText = "2";
        check(line.HasQuantityError && line.Quantity == 1m && line.Total == decimal.MaxValue
            && line.QuantityValidationMessage == LocalizationService.Get("ServiceAmountTooLarge"),
            "An overflowing service quantity edit preserves the last valid amount");
        line.QuantityText = "1";
        check(!line.HasQuantityError && line.QuantityValidationMessage == string.Empty,
            "A corrected overflowing service quantity clears its validation");
    }
}
