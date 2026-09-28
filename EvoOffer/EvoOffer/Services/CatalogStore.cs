using EvoOffer.Models;

namespace EvoOffer.Services;

public static class CatalogStore
{
    private static ProductCatalog _current = ProductCatalog.Empty;

    public static ProductCatalog Current => Volatile.Read(ref _current);

    public static void Replace(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Interlocked.Exchange(ref _current, catalog);
    }
}
