# PowerShell script to create desktop shortcut for Windows Guardian Watchdog
# Run this script as Administrator for best results

$ErrorActionPreference = "Stop"

$WshShell = New-Object -ComObject WScript.Shell
$ExecutablePath = Join-Path $PSScriptRoot "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\WindowsGuardianWatchdog.exe"
$WorkingDirectory = Split-Path $ExecutablePath
$IconPath = Join-Path $PSScriptRoot "Resources\Logo.ico"

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Release executable not found. Run: dotnet build WindowsGuardianWatchdog.csproj -c Release -p:Platform=x64"
}

$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Windows Guardian Watchdog.lnk")
$Shortcut.TargetPath = $ExecutablePath
$Shortcut.WorkingDirectory = $WorkingDirectory
$Shortcut.Description = "Enterprise-Grade Security Monitoring and System Maintenance"
$Shortcut.IconLocation = "$IconPath,0"
$Shortcut.Save()

Write-Host "Desktop shortcut created successfully!" -ForegroundColor Green
Write-Host "Location: $env:USERPROFILE\Desktop\Windows Guardian Watchdog.lnk" -ForegroundColor Cyan

# Also create Start Menu shortcut
$StartMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
if (-not (Test-Path $StartMenuPath)) {
    New-Item -ItemType Directory -Path $StartMenuPath -Force | Out-Null
}

$StartMenuShortcut = $WshShell.CreateShortcut("$StartMenuPath\Windows Guardian Watchdog.lnk")
$StartMenuShortcut.TargetPath = $ExecutablePath
$StartMenuShortcut.WorkingDirectory = $WorkingDirectory
$StartMenuShortcut.Description = "Enterprise-Grade Security Monitoring and System Maintenance"
$StartMenuShortcut.IconLocation = "$IconPath,0"
$StartMenuShortcut.Save()

Write-Host "Start Menu shortcut created successfully!" -ForegroundColor Green

