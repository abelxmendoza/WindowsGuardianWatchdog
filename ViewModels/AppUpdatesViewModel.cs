using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowsGuardianWatchdog.Core.Updates;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class AppUpdatesViewModel : ViewModelBase
    {
        private bool _isBusy;
        private string _statusText = "Check for app and Windows updates, then install them together.";
        private string _logText = "Ready.";

        public AppUpdatesViewModel()
        {
            AppUpdates = new ObservableCollection<AppPackageUpdate>();
            WindowsUpdates = new ObservableCollection<WindowsUpdateItem>();
            CheckCommand = new RelayCommand(CheckAsync, () => !IsBusy);
            UpgradeAppsCommand = new RelayCommand(UpgradeAppsAsync, () => !IsBusy);
            InstallWindowsCommand = new RelayCommand(InstallWindowsAsync, () => !IsBusy);
        }

        public ObservableCollection<AppPackageUpdate> AppUpdates { get; }
        public ObservableCollection<WindowsUpdateItem> WindowsUpdates { get; }
        public ICommand CheckCommand { get; }
        public ICommand UpgradeAppsCommand { get; }
        public ICommand InstallWindowsCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                SetProperty(ref _isBusy, value);
                ((RelayCommand)CheckCommand).RaiseCanExecuteChanged();
                ((RelayCommand)UpgradeAppsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)InstallWindowsCommand).RaiseCanExecuteChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string LogText
        {
            get => _logText;
            private set => SetProperty(ref _logText, value);
        }

        public string AppCountText => AppUpdates.Count == 0 ? "No app updates found" : $"{AppUpdates.Count} app update{(AppUpdates.Count == 1 ? "" : "s")}";
        public string WindowsCountText => WindowsUpdates.Count == 0 ? "No Windows updates found" : $"{WindowsUpdates.Count} Windows update{(WindowsUpdates.Count == 1 ? "" : "s")}";

        private async Task CheckAsync()
        {
            IsBusy = true;
            try
            {
                await CheckCoreAsync();
            }
            catch (Exception ex)
            {
                StatusText = "The update check could not finish.";
                LogText = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CheckCoreAsync()
        {
            StatusText = "Looking for app and Windows updates…";
            LogText = "Checking winget, then Windows Update.";
            AppUpdates.Clear();
            WindowsUpdates.Clear();
            NotifyCounts();

            if (!await AppUpdateService.IsWingetAvailableAsync())
            {
                LogText = "winget is not available. Install App Installer from the Microsoft Store, then check again.";
            }
            else
            {
                var apps = await AppUpdateService.FindAppUpdatesAsync();
                foreach (var app in apps) AppUpdates.Add(app);
            }

            StatusText = "Checking Windows Update…";
            var windowsUpdates = await AppUpdateService.FindWindowsUpdatesAsync();
            foreach (var update in windowsUpdates) WindowsUpdates.Add(update);
            NotifyCounts();
            StatusText = $"{AppCountText}. {WindowsCountText}.";
        }

        private async Task UpgradeAppsAsync()
        {
            IsBusy = true;
            StatusText = "Updating every app winget can upgrade, then asking the Microsoft Store to update its apps. This can take a while.";
            try
            {
                var result = await AppUpdateService.UpgradeAllAppsAsync();
                LogText = string.IsNullOrWhiteSpace(result.Output) ? result.Cancelled ? "Administrator approval was cancelled." : "winget finished without any output." : result.Output;
                StatusText = result.Cancelled ? "App updates were cancelled." : result.Success ? "App updates finished." : "App updates finished with errors. The log has the details.";
                if (!result.Cancelled)
                {
                    var finished = StatusText;
                    var log = LogText;
                    await CheckCoreAsync();
                    LogText = log;
                    StatusText = $"{finished} {AppCountText}. {WindowsCountText}.";
                }
            }
            catch (Exception ex)
            {
                StatusText = "App updates could not finish.";
                LogText = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task InstallWindowsAsync()
        {
            IsBusy = true;
            StatusText = "Installing Windows updates. This can take a while.";
            try
            {
                var result = await AppUpdateService.InstallWindowsUpdatesAsync();
                LogText = string.IsNullOrWhiteSpace(result.Output) ? "Windows Update finished without any output." : result.Output;
                var reboot = result.Output.Contains("WU|RESULT|", StringComparison.Ordinal) && result.Output.Contains("|True", StringComparison.Ordinal);
                StatusText = result.Cancelled
                    ? "Windows Update was cancelled."
                    : reboot
                        ? "Windows updates installed. Restart this PC to finish."
                        : result.Success ? "Windows updates finished." : "Windows Update finished with errors. The log has the details.";
            }
            catch (Exception ex)
            {
                StatusText = "Windows Update could not finish.";
                LogText = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void NotifyCounts()
        {
            OnPropertyChanged(nameof(AppCountText));
            OnPropertyChanged(nameof(WindowsCountText));
        }
    }
}
