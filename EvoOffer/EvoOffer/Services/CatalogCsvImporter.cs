using System.Globalization;
using System.Text;
using EvoOffer.Models;

namespace EvoOffer.Services;

public sealed class CatalogImportException(string message, int? rowNumber = null)
    : Exception(rowNumber is null ? message : LocalizationService.Format("Import_RowError", rowNumber, message))
{
    public int? RowNumber { get; } = rowNumber;
}

public static class CatalogCsvImporter
{
    private static readonly string[] RequiredHeaders = ["Denumire Produs", "Pret", "Categorie / Categorii"];
    private static readonly string[] ColorHeaders =
        ["Atribute: Culoare (variante de produs)", "Atribute: Culoare (lista)"];
    private static readonly string[] SizeHeaders =
        ["Atribute: Cantitate (variante de produs)", "Atribute: Cantitate_11 (lista)", "Atribute: Marime (variante de produs)"];
    private static readonly string[] DimensionHeaders =
        ["Atribute: Lungime Mm (variante de produs)", "Atribute: Latime Mm (variante de produs)", "Atribute: Grosime Mm (variante de produs)"];

    // OfferLineItem multiplies the net line amount by the VAT percentage before
    // dividing by 100. Leave rounding headroom at maximum quantity and 100% VAT.
    public static decimal MaximumPrice { get; } =
        decimal.Floor(decimal.MaxValue / QuantityValue.Maximum / 100m) - 1m;

