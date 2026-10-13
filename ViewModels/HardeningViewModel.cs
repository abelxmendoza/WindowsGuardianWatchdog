using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WindowsGuardianWatchdog.Core.Hardening;
using WindowsGuardianWatchdog.Services;
using Microsoft.UI.Dispatching;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class HardeningViewModel : ViewModelBase
    {
        private readonly HardeningService _service;
        private bool _isApplying;
        private bool _isRollingBack;
        private string _statusText = "Ready";
        private string _defenderStatus = "Checking...";

        public HardeningViewModel()
        {
            _service = new HardeningService();
            Results = new ObservableCollection<HardeningResult>();
            AvailableRules = new ObservableCollection<HardeningRule>(_service.GetAvailableRules());
            SelectedRuleIds = new ObservableCollection<string>();

            ApplyHardeningCommand = new RelayCommand(async () => await ApplyHardeningAsync(), () => !_isApplying && !_isRollingBack);
            RollbackCommand = new RelayCommand(async () => await RollbackAsync(), () => !_isApplying && !_isRollingBack);
            VerifyCommand = new RelayCommand(async () => await VerifyAsync(), () => !_isApplying && !_isRollingBack);

            _ = LoadDefenderStatusAsync();
        }

        public ObservableCollection<HardeningResult> Results { get; }
        public ObservableCollection<HardeningRule> AvailableRules { get; }
        public ObservableCollection<string> SelectedRuleIds { get; }

        public ICommand ApplyHardeningCommand { get; }
        public ICommand RollbackCommand { get; }
        public ICommand VerifyCommand { get; }

        public bool IsApplying
        {
            get => _isApplying;
            set
            {
                SetProperty(ref _isApplying, value);
                ((RelayCommand)ApplyHardeningCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RollbackCommand).RaiseCanExecuteChanged();
                ((RelayCommand)VerifyCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsRollingBack
        {
            get => _isRollingBack;
            set
            {
                SetProperty(ref _isRollingBack, value);
                ((RelayCommand)ApplyHardeningCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RollbackCommand).RaiseCanExecuteChanged();
                ((RelayCommand)VerifyCommand).RaiseCanExecuteChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string DefenderStatus
        {
            get => _defenderStatus;
            set => SetProperty(ref _defenderStatus, value);
        }

        private async Task LoadDefenderStatusAsync()
        {
            var status = await _service.GetDefenderStatusAsync();
            DefenderStatus = status;
        }

        private async Task ApplyHardeningAsync()
        {
            IsApplying = true;
            StatusText = "Applying security hardening...";

            try
            {
                List<HardeningResult> results;

                if (SelectedRuleIds.Count > 0)
                {
                    results = await _service.ApplySelectedHardeningAsync(SelectedRuleIds);
                }
                else
                {
                    results = await _service.ApplyAllHardeningAsync();
                }

                Results.Clear();
                foreach (var result in results)
                {
                    Results.Add(result);
                }

                var successCount = results.Count(r => r.Success);
                var totalCount = results.Count;
                StatusText = $"Applied {successCount} of {totalCount} hardening rules successfully";

                // Refresh defender status
                await LoadDefenderStatusAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsApplying = false;
            }
        }

        private async Task RollbackAsync()
        {
            IsRollingBack = true;
            StatusText = "Rolling back changes...";

            try
            {
                var results = await _service.RollbackAllAsync();

                Results.Clear();
                foreach (var result in results)
                {
                    Results.Add(result);
                }

                var successCount = results.Count(r => r.Success);
                StatusText = $"Rolled back {successCount} hardening rules";

                // Refresh defender status
                await LoadDefenderStatusAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsRollingBack = false;
            }
        }

        private async Task VerifyAsync()
        {
            IsApplying = true;
            StatusText = "Verifying hardening status...";

            try
            {
                var results = await _service.VerifyAllAsync();

                Results.Clear();
                foreach (var result in results)
                {
                    Results.Add(result);
                }

                var enabledCount = results.Count(r => r.Success);
                var totalCount = results.Count;
                StatusText = $"{enabledCount} of {totalCount} hardening rules are active";

                // Refresh defender status
                await LoadDefenderStatusAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsApplying = false;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter)
        {
            await _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

