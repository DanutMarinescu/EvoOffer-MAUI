using EvoOffer.Models;

namespace EvoOffer.Controls;

public partial class QuantityInput : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(QuantityInput), "1", BindingMode.TwoWay);

    public static readonly BindableProperty HasErrorProperty = BindableProperty.Create(
        nameof(HasError), typeof(bool), typeof(QuantityInput), false,
        propertyChanged: OnHasErrorChanged);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public QuantityInput() => InitializeComponent();

    private static void OnHasErrorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var input = (QuantityInput)bindable;
        input.InputBorder.Stroke = new SolidColorBrush((bool)newValue
            ? Color.FromArgb("DC3545") : Color.FromArgb("CCD6E2"));
        SemanticProperties.SetHint(input.QuantityEntry, (bool)newValue
            ? "Enter a quantity between 1 and 999999" : "Enter the item quantity");
    }

    private void OnIncreaseClicked(object? sender, EventArgs e) => Adjust(1);
    private void OnDecreaseClicked(object? sender, EventArgs e) => Adjust(-1);

    private void Adjust(int change)
    {
        if (!QuantityValue.TryParse(Text, out var quantity))
            quantity = QuantityValue.Minimum;

        Text = QuantityValue.Format(Math.Clamp(quantity + change,
            QuantityValue.Minimum, QuantityValue.Maximum));
    }
}
