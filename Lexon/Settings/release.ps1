# Builds, packs, and uploads a Lexon release with Velopack.
#
# One-time setup:
#   dotnet tool install -g vpk
#   $env:GITHUB_TOKEN = "<a token with write access to the releases repo>"
#
# Usage:
#   .\release.ps1                 # pack only, review .\releases first
#   .\release.ps1 -Upload         # pack, then publish the GitHub release
#
# The version comes from <Version> in Lexon.Settings\Lexon.Settings.csproj.
# Bump it there and nowhere else.
#
# Every run first downloads the current release so vpk can emit a delta package
# alongside the full one. The very first release has nothing to download; that is
# expected and the run continues with a full package only.
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$RepoUrl = "https://github.com/reidwilliam723-glitch/Lexon",
    [switch]$Upload,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Lexon.Settings\Lexon.Settings.csproj"
$publishDir = Join-Path $root "publish"
$releaseDir = Join-Path $root "releases"

function Get-LexonVersion {
    param([string]$ProjectPath)

    $xml = [xml](Get-Content -Path $ProjectPath -Raw)
    $version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
    if (-not $version) {
        throw "No <Version> element found in $ProjectPath. Add one, e.g. <Version>1.0.0</Version>."
    }

    return ([string]$version).Trim()
}

function Get-AutoChangelog {
    param([string]$Version)

    $previousTag = git tag --list "v*" --sort=-v:refname | Select-Object -First 1

    if ($previousTag) {
        $range = "$previousTag..HEAD"
        Write-Host "Generating changelog from $range"
    }
    else {
        $range = "HEAD"
        Write-Host "No previous tag found; changelog will cover the full history."
    }

    # --no-merges keeps this to the actual commits, not merge-commit noise.
    # %s is the subject line only — full messages would be too noisy for a
    # release body, and multi-line commit bodies don't reformat cleanly
    # into a flat bullet list anyway.
    $subjects = git log $range --no-merges --pretty=format:"%s"

    if (-not $subjects) {
        return "Lexon $Version.`n`nNo changes recorded since the previous release."
    }

    $bullets = ($subjects -split "`n" | Where-Object { $_ } | ForEach-Object { "- $_" }) -join "`n"
    return "## Lexon $Version`n`n$bullets"
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "The 'vpk' CLI was not found. Install it with: dotnet tool install -g vpk"
}

$version = Get-LexonVersion -ProjectPath $project
Write-Host "Lexon release version $version (from Lexon.Settings.csproj)"
$notesFile = Join-Path $releaseDir "release-notes.md"

& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration -Runtime $Runtime -OutputDir $publishDir | Out-Null

