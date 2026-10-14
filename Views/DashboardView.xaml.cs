using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.Defender;
using WindowsGuardianWatchdog.Core.ML;
using WindowsGuardianWatchdog.Core.Systems;
using WindowsGuardianWatchdog.Services;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class DashboardView : Page
    {
        private readonly SecurityMonitor _securityMonitor = new();
        private readonly FileIntegrityMonitor _fileMonitor = new();
        private readonly BlueTeamDetector _threatDetector = new();
        private readonly DefenderControl _defender = new();
        private readonly SystemHealthMonitor _healthMonitor = new();
        private readonly ObservableCollection<ActivityItem> _activities = new();
        private bool _isBusy;

        public DashboardView()
        {
            InitializeComponent();
            ActivityListView.ItemsSource = _activities;
            _securityMonitor.AlertRaised += (_, alert) => DispatcherQueue.TryEnqueue(() => AddActivity($"[{alert.Severity}] {alert.Message}", "ALERT"));
            _fileMonitor.IntegrityViolation += (_, alert) => DispatcherQueue.TryEnqueue(() => AddActivity(alert.Message, "INTEGRITY"));
            _threatDetector.ThreatDetected += (_, threat) => DispatcherQueue.TryEnqueue(() => AddActivity($"[{threat.Severity}] {threat.Description}", "THREAT"));
            Loaded += async (_, _) => await RefreshStatusAsync();
            MLServiceClient.Initialize(new MLSecurityEngine());
            AddActivity("Ready. Start with step 1, Update your apps.", "HOME");
        }

        private async Task RefreshStatusAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            OperationProgress.Visibility = Visibility.Visible;
            OperationStatusText.Text = "Checking how protected this PC is…";
            try
            {
                var defenderTask = _defender.GetStatusAsync();
                var healthTask = _healthMonitor.GetHealthAsync();
                await Task.WhenAll(defenderTask, healthTask);
                var defender = defenderTask.Result;
                var health = healthTask.Result;
                var controls = new[] { defender.AntivirusEnabled, defender.RealTimeProtectionEnabled, defender.CloudProtectionEnabled, defender.BehaviorMonitoringEnabled, defender.PUAProtectionEnabled };
                var score = controls.Count(enabled => enabled) * 18 + (health.Status == SystemHealthMonitor.HealthStatus.Healthy ? 10 : 0);

                DefenderStatusText.Text = defender.IsHealthy ? "On" : "Needs a look";
                SystemHealthText.Text = health.Status == SystemHealthMonitor.HealthStatus.Healthy ? "Looks good" : "Needs a look";
                MonitorStatusText.Text = _securityMonitor.IsMonitoring ? "Watching" : "Not watching";
                SecurityScoreText.Text = $"{score}/100";
                SecurityStatusText.Text = score >= 85 ? "This PC looks protected" : score >= 60 ? "A few things are worth checking" : "Start with the list below";
                SecuritySummaryText.Text = $"Virus protection is {(defender.AntivirusEnabled ? "on" : "off")}. Watching new files is {(defender.RealTimeProtectionEnabled ? "on" : "off")}. Checked at {DateTime.Now:t}.";
                PostureIcon.Foreground = BrushFor(score >= 85 ? "#42D392" : score >= 60 ? "#F4B942" : "#FF5D73");
                OperationStatusText.Text = "Status refreshed";
            }
            catch (Exception ex)
            {
                SecurityStatusText.Text = "Could not check just now";
                SecuritySummaryText.Text = "Windows did not share its protection status. The steps below still work.";
                DefenderStatusText.Text = "Unknown";
                SystemHealthText.Text = "Unknown";
                OperationStatusText.Text = ex.Message;
            }
            finally { OperationProgress.Visibility = Visibility.Collapsed; _isBusy = false; }
        }

        private static SolidColorBrush BrushFor(string color) => new(Microsoft.UI.ColorHelper.FromArgb(255, Convert.ToByte(color.Substring(1, 2), 16), Convert.ToByte(color.Substring(3, 2), 16), Convert.ToByte(color.Substring(5, 2), 16)));
        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

        private void StartMonitoringButton_Click(object sender, RoutedEventArgs e)
        {
            _securityMonitor.StartMonitoring();
            _fileMonitor.StartMonitoring();
            MonitorStatusText.Text = "Watching";
            StartMonitoringButton.IsEnabled = false;
            AddActivity("Now watching programs and important files.", "WATCHING");
        }

        private async void RunAuditButton_Click(object sender, RoutedEventArgs e) => await RunOperationAsync("Checking the safety switches…", async () =>
        {
            var report = await new SecurityAudit().PerformFullAuditAsync();
            AddActivity($"Protection checkup finished. Score {report.Score} out of 100.", "CHECKUP");
        });

        private async void ThreatHuntButton_Click(object sender, RoutedEventArgs e) => await RunOperationAsync("Looking for known risks…", async () =>
        {
            var report = await _threatDetector.PerformThreatHuntAsync();
            AddActivity(report.ThreatCount == 0 ? "No known risks stood out." : $"Found {report.ThreatCount} item(s) worth a look.", "RISKS");
        });

        private async Task RunOperationAsync(string status, Func<Task> operation)
        {
            if (_isBusy) return;
            _isBusy = true;
            OperationStatusText.Text = status;
            OperationProgress.Visibility = Visibility.Visible;
            try { await operation(); OperationStatusText.Text = "Completed"; }
            catch (Exception ex) { OperationStatusText.Text = $"Could not complete: {ex.Message}"; AddActivity(OperationStatusText.Text, "ERROR"); }
            finally { OperationProgress.Visibility = Visibility.Collapsed; _isBusy = false; }
        }

        private void ViewAlertsButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AlertsView));
        private void OpenUpdatesButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AppUpdatesView));
        private void OpenHardeningButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HardeningView));
        private void OpenCleanupButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CleanupView));
        private void OpenDriversButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(DriversView));
        private void OpenStartupButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(StartupView));
        private void OpenMonitorButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SecurityMonitorView));

        private void AddActivity(string message, string type)
        {
            _activities.Insert(0, new ActivityItem { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = message, Type = type });
            while (_activities.Count > 100) _activities.RemoveAt(_activities.Count - 1);
            if (AlertsCountText != null) AlertsCountText.Text = _activities.Count(a => a.Type is "ALERT" or "THREAT" or "INTEGRITY").ToString();
        }
    }

    public sealed class ActivityItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
