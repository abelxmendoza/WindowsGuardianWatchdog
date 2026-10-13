using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.ML;
using WindowsGuardianWatchdog.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class DashboardView : Page
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly FileIntegrityMonitor _fileMonitor;
        private readonly BlueTeamDetector _threatDetector;
        private readonly ObservableCollection<ActivityItem> _activities;

        public DashboardView()
        {
            this.InitializeComponent();
            
            _securityMonitor = new SecurityMonitor();
            _fileMonitor = new FileIntegrityMonitor();
            _threatDetector = new BlueTeamDetector();
            _activities = new ObservableCollection<ActivityItem>();
            
            ActivityListView.ItemsSource = _activities;
            
            _securityMonitor.AlertRaised += SecurityMonitor_AlertRaised;
            _fileMonitor.IntegrityViolation += FileMonitor_IntegrityViolation;
            _threatDetector.ThreatDetected += ThreatDetector_ThreatDetected;
            
            // Initialize ML engine
            InitializeMLEngine();
            
            UpdateDashboard();
        }

        private void InitializeMLEngine()
        {
            var mlEngine = new MLSecurityEngine();
            MLServiceClient.Initialize(mlEngine);
            
            // Start periodic ML analysis
            var timer = new System.Threading.Timer(async _ =>
            {
                await mlEngine.AnalyzeBehaviorAsync();
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        private void UpdateDashboard()
        {
            SecurityStatusText.Text = _securityMonitor.IsMonitoring ? "Active" : "Inactive";
            ThreatsCountText.Text = "0"; // Will be updated from threat detection
            FilesMonitoredText.Text = "0"; // Will be updated from file monitor
            AlertsCountText.Text = _activities.Count(a => a.Type == "Alert").ToString();
        }

        private void SecurityMonitor_AlertRaised(object? sender, SecurityAlert alert)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _activities.Insert(0, new ActivityItem
                {
                    Timestamp = alert.Timestamp.ToString("HH:mm:ss"),
                    Message = $"[{alert.Severity}] {alert.Message}",
                    Type = "Alert"
                });
                UpdateDashboard();
            });
        }

        private void FileMonitor_IntegrityViolation(object? sender, FileIntegrityAlert alert)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _activities.Insert(0, new ActivityItem
                {
                    Timestamp = alert.Timestamp.ToString("HH:mm:ss"),
                    Message = $"File Integrity: {alert.Message}",
                    Type = "File"
                });
                UpdateDashboard();
            });
        }

        private void ThreatDetector_ThreatDetected(object? sender, ThreatDetection threat)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _activities.Insert(0, new ActivityItem
                {
                    Timestamp = threat.Timestamp.ToString("HH:mm:ss"),
                    Message = $"[{threat.Severity}] {threat.Description}",
                    Type = "Threat"
                });
                UpdateDashboard();
            });
        }

        private void StartMonitoringButton_Click(object sender, RoutedEventArgs e)
        {
            _securityMonitor.StartMonitoring();
            _fileMonitor.StartMonitoring();
            AddActivity("Monitoring started", "System");
            UpdateDashboard();
        }

        private async void RunAuditButton_Click(object sender, RoutedEventArgs e)
        {
            AddActivity("Security audit started...", "System");
            var audit = new SecurityAudit();
            var report = await audit.PerformFullAuditAsync();
            AddActivity($"Security audit completed. Score: {report.Score}/100", "System");
        }

        private async void ThreatHuntButton_Click(object sender, RoutedEventArgs e)
        {
            AddActivity("Threat hunt started...", "System");
            var report = await _threatDetector.PerformThreatHuntAsync();
            AddActivity($"Threat hunt completed. Found {report.ThreatCount} threats", "System");
            ThreatsCountText.Text = report.ThreatCount.ToString();
        }

        private void ViewAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to alerts view
        }

        private void AddActivity(string message, string type)
        {
            _activities.Insert(0, new ActivityItem
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Message = message,
                Type = type
            });
        }
    }

    public class ActivityItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}

