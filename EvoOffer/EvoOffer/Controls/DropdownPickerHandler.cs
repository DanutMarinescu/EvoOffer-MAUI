#if IOS || MACCATALYST
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace EvoOffer.Controls;

public sealed class DropdownPickerHandler : ViewHandler<IPicker, UIButton>
{
    public static readonly IPropertyMapper<IPicker, DropdownPickerHandler> Mapper =
        new PropertyMapper<IPicker, DropdownPickerHandler>(ViewMapper)
        {
            [nameof(IPicker.Items)] = MapMenu,
            [nameof(IPicker.SelectedIndex)] = MapMenu,
            [nameof(IPicker.Title)] = MapAppearance,
            [nameof(IPicker.TitleColor)] = MapAppearance,
            [nameof(IPicker.TextColor)] = MapAppearance,
            [nameof(IPicker.Font)] = MapFont,
            [nameof(IPicker.IsEnabled)] = MapEnabled
        };

    private int _menuVersion;

    public DropdownPickerHandler() : base(Mapper)
    {
    }

    protected override UIButton CreatePlatformView() => new DropdownButton
    {
        ShowsMenuAsPrimaryAction = true,
        AccessibilityTraits = UIAccessibilityTrait.Button
    };

    protected override void DisconnectHandler(UIButton platformView)
    {
        _menuVersion++;
        platformView.Menu = null;
        base.DisconnectHandler(platformView);
    }

    private static void MapMenu(DropdownPickerHandler handler, IPicker picker)
    {
        var version = ++handler._menuVersion;
        var weakHandler = new WeakReference<DropdownPickerHandler>(handler);
        var actions = new UIMenuElement[picker.Items.Count];
        for (var index = 0; index < actions.Length; index++)
        {
            var selectedIndex = index;
            var action = UIAction.Create(picker.Items[index], null, null, _ =>
            {
                // Ignore actions from a menu whose source changed while open.
                if (weakHandler.TryGetTarget(out var current) && current._menuVersion == version
                    && current.VirtualView is { IsEnabled: true } view
                    && selectedIndex < view.Items.Count)
                    view.SelectedIndex = selectedIndex;
            });
            action.State = index == picker.SelectedIndex ? UIMenuElementState.On : UIMenuElementState.Off;
            actions[index] = action;
        }

        handler.PlatformView.Menu = actions.Length == 0 ? null : UIMenu.Create(actions);
        MapAppearance(handler, picker);
        MapEnabled(handler, picker);
    }

    private static void MapAppearance(DropdownPickerHandler handler, IPicker picker)
    {
        var hasSelection = picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count;
        var text = hasSelection ? picker.Items[picker.SelectedIndex] : picker.Title;
        var color = (hasSelection ? picker.TextColor : picker.TitleColor)?.ToPlatform() ?? UIColor.Label;
        handler.PlatformView.SetTitle(text, UIControlState.Normal);
        handler.PlatformView.SetTitleColor(color, UIControlState.Normal);
        handler.PlatformView.TintColor = color;
        handler.PlatformView.AccessibilityValue = text;
    }

    private static void MapFont(DropdownPickerHandler handler, IPicker picker)
    {
        if (handler.MauiContext is { } context)
            handler.PlatformView.TitleLabel.Font = context.Services.GetRequiredService<IFontManager>().GetFont(picker.Font, 17);
    }

    private static void MapEnabled(DropdownPickerHandler handler, IPicker picker) =>
        handler.PlatformView.Enabled = picker.IsEnabled && picker.Items.Count > 0;

    private sealed class DropdownButton : UIButton
    {
        // The UIButtonType constructor creates a native base button and bypasses
        // subclass layout overrides. Use the default constructor for this subclass.
        public DropdownButton()
        {
            HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            SetImage(UIImage.GetSystemImage("chevron.down"), UIControlState.Normal);
            TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            TitleLabel.Lines = 1;
            ImageView.ContentMode = UIViewContentMode.ScaleAspectFit;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            // Reserve the trailing edge for the arrow, even when text truncates.
            const double arrowWidth = 12;
            const double arrowSpace = 24;
            var rightToLeft = EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.RightToLeft;
            TitleLabel.Frame = new CoreGraphics.CGRect(rightToLeft ? arrowSpace : 0, 0,
                Math.Max(0, Bounds.Width - arrowSpace), Bounds.Height);
            TitleLabel.TextAlignment = rightToLeft ? UITextAlignment.Right : UITextAlignment.Left;
            ImageView.Frame = new CoreGraphics.CGRect(rightToLeft ? 0 : Math.Max(0, Bounds.Width - arrowWidth),
                (Bounds.Height - arrowWidth) / 2, arrowWidth, arrowWidth);
        }
    }
}
#endif
