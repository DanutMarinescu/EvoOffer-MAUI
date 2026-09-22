using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using EvoOffer.Models;

namespace EvoOffer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public const string DefaultCustomText = "Thank you for your interest in our products and services. Below you will find our best offer tailored to your needs. If you have any questions, please do not hesitate to contact us.";

    private readonly CatalogItem[] _catalog =
    [
        new("Parchet lemn masiv", "Stejar Natur 14 mm", 180m),
        new("Parchet stratificat", "Stejar Rustic 13 mm", 150m),
        new("Accesorii", "Plintă MDF albă", 25m),
        new("Montaj", "Montaj parchet", 40m)
    ];

    private readonly HashSet<OfferLineItem> _observedItems = [];
    private string _clientName = "ACME SRL";
    private string _customText = DefaultCustomText;
    private string? _selectedCategory;
    private CatalogItem? _selectedItem;
    private decimal _newQuantity = 1m;
    private string _newQuantityText = "1";
    private bool _newQuantityHasError;
    private string _status = "Ready";
    private decimal _vatRate;

    public MainViewModel(decimal vatRate = VatRateValue.Default)
    {
        if (!VatRateValue.IsValid(vatRate))
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        _vatRate = vatRate;
        Categories = new ObservableCollection<string>(_catalog.Select(item => item.Category).Distinct());
        AddCommand = new RelayCommand(_ => AddItem());
        DeleteCommand = new RelayCommand(parameter => DeleteItem(parameter as OfferLineItem));
        ResetCommand = new RelayCommand(_ => Reset());
        Items.CollectionChanged += ItemsCollectionChanged;
        SelectedCategory = Categories.FirstOrDefault();

        decimal[] initialQuantities = [10m, 5m, 12m, 20m];
        for (var index = 0; index < _catalog.Length; index++)
            Items.Add(new OfferLineItem(index + 1, _catalog[index], initialQuantities[index], VatRate));
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
            OnPropertyChanged(nameof(VatHeaderText));
        }
    }

    public string VatHeaderText => $"VAT {VatRateValue.Format(VatRate)}%\n(RON)";

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

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<CatalogItem> AvailableItems { get; } = [];
    public ObservableCollection<OfferLineItem> Items { get; } = [];

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
                return;

            AvailableItems.Clear();
            foreach (var item in _catalog.Where(item => item.Category == value))
                AvailableItems.Add(item);
            SelectedItem = AvailableItems.FirstOrDefault();
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
        set => SetProperty(ref _status, value);
    }

    public decimal GrandTotal => Items.Sum(item => item.Total);
    public string GrandTotalText => GrandTotal.ToString("N2", CultureInfo.InvariantCulture);
    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ResetCommand { get; }

    public void AdjustNewQuantity(int amount) => NewQuantity += amount;

    public bool TryValidateOffer(out string message)
    {
        if (string.IsNullOrWhiteSpace(ClientName))
            message = "Enter a client name before generating an offer.";
        else if (Items.Count == 0)
            message = "Add at least one item before generating an offer.";
        else if (Items.FirstOrDefault(item => item.HasQuantityError) is { } invalidItem)
            message = $"Item {invalidItem.Number}: {QuantityValue.ValidationMessage}";
        else
        {
            message = string.Empty;
            return true;
        }

        Status = message;
        return false;
    }

    private void AddItem()
    {
        if (SelectedItem is null || !AvailableItems.Contains(SelectedItem))
        {
            Status = "Select a category and an item to add.";
            return;
        }

        if (!QuantityValue.TryParse(NewQuantityText, out var quantity))
        {
            Status = QuantityValue.ValidationMessage;
            return;
        }

        Items.Add(new OfferLineItem(Items.Count + 1, SelectedItem, quantity, VatRate));
        Status = $"Added {SelectedItem.Name}.";
    }

    private void DeleteItem(OfferLineItem? item)
    {
        if (item is not null && Items.Remove(item))
            Status = $"Removed {item.Name}.";
    }

    private void Reset()
    {
        Items.Clear();
        ClientName = string.Empty;
        CustomText = DefaultMessage;
        SelectedCategory = Categories.FirstOrDefault();
        SelectedItem = AvailableItems.FirstOrDefault();
        NewQuantity = 1m;
        Status = "Offer reset. Ready for a new client.";
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
            Status = Items.FirstOrDefault(item => item.HasQuantityError) is { } invalidItem
                ? $"Item {invalidItem.Number}: {QuantityValue.ValidationMessage}"
                : "Ready";
        }
    }

    private void NotifyOfferTotals()
    {
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(GrandTotalText));
    }
}
