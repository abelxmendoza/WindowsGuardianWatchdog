using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WindowsGuardianWatchdog.ViewModels;
using WindowsGuardianWatchdog.Core.Systems;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class SecurityOverviewView : Page
    {
        private SecurityOverviewViewModel? _viewModel;

        public SecurityOverviewView()
        {
            this.InitializeComponent();
            _viewModel = new SecurityOverviewViewModel();
            this.DataContext = _viewModel;
            
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            // Update UI elements based on property changes
            if (e.PropertyName == nameof(SecurityOverviewViewModel.DefenderStatus) && _viewModel.DefenderStatus != null)
            {
                UpdateIndicator(RealtimeIndicator, _viewModel.DefenderStatus.RealTimeProtectionEnabled);
                UpdateIndicator(CloudIndicator, _viewModel.DefenderStatus.CloudProtectionEnabled);
                UpdateIndicator(CFAIndicator, _viewModel.DefenderStatus.ControlledFolderAccessEnabled);
                UpdateIndicator(PUAIndicator, _viewModel.DefenderStatus.PUAProtectionEnabled);
                UpdateIndicator(BehaviorIndicator, _viewModel.DefenderStatus.BehaviorMonitoringEnabled);
                UpdateIndicator(AntivirusIndicator, _viewModel.DefenderStatus.AntivirusEnabled);
            }

            if (e.PropertyName == nameof(SecurityOverviewViewModel.NetworkStatus) && _viewModel.NetworkStatus != null)
            {
                UpdateIndicator(FirewallIndicator, _viewModel.NetworkStatus.FirewallEnabled);
                UpdateIndicator(NetworkProtectionIndicator, _viewModel.NetworkStatus.NetworkProtectionEnabled);
                ConnectionsText.Text = $"Active Connections: {_viewModel.NetworkStatus.ActiveConnections}";
            }

            if (e.PropertyName == nameof(SecurityOverviewViewModel.SystemHealth) && _viewModel.SystemHealth != null)
            {
                CpuUsageText.Text = $"{_viewModel.SystemHealth.CpuUsage:F1}%";
                MemoryUsageText.Text = $"{_viewModel.SystemHealth.MemoryUsage:F1}%";
                DiskUsageText.Text = $"{_viewModel.SystemHealth.DiskUsage:F1}%";
            }

            if (e.PropertyName == nameof(SecurityOverviewViewModel.FirefoxSettings) && _viewModel.FirefoxSettings != null)
            {
                UpdateIndicator(FirefoxTrackingIndicator, _viewModel.FirefoxSettings.TrackingProtectionEnabled);
                UpdateIndicator(CookieProtectionIndicator, _viewModel.FirefoxSettings.TotalCookieProtectionEnabled);
            }

            if (e.PropertyName == nameof(SecurityOverviewViewModel.ProtectionStrength))
            {
                ProtectionStrengthText.Text = _viewModel.ProtectionStrength;
                // Update color based on strength
                ProtectionStrengthText.Foreground = _viewModel.ProtectionStrength switch
                {
                    "High" => new SolidColorBrush(Microsoft.UI.Colors.Lime),
                    "Medium" => new SolidColorBrush(Microsoft.UI.Colors.Yellow),
                    "Low" => new SolidColorBrush(Microsoft.UI.Colors.Red),
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            }

            if (e.PropertyName == nameof(SecurityOverviewViewModel.SecurityAlerts))
            {
                AlertBanner.Visibility = _viewModel.SecurityAlerts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateIndicator(Microsoft.UI.Xaml.Shapes.Ellipse indicator, bool enabled)
        {
            indicator.Fill = new SolidColorBrush(enabled ? Microsoft.UI.Colors.Lime : Microsoft.UI.Colors.Red);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                await _viewModel.RefreshAsync();
            }
        }
    }
}

