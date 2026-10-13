# Windows Guardian Watchdog

<p align="center">
  <img src="Resources/Logo.png" alt="Windows Guardian Watchdog wolf logo" width="560" />
</p>

<p align="center">
  <strong>Native Windows protection with a security-first, local-only design.</strong>
</p>

A comprehensive Windows-native security monitoring and threat detection application, inspired by [MacGuardianWatchdog](https://github.com/abelxmendoza/MacGuardianWatchdog). Built with WinUI 3 for a modern, native Windows experience.

The wolf mark is used consistently by the executable, Windows taskbar, Desktop shortcut, Start menu entry, navigation shell, and application pages. The full wordmark above is the canonical project artwork.

## Features

### 🔐 Security Monitoring
- **Real-time Process Monitoring**: Track all running processes, detect suspicious activity
- **Network Connection Analysis**: Monitor network connections for anomalies
- **System Integrity Checks**: Verify Windows Defender, Firewall, and UAC status
- **Threat Detection**: Blue Team threat hunting capabilities
- **File Integrity Monitoring**: Track file changes, detect unauthorized modifications
- **Security Auditing**: Comprehensive security assessment with scoring
- **Automated Remediation**: Fix common security issues automatically

### 🎯 Core Capabilities

1. **Security Monitor**
   - Real-time process monitoring
   - Suspicious process detection
   - Memory and CPU usage tracking
   - System security status checks

2. **File Integrity Monitor**
   - Baseline creation and comparison
   - File change detection (modified, deleted, new files)
   - SHA-256 hash verification
   - Metadata change tracking

3. **Threat Detection (Blue Team)**
   - Process-based threat hunting
   - Network threat analysis
   - Configuration security checks
   - Registry persistence detection
   - Digital signature verification

4. **Security Audit**
   - System security assessment
   - User account analysis
   - Network security evaluation
   - Update status checking
   - Encryption status verification
   - Security scoring (0-100)

5. **Remediation Engine**
   - Enable Windows Defender
   - Enable Firewall
   - Enable UAC
   - Kill suspicious processes
   - Quarantine files
   - Remove startup entries
   - Dry-run mode for safety

6. **Windows Service**
   - Background monitoring
   - Automatic threat detection
   - Event log integration
   - Scheduled threat hunts

## Architecture

```
WindowsGuardianWatchdog/
├── Core/
│   ├── SecurityMonitor.cs          # Real-time security monitoring
│   ├── FileIntegrityMonitor.cs     # File integrity tracking
│   ├── BlueTeamDetector.cs         # Threat detection & hunting
│   ├── SecurityAudit.cs            # Security assessment
│   └── RemediationEngine.cs        # Automated remediation
├── Services/
│   └── WindowsGuardianService.cs   # Windows Service for background monitoring
├── Views/
│   ├── DashboardView.xaml          # Main dashboard
│   ├── SecurityMonitorView.xaml    # Process monitoring UI
│   ├── FileIntegrityView.xaml      # File monitoring UI
│   ├── ThreatDetectionView.xaml    # Threat hunting UI
│   ├── SecurityAuditView.xaml      # Security audit UI
│   ├── RemediationView.xaml        # Remediation center
│   └── AlertsView.xaml             # Security alerts
├── App.xaml                         # WinUI 3 application
├── MainWindow.xaml                  # Main window with navigation
└── WindowsServiceInstaller.cs      # Service installer
```

## Requirements

- Windows 10 version 1809 (build 17763) or later
- Windows 11
- .NET 8.0 or later
- Administrator privileges (for service installation and some features)

## Building the Project

```bash
# Restore packages
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

## Creating Distribution Packages

### Standard ZIP Package (Recommended)
```powershell
.\BuildInstaller.ps1
```
Creates a ZIP file with installer script in `dist\WindowsGuardianWatchdog-v1.0.0.zip`

### MSIX Package (Modern Windows App)
```powershell
# Validate and create an unsigned package
.\CreateMSIXPackage.ps1

# Create a production package signed with a trusted code-signing certificate
.\CreateMSIXPackage.ps1 -CertificatePath C:\secure\codesigning.pfx -CertificatePassword $env:WG_SIGNING_PASSWORD
```
Creates an MSIX package in `dist\msix\WindowsGuardianWatchdog.msix`
and verifies its signature when a certificate is supplied. Requires the Windows SDK.
Keep certificates and passwords outside the repository. A self-signed certificate is
only suitable for local testing and does not establish Smart App Control reputation.

### Portable Package (Self-Contained)
```powershell
.\CreatePortablePackage.ps1
```
Creates a portable ZIP with .NET runtime included in `dist\WindowsGuardianWatchdog-Portable-v1.0.0.zip`

See [DISTRIBUTION.md](DISTRIBUTION.md) for detailed distribution instructions.

## Installation

### As Desktop Application

1. Build the project: `dotnet build -c Release`
2. Run the executable from `bin/Release/net8.0-windows10.0.19041.0/`

### As Windows Service

#### Using PowerShell Script

```powershell
# Install the service (run as Administrator)
.\InstallService.ps1

# Uninstall the service
.\InstallService.ps1 -Uninstall
```

#### Using InstallUtil

```bash
# Install
InstallUtil.exe WindowsGuardianWatchdog.exe

# Uninstall
InstallUtil.exe /u WindowsGuardianWatchdog.exe
```

#### Service Management

```powershell
# Start service
Start-Service -Name "WindowsGuardianWatchdog"

# Stop service
Stop-Service -Name "WindowsGuardianWatchdog"

# Check status
Get-Service -Name "WindowsGuardianWatchdog"

# View logs
Get-EventLog -LogName Application -Source "WindowsGuardianWatchdog" -Newest 50
```

## Usage

### Desktop Application

1. **Launch the application** - The dashboard shows system status at a glance
2. **Start Monitoring** - Click "Start Monitoring" to begin real-time security monitoring
3. **Run Security Audit** - Perform a comprehensive security assessment
4. **Threat Hunt** - Actively search for threats on your system
5. **File Integrity** - Add paths to monitor and create baselines
6. **Remediation** - Fix security issues automatically (with dry-run mode)

### Windows Service

The service runs in the background and:
- Monitors processes every 30 seconds
- Checks file integrity every 5 minutes
- Performs threat hunts every hour
- Logs all events to Windows Event Log

## Security Features Comparison

| Feature | Windows Guardian | MacGuardian |
|---------|-----------------|-------------|
| Process Monitoring | ✅ | ✅ |
| File Integrity | ✅ | ✅ |
| Threat Detection | ✅ | ✅ |
| Security Audit | ✅ | ✅ |
| Automated Remediation | ✅ | ✅ |
| Windows Service | ✅ | LaunchAgent |
| Native UI | WinUI 3 | SwiftUI |
| Background Monitoring | ✅ | ✅ |

## Project Structure

- **Core Modules**: Security monitoring, file integrity, threat detection, auditing, remediation
- **Services**: Windows Service for background operation
- **Views**: WinUI 3 native interface with navigation
- **Installation**: Service installer and PowerShell scripts

## Security Considerations

- **Dry-Run Mode**: All remediation actions default to dry-run for safety
- **Quarantine System**: Files are moved to quarantine, not deleted
- **Event Logging**: All security events are logged to Windows Event Log
- **Administrator Rights**: Required for service installation and some security checks

## Troubleshooting

### Permission Issues
Some operations require administrator privileges. The application will prompt when needed.

### Service Not Starting
- Ensure you have administrator rights
- Check Windows Event Log for errors
- Verify .NET 8.0 runtime is installed

### Monitoring Not Working
- Check if Windows Defender is blocking the application
- Verify file permissions for monitored paths
- Review Event Log for detailed error messages

## Contributing

Contributions welcome! Areas for improvement:
- Additional threat detection rules
- More remediation actions
- Enhanced reporting
- Cloud integration options
- Multi-device management

## License

This project is licensed under the MIT License.

## Acknowledgments

Inspired by [MacGuardianWatchdog](https://github.com/abelxmendoza/MacGuardianWatchdog) - bringing enterprise-grade security monitoring to Windows.

---

**Stay secure! 🔐**

_Windows Guardian Watchdog - Enterprise-Grade Security for Windows_
