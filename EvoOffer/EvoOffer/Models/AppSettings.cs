using System.Text.Json.Serialization;
using EvoOffer.ViewModels;

namespace EvoOffer.Models;

public sealed class AppSettings
{
    public const string Romanian = "Română";
    public const string English = "English";

    public static string DefaultSaveDirectory { get; } = GetDefaultSaveDirectory();

    public string IssuerName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string SaveDirectory { get; set; } = DefaultSaveDirectory;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string Language { get; set; } = Romanian;
    public decimal VatRate { get; set; } = VatRateValue.Default;
    public string DefaultMessage { get; set; } = MainViewModel.DefaultCustomText;

    public void Normalize()
    {
        IssuerName ??= string.Empty;
        if (string.IsNullOrWhiteSpace(LogoPath))
            LogoPath = null;
        if (string.IsNullOrWhiteSpace(SaveDirectory))
            SaveDirectory = DefaultSaveDirectory;
        Email = Email?.Trim() ?? string.Empty;
        PhoneNumber = ContactDataValue.DigitsOnly(PhoneNumber);
        AddressLine1 ??= string.Empty;
        AddressLine2 ??= string.Empty;
        VatNumber = ContactDataValue.DigitsOnly(VatNumber);
        if (Language is not (Romanian or English))
            Language = Romanian;
        if (!VatRateValue.IsValid(VatRate))
            VatRate = VatRateValue.Default;
        DefaultMessage ??= MainViewModel.DefaultCustomText;
    }

    private static string GetDefaultSaveDirectory()
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(directory))
            directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(directory))
            directory = AppContext.BaseDirectory;
        return Path.Combine(directory, "EvoOffer");
    }
}

// Source generation also supports trimmed/AOT builds on Apple platforms.
[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
