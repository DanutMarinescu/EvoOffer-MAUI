using EvoOffer.Services;

namespace EvoOffer;

/// <summary>Displays the actual QuestPDF document with the PDF viewer's controls.</summary>
public sealed class OfferPreviewPage : ContentPage
{
    private readonly OfferPdfPreviewFile _previewFile;
    private readonly WebView _pdfView;
    private readonly ActivityIndicator _loading;
    private readonly Label _status;
    private readonly Button _retry;
    private readonly string _pdfUrl;
    private readonly bool _isTemplatePreview;
    private bool _closed;

    public OfferPreviewPage(OfferPdfPreviewFile previewFile, bool isTemplatePreview = false)
    {
        ArgumentNullException.ThrowIfNull(previewFile);
        _previewFile = previewFile;
        _isTemplatePreview = isTemplatePreview;
        _pdfUrl = new Uri(previewFile.FilePath).AbsoluteUri;

        Title = isTemplatePreview ? LocalizationService.Get("Preview_TemplateTitle") : LocalizationService.Get("Preview_OfferTitle");
        BackgroundColor = ResourceColor("PageBackground", "#F7F9FC");

        var title = new Label
        {
            Text = Title, FontSize = 28, FontAttributes = FontAttributes.Bold,
            TextColor = ResourceColor("Ink", "#101D31")
        };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var close = new Button { Text = LocalizationService.Get("Preview_Close"), AutomationId = "CloseOfferPreview" };
        SemanticProperties.SetHint(close, isTemplatePreview ? LocalizationService.Get("Preview_ReturnSettings") : LocalizationService.Get("Preview_ReturnEditor"));
        close.Clicked += async (_, _) =>
        {
            close.IsEnabled = false;
            try
            {
                await Navigation.PopModalAsync();
            }
            finally
            {
                close.IsEnabled = true;
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }],
            ColumnSpacing = 24,
            Padding = new Thickness(24, 20),
            BackgroundColor = Colors.White
        };
        header.Add(title);
        header.Add(close, 1);

        _loading = new ActivityIndicator { IsRunning = true, WidthRequest = 24, HeightRequest = 24 };
        _status = new Label
        {
            Text = LocalizationService.Get("Preview_Loading"), VerticalOptions = LayoutOptions.Center,
            TextColor = ResourceColor("MutedInk", "#50627C"), AutomationId = "OfferPreviewStatus"
        };
        _retry = new Button { Text = LocalizationService.Get("Preview_Retry"), IsVisible = false, AutomationId = "RetryOfferPreview" };
        var statusBar = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            ],
            ColumnSpacing = 12, Padding = new Thickness(24, 8)
        };
        statusBar.Add(_loading);
        statusBar.Add(_status, 1);
        statusBar.Add(_retry, 2);

        // Windows' MAUI WebView uses WebView2, which renders local PDFs directly.
        _pdfView = new WebView { AutomationId = "OfferPdfPreview" };
        SemanticProperties.SetDescription(_pdfView, isTemplatePreview ? LocalizationService.Get("Preview_TemplateDescription") : LocalizationService.Get("Preview_OfferDescription"));
        _pdfView.Navigated += OnPdfNavigated;
        _pdfView.ProcessTerminated += (_, _) => ShowLoadError(canRetry: false);
        _pdfView.Source = _pdfUrl;
        _retry.Clicked += (_, _) =>
        {
            _retry.IsVisible = false;
            _loading.IsRunning = true;
            _loading.IsVisible = true;
            _status.Text = LocalizationService.Get("Preview_Loading");
            try
            {
                _pdfView.Reload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF viewer reload failed: {ex}");
                ShowLoadError(canRetry: false);
            }
        };

        var layout = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            ]
        };
        layout.Add(header);
        layout.Add(statusBar, 0, 1);
        layout.Add(_pdfView, 0, 2);
        Content = layout;
    }

    private void OnPdfNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_closed || !string.Equals(e.Url, _pdfUrl, StringComparison.Ordinal))
            return;

        if (e.Result != WebNavigationResult.Success)
        {
            ShowLoadError();
            return;
        }

        _loading.IsRunning = false;
        _loading.IsVisible = false;
        _retry.IsVisible = false;
        _status.Text = _isTemplatePreview
            ? LocalizationService.Get("Preview_TemplateInstructions")
            : LocalizationService.Get("Preview_OfferInstructions");
    }

    private void ShowLoadError(bool canRetry = true)
    {
        if (_closed)
            return;

        _loading.IsRunning = false;
        _loading.IsVisible = false;
        _retry.IsVisible = canRetry;
        _status.Text = canRetry
            ? LocalizationService.Get("Preview_LoadError")
            : LocalizationService.Get("Preview_ViewerStopped");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _closed = true;
        // Release the viewer before deleting the document so it no longer holds the PDF open.
        try
        {
            _pdfView.Handler?.DisconnectHandler();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PDF viewer cleanup failed: {ex}");
        }
        finally
        {
            _previewFile.Dispose();
        }
    }

    private static Color ResourceColor(string key, string fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallback);
}
