using EvoOffer.Services;
using EvoOffer.Models;

namespace EvoOffer.Controls;

/// <summary>Bundled render previews also work on platforms without a QuestPDF runtime.</summary>
public sealed class PdfTemplateSelector : VerticalStackLayout
{
    private readonly DropdownPicker _picker;
    private readonly Button _preview;

    public PdfTemplateSelector(string selectedId, bool canRenderPdf, View? colorSettings = null)
    {
        Spacing = 12;
        Children.Add(new Label { Text = LocalizationService.Get("Template_Title"), FontSize = 18, FontAttributes = FontAttributes.Bold });
        _picker = new DropdownPicker
        {
            Title = LocalizationService.Get("Template_Title"), ItemsSource = OfferPdfTemplates.All.ToArray(),
            SelectedItem = OfferPdfTemplates.Find(selectedId), HeightRequest = 44,
            BackgroundColor = Colors.Transparent, AutomationId = "PdfTemplate"
        };
        SemanticProperties.SetDescription(_picker, LocalizationService.Get("Template_Title"));
        Children.Add(new Border
        {
            Stroke = Color.FromArgb("B8C8DC"), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            BackgroundColor = Color.FromArgb("EFF4FA"), Padding = new Thickness(12, 6), Content = _picker
        });
        var description = new Label { TextColor = Color.FromArgb("50627C"), AutomationId = "PdfTemplateDescription" };
        Children.Add(description);
        if (colorSettings is not null)
            Children.Add(colorSettings);
        var image = new Image
        {
            Aspect = Aspect.AspectFit, HeightRequest = 420, MaximumWidthRequest = 400,
            HorizontalOptions = LayoutOptions.Center, AutomationId = "PdfTemplateThumbnail"
        };
        Children.Add(new Border
        {
            Stroke = Color.FromArgb("D2DCE8"), StrokeThickness = 1, Padding = 12,
            BackgroundColor = Color.FromArgb("EFF4FA"), Content = image
        });
        Children.Add(new Label
        {
            Text = LocalizationService.Get("Template_SampleNote"),
            FontSize = 13, TextColor = Color.FromArgb("50627C")
        });
        _preview = new Button
        {
            Text = LocalizationService.Get("Template_PreviewButton"), HorizontalOptions = LayoutOptions.Start,
            IsVisible = canRenderPdf, AutomationId = "PreviewPdfTemplate"
        };
        SemanticProperties.SetHint(_preview, LocalizationService.Get("Template_PreviewHint"));
        _preview.Clicked += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);
        Children.Add(_preview);
        Children.Add(new Label
        {
            Text = canRenderPdf
                ? LocalizationService.Get("Template_PreviewNote")
                : LocalizationService.Get("Template_PreviewWindowsOnly"),
            FontSize = 13, TextColor = Color.FromArgb("50627C")
        });
        void UpdatePreview()
        {
            var template = OfferPdfTemplates.Find(SelectedTemplateId);
            description.Text = template.Description;
            image.Source = template.PreviewImage;
            SemanticProperties.SetDescription(image, LocalizationService.Format("Template_SampleDescription", template.Name, template.Description));
        }
        _picker.SelectedIndexChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    public string SelectedTemplateId => (_picker.SelectedItem as OfferPdfTemplateDefinition)?.Id ?? OfferPdfTemplates.Classic;
    public void SetPreviewBusy(bool busy) => _preview.Text = busy ? LocalizationService.Get("Template_PreviewGenerating") : LocalizationService.Get("Template_PreviewButton");
    public event EventHandler? PreviewRequested;
}
