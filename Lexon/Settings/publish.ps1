# Publishes Lexon as a folder of loose files for Velopack packaging.
#
# Velopack needs individual files on disk (not a bundled single-file exe) so it
# can diff files between versions and build delta packages. Do not add
# PublishSingleFile here — use publish-portable.ps1 for a one-file dev build.
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $root "publish"))
if (-not $OutputDir) {
    $OutputDir = $allowedRoot
}

if (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $root $OutputDir
}

$resolved = [System.IO.Path]::GetFullPath($OutputDir)
$separator = [System.IO.Path]::DirectorySeparatorChar
$allowedPrefix = $allowedRoot.TrimEnd('\', '/') + $separator
if ($resolved -ne $allowedRoot -and -not $resolved.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDir must be under $allowedRoot. Got $resolved"
}

$project = Join-Path $root "Lexon.Settings\Lexon.Settings.csproj"

Write-Host "Publishing Lexon ($Configuration, $Runtime) to $resolved"

if (Test-Path $resolved) {
    Remove-Item $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $resolved -Force | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:DebugType=none `
    -o $resolved

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Publish output: $resolved"
return $resolved
