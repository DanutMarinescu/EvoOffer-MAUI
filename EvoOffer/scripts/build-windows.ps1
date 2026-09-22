param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'Windows builds must run on Windows with the .NET 10 SDK and MAUI Windows workload installed.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The dotnet command was not found. Install the .NET 10 SDK and the MAUI Windows workload first.'
}

$projectPath = Join-Path $PSScriptRoot '..\EvoOffer\EvoOffer.csproj'
$buildArguments = @(
    'build', $projectPath,
    '-f', 'net10.0-windows10.0.19041.0',
    '-c', $Configuration,
    "-p:RuntimeIdentifierOverride=win-$Architecture"
)

& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Run) {
    # Build first: the desktop Run target can launch without compiling.
    & dotnet @buildArguments '-t:Run' '--no-restore'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
