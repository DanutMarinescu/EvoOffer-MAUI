param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [string]$LogDirectory = (Join-Path $PSScriptRoot '..\artifacts\windows\smoke-test')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') {
    throw 'The installer smoke test must run on Windows.'
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A4F3437-704B-4DA6-9B50-13AA50293F26}_is1'
$defaultDirectory = Join-Path $env:LOCALAPPDATA 'Programs\EvoOffer'
$shortcutDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'Offer Generator'
$shortcutPath = Join-Path $shortcutDirectory 'Offer Generator.lnk'

# This test owns a temporary install, but uses the real registration and shortcut.
# Refuse to change an existing installation on a developer's computer.
if ((Test-Path -LiteralPath $uninstallKey) -or
    (Test-Path -LiteralPath $defaultDirectory) -or
    (Test-Path -LiteralPath $shortcutDirectory) -or
    (Get-Process -Name EvoOffer -ErrorAction SilentlyContinue)) {
    throw 'An Offer Generator installation, shortcut, or process already exists. Run this test on a clean Windows account.'
}

$LogDirectory = [System.IO.Path]::GetFullPath($LogDirectory)
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('EvoOffer-InstallerSmoke-' + [guid]::NewGuid().ToString('N'))
$installDirectory = Join-Path $temporaryRoot 'app'
$uninstaller = Join-Path $installDirectory 'unins000.exe'
$userFile = Join-Path $installDirectory 'smoke-test-user-document.txt'

function Invoke-InstallerProcess {
    param([string]$FilePath, [string[]]$ProcessArguments)

    $process = Start-Process -FilePath $FilePath -ArgumentList $ProcessArguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Installer process returned $($process.ExitCode). See logs in $LogDirectory."
    }
}

function Assert-Installed {
    foreach ($name in @('EvoOffer.exe', 'EvoOffer.dll', 'EvoOffer.runtimeconfig.json', 'coreclr.dll', 'unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $name) -PathType Leaf)) {
            throw "The installed application is missing $name."
        }
    }
    if (-not (Get-ChildItem -LiteralPath $installDirectory -Filter 'Microsoft.UI.Xaml.dll' -File -Recurse)) {
        throw 'The installed application is missing the bundled Windows App SDK.'
    }
    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
        throw 'The Start Menu shortcut was not created.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath -ne (Join-Path $installDirectory 'EvoOffer.exe')) {
        throw "The Start Menu shortcut points to the wrong application: $($shortcut.TargetPath)"
    }
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null

    $registration = Get-ItemProperty -LiteralPath $uninstallKey
    if ($registration.InstallLocation.TrimEnd('\') -ne $installDirectory.TrimEnd('\')) {
        throw 'The Windows uninstall registration points to the wrong directory.'
    }
}

try {
    foreach ($phase in @('install', 'reinstall')) {
        $log = Join-Path $LogDirectory "$phase.log"
        Invoke-InstallerProcess -FilePath $installer -ProcessArguments @(
            '/SP-', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
            '/RESTARTEXITCODE=3010', '/TASKS=""',
            "/DIR=`"$installDirectory`"", "/LOG=`"$log`""
        )
        Assert-Installed
        if ($phase -eq 'install') {
            [System.IO.File]::WriteAllText($userFile, 'This user-created document must survive reinstall and uninstall.')
        } elseif (-not (Test-Path -LiteralPath $userFile)) {
            throw 'Reinstall removed a user-created document.'
        }
    }

    $log = Join-Path $LogDirectory 'uninstall.log'
    Invoke-InstallerProcess -FilePath $uninstaller -ProcessArguments @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`""
    )
    foreach ($path in @((Join-Path $installDirectory 'EvoOffer.exe'), $shortcutPath, $uninstallKey)) {
        if (Test-Path -LiteralPath $path) {
            throw "Uninstall left application state behind: $path"
        }
    }
    if (-not (Test-Path -LiteralPath $userFile)) {
        throw 'Uninstall removed a user-created document.'
    }
    Write-Host 'Installer smoke test passed: install, reinstall, shortcut, registration, bundled runtime, and uninstall.'
} finally {
    # Attempt normal uninstall after a failed assertion, retaining logs for diagnosis.
    if (Test-Path -LiteralPath $uninstaller) {
        $cleanupLog = Join-Path $LogDirectory 'cleanup.log'
        $cleanup = Start-Process -FilePath $uninstaller -ArgumentList @(
            '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$cleanupLog`""
        ) -Wait -PassThru
        if ($cleanup.ExitCode -ne 0) {
            Write-Warning "Cleanup returned $($cleanup.ExitCode). Temporary files remain at $temporaryRoot."
        }
    }
    if (-not (Test-Path -LiteralPath $uninstallKey) -and (Test-Path -LiteralPath $temporaryRoot)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
