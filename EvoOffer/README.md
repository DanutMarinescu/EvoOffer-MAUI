# EvoOffer

## Build and run on Windows

Requires Windows, the .NET 10 SDK, the MAUI Windows workload, and a compatible
Windows SDK (10.0.19041.0 or later). You can install the tools through a compatible
Visual Studio release with the **.NET Multi-platform App UI development** workload.
For command-line development, install the .NET 10 SDK and Windows SDK, then run:

```powershell
dotnet workload install maui-windows
```

Windows builds target `net10.0-windows10.0.19041.0` only; they do not require the
Apple workloads or the Xcode preview used on macOS. From this directory, use
Windows PowerShell 5.1 or PowerShell 7:

```powershell
.\scripts\build-windows.ps1
.\scripts\build-windows.ps1 -Run
```

The defaults are Debug and x64. Pass `-Configuration Release` for a release build
or `-Architecture arm64` for a Windows ARM64 PC. In Visual Studio, select the
**EvoOffer** startup project and the **Windows Machine** target to build and run.

## Package for Windows

From PowerShell on Windows, run:

```powershell
.\scripts\package-windows.ps1
```

This publishes an unpackaged Release app with the .NET and Windows App SDK
runtimes included. The default output is:

```text
EvoOffer\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\
```

Run `EvoOffer.exe` from that folder, or copy the **entire folder** to another
Windows PC. For ARM64, use `-Architecture arm64`; the output then uses
`win-arm64` in place of `win-x64`. This folder deployment does not require an MSIX
package or a signing certificate. See
[Microsoft's unpackaged Windows publishing guide](https://learn.microsoft.com/en-us/dotnet/maui/windows/deployment/publish-unpackaged-cli?view=net-maui-10.0).

## Build on macOS

Requires .NET 10, the MAUI workload with Apple SDK `27.0.10539-xcode27.0`
(or a compatible newer release), and Xcode 27. The project explicitly targets
`net10.0-ios27.0` and `net10.0-maccatalyst27.0` so .NET selects the matching SDK.
The current .NET support for Xcode 27 is a preview; see the
[Microsoft release notes](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode27.0-P2-10539).
The minimum supported iOS version remains 15.0. The Xcode 27 Mac Catalyst SDK
requires Catalyst 17.0, so the Mac app now requires macOS 14 or later.

From this directory, run:

```sh
bash scripts/build-mac.sh
```

The project honors `XcodeLocation`, `DEVELOPER_DIR`, existing Apple toolchain
preferences, or a full Xcode selected by `xcode-select`. If the system points to
standalone Command Line Tools and no explicit toolchain is configured, it uses
`/Applications/Xcode.app`. This applies to IDE builds and direct publishing too.
It does not change the system's Xcode selection or disable SDK compatibility checks.
Extra build arguments can be passed to the launcher, for example `-c Release`.

Build and publish output goes to `~/Library/Caches/EvoOffer/build`, outside the Documents
folder. This prevents file-provider Finder metadata from breaking code signing.
Set `EVOOFFER_BUILD_ROOT` to an absolute local output directory, or pass
`--artifacts-path`, to override this location. For the
default Debug build, the app is under
`bin/EvoOffer/debug_net10.0-maccatalyst27.0/Offer Generator.app`.

For a custom Xcode installation:

```sh
DEVELOPER_DIR=/Applications/Xcode-27.app/Contents/Developer bash scripts/build-mac.sh
```

In an IDE, select Xcode 27 in its Apple toolchain settings and use the
`net10.0-maccatalyst27.0` target.

## Package for macOS

From this directory, run:

```sh
bash scripts/package-mac.sh
```

This publishes a Release installer for both Apple Silicon and Intel Macs. With
the default output directory and version, the installer is:

```text
~/Library/Caches/EvoOffer/build/publish/EvoOffer/release_net10.0-maccatalyst27.0/EvoOffer-1.0.pkg
```

The corresponding `Offer Generator.app` is under
`~/Library/Caches/EvoOffer/build/bin/EvoOffer/release_net10.0-maccatalyst27.0/`.
`build-mac.sh -c Release` only builds the app; use `package-mac.sh` for the installer.
For an Apple Silicon-only installer, append `-r maccatalyst-arm64` (its output
folder has an additional `_maccatalyst-arm64` suffix).

The default installer is unsigned, and the app has an ad-hoc signature for local
testing. Public distribution requires your Apple signing identities and
notarization; see [Microsoft's distribution guide](https://learn.microsoft.com/en-us/dotnet/maui/mac-catalyst/deployment/publish-outside-app-store?view=net-maui-10.0).
Pass signing properties to `package-mac.sh` when those are configured.

## Settings

Settings includes an issuer name followed by contact data: e-mail, phone number,
two address lines, and VAT number. These fields have visible outlines and shaded
backgrounds. Contact details are optional; a supplied e-mail address is validated
when leaving the field and before saving. Phone and VAT numbers accept digits
only (including pasted text), and preserve leading zeros.

A language dropdown offers **Română** (the default) and **English**, alongside the
default offer message and VAT percentage. Choose **Save settings** to persist all
values. Cancel discards edits.

The app stores these values in `settings.json` in MAUI's
`FileSystem.Current.AppDataDirectory` and loads them on startup. On first launch,
it creates the file and imports any message and VAT values previously stored in
platform preferences. Subsequent launches use the config file. Failed saves keep
the settings page open and display an error; an unreadable config uses defaults
and displays a status message without overwriting the file.

The language selection is saved as a preference; interface and offer translation
are not implemented by this setting.

## PDF generation

QuestPDF `2026.9.0` is isolated behind `IOfferPdfService` / `OfferPdfService` in
`EvoOffer/Services`. The service is registered with dependency injection in
`MauiProgram.cs`. It returns PDF bytes or writes to a caller-owned stream without
closing it. The existing **Generate offer** action still opens the on-screen
preview; PDF export has not been added to that screen.

**Platform support:** QuestPDF supports Windows, Linux, and native macOS .NET
hosts. It does **not** support iOS, Android, or Mac Catalyst (the app's macOS
target). Check `IOfferPdfService.IsSupported` before offering PDF export. On
unsupported platforms, generation throws a clear `PlatformNotSupportedException`
before loading the native renderer. Apple clients need a backend or a different
renderer to export PDFs. See [QuestPDF's MAUI guidance](https://github.com/QuestPDF/QuestPDF/discussions/925).

The Windows startup configuration uses the **Evaluation** license. Select the
appropriate license in `MauiProgram.cs` before production use; see
[QuestPDF license configuration](https://www.questpdf.com/license/configuration.html).
Standalone hosts must also set `QuestPDF.Settings.License` once at startup. Do
not access that setting on unsupported platforms: it initializes native code.

Configure shared document defaults where `OfferPdfOptions` is registered:

```csharp
builder.Services.AddSingleton(new OfferPdfOptions
{
    PageSize = QuestPDF.Helpers.PageSizes.A4,
    Margin = 30, // Points: 72 points = 1 inch.
    FontFamily = "Lato",
    FontSize = 10,
    AccentColor = QuestPDF.Infrastructure.Color.FromHex("#147EF0"),
    Title = "Offer",
    FooterText = "Thank you for your business.",
    ShowPageNumbers = true
});
```

Lato is bundled with QuestPDF and includes Romanian characters. Register any
other font with QuestPDF's `FontManager` before using it. The layout methods in
`OfferPdfService.cs` own the issuer/contact block, client, message, repeating
table headers, totals, and footer. Labels are English and amounts are RON,
matching the current preview.

Given an injected `IOfferPdfService pdfService`, create a snapshot on the UI
thread, then generate from that snapshot (large offers can run on a worker):

```csharp
var offer = new OfferPdfData(
    viewModel.ClientName, viewModel.CustomText, viewModel.Items, settings);

if (pdfService.IsSupported)
{
    byte[] pdf = await Task.Run(() => pdfService.Generate(offer));
    await File.WriteAllBytesAsync(outputPath, pdf);
}
```

The snapshot copies contact details and the model's already-rounded line totals,
so later edits cannot change the PDF. A blank client, empty offer, or invalid
quantity is rejected. Pass an `OfferPdfOptions` instance to `Generate` to replace
the defaults for one document, for example using `defaults with { PageSize =
QuestPDF.Helpers.PageSizes.A4.Landscape() }`. Invalid dimensions, margins, and
font sizes are rejected before rendering. Layouts too large for the chosen page
are reported through QuestPDF's layout exceptions.

## Regression checks

```sh
dotnet run --project EvoOffer.Tests
```
