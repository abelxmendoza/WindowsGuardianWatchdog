# Create MSIX Package for Windows Guardian Watchdog
# This creates a modern Windows app package that can be installed via Microsoft Store or sideloading

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\dist\msix",

    [Parameter(Mandatory=$false)]
    [string]$CertificatePath,

    [Parameter(Mandatory=$false)]
    [string]$CertificatePassword,

    [Parameter(Mandatory=$false)]
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Guardian Watchdog - MSIX Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

function Find-WindowsSdkTool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $sdkBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    $tool = Get-ChildItem $sdkBin -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$Name" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
    return $tool
}

$makeAppx = Find-WindowsSdkTool "MakeAppx.exe"
$signTool = Find-WindowsSdkTool "SignTool.exe"
if (-not $makeAppx -or -not $signTool) {
    throw "MakeAppx.exe and SignTool.exe are required. Install the Windows SDK."
}

Write-Host "[1/5] Publishing application..." -ForegroundColor Yellow
$publishPath = Join-Path $OutputPath "publish"
dotnet publish .\WindowsGuardianWatchdog.csproj -c $Configuration -r win-x64 --self-contained true -p:WindowsPackageType=None -o $publishPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "[2/5] Preparing MSIX package structure..." -ForegroundColor Yellow

# Create package structure
$packagePath = Join-Path $OutputPath "package"
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

# Copy published files
Copy-Item -Path "$publishPath\*" -Destination $packagePath -Recurse -Force

# Copy resources
if (Test-Path "Resources") {
    New-Item -ItemType Directory -Path "$packagePath\Resources" -Force | Out-Null
    Copy-Item -Path "Resources\*" -Destination "$packagePath\Resources\" -Recurse -Force
}

# Copy manifest
Copy-Item -Path "Package.appxmanifest" -Destination "$packagePath\AppxManifest.xml" -Force

# A signed MSIX requires the manifest Publisher to exactly match the signing
# certificate subject. Keep the checked-in value useful for unsigned validation,
# but stamp the real identity into the generated package when a PFX is supplied.
if ($CertificatePath) {
    $resolvedCertificate = (Resolve-Path $CertificatePath).Path
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $resolvedCertificate,
        $CertificatePassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    if (-not $certificate.HasPrivateKey) { throw "The signing certificate does not contain a private key." }
    $manifestPath = Join-Path $packagePath "AppxManifest.xml"
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    $manifest.Package.Identity.Publisher = $certificate.Subject
    $manifest.Save($manifestPath)
}

Write-Host "✓ Package structure created" -ForegroundColor Green
Write-Host ""

Write-Host "[3/5] Creating MSIX package..." -ForegroundColor Yellow
$msixPath = Join-Path $OutputPath "WindowsGuardianWatchdog.msix"
& $makeAppx pack /d $packagePath /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ MSIX package creation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ MSIX package created: $msixPath" -ForegroundColor Green
Write-Host ""

Write-Host "[4/5] Signing and verifying package..." -ForegroundColor Yellow
if ($CertificatePath) {
    $signArguments = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $TimestampUrl, "/f", $resolvedCertificate)
    if ($CertificatePassword) { $signArguments += @("/p", $CertificatePassword) }
    $signArguments += $msixPath
    & $signTool $signArguments
    if ($LASTEXITCODE -ne 0) { throw "MSIX signing failed." }

    & $signTool verify /pa /v $msixPath
    if ($LASTEXITCODE -ne 0) { throw "MSIX signature verification failed." }
    Write-Host "✓ MSIX signed and verified" -ForegroundColor Green
} else {
    Write-Host "! Package is unsigned. Supply -CertificatePath to create an installable signed release." -ForegroundColor Yellow
}
Write-Host ""

Write-Host "[5/5] Creating installation instructions..." -ForegroundColor Yellow
$installInstructions = @"
Windows Guardian Watchdog - MSIX Installation
============================================

Installation Methods:

1. Double-click the .msix file and follow the prompts
   (Requires: Windows 10 1809+ or Windows 11)

2. Via PowerShell (Run as Administrator):
   Add-AppxPackage -Path "WindowsGuardianWatchdog.msix"

3. Via Command Prompt (Run as Administrator):
   powershell Add-AppxPackage -Path "WindowsGuardianWatchdog.msix"

The package must be signed by a certificate trusted on this computer.

After installation:
- The app will appear in Start Menu
- You can pin it to taskbar
- Desktop shortcut can be created from Start Menu

Uninstallation:
  Remove-AppxPackage WindowsGuardianWatchdog
"@
$installInstructions | Out-File -FilePath "$OutputPath\INSTALL.txt" -Encoding UTF8

Write-Host "✓ Installation instructions created" -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "MSIX Package Created!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package: $msixPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "To install:" -ForegroundColor Yellow
Write-Host "  Double-click the .msix file or run:" -ForegroundColor White
Write-Host "  Add-AppxPackage -Path `"$msixPath`"" -ForegroundColor White
Write-Host ""

