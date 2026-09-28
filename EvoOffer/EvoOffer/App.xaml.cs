using EvoOffer.Services;

namespace EvoOffer;

public partial class App : Application
{
    private readonly IOfferPdfService _pdfService;

    public App(IOfferPdfService pdfService)
    {
        InitializeComponent();
        _pdfService = pdfService;
        UserAppTheme = AppTheme.Light;
        RefreshLanguageResources();
        LocalizationService.LanguageChanged += (_, _) => RefreshLanguageResources();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage(_pdfService))
        {
            Width = 1440,
            Height = 960,
            MinimumWidth = 760,
            MinimumHeight = 760
        };
        window.SetDynamicResource(Window.TitleProperty, "AppTitle");
        return window;
    }

    private void RefreshLanguageResources()
    {
        foreach (var (key, value) in LocalizationService.GetStrings())
            Resources[key] = value;
    }
}
