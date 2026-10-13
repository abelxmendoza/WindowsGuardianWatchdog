# PowerShell script to install/uninstall the Windows Service
param(
    [Parameter(Mandatory=$false)]
    [switch]$Uninstall,
    
    [Parameter(Mandatory=$false)]
    [string]$ServicePath = ".\bin\Release\net8.0-windows\WindowsGuardianWatchdog.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ServicePath)) {
    Write-Error "Service executable not found at: $ServicePath"
    Write-Host "Please build the project first or provide the correct path with -ServicePath parameter"
    exit 1
}

if ($Uninstall) {
    Write-Host "Uninstalling Windows Guardian Watchdog Service..."
    
    # Stop the service if it's running
    $service = Get-Service -Name "WindowsGuardianWatchdog" -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq "Running") {
        Write-Host "Stopping service..."
        Stop-Service -Name "WindowsGuardianWatchdog" -Force
    }
    
    # Uninstall using InstallUtil
    $installUtilPath = "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\InstallUtil.exe"
    if (-not (Test-Path $installUtilPath)) {
        $installUtilPath = "${env:ProgramFiles}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\InstallUtil.exe"
    }
    
    if (Test-Path $installUtilPath) {
        & $installUtilPath /u $ServicePath
    } else {
        Write-Host "InstallUtil not found. Using .NET InstallUtil..."
        $dotnetInstallUtil = "$(Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)"
        if ($dotnetInstallUtil) {
            dotnet installutil /u $ServicePath
        } else {
            Write-Error "InstallUtil not found. Please install .NET Framework SDK or use Visual Studio."
        }
    }
    
    Write-Host "Service uninstalled successfully."
} else {
    Write-Host "Installing Windows Guardian Watchdog Service..."
    
    # Install using InstallUtil
    $installUtilPath = "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\InstallUtil.exe"
    if (-not (Test-Path $installUtilPath)) {
        $installUtilPath = "${env:ProgramFiles}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\InstallUtil.exe"
    }
    
    if (Test-Path $installUtilPath) {
        & $installUtilPath $ServicePath
    } else {
        Write-Host "InstallUtil not found. Using .NET InstallUtil..."
        $dotnetInstallUtil = "$(Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)"
        if ($dotnetInstallUtil) {
            dotnet installutil $ServicePath
        } else {
            Write-Error "InstallUtil not found. Please install .NET Framework SDK or use Visual Studio."
        }
    }
    
    Write-Host "Service installed successfully."
    Write-Host "You can now start the service using: Start-Service -Name 'WindowsGuardianWatchdog'"
}

