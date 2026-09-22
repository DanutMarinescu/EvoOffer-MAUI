using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

#if IOS || MACCATALYST
using Foundation;
using UIKit;
using UniformTypeIdentifiers;
#endif

namespace EvoOffer.Controls;

public static class SettingsPathPicker
{
    public static Task<string?> PickLogoAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
#if IOS || MACCATALYST
        return await PickAppleAsync([UTTypes.Png, UTTypes.Jpeg, UTTypes.WebP], LogoAccess);
#else
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a logo image",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".png", ".jpg", ".jpeg", ".webp"],
                [DevicePlatform.Android] = ["image/png", "image/jpeg", "image/webp"]
            })
        });
        if (result is null)
            return null;
        var extension = Path.GetExtension(result.FullPath);
        if (!new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new IOException("Choose a PNG, JPEG, or WebP image for the logo.");
        if (!File.Exists(result.FullPath))
            throw new FileNotFoundException("The selected logo image is no longer available.", result.FullPath);
        return result.FullPath;
#endif
    });

    public static Task<string?> PickSaveDirectoryAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
    {
#if IOS || MACCATALYST
        return await PickAppleAsync([UTTypes.Folder], DirectoryAccess);
#elif WINDOWS
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("No window is available to show the folder browser.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Folder selection is not supported on this platform.");
#endif
    });

    // Keep external Apple resources accessible to the existing path-based PDF service.
    // Each setting retains at most one saved and one draft security scope.
    public static void RestoreSavedAccess()
    {
#if IOS || MACCATALYST
        LogoAccess.Restore();
        DirectoryAccess.Restore();
#endif
    }

    public static void CommitSavedAccess(string? logoPath, string? saveDirectory)
    {
#if IOS || MACCATALYST
        var errors = new List<Exception>();
        foreach (var (resource, path) in new[] { (LogoAccess, logoPath), (DirectoryAccess, saveDirectory) })
        {
            try
            {
                resource.Commit(path);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
        if (errors.Count > 0)
            throw new AggregateException("Access to the selected paths could not be remembered for the next launch.", errors);
#endif
    }

    public static void DiscardUnsavedAccess()
    {
#if IOS || MACCATALYST
        LogoAccess.Discard();
        DirectoryAccess.Discard();
#endif
    }

#if IOS || MACCATALYST
    private static readonly SavedResourceAccess LogoAccess = new("Settings.LogoBookmark");
    private static readonly SavedResourceAccess DirectoryAccess = new("Settings.SaveDirectoryBookmark");

    private static async Task<string?> PickAppleAsync(UTType[] types, SavedResourceAccess resource)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var picker = new UIDocumentPickerViewController(types, asCopy: false)
        {
            AllowsMultipleSelection = false,
            ModalPresentationStyle = UIModalPresentationStyle.FormSheet
        };
        void Picked(object? sender, UIDocumentPickedAtUrlsEventArgs args)
        {
            try
            {
                var url = args.Urls.FirstOrDefault();
                completion.TrySetResult(url is null ? null : resource.Select(url));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
        void Cancelled(object? sender, EventArgs args) => completion.TrySetResult(null);
        using var dismissal = new PickerDismissalDelegate(() => completion.TrySetResult(null));
        picker.DidPickDocumentAtUrls += Picked;
        picker.WasCancelled += Cancelled;
        try
        {
            if (picker.PresentationController is { } presentation)
                presentation.Delegate = dismissal;
            var controller = Platform.GetCurrentUIViewController()
                ?? throw new InvalidOperationException("No window is available to show the file browser.");
            controller.PresentViewController(picker, true, null);
            return await completion.Task;
        }
        finally
        {
            picker.DidPickDocumentAtUrls -= Picked;
            picker.WasCancelled -= Cancelled;
        }
    }

    private sealed class PickerDismissalDelegate(Action dismissed) : UIAdaptivePresentationControllerDelegate
    {
        public override void DidDismiss(UIPresentationController presentationController) => dismissed();
    }

    private sealed class ResourceAccess : IDisposable
    {
        private readonly NSUrl _url;
        private readonly bool _started;
        private bool _disposed;

        public string Path { get; }
        public string Bookmark { get; }

        public ResourceAccess(NSUrl url)
        {
            _url = url;
            Path = url.Path ?? throw new IOException("The selected item has no local path.");
            _started = url.StartAccessingSecurityScopedResource();
            try
            {
#if MACCATALYST
                // NSURL.h explicitly supports this flag on Mac Catalyst 13+;
                // the .NET enum metadata incorrectly advertises macOS only.
#pragma warning disable CA1416
                const NSUrlBookmarkCreationOptions options = NSUrlBookmarkCreationOptions.WithSecurityScope;
#pragma warning restore CA1416
#else
                const NSUrlBookmarkCreationOptions options = NSUrlBookmarkCreationOptions.MinimalBookmark;
#endif
                using var data = url.CreateBookmarkData(options, null, null, out var error);
                using (error)
                {
                    if (data is null || error is not null)
                        throw new IOException(error?.LocalizedDescription ?? "Access to the selected item could not be saved.");
                    Bookmark = Convert.ToBase64String(data.ToArray());
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_started)
                _url.StopAccessingSecurityScopedResource();
            // Native pickers can return the same managed URL wrapper for repeated
            // selections. Let its finalizer release it after all scopes drop it.
        }
    }

    private sealed class SavedResourceAccess(string preferenceKey)
    {
        private ResourceAccess? _saved;
        private ResourceAccess? _draft;
        private bool _restored;

        public void Restore()
        {
            if (_restored)
                return;
            _restored = true;
            try
            {
                var bookmark = Preferences.Default.Get(preferenceKey, string.Empty);
                if (string.IsNullOrEmpty(bookmark))
                    return;
                using var data = NSData.FromArray(Convert.FromBase64String(bookmark));
                var options = NSUrlBookmarkResolutionOptions.WithoutUI;
#if MACCATALYST
                // NSURL.h marks this flag available on Mac Catalyst 13+.
#pragma warning disable CA1416
                options |= NSUrlBookmarkResolutionOptions.WithSecurityScope;
#pragma warning restore CA1416
#endif
                var url = NSUrl.FromBookmarkData(data, options, null, out var stale, out var error);
                using (error)
                {
                    if (url is null || error is not null)
                    {
                        url?.Dispose();
                        return;
                    }
                }
                _saved = new ResourceAccess(url);
                if (stale)
                    Preferences.Default.Set(preferenceKey, _saved.Bookmark);
            }
            catch (Exception exception)
            {
                // Deleted files or unavailable providers must not prevent the app opening.
                System.Diagnostics.Debug.WriteLine($"Unable to restore {preferenceKey}: {exception.Message}");
            }
        }

        public string Select(NSUrl url)
        {
            var selected = new ResourceAccess(url);
            _draft?.Dispose();
            _draft = selected;
            return selected.Path;
        }

        public void Commit(string? path)
        {
            var selected = _draft?.Path == path ? _draft : _saved?.Path == path ? _saved : null;
            try
            {
                if (selected is null)
                    Preferences.Default.Remove(preferenceKey);
                else
                    Preferences.Default.Set(preferenceKey, selected.Bookmark);
            }
            finally
            {
                // The settings file is already saved. Retain its selected paths even
                // if remembering access fails, so Cancel and a later retry are safe.
                if (!ReferenceEquals(_saved, selected))
                    _saved?.Dispose();
                if (!ReferenceEquals(_draft, selected))
                    _draft?.Dispose();
                _saved = selected;
                _draft = null;
            }
        }

        public void Discard()
        {
            _draft?.Dispose();
            _draft = null;
        }
    }
#endif
}
