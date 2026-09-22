using System.Collections.Specialized;
using System.Text.Json;
using EvoOffer.Controls;
using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

namespace EvoOffer;

public partial class MainPage : ContentPage
{
    private const string DefaultMessagePreference = "offer_default_message";
    private const string VatRatePreference = "offer_vat_percentage";
    private const double MinimumTableWidth = 1160;
    private readonly MainViewModel _viewModel;
    private readonly IOfferPdfService _pdfService;
    private readonly SettingsStore _settingsStore = new(FileSystem.Current.AppDataDirectory);
    private AppSettings _settings;
    private bool? _usingSidebar;
    private bool? _usingCompactComposer;
    private bool _openingDialog;

    public MainPage(IOfferPdfService pdfService)
    {
        ArgumentNullException.ThrowIfNull(pdfService);
        _pdfService = pdfService;
        InitializeComponent();
        var savedVatRate = Preferences.Default.Get(VatRatePreference, VatRateValue.Format(VatRateValue.Default));
        var initialSettings = new AppSettings
        {
            VatRate = VatRateValue.TryParse(savedVatRate, out var vatRate) ? vatRate : VatRateValue.Default,
            DefaultMessage = Preferences.Default.Get(DefaultMessagePreference, MainViewModel.DefaultCustomText)
        };
        string? loadError = null;
        try
        {
            _settings = _settingsStore.Load(initialSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _settings = initialSettings;
            loadError = "Could not read or create settings.json. Using default settings; open Settings to save again.";
        }
        SettingsPathPicker.RestoreSavedAccess();
        _viewModel = new MainViewModel(_settings.VatRate);
        _viewModel.DefaultMessage = _settings.DefaultMessage;
        _viewModel.CustomText = _viewModel.DefaultMessage;
        if (loadError is not null)
            _viewModel.Status = loadError;
        BindingContext = _viewModel;
        SizeChanged += OnPageSizeChanged;
        FormArea.SizeChanged += OnFormAreaSizeChanged;
        _viewModel.Items.CollectionChanged += OnItemsChanged;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
            return;

        var sidebar = Width >= 1180;
        var shortWindow = Height < 820;
        var compact = shortWindow || !sidebar;
        PageLayout.Padding = shortWindow ? new Thickness(12) : compact ? new Thickness(16, 12, 16, 12) : new Thickness(24, 20, 24, 16);
        PageLayout.RowSpacing = shortWindow ? 8 : compact ? 12 : 18;
        Header.MinimumHeightRequest = shortWindow ? 48 : compact ? 64 : 82;
        Subtitle.IsVisible = !shortWindow;
        Header.ColumnSpacing = compact ? 16 : 24;
        Heading.FontSize = Width < 1000 ? 27 : 34;
        Subtitle.FontSize = compact ? 16 : 19;
        BrandIcon.WidthRequest = compact ? 46 : 60;
        BrandIcon.HeightRequest = shortWindow ? 42 : compact ? 52 : 64;
        ClientCard.Padding = shortWindow ? 12 : compact ? 16 : 20;
        ItemsCard.Padding = shortWindow ? 12 : compact ? 16 : 20;
        ItemSection.RowSpacing = shortWindow ? 12 : 20;
        ItemComposer.RowSpacing = shortWindow ? 8 : 12;
        CustomTextLabel.Margin = new Thickness(0, shortWindow ? 8 : 20, 0, 8);
        ClientFields.RowDefinitions[1].Height = shortWindow ? 38 : compact ? 42 : 48;
        ClientFields.RowDefinitions[3].Height = shortWindow ? 56 : compact ? 72 : 104;

        if (_usingSidebar == sidebar)
            return;
        _usingSidebar = sidebar;

        Workspace.ColumnDefinitions.Clear();
        Workspace.RowDefinitions.Clear();
        ActionsLayout.ColumnDefinitions.Clear();
        ActionsLayout.RowDefinitions.Clear();
        if (sidebar)
        {
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(266) });
            Workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            Grid.SetRow(FormArea, 0);
            Grid.SetColumn(ActionsCard, 1);
            Grid.SetRow(ActionsCard, 0);
            ActionsCard.Padding = 20;
            ActionsLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
            ActionsLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            ActionsLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            ActionsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            Place(GenerateButton, 0, 0);
            Place(ResetButton, 1, 0);
            Place(ExitButton, 2, 0);
        }
        else
        {
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            Workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            Grid.SetRow(FormArea, 1);
            Grid.SetColumn(ActionsCard, 0);
            Grid.SetRow(ActionsCard, 0);
            ActionsCard.Padding = 10;
            ActionsLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            ActionsLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            ActionsLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            ActionsLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            Place(GenerateButton, 0, 0);
            Place(ResetButton, 0, 1);
            Place(ExitButton, 0, 2);
        }
    }

    private void OnFormAreaSizeChanged(object? sender, EventArgs e)
    {
        var compact = FormArea.Width < 950;
        if (_usingCompactComposer == compact || FormArea.Width <= 0)
            return;
        _usingCompactComposer = compact;
        ItemComposer.ColumnDefinitions.Clear();
        ItemComposer.RowDefinitions.Clear();
        ItemComposer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (compact)
        {
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            ItemComposer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Place(CategoryField, 0, 0);
            Place(ItemField, 0, 1);
            Place(QuantityField, 1, 0);
            Place(AddButton, 1, 1);
        }
        else
        {
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(152) });
            ItemComposer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) });
            Place(CategoryField, 0, 0);
            Place(ItemField, 0, 1);
            Place(QuantityField, 0, 2);
            Place(AddButton, 0, 3);
        }
    }

    private static void Place(BindableObject view, int row, int column)
    {
        Grid.SetRow(view, row);
        Grid.SetColumn(view, column);
    }

    private void OnTableFrameSizeChanged(object? sender, EventArgs e)
    {
        // Defer until the current native layout pass has completed so a resize
        // invalidation is not lost while the parent is arranging its children.
        Dispatcher.Dispatch(UpdateTableViewport);
    }

    private void UpdateTableViewport()
    {
        // Size from the bounded parent, rather than the scroll content's desired size.
        var viewportWidth = TableFrame.Width - 2;
        var viewportHeight = TableFrame.Height - 2;
        if (!double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight)
            || viewportWidth <= 0 || viewportHeight <= 0)
            return;

        TableContent.WidthRequest = Math.Max(MinimumTableWidth, viewportWidth);
        TableContent.HeightRequest = viewportHeight;
        ((IView)TableFrame).InvalidateMeasure();
        ColumnNavigation.IsVisible = viewportWidth < MinimumTableWidth;

    }

    private async void OnFirstColumnsClicked(object? sender, EventArgs e) =>
        await TableViewport.ScrollToAsync(0, 0, true);

    private async void OnLastColumnsClicked(object? sender, EventArgs e)
    {
#if MACCATALYST || IOS
        if (TableViewport.Handler?.PlatformView is UIKit.UIScrollView nativeScroll)
        {
            // UIKit's bounds are the viewport size even when Catalyst scales
            // the native frame. MAUI's Frame-based clamp can stop too early.
            nativeScroll.LayoutIfNeeded();
            var end = Math.Max(0, nativeScroll.ContentSize.Width - nativeScroll.Bounds.Width);
            nativeScroll.SetContentOffset(new CoreGraphics.CGPoint(end, 0), true);
            return;
        }
#endif
        await TableViewport.ScrollToAsync(Math.Max(0, TableViewport.ContentSize.Width - TableViewport.Width), 0, true);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is OfferLineItem item)
            Dispatcher.Dispatch(() => ItemsTable.ScrollTo(item, position: ScrollToPosition.End, animate: false));
    }

    private void OnDeleteItemClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: OfferLineItem item })
            _viewModel.DeleteCommand.Execute(item);
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
    {
        if (_openingDialog)
            return;
        _openingDialog = true;
        OfferPdfPreviewFile? previewFile = null;
        string? savedPdfPath = null;
        try
        {
            if (!_viewModel.TryValidateOffer(out var message))
            {
                await DisplayAlertAsync("Check your offer", message, "OK");
                return;
            }

            if (!_pdfService.IsSupported)
            {
                await DisplayAlertAsync("PDF preview unavailable",
                    "PDF preview is currently available in the Windows app. It is not yet available on this platform.", "OK");
                return;
            }

#if WINDOWS
            try
            {
                _ = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            // WinUI reports a missing runtime as HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND).
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80070002))
            {
                await DisplayAlertAsync("PDF viewer required",
                    "Install the Microsoft Edge WebView2 Runtime, then generate your offer again to preview the PDF.", "OK");
                return;
            }
#endif

            // Snapshot on the UI thread so later edits cannot change the PDF being generated.
            var offer = new OfferPdfData(_viewModel.ClientName, _viewModel.CustomText, _viewModel.Items, _settings);
            GenerateButton.IsEnabled = false;
            GenerateButton.Text = "Generating…";
            _viewModel.Status = "Generating offer PDF…";
            previewFile = await OfferPdfPreviewFile.CreateAsync(_pdfService, offer, FileSystem.Current.CacheDirectory);
            savedPdfPath = await previewFile.SaveCopyAsync(_settings.SaveDirectory);
            await Navigation.PushModalAsync(new OfferPreviewPage(previewFile));
            previewFile = null; // The preview page now owns the temporary PDF.
            _viewModel.Status = $"Offer PDF saved to {savedPdfPath}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Offer PDF preview failed: {ex}");
            _viewModel.Status = savedPdfPath is null
                ? "Could not generate or save the offer PDF. Check the logo and save directory in Settings."
                : $"Offer PDF saved to {savedPdfPath}";
            await DisplayAlertAsync(savedPdfPath is null ? "Could not save PDF" : "Could not open PDF preview",
                savedPdfPath is null
                    ? "The offer PDF could not be generated or saved. Check that the logo image is available and the save directory is writable. Your offer is still available to edit."
                    : $"Your PDF was saved to {savedPdfPath}, but the preview could not be opened.", "OK");
        }
        finally
        {
            previewFile?.Dispose();
            GenerateButton.IsEnabled = true;
            GenerateButton.Text = "Generate Offer";
            _openingDialog = false;
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (_openingDialog)
            return;
        _openingDialog = true;
        try
        {
            var issuerEntry = CreateSettingsEntry(_settings.IssuerName, "Issuer name", "IssuerName");
            var emailEntry = CreateSettingsEntry(_settings.Email, "name@example.com", "Email");
            emailEntry.Keyboard = Keyboard.Email;
            SemanticProperties.SetDescription(emailEntry, "E-mail");
            var emailBorder = CreateSettingsInputBorder(emailEntry);
            var emailError = new Label
            {
                Text = ContactDataValue.EmailValidationMessage,
                TextColor = Color.FromArgb("B42318"),
                IsVisible = false,
                AutomationId = "EmailError"
            };
            bool ValidateEmail()
            {
                var isValid = ContactDataValue.IsValidEmail(emailEntry.Text);
                emailError.IsVisible = !isValid;
                emailBorder.Stroke = Color.FromArgb(isValid ? "B8C8DC" : "B42318");
                SemanticProperties.SetHint(emailEntry, isValid ? "E-mail address" : ContactDataValue.EmailValidationMessage);
                return isValid;
            }
            emailEntry.Unfocused += (_, _) => ValidateEmail();
            emailEntry.TextChanged += (_, _) =>
            {
                if (emailError.IsVisible)
                    ValidateEmail();
            };
            var phoneEntry = CreateSettingsEntry(_settings.PhoneNumber, "Phone number", "PhoneNumber");
            phoneEntry.Keyboard = Keyboard.Numeric;
            phoneEntry.TextChanged += OnDigitsOnlyTextChanged;
            var addressLine1Entry = CreateSettingsEntry(_settings.AddressLine1, "Street and number", "AddressLine1");
            SemanticProperties.SetDescription(addressLine1Entry, "Address line 1");
            var addressLine2Entry = CreateSettingsEntry(_settings.AddressLine2, "City, postal code, country", "AddressLine2");
            SemanticProperties.SetDescription(addressLine2Entry, "Address line 2");
            var vatNumberEntry = CreateSettingsEntry(_settings.VatNumber, "VAT number", "VatNumber");
            vatNumberEntry.Keyboard = Keyboard.Numeric;
            vatNumberEntry.TextChanged += OnDigitsOnlyTextChanged;
            var languagePicker = new DropdownPicker
            {
                Title = "Language",
                ItemsSource = new[] { AppSettings.Romanian, AppSettings.English },
                SelectedItem = _settings.Language,
                BackgroundColor = Colors.White,
                HeightRequest = 44,
                AutomationId = "Language"
            };
            SemanticProperties.SetDescription(languagePicker, "Language");
            var editor = new Editor
            {
                Text = _viewModel.DefaultMessage,
                HeightRequest = 180,
                BackgroundColor = Colors.Transparent
            };
            SemanticProperties.SetDescription(editor, "Default offer message");
            var vatEntry = new Entry
            {
                Text = VatRateValue.Format(_viewModel.VatRate),
                Keyboard = Keyboard.Numeric,
                BackgroundColor = Colors.Transparent,
                MinimumHeightRequest = 44,
                AutomationId = "VatPercentage"
            };
            SemanticProperties.SetDescription(vatEntry, "VAT percentage");
            var vatError = new Label
            {
                Text = VatRateValue.ValidationMessage,
                TextColor = Color.FromArgb("B42318"),
                IsVisible = false
            };
            vatEntry.TextChanged += (_, _) => vatError.IsVisible = false;
            var save = new Button { Text = "Save settings", BackgroundColor = Color.FromArgb("147EF0"), TextColor = Colors.White };
            var cancel = new Button { Text = "Cancel" };
            var page = new ContentPage { Title = "Settings" };
            var selectingPath = false;
            page.Disappearing += (_, _) =>
            {
                if (!selectingPath)
                    SettingsPathPicker.DiscardUnsavedAccess();
            };
            var logoPathEntry = CreateSettingsEntry(_settings.LogoPath ?? string.Empty, "No logo selected", "LogoPath");
            logoPathEntry.IsReadOnly = true;
            SemanticProperties.SetDescription(logoPathEntry, "Logo image path");
            var saveDirectoryEntry = CreateSettingsEntry(_settings.SaveDirectory, "Save directory", "SaveDirectory");
            saveDirectoryEntry.IsReadOnly = true;
            var browseLogo = new Button { Text = "Browse…", ImageSource = "folder.png", AutomationId = "BrowseLogo", Padding = new Thickness(12, 8) };
            var browseDirectory = new Button { Text = "Browse…", ImageSource = "folder.png", AutomationId = "BrowseSaveDirectory", Padding = new Thickness(12, 8) };
            SemanticProperties.SetDescription(browseLogo, "Browse for a logo image");
            SemanticProperties.SetDescription(browseDirectory, "Browse for a PDF save folder");
            var removeLogo = new Button
            {
                Text = "Remove logo", AutomationId = "RemoveLogo", HorizontalOptions = LayoutOptions.Start,
                IsEnabled = !string.IsNullOrWhiteSpace(logoPathEntry.Text)
            };
            removeLogo.Clicked += (_, _) =>
            {
                logoPathEntry.Text = string.Empty;
                removeLogo.IsEnabled = false;
            };
            async Task BrowsePathAsync(Func<Task<string?>> pick, Entry entry)
            {
                selectingPath = true;
                save.IsEnabled = cancel.IsEnabled = browseLogo.IsEnabled = browseDirectory.IsEnabled = removeLogo.IsEnabled = false;
                try
                {
                    var path = await pick();
                    if (path is not null)
                        entry.Text = path;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Settings path picker failed: {ex}");
                    await page.DisplayAlertAsync("Could not select path", "The file browser could not complete your selection. Please try again.", "OK");
                }
                finally
                {
                    selectingPath = false;
                    save.IsEnabled = cancel.IsEnabled = browseLogo.IsEnabled = browseDirectory.IsEnabled = true;
                    removeLogo.IsEnabled = !string.IsNullOrWhiteSpace(logoPathEntry.Text);
                }
            }
            browseLogo.Clicked += async (_, _) => await BrowsePathAsync(SettingsPathPicker.PickLogoAsync, logoPathEntry);
            browseDirectory.Clicked += async (_, _) => await BrowsePathAsync(SettingsPathPicker.PickSaveDirectoryAsync, saveDirectoryEntry);
            var buttons = new HorizontalStackLayout { Spacing = 12, Children = { save, cancel } };
            page.Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 32, Spacing = 12, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new Label { Text = "Settings", FontSize = 28, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Issuer name", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(issuerEntry),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "Logo", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Optional image displayed on generated offers.", TextColor = Color.FromArgb("50627C") },
                        CreateSettingsPathRow(logoPathEntry, browseLogo),
                        removeLogo,
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "Save directory", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Generated PDFs are saved in this folder. The folder is created when needed.", TextColor = Color.FromArgb("50627C") },
                        CreateSettingsPathRow(saveDirectoryEntry, browseDirectory),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "Contact data", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "E-mail", FontAttributes = FontAttributes.Bold },
                        emailBorder,
                        emailError,
                        new Label { Text = "Phone number", FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(phoneEntry),
                        new Label { Text = "Address line 1", FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(addressLine1Entry),
                        new Label { Text = "Address line 2", FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(addressLine2Entry),
                        new Label { Text = "VAT number", FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(vatNumberEntry),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "Language", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(languagePicker),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "Default offer message", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Use this message for the current offer and whenever you reset the form.", TextColor = Color.FromArgb("50627C") },
                        CreateSettingsInputBorder(editor),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = "VAT percentage (%)", FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Applied to all items in the current offer and future offers. Unit prices exclude VAT; totals include VAT. Use 0 for no VAT.", TextColor = Color.FromArgb("50627C") },
                        CreateSettingsInputBorder(vatEntry),
                        vatError, buttons
                    }
                }
            };
            save.Clicked += async (_, _) =>
            {
                if (!ValidateEmail())
                {
                    await ((ScrollView)page.Content).ScrollToAsync(0, 0, true);
                    emailEntry.Focus();
                    return;
                }

                if (!VatRateValue.TryParse(vatEntry.Text, out var vatRate))
                {
                    vatError.IsVisible = true;
                    vatEntry.Focus();
                    return;
                }

                var settings = new AppSettings
                {
                    IssuerName = issuerEntry.Text ?? string.Empty,
                    LogoPath = logoPathEntry.Text,
                    SaveDirectory = saveDirectoryEntry.Text ?? AppSettings.DefaultSaveDirectory,
                    Email = emailEntry.Text?.Trim() ?? string.Empty,
                    PhoneNumber = ContactDataValue.DigitsOnly(phoneEntry.Text),
                    AddressLine1 = addressLine1Entry.Text ?? string.Empty,
                    AddressLine2 = addressLine2Entry.Text ?? string.Empty,
                    VatNumber = ContactDataValue.DigitsOnly(vatNumberEntry.Text),
                    Language = languagePicker.SelectedItem as string ?? AppSettings.Romanian,
                    VatRate = vatRate,
                    DefaultMessage = editor.Text ?? string.Empty
                };
                try
                {
                    _settingsStore.Save(settings);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await page.DisplayAlertAsync("Settings not saved", "Could not write settings.json. Check available storage and try again.", "OK");
                    return;
                }

                _settings = settings;
                _viewModel.VatRate = vatRate;
                _viewModel.DefaultMessage = settings.DefaultMessage;
                _viewModel.CustomText = _viewModel.DefaultMessage;
                try
                {
                    SettingsPathPicker.CommitSavedAccess(settings.LogoPath, settings.SaveDirectory);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not persist selected file access: {ex}");
                    _viewModel.Status = "Settings saved, but access to the selected paths could not be remembered.";
                    await page.DisplayAlertAsync("Could not remember file access",
                        "Your settings were saved, but access to the selected image or folder could not be remembered. Try saving again, or reselect the paths with Browse.", "OK");
                    return;
                }
                _viewModel.Status = "Settings saved.";
                await Navigation.PopModalAsync();
            };
            cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
            await Navigation.PushModalAsync(page);
        }
        finally
        {
            _openingDialog = false;
        }
    }

    private static Entry CreateSettingsEntry(string text, string placeholder, string automationId)
    {
        var entry = new Entry
        {
            Text = text,
            Placeholder = placeholder,
            AutomationId = automationId,
            BackgroundColor = Colors.Transparent,
            MinimumHeightRequest = 44,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false
        };
        SemanticProperties.SetDescription(entry, placeholder);
        return entry;
    }

    private static Border CreateSettingsInputBorder(View input) => new()
    {
        Stroke = Color.FromArgb("B8C8DC"),
        StrokeThickness = 1,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
        BackgroundColor = Color.FromArgb("EFF4FA"),
        Padding = new Thickness(12, 6),
        Content = input
    };

    private static Grid CreateSettingsPathRow(Entry path, Button browse)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            ],
            ColumnSpacing = 12
        };
        row.Add(CreateSettingsInputBorder(path));
        row.Add(browse, 1);
        return row;
    }

    private static void OnDigitsOnlyTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry)
            return;
        var text = e.NewTextValue ?? string.Empty;
        var digits = ContactDataValue.DigitsOnly(text);
        if (text == digits)
            return;

        var cursor = Math.Clamp(entry.CursorPosition, 0, text.Length);
        var filteredCursor = ContactDataValue.DigitsOnly(text[..cursor]).Length;
        entry.Text = digits;
        entry.CursorPosition = filteredCursor;
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (Window is { } window)
            Application.Current?.CloseWindow(window);
    }
}
