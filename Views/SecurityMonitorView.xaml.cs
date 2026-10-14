using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class SecurityMonitorView : Page
    {
        private readonly SecurityMonitor _monitor;
        private readonly ObservableCollection<ProcessInfo> _processes;

        public SecurityMonitorView()
        {
            this.InitializeComponent();
            _monitor = new SecurityMonitor();
            _processes = new ObservableCollection<ProcessInfo>();
            ProcessesListView.ItemsSource = _processes;
            _monitor.ProcessDetected += Monitor_ProcessDetected;
            Loaded += async (_, _) => await RefreshProcessesAsync();
        }

        private void Monitor_ProcessDetected(object? sender, ProcessInfo process)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var existing = _processes.FirstOrDefault(p => p.Id == process.Id);
                if (existing != null)
                {
                    var index = _processes.IndexOf(existing);
                    _processes[index] = process;
                }
                else
                {
                    _processes.Add(process);
                }
            });
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _monitor.StartMonitoring();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _monitor.StopMonitoring();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshProcessesAsync();

        private async Task RefreshProcessesAsync()
        {
            ProcessStatusText.Text = "Reading processes…";
            var snapshot = await Task.Run(() => Process.GetProcesses()
                .Select(process =>
                {
                    try
                    {
                        var info = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            MemoryUsage = process.WorkingSet64,
                            PriorityText = ProcessInfo.ReadPriority(process),
                            Importance = ProcessInfo.DescribeImportance(process),
                            IsProtected = ProcessInfo.IsProtectedProcess(process.ProcessName) || process.Id == Environment.ProcessId
                        };
                        try { info.Path = process.MainModule?.FileName ?? string.Empty; } catch { info.Path = string.Empty; }
                        return info;
                    }
                    catch
                    {
                        return new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            Importance = ProcessInfo.IsProtectedProcess(process.ProcessName) ? "Protected" : "System",
                            IsProtected = true,
                            PriorityText = "System"
                        };
                    }
                })
                .OrderByDescending(process => process.MemoryUsage)
                .ToList());

            _processes.Clear();
            foreach (var process in snapshot) _processes.Add(process);
            var protectedCount = snapshot.Count(process => process.IsProtected);
            ProcessStatusText.Text = $"{snapshot.Count} processes. {protectedCount} are protected and will not be ended in bulk.";
        }

        private async void EndSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProcessesListView.SelectedItems.OfType<ProcessInfo>().Where(process => !process.IsProtected).Select(process => process.Id).ToList();
            if (selected.Count == 0)
            {
                ProcessStatusText.Text = "Select one or more processes that are not marked Protected.";
                return;
            }

            foreach (var id in selected) EndProcess(id);
            await RefreshProcessesAsync();
        }

        private async void EndNonessentialButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "End nonessential processes?",
                Content = "This ends the processes that are not required to keep Windows, security services, and this app running. Protected processes stay open.",
                PrimaryButtonText = "End them",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var targets = _processes.Where(process => !process.IsProtected).Select(process => process.Id).ToList();
            foreach (var id in targets) EndProcess(id);
            await RefreshProcessesAsync();
        }

        private void EndProcess(int processId)
        {
            if (_processes.FirstOrDefault(process => process.Id == processId)?.IsProtected == true) return;
            try
            {
                var process = Process.GetProcessById(processId);
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1500)) process.Kill();
                }
                catch
                {
                    process.Kill();
                }
            }
            catch (ArgumentException)
            {
                // The process already exited.
            }
            catch (Exception ex)
            {
                ProcessStatusText.Text = $"Could not end process {processId}. {ex.Message}";
            }
        }

        private void EndTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int processId)
            {
                EndProcess(processId);
                _ = RefreshProcessesAsync();
            }
        }
    }
}

