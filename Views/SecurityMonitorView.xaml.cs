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

        private void EndTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int processId)
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    
                    // Try graceful shutdown first
                    try
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(2000)) // Wait 2 seconds
                        {
                            // Force kill if graceful shutdown failed
                            process.Kill();
                        }
                    }
                    catch
                    {
                        // If CloseMainWindow fails, force kill
                        process.Kill();
                    }
                    
                    // Remove from list
                    var processInfo = _processes.FirstOrDefault(p => p.Id == processId);
                    if (processInfo != null)
                    {
                        _processes.Remove(processInfo);
                    }
                }
                catch (ArgumentException)
                {
                    // Process already terminated
                    var processInfo = _processes.FirstOrDefault(p => p.Id == processId);
                    if (processInfo != null)
                    {
                        _processes.Remove(processInfo);
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Access denied - show error
                    var dialog = new ContentDialog
                    {
                        Title = "Access Denied",
                        Content = $"Cannot end task: {ex.Message}\n\nYou may need to run as Administrator to end this process.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to end task: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
        }
    }
}