# Start from an empty output directory. It is scratch space this script rebuilds
# every run, and leftovers caused two problems: the delta check below counted them
# as a "previous release" even when GitHub had none, and vpk pack stopped to ask
# about overwriting a stale package of the same version.
#
# The contents are emptied rather than the directory removed, because this script
# tells you to review the folder between runs and having it open in Explorer or a
# terminal holds a handle on the directory itself, failing every later run.
if (Test-Path $releaseDir) {
    try {
        Remove-Item -Path (Join-Path $releaseDir "*") -Recurse -Force
    }
    catch {
        throw "Could not clear $releaseDir. Close anything still using files in it, such as a previously built Setup.exe, and run again. $($_.Exception.Message)"
    }
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

# vpk can only diff against a previous release that is already sitting in the
# output directory, so fetch the current one before packing. Without this every
# release ships as a full package and testers re-download the whole app.
Write-Host "Fetching previous release for delta generation"
$downloadArgs = @(
    "download", "github",
    "--repoUrl", $RepoUrl,
    "--outputDir", $releaseDir
)
if ($env:GITHUB_TOKEN) {
    $downloadArgs += @("--token", $env:GITHUB_TOKEN)
}

# A repo with no releases yet is the expected first-run case, so this step must
# never abort the run. Both ways PowerShell could turn it into a terminating error
# are suppressed: $ErrorActionPreference is Stop above, and 7.4+ promotes native
# command failures on its own unless $PSNativeCommandUseErrorActionPreference is off.
try {
    $ErrorActionPreference = "Continue"
    $PSNativeCommandUseErrorActionPreference = $false

    vpk @downloadArgs
}
catch {
    Write-Host "Could not fetch the previous release: $($_.Exception.Message)"
}
finally {
    $ErrorActionPreference = "Stop"
}

# vpk warns and still exits 0 when it finds nothing to download, so decide whether
# a delta is possible from what actually landed on disk rather than the exit code.
# The directory was emptied above, so anything here now came from GitHub.
$downloaded = @(Get-ChildItem -Path $releaseDir -Filter "*-full.nupkg" -ErrorAction SilentlyContinue)

# A download of this same version cannot be diffed against, so it does not count
# as something to build a delta from. That happens when re-packing a version that
# is already published.
$earlier = @($downloaded | Where-Object { $_.Name -notlike "*-$version-full.nupkg" })

if ($earlier.Count -gt 0) {
    Write-Host "Downloaded $($earlier.Count) earlier release(s) from GitHub. vpk pack will generate a delta."
}
elseif ($downloaded.Count -gt 0) {
    Write-Host "GitHub's newest release is already $version, so there is nothing earlier to diff against. Packing a full release only."
}
else {
    Write-Host "GitHub has no previous release to diff against. Packing a full release only."
}

(Get-AutoChangelog -Version $version) | Set-Content -Path $notesFile -Encoding utf8
Write-Host "Wrote changelog to $notesFile"

Write-Host "Packing with vpk"
$packArgs = @(
    "pack",
    "--packId", "Lexon",
    "--packVersion", $version,
    "--packTitle", "Lexon",
    "--packAuthors", "Lexon",
    "--packDir", $publishDir,
    "--mainExe", "Lexon.Settings.exe",
    "--outputDir", $releaseDir,
    # Never stop for a keypress. Clearing the output directory above removes the
    # usual cause, but re-packing a version already published to GitHub pulls that
    # version down and would prompt again. Deliberately not passed to the upload
    # step below, where auto-confirming could overwrite a published release.
    "--yes"
)

vpk @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

Write-Host "Packed into $releaseDir"

if (-not $Upload) {
    Write-Host "Skipping upload. Re-run with -Upload to publish the GitHub release."
    return
}

if (-not $env:GITHUB_TOKEN) {
    throw "GITHUB_TOKEN is not set. Set it to a token with write access to $RepoUrl."
}

Write-Host "Uploading release $version to $RepoUrl"
$uploadArgs = @(
    "upload", "github",
    "--repoUrl", $RepoUrl,
    "--token", $env:GITHUB_TOKEN,
    "--outputDir", $releaseDir,
    "--tag", "v$version",
    "--releaseName", "Lexon $version",
    "--publish"
)

if ($Prerelease) {
    $uploadArgs += "--pre"
}

vpk @uploadArgs
if ($LASTEXITCODE -ne 0) {
    throw "vpk upload failed with exit code $LASTEXITCODE."
}

if (Get-Command gh -ErrorAction SilentlyContinue) {
    # gh has its own auth; reuse the token already required above rather
    # than making the caller set up a second credential.
    if (-not $env:GH_TOKEN -and $env:GITHUB_TOKEN) {
        $env:GH_TOKEN = $env:GITHUB_TOKEN
    }

    $repoSlug = ($RepoUrl -replace '^https://github.com/', '')
    gh release edit "v$version" --repo $repoSlug --title "Lexon $version" --notes-file $notesFile

    if ($LASTEXITCODE -ne 0) {
        Write-Host "gh release edit failed (exit $LASTEXITCODE). The release was still published by vpk; set its notes manually from $notesFile."
    }
    else {
        Write-Host "Set release notes from $notesFile"
    }
}
else {
    Write-Host "GitHub CLI ('gh') not found, so release notes were not set automatically. Install it from https://cli.github.com, or edit the release manually using the text in $notesFile."
}

Write-Host "Released Lexon $version. Testers install from Setup.exe on the release page."
