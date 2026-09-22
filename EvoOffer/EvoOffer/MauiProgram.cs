using EvoOffer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace EvoOffer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if WINDOWS
        // WebView2 needs a writable cache even when the app is installed in Program Files.
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER",
            Path.Combine(FileSystem.Current.AppDataDirectory, "WebView2"));
        // Settings initializes native dependencies; only access it on supported MAUI targets.
        // Choose the appropriate QuestPDF license before production use:
        // https://www.questpdf.com/license/configuration.html
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Evaluation;
#endif
        // Configure PDF layout defaults here; callers can also override them per document.
        builder.Services.AddSingleton(new OfferPdfOptions());
        builder.Services.AddSingleton<IOfferPdfService, OfferPdfService>();

#if IOS || MACCATALYST
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<Controls.DropdownPicker, Controls.DropdownPickerHandler>());
#endif

        // The shared XAML borders own the input outline on every desktop platform.
        EntryHandler.Mapper.AppendToMapping("OfferInput", (handler, _) =>
        {
#if IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
#endif
        });
        PickerHandler.Mapper.AppendToMapping("OfferInput", (handler, _) =>
        {
#if IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
#endif
        });
        EditorHandler.Mapper.AppendToMapping("OfferInput", (handler, _) =>
        {
#if IOS || MACCATALYST
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
#endif
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
