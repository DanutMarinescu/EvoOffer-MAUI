using EvoOffer.Services;
using EvoOffer.Models;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace EvoOffer.Controls;

/// <summary>A compact color field with an expandable palette and RGB controls.</summary>
public sealed class SettingsColorPicker : ContentView
{
    private readonly string _label;
    private readonly Entry _hexEntry;
    private readonly Border _hexBorder;
    private readonly Label _error;
    private readonly Button _swatch;
    private readonly Button _toggle;
    private readonly Border _pickerPanel;
    private readonly Slider[] _channels = new Slider[3];
    private readonly Label[] _channelValues = new Label[3];
    private readonly List<(Button Button, string Hex, string Name)> _presets = [];
    private bool _updating;
    private string _selectedHex = string.Empty;

    public event EventHandler? ColorChanged;

    public bool IsValid => PdfColorValue.TryNormalize(_hexEntry.Text, out _);

    public string SelectedColorHex => PdfColorValue.TryNormalize(_hexEntry.Text, out var normalized)
        ? normalized
        : throw new InvalidOperationException(LocalizationService.Format("Controls_ColorInvalidSelection", _label));

    public SettingsColorPicker(string label, string selectedHex, string automationId)
    {
        _label = label;
        AutomationId = automationId;

        _hexEntry = new Entry
        {
            AutomationId = automationId + "Hex",
            Placeholder = "#RRGGBB",
            MinimumHeightRequest = 44,
            BackgroundColor = Colors.Transparent,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            ReturnType = ReturnType.Done,
            FontSize = 16
        };
        SemanticProperties.SetDescription(_hexEntry, LocalizationService.Format("Controls_ColorHexDescription", label));
        SemanticProperties.SetHint(_hexEntry, LocalizationService.Get("Controls_ColorHexHint"));
        _hexBorder = new Border
        {
            Stroke = Color.FromArgb("#B8C8DC"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Color.FromArgb("#EFF4FA"),
            Padding = new Thickness(10, 0),
            Content = _hexEntry
        };
        _error = new Label
        {
            Text = LocalizationService.Get("Controls_ColorHexError"),
            TextColor = Color.FromArgb("#B42318"),
            FontSize = 13,
            IsVisible = false,
            AutomationId = automationId + "Error"
        };

        _swatch = new Button
        {
            WidthRequest = 44,
            HeightRequest = 44,
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44,
            Padding = 0,
            BorderColor = Color.FromArgb("#8491A3"),
            BorderWidth = 1,
            AutomationId = automationId + "Swatch"
        };
        _toggle = new Button
        {
            Text = LocalizationService.Get("Controls_ColorChoose"),
            FontSize = 14,
            MinimumHeightRequest = 44,
            Padding = new Thickness(12, 8),
            AutomationId = automationId + "Choose"
        };

        var fields = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            ],
            ColumnSpacing = 10
        };
        fields.Add(_swatch);
        fields.Add(_hexBorder, 1);
        fields.Add(_toggle, 2);

