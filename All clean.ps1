# package-for-review.ps1
#
# Creates a clean Lexon.zip for uploading to Claude, automatically
# excluding build output, release artifacts, and IDE junk that add
# size but no review value. Run this instead of zipping the folder
# directly, and the zip will stay small no matter how big bin/obj or
# the releases/ folder grow.
#
# Usage (from anywhere):
#   .\package-for-review.ps1
#   .\package-for-review.ps1 -SourcePath "C:\path\to\Lexon" -OutputZip "C:\path\to\Lexon.zip"

param(
    [string]$SourcePath = (Get-Location),
    [string]$OutputZip = (Join-Path (Get-Location) "Lexon.zip")
)

# Mirrors what .gitignore already flags as disposable, plus a couple
# of Velopack/IDE folders that live outside plain build output.
$exclude = @('bin', 'obj', 'releases', 'publish', '.vs', '.vpk', 'Run')

$stagingDir = Join-Path $env:TEMP "LexonZipStage_$(Get-Random)"
New-Item -ItemType Directory -Path $stagingDir | Out-Null

try {
    robocopy $SourcePath $stagingDir /E /XD $exclude /NFL /NDL /NJH /NJS /NC /NS | Out-Null

    if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }
    # -Force here makes Get-ChildItem include hidden items like .git —
    # the earlier wildcard form (`"$stagingDir\*"`) silently drops those.
    $items = Get-ChildItem -Path $stagingDir -Force
    Compress-Archive -Path $items.FullName -DestinationPath $OutputZip -Force

    $sizeMb = [math]::Round((Get-Item $OutputZip).Length / 1MB, 1)
    Write-Host "Created $OutputZip ($sizeMb MB)"
}
finally {
    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
