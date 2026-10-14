# Create the download users should receive: one signed MSIX, its checksum, and an optional updater.
# Windows trusts the result only when SignTool verify /pa succeeds.

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\dist\release",
    [string]$CertificatePath,
    [string]$CertificatePassword = $env:WG_SIGNING_PASSWORD,
    [string]$CertificateThumbprint = $env:WG_SIGNING_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com",
    [string]$TrustedSigningDlib,
    [string]$TrustedSigningMetadata,
    [string]$Publisher,
    [string]$DownloadBaseUrl,
    [switch]$AllowUnsigned
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$msixArguments = @{
    Configuration = $Configuration
    OutputPath = $OutputPath
    TimestampUrl = $TimestampUrl
}
if ($CertificatePath) { $msixArguments.CertificatePath = $CertificatePath }
if ($CertificatePassword) { $msixArguments.CertificatePassword = $CertificatePassword }
if ($CertificateThumbprint) { $msixArguments.CertificateThumbprint = $CertificateThumbprint }
if ($TrustedSigningDlib) { $msixArguments.TrustedSigningDlib = $TrustedSigningDlib }
if ($TrustedSigningMetadata) { $msixArguments.TrustedSigningMetadata = $TrustedSigningMetadata }
if ($Publisher) { $msixArguments.Publisher = $Publisher }
if ($AllowUnsigned) { $msixArguments.AllowUnsigned = $true }

& "$PSScriptRoot\CreateMSIXPackage.ps1" @msixArguments
$msixPath = (Resolve-Path (Join-Path $OutputPath "WindowsGuardianWatchdog.msix")).Path

$hash = (Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = Join-Path $OutputPath "SHA256SUMS.txt"
"$hash  $(Split-Path $msixPath -Leaf)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

$versionText = "1.0.0.0"
$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot "WindowsGuardianWatchdog.csproj") -Raw
if ($project -match '<Version>([^<]+)</Version>') {
    $parts = $Matches[1].Trim().Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    $versionText = ($parts | Select-Object -First 4) -join '.'
}

if ($DownloadBaseUrl) {
    $base = $DownloadBaseUrl.TrimEnd('/')
    $packageName = "WindowsGuardianWatchdog"
    $publisher = "CN=WindowsGuardianWatchdog"
    $manifestPath = Join-Path $OutputPath "package\AppxManifest.xml"
    if (Test-Path $manifestPath) {
        [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
        $packageName = $manifest.Package.Identity.Name
        $publisher = $manifest.Package.Identity.Publisher
        if ($manifest.Package.Identity.Version) { $versionText = $manifest.Package.Identity.Version }
    }

    $publisher = [System.Security.SecurityElement]::Escape($publisher)
    $packageName = [System.Security.SecurityElement]::Escape($packageName)
    $versionText = [System.Security.SecurityElement]::Escape($versionText)
    $msixUri = [System.Security.SecurityElement]::Escape("$base/WindowsGuardianWatchdog.msix")
    $installerUri = [System.Security.SecurityElement]::Escape("$base/WindowsGuardianWatchdog.appinstaller")

    @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
    Uri="$installerUri"
    Version="$versionText"
    xmlns="http://schemas.microsoft.com/appx/appinstaller/2018">
  <MainPackage
      Name="$packageName"
      Publisher="$publisher"
      Version="$versionText"
      ProcessorArchitecture="x64"
      Uri="$msixUri" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="24" />
  </UpdateSettings>
</AppInstaller>
"@ | Set-Content -LiteralPath (Join-Path $OutputPath "WindowsGuardianWatchdog.appinstaller") -Encoding utf8
}

Write-Host ""
Write-Host "Give users these files:" -ForegroundColor Green
Write-Host "  $msixPath"
Write-Host "  $checksumPath"
if ($DownloadBaseUrl) { Write-Host "  $(Join-Path $OutputPath 'WindowsGuardianWatchdog.appinstaller')" }
Write-Host "SHA256 $hash" -ForegroundColor Cyan
