# Create Portable Package for Windows Guardian Watchdog
# This creates a portable version that can run from any location (USB drive, etc.)

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\dist\portable"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Guardian Watchdog - Portable Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build and publish
Write-Host "[1/3] Building application..." -ForegroundColor Yellow
dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[2/3] Publishing portable package..." -ForegroundColor Yellow
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Publish as self-contained (includes .NET runtime)
dotnet publish -c $Configuration --no-build -o $OutputPath --self-contained true -r win-x64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Publish failed!" -ForegroundColor Red
    exit 1
}

# Copy resources
if (Test-Path "Resources") {
    New-Item -ItemType Directory -Path "$OutputPath\Resources" -Force | Out-Null
    Copy-Item -Path "Resources\*" -Destination "$OutputPath\Resources\" -Recurse -Force
}

Write-Host "✓ Portable package created" -ForegroundColor Green
Write-Host ""

Write-Host "[3/3] Creating README..." -ForegroundColor Yellow
$readmeContent = @"
Windows Guardian Watchdog - Portable Edition
============================================

This is a portable version of Windows Guardian Watchdog.
You can run it from any location (USB drive, network share, etc.)

System Requirements:
- Windows 11, or Windows 10 version 2004 or later with current updates
- No .NET installation required (runtime included)

Usage:
1. Extract this folder to any location
2. Run WindowsGuardianWatchdog.exe
3. The app will create its configuration in:
   %APPDATA%\WindowsGuardianWatchdog

Portable Features:
- No installation required
- No registry entries
- Can run from USB drive
- All settings stored in AppData

Note: Some features may require administrator privileges.
Right-click and select "Run as administrator" if needed.

For more information, visit the project repository.
"@
$readmeContent | Out-File -FilePath "$OutputPath\README.txt" -Encoding UTF8

# Create ZIP
$zipPath = ".\dist\WindowsGuardianWatchdog-Portable-v1.0.0.zip"
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Portable Package Created!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package: $zipPath" -ForegroundColor Cyan
Write-Host "Size: $((Get-Item $zipPath).Length / 1MB) MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "This package:" -ForegroundColor Yellow
Write-Host "  ✓ Includes .NET runtime (no installation needed)" -ForegroundColor White
Write-Host "  ✓ Can run from any location" -ForegroundColor White
Write-Host "  ✓ Perfect for USB drives" -ForegroundColor White
Write-Host ""

