using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace EvoOffer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

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
