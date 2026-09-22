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
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage(_pdfService))
        {
            Title = "Offer Generator",
            Width = 1440,
            Height = 960,
            MinimumWidth = 760,
            MinimumHeight = 760
        };
    }
}
