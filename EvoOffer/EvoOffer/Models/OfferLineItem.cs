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
    private bool _hasAmountError;
    private DiscountDefinition? _discount;

    public OfferLineItem(int number, ServiceDefinition service, decimal quantity, decimal vatRate = VatRateValue.Default)
        : this(number, CreateServiceItem(service), quantity, vatRate)
    {
        UnitOfMeasurement = service.UnitOfMeasurement.Trim();
        IsService = true;
    }

    private static CatalogItem CreateServiceItem(ServiceDefinition service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (!new ServiceDraft(service).TryBuild(out var snapshot))
            throw new ArgumentException(LocalizationService.Get("InvalidServiceValidation"), nameof(service));
        // A service's saved net price is authoritative; it has no imported gross price.
        return new CatalogItem(string.Empty, snapshot.Name, snapshot.PriceExcludingVat);
    }

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
    public string UnitOfMeasurement { get; } = string.Empty;
    public bool HasUnitOfMeasurement => UnitOfMeasurement.Length > 0;
    public bool IsService { get; }
    public bool IsProduct => !IsService;
    public string RemoveDescription => LocalizationService.Format("RemoveProductDescription", Name);
    public string DiscountActionDescription => LocalizationService.Format("DiscountActionDescription", Name);
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
            _hasAmountError = valid && IsService && !CanCalculateServiceAmount(quantity);
            valid &= !_hasAmountError;
            if (_hasQuantityError != !valid)
            {
                _hasQuantityError = !valid;
                OnPropertyChanged(nameof(HasQuantityError));
                OnPropertyChanged(nameof(QuantityValidationMessage));
            }
            else if (!valid)
                OnPropertyChanged(nameof(QuantityValidationMessage));

            if (valid && _quantity != quantity)
            {
                _quantity = quantity;
                OnPropertyChanged(nameof(Quantity));
                NotifyAmounts();
            }
        }
    }

    public bool HasQuantityError => _hasQuantityError;
    public string QuantityValidationMessage => !HasQuantityError ? string.Empty
        : _hasAmountError ? LocalizationService.Get("ServiceAmountTooLarge") : QuantityValue.ValidationMessage;

    private bool CanCalculateServiceAmount(decimal quantity)
    {
        try
        {
            var net = decimal.Round(UnitPrice * quantity, 2, MidpointRounding.AwayFromZero);
            var vat = decimal.Round(net * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
            _ = net + vat;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
    public DiscountDefinition? Discount
    {
        get => _discount;
        set
        {
            if (value is not null)
            {
                var baseAmount = value.Scope == DiscountScope.PerUnit ? OriginalTotal / Quantity : OriginalTotal;
                if (!value.IsValid || (value.ValueType == DiscountValueType.Absolute && value.Value > baseAmount))
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (value.Value == 0m)
                    value = null;
            }
            if (!SetProperty(ref _discount, value))
                return;
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(DiscountDescription));
            NotifyAmounts();
        }
    }

    public bool HasDiscount => Discount is not null;
    public string DiscountDescription => Discount?.Describe() ?? string.Empty;
    public decimal DiscountAmount => Discount?.CalculateAmount(OriginalTotal, Quantity) ?? 0m;
    public string DiscountAmountText => $"−{DiscountAmount.ToString("N2", LocalizationService.Culture)} RON";
    public decimal DiscountNetAmount => OriginalNetTotal - NetTotal;
    public decimal DiscountVatAmount => OriginalVatAmount - VatAmount;
    public string DiscountVatAmountText => $"−{DiscountVatAmount.ToString("N2", LocalizationService.Culture)}";

    // Preserve the undiscounted source amounts, including imported gross prices.
    public decimal OriginalNetTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    // Imported gross prices are authoritative. Derive VAT as the remainder so
    // rounding the net amount cannot change a 100 RON CSV price to 99.99 RON.
    private decimal ImportedGrossTotal => decimal.Round(
        (_selectedVariant.ImportedVatRate == VatRate ? _selectedVariant.PriceIncludingVat : UnitPrice * (1m + VatRate / 100m))
        * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal OriginalVatAmount => _selectedVariant.ImportedVatRate.HasValue
        ? ImportedGrossTotal - OriginalNetTotal
        : decimal.Round(OriginalNetTotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal OriginalTotal => _selectedVariant.ImportedVatRate.HasValue
        ? ImportedGrossTotal : OriginalNetTotal + OriginalVatAmount;

    // Allocate the gross reduction proportionally between net and VAT. Keep
    // VAT as the remainder so every displayed line still reconciles exactly.
    public decimal NetTotal => DiscountAmount == 0m ? OriginalNetTotal
        : Total == 0m ? 0m
        : decimal.Round(OriginalNetTotal * (Total / OriginalTotal), 2, MidpointRounding.AwayFromZero);
    public decimal VatAmount => DiscountAmount == 0m ? OriginalVatAmount : Total - NetTotal;
    public string VatAmountText => VatAmount.ToString("N2", LocalizationService.Culture);
    public decimal Total => OriginalTotal - DiscountAmount;
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
        OnPropertyChanged(nameof(DiscountActionDescription));
        OnPropertyChanged(nameof(DiscountDescription));
        OnPropertyChanged(nameof(DiscountAmountText));
        OnPropertyChanged(nameof(DiscountVatAmountText));
        OnPropertyChanged(nameof(UnitPriceText));
        OnPropertyChanged(nameof(VatAmountText));
        OnPropertyChanged(nameof(TotalText));
    }

    private void NotifyAmounts()
    {
        OnPropertyChanged(nameof(OriginalNetTotal));
        OnPropertyChanged(nameof(OriginalVatAmount));
        OnPropertyChanged(nameof(OriginalTotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(DiscountAmountText));
        OnPropertyChanged(nameof(DiscountNetAmount));
        OnPropertyChanged(nameof(DiscountVatAmount));
        OnPropertyChanged(nameof(DiscountVatAmountText));
        OnPropertyChanged(nameof(NetTotal));
        OnPropertyChanged(nameof(VatAmount));
        OnPropertyChanged(nameof(VatAmountText));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
    }
}
