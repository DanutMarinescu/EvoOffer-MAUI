namespace EvoOffer.Models;

/// <summary>An immutable, completely validated catalog snapshot.</summary>
public sealed class ProductCatalog
{
    public static ProductCatalog Empty { get; } = new([], 0m);

    internal ProductCatalog(IEnumerable<CatalogItem> items, decimal vatRate)
    {
        Items = Array.AsReadOnly(items.ToArray());
        Categories = Array.AsReadOnly(BuildCategories(Items).ToArray());
        VatRate = vatRate;
    }

    public IReadOnlyList<CatalogItem> Items { get; }
    public IReadOnlyList<CatalogCategory> Categories { get; }
    public decimal VatRate { get; }

    public ProductCatalog WithVatRate(decimal vatRate)
    {
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate), VatRateValue.ValidationMessage);
        if (vatRate == VatRate)
            return this;
        return new ProductCatalog(Items.Select(item => item.WithVatRate(vatRate)), vatRate);
    }

    private static IEnumerable<CatalogCategory> BuildCategories(IEnumerable<CatalogItem> items)
    {
        var nodes = new Dictionary<string, CategoryNode>(StringComparer.Ordinal);
        var roots = new List<CategoryNode>();
        foreach (var path in items.SelectMany(item => item.CategoryPaths))
        {
            CategoryNode? parent = null;
            var segments = path.Split(" > ", StringSplitOptions.None);
            var currentPath = string.Empty;
            for (var depth = 0; depth < segments.Length; depth++)
            {
                currentPath = depth == 0 ? segments[depth] : currentPath + " > " + segments[depth];
                if (!nodes.TryGetValue(currentPath, out var node))
                {
                    node = new CategoryNode(new CatalogCategory(currentPath, segments[depth], depth));
                    nodes.Add(currentPath, node);
                    (parent?.Children ?? roots).Add(node);
                }
                parent = node;
            }
        }

        // Iterative traversal also supports unusually deep CSV category paths.
        var pending = new Stack<CategoryNode>(roots.AsEnumerable().Reverse());
        while (pending.TryPop(out var node))
        {
            yield return node.Category;
            for (var index = node.Children.Count - 1; index >= 0; index--)
                pending.Push(node.Children[index]);
        }
    }

    private sealed class CategoryNode(CatalogCategory category)
    {
        public CatalogCategory Category { get; } = category;
        public List<CategoryNode> Children { get; } = [];
    }
}
