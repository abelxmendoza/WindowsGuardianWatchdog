using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Cleanup;
using Microsoft.UI.Dispatching;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class CleanupViewModel : ViewModelBase
    {
        private readonly CleanupOrchestrator _orchestrator;
        private CleanupReport? _currentReport;
        private bool _isScanning;
        private bool _isCleaning;
        private string _statusText = "Ready";
        private int _progress;

        public CleanupViewModel()
        {
            _orchestrator = new CleanupOrchestrator();
            CleanupItems = new ObservableCollection<CleanupItemViewModel>();
            
            _orchestrator.ProgressUpdated += Orchestrator_ProgressUpdated;
            _orchestrator.CleanupCompleted += Orchestrator_CleanupCompleted;
        }

        public ObservableCollection<CleanupItemViewModel> CleanupItems { get; }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool IsCleaning
        {
            get => _isCleaning;
            set => SetProperty(ref _isCleaning, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public long TotalSize => CleanupItems.Sum(i => i.Size);

        public string TotalSizeFormatted => FormatBytes(TotalSize);

        public async Task ScanAsync()
        {
            IsScanning = true;
            StatusText = "Scanning...";
            CleanupItems.Clear();

            try
            {
                _currentReport = await _orchestrator.ScanAllAsync();
                
                foreach (var result in _currentReport.Results)
                {
                    foreach (var item in result.Items)
                    {
                        CleanupItems.Add(new CleanupItemViewModel
                        {
                            Path = item.Path,
                            Category = item.Category,
                            Size = item.Size,
                            LastModified = item.LastModified,
                            Type = item.Type.ToString()
                        });
                    }
                }

                StatusText = $"Found {CleanupItems.Count} items ({TotalSizeFormatted})";
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

        public async Task CleanAsync(bool dryRun)
        {
            if (_currentReport == null) return;

            IsCleaning = true;
            StatusText = dryRun ? "Dry run..." : "Cleaning...";

            try
            {
                await _orchestrator.ExecuteCleanupAsync(_currentReport, dryRun);
                StatusText = dryRun ? "Dry run completed" : "Cleanup completed";
                
                if (!dryRun)
                {
                    CleanupItems.Clear();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsCleaning = false;
            }
        }

        private void Orchestrator_ProgressUpdated(object? sender, CleanupProgress progress)
        {
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                StatusText = $"{progress.Category}: {progress.Status}";
                Progress = progress.Progress;
            });
        }

        private void Orchestrator_CleanupCompleted(object? sender, CleanupResult result)
        {
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                StatusText = "Cleanup completed";
            });
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class CleanupItemViewModel
    {
        public string Path { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public abstract class ViewModelBase
    {
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

