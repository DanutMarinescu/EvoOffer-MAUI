using System.Globalization;
using EvoOffer.Models;
using EvoOffer.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace EvoOffer;

public sealed class OfferPreviewPage : ContentPage
{
    private readonly Color _ink = ResourceColor("Ink", "#101D31");
    private readonly Color _mutedInk = ResourceColor("MutedInk", "#50627C");

    public OfferPreviewPage(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // Keep this preview stable if the editor changes while it is open.
        var clientName = viewModel.ClientName.Trim();
        var message = viewModel.CustomText;
        var vatRate = viewModel.VatRate;
        var lines = viewModel.Items.Select(item => new PreviewLine(
            item.Number, item.Category, item.Name, item.UnitPrice, item.Quantity, item.NetTotal, item.VatAmount, item.Total)).ToArray();
        var grandTotal = lines.Sum(line => line.Total);

        Title = "Offer preview";
        BackgroundColor = ResourceColor("PageBackground", "#F7F9FC");

        var title = Text("Offer preview", 28, bold: true);
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var subtitle = Text("Review your offer details", 15, color: _mutedInk);
        var close = new Button { Text = "Close preview", AutomationId = "CloseOfferPreview" };
        SemanticProperties.SetHint(close, "Return to the offer editor.");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }],
            ColumnSpacing = 24,
            Padding = new Thickness(28, 24),
            BackgroundColor = Colors.White
        };
        header.Add(new VerticalStackLayout { Spacing = 5, Children = { title, subtitle } });
        header.Add(close, 1);

        var preparedFor = Text("PREPARED FOR", 12, bold: true, color: _mutedInk);
        var client = Text(clientName, 26, bold: true);
        SemanticProperties.SetHeadingLevel(client, SemanticHeadingLevel.Level2);
        var document = new VerticalStackLayout { Spacing = 22, Padding = 28, MaximumWidthRequest = 1120 };
        document.Children.Add(new VerticalStackLayout { Spacing = 8, Children = { preparedFor, client } });
        if (!string.IsNullOrWhiteSpace(message))
            document.Children.Add(Text(message, 16));

        var table = new VerticalStackLayout { Spacing = 0, WidthRequest = 1060 };
        table.Children.Add(CreateRow(["#", "Category", "Item", "Unit price (RON)\nVAT excluded", "Quantity",
            $"VAT {VatRateValue.Format(vatRate)}%\n(RON)", "Total (RON)\nVAT included"], isHeader: true));
        foreach (var line in lines)
        {
            var row = CreateRow(
                [line.Number.ToString(CultureInfo.InvariantCulture), line.Category, line.Name,
                    Money(line.UnitPrice), QuantityValue.Format(line.Quantity), Money(line.VatAmount), Money(line.Total)],
                alternate: line.Number % 2 == 0);
            table.Children.Add(row);
        }

        document.Children.Add(new Border
        {
            Stroke = Color.FromArgb("#E0E7F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            BackgroundColor = Colors.White,
            Content = new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                Content = table
            }
        });

        document.Children.Add(Text($"Subtotal (VAT excluded): {Money(lines.Sum(line => line.NetTotal))} RON", 16));
        document.Children.Add(Text($"VAT ({VatRateValue.Format(vatRate)}%): {Money(lines.Sum(line => line.VatAmount))} RON", 16));

        var total = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }],
            ColumnSpacing = 20,
            Padding = new Thickness(20, 18),
            BackgroundColor = Color.FromArgb("#EAF5EE")
        };
        total.Add(Text("Grand total (VAT included)", 19, bold: true));
        var amount = Text($"{Money(grandTotal)} RON", 24, bold: true, color: Color.FromArgb("#08773D"));
        amount.HorizontalTextAlignment = TextAlignment.End;
        SemanticProperties.SetDescription(amount, $"Grand total including VAT, {Money(grandTotal)} Romanian lei");
        total.Add(amount, 1);
        document.Children.Add(total);

        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Vertical,
            VerticalScrollBarVisibility = ScrollBarVisibility.Always,
            Content = document,
            AutomationId = "OfferPreviewScroll"
        };
        var layout = new Grid
        {
            RowDefinitions = [new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star }]
        };
        layout.Add(header);
        layout.Add(scroll, 0, 1);
        Content = layout;

        SizeChanged += (_, _) =>
        {
            var padding = Width < 900 ? 18 : 28;
            document.Padding = padding;
            header.Padding = new Thickness(padding, 20);
        };
    }

    private Grid CreateRow(string[] values, bool isHeader = false, bool alternate = false)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(140) },
                new ColumnDefinition { Width = new GridLength(80) },
                new ColumnDefinition { Width = new GridLength(110) },
                new ColumnDefinition { Width = new GridLength(140) }
            ],
            ColumnSpacing = 12,
            Padding = new Thickness(14, isHeader ? 13 : 17),
            BackgroundColor = isHeader ? Color.FromArgb("#EEF3F8")
                : alternate ? Color.FromArgb("#F8FAFC") : Colors.White
        };

        for (var column = 0; column < values.Length; column++)
        {
            var label = Text(values[column], isHeader ? 13 : 14, bold: isHeader,
                color: isHeader ? _mutedInk : _ink);
            label.HorizontalTextAlignment = column >= 3 ? TextAlignment.End : TextAlignment.Start;
            row.Add(label, column);
        }

        return row;
    }

    private Label Text(string text, double size, bool bold = false, Color? color = null) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        TextColor = color ?? _ink,
        LineBreakMode = LineBreakMode.WordWrap,
        VerticalOptions = LayoutOptions.Center
    };

    private static Color ResourceColor(string key, string fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallback);

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private sealed record PreviewLine(int Number, string Category, string Name, decimal UnitPrice, decimal Quantity,
        decimal NetTotal, decimal VatAmount, decimal Total);
}
