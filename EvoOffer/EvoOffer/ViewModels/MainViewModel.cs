using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using EvoOffer.Models;
using EvoOffer.Services;

namespace EvoOffer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public static string DefaultCustomText => LocalizationService.Get("DefaultOfferMessage");

    public static string GetDefaultMessage(string? language) => LocalizationService.Get("DefaultOfferMessage", language);

    public static bool IsDefaultMessage(string? value) =>
        value == GetDefaultMessage(AppSettings.Romanian) || value == GetDefaultMessage(AppSettings.English);

    private ProductCatalog _catalog = ProductCatalog.Empty;
    private IReadOnlyList<CatalogItem> _availableItems = Array.Empty<CatalogItem>();
    private readonly HashSet<OfferLineItem> _observedItems = [];
    private string _clientName = string.Empty;
    private string _customText = DefaultCustomText;
    private CatalogCategory? _selectedCategory;
    private CatalogItem? _selectedItem;
    private decimal _newQuantity = 1m;
    private string _newQuantityText = "1";
    private bool _newQuantityHasError;
    private string _status = LocalizationService.Get("StatusReady");
    private string? _statusKey = "StatusReady";
    private object[] _statusArguments = [];
    private decimal _vatRate;

    public MainViewModel(decimal vatRate = VatRateValue.Default, ProductCatalog? catalog = null)
    {
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        _vatRate = vatRate;
        AddCommand = new RelayCommand(_ => AddItem());
        DeleteCommand = new RelayCommand(parameter => DeleteItem(parameter as OfferLineItem));
        ResetCommand = new RelayCommand(_ => Reset());
        Items.CollectionChanged += ItemsCollectionChanged;
        RefreshCatalog((catalog ?? CatalogStore.Current).WithVatRate(vatRate), preserveSelection: false);
    }

    public string DefaultMessage { get; set; } = DefaultCustomText;

    public decimal VatRate
    {
        get => _vatRate;
        set
        {
            if (!VatRateValue.IsValid(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (!SetProperty(ref _vatRate, value))
                return;

            foreach (var item in Items)
                item.VatRate = value;
            RefreshCatalog(Catalog.WithVatRate(value), preserveSelection: true);
            CatalogStore.Replace(Catalog);
            OnPropertyChanged(nameof(VatHeaderText));
        }
    }

    public string VatHeaderText => LocalizationService.Format("VatColumnHeader", VatRateValue.Format(VatRate));

    public string ClientName
    {
        get => _clientName;
        set => SetProperty(ref _clientName, value ?? string.Empty);
    }

    public string CustomText
    {
        get => _customText;
        set => SetProperty(ref _customText, value ?? string.Empty);
    }

    public ProductCatalog Catalog => _catalog;
    public IReadOnlyList<CatalogCategory> Categories => Catalog.Categories;
    public IReadOnlyList<CatalogItem> AvailableItems => _availableItems;
    public ObservableCollection<OfferLineItem> Items { get; } = [];

    public CatalogCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            var category = value is null ? null : Categories.FirstOrDefault(candidate => candidate.Path == value.Path);
            if (!SetProperty(ref _selectedCategory, category))
                return;

            RefreshAvailableItems();
        }
    }

    public CatalogItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public decimal NewQuantity
    {
        get => _newQuantity;
        set => NewQuantityText = QuantityValue.Format(Math.Clamp(value, QuantityValue.Minimum, QuantityValue.Maximum));
    }

    public string NewQuantityText
    {
        get => _newQuantityText;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _newQuantityText, value))
                return;

            var valid = QuantityValue.TryParse(value, out var quantity);
            if (_newQuantityHasError != !valid)
            {
                _newQuantityHasError = !valid;
                OnPropertyChanged(nameof(NewQuantityHasError));
                OnPropertyChanged(nameof(NewQuantityValidationMessage));
            }

            if (valid && _newQuantity != quantity)
            {
                _newQuantity = quantity;
                OnPropertyChanged(nameof(NewQuantity));
            }
        }
    }

    public bool NewQuantityHasError => _newQuantityHasError;
    public string NewQuantityValidationMessage => NewQuantityHasError ? QuantityValue.ValidationMessage : string.Empty;
    public bool HasValidationErrors => Items.Any(item => item.HasQuantityError);

    public string Status
    {
        get => _status;
        set
        {
            _statusKey = null;
            _statusArguments = [];
            SetProperty(ref _status, value ?? string.Empty);
        }
    }

    public decimal GrandTotal => Items.Sum(item => item.Total);
    public string GrandTotalText => GrandTotal.ToString("N2", LocalizationService.Culture);
    public string GrandTotalSummary => LocalizationService.Format("GrandTotalSummary", GrandTotalText);
    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ResetCommand { get; }

    public void AdjustNewQuantity(int amount) => NewQuantity += amount;

    public void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArguments = args;
        SetProperty(ref _status, LocalizationService.Format(key, args), nameof(Status));
    }

    public void RefreshLocalization()
    {
        if (IsDefaultMessage(DefaultMessage))
            DefaultMessage = DefaultCustomText;
        if (IsDefaultMessage(CustomText))
            CustomText = DefaultCustomText;

        // Keep invalid input available for correction. Valid quantities retain
        // their numeric value when the decimal separator changes.
        if (!NewQuantityHasError)
            SetProperty(ref _newQuantityText, QuantityValue.Format(NewQuantity), nameof(NewQuantityText));
        foreach (var item in Items)
            item.RefreshLocalization();

        OnPropertyChanged(nameof(DefaultMessage));
        OnPropertyChanged(nameof(VatHeaderText));
        OnPropertyChanged(nameof(NewQuantityValidationMessage));
        NotifyOfferTotals();
        if (_statusKey is not null)
            SetProperty(ref _status, LocalizationService.Format(_statusKey, _statusArguments), nameof(Status));
    }

    public void ApplyCatalog(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        RefreshCatalog(catalog.WithVatRate(VatRate), preserveSelection: false);
        CatalogStore.Replace(Catalog);
    }

    private void RefreshCatalog(ProductCatalog catalog, bool preserveSelection)
    {
        var previousCategoryPath = preserveSelection ? SelectedCategory?.Path : null;
        var previousItemIndex = preserveSelection && SelectedItem is not null
            ? Catalog.Items.ToList().FindIndex(item => ReferenceEquals(item, SelectedItem))
            : -1;

        _catalog = catalog;
        OnPropertyChanged(nameof(Catalog));
        OnPropertyChanged(nameof(Categories));
        _selectedCategory = Categories.FirstOrDefault(category => category.Path == previousCategoryPath)
            ?? Categories.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedCategory));

        var preferredItem = previousItemIndex >= 0 && previousItemIndex < Catalog.Items.Count
            ? Catalog.Items[previousItemIndex]
            : null;
        RefreshAvailableItems(preferredItem);
    }

    private void RefreshAvailableItems(CatalogItem? preferredItem = null)
    {
        _availableItems = SelectedCategory is { } category
            ? Array.AsReadOnly(Catalog.Items.Where(item => item.BelongsTo(category)).ToArray())
            : Array.Empty<CatalogItem>();
        OnPropertyChanged(nameof(AvailableItems));
        SelectedItem = preferredItem is not null && AvailableItems.Contains(preferredItem)
            ? preferredItem
            : AvailableItems.FirstOrDefault();
    }

    public bool TryValidateOffer(out string message)
    {
        if (string.IsNullOrWhiteSpace(ClientName))
            SetStatus("ClientNameRequired");
        else if (Items.Count == 0)
            SetStatus("OfferItemsRequired");
        else if (Items.FirstOrDefault(item => item.HasQuantityError) is { } invalidItem)
            SetStatus("ItemQuantityValidation", invalidItem.Number);
        else
        {
            message = string.Empty;
            return true;
        }

        message = Status;
        return false;
    }

    private void AddItem()
    {
        if (SelectedItem is null || !AvailableItems.Contains(SelectedItem))
        {
            SetStatus("SelectProductToAdd");
            return;
        }

        if (!QuantityValue.TryParse(NewQuantityText, out var quantity))
        {
            SetStatus("QuantityValidation");
            return;
        }

        if (SelectedItem.HasAmbiguousVariants)
        {
            SetStatus("AmbiguousProductStatus", SelectedItem.Name);
            return;
        }

        Items.Add(new OfferLineItem(Items.Count + 1, SelectedItem, quantity, VatRate));
        SetStatus("ProductAdded", SelectedItem.Name);
    }

    private void DeleteItem(OfferLineItem? item)
    {
        if (item is not null && Items.Remove(item))
            SetStatus("ProductRemoved", item.Name);
    }

    private void Reset()
    {
        Items.Clear();
        ClientName = string.Empty;
        CustomText = DefaultMessage;
        SelectedCategory = Categories.FirstOrDefault();
        SelectedItem = AvailableItems.FirstOrDefault();
        NewQuantity = 1m;
        SetStatus("OfferResetStatus");
    }

    private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _observedItems)
                item.PropertyChanged -= ItemPropertyChanged;
            _observedItems.Clear();

            foreach (var item in Items)
                ObserveItem(item);
        }
        else if (e.Action != NotifyCollectionChangedAction.Move)
        {
            if (e.OldItems is not null)
                foreach (OfferLineItem item in e.OldItems)
                    if (_observedItems.Remove(item))
                        item.PropertyChanged -= ItemPropertyChanged;

            if (e.NewItems is not null)
                foreach (OfferLineItem item in e.NewItems)
                    ObserveItem(item);
        }

        var firstIndex = e.Action switch
        {
            NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace => e.NewStartingIndex,
            NotifyCollectionChangedAction.Remove => e.OldStartingIndex,
            NotifyCollectionChangedAction.Move => Math.Min(e.OldStartingIndex, e.NewStartingIndex),
            _ => 0
        };
        var endIndex = e.Action switch
        {
            NotifyCollectionChangedAction.Move => Math.Max(e.OldStartingIndex, e.NewStartingIndex) + (e.NewItems?.Count ?? 1),
            NotifyCollectionChangedAction.Replace when e.OldItems?.Count == e.NewItems?.Count =>
                e.NewStartingIndex + (e.NewItems?.Count ?? 0),
            _ => Items.Count
        };
        for (var index = Math.Max(0, firstIndex); index < Math.Min(endIndex, Items.Count); index++)
            Items[index].Number = index + 1;

        NotifyOfferTotals();
        OnPropertyChanged(nameof(HasValidationErrors));
    }

    private void ObserveItem(OfferLineItem item)
    {
        item.VatRate = VatRate;
        if (_observedItems.Add(item))
            item.PropertyChanged += ItemPropertyChanged;
    }

    private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OfferLineItem.Total))
            NotifyOfferTotals();

        if (e.PropertyName == nameof(OfferLineItem.HasQuantityError))
        {
            OnPropertyChanged(nameof(HasValidationErrors));
            if (Items.FirstOrDefault(item => item.HasQuantityError) is { } invalidItem)
                SetStatus("ItemQuantityValidation", invalidItem.Number);
            else
                SetStatus("StatusReady");
        }
    }

    private void NotifyOfferTotals()
    {
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(GrandTotalText));
        OnPropertyChanged(nameof(GrandTotalSummary));
    }
}
