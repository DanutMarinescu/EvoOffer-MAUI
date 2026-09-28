using EvoOffer.Models;

namespace EvoOffer.Services;

/// <summary>Sample products for settings previews, independent of the user's offer and catalog.</summary>
public static class OfferPdfTemplatePreview
{
    public static OfferPdfData Create(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var english = settings.Language == AppSettings.English;
        var vatRate = VatRateValue.IsValid(settings.VatRate) ? settings.VatRate : VatRateValue.Default;
        string Text(string romanian, string en) => english ? en : romanian;
        var items = new[]
        {
            new OfferLineItem(1, new CatalogItem(Text("Pardoseli", "Flooring"), Text("Parchet stejar natural", "Natural oak flooring"), 120m), 12m, vatRate),
            new OfferLineItem(2, new CatalogItem(Text("Accesorii", "Accessories"), Text("Plintă din lemn", "Wood skirting board"), 35m), 8m, vatRate),
            new OfferLineItem(3, new CatalogItem(Text("Servicii", "Services"), Text("Montaj și finisare", "Installation and finishing"), 250m), 1m, vatRate)
        };
        // Copy draft values: previewing must never normalize or otherwise change the settings being edited.
        var issuer = new AppSettings
        {
            IssuerName = string.IsNullOrWhiteSpace(settings.IssuerName) ? Text("Compania Exemplu", "Example Company") : settings.IssuerName,
            LogoPath = settings.LogoPath,
            Email = settings.Email,
            PhoneNumber = settings.PhoneNumber,
            AddressLine1 = settings.AddressLine1,
            AddressLine2 = settings.AddressLine2,
            VatNumber = settings.VatNumber,
            Language = settings.Language,
            PdfTemplateId = settings.PdfTemplateId,
            PdfPrimaryColor = settings.PdfPrimaryColor,
            PdfSecondaryColor = settings.PdfSecondaryColor,
            PdfTextColor = settings.PdfTextColor
        };
        return new OfferPdfData(Text("Client exemplu", "Sample customer"), settings.DefaultMessage, items, issuer);
    }
}
