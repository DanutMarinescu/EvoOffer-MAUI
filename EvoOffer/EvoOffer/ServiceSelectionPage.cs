using EvoOffer.Controls;
using EvoOffer.Models;
using EvoOffer.Services;
using EvoOffer.ViewModels;

namespace EvoOffer;

/// <summary>Selects a saved service without changing its definition in settings.</summary>
public sealed class ServiceSelectionPage : ContentPage
{
    public ServiceSelectionPage(IReadOnlyList<ServiceDefinition> services, MainViewModel offer)
    {
        Title = LocalizationService.Get("AddServiceToOffer");
        var picker = new DropdownPicker
        {
            Title = LocalizationService.Get("SelectService"),
            ItemsSource = services.ToArray(),
            ItemDisplayBinding = new Binding(nameof(ServiceDefinition.Name)),
            AutomationId = "OfferServicePicker",
            HeightRequest = 48
        };
        SemanticProperties.SetDescription(picker, LocalizationService.Get("SelectService"));
        var unit = new Label { AutomationId = "OfferServiceUnit" };
        var price = new Label { AutomationId = "OfferServicePrice" };
        var quantity = new QuantityInput
        {
            Text = "1", HeightRequest = 48, WidthRequest = 180,
            HorizontalOptions = LayoutOptions.Start, AutomationId = "OfferServiceQuantity"
        };
        var error = new Label
        {
            TextColor = Color.FromArgb("B42318"), IsVisible = false,
            AutomationId = "OfferServiceError"
        };
        var add = new Button
        {
            Text = LocalizationService.Get("AddServiceToOffer"), ImageSource = "plus.png",
            BackgroundColor = Color.FromArgb("147EF0"), TextColor = Colors.White,
            AutomationId = "ConfirmAddService"
        };
        var cancel = new Button { Text = LocalizationService.Get("Cancel"), AutomationId = "CancelAddService" };

        void UpdateSelection()
        {
            var service = picker.SelectedItem as ServiceDefinition;
            unit.Text = service?.UnitOfMeasurement ?? "—";
            price.Text = service is null ? "—" : $"{service.PriceExcludingVat.ToString("N2", LocalizationService.Culture)} RON";
            var valid = service is not null && new ServiceDraft(service).TryBuild(out _);
            add.IsEnabled = valid;
            error.Text = LocalizationService.Get("InvalidServiceValidation");
            error.IsVisible = service is not null && !valid;
        }
        picker.SelectedIndexChanged += (_, _) => UpdateSelection();
        quantity.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != nameof(QuantityInput.Text))
                return;
            quantity.HasError = !QuantityValue.TryParse(quantity.Text, out _);
            UpdateSelection();
            if (quantity.HasError)
            {
                error.Text = QuantityValue.ValidationMessage;
                error.IsVisible = true;
            }
        };

        var adding = false;
        add.Clicked += async (_, _) =>
        {
            if (adding || picker.SelectedItem is not ServiceDefinition selected)
                return;
            quantity.HasError = !QuantityValue.TryParse(quantity.Text, out _);
            if (!offer.TryAddService(selected, quantity.Text))
            {
                error.Text = offer.Status;
                error.IsVisible = true;
                return;
            }
            adding = true;
            add.IsEnabled = cancel.IsEnabled = false;
            await Navigation.PopModalAsync();
        };
        cancel.Clicked += async (_, _) =>
        {
            cancel.IsEnabled = false;
            await Navigation.PopModalAsync();
        };

        var buttons = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }],
            ColumnSpacing = 12
        };
        buttons.Add(add);
        buttons.Add(cancel, 1);
        var heading = new Label { Text = Title, FontSize = 28, FontAttributes = FontAttributes.Bold };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 32, Spacing = 16, MaximumWidthRequest = 640, HorizontalOptions = LayoutOptions.Fill,
                Children =
                {
                    heading,
                    new Label { Text = LocalizationService.Get("SelectServiceHelp"), TextColor = Color.FromArgb("50627C") },
                    FieldLabel("SelectService"),
                    new Border
                    {
                        Style = (Style)Application.Current!.Resources["InputBorderStyle"],
                        Content = picker
                    },
                    FieldLabel("ServiceUnit"), unit,
                    FieldLabel("ServicePrice"), price,
                    FieldLabel("Quantity"), quantity, error, buttons
                }
            }
        };
        picker.SelectedIndex = services.Count > 0 ? 0 : -1;
        UpdateSelection();
    }

    private static Label FieldLabel(string key) => new()
    {
        Text = LocalizationService.Get(key), FontAttributes = FontAttributes.Bold
    };
}
