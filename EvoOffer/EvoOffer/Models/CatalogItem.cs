namespace EvoOffer.Models;

public sealed record CatalogItem(string Category, string Name, decimal UnitPrice)
{
    public override string ToString() => Name;
}
