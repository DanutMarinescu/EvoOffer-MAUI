#requires -Version 5.1
<#
.SYNOPSIS
Publishes EvoOffer and builds a Windows setup EXE with Inno Setup.
.EXAMPLE
.\scripts\create-windows-installer.ps1 -Architecture x64
.EXAMPLE
.\scripts\create-windows-installer.ps1 -Architecture arm64 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [ValidatePattern('^\d+\.\d+(\.\d+){0,2}$')]
    [string]$Version,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\windows'),
    [string]$InnoSetupPath,
    # Optional previously downloaded Microsoft Evergreen bootstrapper for offline builds.
    [string]$WebView2BootstrapperPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') {
    throw 'Creating a Windows installer requires Windows, the .NET 10 SDK, the MAUI Windows workload, and Inno Setup 6.3 or later.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK and run: dotnet workload install maui-windows'
}

if (-not $InnoSetupPath) {
    $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compilerCommand) {
        $InnoSetupPath = $compilerCommand.Source
    } else {
        :findCompiler foreach ($majorVersion in @(7, 6)) {
            foreach ($programDirectory in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
                if ($programDirectory) {
                    $candidate = Join-Path $programDirectory "Inno Setup $majorVersion\ISCC.exe"
                    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                        $InnoSetupPath = $candidate
                        break findCompiler
                    }
                }
            }
        }
    }
}
if (-not $InnoSetupPath -or -not (Test-Path -LiteralPath $InnoSetupPath -PathType Leaf)) {
    throw 'Inno Setup was not found. Install Inno Setup 6.3 or later from https://jrsoftware.org/isdl.php, or pass -InnoSetupPath with the full path to ISCC.exe.'
}
$InnoSetupPath = (Resolve-Path -LiteralPath $InnoSetupPath).Path
$compilerFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($InnoSetupPath)
$compilerVersion = [version]::new($compilerFileVersion.FileMajorPart, $compilerFileVersion.FileMinorPart, $compilerFileVersion.FileBuildPart)
# Inno Setup 7 can have zeroed file metadata; query the compiler engine instead.
# Keep the metadata path for older compilers that do not support --version.
if ($compilerVersion.Major -eq 0) {
    $versionOutput = & $InnoSetupPath --version
    if ($LASTEXITCODE -ne 0 -or -not [version]::TryParse(($versionOutput -join "`n").Trim(), [ref]$compilerVersion)) {
        throw "Could not determine the Inno Setup version at '$InnoSetupPath'. Install Inno Setup 6.3 or later, or pass -InnoSetupPath with the full path to a supported ISCC.exe."
    }
}
if ($compilerVersion -lt [version]'6.3') {
    throw 'Inno Setup 6.3 or later is required for x64 and ARM64 architecture support.'
}
Write-Host "Using Inno Setup $compilerVersion at $InnoSetupPath"

$projectPath = Join-Path $PSScriptRoot '..\EvoOffer\EvoOffer.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
if (-not $Version) {
    $Version = $project.SelectSingleNode('/Project/PropertyGroup/ApplicationDisplayVersion').InnerText
}
if ($Version -notmatch '^\d+\.\d+(\.\d+){0,2}$' -or @($Version.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count -gt 0) {
    throw 'The installer version must have two to four numeric components, each between 0 and 65535 (for example 1.0 or 1.2.3).'
}
$appName = $project.SelectSingleNode('/Project/PropertyGroup/ApplicationTitle').InnerText
if ([string]::IsNullOrWhiteSpace($appName) -or $appName -match '["\r\n]') {
    throw 'ApplicationTitle must be a nonempty, single-line name without double quotes.'
}
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$outputBaseFilename = "EvoOffer-$Version-win-$Architecture-Setup"
$installerPath = Join-Path $OutputDirectory "$outputBaseFilename.exe"

# A new staging directory prevents obsolete files from earlier publishes entering setup.
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("EvoOffer-Installer-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
try {
    $publishDirectory = Join-Path $stagingDirectory 'publish'
    $compiledDirectory = Join-Path $stagingDirectory 'installer'
    $bootstrapper = Join-Path $stagingDirectory 'MicrosoftEdgeWebview2Setup.exe'
    if ($WebView2BootstrapperPath) {
        Copy-Item -LiteralPath $WebView2BootstrapperPath -Destination $bootstrapper
    } else {
        Write-Host 'Downloading the Microsoft WebView2 installer for PDF preview support...'
        # Windows PowerShell 5.1 may otherwise use TLS 1.0 on older build machines.
        $previousSecurityProtocol = [Net.ServicePointManager]::SecurityProtocol
        try {
            [Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapper
        } finally {
            [Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol
        }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw 'The WebView2 bootstrapper does not have a valid Microsoft signature. Installer creation stopped.'
    }

    & (Join-Path $PSScriptRoot 'package-windows.ps1') -Architecture $Architecture -Version $Version -OutputDirectory $publishDirectory
    foreach ($requiredFile in @('EvoOffer.exe', 'EvoOffer.dll', 'EvoOffer.deps.json', 'EvoOffer.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'Microsoft.UI.Xaml.dll', 'QuestPdfSkia.dll', 'qpdf.dll', 'QuestPDF.Fonts.Lato.br')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $requiredFile) -PathType Leaf)) {
            throw "The published app is missing $requiredFile. A complete self-contained publish is required."
        }
    }

    $compilerArguments = @(
        "/DPublishDir=$publishDirectory",
        "/DOutputDir=$compiledDirectory",
        "/DAppVersion=$Version",
        "/DAppName=$appName",
        "/DArchitecture=$Architecture",
        "/DOutputBaseFilename=$outputBaseFilename",
        "/DWebView2BootstrapperPath=$bootstrapper",
        (Join-Path $PSScriptRoot '..\installer\windows\EvoOffer.iss')
    )
    & $InnoSetupPath @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }
    $compiledInstaller = Join-Path $compiledDirectory "$outputBaseFilename.exe"
    if (-not (Test-Path -LiteralPath $compiledInstaller -PathType Leaf)) {
        throw 'Inno Setup completed without creating the expected installer.'
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Copy-Item -LiteralPath $compiledInstaller -Destination $installerPath -Force
    $hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$installerPath.sha256" -Encoding ascii -Value "$hash  $outputBaseFilename.exe"
    Write-Host "Installer: $installerPath"
    Write-Host "SHA-256:   $installerPath.sha256"
} finally {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
