using EvoOffer.Services;
using EvoOffer.Models;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace EvoOffer.Controls;

/// <summary>A category picker whose rows preserve the catalog's hierarchy on every platform.</summary>
public sealed class CategoryDropdown : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IReadOnlyList<CatalogCategory>), typeof(CategoryDropdown),
        defaultValue: null, propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(CatalogCategory), typeof(CategoryDropdown),
        defaultValue: null, defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, _, _) => ((CategoryDropdown)bindable).UpdateSelection());

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(CategoryDropdown), LocalizationService.Get("Controls_CategoryTitle"),
        propertyChanged: (bindable, _, _) => ((CategoryDropdown)bindable).UpdateSelection());

    private const double RowHeight = 44;
    private readonly Button _openButton;
    private readonly Label _selection;
    private readonly Label _arrow;
    private Grid? _host;
    private ContentPage? _page;
    private AbsoluteLayout? _overlay;
#if IOS || MACCATALYST
    private EscapeKeyRecognizer? _escapeKeyRecognizer;
#endif

    public CategoryDropdown()
    {
        _openButton = CreatePlainButton();
        _openButton.Clicked += (_, _) => Toggle();
        _selection = new Label
        {
            FontSize = 17,
            Margin = new Thickness(12, 0, 28, 0),
            VerticalOptions = LayoutOptions.Center,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation,
            InputTransparent = true
        };
        _arrow = new Label
        {
            Text = "⌄",
            FontSize = 20,
            Margin = new Thickness(0, 0, 10, 3),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            TextColor = Color.FromArgb("50627C")
        };
        Content = new Grid { Children = { _openButton, _selection, _arrow } };
        SetDynamicResource(TitleProperty, "Controls_CategoryTitle");
        Loaded += (_, _) =>
        {
            LocalizationService.LanguageChanged += OnLanguageChanged;
            UpdateSelection();
        };
        Unloaded += (_, _) =>
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            Close(false);
        };
        SizeChanged += (_, _) => Close(false);
        UpdateSelection();
    }

    public IReadOnlyList<CatalogCategory>? ItemsSource
    {
        get => (IReadOnlyList<CatalogCategory>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public CatalogCategory? SelectedItem
    {
        get => (CatalogCategory?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var picker = (CategoryDropdown)bindable;
        // A new catalog must never leave selectable rows from the previous snapshot open.
        picker.Close(false);
        picker.UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_selection is null)
            return;
        var selected = SelectedItem;
        _selection.Text = selected?.Name ?? Title;
        _selection.TextColor = Color.FromArgb(selected is null ? "50627C" : "101D31");
        _openButton.IsEnabled = ItemsSource is { Count: > 0 };
        _arrow.Opacity = _openButton.IsEnabled ? 1 : 0.45;
        ToolTipProperties.SetText(_openButton, selected?.Path ?? Title);
        SemanticProperties.SetDescription(_openButton, LocalizationService.Format("Controls_CategoryDescription", selected?.Path ?? Title));
        SemanticProperties.SetHint(_openButton, LocalizationService.Get("Controls_CategoryOpen"));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Close(false);
        UpdateSelection();
    }

    private void Toggle()
    {
        if (_overlay is not null)
        {
            Close();
            return;
        }
        if (ItemsSource is not { Count: > 0 } categories || !IsEnabled)
            return;

        var ancestor = Parent;
        while (ancestor is not null && ancestor is not ContentPage)
            ancestor = ancestor.Parent;
        if (ancestor is not ContentPage { Content: Grid host } page || host.Width <= 0 || host.Height <= 0)
            return;

        var origin = GetOrigin(host);
        if (origin is null)
            return;

        const double edge = 8;
        const double gap = 4;
        var menuWidth = Math.Min(Math.Max(Width, 280), host.Width - edge * 2);
        var desiredHeight = Math.Min(360, categories.Count * RowHeight + 2);
        var below = host.Height - (origin.Value.Y + Height) - edge - gap;
        var above = origin.Value.Y - edge - gap;
        var openBelow = below >= Math.Min(desiredHeight, 132) || below >= above;
        var menuHeight = Math.Min(desiredHeight, openBelow ? below : above);
        if (menuWidth <= 0 || menuHeight < RowHeight)
            return;
        var menuX = Math.Clamp(origin.Value.X, edge, Math.Max(edge, host.Width - menuWidth - edge));
        var menuY = openBelow ? origin.Value.Y + Height + gap : origin.Value.Y - menuHeight - gap;

        var dismiss = CreatePlainButton();
        SemanticProperties.SetDescription(dismiss, LocalizationService.Get("Controls_CategoryDismiss"));
        dismiss.Clicked += (_, _) => Close();

        var list = new CollectionView
        {
            ItemsSource = categories,
            SelectionMode = SelectionMode.None,
            ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            BackgroundColor = Colors.White,
            ItemTemplate = new DataTemplate(() => CreateCategoryRow())
        };
        var panel = new Border
        {
            Stroke = Color.FromArgb("CCD6E2"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Colors.White,
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.14f, Radius = 12, Offset = new Point(0, 4) },
            Content = list
        };

        // Cancel the root Grid's padding so the overlay fills the entire page.
        // Each ancestor's layout position already includes that padding.
        _overlay = new AbsoluteLayout
        {
            Margin = new Thickness(-host.Padding.Left, -host.Padding.Top, -host.Padding.Right, -host.Padding.Bottom),
            ZIndex = 1000
        };
        AbsoluteLayout.SetLayoutBounds(dismiss, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(dismiss, AbsoluteLayoutFlags.All);
        _overlay.Children.Add(dismiss);
        AbsoluteLayout.SetLayoutBounds(panel, new Rect(menuX, menuY, menuWidth, menuHeight));
        _overlay.Children.Add(panel);
        Grid.SetRowSpan(_overlay, Math.Max(1, host.RowDefinitions.Count));
        Grid.SetColumnSpan(_overlay, Math.Max(1, host.ColumnDefinitions.Count));
        _host = host;
        _page = page;
        host.SizeChanged += OnHostSizeChanged;
        page.Disappearing += OnPageDisappearing;
        _overlay.HandlerChanged += OnOverlayHandlerChanged;
        host.Children.Add(_overlay);
        _arrow.Text = "⌃";
        SemanticProperties.SetHint(_openButton, LocalizationService.Get("Controls_CategoryClose"));
        Dispatcher.Dispatch(() =>
        {
            if (_overlay is null)
                return;
            dismiss.Focus();
            if (SelectedItem is { } selected && categories.Contains(selected))
                list.ScrollTo(selected, position: ScrollToPosition.Center, animate: false);
        });
    }

    private Grid CreateCategoryRow()
    {
        var button = CreatePlainButton();
        var name = new Label
        {
            FontSize = 17,
            VerticalOptions = LayoutOptions.Center,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation,
            InputTransparent = true,
            TextColor = Color.FromArgb("101D31")
        };
        var check = new Label
        {
            Text = "✓",
            FontSize = 17,
            TextColor = Color.FromArgb("147EF0"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 12, 0),
            InputTransparent = true
        };
        var row = new Grid { HeightRequest = RowHeight, Children = { button, name, check } };
        row.BindingContextChanged += (_, _) =>
        {
            if (row.BindingContext is not CatalogCategory category)
                return;
            name.Text = category.Name;
            name.FontAttributes = category.IsRoot ? FontAttributes.Bold : FontAttributes.None;
            name.Margin = new Thickness(12 + category.Depth * 20, 0, 36, 0);
            check.IsVisible = category.Path == SelectedItem?.Path;
            button.BackgroundColor = check.IsVisible ? Color.FromArgb("E8F2FE") : Colors.Transparent;
            SemanticProperties.SetDescription(button, category.Path);
            SemanticProperties.SetHint(button, (category.IsRoot ? LocalizationService.Get("Controls_CategoryRoot") : LocalizationService.Format("Controls_CategoryLevel", category.Depth + 1))
                + (check.IsVisible ? LocalizationService.Get("Controls_SelectedSuffix") : string.Empty));
            ToolTipProperties.SetText(button, category.Path);
        };
        button.Clicked += (_, _) =>
        {
            if (row.BindingContext is CatalogCategory category && ItemsSource?.Contains(category) == true)
                SelectedItem = category;
            Close();
        };
        return row;
    }

    private Point? GetOrigin(Grid host)
    {
        double x = 0, y = 0;
        Element? element = this;
        while (element is VisualElement view && element != host)
        {
            x += view.X + view.TranslationX;
            y += view.Y + view.TranslationY;
            element = view.Parent;
        }
        return element == host ? new Point(x, y) : null;
    }

    private static Button CreatePlainButton() => new()
    {
        Text = string.Empty,
        BackgroundColor = Colors.Transparent,
        BorderWidth = 0,
        CornerRadius = 0,
        Padding = 0,
        MinimumHeightRequest = 0,
        MinimumWidthRequest = 0,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill
    };

    private void OnHostSizeChanged(object? sender, EventArgs e) => Close(false);
    private void OnPageDisappearing(object? sender, EventArgs e) => Close(false);

    private void Close(bool restoreFocus = true)
    {
        if (_overlay is null)
            return;
#if WINDOWS
        if (_overlay.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native)
            native.KeyDown -= OnOverlayKeyDown;
#elif IOS || MACCATALYST
        if (_escapeKeyRecognizer is { } recognizer)
        {
            recognizer.View?.RemoveGestureRecognizer(recognizer);
            recognizer.Dispose();
            _escapeKeyRecognizer = null;
        }
#endif
        _overlay.HandlerChanged -= OnOverlayHandlerChanged;
        if (_host is { } host)
        {
            host.SizeChanged -= OnHostSizeChanged;
            host.Children.Remove(_overlay);
        }
        if (_page is { } page)
            page.Disappearing -= OnPageDisappearing;
        _host = null;
        _page = null;
        _overlay = null;
        _arrow.Text = "⌄";
        SemanticProperties.SetHint(_openButton, LocalizationService.Get("Controls_CategoryOpen"));
        if (restoreFocus)
            _openButton.Focus();
    }

    private void OnOverlayHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (_overlay?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native)
            native.KeyDown += OnOverlayKeyDown;
#elif IOS || MACCATALYST
        if (_overlay?.Handler?.PlatformView is UIKit.UIView native)
        {
            native.AccessibilityViewIsModal = true;
            _escapeKeyRecognizer = new EscapeKeyRecognizer(() => Dispatcher.Dispatch(() => Close()));
            native.AddGestureRecognizer(_escapeKeyRecognizer);
        }
#endif
    }

#if WINDOWS
    private void OnOverlayKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
#elif IOS || MACCATALYST
    // A recognizer on the overlay receives hardware-key presses from its child
    // buttons without replacing any of MAUI's native control handlers.
    private sealed class EscapeKeyRecognizer(Action onEscape) : UIKit.UIGestureRecognizer
    {
        public override void PressesBegan(Foundation.NSSet<UIKit.UIPress> presses, UIKit.UIPressesEvent evt)
        {
            if (presses.ToArray().Any(press => press.Key?.CharactersIgnoringModifiers == "\u001b"))
            {
                State = UIKit.UIGestureRecognizerState.Recognized;
                onEscape();
            }
            else
                State = UIKit.UIGestureRecognizerState.Failed;
        }

        public override void TouchesBegan(Foundation.NSSet touches, UIKit.UIEvent evt) =>
            State = UIKit.UIGestureRecognizerState.Failed;
    }
#endif
}
