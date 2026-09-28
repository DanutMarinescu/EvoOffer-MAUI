using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

internal static class VariantSelectionTests
{
    public static void Run(Action<bool, string> check)
    {
        // This incomplete size/color matrix comes directly from the supplied example.
        var catalog = CatalogCsvImporter.Import(new StringReader("""
            Denumire Produs,Pret,Categorie / Categorii,Atribute: Culoare (variante de produs),Atribute: Culoare (lista),Atribute: Cantitate (variante de produs),Atribute: Cantitate_11 (lista),Atribute: Marime (variante de produs)
            Lac Loba Viva,455,Lacuri > Loba,Semi-Mat,,5L,,
            Lac Loba Viva,485,Lacuri > Loba,Mat,,5L,,
            Lac Loba Viva,885,Promoții,Semi-Mat,,10L,,
            Lac Loba Viva,455,Lacuri > Loba,Semi-Mat,,5L,,
            Single color,100,Lacuri,,Alb,,,
            Single size,121,Lacuri,,,,,"13,12 kg"
            Plain,10,Lacuri,,,,,
            Ambiguous,100,Lacuri,,,,,
            Ambiguous,200,Lacuri,,,,,
            """), 21m);
        var vm = new MainViewModel(21m, catalog) { ClientName = "Variant customer" };
        check(vm.AvailableItems.Count(item => item.Name == "Lac Loba Viva") == 1,
            "Item dropdown deduplicates product names while retaining variant combinations");
        vm.SelectedCategory = catalog.Categories.Single(category => category.Path == "Promoții");
        check(vm.AvailableItems.Single().Variants.Count == 3,
            "A product's full variant matrix is available through any of its category memberships");
        vm.AddCommand.Execute(null);
        var line = vm.Items.Single();
        check(line.SelectedSize == "5L" && line.SelectedColor == "Semi-Mat" && line.Total == 455m,
            "New offer lines initialize to the first actual CSV variant and its price");
        check(line.AvailableSizes.SequenceEqual(new[] { "5L", "10L" })
            && line.AvailableColors.SequenceEqual(new[] { "Semi-Mat", "Mat" }),
            "Both dropdowns retain all distinct options in source order");

        var notifications = new HashSet<string?>();
        line.PropertyChanged += (_, e) =>
        {
            notifications.Add(e.PropertyName);
            check(catalog.Items[0].Variants.Any(variant => variant.Size == line.SelectedSize
                && variant.Color == line.SelectedColor && variant.UnitPrice == line.UnitPrice),
                "Every selection notification observes a complete valid combination and matching price");
        };
        line.SelectedColor = "Mat";
        check(line.SelectedSize == "5L" && line.Total == 485m && vm.GrandTotal == 485m,
            "Changing color preserves a compatible size and refreshes line and offer prices");
        notifications.Clear();
        line.SelectedSize = "10L";
        check(line.SelectedColor == "Semi-Mat" && line.Total == 885m && vm.GrandTotal == 885m,
            "Choosing 10L automatically replaces unavailable Mat with Semi-Mat and selects 885 RON");
        check(new[] { "SelectedSize", "SelectedColor", "SelectedVariant", "UnitPrice", "UnitPriceText",
            "NetTotal", "VatAmount", "VatAmountText", "Total", "TotalText" }.All(notifications.Contains),
            "Variant changes notify both pickers and every displayed amount");
        line.SelectedColor = "Mat";
        check(line.SelectedSize == "5L" && line.Total == 485m,
            "Choosing Mat from 10L automatically selects the valid 5L size");
        line.SelectedColor = "Semi-Mat";
        check(line.SelectedSize == "5L" && line.Total == 455m,
            "Changing color keeps the existing size when it is compatible");
        check(line.AvailableSizes.Count == 2 && line.AvailableColors.Count == 2,
            "Options are never filtered after selecting a variant");
        line.SelectedSize = "nonexistent";
        line.SelectedColor = null!;
        check(line.SelectedSize == "5L" && line.SelectedColor == "Semi-Mat" && line.Total == 455m,
            "Unknown and transient null selections cannot create invalid combinations");

        vm.AddCommand.Execute(null);
        var secondLine = vm.Items[1];
        line.SelectedSize = "10L";
        check(secondLine.SelectedSize == "5L" && secondLine.Total == 455m,
            "Each offer line independently selects a variant from the immutable catalog");
        line.Quantity = 2.5m;
        check(line.Total == 2212.5m && line.NetTotal + line.VatAmount == line.Total
            && vm.GrandTotal == 2667.5m,
            "Variant prices preserve VAT-inclusive totals with fractional quantities");
        var snapshot = new OfferPdfData(vm.ClientName, null, vm.Items, new AppSettings());
        line.SelectedColor = "Mat";
        check(snapshot.Items[0].Size == "10L" && snapshot.Items[0].Color == "Semi-Mat"
            && snapshot.Items[0].Total == 2212.5m,
            "PDF snapshots preserve the chosen variant and price independently of later edits");

        var netBeforeVatChange = line.UnitPrice;
        vm.VatRate = 0m;
        check(line.UnitPrice == netBeforeVatChange && line.Total == line.NetTotal,
            "Changing VAT preserves the selected existing variant's net price");
        line.SelectedSize = "10L";
        check(line.UnitPrice == 885m / 1.21m && line.Total == line.NetTotal,
            "Switching variants in an existing line consistently uses its captured catalog prices");
        vm.AddCommand.Execute(null);
        check(vm.Items[2].UnitPrice == 455m && vm.Items[2].Total == 455m,
            "New lines after VAT changes use repriced variants from the current catalog");
        vm.ApplyCatalog(ProductCatalog.Empty);
        line.SelectedColor = "Mat";
        check(line.SelectedSize == "5L" && line.UnitPrice == 485m / 1.21m,
            "Existing lines retain all valid variants after the catalog is replaced or cleared");

        foreach (var (name, size, color, hasSize, hasColor) in new[]
            { ("Single color", "—", "Alb", false, true), ("Single size", "13,12 kg", "—", true, false),
              ("Plain", "—", "—", false, false) })
        {
            var single = new OfferLineItem(1, catalog.Items.Single(item => item.Name == name), 1m, 21m);
            check(single.SelectedSize == size && single.SelectedColor == color
                && single.HasSizeOptions == hasSize && single.HasColorOptions == hasColor,
                "Missing attributes have a placeholder and disabled picker: " + name);
        }

        var ambiguousVm = new MainViewModel(21m, catalog);
        ambiguousVm.SelectedItem = ambiguousVm.AvailableItems.Single(item => item.Name == "Ambiguous");
        ambiguousVm.AddCommand.Execute(null);
        check(ambiguousVm.Items.Count == 0 && ambiguousVm.Status.Contains("different prices"),
            "Products with indistinguishable prices cannot silently add an arbitrary offer price");
        var rejected = false;
        try { _ = new OfferLineItem(1, ambiguousVm.SelectedItem, 1m, 21m); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Direct line construction also rejects ambiguous product pricing");

        var missingAndLiteral = CatalogCsvImporter.Import(new StringReader("""
            Denumire Produs,Pret,Categorie / Categorii,Atribute: Culoare (lista),Atribute: Cantitate_11 (lista)
            Literal labels,100,A,,
            Literal labels,200,A,—,—
            Literal labels,300,A,— (not specified),— (not specified)
            """), 0m).Items.Single();
        var literalLine = new OfferLineItem(1, missingAndLiteral, 1m, 0m);
        var missingSize = literalLine.SelectedSize;
        var missingColor = literalLine.SelectedColor;
        check(literalLine.AvailableSizes.Count == 3 && literalLine.AvailableColors.Count == 3,
            "Missing attribute placeholders cannot hide literal source labels with the same text");
        literalLine.SelectedSize = "—";
        check(literalLine.SelectedVariant.Size == "—" && literalLine.SelectedColor == "—" && literalLine.Total == 200m,
            "A literal em dash remains a selectable source attribute with its own price");
        literalLine.SelectedColor = "— (not specified)";
        check(literalLine.SelectedSize == "— (not specified)" && literalLine.Total == 300m,
            "Literal fallback placeholder text also remains independently selectable");
        literalLine.SelectedSize = missingSize;
        check(literalLine.SelectedVariant.Size == "" && literalLine.SelectedColor == missingColor && literalLine.Total == 100m,
            "Selecting an absent attribute restores its valid absent counterpart and source price");
    }
}
