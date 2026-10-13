using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core;
using System.Diagnostics;

namespace WindowsGuardianWatchdog.Services
{
    public partial class WindowsGuardianService : ServiceBase
    {
        private SecurityMonitor? _securityMonitor;
        private FileIntegrityMonitor? _fileMonitor;
        private BlueTeamDetector? _threatDetector;
        private CancellationTokenSource? _cancellationTokenSource;

        public WindowsGuardianService()
        {
            InitializeComponent();
            ServiceName = "WindowsGuardianWatchdog";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            _securityMonitor = new SecurityMonitor();
            _fileMonitor = new FileIntegrityMonitor();
            _threatDetector = new BlueTeamDetector();

            // Set up event handlers for logging
            _securityMonitor.AlertRaised += (s, alert) =>
            {
                EventLog.WriteEntry($"Security Alert: {alert.Message}", 
                    alert.Severity == AlertSeverity.Critical ? EventLogEntryType.Error : EventLogEntryType.Warning);
            };

            _fileMonitor.IntegrityViolation += (s, alert) =>
            {
                EventLog.WriteEntry($"File Integrity Violation: {alert.Message}", EventLogEntryType.Warning);
            };

            _threatDetector.ThreatDetected += (s, threat) =>
            {
                EventLog.WriteEntry($"Threat Detected: {threat.Description}", EventLogEntryType.Warning);
            };

            // Start monitoring
            _securityMonitor.StartMonitoring(30000); // Check every 30 seconds
            _fileMonitor.StartMonitoring(300000); // Check every 5 minutes

            // Run periodic threat hunts
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _threatDetector.PerformThreatHuntAsync();
                        await Task.Delay(3600000, _cancellationTokenSource.Token); // Every hour
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry($"Error in threat hunt: {ex.Message}", EventLogEntryType.Error);
                    }
                }
            }, _cancellationTokenSource.Token);

            EventLog.WriteEntry("Windows Guardian Watchdog service started", EventLogEntryType.Information);
        }

        protected override void OnStop()
        {
            _cancellationTokenSource?.Cancel();
            
            _securityMonitor?.StopMonitoring();
            _fileMonitor?.StopMonitoring();
            
            _securityMonitor?.Dispose();
            _fileMonitor?.Dispose();
            
            EventLog.WriteEntry("Windows Guardian Watchdog service stopped", EventLogEntryType.Information);
        }
    }
}

