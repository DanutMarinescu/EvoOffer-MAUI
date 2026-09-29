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
    private const double MinimumTableWidth = 1620;
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
        var savedVatRate = Preferences.Default.Get(VatRatePreference, VatRateValue.Format(VatRateValue.Default));
        var initialSettings = new AppSettings
        {
            VatRate = VatRateValue.TryParse(savedVatRate, out var vatRate) ? vatRate : VatRateValue.Default,
            DefaultMessage = Preferences.Default.Get(DefaultMessagePreference, MainViewModel.DefaultCustomText)
        };
        var settingsLoadFailed = false;
        try
        {
            _settings = _settingsStore.Load(initialSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _settings = initialSettings;
            _settings.Normalize();
            settingsLoadFailed = true;
        }
        LocalizationService.SetLanguage(_settings.Language);
        InitializeComponent();
        SettingsPathPicker.RestoreSavedAccess();
        _viewModel = new MainViewModel(_settings.VatRate, ProductCatalog.Empty);
        _viewModel.DefaultMessage = _settings.DefaultMessage;
        _viewModel.CustomText = _viewModel.DefaultMessage;
        if (settingsLoadFailed)
            _viewModel.SetStatus("SettingsLoadFailed");
        BindingContext = _viewModel;
        SizeChanged += OnPageSizeChanged;
        FormArea.SizeChanged += OnFormAreaSizeChanged;
        _viewModel.Items.CollectionChanged += OnItemsChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        // Each new main page reloads the saved file once, after its controls are ready.
        // Returning from Settings must not reload the catalog or reset selections.
        Loaded -= OnLoaded;
        if (string.IsNullOrWhiteSpace(_settings.DataFilePath))
        {
            await _viewModel.ReloadCatalogAsync(_settings.DataFilePath);
            return;
        }

        SettingsButton.IsEnabled = false;
        var previousStatus = _viewModel.Status;
        _viewModel.SetStatus("StatusLoadingCatalog");
        try
        {
            await _viewModel.ReloadCatalogAsync(_settings.DataFilePath);
            if (previousStatus == LocalizationService.Get("StatusReady"))
                _viewModel.SetStatus("StatusCatalogLoaded", _viewModel.Catalog.Items.Count);
            else
                _viewModel.Status = previousStatus;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Saved catalog could not be loaded: {ex}");
            _viewModel.SetStatus("StatusSavedCatalogUnavailable");
            await DisplayAlertAsync(LocalizationService.Get("CatalogUnavailable"), ex is CatalogImportException
                ? ex.Message
                : LocalizationService.Get("SavedCatalogReadFailed"), LocalizationService.Get("Ok"));
        }
        finally
        {
            SettingsButton.IsEnabled = true;
        }
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
                await DisplayAlertAsync(LocalizationService.Get("CheckOffer"), message, LocalizationService.Get("Ok"));
                return;
            }

            if (!_pdfService.IsSupported)
            {
                await DisplayAlertAsync(LocalizationService.Get("PdfPreviewUnavailable"),
                    LocalizationService.Get("PdfPreviewPlatformUnavailable"), LocalizationService.Get("Ok"));
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
                await DisplayAlertAsync(LocalizationService.Get("PdfViewerRequired"),
                    LocalizationService.Get("PdfViewerInstallHelp"), LocalizationService.Get("Ok"));
                return;
            }
