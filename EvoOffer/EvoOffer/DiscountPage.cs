using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

namespace EvoOffer;

/// <summary>Edits a discount without changing the offer until OK or Reset is selected.</summary>
public sealed class DiscountPage : ContentPage
{
    public DiscountPage(MainViewModel offer, OfferLineItem? item = null)
    {
        var current = item?.Discount ?? (item is null ? offer.GeneralDiscount : null);
        Title = LocalizationService.Get(item is null ? "OfferDiscount" : "ItemDiscount");
        var group = Guid.NewGuid().ToString("N");
        var perUnit = Choice("DiscountPerUnit", group + "Scope", "DiscountPerUnit", current?.Scope == DiscountScope.PerUnit);
        var itemTotal = Choice("DiscountItemTotal", group + "Scope", "DiscountItemTotal", current?.Scope != DiscountScope.PerUnit);
        var percentage = Choice("DiscountPercentage", group + "Type", "DiscountPercentage", current?.ValueType != DiscountValueType.Absolute);
        var absolute = Choice("DiscountAbsolute", group + "Type", "DiscountAbsolute", current?.ValueType == DiscountValueType.Absolute);
        var valueLabel = new Label { FontAttributes = FontAttributes.Bold };
        var value = new Entry
        {
            Text = current?.Value.ToString("0.##", LocalizationService.Culture) ?? string.Empty,
            Keyboard = Keyboard.Numeric, ReturnType = ReturnType.Done,
            AutomationId = "DiscountValue", Margin = new Thickness(12, 0),
            Placeholder = "0", ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        var valueBorder = new Border
        {
            Style = (Style)Application.Current!.Resources["InputBorderStyle"],
            HeightRequest = 48, Content = value
        };
        var basis = new Label { TextColor = Color.FromArgb("50627C"), AutomationId = "DiscountBase" };
        var preview = new Label { TextColor = Color.FromArgb("B42318"), AutomationId = "DiscountPreview" };
        var error = new Label { TextColor = Color.FromArgb("B42318"), IsVisible = false, AutomationId = "DiscountError" };
        var ok = new Button
        {
            Text = LocalizationService.Get("Ok"), BackgroundColor = Color.FromArgb("147EF0"),
            TextColor = Colors.White, AutomationId = "ConfirmDiscount"
        };
        var reset = new Button { Text = LocalizationService.Get("Reset"), AutomationId = "ResetDiscount" };
        var cancel = new Button { Text = LocalizationService.Get("Cancel"), AutomationId = "CancelDiscount" };

        decimal GrossTotal() => item?.OriginalTotal ?? offer.TotalBeforeGeneralDiscount;
        DiscountScope Scope() => item is not null && perUnit.IsChecked ? DiscountScope.PerUnit : DiscountScope.ItemTotal;
        decimal BaseAmount() => Scope() == DiscountScope.PerUnit ? GrossTotal() / item!.Quantity : GrossTotal();
        bool ReadDiscount(out DiscountDefinition? discount, out string message) => DiscountDefinition.TryCreate(
            Scope(), percentage.IsChecked ? DiscountValueType.Percentage : DiscountValueType.Absolute,
            value.Text, BaseAmount(), out discount, out message);

        void UpdatePreview(bool showError)
        {
            valueLabel.Text = LocalizationService.Get(percentage.IsChecked ? "DiscountPercentageValue" : "DiscountAbsoluteValue");
            SemanticProperties.SetDescription(value, valueLabel.Text);
            basis.Text = LocalizationService.Format("DiscountBaseAmount", BaseAmount().ToString("N2", LocalizationService.Culture));
            var valid = ReadDiscount(out var discount, out var message);
            error.Text = message;
            error.IsVisible = showError && !valid;
            valueBorder.Stroke = Color.FromArgb(error.IsVisible ? "B42318" : "CCD6E2");
            preview.IsVisible = valid;
            if (valid)
            {
                var reduction = discount?.CalculateAmount(GrossTotal(), item?.Quantity ?? 1m) ?? 0m;
                preview.Text = LocalizationService.Format("DiscountPreviewAmount",
                    reduction.ToString("N2", LocalizationService.Culture),
                    (GrossTotal() - reduction).ToString("N2", LocalizationService.Culture));
            }
        }

        foreach (var choice in new[] { perUnit, itemTotal, percentage, absolute })
            choice.CheckedChanged += (_, _) => UpdatePreview(error.IsVisible);
        value.TextChanged += (_, _) => UpdatePreview(error.IsVisible);

        var closing = false;
        async Task CloseAsync(bool apply, DiscountDefinition? discount)
        {
            if (closing)
                return;
            closing = true;
            ok.IsEnabled = reset.IsEnabled = cancel.IsEnabled = false;
            if (apply)
            {
                if (item is null)
                    offer.GeneralDiscount = discount;
                else
                    item.Discount = discount;
            }
            await Navigation.PopModalAsync();
        }
        ok.Clicked += async (_, _) =>
        {
            if (!ReadDiscount(out var discount, out _))
            {
                UpdatePreview(showError: true);
                value.Focus();
                return;
            }
            await CloseAsync(apply: true, discount);
        };
        reset.Clicked += async (_, _) => await CloseAsync(apply: true, null);
        cancel.Clicked += async (_, _) => await CloseAsync(apply: false, null);

        var heading = new Label { Text = Title, FontSize = 28, FontAttributes = FontAttributes.Bold };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var scopeChoices = new VerticalStackLayout
        {
            IsVisible = item is not null, Spacing = 4,
            Children = { new Label { Text = LocalizationService.Get("DiscountApplyTo"), FontAttributes = FontAttributes.Bold }, perUnit, itemTotal }
        };
        var buttons = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)],
            ColumnSpacing = 12, Margin = new Thickness(0, 8, 0, 0)
        };
        buttons.Add(ok);
        buttons.Add(reset, 1);
        buttons.Add(cancel, 2);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 32, Spacing = 16, MaximumWidthRequest = 640, HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    heading,
                    new Label { Text = item?.Name ?? LocalizationService.Get("OfferDiscountHelp"), FontAttributes = FontAttributes.Bold },
                    new Label { Text = LocalizationService.Get("DiscountVatHelp"), TextColor = Color.FromArgb("50627C") },
                    scopeChoices,
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { new Label { Text = LocalizationService.Get("DiscountType"), FontAttributes = FontAttributes.Bold }, percentage, absolute }
                    },
                    basis, valueLabel, valueBorder, preview, error, buttons
                }
            }
        };
        UpdatePreview(showError: false);
    }

    private static RadioButton Choice(string key, string group, string automationId, bool isChecked) => new()
    {
        Content = LocalizationService.Get(key), GroupName = group, IsChecked = isChecked,
        AutomationId = automationId, TextColor = Color.FromArgb("101D31"), MinimumHeightRequest = 44
    };
}
