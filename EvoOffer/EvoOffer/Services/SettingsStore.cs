using System.Text.Json;
using EvoOffer.Models;

namespace EvoOffer.Services;

public sealed class SettingsStore(string directory)
{
    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    public AppSettings Load(AppSettings? initialSettings = null)
    {
        AppSettings settings;
        try
        {
            using var stream = File.OpenRead(FilePath);
            settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings)
                ?? throw new JsonException("Settings must contain a JSON object.");
        }
        catch (FileNotFoundException)
        {
            settings = initialSettings ?? new AppSettings();
            Save(settings);
        }
        catch (DirectoryNotFoundException)
        {
            settings = initialSettings ?? new AppSettings();
            Save(settings);
        }

        settings.Normalize();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = FilePath + ".tmp";
        // Finish writing before replacing the previous config so a failed write
        // cannot leave a partially written settings file.
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
