using EvoOffer.Models;
using EvoOffer.Services;
using Microsoft.Maui.Controls.Shapes;

namespace EvoOffer.Controls;

/// <summary>Edits independent service drafts until the settings page is saved.</summary>
public sealed class SettingsServicesEditor : ContentView
{
    private readonly VerticalStackLayout _rows = new() { Spacing = 12 };
    private readonly List<ServiceRow> _editors = [];
    private readonly Grid _header = CreateTableGrid();
    private readonly Label _empty;
    private bool _compact;
    private int _nextRowId;

    public SettingsServicesEditor(IEnumerable<ServiceDefinition> services)
    {
        AutomationId = "ServicesEditor";
        var headings = new[] { "ServiceName", "ServiceUnit", "ServicePrice" };
        for (var column = 0; column < headings.Length; column++)
            _header.Add(new Label
            {
                Text = LocalizationService.Get(headings[column]),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.End
            }, column);

        _empty = new Label
        {
            Text = LocalizationService.Get("ServicesEmpty"),
            TextColor = Color.FromArgb("50627C"),
            AutomationId = "ServicesEmpty"
        };
        var add = new Button
        {
            Text = LocalizationService.Get("AddService"),
            ImageSource = "plus.png",
            AutomationId = "AddService",
            HorizontalOptions = LayoutOptions.Start
        };
        add.Clicked += (_, _) =>
        {
            var row = AddRow(new ServiceDraft());
            Dispatcher.Dispatch(() => row.NameEntry.Focus());
        };

        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = LocalizationService.Get("Services"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = LocalizationService.Get("ServicesHelp"), TextColor = Color.FromArgb("50627C") },
                _header, _rows, _empty, add
            }
        };
        foreach (var service in services)
            AddRow(new ServiceDraft(service));
        UpdateEmptyState();
        SizeChanged += (_, _) =>
        {
            if (Width <= 0 || _compact == (Width < 560))
                return;
            _compact = Width < 560;
            foreach (var row in _editors)
                row.SetCompact(_compact);
            UpdateEmptyState();
        };
    }

    public bool TryReadServices(out List<ServiceDefinition> services, out Entry? firstInvalid)
    {
        services = [];
        firstInvalid = null;
        foreach (var row in _editors)
        {
            var invalid = row.Validate();
            firstInvalid ??= invalid;
            if (row.Draft.TryBuild(out var service))
                services.Add(service);
        }
        if (firstInvalid is null)
            return true;
        services.Clear();
        return false;
    }

    private ServiceRow AddRow(ServiceDraft draft)
    {
        var row = new ServiceRow(draft, ++_nextRowId);
        row.SetCompact(_compact);
        row.DeleteRequested += (_, _) =>
        {
            _editors.Remove(row);
            _rows.Children.Remove(row);
            UpdateEmptyState();
        };
        _editors.Add(row);
        _rows.Children.Add(row);
        UpdateEmptyState();
        return row;
    }

    private void UpdateEmptyState()
    {
        _empty.IsVisible = _editors.Count == 0;
        _header.IsVisible = !_compact && _editors.Count > 0;
    }

    private static Grid CreateTableGrid() => new()
    {
        ColumnSpacing = 10,
        RowSpacing = 10,
        ColumnDefinitions =
        [
            new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) },
            new ColumnDefinition { Width = new GridLength(44) }
        ]
    };

    private sealed class ServiceRow : ContentView
    {
        private readonly Grid _grid = CreateTableGrid();
        private readonly Field[] _fields;
        private readonly ImageButton _delete;

        public ServiceDraft Draft { get; }
        public Entry NameEntry => _fields[0].Entry;
        public event EventHandler? DeleteRequested;

        public ServiceRow(ServiceDraft draft, int id)
        {
            Draft = draft;
            AutomationId = $"ServiceRow{id}";
            _fields =
            [
                new Field(draft.Name, "ServiceName", "ServiceNameRequired", $"ServiceName{id}",
                    value => Draft.Name = value, () => Draft.HasNameError),
                new Field(draft.UnitOfMeasurement, "ServiceUnit", "ServiceUnitRequired", $"ServiceUnit{id}",
                    value => Draft.UnitOfMeasurement = value, () => Draft.HasUnitError),
                new Field(draft.PriceText, "ServicePrice", "ServicePriceValidation", $"ServicePrice{id}",
                    value => Draft.PriceText = value, () => Draft.HasPriceError)
            ];
            _fields[2].Entry.Keyboard = Keyboard.Numeric;
            _fields[2].Entry.HorizontalTextAlignment = TextAlignment.End;
            _delete = new ImageButton
            {
                Source = "trash.png",
                WidthRequest = 44,
                HeightRequest = 44,
                MinimumWidthRequest = 44,
                MinimumHeightRequest = 44,
                Padding = 12,
                BackgroundColor = Color.FromArgb("FCEDEC"),
                CornerRadius = 6,
                VerticalOptions = LayoutOptions.Start,
                AutomationId = $"DeleteService{id}"
            };
            _delete.Clicked += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
            NameEntry.TextChanged += (_, _) => UpdateDeleteDescription();
            UpdateDeleteDescription();
            for (var column = 0; column < _fields.Length; column++)
                _grid.Add(_fields[column].Container, column);
            _grid.Add(_delete, 3);
            Content = _grid;
        }

        public Entry? Validate()
        {
            Entry? firstInvalid = null;
            foreach (var field in _fields)
                if (!field.Validate())
                    firstInvalid ??= field.Entry;
            return firstInvalid;
        }

        public void SetCompact(bool compact)
        {
            _grid.ColumnDefinitions = compact
                ? [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = new GridLength(44) }]
                : CreateTableGrid().ColumnDefinitions;
            _grid.RowDefinitions = compact
                ? [new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }]
                : [new RowDefinition { Height = GridLength.Auto }];
            for (var index = 0; index < _fields.Length; index++)
            {
                _fields[index].Heading.IsVisible = compact;
                Grid.SetRow(_fields[index].Container, compact ? index : 0);
                Grid.SetColumn(_fields[index].Container, compact ? 0 : index);
            }
            Grid.SetColumn(_delete, compact ? 1 : 3);
            _delete.Margin = compact ? new Thickness(0, 24, 0, 0) : new Thickness(0);
            Padding = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0);
        }

        private void UpdateDeleteDescription()
        {
            var description = string.IsNullOrWhiteSpace(Draft.Name)
                ? LocalizationService.Get("DeleteService")
                : LocalizationService.Format("DeleteServiceDescription", Draft.Name.Trim());
            SemanticProperties.SetDescription(_delete, description);
            ToolTipProperties.SetText(_delete, description);
        }
    }

    private sealed class Field
    {
        private readonly Border _border;
        private readonly Label _error;
        private readonly Func<bool> _hasError;

        public Entry Entry { get; }
        public Label Heading { get; }
        public VerticalStackLayout Container { get; }

        public Field(string text, string labelKey, string errorKey, string automationId, Action<string> setValue, Func<bool> hasError)
        {
            _hasError = hasError;
            var label = LocalizationService.Get(labelKey);
            Entry = new Entry
            {
                Text = text, Placeholder = label, AutomationId = automationId,
                MinimumHeightRequest = 44, FontSize = 16,
                BackgroundColor = Colors.Transparent,
                IsSpellCheckEnabled = false, IsTextPredictionEnabled = false
            };
            SemanticProperties.SetDescription(Entry, label);
            Heading = new Label { Text = label, FontSize = 14, FontAttributes = FontAttributes.Bold, IsVisible = false };
            _border = new Border
            {
                Stroke = Color.FromArgb("B8C8DC"), StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                BackgroundColor = Color.FromArgb("EFF4FA"), Padding = new Thickness(8, 0), Content = Entry
            };
            _error = new Label
            {
                Text = LocalizationService.Get(errorKey), FontSize = 13,
                TextColor = Color.FromArgb("B42318"), IsVisible = false, AutomationId = automationId + "Error"
            };
            Container = new VerticalStackLayout { Spacing = 4, Children = { Heading, _border, _error } };
            Entry.TextChanged += (_, _) =>
            {
                setValue(Entry.Text ?? string.Empty);
                if (_error.IsVisible)
                    Validate();
            };
            Entry.Unfocused += (_, _) => Validate();
        }

        public bool Validate()
        {
            var invalid = _hasError();
            _error.IsVisible = invalid;
            _border.Stroke = Color.FromArgb(invalid ? "B42318" : "B8C8DC");
            SemanticProperties.SetHint(Entry, invalid ? _error.Text : string.Empty);
            return !invalid;
        }
    }
}
