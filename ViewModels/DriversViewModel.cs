using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Drivers;
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

        public DriversViewModel()
        {
            _scanner = new DriverScanner();
            _updateChecker = new DriverUpdateChecker();
            _installer = new DriverInstaller();
            Drivers = new ObservableCollection<DriverViewModel>();
            
            _installer.ProgressUpdated += Installer_ProgressUpdated;
        }

        public ObservableCollection<DriverViewModel> Drivers { get; }

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

        public int OutdatedCount => Drivers.Count(d => d.Status == "Outdated");

        public async Task ScanAsync()
        {
            IsScanning = true;
            StatusText = "Scanning drivers...";
            Drivers.Clear();

            try
            {
                var drivers = await Task.Run(() => _scanner.Scan());
                
                foreach (var driver in drivers)
                {
                    var vm = new DriverViewModel
                    {
                        DeviceId = driver.DeviceId,
                        DeviceName = driver.DeviceName,
                        DriverVersion = driver.DriverVersion,
                        DriverDate = driver.DriverDate?.ToString("yyyy-MM-dd") ?? "Unknown",
                        Provider = driver.DriverProviderName,
                        IsSigned = driver.IsSigned,
                        Status = driver.Status.ToString()
                    };

                    // Check for updates
                    var hasUpdate = await _updateChecker.IsUpdateAvailableAsync(driver);
                    if (hasUpdate)
                    {
                        vm.Status = "Outdated";
                        vm.IsUpdateAvailable = true;
                    }

                    Drivers.Add(vm);
                }

                StatusText = $"Found {Drivers.Count} drivers ({OutdatedCount} outdated)";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task UpdateDriverAsync(DriverViewModel driver)
        {
            StatusText = $"Updating {driver.DeviceName}...";
            // TODO: Implement driver update logic
            await Task.Delay(1000); // Placeholder
            StatusText = "Update completed";
        }

        private void Installer_ProgressUpdated(object? sender, DriverInstallProgress progress)
        {
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                StatusText = progress.Status;
            });
        }
    }

    public class DriverViewModel
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public string DriverDate { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsSigned { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsUpdateAvailable { get; set; }
    }
}

