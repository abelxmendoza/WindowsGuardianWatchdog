using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Defender;
using WindowsGuardianWatchdog.Core.Network;
using WindowsGuardianWatchdog.Core.Systems;
using WindowsGuardianWatchdog.Core.Browser;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class SecurityOverviewViewModel : ViewModelBase
    {
        private readonly DefenderControl _defenderControl;
        private readonly NetworkMonitor _networkMonitor;
        private readonly SystemHealthMonitor _healthMonitor;
        private readonly BrowserControl _browserControl;

        private DefenderControl.DefenderStatus? _defenderStatus;
        private NetworkMonitor.NetworkStatus? _networkStatus;
        private SystemHealthMonitor.SystemHealth? _systemHealth;
        private BrowserControl.FirefoxSettings? _firefoxSettings;
        private string _protectionStrength = "Checking...";
        private bool _isLoading = true;

        public SecurityOverviewViewModel()
        {
            _defenderControl = new DefenderControl();
            _networkMonitor = new NetworkMonitor();
            _healthMonitor = new SystemHealthMonitor();
            _browserControl = new BrowserControl();

            SecurityAlerts = new ObservableCollection<string>();
            
            _ = LoadDataAsync();
        }

        public ObservableCollection<string> SecurityAlerts { get; }

        public DefenderControl.DefenderStatus? DefenderStatus
        {
            get => _defenderStatus;
            set => SetProperty(ref _defenderStatus, value);
        }

        public NetworkMonitor.NetworkStatus? NetworkStatus
        {
            get => _networkStatus;
            set => SetProperty(ref _networkStatus, value);
        }

        public SystemHealthMonitor.SystemHealth? SystemHealth
        {
            get => _systemHealth;
            set => SetProperty(ref _systemHealth, value);
        }

        public BrowserControl.FirefoxSettings? FirefoxSettings
        {
            get => _firefoxSettings;
            set => SetProperty(ref _firefoxSettings, value);
        }

        public string ProtectionStrength
        {
            get => _protectionStrength;
            set => SetProperty(ref _protectionStrength, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                var defenderTask = _defenderControl.GetStatusAsync();
                var networkTask = _networkMonitor.GetNetworkStatusAsync();
                var healthTask = _healthMonitor.GetHealthAsync();
                var browserTask = _browserControl.GetFirefoxSettingsAsync();

                await Task.WhenAll(defenderTask, networkTask, healthTask, browserTask);

                DefenderStatus = await defenderTask;
                NetworkStatus = await networkTask;
                SystemHealth = await healthTask;
                FirefoxSettings = await browserTask;

                CalculateProtectionStrength();
                CheckSecurityAlerts();
            }
            catch (Exception ex)
            {
                SecurityAlerts.Add($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateProtectionStrength()
        {
            if (DefenderStatus == null || NetworkStatus == null)
            {
                ProtectionStrength = "Unknown";
                return;
            }

            int score = 0;
            int maxScore = 7;

            if (DefenderStatus.RealTimeProtectionEnabled) score++;
            if (DefenderStatus.CloudProtectionEnabled) score++;
            if (DefenderStatus.ControlledFolderAccessEnabled) score++;
            if (DefenderStatus.PUAProtectionEnabled) score++;
            if (NetworkStatus.FirewallEnabled) score++;
            if (NetworkStatus.NetworkProtectionEnabled) score++;
            if (FirefoxSettings?.TrackingProtectionEnabled == true) score++;

            var percentage = (double)score / maxScore * 100;

            ProtectionStrength = percentage switch
            {
                >= 85 => "High",
                >= 60 => "Medium",
                _ => "Low"
            };
        }

        private void CheckSecurityAlerts()
        {
            SecurityAlerts.Clear();

            if (DefenderStatus != null)
            {
                if (!DefenderStatus.RealTimeProtectionEnabled)
                    SecurityAlerts.Add("⚠️ Real-Time Protection is disabled");

                if (!DefenderStatus.CloudProtectionEnabled)
                    SecurityAlerts.Add("⚠️ Cloud Protection is disabled");

                if (!DefenderStatus.ControlledFolderAccessEnabled)
                    SecurityAlerts.Add("⚠️ Controlled Folder Access is disabled");
            }

            if (NetworkStatus != null)
            {
                if (!NetworkStatus.FirewallEnabled)
                    SecurityAlerts.Add("🔴 Windows Firewall is disabled");

                if (!NetworkStatus.NetworkProtectionEnabled)
                    SecurityAlerts.Add("⚠️ Network Protection is disabled");
            }

            if (SystemHealth != null && SystemHealth.Status == SystemHealthMonitor.HealthStatus.Critical)
            {
                SecurityAlerts.Add("🔴 System resources critically low");
            }
        }

        public async Task RefreshAsync()
        {
            await LoadDataAsync();
        }
    }
}

