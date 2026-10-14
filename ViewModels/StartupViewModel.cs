using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Systems;
using WindowsGuardianWatchdog.ViewModels;
using Microsoft.UI.Dispatching;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class StartupViewModel : ViewModelBase
    {
        private readonly StartupManager _manager;
        private bool _isLoading;
        private string _statusText = "Ready";

        public StartupViewModel()
        {
            _manager = new StartupManager();
            StartupItems = new ObservableCollection<StartupItemViewModel>();
        }

        public ObservableCollection<StartupItemViewModel> StartupItems { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusText = "Loading startup items...";
            StartupItems.Clear();

            try
            {
                var items = await _manager.ListAsync();
                
                foreach (var item in items)
                {
                    StartupItems.Add(new StartupItemViewModel
                    {
                        Name = item.Name,
                        Command = item.Command,
                        Location = item.Location,
                        Type = item.Type.ToString(),
                        IsEnabled = item.IsEnabled
                    });
                }

                StatusText = $"Found {StartupItems.Count} startup items";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task DisableAsync(StartupItemViewModel item)
        {
            try
            {
                var startupItem = new StartupItem
                {
                    Name = item.Name,
                    Command = item.Command,
                    Location = item.Location,
                    Type = Enum.Parse<StartupItemType>(item.Type),
                    IsEnabled = item.IsEnabled
                };

                var success = await _manager.DisableAsync(startupItem);
                if (success)
                {
                    item.IsEnabled = false;
                    StatusText = $"Disabled: {item.Name}";
                }
                else
                {
                    StatusText = $"Could not disable {item.Name}. All-users startup entries need administrator approval.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }
    }

    public class StartupItemViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled;

        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

