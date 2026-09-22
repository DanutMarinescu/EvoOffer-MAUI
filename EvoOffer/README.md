# EvoOffer

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

## Regression checks

```sh
dotnet run --project EvoOffer.Tests
```
