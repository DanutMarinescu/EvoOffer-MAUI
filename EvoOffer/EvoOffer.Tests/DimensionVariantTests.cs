using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

public static class DimensionVariantTests
{
    private const string Header = "Denumire Produs;Pret;Categorie / Categorii";
    private const string LengthHeader = "Atribute: Lungime Mm (variante de produs)";
    private const string WidthHeader = "Atribute: Latime Mm (variante de produs)";
    private const string ThicknessHeader = "Atribute: Grosime Mm (variante de produs)";
    private const string DimensionHeaders = Header + ";" + LengthHeader + ";" + WidthHeader + ";" + ThicknessHeader;

    public static void Run(Action<bool, string> check)
    {
        var catalog = Import(DimensionHeaders + ";Atribute: Culoare (variante de produs)\n"
            + "Parchet;121;Pardoseli;600;90;22;Natur\n"
            + "Parchet;242;Pardoseli;700;90;22;Natur\n"
            + "Parchet;363;Pardoseli;700;90;22;Alb\n"
            + " Parchet ;121;Pardoseli; 600 ; 90 ; 22 ; Natur ", 21m);
        var product = catalog.Items.Single();
        check(product.Variants.Select(variant => (variant.Size, variant.Color, variant.PriceIncludingVat)).SequenceEqual(new[]
            {
                ("600 mm x 90 mm x 22 mm", "Natur", 121m),
                ("700 mm x 90 mm x 22 mm", "Natur", 242m),
                ("700 mm x 90 mm x 22 mm", "Alb", 363m)
            }) && !product.HasAmbiguousVariants,
            "Dimension fallback builds length/width/thickness sizes, keeps length-only variants distinct, and collapses identical resulting combinations");
        check(product.Variants.Select(variant => variant.UnitPrice).SequenceEqual(new[] { 100m, 200m, 300m }),
            "Dimension variants retain the matching source prices and VAT-exclusive amounts");

        var vm = new MainViewModel(21m, catalog) { NewQuantity = 2m };
        vm.AddCommand.Execute(null);
        var line = vm.Items.Single();
        check(vm.AvailableItems.Count == 1 && line.AvailableSizes.SequenceEqual(new[]
            { "600 mm x 90 mm x 22 mm", "700 mm x 90 mm x 22 mm" })
            && line.AvailableColors.SequenceEqual(new[] { "Natur", "Alb" })
            && line.SelectedSize == "600 mm x 90 mm x 22 mm" && vm.GrandTotal == 242m,
            "A dimension-based product appears once in the product dropdown and exposes every available size and color");
        line.SelectedColor = "Alb";
        check(line.SelectedSize == "700 mm x 90 mm x 22 mm" && line.SelectedColor == "Alb"
            && line.SelectedVariant.PriceIncludingVat == 363m && line.UnitPrice == 300m
            && line.NetTotal == 600m && line.VatAmount == 126m && line.Total == 726m && vm.GrandTotal == 726m,
            "Choosing a color corrects an unavailable dimension size and refreshes the selected price and offer total");
        line.SelectedSize = "600 mm x 90 mm x 22 mm";
        check(line.SelectedColor == "Natur" && line.SelectedVariant.PriceIncludingVat == 121m
            && line.UnitPrice == 100m && line.NetTotal == 200m && line.VatAmount == 42m
            && line.Total == 242m && vm.GrandTotal == 242m,
            "Choosing a dimension size corrects an unavailable color and restores the matching source price");
        line.SelectedSize = "700 mm x 90 mm x 22 mm";
        check(line.SelectedColor == "Natur" && line.SelectedVariant.PriceIncludingVat == 242m
            && line.UnitPrice == 200m && vm.GrandTotal == 484m
            && line.AvailableSizes.Count == 2 && line.AvailableColors.Count == 2,
            "Changing dimensions preserves a compatible color and leaves every dropdown option visible");

        var reordered = Import(WidthHeader + ";Pret;Denumire Produs;" + ThicknessHeader
            + ";Categorie / Categorii;" + LengthHeader + "\n90;121;Reordered;22;Pardoseli;600", 21m);
        check(reordered.Items.Single().Variants.Single().Size == "600 mm x 90 mm x 22 mm",
            "Dimension headers map by name independently of export column order");
        var literal = Import(DimensionHeaders + "\nRanges;121;Pardoseli; 600 - 2200 ; 90-120 ; 22,5 ", 21m);
        check(literal.Items.Single().Variants.Single().Size == "600 - 2200 mm x 90-120 mm x 22,5 mm",
            "Dimension values remain trimmed literal text, preserving decimal commas and ranges");

        foreach (var explicitSizeHeader in new[]
            {
                "Atribute: Cantitate (variante de produs)",
                "Atribute: Cantitate_11 (lista)",
                "Atribute: Marime (variante de produs)"
            })
        {
            var explicitSize = Import(DimensionHeaders + ";" + explicitSizeHeader + "\n"
                + "Explicit;121;Pardoseli;600;90;22; 5L \n"
                + "Explicit;121;Pardoseli;700;120;25;5L", 21m).Items.Single();
            check(explicitSize.Variants.Count == 1 && explicitSize.Variants[0].Size == "5L",
                "An existing size alias takes precedence over dimensions and deduplication uses the resulting size: " + explicitSizeHeader);
            var emptyExplicitSize = Import(DimensionHeaders + ";" + explicitSizeHeader
                + "\nFallback;121;Pardoseli;600;90;22;   ", 21m).Items.Single();
            check(emptyExplicitSize.Variants[0].Size == "600 mm x 90 mm x 22 mm",
                "An empty existing size alias allows dimension fallback: " + explicitSizeHeader);
        }

        var partial = Import(DimensionHeaders + "\n"
            + "Missing width;121;Pardoseli;600;;22\n"
            + "Only thickness;121;Pardoseli;;;22\n"
            + "Only width;121;Pardoseli;;90;\n"
            + "Only length;121;Pardoseli;600;;\n"
            + "All empty;121;Pardoseli; ; ; ", 21m);
        check(partial.Items.Select(item => item.Variants[0].Size).SequenceEqual(new[]
            { "600 mm x 22 mm", "22 mm", "90 mm", "600 mm", string.Empty }),
            "Partial dimensions include only populated values without phantom units or separators, while all-empty dimensions remain unspecified");
        var missingHeader = Import(Header + ";" + LengthHeader + ";" + ThicknessHeader
            + "\nMissing width header;121;Pardoseli;600;22", 21m);
        check(missingHeader.Items.Single().Variants[0].Size == "600 mm x 22 mm",
            "An absent dimension header is skipped like a missing value while retaining the remaining dimension order");
        check(Import(Header + "\nLegacy;121;Pardoseli", 21m).Items.Single().Variants[0].Size == string.Empty,
            "Legacy catalogs without dimension headers retain their unspecified size");
        var thicknessOnlyHeader = Import(Header + ";" + ThicknessHeader
            + "\nThickness only;121;Pardoseli;22", 21m);
        check(thicknessOnlyHeader.Items.Single().Variants[0].Size == "22 mm",
            "A catalog with only a thickness attribute produces a single dimension without a leading or trailing separator");
        var collapsedDimensions = Import(DimensionHeaders + "\n"
            + "Unlabelled positions;121;Pardoseli;22;;\n"
            + "Unlabelled positions;242;Pardoseli;;;22", 21m).Items.Single();
        check(collapsedDimensions.Variants.Count == 2 && collapsedDimensions.HasAmbiguousVariants
            && collapsedDimensions.Variants.All(variant => variant.Size == "22 mm"),
            "Rows whose nullable dimensions form the same size retain distinct prices and are marked ambiguous");

        foreach (var duplicateHeader in new[] { LengthHeader, WidthHeader, ThicknessHeader })
        {
            try
            {
                Import(DimensionHeaders + ";" + duplicateHeader + "\nDuplicate;121;Pardoseli;600;90;22;600", 21m);
                check(false, "Duplicate dimension headers must be rejected: " + duplicateHeader);
            }
            catch (CatalogImportException exception)
            {
                check(exception.RowNumber == 1 && exception.Message.Contains(duplicateHeader),
                    "Duplicate recognized dimension headers are rejected at the header row: " + duplicateHeader);
            }
        }
    }

    private static ProductCatalog Import(string csv, decimal vatRate) =>
        CatalogCsvImporter.Import(new StringReader(csv), vatRate);
}
