# Build Installer Script for Windows Guardian Watchdog
# This script builds the app and creates a distributable installer package

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\dist"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Guardian Watchdog - Build Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean previous builds
Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean -c $Configuration
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
Write-Host "✓ Cleaned" -ForegroundColor Green
Write-Host ""

# Step 2: Restore packages
Write-Host "[2/5] Restoring packages..." -ForegroundColor Yellow
dotnet restore
Write-Host "✓ Packages restored" -ForegroundColor Green
Write-Host ""

# Step 3: Build the application
Write-Host "[3/5] Building application ($Configuration)..." -ForegroundColor Yellow
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 4: Publish the application
Write-Host "[4/5] Publishing application..." -ForegroundColor Yellow
$publishPath = Join-Path $OutputPath "publish"
dotnet publish -c $Configuration --no-build -o $publishPath --self-contained false
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Publish failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Published to $publishPath" -ForegroundColor Green
Write-Host ""

# Step 5: Create distribution package
Write-Host "[5/5] Creating distribution package..." -ForegroundColor Yellow

# Copy additional files
$distPath = Join-Path $OutputPath "WindowsGuardianWatchdog"
New-Item -ItemType Directory -Path $distPath -Force | Out-Null

# Copy published files
Copy-Item -Path "$publishPath\*" -Destination $distPath -Recurse -Force

# Copy resources
if (Test-Path "Resources") {
    Copy-Item -Path "Resources\*" -Destination "$distPath\Resources\" -Recurse -Force
}

# Create README
$readmeContent = @"
Windows Guardian Watchdog
========================

Installation Instructions:
1. Extract all files to a folder (e.g., C:\Program Files\WindowsGuardianWatchdog)
2. Run WindowsGuardianWatchdog.exe
3. (Optional) Run CreateDesktopShortcut.ps1 to create desktop shortcut

System Requirements:
- Windows 11, or Windows 10 version 2004 or later with current updates
- .NET 8.0 Runtime (will be installed automatically if missing)

For more information, visit the project repository.
"@
$readmeContent | Out-File -FilePath "$distPath\README.txt" -Encoding UTF8

# Create ZIP package
$zipPath = Join-Path $OutputPath "WindowsGuardianWatchdog-v1.0.0.zip"
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Compress-Archive -Path "$distPath\*" -DestinationPath $zipPath -Force
Write-Host "✓ Created ZIP: $zipPath" -ForegroundColor Green

# Create installer script
$installerScript = @"
@echo off
echo ========================================
echo Windows Guardian Watchdog - Installer
echo ========================================
echo.

set INSTALL_DIR=%ProgramFiles%\WindowsGuardianWatchdog

echo Installing to: %INSTALL_DIR%
echo.

if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
)

echo Copying files...
xcopy /E /I /Y * "%INSTALL_DIR%\"

echo.
echo Creating desktop shortcut...
powershell -Command "`$WshShell = New-Object -ComObject WScript.Shell; `$Shortcut = `$WshShell.CreateShortcut('%USERPROFILE%\Desktop\Windows Guardian Watchdog.lnk'); `$Shortcut.TargetPath = '%INSTALL_DIR%\WindowsGuardianWatchdog.exe'; `$Shortcut.WorkingDirectory = '%INSTALL_DIR%'; `$Shortcut.Description = 'Enterprise-Grade Security Monitoring'; `$Shortcut.Save()"

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo The application has been installed to:
echo %INSTALL_DIR%
echo.
echo A desktop shortcut has been created.
echo.
pause
"@
$installerScript | Out-File -FilePath "$distPath\install.bat" -Encoding ASCII

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Distribution files:" -ForegroundColor Cyan
Write-Host "  - ZIP Package: $zipPath" -ForegroundColor White
Write-Host "  - Installer: $distPath\install.bat" -ForegroundColor White
Write-Host "  - Application: $distPath" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test the ZIP package by extracting and running" -ForegroundColor White
Write-Host "  2. Distribute the ZIP file for manual installation" -ForegroundColor White
Write-Host "  3. Or use install.bat for automated installation" -ForegroundColor White
Write-Host ""