#endif

            // Snapshot on the UI thread so later edits cannot change the PDF being generated.
            var offer = new OfferPdfData(_viewModel.ClientName, _viewModel.CustomText, _viewModel.Items, _settings);
            GenerateButton.IsEnabled = false;
            GenerateButton.Text = LocalizationService.Get("Generating");
            _viewModel.SetStatus("StatusGeneratingPdf");
            previewFile = await OfferPdfPreviewFile.CreateAsync(_pdfService, offer, FileSystem.Current.CacheDirectory);
            savedPdfPath = await previewFile.SaveCopyAsync(_settings.SaveDirectory);
            await Navigation.PushModalAsync(new OfferPreviewPage(previewFile));
            previewFile = null; // The preview page now owns the temporary PDF.
            _viewModel.SetStatus("StatusPdfSaved", savedPdfPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Offer PDF preview failed: {ex}");
            if (savedPdfPath is null)
                _viewModel.SetStatus("StatusPdfSaveFailed");
            else
                _viewModel.SetStatus("StatusPdfSaved", savedPdfPath);
            await DisplayAlertAsync(savedPdfPath is null ? LocalizationService.Get("PdfSaveFailed") : LocalizationService.Get("PdfPreviewOpenFailed"),
                savedPdfPath is null
                    ? LocalizationService.Get("PdfSaveFailedHelp")
                    : LocalizationService.Format("PdfSavedPreviewFailedHelp", savedPdfPath), LocalizationService.Get("Ok"));
        }
        finally
        {
            previewFile?.Dispose();
            GenerateButton.IsEnabled = true;
            GenerateButton.SetDynamicResource(Button.TextProperty, "GenerateOffer");
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
            var issuerEntry = CreateSettingsEntry(_settings.IssuerName, LocalizationService.Get("IssuerName"), "IssuerName");
            var emailEntry = CreateSettingsEntry(_settings.Email, LocalizationService.Get("EmailPlaceholder"), "Email");
            emailEntry.Keyboard = Keyboard.Email;
            SemanticProperties.SetDescription(emailEntry, LocalizationService.Get("Email"));
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
                SemanticProperties.SetHint(emailEntry, isValid ? LocalizationService.Get("EmailAddress") : ContactDataValue.EmailValidationMessage);
                return isValid;
            }
            emailEntry.Unfocused += (_, _) => ValidateEmail();
            emailEntry.TextChanged += (_, _) =>
            {
                if (emailError.IsVisible)
                    ValidateEmail();
            };
            var phoneEntry = CreateSettingsEntry(_settings.PhoneNumber, LocalizationService.Get("PhoneNumber"), "PhoneNumber");
            phoneEntry.Keyboard = Keyboard.Numeric;
            phoneEntry.TextChanged += OnDigitsOnlyTextChanged;
            var addressLine1Entry = CreateSettingsEntry(_settings.AddressLine1, LocalizationService.Get("StreetAndNumber"), "AddressLine1");
            SemanticProperties.SetDescription(addressLine1Entry, LocalizationService.Get("AddressLine1"));
            var addressLine2Entry = CreateSettingsEntry(_settings.AddressLine2, LocalizationService.Get("CityPostalCodeCountry"), "AddressLine2");
            SemanticProperties.SetDescription(addressLine2Entry, LocalizationService.Get("AddressLine2"));
            var vatNumberEntry = CreateSettingsEntry(_settings.VatNumber, LocalizationService.Get("VatNumber"), "VatNumber");
            vatNumberEntry.Keyboard = Keyboard.Numeric;
            vatNumberEntry.TextChanged += OnDigitsOnlyTextChanged;
            var languagePicker = new DropdownPicker
            {
                Title = LocalizationService.Get("Language"),
                ItemsSource = new[] { AppSettings.Romanian, AppSettings.English },
                SelectedItem = _settings.Language,
                BackgroundColor = Colors.White,
                HeightRequest = 44,
                AutomationId = "Language"
            };
            SemanticProperties.SetDescription(languagePicker, LocalizationService.Get("Language"));
            var editor = new Editor
            {
                Text = _viewModel.DefaultMessage,
                HeightRequest = 180,
                BackgroundColor = Colors.Transparent
            };
            SemanticProperties.SetDescription(editor, LocalizationService.Get("DefaultOfferMessageLabel"));
            var vatEntry = new Entry
            {
                Text = VatRateValue.Format(_viewModel.VatRate),
                Keyboard = Keyboard.Numeric,
                BackgroundColor = Colors.Transparent,
                MinimumHeightRequest = 44,
                AutomationId = "VatPercentage"
            };
            SemanticProperties.SetDescription(vatEntry, LocalizationService.Get("VatPercentage"));
            var vatError = new Label
            {
                Text = VatRateValue.ValidationMessage,
                TextColor = Color.FromArgb("B42318"),
                IsVisible = false
            };
            vatEntry.TextChanged += (_, _) => vatError.IsVisible = false;
            var save = new Button { Text = LocalizationService.Get("SaveSettings"), BackgroundColor = Color.FromArgb("147EF0"), TextColor = Colors.White };
            var cancel = new Button { Text = LocalizationService.Get("Cancel") };
            var page = new ContentPage { Title = LocalizationService.Get("Settings") };
            var selectingPath = false;
            var savingSettings = false;
            var previewingTemplate = false;
            var dataFileSelected = false;
            page.Disappearing += (_, _) =>
            {
                if (!selectingPath && !savingSettings && !previewingTemplate)
                    SettingsPathPicker.DiscardUnsavedAccess();
            };
            var logoPathEntry = CreateSettingsEntry(_settings.LogoPath ?? string.Empty, LocalizationService.Get("NoLogoSelected"), "LogoPath");
            logoPathEntry.IsReadOnly = true;
            SemanticProperties.SetDescription(logoPathEntry, LocalizationService.Get("LogoImagePath"));
            var saveDirectoryEntry = CreateSettingsEntry(_settings.SaveDirectory, LocalizationService.Get("SaveDirectory"), "SaveDirectory");
            saveDirectoryEntry.IsReadOnly = true;
            var dataFilePathEntry = CreateSettingsEntry(_settings.DataFilePath ?? string.Empty, LocalizationService.Get("NoCsvSelected"), "DataFilePath");
            dataFilePathEntry.IsReadOnly = true;
            SemanticProperties.SetDescription(dataFilePathEntry, LocalizationService.Get("DataFilePath"));
            var browseLogo = new Button { Text = LocalizationService.Get("Browse"), ImageSource = "folder.png", AutomationId = "BrowseLogo", Padding = new Thickness(12, 8) };
            var browseDirectory = new Button { Text = LocalizationService.Get("Browse"), ImageSource = "folder.png", AutomationId = "BrowseSaveDirectory", Padding = new Thickness(12, 8) };
            var browseDataFile = new Button { Text = LocalizationService.Get("Browse"), ImageSource = "folder.png", AutomationId = "BrowseDataFile", Padding = new Thickness(12, 8) };
            SemanticProperties.SetDescription(browseLogo, LocalizationService.Get("BrowseLogo"));
            SemanticProperties.SetDescription(browseDirectory, LocalizationService.Get("BrowseSaveDirectory"));
            SemanticProperties.SetDescription(browseDataFile, LocalizationService.Get("BrowseDataFile"));
            var removeLogo = new Button
            {
                Text = LocalizationService.Get("RemoveLogo"), AutomationId = "RemoveLogo", HorizontalOptions = LayoutOptions.Start,
                IsEnabled = !string.IsNullOrWhiteSpace(logoPathEntry.Text)
            };
            removeLogo.Clicked += (_, _) =>
            {
                logoPathEntry.Text = string.Empty;
                removeLogo.IsEnabled = false;
            };
            var clearDataFile = new Button
            {
                Text = LocalizationService.Get("ClearSelection"), AutomationId = "ClearDataFile", HorizontalOptions = LayoutOptions.Start,
                IsEnabled = !string.IsNullOrWhiteSpace(dataFilePathEntry.Text)
            };
            clearDataFile.Clicked += (_, _) =>
            {
                dataFilePathEntry.Text = string.Empty;
                clearDataFile.IsEnabled = false;
                dataFileSelected = true;
            };
            async Task BrowsePathAsync(Func<Task<string?>> pick, Entry entry)
            {
                selectingPath = true;
                save.IsEnabled = cancel.IsEnabled = browseLogo.IsEnabled = browseDirectory.IsEnabled =
                    browseDataFile.IsEnabled = removeLogo.IsEnabled = clearDataFile.IsEnabled = false;
                try
                {
                    var path = await pick();
                    if (path is not null)
                    {
                        entry.Text = path;
                        if (entry == dataFilePathEntry)
                            dataFileSelected = true;
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) when (entry == dataFilePathEntry)
                {
                    await page.DisplayAlertAsync(LocalizationService.Get("DataFileSelectionFailed"), LocalizationService.Get("ChooseCsvFile"), LocalizationService.Get("Ok"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Settings path picker failed: {ex}");
                    await page.DisplayAlertAsync(LocalizationService.Get("PathSelectionFailed"), LocalizationService.Get("PathSelectionFailedHelp"), LocalizationService.Get("Ok"));
                }
                finally
                {
                    selectingPath = false;
                    save.IsEnabled = cancel.IsEnabled = browseLogo.IsEnabled = browseDirectory.IsEnabled = browseDataFile.IsEnabled = true;
                    removeLogo.IsEnabled = !string.IsNullOrWhiteSpace(logoPathEntry.Text);
                    clearDataFile.IsEnabled = !string.IsNullOrWhiteSpace(dataFilePathEntry.Text);
                }
            }
            browseLogo.Clicked += async (_, _) => await BrowsePathAsync(SettingsPathPicker.PickLogoAsync, logoPathEntry);
            browseDirectory.Clicked += async (_, _) => await BrowsePathAsync(SettingsPathPicker.PickSaveDirectoryAsync, saveDirectoryEntry);
            browseDataFile.Clicked += async (_, _) => await BrowsePathAsync(SettingsPathPicker.PickDataFileAsync, dataFilePathEntry);
            var primaryColorPicker = new SettingsColorPicker(LocalizationService.Get("PrimaryColor"), _settings.PdfPrimaryColor, "PdfPrimaryColor");
            var secondaryColorPicker = new SettingsColorPicker(LocalizationService.Get("SecondaryColor"), _settings.PdfSecondaryColor, "PdfSecondaryColor");
            var textColorPicker = new SettingsColorPicker(LocalizationService.Get("TextColor"), _settings.PdfTextColor, "PdfTextColor");
            var colorPickers = new[] { primaryColorPicker, secondaryColorPicker, textColorPicker };
            var templateColors = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = LocalizationService.Get("TemplateColors"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = LocalizationService.Get("TemplateColorsHelp"),
                        TextColor = Color.FromArgb("50627C")
                    },
                    primaryColorPicker, secondaryColorPicker, textColorPicker
                }
            };
            var templateSelector = new PdfTemplateSelector(_settings.PdfTemplateId, _pdfService.IsSupported, templateColors);
            async Task<bool> ValidateTemplateColorsAsync()
            {
                SettingsColorPicker? firstInvalid = null;
                foreach (var picker in colorPickers)
                    if (!picker.Validate())
                        firstInvalid ??= picker;
                if (firstInvalid is null)
                    return true;
                await ((ScrollView)page.Content).ScrollToAsync(firstInvalid, ScrollToPosition.Center, true);
                firstInvalid.FocusHexEntry();
                return false;
            }
            AppSettings ReadDraftSettings(decimal vatRate) => new()
            {
                IssuerName = issuerEntry.Text ?? string.Empty,
                LogoPath = logoPathEntry.Text,
                SaveDirectory = saveDirectoryEntry.Text ?? AppSettings.DefaultSaveDirectory,
                DataFilePath = dataFilePathEntry.Text,
                Email = emailEntry.Text?.Trim() ?? string.Empty,
                PhoneNumber = ContactDataValue.DigitsOnly(phoneEntry.Text),
                AddressLine1 = addressLine1Entry.Text ?? string.Empty,
                AddressLine2 = addressLine2Entry.Text ?? string.Empty,
                VatNumber = ContactDataValue.DigitsOnly(vatNumberEntry.Text),
                Language = languagePicker.SelectedItem as string ?? AppSettings.Romanian,
                PdfTemplateId = templateSelector.SelectedTemplateId,
                PdfPrimaryColor = primaryColorPicker.SelectedColorHex,
                PdfSecondaryColor = secondaryColorPicker.SelectedColorHex,
                PdfTextColor = textColorPicker.SelectedColorHex,
                VatRate = vatRate,
                DefaultMessage = editor.Text ?? string.Empty
            };
            templateSelector.PreviewRequested += async (_, _) =>
            {
                if (selectingPath || savingSettings || previewingTemplate)
                    return;
                if (!await ValidateTemplateColorsAsync())
                    return;
                if (!VatRateValue.TryParse(vatEntry.Text, out var previewVatRate))
                {
                    vatError.IsVisible = true;
                    await page.DisplayAlertAsync(LocalizationService.Get("CheckVatPercentage"), VatRateValue.ValidationMessage, LocalizationService.Get("Ok"));
                    vatEntry.Focus();
                    return;
                }

                OfferPdfPreviewFile? previewFile = null;
                var previewOpened = false;
                previewingTemplate = true;
                templateSelector.SetPreviewBusy(true);
                page.Content.IsEnabled = false;
                try
                {
                    var previewSettings = ReadDraftSettings(previewVatRate);
                    previewSettings.Normalize();
                    var sample = OfferPdfTemplatePreview.Create(previewSettings);
                    previewFile = await OfferPdfPreviewFile.CreateAsync(_pdfService, sample, FileSystem.Current.CacheDirectory);
                    var previewPage = new OfferPreviewPage(previewFile, isTemplatePreview: true);
                    previewPage.Disappearing += (_, _) => previewingTemplate = false;
                    await page.Navigation.PushModalAsync(previewPage);
                    previewOpened = true;
                    previewFile = null; // The preview page owns this temporary sample; no permanent copy is saved.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Template preview failed: {ex}");
                    await page.DisplayAlertAsync(LocalizationService.Get("TemplatePreviewFailed"),
                        LocalizationService.Get("TemplatePreviewFailedHelp"), LocalizationService.Get("Ok"));
                }
                finally
                {
                    previewFile?.Dispose();
                    if (!previewOpened)
                        previewingTemplate = false;
                    templateSelector.SetPreviewBusy(false);
                    page.Content.IsEnabled = true;
                }
            };
            var buttons = new HorizontalStackLayout { Spacing = 12, Children = { save, cancel } };
            page.Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 32, Spacing = 12, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new Label { Text = LocalizationService.Get("Settings"), FontSize = 28, FontAttributes = FontAttributes.Bold },
                        templateSelector,
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("IssuerName"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(issuerEntry),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("Logo"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("LogoHelp"), TextColor = Color.FromArgb("50627C") },
                        CreateSettingsPathRow(logoPathEntry, browseLogo),
                        removeLogo,
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("SaveDirectory"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("SaveDirectoryHelp"), TextColor = Color.FromArgb("50627C") },
                        CreateSettingsPathRow(saveDirectoryEntry, browseDirectory),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("DataFile"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("DataFileRequiredHeadersHelp"), TextColor = Color.FromArgb("50627C") },
                        new Label { Text = LocalizationService.Get("DataFileVariantHeadersHelp"), TextColor = Color.FromArgb("50627C") },
                        new Label { Text = LocalizationService.Get("DataFileDimensionsHelp"), TextColor = Color.FromArgb("50627C") },
                        new Label { Text = LocalizationService.Get("DataFileCategoriesHelp"), TextColor = Color.FromArgb("50627C") },
                        CreateSettingsPathRow(dataFilePathEntry, browseDataFile),
                        clearDataFile,
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("ContactData"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("Email"), FontAttributes = FontAttributes.Bold },
                        emailBorder,
                        emailError,
                        new Label { Text = LocalizationService.Get("PhoneNumber"), FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(phoneEntry),
                        new Label { Text = LocalizationService.Get("AddressLine1"), FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(addressLine1Entry),
                        new Label { Text = LocalizationService.Get("AddressLine2"), FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(addressLine2Entry),
                        new Label { Text = LocalizationService.Get("VatNumber"), FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(vatNumberEntry),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("Language"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        CreateSettingsInputBorder(languagePicker),
                        new Label { Text = LocalizationService.Get("LanguageHelp"), TextColor = Color.FromArgb("50627C") },
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("DefaultOfferMessageLabel"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("DefaultOfferMessageHelp"), TextColor = Color.FromArgb("50627C") },
                        CreateSettingsInputBorder(editor),
                        new BoxView { HeightRequest = 1, Color = Color.FromArgb("D2DCE8"), HorizontalOptions = LayoutOptions.Fill },
                        new Label { Text = LocalizationService.Get("VatPercentageLabel"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                        new Label { Text = LocalizationService.Get("VatPercentageHelp"), TextColor = Color.FromArgb("50627C") },
                        CreateSettingsInputBorder(vatEntry),
                        vatError, buttons
                    }
                }
            };
            save.Clicked += async (_, _) =>
            {
                if (savingSettings || selectingPath)
                    return;
                if (!await ValidateTemplateColorsAsync())
                    return;
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

                var defaultMessageEdited = editor.Text != _viewModel.DefaultMessage;
                var settings = ReadDraftSettings(vatRate);
                settings.Normalize();
                savingSettings = true;
                page.Content.IsEnabled = false;
                try
                {
                    ProductCatalog? importedCatalog = null;
                    var importRequested = settings.DataFilePath is not null && (dataFileSelected
                        || settings.DataFilePath != _settings.DataFilePath || _viewModel.Catalog.Items.Count == 0);
                    if (importRequested)
                    {
                        save.Text = LocalizationService.Get("Importing");
                        try
                        {
                            importedCatalog = await Task.Run(() => CatalogCsvImporter.ImportFile(settings.DataFilePath!, vatRate));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"CSV import failed: {ex}");
                            await page.DisplayAlertAsync(LocalizationService.Get("CsvImportFailed"), (ex is CatalogImportException
                                ? ex.Message
                                : LocalizationService.Get("CsvReadFailedHelp"))
                                + LocalizationService.Get("PreviousSettingsUnchanged"), LocalizationService.Get("Ok"));
                            return;
                        }
                    }

                    try
                    {
                        _settingsStore.Save(settings);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        await page.DisplayAlertAsync(LocalizationService.Get("SettingsNotSaved"), LocalizationService.Get("SettingsSaveFailedHelp"), LocalizationService.Get("Ok"));
                        return;
                    }

                    _settings = settings;
                    LocalizationService.SetLanguage(settings.Language);
                    _viewModel.RefreshLocalization();
                    _viewModel.VatRate = vatRate;
                    if (settings.DataFilePath is null)
                        _viewModel.ApplyCatalog(ProductCatalog.Empty);
                    else if (importedCatalog is not null)
                        _viewModel.ApplyCatalog(importedCatalog);
                    _viewModel.DefaultMessage = settings.DefaultMessage;
                    if (defaultMessageEdited || MainViewModel.IsDefaultMessage(_viewModel.CustomText))
                        _viewModel.CustomText = _viewModel.DefaultMessage;
                    dataFileSelected = false;
                    try
                    {
                        SettingsPathPicker.CommitSavedAccess(settings.LogoPath, settings.SaveDirectory, settings.DataFilePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not persist selected file access: {ex}");
                        _viewModel.SetStatus("StatusFileAccessNotSaved");
                        await page.DisplayAlertAsync(LocalizationService.Get("FileAccessNotSaved"),
                            LocalizationService.Get("FileAccessNotSavedHelp"), LocalizationService.Get("Ok"));
                        await Navigation.PopModalAsync();
                        return;
                    }
                    if (importedCatalog is null)
                        _viewModel.SetStatus("StatusSettingsSaved");
                    else
                        _viewModel.SetStatus("StatusCatalogImported", importedCatalog.Items.Count);
                    if (importedCatalog is not null)
                        await page.DisplayAlertAsync(LocalizationService.Get("ImportComplete"),
                            LocalizationService.Format("CatalogImportedHelp", importedCatalog.Items.Count), LocalizationService.Get("Ok"));
                    await Navigation.PopModalAsync();
                }
                finally
                {
                    savingSettings = false;
                    page.Content.IsEnabled = true;
                    save.Text = LocalizationService.Get("SaveSettings");
                }
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
