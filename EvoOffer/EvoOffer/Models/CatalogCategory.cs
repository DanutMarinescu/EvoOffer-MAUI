namespace EvoOffer.Models;

public sealed record CatalogCategory(string Path, string Name, int Depth)
{
    public bool IsRoot => Depth == 0;

    public override string ToString() => Name;
}
