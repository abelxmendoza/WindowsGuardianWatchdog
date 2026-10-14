using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Drivers;
using WindowsGuardianWatchdog.Core.Scripting;
using WindowsGuardianWatchdog.ViewModels;
using Microsoft.UI.Dispatching;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class DriversViewModel : ViewModelBase
    {
        private readonly DriverScanner _scanner;
        private readonly DriverUpdateChecker _updateChecker;
        private readonly DriverInstaller _installer;
        private bool _isScanning;
        private string _statusText = "Ready";

        private readonly DispatcherQueue? _queue;

        public DriversViewModel()
        {
            _scanner = new DriverScanner();
            _updateChecker = new DriverUpdateChecker();
            _installer = new DriverInstaller();
            Drivers = new ObservableCollection<DriverViewModel>();
            AvailableUpdates = new ObservableCollection<string>();
            _queue = DispatcherQueue.GetForCurrentThread();
            
            _installer.ProgressUpdated += Installer_ProgressUpdated;
        }

        public ObservableCollection<DriverViewModel> Drivers { get; }
        public ObservableCollection<string> AvailableUpdates { get; }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int OutdatedCount => Drivers.Count(d => !d.IsHealthy);

        public async Task ScanAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            StatusText = "Reading installed drivers…";
            Drivers.Clear();

            try
            {
                var drivers = await Task.Run(() => _scanner.Scan());
                
                foreach (var driver in drivers)
                {
                    Drivers.Add(new DriverViewModel
                    {
                        DeviceId = driver.DeviceId,
                        DeviceName = driver.DeviceName,
                        DriverVersion = string.IsNullOrWhiteSpace(driver.DriverVersion) ? "No version" : driver.DriverVersion,
                        DriverDate = driver.DriverDate?.ToString("yyyy-MM-dd") ?? "Unknown",
                        Provider = string.IsNullOrWhiteSpace(driver.DriverProviderName) ? "Unknown provider" : driver.DriverProviderName,
                        IsSigned = driver.IsSigned,
                        Status = driver.Health,
                        IsHealthy = driver.Health is "Working"
                    });
                }

                var attention = Drivers.Where(driver => !driver.IsHealthy)
                    .Select(driver => $"{driver.DeviceName} ({driver.Status.ToLowerInvariant()})")
                    .ToList();
                StatusText = attention.Count == 0
                    ? $"Found {Drivers.Count} drivers. All of them are working."
                    : $"Found {Drivers.Count} drivers. {attention.Count} need attention: {string.Join(", ", attention.Take(3))}{(attention.Count > 3 ? "…" : ".")}";
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task ScanHardwareAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            StatusText = "Asking Windows to scan for hardware changes…";
            try
            {
                var result = await _updateChecker.ScanHardwareAsync();
                StatusText = result.Success
                    ? "Hardware scan finished. Reading drivers again…"
                    : string.IsNullOrWhiteSpace(result.Output) ? "Hardware scan failed." : result.Output.Trim();
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                IsScanning = false;
                return;
            }

            IsScanning = false;
            await ScanAsync();
        }

        public async Task CheckForUpdatesAsync()
        {
            if (IsScanning) return;
            if (Drivers.Count == 0)
            {
                await ScanAsync();
            }

            IsScanning = true;
            StatusText = "Asking Windows Update for newer drivers…";
            try
            {
                var titles = await _updateChecker.FindDriverUpdateTitlesAsync();
                AvailableUpdates.Clear();
                foreach (var title in titles) AvailableUpdates.Add(title);
                foreach (var driver in Drivers)
                {
                    var match = titles.Any(title => DriverUpdateChecker.Matches(new DriverInfo
                    {
                        DeviceName = driver.DeviceName,
                        DriverProviderName = driver.Provider
                    }, new[] { title }));
                    driver.IsUpdateAvailable = match;
                    if (match)
                    {
                        driver.Status = "Update available";
                        driver.IsHealthy = false;
                    }
                }

                var attention = Drivers.Count(driver => !driver.IsHealthy);
                StatusText = titles.Count == 0
                    ? attention == 0
                        ? "Windows Update has no newer drivers."
                        : "Windows Update has no newer drivers. The devices listed in orange still need attention on this PC."
                    : $"Windows Update has {titles.Count} driver update{(titles.Count == 1 ? "" : "s")}.";
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task UpdateDriverAsync(DriverViewModel driver)
        {
            StatusText = $"Installing driver updates from Windows Update for {driver.DeviceName}. Approve the administrator prompt.";
            await InstallAvailableUpdatesAsync();
        }

        public async Task InstallAvailableUpdatesAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            StatusText = "Installing driver updates from Windows Update. Approve the administrator prompt.";
            try
            {
                var result = await _updateChecker.InstallDriverUpdatesAsync();
                StatusText = DescribeInstallResult(result);
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsScanning = false;
            }
        }

        private static string DescribeInstallResult(ScriptResult result)
        {
            if (result.Cancelled) return "Driver update was cancelled.";
            if (result.Output.Contains("DRV|NONE", StringComparison.Ordinal)) return "Windows Update has no newer drivers.";
            if (result.Output.Contains("DRV|RESULT|4", StringComparison.Ordinal) || result.Output.Contains("DRV|RESULT|5", StringComparison.Ordinal))
                return "Windows Update could not install the driver updates.";
            if (result.Output.Contains("|True", StringComparison.Ordinal)) return "Driver updates installed. Restart this PC to finish.";
            if (result.Output.Contains("DRV|RESULT|2", StringComparison.Ordinal) || result.Output.Contains("DRV|RESULT|3", StringComparison.Ordinal))
                return "Driver updates installed.";
            return result.Success ? "Driver updates finished." : DriverUpdateChecker.ExplainUpdateError(result.Output);
        }

        private void Installer_ProgressUpdated(object? sender, DriverInstallProgress progress)
        {
            if (_queue == null)
            {
                StatusText = progress.Status;
                return;
            }

            _queue.TryEnqueue(() => StatusText = progress.Status);
        }
    }

    public class DriverViewModel : ViewModelBase
    {
        private string _status = string.Empty;
        private bool _isUpdateAvailable;
        private bool _isHealthy = true;

        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public string DriverDate { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsSigned { get; set; }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        public bool IsHealthy
        {
            get => _isHealthy;
            set => SetProperty(ref _isHealthy, value);
        }
    }
}

