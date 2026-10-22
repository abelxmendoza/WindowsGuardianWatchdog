# Build a Windows installer package for Windows Guardian Watchdog.
# A package users and Windows will trust must be signed by a certificate that chains to a public CA.

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\dist\msix",
    [string]$CertificatePath,
    [string]$CertificatePassword = $env:WG_SIGNING_PASSWORD,
    [string]$CertificateThumbprint = $env:WG_SIGNING_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com",
    [string]$TrustedSigningDlib,
    [string]$TrustedSigningMetadata,
    [string]$Publisher,
    [switch]$AllowUnsigned,
    [switch]$AllowUntrustedCertificate
)

$ErrorActionPreference = "Stop"

function Find-WindowsSdkTool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $preferred = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }
    $sdkBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    foreach ($architecture in @($preferred, "x64", "arm64", "x86")) {
        $tool = Get-ChildItem $sdkBin -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+\.\d+' } |
            Sort-Object { [version]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName "$architecture\$Name" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($tool) { return $tool }
    }
    return $null
}

function Sign-Artifact {
    param([Parameter(Mandatory)][string]$Path)

    $arguments = @(
        "sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $TimestampUrl,
        "/d", "Windows Guardian Watchdog"
    )
    if ($TrustedSigningDlib) {
        if (-not $TrustedSigningMetadata) { throw "Trusted Signing requires -TrustedSigningMetadata." }
        $arguments += @("/dlib", $TrustedSigningDlib, "/dmdf", $TrustedSigningMetadata)
    }
    elseif ($CertificateThumbprint) {
        $arguments += @("/sha1", $CertificateThumbprint)
    }
    else {
        $arguments += @("/f", $CertificatePath)
        if ($CertificatePassword) { $arguments += @("/p", $CertificatePassword) }
    }
    $arguments += $Path
    & $signTool @arguments
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $Path" }

    if (-not $AllowUntrustedCertificate) {
        & $signTool verify /pa $Path
        if ($LASTEXITCODE -ne 0) {
            throw "Windows does not trust the signature on $Path. Use a public code-signing certificate or Microsoft Trusted Signing. A self-signed certificate is not enough."
        }
    }
}

Write-Host "Windows Guardian Watchdog - MSIX package" -ForegroundColor Cyan

$makeAppx = Find-WindowsSdkTool "MakeAppx.exe"
$signTool = Find-WindowsSdkTool "SignTool.exe"
if (-not $makeAppx -or -not $signTool) {
    throw "MakeAppx.exe and SignTool.exe are required. Install the Windows SDK."
}

$willSign = [bool]($CertificatePath -or $CertificateThumbprint -or $TrustedSigningDlib)
if (-not $willSign -and -not $AllowUnsigned) {
    throw "Refusing to build an installable package without a signature. Pass -CertificatePath, -CertificateThumbprint, or -TrustedSigningDlib. Use -AllowUnsigned only for a local test build."
}

$publisher = $null
if ($CertificatePath) {
    $CertificatePath = (Resolve-Path $CertificatePath).Path
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $CertificatePath,
        $CertificatePassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    if (-not $certificate.HasPrivateKey) { throw "The signing certificate does not contain a private key." }
    $publisher = $certificate.Subject
}
elseif ($CertificateThumbprint) {
    $found = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My |
        Where-Object { $_.Thumbprint -eq ($CertificateThumbprint -replace '\s', '') } |
        Select-Object -First 1
    if (-not $found) { throw "No certificate with thumbprint $CertificateThumbprint was found in the Current User or Local Machine store." }
    $publisher = $found.Subject
}
if ($Publisher) { $publisher = $Publisher }
if ($willSign -and -not $publisher) {
    throw "Pass -Publisher with the exact subject of the signing certificate, for example 'CN=Your Name, O=Your Company, C=US'."
}

Write-Host "Publishing the application..." -ForegroundColor Yellow
$publishPath = Join-Path $OutputPath "publish"
dotnet publish .\WindowsGuardianWatchdog.csproj -c $Configuration -r win-x64 --self-contained true -p:WindowsPackageType=None -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$packagePath = Join-Path $OutputPath "package"
if (Test-Path $packagePath) { Remove-Item $packagePath -Recurse -Force }
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
Copy-Item -Path "$publishPath\*" -Destination $packagePath -Recurse -Force
if (Test-Path "Resources") {
    New-Item -ItemType Directory -Path "$packagePath\Resources" -Force | Out-Null
    Copy-Item -Path "Resources\*" -Destination "$packagePath\Resources\" -Recurse -Force
}

@"
Windows Guardian Watchdog privacy notice
========================================

The app runs on this PC. Security checks, cleanup, hardening, and update scans stay local.
It does not create an account and it does not send usage telemetry.

App updates use Windows Package Manager (winget) and Windows Update. Those Windows features
contact Microsoft using your existing Windows settings. Hardening can turn on Defender cloud
protection, which lets Defender check file reputation with Microsoft. Sample upload is left off.

The app asks for administrator approval because firewall, Defender, and update changes require it.
Windows shows the publisher from the code-signing certificate before it grants that approval.
"@ | Set-Content -LiteralPath (Join-Path $packagePath "Privacy.txt") -Encoding utf8

Copy-Item -Path "Package.appxmanifest" -Destination (Join-Path $packagePath "AppxManifest.xml") -Force
if ($publisher) {
    $manifestPath = Join-Path $packagePath "AppxManifest.xml"
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    $manifest.Package.Identity.Publisher = $publisher
    $manifest.Save($manifestPath)
}

$executable = Join-Path $packagePath "WindowsGuardianWatchdog.exe"
if ($willSign) {
    Write-Host "Signing WindowsGuardianWatchdog.exe..." -ForegroundColor Yellow
    Sign-Artifact -Path $executable
}

$msixPath = Join-Path $OutputPath "WindowsGuardianWatchdog.msix"
Write-Host "Packing MSIX..." -ForegroundColor Yellow
& $makeAppx pack /d $packagePath /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "MSIX package creation failed." }

if ($willSign) {
    Write-Host "Signing and verifying the MSIX..." -ForegroundColor Yellow
    Sign-Artifact -Path $msixPath
    Write-Host "Windows trusts this signature." -ForegroundColor Green
}
else {
    Write-Host "Unsigned test package. Windows SmartScreen and Microsoft will not trust it." -ForegroundColor Yellow
}

@"
Install Windows Guardian Watchdog
==================================

1. Confirm the file matches SHA256SUMS.txt:
   certutil -hashfile WindowsGuardianWatchdog.msix SHA256

2. Double-click WindowsGuardianWatchdog.msix
   Windows shows the publisher from the signature. Continue only if that publisher is the one you expect.

3. Approve the administrator prompt when the app opens. That prompt is how Windows lets the app
   check Defender, the firewall, and Windows Update.

The package includes the .NET runtime, so a separate runtime install is not required.
Windows 11, or Windows 10 version 2004 or later with current updates, is required.
An elevated Windows App SDK app needs Windows 10 update KB5013942 or newer.

Remove it from Settings > Apps, or run:
  Get-AppxPackage WindowsGuardianWatchdog | Remove-AppxPackage
"@ | Set-Content -LiteralPath (Join-Path $OutputPath "INSTALL.txt") -Encoding utf8

Write-Host "Package: $msixPath" -ForegroundColor Cyan
