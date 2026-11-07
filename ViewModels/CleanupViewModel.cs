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
        private readonly DispatcherQueue? _dispatcher;
        private CleanupReport? _currentReport;
        private bool _isScanning;
        private bool _isCleaning;
        private string _statusText = "Ready";
        private int _progress;

        public CleanupViewModel()
        {
            _orchestrator = new CleanupOrchestrator();
            CleanupItems = new ObservableCollection<CleanupItemViewModel>();
            
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _orchestrator.ProgressUpdated += Orchestrator_ProgressUpdated;
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
                            Category = string.IsNullOrEmpty(item.Description) ? item.Category : $"{item.Category} · {item.Description}",
                            Size = item.Size,
                            SizeText = FormatBytes(item.Size),
                            LastModified = item.LastModified,
                            Type = item.Type.ToString()
                        });
                    }
                }

                StatusText = CleanupItems.Count == 0
                    ? "Nothing to clean up. This PC is already tidy."
                    : $"Found {CleanupItems.Count} places to clean, about {TotalSizeFormatted}. Nothing has been deleted yet.";
                OnPropertyChanged(nameof(TotalSizeFormatted));
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
            if (_currentReport == null)
            {
                StatusText = "Choose \"1. Look first\" so there is a list to work from.";
                return;
            }

            IsCleaning = true;
            StatusText = dryRun ? "Working out what would be removed…" : "Cleaning…";

            try
            {
                var outcome = await _orchestrator.ExecuteCleanupAsync(_currentReport, dryRun);
                if (outcome.Preview)
                {
                    StatusText = $"Preview: cleaning up would remove about {FormatBytes(outcome.FreedBytes)} from the {CleanupItems.Count} places listed. Nothing was deleted.";
                    return;
                }

                var skipped = outcome.Skipped == 0
                    ? string.Empty
                    : $" {outcome.Skipped} file{(outcome.Skipped == 1 ? " was" : "s were")} in use and left alone; close your browsers and other apps, then try again to get those too.";
                StatusText = $"Freed {FormatBytes(outcome.FreedBytes)} ({outcome.Removed} files).{skipped}";
                CleanupItems.Clear();
                _currentReport = null;
                OnPropertyChanged(nameof(TotalSizeFormatted));
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
            void Apply()
            {
                StatusText = $"{progress.Category}: {progress.Status}";
                Progress = progress.Progress;
            }

            if (_dispatcher == null || _dispatcher.HasThreadAccess) Apply();
            else _dispatcher.TryEnqueue(Apply);
        }

        public static string FormatBytes(long bytes)
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
        public string SizeText { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Base for the pages' view models. It must implement INotifyPropertyChanged: without the interface WinUI reads
    /// each {Binding} once and never again, which is how status lines like "Scanning…" or "Cleanup completed" never
    /// showed up. Changes made on a background thread are passed to the UI thread, which WinUI requires.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly DispatcherQueue? _uiQueue = DispatcherQueue.GetForCurrentThread();

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
            if (_uiQueue == null || _uiQueue.HasThreadAccess)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            else
                _uiQueue.TryEnqueue(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
