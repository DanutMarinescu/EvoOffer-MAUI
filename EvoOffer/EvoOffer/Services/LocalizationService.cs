using System.Collections;
using System.Globalization;
using System.Resources;
using EvoOffer.Models;

namespace EvoOffer.Services;

/// <summary>Shared UI translations; Romanian is also the fallback for unknown languages.</summary>
public static class LocalizationService
{
    private static readonly ResourceManager Resources = new(
        "EvoOffer.Resources.Strings.AppResources", typeof(LocalizationService).Assembly);

    public static string Language { get; private set; } = AppSettings.Romanian;
    public static CultureInfo Culture => CultureFor(Language);
    public static event EventHandler? LanguageChanged;

    public static CultureInfo CultureFor(string? language) =>
        CultureInfo.GetCultureInfo(language == AppSettings.English ? "en-GB" : "ro-RO");

    public static string Get(string key) => Get(key, Language);

    public static string Get(string key, string? language) =>
        Resources.GetString(key, CultureFor(language))
        ?? throw new MissingManifestResourceException($"Missing UI translation: {key}");

    public static string Format(string key, params object[] args) =>
        string.Format(Culture, Get(key), args);

    public static void SetLanguage(string? language)
    {
        var normalized = language == AppSettings.English ? AppSettings.English : AppSettings.Romanian;
        if (Language == normalized)
            return;
        Language = normalized;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    // MAUI installs these as dynamic resources so existing controls, including
    // recycled table rows and accessibility labels, update without a restart.
    public static IReadOnlyDictionary<string, string> GetStrings()
    {
        var defaults = Resources.GetResourceSet(CultureInfo.InvariantCulture, true, true)
            ?? throw new MissingManifestResourceException("The Romanian UI resources are missing.");
        return defaults.Cast<DictionaryEntry>().ToDictionary(
            entry => (string)entry.Key, entry => Get((string)entry.Key));
    }
}
