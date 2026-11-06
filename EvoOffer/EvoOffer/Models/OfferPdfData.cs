namespace EvoOffer.Models;

/// <summary>A snapshot of an offer, independent of subsequent edits in the UI.</summary>
public sealed class OfferPdfData
{
    public OfferPdfData(string clientName, string? message, IEnumerable<OfferLineItem> items, AppSettings issuer,
        DiscountDefinition? generalDiscount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(issuer);

        var lines = items.Select(item =>
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.HasQuantityError)
                throw new ArgumentException($"Item {item.Number} has an invalid quantity.", nameof(items));

            return new OfferPdfLine(item.Number, item.Category, item.Name, item.UnitPrice,
                item.Quantity, item.VatRate, item.NetTotal, item.VatAmount, item.Total,
                item.SelectedVariant.Size, item.SelectedVariant.Color, item.UnitOfMeasurement,
                item.Discount, item.OriginalNetTotal, item.OriginalVatAmount, item.OriginalTotal,
                item.DiscountNetAmount, item.DiscountVatAmount, item.DiscountAmount);
        }).ToArray();
        if (lines.Length == 0)
            throw new ArgumentException("Add at least one item before generating an offer.", nameof(items));

        ClientName = clientName.Trim();
        Message = message ?? string.Empty;
        Items = Array.AsReadOnly(lines);
        if (generalDiscount is not null && (!generalDiscount.IsValid || generalDiscount.Scope != DiscountScope.ItemTotal))
            throw new ArgumentException("An offer discount must apply to the whole offer.", nameof(generalDiscount));
        GeneralDiscount = generalDiscount is { Value: > 0m } ? generalDiscount : null;
        IssuerName = issuer.IssuerName;
        LogoPath = string.IsNullOrWhiteSpace(issuer.LogoPath) ? null : issuer.LogoPath;
        Language = issuer.Language == AppSettings.English ? AppSettings.English : AppSettings.Romanian;
        PdfTemplateId = OfferPdfTemplates.Normalize(issuer.PdfTemplateId);
        PdfPrimaryColor = PdfColorValue.Normalize(issuer.PdfPrimaryColor, PdfColorValue.DefaultPrimary);
        PdfSecondaryColor = PdfColorValue.Normalize(issuer.PdfSecondaryColor, PdfColorValue.DefaultSecondary);
        PdfTextColor = PdfColorValue.Normalize(issuer.PdfTextColor, PdfColorValue.DefaultText);
        AddressLine1 = issuer.AddressLine1;
        AddressLine2 = issuer.AddressLine2;
        Email = issuer.Email;
        PhoneNumber = issuer.PhoneNumber;
        VatNumber = issuer.VatNumber;
        IssuerContactLines = Array.AsReadOnly(new[]
        {
            issuer.AddressLine1, issuer.AddressLine2, issuer.Email, issuer.PhoneNumber,
            string.IsNullOrWhiteSpace(issuer.VatNumber) ? string.Empty : $"VAT number: {issuer.VatNumber}"
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
    }

    public string ClientName { get; }
    public string Message { get; }
    public string IssuerName { get; }
    public string? LogoPath { get; }
    public string Language { get; }
    public string PdfTemplateId { get; }
    public string PdfPrimaryColor { get; }
    public string PdfSecondaryColor { get; }
    public string PdfTextColor { get; }
    public string AddressLine1 { get; }
    public string AddressLine2 { get; }
    public string Email { get; }
    public string PhoneNumber { get; }
    public string VatNumber { get; }
    public IReadOnlyList<string> IssuerContactLines { get; }
    public IReadOnlyList<OfferPdfLine> Items { get; }
    public DiscountDefinition? GeneralDiscount { get; }
    public bool HasGeneralDiscount => GeneralDiscount is { Value: > 0m };
    public decimal SubtotalBeforeGeneralDiscount => Items.Sum(item => item.NetTotal);
    public decimal VatTotalBeforeGeneralDiscount => Items.Sum(item => item.VatAmount);
    public decimal TotalBeforeGeneralDiscount => Items.Sum(item => item.Total);
    public decimal GeneralDiscountAmount => GeneralDiscount?.CalculateAmount(TotalBeforeGeneralDiscount) ?? 0m;
    public decimal GeneralDiscountNetAmount => SubtotalBeforeGeneralDiscount - Subtotal;
    public decimal GeneralDiscountVatAmount => VatTotalBeforeGeneralDiscount - VatTotal;
    public decimal Subtotal => GeneralDiscountAmount == 0m ? SubtotalBeforeGeneralDiscount
        : decimal.Round(SubtotalBeforeGeneralDiscount * (GrandTotal / TotalBeforeGeneralDiscount),
            2, MidpointRounding.AwayFromZero);
    public decimal VatTotal => GeneralDiscountAmount == 0m ? VatTotalBeforeGeneralDiscount : GrandTotal - Subtotal;
    public decimal GrandTotal => TotalBeforeGeneralDiscount - GeneralDiscountAmount;
}

public sealed record OfferPdfLine(int Number, string Category, string Name, decimal UnitPrice,
    decimal Quantity, decimal VatRate, decimal NetTotal, decimal VatAmount, decimal Total,
    string Size = "", string Color = "", string UnitOfMeasurement = "",
    DiscountDefinition? Discount = null, decimal OriginalNetTotal = 0m, decimal OriginalVatAmount = 0m,
    decimal OriginalTotal = 0m, decimal DiscountNetAmount = 0m, decimal DiscountVatAmount = 0m,
    decimal DiscountAmount = 0m)
{
    public bool HasDiscount => Discount is { Value: > 0m };
}