    public static ProductCatalog ImportFile(string path, decimal vatRate)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return Import(reader, vatRate);
    }

    public static ProductCatalog Import(TextReader reader, decimal vatRate)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate), VatRateValue.ValidationMessage);

        string csv;
        try { csv = reader.ReadToEnd().TrimStart('\uFEFF'); }
        catch (DecoderFallbackException)
        {
            throw new CatalogImportException(LocalizationService.Get("Import_InvalidEncoding"));
        }

        using var records = ReadRecords(csv, DetectDelimiter(csv)).GetEnumerator();
        if (!records.MoveNext())
            throw new CatalogImportException(LocalizationService.Get("Import_RequiredHeaderRow"), 1);
        var headerFields = records.Current.Fields;
        var headerIndexes = MapHeaders(headerFields);
        var colorIndexes = ColorHeaders.Where(headerIndexes.ContainsKey).Select(header => headerIndexes[header]).ToArray();
        var sizeIndexes = SizeHeaders.Where(headerIndexes.ContainsKey).Select(header => headerIndexes[header]).ToArray();
        var dimensionIndexes = DimensionHeaders.Select(header => headerIndexes.GetValueOrDefault(header, -1)).ToArray();

        var products = new List<ProductRows>();
        var productsByName = new Dictionary<string, ProductRows>(StringComparer.Ordinal);
        while (records.MoveNext())
        {
            var record = records.Current;
            if (record.IsBlank)
                continue;
            if (record.Fields.Length != headerFields.Length)
                throw new CatalogImportException(LocalizationService.Format("Import_FieldCount", headerFields.Length), record.RowNumber);

            var name = record.Fields[headerIndexes["Denumire Produs"]].Trim();
            if (name.Length == 0)
                throw new CatalogImportException(LocalizationService.Get("Import_NameRequired"), record.RowNumber);
            var priceText = record.Fields[headerIndexes["Pret"]].Trim();
            if (!TryParsePrice(priceText, out var grossPrice))
                throw new CatalogImportException(LocalizationService.Get("Import_InvalidPrice"), record.RowNumber);
            if (grossPrice > MaximumPrice)
                throw new CatalogImportException(LocalizationService.Get("Import_PriceTooLarge"), record.RowNumber);

            var paths = ParseCategoryPaths(record.Fields[headerIndexes["Categorie / Categorii"]], record.RowNumber);
            var color = ReadAttribute(record, colorIndexes, LocalizationService.Get("Import_ColorAttribute"));
            var size = ReadAttribute(record, sizeIndexes, LocalizationService.Get("Import_SizeAttribute"));
            if (size.Length == 0)
                size = ReadDimensions(record, dimensionIndexes);
            if (!productsByName.TryGetValue(name, out var product))
            {
                product = new ProductRows(name);
                productsByName.Add(name, product);
                products.Add(product);
            }
            product.Add(paths, new CatalogVariant(size, color, grossPrice / (1m + vatRate / 100m), grossPrice, vatRate));
        }

        if (products.Count == 0)
            throw new CatalogImportException(LocalizationService.Get("Import_NoProducts"));
        return new ProductCatalog(products.Select(product => product.ToCatalogItem()), vatRate);
    }

    private static Dictionary<string, int> MapHeaders(string[] fields)
    {
        var knownHeaders = RequiredHeaders.Concat(ColorHeaders).Concat(SizeHeaders).Concat(DimensionHeaders)
            .ToHashSet(StringComparer.Ordinal);
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < fields.Length; index++)
        {
            var header = fields[index].Trim();
            // Other export columns are not part of the product selection contract.
            if (knownHeaders.Contains(header) && !indexes.TryAdd(header, index))
                throw new CatalogImportException(LocalizationService.Format("Import_DuplicateHeader", header), 1);
        }
        var missing = RequiredHeaders.Where(header => !indexes.ContainsKey(header)).ToArray();
        if (missing.Length > 0)
            throw new CatalogImportException(LocalizationService.Format("Import_MissingHeaders", string.Join(", ", missing)), 1);
        return indexes;
    }

    private static string ReadAttribute(CsvRecord record, int[] indexes, string attributeName)
    {
        var values = indexes.Select(index => record.Fields[index].Trim()).Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length > 1)
            throw new CatalogImportException(LocalizationService.Format("Import_ConflictingAttributes", attributeName), record.RowNumber);
        return values.FirstOrDefault() ?? string.Empty;
    }

    private static string ReadDimensions(CsvRecord record, int[] indexes)
    {
        // Dimension columns and individual cells are optional. Keep the populated
        // values in length/width/thickness order without empty units or separators.
        var values = indexes.Select(index => index < 0 ? string.Empty : record.Fields[index].Trim())
            .Where(value => value.Length > 0);
        return string.Join(" x ", values.Select(value => value + " mm"));
    }

    private static string[] ParseCategoryPaths(string text, int rowNumber)
    {
        var paths = new List<string>();
        foreach (var rawPath in text.Split('|'))
        {
            var segments = rawPath.Split('>').Select(segment => segment.Trim()).ToArray();
            if (segments.Any(string.IsNullOrEmpty))
                throw new CatalogImportException(LocalizationService.Get("Import_InvalidCategory"), rowNumber);
            paths.Add(string.Join(" > ", segments));
        }
        return paths.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryParsePrice(string text, out decimal price)
    {
        price = 0m;
        var separator = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is >= '0' and <= '9')
                continue;
            if (text[index] is not ('.' or ',') || separator >= 0 || index == 0 || index == text.Length - 1)
                return false;
            separator = index;
        }
        if (separator >= 0 && text.Length - separator - 1 > 28)
            return false;
        var normalized = text.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price))
            return false;
        // decimal.TryParse can silently round excess significant digits. Prices
        // must remain exact, so verify the parsed value against canonical text.
        normalized = normalized.TrimStart('0');
        if (normalized.Contains('.'))
            normalized = normalized.TrimEnd('0').TrimEnd('.');
        if (normalized.Length == 0 || normalized.StartsWith('.'))
            normalized = "0" + normalized;
        return price.ToString("0.############################", CultureInfo.InvariantCulture) == normalized;
    }

    private static char DetectDelimiter(string csv)
    {
        var quoted = false;
        var commas = 0;
        var semicolons = 0;
        foreach (var character in csv)
        {
            if (character == '"')
                quoted = !quoted;
            else if (!quoted)
            {
                if (character is '\r' or '\n')
                    break;
                if (character == ',') commas++;
                if (character == ';') semicolons++;
            }
        }
        return semicolons > commas ? ';' : ',';
    }

    private static IEnumerable<CsvRecord> ReadRecords(string csv, char delimiter)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var rowNumber = 1;
        var recordRow = 1;
        var quoted = false;
        var quoteClosed = false;
        var hasSyntax = false;
        var started = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            started = true;
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                {
                    field.Append(character);
                    if (character == '\r' || (character == '\n' && (index == 0 || csv[index - 1] != '\r')))
                        rowNumber++;
                }
                continue;
            }

            if (character == delimiter || character is '\r' or '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                quoteClosed = false;
                if (character == delimiter)
                {
                    hasSyntax = true;
                    continue;
                }
                yield return new CsvRecord(fields.ToArray(), recordRow,
                    !hasSyntax && fields.All(string.IsNullOrWhiteSpace));
                fields.Clear();
                hasSyntax = false;
                started = false;
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                    index++;
                rowNumber++;
                recordRow = rowNumber;
            }
            else if (character == '"')
            {
                if (quoteClosed || field.ToString().Any(c => !char.IsWhiteSpace(c)))
                    throw new CatalogImportException(LocalizationService.Get("Import_UnexpectedQuote"), rowNumber);
                field.Clear();
                quoted = true;
                hasSyntax = true;
            }
            else
            {
                if (quoteClosed && !char.IsWhiteSpace(character))
                    throw new CatalogImportException(LocalizationService.Get("Import_TextAfterQuote"), rowNumber);
                if (!quoteClosed)
                    field.Append(character);
            }
        }

        if (quoted)
            throw new CatalogImportException(LocalizationService.Get("Import_UnclosedQuote"), recordRow);
        if (started)
        {
            fields.Add(field.ToString());
            yield return new CsvRecord(fields.ToArray(), recordRow,
                !hasSyntax && fields.All(string.IsNullOrWhiteSpace));
        }
    }

    private sealed record CsvRecord(string[] Fields, int RowNumber, bool IsBlank);

    private sealed class ProductRows(string name)
    {
        private readonly List<string> categoryPaths = [];
        private readonly HashSet<string> categorySet = new(StringComparer.Ordinal);
        private readonly List<CatalogVariant> variants = [];
        private readonly HashSet<(string Size, string Color, decimal Price)> variantSet = [];

        public void Add(IEnumerable<string> paths, CatalogVariant variant)
        {
            foreach (var path in paths)
                if (categorySet.Add(path))
                    categoryPaths.Add(path);
            if (variantSet.Add((variant.Size, variant.Color, variant.PriceIncludingVat)))
                variants.Add(variant);
        }

        public CatalogItem ToCatalogItem() => new(name, variants, categoryPaths);
    }
}
