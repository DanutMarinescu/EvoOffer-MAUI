namespace EvoOffer.Models;

public sealed record CatalogItem
{
    // Existing offer/PDF callers supply a VAT-exclusive unit price.
    public CatalogItem(string category, string name, decimal unitPrice)
        : this(name, unitPrice, unitPrice,
            string.IsNullOrEmpty(category) ? [] : [category])
    {
    }

    internal CatalogItem(string name, decimal priceIncludingVat, decimal unitPrice, IEnumerable<string> categoryPaths,
        decimal? importedVatRate = null)
        : this(name, [new CatalogVariant(string.Empty, string.Empty, unitPrice, priceIncludingVat, importedVatRate)], categoryPaths)
    {
    }

    public CatalogItem(string name, IEnumerable<CatalogVariant> variants, IEnumerable<string> categoryPaths)
    {
        ArgumentNullException.ThrowIfNull(variants);
        Name = name;
        Variants = Array.AsReadOnly(variants.ToArray());
        if (Variants.Count == 0)
            throw new ArgumentException("A catalog product must contain at least one variant.", nameof(variants));
        CategoryPaths = Array.AsReadOnly(categoryPaths.ToArray());
        Category = string.Join(" | ", CategoryPaths);
        HasAmbiguousVariants = Variants.GroupBy(variant => (variant.Size, variant.Color))
            .Any(group => group.Select(variant => variant.PriceIncludingVat).Distinct().Skip(1).Any());
    }

    public string Category { get; }
    public string Name { get; }
    public decimal UnitPrice => Variants[0].UnitPrice;
    public decimal PriceIncludingVat => Variants[0].PriceIncludingVat;
    public decimal? ImportedVatRate => Variants[0].ImportedVatRate;
    public IReadOnlyList<string> CategoryPaths { get; }
    public IReadOnlyList<CatalogVariant> Variants { get; }
    public bool HasAmbiguousVariants { get; }

    public bool BelongsTo(CatalogCategory category) => CategoryPaths.Any(path =>
        path.Equals(category.Path, StringComparison.Ordinal)
        || path.StartsWith(category.Path + " > ", StringComparison.Ordinal));

    internal CatalogItem WithVatRate(decimal vatRate) =>
        new(Name, Variants.Select(variant => variant.WithVatRate(vatRate)), CategoryPaths);

    public override string ToString() => Name;
}
