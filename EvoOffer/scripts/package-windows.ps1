param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'Windows publishing must run on Windows with the .NET 10 SDK and MAUI Windows workload installed.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The dotnet command was not found. Install the .NET 10 SDK and the MAUI Windows workload first.'
}

$projectDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\EvoOffer'))
$projectPath = Join-Path $projectDirectory 'EvoOffer.csproj'
$framework = 'net10.0-windows10.0.19041.0'
$publishDirectory = Join-Path $projectDirectory "bin\Release\$framework\win-$Architecture\publish"

& dotnet publish $projectPath `
    -f $framework -c Release `
    "-p:RuntimeIdentifierOverride=win-$Architecture" `
    '-p:WindowsPackageType=None' `
    '-p:WindowsAppSDKSelfContained=true' `
    '-p:SelfContained=true' `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Published to $publishDirectory"
Write-Host 'Copy the entire publish folder to the destination Windows PC and run EvoOffer.exe.'
