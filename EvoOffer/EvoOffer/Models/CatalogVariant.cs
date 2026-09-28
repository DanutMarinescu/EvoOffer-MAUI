namespace EvoOffer.Models;

/// <summary>One imported size/color combination and its source price.</summary>
public sealed record CatalogVariant
{
    public CatalogVariant(string size, string color, decimal unitPrice, decimal priceIncludingVat,
        decimal? importedVatRate = null)
    {
        Size = size;
        Color = color;
        UnitPrice = unitPrice;
        PriceIncludingVat = priceIncludingVat;
        ImportedVatRate = importedVatRate;
    }

    public string Size { get; }
    public string Color { get; }
    public decimal UnitPrice { get; }
    public decimal PriceIncludingVat { get; }
    public decimal? ImportedVatRate { get; }

    internal CatalogVariant WithVatRate(decimal vatRate) =>
        new(Size, Color, PriceIncludingVat / (1m + vatRate / 100m), PriceIncludingVat, vatRate);
}
