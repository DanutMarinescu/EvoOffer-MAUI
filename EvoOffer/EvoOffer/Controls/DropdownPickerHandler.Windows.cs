#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Controls;

namespace EvoOffer.Controls;

public sealed class DropdownPickerHandler : PickerHandler
{
    private static readonly IPropertyMapper<IPicker, IPickerHandler> DropdownMapper =
        new PropertyMapper<IPicker, IPickerHandler>(PickerHandler.Mapper)
        {
            [nameof(IPicker.Title)] = MapPlaceholder,
            [nameof(IPicker.TitleColor)] = MapPlaceholderColor
        };

    public DropdownPickerHandler() : base(DropdownMapper)
    {
    }

    private static void MapPlaceholder(IPickerHandler handler, IPicker picker)
    {
        // MAUI maps Title to a separate ComboBox header. Our fields already have
        // labels, and that extra row clips the selection and arrow in compact cells.
        handler.PlatformView.Header = null;
        handler.PlatformView.HeaderTemplate = null;
        handler.PlatformView.PlaceholderText = picker.Title ?? string.Empty;
        handler.UpdateValue(nameof(IView.Semantics));
    }

    private static void MapPlaceholderColor(IPickerHandler handler, IPicker picker)
    {
        // Override TitleColor too: the default mapping recreates the header.
        if (picker.TitleColor is { } color)
            handler.PlatformView.PlaceholderForeground = color.ToPlatform();
        else
            handler.PlatformView.ClearValue(ComboBox.PlaceholderForegroundProperty);
    }
}
#endif
