using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class FileIntegrityView : Page
    {
        private readonly FileIntegrityMonitor _monitor;
        private readonly ObservableCollection<FileIntegrityAlert> _violations;

        public FileIntegrityView()
        {
            this.InitializeComponent();
            _monitor = new FileIntegrityMonitor();
            _violations = new ObservableCollection<FileIntegrityAlert>();
            ViolationsListView.ItemsSource = _violations;
            
            _monitor.IntegrityViolation += Monitor_IntegrityViolation;
        }

        private void Monitor_IntegrityViolation(object? sender, FileIntegrityAlert alert)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _violations.Insert(0, alert);
            });
        }

        private void AddPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PathTextBox.Text))
            {
                _monitor.AddMonitoredPath(PathTextBox.Text);
                PathTextBox.Text = string.Empty;
            }
        }

        private async void CreateBaselineButton_Click(object sender, RoutedEventArgs e)
        {
            await _monitor.CreateBaselineAsync();
        }

        private void StartMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _monitor.StartMonitoring();
            StartMonitorButton.IsEnabled = false;
            StopMonitorButton.IsEnabled = true;
        }

        private void StopMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _monitor.StopMonitoring();
            StartMonitorButton.IsEnabled = true;
            StopMonitorButton.IsEnabled = false;
        }
    }
}

