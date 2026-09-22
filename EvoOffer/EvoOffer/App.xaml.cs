namespace EvoOffer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "Offer Generator",
            Width = 1440,
            Height = 960,
            MinimumWidth = 760,
            MinimumHeight = 760
        };
    }
}
