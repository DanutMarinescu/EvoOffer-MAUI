using System.Globalization;

namespace EvoOffer.Models;

public sealed class OfferLineItem : ObservableObject
{
    private int _number;
    private decimal _quantity;
    private string _quantityText;
    private bool _hasQuantityError;
    private decimal _vatRate;

    public OfferLineItem(int number, CatalogItem item, decimal quantity, decimal vatRate = VatRateValue.Default)
    {
        if (quantity < QuantityValue.Minimum || quantity > QuantityValue.Maximum)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate));

        _number = number;
        Category = item.Category;
        Name = item.Name;
        UnitPrice = item.UnitPrice;
        _quantity = quantity;
        _quantityText = QuantityValue.Format(quantity);
        _vatRate = vatRate;
    }

    public int Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }

    public string Category { get; }
    public string Name { get; }
    public decimal UnitPrice { get; }
    public string UnitPriceText => UnitPrice.ToString("N2", CultureInfo.InvariantCulture);

    public decimal VatRate
    {
        get => _vatRate;
        set
        {
            if (!VatRateValue.IsValid(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (SetProperty(ref _vatRate, value))
                NotifyAmounts();
        }
    }

    public decimal Quantity
    {
        get => _quantity;
        set => QuantityText = QuantityValue.Format(Math.Clamp(value, QuantityValue.Minimum, QuantityValue.Maximum));
    }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _quantityText, value))
                return;

            var valid = QuantityValue.TryParse(value, out var quantity);
            if (_hasQuantityError != !valid)
            {
                _hasQuantityError = !valid;
                OnPropertyChanged(nameof(HasQuantityError));
                OnPropertyChanged(nameof(QuantityValidationMessage));
            }

            if (valid && _quantity != quantity)
            {
                _quantity = quantity;
                OnPropertyChanged(nameof(Quantity));
                NotifyAmounts();
            }
        }
    }

    public bool HasQuantityError => _hasQuantityError;
    public string QuantityValidationMessage => HasQuantityError ? QuantityValue.ValidationMessage : string.Empty;
    // Round each line's net amount and VAT so displayed amounts add up to the total.
    public decimal NetTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal VatAmount => decimal.Round(NetTotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public string VatAmountText => VatAmount.ToString("N2", CultureInfo.InvariantCulture);
    public decimal Total => NetTotal + VatAmount;
    public string TotalText => Total.ToString("N2", CultureInfo.InvariantCulture);

    public void AdjustQuantity(int amount) => Quantity += amount;

    private void NotifyAmounts()
    {
        OnPropertyChanged(nameof(NetTotal));
        OnPropertyChanged(nameof(VatAmount));
        OnPropertyChanged(nameof(VatAmountText));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
    }
}
