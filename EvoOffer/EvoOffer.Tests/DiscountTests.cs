using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

internal static class DiscountTests
{
    public static void Run(Action<bool, string> check)
    {
        var originalLanguage = LocalizationService.Language;
        var originalCatalog = CatalogStore.Current;
        try
        {
            LocalizationService.SetLanguage(AppSettings.English);
            CheckInput(check);
            CheckItemDiscounts(check);
            CheckRecalculation(check);
            CheckOfferDiscounts(check);
        }
        finally
        {
            LocalizationService.SetLanguage(originalLanguage);
            CatalogStore.Replace(originalCatalog);
        }
    }

    private static void CheckInput(Action<bool, string> check)
    {
        foreach (var language in new[] { AppSettings.English, AppSettings.Romanian })
        {
            LocalizationService.SetLanguage(language);
            foreach (var text in new[] { "12.5", "12,5", " 12.50 " })
                check(DiscountDefinition.TryCreate(DiscountScope.PerUnit, DiscountValueType.Percentage,
                    text, 121m, out var discount, out var error)
                    && discount == new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Percentage, 12.5m)
                    && error.Length == 0,
                    "Discount entry accepts both decimal separators and trims whitespace");

            foreach (var text in new string?[] { null, "", " ", "invalid", "-1", "100.01", "1.234", "1e1", "1,2.3", "10%" })
                check(!DiscountDefinition.TryCreate(DiscountScope.ItemTotal, DiscountValueType.Percentage,
                    text, 121m, out _, out var error) && error.Length > 0,
                    "Invalid percentage discounts are rejected with a validation message: " + text);

            foreach (var text in new[] { "-0.01", "121.01", "1.234", "invalid" })
                check(!DiscountDefinition.TryCreate(DiscountScope.ItemTotal, DiscountValueType.Absolute,
                    text, 121m, out _, out var error) && error.Length > 0,
                    "Absolute discounts cannot be negative, malformed, or exceed their VAT-inclusive base: " + text);

            foreach (var type in new[] { DiscountValueType.Percentage, DiscountValueType.Absolute })
                check(DiscountDefinition.TryCreate(DiscountScope.ItemTotal, type, "0", 121m,
                    out var discount, out _) && discount is null,
                    "A zero discount clears the discount rather than displaying a zero-value adjustment");

            check(DiscountDefinition.TryCreate(DiscountScope.ItemTotal, DiscountValueType.Percentage,
                "100", 121m, out var fullPercentage, out _) && fullPercentage!.CalculateAmount(121m) == 121m,
                "One hundred percent is a valid full discount");
            check(DiscountDefinition.TryCreate(DiscountScope.PerUnit, DiscountValueType.Absolute,
                "121", 121m, out var fullUnit, out _) && fullUnit!.CalculateAmount(302.5m, 2.5m) == 302.5m,
                "A full per-unit discount scales with fractional quantities");
        }
        LocalizationService.SetLanguage(AppSettings.English);
    }

    private static void CheckItemDiscounts(Action<bool, string> check)
    {
        foreach (var (scope, type, value, amount, net, vat, total) in new[]
        {
            (DiscountScope.PerUnit, DiscountValueType.Percentage, 10m, 30.25m, 225m, 47.25m, 272.25m),
            (DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m, 30.25m, 225m, 47.25m, 272.25m),
            (DiscountScope.PerUnit, DiscountValueType.Absolute, 12.1m, 30.25m, 225m, 47.25m, 272.25m),
            (DiscountScope.ItemTotal, DiscountValueType.Absolute, 12.1m, 12.1m, 240m, 50.4m, 290.4m)
        })
        {
            var line = new OfferLineItem(1, new CatalogItem("Materials", "Flooring", 100m), 2.5m, 21m);
            var changes = new HashSet<string?>();
            line.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
            line.Discount = new DiscountDefinition(scope, type, value);
            check(line.HasDiscount && line.OriginalNetTotal == 250m && line.OriginalVatAmount == 52.5m
                && line.OriginalTotal == 302.5m && line.UnitPrice == 100m && line.Quantity == 2.5m,
                "Applying an item discount retains the source price, quantity and original amounts");
            check(line.DiscountAmount == amount && line.NetTotal == net && line.VatAmount == vat
                && line.Total == total && line.NetTotal + line.VatAmount == line.Total
                && line.DiscountNetAmount + line.DiscountVatAmount == line.DiscountAmount,
                $"{scope} {type} discounts use the requested VAT-inclusive amount and reconcile net/VAT totals");
            check(new[] { nameof(line.Discount), nameof(line.HasDiscount), nameof(line.DiscountDescription),
                nameof(line.DiscountAmount), nameof(line.DiscountAmountText), nameof(line.NetTotal),
                nameof(line.VatAmountText), nameof(line.TotalText) }.All(changes.Contains),
                "Applying a discount refreshes its explanatory row and all displayed amounts");
            line.Discount = null;
            check(!line.HasDiscount && line.DiscountAmount == 0m && line.NetTotal == 250m
                && line.VatAmount == 52.5m && line.Total == 302.5m,
                "Resetting an item discount restores all original amounts exactly");
        }

        var imported = CatalogCsvImporter.Import(new StringReader("Denumire Produs,Pret,Categorie / Categorii\nImported,100,Materials"), 21m);
        var importedLine = new OfferLineItem(1, imported.Items.Single(), 2.5m, 21m)
        {
            Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m)
        };
        check(importedLine.OriginalTotal == 250m && importedLine.DiscountAmount == 25m
            && importedLine.NetTotal == 185.95m && importedLine.VatAmount == 39.05m && importedLine.Total == 225m,
            "Imported gross prices remain authoritative when discounts allocate rounded net and VAT amounts");
        importedLine.Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 100m);
        check(importedLine.Total == 0m && importedLine.NetTotal == 0m && importedLine.VatAmount == 0m,
            "A full discount clears both taxable amount and VAT");
        importedLine.Discount = null;
        check(importedLine.Total == 250m && importedLine.NetTotal + importedLine.VatAmount == 250m,
            "Clearing a full discount restores the exact imported gross amount");

        var free = new OfferLineItem(1, new CatalogItem("Materials", "Free sample", 0m), 1m, 21m)
        {
            Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m)
        };
        check(free.DiscountAmount == 0m && free.NetTotal == 0m && free.VatAmount == 0m && free.Total == 0m,
            "Percentage discounts on free items do not divide by zero");

        var rounded = new OfferLineItem(1, new CatalogItem("Materials", "Small amount", 0.05m), 1m, 0m)
        {
            Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m)
        };
        check(rounded.DiscountAmount == 0.01m && rounded.Total == 0.04m && rounded.VatAmount == 0m,
            "Half-cent discount amounts round away from zero without inventing VAT");

        var protectedLine = new OfferLineItem(1, new CatalogItem("Materials", "Protected", 100m), 1m, 21m);
        foreach (var invalid in new[]
        {
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, -1m),
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 101m),
            new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Absolute, 121.01m),
            new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Absolute, 1.234m)
        })
        {
            var rejected = false;
            try { protectedLine.Discount = invalid; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected && !protectedLine.HasDiscount && protectedLine.Total == 121m,
                "Direct invalid discount assignments cannot bypass validation or change amounts");
        }
    }

    private static void CheckRecalculation(Action<bool, string> check)
    {
        var product = new CatalogItem("Variants", new[]
        {
            new CatalogVariant("Large", "Natural", 100m, 121m, 21m),
            new CatalogVariant("Small", "Natural", 10m, 12.1m, 21m)
        }, new[] { "Materials" });
        var line = new OfferLineItem(1, product, 2m, 21m)
        {
            Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Absolute, 200m)
        };
        check(line.Total == 42m, "An item-total absolute discount is deducted once from the line");
        line.QuantityText = "invalid";
        check(line.HasQuantityError && line.DiscountAmount == 200m && line.Total == 42m,
            "Invalid quantity input preserves the last valid discount and total");
        line.Quantity = 1m;
        check(!line.HasQuantityError && line.DiscountAmount == 121m && line.Total == 0m
            && line.NetTotal == 0m && line.VatAmount == 0m && line.Discount!.Value == 200m,
            "Reducing quantity caps a saved absolute discount at the new total without negative amounts");
        line.Quantity = 2m;
        check(line.DiscountAmount == 200m && line.Total == 42m,
            "Increasing quantity restores the saved absolute discount after a temporary cap");
        line.SelectedSize = "Small";
        check(line.DiscountAmount == 24.2m && line.Total == 0m,
            "Selecting a cheaper variant safely caps an existing discount");
        line.Discount = null;
        line.SelectedSize = "Large";
        line.Discount = new DiscountDefinition(DiscountScope.PerUnit, DiscountValueType.Absolute, 12.1m);
        line.Quantity = 2.5m;
        check(line.DiscountAmount == 30.25m && line.Total == 272.25m,
            "Per-unit absolute discounts recalculate when the quantity changes");
        line.VatRate = 0m;
        check(line.DiscountAmount == 30.25m && line.NetTotal == 219.75m
            && line.VatAmount == 0m && line.Total == 219.75m,
            "VAT changes retain the entered gross discount while recalculating the taxable total");

        var changes = new HashSet<string?>();
        line.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        LocalizationService.SetLanguage(AppSettings.Romanian);
        line.RefreshLocalization();
        check(line.DiscountAmountText.Contains("30,25") && changes.Contains(nameof(line.DiscountDescription))
            && changes.Contains(nameof(line.DiscountAmountText)) && line.Total == 219.75m,
            "Language changes refresh discount descriptions and amounts without changing their values");
        LocalizationService.SetLanguage(AppSettings.English);
    }

    private static void CheckOfferDiscounts(Action<bool, string> check)
    {
        var catalog = CatalogCsvImporter.Import(new StringReader("Denumire Produs,Pret,Categorie / Categorii\nFlooring,121,Materials"), 21m);
        var vm = new MainViewModel(21m, catalog) { ClientName = "Discount customer", NewQuantity = 2m };
        vm.AddCommand.Execute(null);
        var product = vm.Items.Single();
        product.Discount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m);
        check(vm.TryAddService(new ServiceDefinition { Name = "Installation", UnitOfMeasurement = "m²", PriceExcludingVat = 20m }, "3"),
            "A service can share an offer with discounted products");
        var changes = new HashSet<string?>();
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        vm.GeneralDiscount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 10m);
        check(vm.HasGeneralDiscount && vm.TotalBeforeGeneralDiscount == 290.4m
            && vm.GeneralDiscountAmount == 29.04m && vm.GrandTotal == 261.36m,
            "The general percentage discount applies after item discounts across products and services");
        check(new[] { nameof(vm.GeneralDiscount), nameof(vm.HasGeneralDiscount), nameof(vm.GeneralDiscountSummary),
            nameof(vm.GeneralDiscountAmount), nameof(vm.GrandTotalSummary) }.All(changes.Contains),
            "Applying the general discount refreshes the footer discount and final total");

        var settings = new AppSettings { Language = AppSettings.English };
        var snapshot = new OfferPdfData(vm.ClientName, null, vm.Items, settings, vm.GeneralDiscount);
        check(snapshot.GrandTotal == 261.36m && snapshot.Subtotal == 216m && snapshot.VatTotal == 45.36m
            && snapshot.Subtotal + snapshot.VatTotal == snapshot.GrandTotal,
            "The PDF snapshot carries combined item and general discounts with reconciled net/VAT totals");

        vm.GeneralDiscount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Absolute, 40m);
        check(vm.TotalBeforeGeneralDiscount == 290.4m && vm.GeneralDiscountAmount == 40m && vm.GrandTotal == 250.4m,
            "An absolute general discount reduces the offer's final VAT-inclusive total once");
        changes.Clear();
        product.Quantity = 1m;
        check(vm.TotalBeforeGeneralDiscount == 181.5m && vm.GrandTotal == 141.5m
            && changes.Contains(nameof(vm.GeneralDiscountSummary)) && changes.Contains(nameof(vm.GrandTotalSummary)),
            "Quantity edits refresh the general discount footer and final offer total");
        vm.DeleteCommand.Execute(product);
        check(vm.GrandTotal == 32.6m, "Removing a discounted product recalculates the remaining general discount");
        vm.Items.Single().Quantity = 1m;
        check(vm.GeneralDiscountAmount == 24.2m && vm.GrandTotal == 0m && vm.GeneralDiscount!.Value == 40m,
            "A general absolute discount is capped when the offer becomes smaller");
        vm.GeneralDiscount = null;
        check(!vm.HasGeneralDiscount && vm.GrandTotal == 24.2m,
            "Resetting the general discount restores the sum of the remaining lines");
        vm.GeneralDiscount = new DiscountDefinition(DiscountScope.ItemTotal, DiscountValueType.Percentage, 100m);
        check(vm.GrandTotal == 0m && vm.GeneralDiscountAmount == 24.2m,
            "A full general discount produces a zero offer total");
        vm.ResetCommand.Execute(null);
        check(vm.Items.Count == 0 && !vm.HasGeneralDiscount && vm.GeneralDiscount is null
            && vm.GeneralDiscountAmount == 0m && vm.GrandTotal == 0m,
            "Resetting the offer clears both its lines and its general discount");
        check(snapshot.GrandTotal == 261.36m && snapshot.Items.Count == 2 && snapshot.Items[0].Total == 217.8m
            && snapshot.Subtotal == 216m && snapshot.VatTotal == 45.36m,
            "Changing quantities, discounts and resetting the offer cannot mutate an existing PDF snapshot");
    }
}
