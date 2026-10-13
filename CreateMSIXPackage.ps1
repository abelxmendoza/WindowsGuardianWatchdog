# Create MSIX Package for Windows Guardian Watchdog
# This creates a modern Windows app package that can be installed via Microsoft Store or sideloading

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\dist\msix"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Guardian Watchdog - MSIX Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for MakeAppx.exe (Windows SDK)
$makeAppx = Get-Command "MakeAppx.exe" -ErrorAction SilentlyContinue
if (-not $makeAppx) {
    $sdkPath = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x64\MakeAppx.exe"
    if (Test-Path $sdkPath) {
        $makeAppx = $sdkPath
    } else {
        Write-Host "✗ MakeAppx.exe not found!" -ForegroundColor Red
        Write-Host "  Please install Windows SDK 10.0.26100.0 or later" -ForegroundColor Yellow
        Write-Host "  Download from: https://developer.microsoft.com/windows/downloads/windows-sdk/" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "[1/4] Building application..." -ForegroundColor Yellow
dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "[2/4] Preparing MSIX package structure..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Create package structure
$packagePath = Join-Path $OutputPath "package"
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

# Copy published files
$publishPath = "bin\$Configuration\net8.0-windows10.0.19041.0"
if (Test-Path $publishPath) {
    Copy-Item -Path "$publishPath\*" -Destination $packagePath -Recurse -Force
}

# Copy resources
if (Test-Path "Resources") {
    New-Item -ItemType Directory -Path "$packagePath\Resources" -Force | Out-Null
    Copy-Item -Path "Resources\*" -Destination "$packagePath\Resources\" -Recurse -Force
}

# Copy manifest
Copy-Item -Path "Package.appxmanifest" -Destination "$packagePath\AppxManifest.xml" -Force

Write-Host "✓ Package structure created" -ForegroundColor Green
Write-Host ""

Write-Host "[3/4] Creating MSIX package..." -ForegroundColor Yellow
$msixPath = Join-Path $OutputPath "WindowsGuardianWatchdog.msix"
& $makeAppx pack /d $packagePath /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ MSIX package creation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ MSIX package created: $msixPath" -ForegroundColor Green
Write-Host ""

Write-Host "[4/4] Creating installation instructions..." -ForegroundColor Yellow
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

Note: You may need to enable "Sideload apps" in Windows Settings:
  Settings > Apps > Apps & features > Install apps from any source

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

