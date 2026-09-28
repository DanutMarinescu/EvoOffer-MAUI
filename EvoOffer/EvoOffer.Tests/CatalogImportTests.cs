using System.Globalization;
using System.Text;
using EvoOffer.Models;
using EvoOffer.Services;

public static class CatalogImportTests
{
    private const string Header = "Denumire Produs,Pret,Categorie / Categorii";
    private const string AttributeHeaders = Header + ",Atribute: Culoare (variante de produs),Atribute: Culoare (lista)"
        + ",Atribute: Cantitate (variante de produs),Atribute: Cantitate_11 (lista),Atribute: Marime (variante de produs)";

    public static void Run(Action<bool, string> check)
    {
        var catalog = Import("\uFEFF Denumire Produs , Pret , Categorie / Categorii\r\n"
            + "  Țeavă  ,121, A>B>C>D | E > F | G | A > B > C > D\r\n"
            + "Cot,100,A > B\r\n"
            + "Piuliță,0,A > H\r\n"
            + "Cablu,12.10,Z > B\r\n", 21m);
        check(catalog.Items.Count == 4 && catalog.Items[0].Name == "Țeavă"
            && catalog.Items[0].UnitPrice == 100m && catalog.Items[0].PriceIncludingVat == 121m,
            "CSV imports Unicode product names and divides gross price by the configured VAT factor");
        check(catalog.Items[1].UnitPrice == 100m / 1.21m,
            "Imported net prices retain full decimal precision rather than prematurely rounding cents");
        check(catalog.Items[0].ImportedVatRate == 21m
            && new CatalogItem("Test", "Legacy", 100m).ImportedVatRate is null,
            "Imported gross prices are distinguished from legacy VAT-exclusive fixtures");
        check(catalog.Items[0].CategoryPaths.SequenceEqual(new[] { "A > B > C > D", "E > F", "G" })
            && catalog.Items[0].Category == "A > B > C > D | E > F | G",
            "Category paths normalize whitespace, remove duplicates and preserve multiple memberships");
        check(catalog.Categories.Select(category => category.Path).SequenceEqual(new[]
            { "A", "A > B", "A > B > C", "A > B > C > D", "A > H", "E", "E > F", "G", "Z", "Z > B" }),
            "Categories are distinct full paths in stable depth-first order, including every ancestor");
        check(catalog.Categories.Select(category => category.Depth).SequenceEqual(new[] { 0, 1, 2, 3, 1, 0, 1, 0, 0, 1 })
            && catalog.Categories.All(category => category.IsRoot == (category.Depth == 0)),
            "Category hierarchy exposes the depth and root markers needed by dropdown presentation");
        foreach (var path in new[] { "A", "A > B", "A > B > C", "A > B > C > D", "E", "E > F", "G" })
            check(catalog.Items[0].BelongsTo(catalog.Categories.Single(category => category.Path == path)),
                "Products belong to every ancestor along all their category paths: " + path);
        check(!catalog.Items[0].BelongsTo(catalog.Categories.Single(category => category.Path == "Z > B"))
            && !catalog.Items[0].BelongsTo(new CatalogCategory("A > B > C > D > Child", "Child", 4)),
            "Same-named children under other parents and deeper descendants do not share membership");
        var prefix = Import(Header + "\nItem,1,AB > Child", 0m);
        check(!prefix.Items[0].BelongsTo(new CatalogCategory("A", "A", 0)),
            "Category membership uses path boundaries rather than text prefix alone");

        var repriced = catalog.WithVatRate(9.5m);
        check(repriced.Items[0].PriceIncludingVat == 121m && repriced.Items[0].UnitPrice == 121m / 1.095m
            && repriced.VatRate == 9.5m && repriced.Items[0].ImportedVatRate == 9.5m
            && catalog.Items[0].UnitPrice == 100m && catalog.VatRate == 21m,
            "Changing VAT creates a new snapshot from unchanged imported gross prices");
        check(repriced.Categories.SequenceEqual(catalog.Categories) && !ReferenceEquals(repriced.Items, catalog.Items),
            "VAT replacement preserves category identities and never mutates the original collection");
        check(ProductCatalog.Empty.Items.Count == 0 && ProductCatalog.Empty.Categories.Count == 0,
            "The empty catalog contains no demo products or categories");
        RejectMutation(() => ((IList<CatalogItem>)catalog.Items).Clear(), check, "Catalog items cannot be changed through a mutable interface");
        RejectMutation(() => ((IList<CatalogCategory>)catalog.Categories).Clear(), check, "Catalog categories cannot be changed through a mutable interface");
        RejectMutation(() => ((IList<string>)catalog.Items[0].CategoryPaths).Clear(), check, "Item category paths cannot be changed through a mutable interface");

        var quoted = Import(Header + "\n\"Product, \"\"quoted\"\"\nsecond line\",\"12,10\",\"Main, category > Child\"\n\n  \n", 21m);
        check(quoted.Items.Single().Name == "Product, \"quoted\"\nsecond line"
            && quoted.Items[0].PriceIncludingVat == 12.10m
            && quoted.Categories[0].Name == "Main, category",
            "CSV quotes support embedded commas, escaped quotes, line breaks and quoted decimal commas");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "ro-RO" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var semicolon = Import("Denumire Produs;Pret;Categorie / Categorii\nItem;121,50;A > B\nOther;000.50;A", 0m);
                check(semicolon.Items[0].UnitPrice == 121.5m && semicolon.Items[1].UnitPrice == 0.5m,
                    "Semicolon CSV accepts decimal commas without depending on the device culture: " + culture);
            }
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }

        RunVariantChecks(check);

        foreach (var text in new[]
            {
                "", "\n" + Header + "\nItem,1,A", "Name,Pret,Categorie / Categorii\nItem,1,A",
                Header + ",Pret\nItem,1,A,2", AttributeHeaders + ",Atribute: Culoare (lista)\nItem,1,A,,,,,,",
                Header, Header + "\n\n  \r\n", Header + "\n,1,A", Header + "\nItem,,A", Header + "\nItem,1,",
                Header + "\nItem,1", Header + "\nItem,1,A,Extra", Header + "\n,,", Header + "\n\"\"",
                Header + "\nItem,1,A >", Header + "\nItem,1,> A", Header + "\nItem,1,A >> B",
                Header + "\nItem,1,A || B", Header + "\nItem,1,A |", Header + "\nItem,1, | A",
                Header + "\nItem,1,A >  > B", Header + "\n\"Unclosed,1,A",
                Header + "\nUn\"quoted,1,A", Header + "\n\"Closed\"trailing,1,A"
            })
            RejectImport(text, check);
        foreach (var price in new[]
            { "-1", "+1", "1e2", "NaN", "12 lei", "1 000", "1.2.3", "1,2.3", ".5", "5.", "1/2",
              "0.00000000000000000000000000001", "1234.1234567890123456789012345678", decimal.MaxValue.ToString(CultureInfo.InvariantCulture),
              (CatalogCsvImporter.MaximumPrice + 1m).ToString(CultureInfo.InvariantCulture) })
            RejectImport(Header + "\nItem,\"" + price + "\",A", check);
        foreach (var vatRate in new[] { -1m, 100.01m, 1.234m })
        {
            var rejected = false;
            try { Import(Header + "\nItem,1,A", vatRate); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "CSV import rejects invalid VAT settings before creating a catalog");
        }
        try
        {
            Import(Header + "\n\"Two\nlines\",1,A\nBad,abc,A", 0m);
            check(false, "Malformed CSV must fail");
        }
        catch (CatalogImportException exception)
        {
            check(exception.RowNumber == 4 && exception.Message.Contains("Row 4"),
                "CSV validation reports the physical row after multiline quoted records");
        }

        var boundary = Import(Header + "\nMaximum," + CatalogCsvImporter.MaximumPrice.ToString(CultureInfo.InvariantCulture) + ",A", 0m);
        var safeLine = new OfferLineItem(1, boundary.Items[0], QuantityValue.Maximum, 100m);
        check(safeLine.NetTotal >= 0m && safeLine.VatAmount >= 0m && safeLine.Total >= safeLine.NetTotal,
            "Largest accepted imported price remains safe at maximum quantity and VAT");

        var oldCatalog = CatalogStore.Current;
        try
        {
            CatalogStore.Replace(catalog);
            check(ReferenceEquals(CatalogStore.Current, catalog), "Static catalog store exposes the complete immutable snapshot");
            try { CatalogStore.Replace(Import(Header + "\nGood,1,A\nBad,invalid,A", 0m)); }
            catch (CatalogImportException) { }
            check(ReferenceEquals(CatalogStore.Current, catalog), "A bad row cannot publish a partially imported replacement catalog");
        }
        finally { CatalogStore.Replace(oldCatalog); }

        var csvFilePath = Path.Combine(Path.GetTempPath(), "EvoOffer-catalog-" + Guid.NewGuid() + ".csv");
        try
        {
            File.WriteAllText(csvFilePath, Header + "\r\nȘurub,21.4,Materiale", new UTF8Encoding(true));
            check(CatalogCsvImporter.ImportFile(csvFilePath, 7m).Items.Single().UnitPrice == 20m,
                "File import reads UTF-8 BOM and Unicode text and uses the selected VAT rate");
        }
        finally { File.Delete(csvFilePath); }
    }

    private static void RunVariantChecks(Action<bool, string> check)
    {
        var reordered = Import("Unused,Atribute: Marime (variante de produs),Pret,Categorie / Categorii,Denumire Produs\n"
            + "ignored,50 mm,12.10,Accesorii,Pensulă", 21m);
        check(reordered.Items.Single().Name == "Pensulă" && reordered.Items[0].Variants[0].Size == "50 mm"
            && reordered.Items[0].UnitPrice == 10m,
            "Required and optional columns are matched by header name and unrelated export columns are ignored");

        var catalog = Import(AttributeHeaders + "\n"
            + " Lac Loba Viva ,455,Lacuri > Interior,Semi-Mat,,5L,,\n"
            + "Pensulă,12.10,Accesorii,,,,,50 mm\n"
            + "Lac Loba Viva,485,Lacuri > Interior,Mat,,5L,,\n"
            + "Lac Loba Viva,885,Finisaje,,Semi-Mat,,10L,\n"
            + "Lac Loba Viva,455,Lacuri > Interior | Promoții,Semi-Mat,,5L,,\n"
            + "Adeziv,242,Adezivi,, Alb ,,\" 13,12 kg \",\n"
            + "Fără atribute,121,Altele,,,,,", 21m);
        var product = catalog.Items[0];
        check(catalog.Items.Select(item => item.Name).SequenceEqual(new[]
            { "Lac Loba Viva", "Pensulă", "Adeziv", "Fără atribute" }),
            "Rows with the same trimmed product name become one dropdown item in first-appearance order");
        check(product.CategoryPaths.SequenceEqual(new[] { "Lacuri > Interior", "Finisaje", "Promoții" }),
            "Grouping variants merges all category memberships in source order, including duplicate rows");
        check(product.Variants.Select(variant => (variant.Size, variant.Color, variant.PriceIncludingVat)).SequenceEqual(new[]
            { ("5L", "Semi-Mat", 455m), ("5L", "Mat", 485m), ("10L", "Semi-Mat", 885m) })
            && !product.HasAmbiguousVariants,
            "The workbook's size/color/price combinations are retained in source order without duplicate rows or invented combinations");
        check(product.Variants.All(variant => variant.UnitPrice == variant.PriceIncludingVat / 1.21m
            && variant.ImportedVatRate == 21m)
            && product.UnitPrice == product.Variants[0].UnitPrice
            && product.PriceIncludingVat == product.Variants[0].PriceIncludingVat,
            "Every variant retains its own gross/net price and default product pricing uses the first source variant");
        check(catalog.Items[1].Variants[0].Size == "50 mm" && catalog.Items[1].Variants[0].Color == string.Empty
            && catalog.Items[2].Variants[0].Size == "13,12 kg" && catalog.Items[2].Variants[0].Color == "Alb"
            && catalog.Items[3].Variants[0].Size == string.Empty && catalog.Items[3].Variants[0].Color == string.Empty,
            "All quantity/size and color aliases resolve while missing attributes and literal decimal commas remain valid");
        check(Import(Header + "\nItem,1,A\nitem,2,A", 0m).Items.Count == 2,
            "Product deduplication uses exact ordinal names rather than merging distinct case-sensitive names");

        var repriced = catalog.WithVatRate(9.5m);
        check(repriced.Items[0].Variants.Count == 3
            && repriced.Items[0].Variants.Zip(product.Variants).All(pair =>
                pair.First.Size == pair.Second.Size && pair.First.Color == pair.Second.Color
                && pair.First.PriceIncludingVat == pair.Second.PriceIncludingVat
                && pair.First.UnitPrice == pair.Second.PriceIncludingVat / 1.095m
                && pair.First.ImportedVatRate == 9.5m && pair.Second.ImportedVatRate == 21m),
            "Changing catalog VAT reprices every variant from unchanged gross prices without modifying the original snapshot");
        RejectMutation(() => ((IList<CatalogVariant>)product.Variants).Clear(), check,
            "Catalog variants cannot be changed through a mutable interface");
        CatalogVariant[] mutableVariants = [new("5L", "Mat", 100m, 121m, 21m)];
        var snapshot = new CatalogItem("Snapshot", mutableVariants, ["A"]);
        mutableVariants[0] = new CatalogVariant("10L", "Semi-Mat", 200m, 242m, 21m);
        check(snapshot.Variants[0].Size == "5L" && snapshot.Variants[0].PriceIncludingVat == 121m,
            "Product variants copy the source collection before exposing the immutable snapshot");
        var legacy = new CatalogItem("A", "Legacy", 25m);
        check(legacy.Variants.Count == 1 && legacy.Variants[0].Size == string.Empty
            && legacy.Variants[0].Color == string.Empty && legacy.Variants[0].ImportedVatRate is null
            && legacy.UnitPrice == 25m,
            "Existing product constructors remain compatible through a single unattributed variant");

        var ambiguous = Import(Header + "\nParchet,121,A\nParchet,242,B\nParchet,121,A", 21m).Items.Single();
        check(ambiguous.HasAmbiguousVariants && ambiguous.Variants.Count == 2
            && ambiguous.Variants.Select(variant => variant.PriceIncludingVat).SequenceEqual(new[] { 121m, 242m }),
            "Conflicting prices for the same unlabelled combination remain identifiable and mark the product as ambiguous");
        var attributedAmbiguity = Import(AttributeHeaders + "\nItem,1,A,Mat,,5L,,\nItem,2,A,Mat,,5L,,", 0m).Items.Single();
        check(attributedAmbiguity.HasAmbiguousVariants && attributedAmbiguity.Variants.Count == 2,
            "Conflicting prices are detected for labelled size/color combinations too");
        var repeatedAliases = Import(AttributeHeaders + "\nItem,121,A, Alb ,Alb,5L, 5L ,5L", 21m).Items.Single();
        check(repeatedAliases.Variants[0].Color == "Alb" && repeatedAliases.Variants[0].Size == "5L",
            "Identical attribute values repeated in alias columns do not create ambiguous combinations");
        foreach (var row in new[] { "Item,1,A,Alb,Negru,5L,,", "Item,1,A,Alb,,5L,10L,", "Item,1,A,Alb,,5L,,50 mm" })
        {
            try
            {
                Import(AttributeHeaders + "\n" + row, 21m);
                check(false, "Conflicting attribute columns must fail");
            }
            catch (CatalogImportException exception)
            {
                check(exception.RowNumber == 2 && exception.Message.Contains("conflicting"),
                    "Conflicting aliases report the source row instead of creating a cross-product of attributes");
            }
        }
    }

    private static ProductCatalog Import(string csv, decimal vatRate) =>
        CatalogCsvImporter.Import(new StringReader(csv), vatRate);

    private static void RejectImport(string csv, Action<bool, string> check)
    {
        var rejected = false;
        try { Import(csv, 21m); }
        catch (CatalogImportException) { rejected = true; }
        check(rejected, "Reject malformed or incomplete CSV without accepting partial data: " + csv.Replace('\n', ' '));
    }

    private static void RejectMutation(Action mutation, Action<bool, string> check, string description)
    {
        var rejected = false;
        try { mutation(); }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, description);
    }
}