        var palette = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Center
        };
        (string Name, string Hex)[] presets =
        [
            ("Navy", PdfColorValue.DefaultPrimary),
            ("Slate", PdfColorValue.DefaultSecondary),
            ("Charcoal", PdfColorValue.DefaultText),
            ("Blue", "#147EF0"),
            ("Teal", "#0E766E"),
            ("Green", "#16934D"),
            ("Red", "#B42318"),
            ("Orange", "#D97706"),
            ("Gold", "#F4CB42"),
            ("Purple", "#7C3AED"),
            ("Black", "#000000"),
            ("White", "#FFFFFF")
        ];
        foreach (var (name, hex) in presets)
        {
            var color = Color.FromArgb(hex);
            var button = new Button
            {
                BackgroundColor = color,
                TextColor = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue > 0.6
                    ? Colors.Black : Colors.White,
                BorderColor = Color.FromArgb("#8491A3"),
                BorderWidth = 1,
                WidthRequest = 44,
                HeightRequest = 44,
                MinimumWidthRequest = 44,
                MinimumHeightRequest = 44,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = 0,
                FontSize = 20,
                AutomationId = automationId + "Preset" + name
            };
            var localizedName = LocalizationService.Get("Controls_Color" + name);
            ToolTipProperties.SetText(button, $"{localizedName} ({hex})");
            button.Clicked += (_, _) => SetColor(hex, updateEntry: true);
            _presets.Add((button, hex, localizedName));
            palette.Children.Add(button);
        }

        var options = new VerticalStackLayout { Spacing = 8 };
        options.Children.Add(new Label
        {
            Text = LocalizationService.Get("Controls_ColorOptionsHint"),
            TextColor = Color.FromArgb("#50627C"),
            FontSize = 13
        });
        options.Children.Add(palette);
        string[] channelNames = ["Red", "Green", "Blue"];
        for (var i = 0; i < channelNames.Length; i++)
        {
            var channel = channelNames[i];
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                MinimumHeightRequest = 44,
                MinimumTrackColor = Color.FromArgb("#147EF0"),
                MaximumTrackColor = Color.FromArgb("#D2DCE8"),
                ThumbColor = Color.FromArgb("#147EF0"),
                AutomationId = automationId + channel
            };
            SemanticProperties.SetDescription(slider, LocalizationService.Format("Controls_ColorChannelDescription", label, LocalizationService.Get("Controls_Color" + channel)));
            SemanticProperties.SetHint(slider, LocalizationService.Get("Controls_ColorChannelHint"));
            _channels[i] = slider;
            var value = new Label
            {
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center,
                AutomationId = automationId + channel + "Value"
            };
            _channelValues[i] = value;
            var row = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(32) }
                ],
                ColumnSpacing = 8
            };
            row.Add(new Label { Text = LocalizationService.Get("Controls_Color" + channel), FontSize = 14, VerticalOptions = LayoutOptions.Center });
            row.Add(slider, 1);
            row.Add(value, 2);
            options.Children.Add(row);
            slider.ValueChanged += OnChannelChanged;
        }

        _pickerPanel = new Border
        {
            Stroke = Color.FromArgb("#D2DCE8"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Colors.White,
            Padding = 12,
            IsVisible = false,
            Content = options,
            AutomationId = automationId + "Panel"
        };
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = label, FontSize = 16, FontAttributes = FontAttributes.Bold },
                fields,
                _error,
                _pickerPanel
            }
        };

        _hexEntry.TextChanged += (_, _) =>
        {
            if (_updating)
                return;
            if (PdfColorValue.TryNormalize(_hexEntry.Text, out var normalized))
                SetColor(normalized, updateEntry: false);
            else
                UpdateValidation(false);
        };
        _hexEntry.Completed += (_, _) => NormalizeEntry();
        _hexEntry.Unfocused += (_, _) => NormalizeEntry();
        _toggle.Clicked += (_, _) => TogglePicker();
        _swatch.Clicked += (_, _) => TogglePicker();
        SetColor(PdfColorValue.Normalize(selectedHex, PdfColorValue.DefaultPrimary), updateEntry: true);
        UpdateToggleDescription();
    }

    public bool Validate()
    {
        var valid = PdfColorValue.TryNormalize(_hexEntry.Text, out var normalized);
        UpdateValidation(valid);
        if (valid)
            SetColor(normalized, updateEntry: true);
        return valid;
    }

    public void FocusHexEntry() => _hexEntry.Focus();

    private void NormalizeEntry()
    {
        if (PdfColorValue.TryNormalize(_hexEntry.Text, out var normalized))
            SetColor(normalized, updateEntry: true);
    }

    private void OnChannelChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_updating)
            return;
        var red = (int)Math.Round(_channels[0].Value);
        var green = (int)Math.Round(_channels[1].Value);
        var blue = (int)Math.Round(_channels[2].Value);
        SetColor($"#{red:X2}{green:X2}{blue:X2}", updateEntry: true);
    }

    private void SetColor(string hex, bool updateEntry)
    {
        var changed = _selectedHex != hex;
        _updating = true;
        try
        {
            _selectedHex = hex;
            if (updateEntry)
                _hexEntry.Text = hex;
            _swatch.BackgroundColor = Color.FromArgb(hex);
            SemanticProperties.SetDescription(_swatch, LocalizationService.Format("Controls_ColorSwatchDescription", _label, hex));
            ToolTipProperties.SetText(_swatch, hex);
            for (var i = 0; i < _channels.Length; i++)
            {
                var value = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16);
                _channels[i].Value = value;
                _channelValues[i].Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            foreach (var (button, presetHex, name) in _presets)
            {
                var selected = hex == presetHex;
                button.Text = selected ? "✓" : string.Empty;
                button.BorderWidth = selected ? 2 : 1;
                SemanticProperties.SetDescription(button,
                    $"{_label}, {name}, {presetHex}{(selected ? LocalizationService.Get("Controls_SelectedSuffix") : string.Empty)}");
            }
            UpdateValidation(true);
        }
        finally
        {
            _updating = false;
        }
        if (changed)
            ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateValidation(bool valid)
    {
        _error.IsVisible = !valid;
        _hexBorder.Stroke = Color.FromArgb(valid ? "#B8C8DC" : "#B42318");
    }

    private void TogglePicker()
    {
        _pickerPanel.IsVisible = !_pickerPanel.IsVisible;
        _toggle.Text = _pickerPanel.IsVisible ? LocalizationService.Get("Controls_ColorDone") : LocalizationService.Get("Controls_ColorChoose");
        UpdateToggleDescription();
    }

    private void UpdateToggleDescription() => SemanticProperties.SetDescription(_toggle,
        _pickerPanel.IsVisible ? LocalizationService.Format("Controls_ColorClosePicker", _label) : LocalizationService.Format("Controls_ColorOpenPicker", _label));
}
