using System.Globalization;
using EvoOffer.Models;

namespace EvoOffer.Services;

/// <summary>Owns a temporary QuestPDF document for the lifetime of its preview.</summary>
public sealed class OfferPdfPreviewFile : IDisposable
{
    private OfferPdfPreviewFile(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public static async Task<OfferPdfPreviewFile> CreateAsync(IOfferPdfService pdfService,
        OfferPdfData offer, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfService);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        if (!pdfService.IsSupported)
            throw new PlatformNotSupportedException(
                "QuestPDF does not support iOS, Android or Mac Catalyst. Generate PDFs on Windows or in a desktop/server .NET host.");

        // Each preview has its own directory so identical client names and timestamps are safe.
        var previewDirectory = Path.Combine(cacheDirectory, "offer-previews", Guid.NewGuid().ToString("N"));
        var clientName = string.Concat(offer.ClientName.Select(character =>
            char.IsControl(character) || "<>:\"/\\|?*".Contains(character) ? '_' : character));
        var generatedAt = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(previewDirectory, $"{clientName}-{generatedAt}.pdf");
        OfferPdfPreviewFile? preview = null;
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(previewDirectory);
                preview = new OfferPdfPreviewFile(filePath);
                using var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                pdfService.Generate(offer, output);
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return preview!;
        }
        catch
        {
            preview?.Dispose();
            throw;
        }
    }

    /// <summary>Saves a permanent copy independently of the temporary preview's lifetime.</summary>
    public async Task<string> SaveCopyAsync(string saveDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetFullPath(saveDirectory);
        var fileName = Path.GetFileNameWithoutExtension(FilePath);
        var savedPath = Path.Combine(directory, $"{fileName}.pdf");
        var stagingPath = Path.Combine(directory, $".offer-{Guid.NewGuid():N}.tmp");
        var stagingCreated = false;
        try
        {
            Directory.CreateDirectory(directory);
            await using (var source = new FileStream(FilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, useAsync: true))
            await using (var destination = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, useAsync: true))
            {
                stagingCreated = true;
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Publish only the completed PDF. Never replace an existing offer.
            for (var copyNumber = 2; ; copyNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(stagingPath, savedPath);
                    return savedPath;
                }
                catch (IOException) when (File.Exists(savedPath) || Directory.Exists(savedPath))
                {
                    savedPath = Path.Combine(directory, $"{fileName} ({copyNumber}).pdf");
                }
            }
        }
        catch
        {
            if (stagingCreated)
            {
                try { File.Delete(stagingPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Preserve the original error if the destination becomes unavailable.
                }
            }
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            File.Delete(FilePath);
            Directory.Delete(Path.GetDirectoryName(FilePath)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A native viewer can briefly keep the cached PDF locked after closing.
        }
    }
}
