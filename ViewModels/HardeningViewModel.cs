using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WindowsGuardianWatchdog.Core.Hardening;
using WindowsGuardianWatchdog.Services;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class HardeningViewModel : ViewModelBase
    {
        private readonly HardeningService _service;
        private bool _isApplying;
        private bool _isRollingBack;
        private string _statusText = "Checking this PC…";
        private string _defenderStatus = "Checking Microsoft Defender…";
        private string _summaryText = "Reading the current baseline.";

        public HardeningViewModel()
        {
            _service = new HardeningService();
            Rules = new ObservableCollection<HardeningRuleItem>(
                _service.GetAvailableRules().Select(rule => new HardeningRuleItem
                {
                    Id = rule.Id,
                    Name = rule.Name,
                    Description = rule.Description,
                    Category = rule.Category,
                    Severity = rule.Severity.ToString(),
                    RequiresAdmin = rule.RequiresAdmin,
                    IsSelected = rule.Recommended,
                    Status = "Checking"
                }));
            Results = new ObservableCollection<HardeningResult>();

            ApplySelectedCommand = new RelayCommand(async () => await ApplyAsync(selectedOnly: true), CanRun);
            ApplyAllCommand = new RelayCommand(async () => await ApplyAsync(selectedOnly: false), CanRun);
            VerifyCommand = new RelayCommand(async () => await VerifyAsync(), CanRun);

            _ = InitializeAsync();
        }

        public ObservableCollection<HardeningRuleItem> Rules { get; }
        public ObservableCollection<HardeningResult> Results { get; }
        public ICommand ApplySelectedCommand { get; }
        public ICommand ApplyAllCommand { get; }
        public ICommand VerifyCommand { get; }

        public bool IsBusy => _isApplying || _isRollingBack;

        public bool IsApplying
        {
            get => _isApplying;
            set
            {
                SetProperty(ref _isApplying, value);
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommands();
            }
        }

        public bool IsRollingBack
        {
            get => _isRollingBack;
            set
            {
                SetProperty(ref _isRollingBack, value);
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommands();
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

        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public async Task RollbackAsync()
        {
            IsRollingBack = true;
            StatusText = "Rolling back the baseline. Approve the administrator prompt.";
            try
            {
                var results = await _service.RollbackAllAsync();
                ShowResults(results);
                var rolled = results.Count(result => result.Success);
                StatusText = rolled == 0
                    ? results.FirstOrDefault()?.Error ?? "Nothing was rolled back."
                    : $"Rolled back {rolled} control{(rolled == 1 ? "" : "s")}.";
                await RefreshStateAsync();
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsRollingBack = false;
            }
        }

        private async Task InitializeAsync()
        {
            await Task.WhenAll(LoadDefenderStatusAsync(), VerifyAsync());
        }

        private async Task LoadDefenderStatusAsync()
        {
            DefenderStatus = await _service.GetDefenderStatusAsync();
        }

        private async Task ApplyAsync(bool selectedOnly)
        {
            var ids = selectedOnly
                ? Rules.Where(rule => rule.IsSelected).Select(rule => rule.Id).ToList()
                : Rules.Select(rule => rule.Id).ToList();
            if (ids.Count == 0)
            {
                StatusText = "Select at least one control, or apply the full baseline.";
                return;
            }

            IsApplying = true;
            StatusText = "Applying the baseline. Approve the administrator prompt.";
            try
            {
                var results = selectedOnly
                    ? await _service.ApplySelectedHardeningAsync(ids)
                    : await _service.ApplyAllHardeningAsync();
                ShowResults(results);
                var applied = results.Count(result => result.Success);
                StatusText = $"Applied {applied} of {results.Count} controls.";
                await RefreshStateAsync();
                await LoadDefenderStatusAsync();
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsApplying = false;
            }
        }

        private async Task VerifyAsync()
        {
            if (IsBusy) return;
            IsApplying = true;
            StatusText = "Checking which controls are already on…";
            try
            {
                await RefreshStateAsync();
                StatusText = SummaryText;
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsApplying = false;
            }
        }

        private async Task RefreshStateAsync()
        {
            var results = await _service.VerifyAllAsync();
            foreach (var result in results)
            {
                var rule = Rules.FirstOrDefault(item => item.Id == result.RuleId);
                if (rule != null) rule.Status = result.StatusText;
            }

            var active = results.Count(result => result.Success);
            SummaryText = $"{active} of {results.Count} controls are on.";
        }

        private void ShowResults(IEnumerable<HardeningResult> results)
        {
            Results.Clear();
            foreach (var result in results)
            {
                var rule = Rules.FirstOrDefault(item => item.Id == result.RuleId);
                if (rule != null) rule.Status = result.StatusText;
                Results.Add(result);
            }
        }

        private bool CanRun() => !IsBusy;

        private void RaiseCommands()
        {
            ((RelayCommand)ApplySelectedCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApplyAllCommand).RaiseCanExecuteChanged();
            ((RelayCommand)VerifyCommand).RaiseCanExecuteChanged();
        }
    }

    public class HardeningRuleItem : ViewModelBase
    {
        private bool _isSelected;
        private string _status = "Checking";

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool RequiresAdmin { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
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
