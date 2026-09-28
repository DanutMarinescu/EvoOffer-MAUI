using EvoOffer.Services;

namespace EvoOffer.Models;

public sealed class OfferLineItem : ObservableObject
{
    private int _number;
    private decimal _quantity;
    private string _quantityText;
    private bool _hasQuantityError;
    private decimal _vatRate;
    private readonly IReadOnlyList<CatalogVariant> _variants;
    private string _unspecifiedSize;
    private string _unspecifiedColor;
    private CatalogVariant _selectedVariant;
    private bool _updatingSelection;

    public OfferLineItem(int number, CatalogItem item, decimal quantity, decimal vatRate = VatRateValue.Default)
    {
        if (quantity < QuantityValue.Minimum || quantity > QuantityValue.Maximum)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        ArgumentNullException.ThrowIfNull(item);
        if (item.HasAmbiguousVariants)
            throw new ArgumentException(LocalizationService.Get("AmbiguousProductValidation"), nameof(item));

        _number = number;
        Category = item.Category;
        Name = item.Name;
        _variants = item.Variants;
        _selectedVariant = _variants[0];
        _unspecifiedSize = MissingAttributeText(_variants.Select(variant => variant.Size));
        _unspecifiedColor = MissingAttributeText(_variants.Select(variant => variant.Color));
        AvailableSizes = Array.AsReadOnly(_variants.Select(variant => SizeText(variant.Size)).Distinct(StringComparer.Ordinal).ToArray());
        AvailableColors = Array.AsReadOnly(_variants.Select(variant => ColorText(variant.Color)).Distinct(StringComparer.Ordinal).ToArray());
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
    public string RemoveDescription => LocalizationService.Format("RemoveProductDescription", Name);
    public decimal UnitPrice => _selectedVariant.UnitPrice;
    public string UnitPriceText => UnitPrice.ToString("N2", LocalizationService.Culture);
    public CatalogVariant SelectedVariant => _selectedVariant;
    public IReadOnlyList<string> AvailableSizes { get; private set; }
    public IReadOnlyList<string> AvailableColors { get; private set; }
    public bool HasSizeOptions => _variants.Any(variant => variant.Size.Length > 0);
    public bool HasColorOptions => _variants.Any(variant => variant.Color.Length > 0);

    public string SelectedSize
    {
        get => SizeText(_selectedVariant.Size);
        set => SelectVariant(value, changingSize: true);
    }

    public string SelectedColor
    {
        get => ColorText(_selectedVariant.Color);
        set => SelectVariant(value, changingSize: false);
    }

    private string SizeText(string value) => value.Length == 0 ? _unspecifiedSize : value;
    private string ColorText(string value) => value.Length == 0 ? _unspecifiedColor : value;

    private static string MissingAttributeText(IEnumerable<string> values)
    {
        // Missing attributes must never shadow a literal label from the CSV.
        var labels = values.ToHashSet(StringComparer.Ordinal);
        var placeholder = "—";
        for (var suffix = 1; labels.Contains(placeholder); suffix++)
            placeholder = suffix == 1
                ? LocalizationService.Get("UnspecifiedAttribute")
                : LocalizationService.Format("UnspecifiedAttributeNumbered", suffix);
        return placeholder;
    }

    private void SelectVariant(string? value, bool changingSize)
    {
        if (_updatingSelection)
            return;

        var candidates = _variants.Where(variant => changingSize
            ? SizeText(variant.Size) == value
            : ColorText(variant.Color) == value).ToArray();
        // Keep the other selection whenever possible, otherwise use the first
        // actual CSV combination. Never synthesize a size/color pair or price.
        var selected = candidates.FirstOrDefault(variant => changingSize
            ? variant.Color == _selectedVariant.Color
            : variant.Size == _selectedVariant.Size) ?? candidates.FirstOrDefault();
        if (ReferenceEquals(selected, _selectedVariant))
            return;

        _updatingSelection = true;
        try
        {
            if (selected is null)
            {
                // A transient cleared picker or an unknown value cannot clear a
                // valid selection. Push the current value back to the binding.
                OnPropertyChanged(changingSize ? nameof(SelectedSize) : nameof(SelectedColor));
                return;
            }

            _selectedVariant = selected;
            OnPropertyChanged(nameof(SelectedVariant));
            OnPropertyChanged(nameof(SelectedSize));
            OnPropertyChanged(nameof(SelectedColor));
            OnPropertyChanged(nameof(UnitPrice));
            OnPropertyChanged(nameof(UnitPriceText));
            NotifyAmounts();
        }
        finally { _updatingSelection = false; }
    }

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
    // Imported gross prices are authoritative. Derive VAT as the remainder so
    // rounding the net amount cannot change a 100 RON CSV price to 99.99 RON.
    private decimal ImportedGrossTotal => decimal.Round(
        (_selectedVariant.ImportedVatRate == VatRate ? _selectedVariant.PriceIncludingVat : UnitPrice * (1m + VatRate / 100m))
        * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal VatAmount => _selectedVariant.ImportedVatRate.HasValue
        ? ImportedGrossTotal - NetTotal
        : decimal.Round(NetTotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public string VatAmountText => VatAmount.ToString("N2", LocalizationService.Culture);
    public decimal Total => _selectedVariant.ImportedVatRate.HasValue ? ImportedGrossTotal : NetTotal + VatAmount;
    public string TotalText => Total.ToString("N2", LocalizationService.Culture);

    public void AdjustQuantity(int amount) => Quantity += amount;

    public void RefreshLocalization()
    {
        // Refresh only display labels, preserving the exact imported variant.
        // Picker updates can send transient selections while their lists change.
        _updatingSelection = true;
        try
        {
            _unspecifiedSize = MissingAttributeText(_variants.Select(variant => variant.Size));
            _unspecifiedColor = MissingAttributeText(_variants.Select(variant => variant.Color));
            AvailableSizes = Array.AsReadOnly(_variants.Select(variant => SizeText(variant.Size)).Distinct(StringComparer.Ordinal).ToArray());
            AvailableColors = Array.AsReadOnly(_variants.Select(variant => ColorText(variant.Color)).Distinct(StringComparer.Ordinal).ToArray());
            OnPropertyChanged(nameof(AvailableSizes));
            OnPropertyChanged(nameof(AvailableColors));
            OnPropertyChanged(nameof(SelectedSize));
            OnPropertyChanged(nameof(SelectedColor));
        }
        finally { _updatingSelection = false; }

        if (!HasQuantityError)
            SetProperty(ref _quantityText, QuantityValue.Format(Quantity), nameof(QuantityText));
        OnPropertyChanged(nameof(QuantityValidationMessage));
        OnPropertyChanged(nameof(RemoveDescription));
        OnPropertyChanged(nameof(UnitPriceText));
        OnPropertyChanged(nameof(VatAmountText));
        OnPropertyChanged(nameof(TotalText));
    }

    private void NotifyAmounts()
    {
        OnPropertyChanged(nameof(NetTotal));
        OnPropertyChanged(nameof(VatAmount));
        OnPropertyChanged(nameof(VatAmountText));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
    }
}
