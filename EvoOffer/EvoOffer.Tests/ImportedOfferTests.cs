using EvoOffer.Models;
using EvoOffer.Services;

internal static class ImportedOfferTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var (gross, vat) in new[] { (100m, 21m), (99.99m, 19m), (0.03m, 100m), (121m, 21m) })
        {
            var csv = FormattableString.Invariant($"Denumire Produs,Pret,Categorie / Categorii\nProduct,{gross},A > B");
            var product = CatalogCsvImporter.Import(new StringReader(csv), vat).Items.Single();
            var line = new OfferLineItem(1, product, 1m, vat);
            check(line.Total == gross && line.NetTotal + line.VatAmount == gross,
                "Imported gross prices survive VAT rounding and displayed line amounts add up");
            line.Quantity = 2.5m;
            check(line.Total == decimal.Round(gross * 2.5m, 2, MidpointRounding.AwayFromZero)
                && line.Total == line.NetTotal + line.VatAmount,
                "Fractional quantities multiply the imported VAT-inclusive price");
            line.VatRate = 0m;
            check(line.VatAmount == 0m && line.Total == line.NetTotal,
                "Changing an existing offer line to zero VAT preserves its net price");
            line.VatRate = vat;
            check(line.Total == decimal.Round(gross * 2.5m, 2, MidpointRounding.AwayFromZero),
                "Restoring VAT restores the original imported gross price");
        }
    }
}
