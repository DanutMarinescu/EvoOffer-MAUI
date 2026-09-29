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

The Windows app always uses the light theme, including native controls, so
Windows dark mode does not recolour controls against the app's light backgrounds.
To check this, launch with Windows app mode set to Dark, open Settings and the
dropdowns, and focus the text fields. Switch between Light and Dark while the
app is open and confirm that the app's colours stay consistent and text remains
readable.

## Package for Windows

### Create a setup installer

On Windows, install the build tools described above and
[Inno Setup 6.3 or later](https://jrsoftware.org/isdl.php), then run from this
directory in Windows PowerShell 5.1 or PowerShell 7:

```powershell
.\scripts\create-windows-installer.ps1
```

The default installer and its SHA-256 checksum are written to:

```text
artifacts\windows\EvoOffer-1.0-win-x64-Setup.exe
artifacts\windows\EvoOffer-1.0-win-x64-Setup.exe.sha256
```

Share the setup `.exe` with users. It installs **Offer Generator** for the current
user, adds a Start menu shortcut, offers an optional desktop shortcut, and adds
an entry to Windows **Installed apps** for uninstalling. Administrator access is
not required. The .NET and Windows App SDK runtimes are bundled, so users do not
need the SDK or MAUI workloads. It supports Windows 10 version 1809 or later and
Windows 11, subject to the requirements of the bundled runtime versions.

Setup also includes Microsoft's Evergreen WebView2 bootstrapper for PDF previews.
An internet connection is needed during installation only when WebView2 is not
already installed. For offline PCs, install the matching
[Evergreen Standalone WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
first. The build downloads the bootstrapper and checks its Microsoft signature;
`-WebView2BootstrapperPath` accepts a previously downloaded copy instead.

The installer defaults to the project's `ApplicationDisplayVersion`. To build
for an ARM64 PC, set a release version, or choose an output folder:

```powershell
.\scripts\create-windows-installer.ps1 -Architecture arm64
.\scripts\create-windows-installer.ps1 -Version 1.1.0 -OutputDirectory .\artifacts\release
```

`-Version` also sets the published application's display version. Pass
`-InnoSetupPath 'C:\path\to\ISCC.exe'` if the compiler is not found automatically.
The script detects Inno Setup 7 (including 7.1) and 6 in their standard installation
folders, preferring 7, or uses `ISCC.exe` from `PATH` when available.
The script publishes into a fresh temporary folder and copies the entire payload
into setup, including PDF libraries, fonts, images, and translations.

Run a newer setup to upgrade an existing installation. Saved settings, imported
catalog data, and generated offers are preserved during upgrades and uninstall.
Setup blocks downgrades and requires uninstalling before changing between x64
and ARM64. The setup is unsigned by default; public distribution should sign the
app and installer with your code-signing identity. Windows may display an
unknown-publisher or SmartScreen prompt until signing and reputation are established.

The repository's **Windows installer** GitHub Actions workflow builds both
architectures and makes the installers and checksums available as downloadable
artifacts. Run it from **Actions → Windows installer → Run workflow** after these
files are committed and pushed. The workflow also checks the x64 installer with
a silent install, reinstall, and uninstall. A Windows PC is still needed to check
app startup, PDF preview, and installation on a clean machine without WebView2.
The final Windows executable cannot be built on macOS.

### Publish a portable folder

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

Both Apple targets register a MAUI scene delegate and scene manifest for the
UIKit scene lifecycle required by the version 27 SDK. The app keeps a single
window. The build also tracks changes to the source `Info.plist`, ensuring
incremental builds include updated startup configuration.

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

The **PDF template** dropdown offers **Classic** (the original letterhead),
**Modern** (a bold title band and product cards), and **Minimal** (a compact
letterhead and ruled table). Each also has a **No issuer name** variant with a
larger logo and the offer title in place of the issuer name, including on
continuation pages. These variants retain the issuer's contact details and work
without a logo. Selecting a template immediately updates a sample
image rendered from that layout. These bundled previews work on every platform.
On Windows, **Preview PDF with my details** opens a full PDF using sample
products and the unsaved colors, issuer, logo, language, message and VAT settings. It
works before adding any offer items and does not automatically save a PDF to the
offer destination. Close the preview to continue editing settings.

**Template colors** provides color pickers for **Primary color** (headings and
bars), **Secondary color** (supporting labels, rules and subtle panel tints), and
**Text color** (body copy). Choose a preset swatch, adjust the red/green/blue
sliders, or enter a three- or six-digit hex value. The same palette applies to
all six template choices. Text inside primary-colored bars automatically uses dark
or white lettering for contrast. The bundled layout images show the default
palette; the full PDF preview uses your current color choices.

Choose **Save settings** to use the selected template for subsequent offers;
**Cancel** preserves the previous template and colors. `PdfTemplateId`,
`PdfPrimaryColor`, `PdfSecondaryColor`, and `PdfTextColor` are stored in
`settings.json`. Missing or unrecognized template IDs fall back to Classic;
missing or invalid colors use the original navy, gray, and dark text defaults.
Invalid hex input is highlighted and must be corrected before previewing or saving.

Settings includes an issuer name followed by contact data: e-mail, phone number,
two address lines, and VAT number. These fields have visible outlines and shaded
backgrounds. Contact details are optional; a supplied e-mail address is validated
when leaving the field and before saving. Phone and VAT numbers accept digits
only (including pasted text), and preserve leading zeros.

The **Logo** section shows an optional image path. Its folder button opens an
image-only browser (PNG, JPEG, or WebP); **Remove logo** clears the selection.
The selected image appears in the generated PDF header. The **Save directory**
section shows the PDF destination and opens a folder-only browser. It defaults
to `Documents/EvoOffer` (or the app's local data folder if Documents is unavailable).
The destination is created when needed. Both selections are applied with
**Save settings**; canceling a browser or the settings screen preserves the
previous settings.

The **Data file** section selects a `.csv` product catalog. Choose a file, then
**Save settings** to confirm, validate, and import it. A success popup reports the
number of imported products. Invalid files leave the previous catalog and saved
settings intact, with an error explaining what needs fixing. Cancel discards the
selection. **Clear selection**, followed by **Save settings**, clears the catalog
dropdowns; products already added to the current offer remain in the offer.

The first CSV record must contain these three headers (column order may vary):

```csv
Denumire Produs,Pret,Categorie / Categorii
Stejar Natur,121,Parchet > Lemn masiv > Stejar | Promoții
Plintă albă,24.20,Accesorii > Plinte
```

At least one product is required. Names, prices, and category paths must be
present on every data row. `Pret` is a nonnegative VAT-inclusive price; the
catalog calculates the net price as `Pret / (1 + VAT / 100)` using the saved VAT
percentage. CSV quoting supports commas, escaped quotation marks, and newlines
inside fields. Semicolon-separated CSV is also accepted, as are decimal commas
in a quoted field or a semicolon-separated file. Do not use thousands separators.

Optional attribute columns identify product variants:

- Color: `Atribute: Culoare (variante de produs)` or `Atribute: Culoare (lista)`.
- Size: `Atribute: Cantitate (variante de produs)`, `Atribute: Cantitate_11 (lista)`,
  or `Atribute: Marime (variante de produs)`.

When those size fields are blank, the following dimension columns provide Size:
`Atribute: Lungime Mm (variante de produs)`, `Atribute: Latime Mm (variante de produs)`,
and `Atribute: Grosime Mm (variante de produs)`. They combine in length × width ×
thickness order, for example `500 mm x 70 mm x 22 mm`, regardless of CSV column order.
An existing size value takes precedence. Dimension columns and values are optional:
empty dimensions are skipped, so thickness alone shows `22 mm`, while length and
thickness show `500 mm x 22 mm`. If all three are absent, Size remains blank.
Each combined size keeps its row's color and price, including sizes that differ
only in length.

The color and size alias columns accept at most one distinct value per attribute
on each row; the dimension columns are combined as described above. Blank
attributes are allowed. Values are literal labels, including
sizes such as `13,12 kg`; quote these when using comma-separated CSV. Other
export columns are ignored. The app still imports CSV files; save an Excel
workbook as CSV before selecting it.

Rows with the same trimmed product name appear once in the Item dropdown.
Their category memberships are combined, and their size/color/price variants
remain in the read-only catalog. Exact duplicate variants are collapsed. In the
offer table, **Size** and **Color** always show all options for that product.
Changing one keeps the other if compatible; otherwise, the other switches to
the first valid combination in CSV order. The unit price, VAT, and totals update
from that combination. Missing attributes display `—` and a wholly absent
attribute has a disabled dropdown. Separate offer lines can choose different
variants of the same product.

If a product has different prices for the same size and color, its variants are
retained but it cannot be added to an offer. Add distinguishing attributes to
the CSV and reimport it to make its prices selectable without ambiguity.

`>` adds a subcategory and `|` separates category paths. For example,
`A > B > C > D | E > F | G` makes a product available under A, B, C, D, E, F,
and G. The category dropdown lists each hierarchy in order, with bold root
categories and indented descendants. Selecting a parent includes all its
descendants' products. A subcategory with the same name under different parents
is kept separate, and a product matching multiple paths appears only once.

The app starts with an empty offer and catalog until a CSV is selected. A saved
CSV is validated and loaded again at startup; unreadable or invalid saved files
produce an error and can be replaced from Settings. During use, the imported
catalog is an immutable in-memory snapshot. Select the file again with **Browse**
and save to import external edits. Changing VAT recalculates catalog net prices
from the original gross prices. Existing offer lines keep their net unit prices
and recalculate VAT, matching the existing offer behavior. Their variant options
and prices remain a snapshot of the catalog used when they were added, including
after a catalog replacement or clear.

A language dropdown offers **Română** (the default) and **English**, alongside the
default offer message and VAT percentage. Choose **Save settings** to persist all
values. Cancel discards edits.

The app stores these values, including `LogoPath` and `DataFilePath` (both nullable), and `SaveDirectory`, in `settings.json` in MAUI's
`FileSystem.Current.AppDataDirectory` and loads them on startup. On first launch,
it creates the file and imports any message and VAT values previously stored in
platform preferences. Subsequent launches use the config file. Failed saves keep
the settings page open and display an error; an unreadable config uses defaults
and displays a status message without overwriting the file.

The language selection controls the whole interface, including settings, dialogs,
validation messages, accessibility labels, and number formatting, as well as PDF
labels, dates, and the greeting and closing note. Romanian is the default, even
when the operating system uses another language. Saving the language updates the
open offer immediately, without restarting, and restores that choice on the next
launch. Canceling settings or a failed save keeps the previous language.

The built-in offer message follows the selected language. Edited messages and
issuer, client, and imported product data are preserved as entered. UI strings
live in `EvoOffer/Resources/Strings/AppResources.resx` (Romanian fallback) and
`AppResources.en.resx` (English).

## PDF generation

QuestPDF `2026.9.0` is isolated behind `IOfferPdfService` / `OfferPdfService` in
`EvoOffer/Services`. The service is registered with dependency injection in
`MauiProgram.cs` and passed through the app to the main page. It returns PDF bytes
or writes to a caller-owned stream without closing it.

On Windows, **Generate Offer** validates the offer, snapshots its items and saved
issuer/contact details and optional logo path, and generates the PDF in the background.
It saves a uniquely named PDF in the configured save directory, then opens
that actual document in an in-app PDF preview, replacing the former hand-built
table preview. The viewer provides page navigation, zoom, save, and print controls.
Generation errors keep the offer editable and display a message; viewer loading
errors provide a retry action. **Close preview** returns to the editor.

`OfferPdfPreviewFile` owns a uniquely named PDF in the app's cache directory under
`offer-previews`. It removes partial output after generation errors or
cancellation and attempts to delete the completed PDF when its viewer closes.
The permanent copy in the save directory remains available after closing the
preview or app. Saving uses a temporary file followed by a rename, so unfinished
copies are cleaned up and existing offers are never overwritten. Missing logo
images or inaccessible save folders are reported without losing the offer form.
The preview uses Windows' WebView2 PDF support and requires the WebView2 Runtime.
Its browser cache is configured in app data so installed copies also work from
read-only locations such as Program Files. See
[Microsoft's local PDF viewing documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content#loading-local-content-by-navigating-to-a-file-url).

**Platform support:** QuestPDF supports Windows, Linux, and native macOS .NET
hosts. It does **not** support iOS, Android, or Mac Catalyst (the app's macOS
target). The button checks `IOfferPdfService.IsSupported` and shows a PDF preview
unavailable message on these platforms, without invoking the renderer. On
unsupported platforms, direct service generation throws a clear `PlatformNotSupportedException`
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
    AccentColor = null, // Use saved primary color; set a Color to override.
    SecondaryColor = null, // Use saved secondary color.
    TextColor = null, // Use saved body text color.
    TemplateId = null, // Use the saved selection; or override with OfferPdfTemplates.Modern.
    Title = null, // Localized commercial-offer title, or your own title.
    Tagline = "", // Optional line beneath the company name.
    ValidUntil = null, // Optional DateOnly, supplied per offer when needed.
    FooterText = null, // Localized closing note; use "" to hide it.
    ShowPageNumbers = true
});
```

Lato is bundled with QuestPDF and includes Romanian characters. Register any
other font with QuestPDF's `FontManager` before using it. `OfferPdfTemplate.cs`
contains all three reusable layouts. Classic keeps the original A4 letterhead design: an optional logo to the left of the
issuer name, contact details with vector icons and vertical separators, an
underlined recipient, a greeting and custom message, and a six-column product
table. Selected sizes, colors, and product categories appear under their names. The navy table heading and
grand-total bar follow the reference design, with thin rules between items and
full-width subtotal and VAT rows.

The snapshot captures the selected template and colors, saved Romanian/English language and structured contact
details. Amounts use that language's formatting and always remain in RON. Totals
use the existing rounded model values, not amounts from the reference artwork.
A uniform VAT rate appears in the summary; mixed rates appear next to each
line's VAT amount. Long offers repeat the table heading and use a compact
company/client header on subsequent pages. Normal rows and the summary stay
together, while exceptionally long descriptions can continue onto another page.
The closing note appears once after the totals, followed by optional page numbers
at the bottom of each page.

No sample company, logo, tagline, or expiry date is inserted into real offers.
The `Tagline` and `ValidUntil` template options are optional configuration values,
not new settings-screen fields. Callers can supply an expiry date with a
per-document override such as `defaults with { ValidUntil = new DateOnly(2026, 10, 22) }`.

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

To also render Romanian, English, 150-item, and long-content template samples for
visual inspection, choose an output directory:

```sh
dotnet run --project EvoOffer.Tests -- --render-samples /tmp/evooffer-pdf-samples
```

Sample contact details and dates are fixtures only. The reference sample uses
consistent calculated VAT amounts rather than the inconsistent example totals
in the supplied image.

The sample command also produces `template-classic.pdf`, `template-modern.pdf`,
and `template-minimal.pdf`, plus their `-no-issuer-name.pdf` variants with a sample
logo, from the same renderer and sample-data helper used by Settings, plus
`palette-*.pdf` examples with custom colors and light primary
backgrounds. After changing a layout, refresh its bundled Settings image with
Poppler (repeat for the other layouts and their variants):

```sh
pdftoppm -f 1 -singlefile -scale-to 1000 -png /tmp/evooffer-pdf-samples/template-classic.pdf EvoOffer/Resources/Images/template_classic
pdftoppm -f 1 -singlefile -scale-to 1000 -png /tmp/evooffer-pdf-samples/template-classic-no-issuer-name.pdf EvoOffer/Resources/Images/template_classic_no_issuer_name
```
