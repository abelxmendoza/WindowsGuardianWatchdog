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
            var ranked = _service.GetAvailableRules()
                .Select(rule => new HardeningRuleItem
                {
                    Id = rule.Id,
                    Name = rule.Name,
                    Description = rule.Description,
                    Why = "Why: " + WhyFor(rule.Id),
                    Category = rule.Category,
                    Severity = rule.Severity.ToString(),
                    Priority = PriorityFor(rule),
                    RankWeight = rule.Recommended ? (rule.Severity == HardeningSeverity.Critical ? 0 : 1) : 2,
                    RequiresAdmin = rule.RequiresAdmin,
                    IsSelected = rule.Recommended,
                    Status = "Checking"
                })
                .OrderBy(rule => rule.RankWeight)
                .ThenBy(rule => rule.Name)
                .ToList();
            for (var index = 0; index < ranked.Count; index++) ranked[index].Rank = (index + 1).ToString();
            Rules = new ObservableCollection<HardeningRuleItem>(ranked);
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

        private static string PriorityFor(HardeningRule rule)
        {
            if (!rule.Recommended) return "Careful";
            return rule.Severity == HardeningSeverity.Critical ? "Do this first" : "Recommended";
        }

        private static string WhyFor(string id) => id switch
        {
            "DEFENDER_REALTIME" => "If this is off, a bad file can open before Windows has a chance to stop it.",
            "DEFENDER_PUA" => "This blocks the extra junk some downloads try to install, such as adware.",
            "DEFENDER_CLOUD" => "A brand-new bad file is easier to recognize when Windows can ask Microsoft about it. Your files are not uploaded.",
            "DEFENDER_ASR" => "This blocks a set of tricks used against Office and scripts. Some older apps can stop working, so it is off unless you choose it.",
            "DEFENDER_CONTROLLED_FOLDER" => "This stops unknown apps from changing Documents and Pictures. An app you trust may need permission afterward.",
            "FIREWALL_ENABLE" => "The firewall is the door between this PC and other computers. It should stay shut to strangers.",
            "UAC_ENABLE" => "This is the prompt that asks before a program changes Windows. It stops quiet changes.",
            "SMARTSCREEN" => "This is the warning you see before Windows runs an app it does not recognize.",
            "AUTORUN_DISABLE" => "A USB stick should not be able to start a program just because you plugged it in.",
            "SMB1_DISABLE" => "This turns off an old way computers share files. Almost nobody needs it, and it is a common way in.",
            "WINDOWS_UPDATE_AUTO" => "Windows can install its own fixes while you are away, so the PC does not stay behind.",
            "GUEST_DISABLE" => "The Guest account is an extra door into the PC. Most people never use it.",
            "REMOTE_ASSISTANCE_DISABLE" => "This stops someone from asking to control the PC through Windows Remote Assistance.",
            _ => "This is one of Windows' built-in safety switches."
        };

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
        public string Priority { get; set; } = "Recommended";
        public string Why { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public int RankWeight { get; set; }
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
