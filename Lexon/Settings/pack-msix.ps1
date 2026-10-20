# Builds a full-trust Lexon MSIX for Partner Center upload (or optional sideload).
#
# Partner Center → Product identity supplies:
#   -PackageName          Package/Identity/Name
#   -Publisher            Package/Identity/Publisher  (CN=<GUID>)
#   -PublisherDisplayName Publisher display name
#
# Usage:
#   .\pack-msix.ps1 -PackageName "..." -Publisher "CN=..." -PublisherDisplayName "..."
#   .\pack-msix.ps1 ... -OutputPath "$env:USERPROFILE\Desktop\Lexon.msix"
#   .\pack-msix.ps1 ... -SelfSign   # local sideload only; Store upload stays unsigned
#
# Store mode (-Store, default) refuses the placeholder publisher CN=WGR.
param(
    [string]$PackageName = "WGR.Lexon",
    [string]$Publisher = "CN=WGR",
    [string]$PublisherDisplayName = "WGR",
    [string]$DisplayName = "Lexon",
    [string]$OutputPath = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$StoreMode = $true,
    [switch]$SelfSign
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Lexon.Settings\Lexon.Settings.csproj"
$pkgDir = Join-Path $root "Lexon.Package"
$publish = Join-Path $root "msix-staging"
$makeAppx = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools\10.0.22621.1\bin\10.0.22621.0\x64\makeappx.exe"

function Get-LexonVersion {
    [xml]$csproj = Get-Content $project
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $version) { throw "Could not read <Version> from $project" }
    return $version.Trim()
}

function Assert-StoreIdentity {
    param([string]$PublisherValue, [bool]$StoreMode)
    if (-not $StoreMode) { return }
    if ([string]::IsNullOrWhiteSpace($PublisherValue) -or $PublisherValue.Trim() -eq "CN=WGR") {
        throw "Store mode requires the Partner Center publisher identity (CN=<GUID>), not CN=WGR."
    }
}

Assert-StoreIdentity -PublisherValue $Publisher -StoreMode:$StoreMode

$version = Get-LexonVersion
$packageVersion = "$version.0"
if ($OutputPath -eq "") {
    $safeName = $PackageName -replace '[^\w\.-]', '_'
    $verFile = $packageVersion -replace '\.', '_'
    $OutputPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "${safeName}_${verFile}_x64.msix"
}

if (-not (Test-Path $makeAppx)) {
    Write-Host "Restoring Windows SDK BuildTools for MakeAppx..."
    $tmp = Join-Path $env:TEMP "lexon-makeappx-restore"
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    Set-Location $tmp
    dotnet new classlib -n SdkTools -f net8.0 --force | Out-Null
    Set-Location (Join-Path $tmp "SdkTools")
    dotnet add package Microsoft.Windows.SDK.BuildTools --version 10.0.22621.1 | Out-Null
    if (-not (Test-Path $makeAppx)) {
        throw "MakeAppx not found at $makeAppx. Install Microsoft.Windows.SDK.BuildTools."
    }
}

Write-Host "Lexon MSIX $packageVersion (Identity Name=$PackageName Publisher=$Publisher)"
Assert-StoreIdentity -PublisherValue $Publisher -StoreMode:$StoreMode

if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
New-Item -ItemType Directory -Force -Path $publish | Out-Null

$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE ".nuget\packages"
dotnet publish $project -c $Configuration -r $Runtime --self-contained true -o $publish `
    /p:PublishSingleFile=false /p:DebugType=None /p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Get-ChildItem $publish -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force

$manifestPath = Join-Path $publish "AppxManifest.xml"
$template = Get-Content (Join-Path $pkgDir "AppxManifest.xml") -Raw
$template = $template -replace 'Name="WGR\.Lexon"', "Name=`"$PackageName`""
$template = $template -replace 'Publisher="CN=WGR"', "Publisher=`"$Publisher`""
$template = $template -replace 'Version="1\.\d+\.\d+\.0"', "Version=`"$packageVersion`""
$template = $template -replace '<DisplayName>Lexon</DisplayName>', "<DisplayName>$DisplayName</DisplayName>"
$template = $template -replace '<PublisherDisplayName>WGR</PublisherDisplayName>', "<PublisherDisplayName>$PublisherDisplayName</PublisherDisplayName>"
# Drop any leftover placeholder comment if present.
$template = $template -replace '(?s)\s*<!-- Publisher must be replaced.*?-->\s*', "`n  "
Set-Content -Path $manifestPath -Value $template -Encoding UTF8

$assetsDest = Join-Path $publish "Assets"
New-Item -ItemType Directory -Force -Path $assetsDest | Out-Null
Copy-Item (Join-Path $pkgDir "Assets\*.png") $assetsDest -Force

& $makeAppx pack /d $publish /p $OutputPath /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed" }

if ($SelfSign) {
    $certSubject = $Publisher
    Write-Host "Self-signing for sideload with subject $certSubject (not for Store upload)."
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $certSubject `
            -KeyUsage DigitalSignature -FriendlyName "Lexon sideload" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    }
    $signtool = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\signtool.exe" `
        -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $signtool) { throw "signtool.exe not found for -SelfSign" }
    & $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $OutputPath
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
}

Write-Host "Packed $OutputPath"
Write-Host "Store upload: leave unsigned (omit -SelfSign). Partner Center signs the package."
