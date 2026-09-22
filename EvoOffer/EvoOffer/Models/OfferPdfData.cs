namespace EvoOffer.Models;

/// <summary>A snapshot of an offer, independent of subsequent edits in the UI.</summary>
public sealed class OfferPdfData
{
    public OfferPdfData(string clientName, string? message, IEnumerable<OfferLineItem> items, AppSettings issuer)
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
                item.Quantity, item.VatRate, item.NetTotal, item.VatAmount, item.Total);
        }).ToArray();
        if (lines.Length == 0)
            throw new ArgumentException("Add at least one item before generating an offer.", nameof(items));

        ClientName = clientName.Trim();
        Message = message ?? string.Empty;
        Items = Array.AsReadOnly(lines);
        IssuerName = issuer.IssuerName;
        LogoPath = string.IsNullOrWhiteSpace(issuer.LogoPath) ? null : issuer.LogoPath;
        Language = issuer.Language == AppSettings.English ? AppSettings.English : AppSettings.Romanian;
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
    public string AddressLine1 { get; }
    public string AddressLine2 { get; }
    public string Email { get; }
    public string PhoneNumber { get; }
    public string VatNumber { get; }
    public IReadOnlyList<string> IssuerContactLines { get; }
    public IReadOnlyList<OfferPdfLine> Items { get; }
    public decimal Subtotal => Items.Sum(item => item.NetTotal);
    public decimal VatTotal => Items.Sum(item => item.VatAmount);
    public decimal GrandTotal => Items.Sum(item => item.Total);
}

public sealed record OfferPdfLine(int Number, string Category, string Name, decimal UnitPrice,
    decimal Quantity, decimal VatRate, decimal NetTotal, decimal VatAmount, decimal Total);
