using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class ThreatDetectionView : Page
    {
        private readonly BlueTeamDetector _detector;
        private readonly ObservableCollection<ThreatDetection> _threats;

        public ThreatDetectionView()
        {
            this.InitializeComponent();
            _detector = new BlueTeamDetector();
            _threats = new ObservableCollection<ThreatDetection>();
            ThreatsListView.ItemsSource = _threats;
            
            _detector.ThreatDetected += Detector_ThreatDetected;
        }

        private void Detector_ThreatDetected(object? sender, ThreatDetection threat)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _threats.Insert(0, threat);
            });
        }

        private async void ThreatHuntButton_Click(object sender, RoutedEventArgs e)
        {
            ThreatReportText.Text = "Running threat hunt...";
            _threats.Clear();
            
            var report = await _detector.PerformThreatHuntAsync();
            
            ThreatReportText.Text = $"Threat hunt completed. Found {report.ThreatCount} threats. " +
                                   $"Severity: {report.Severity}";
        }
    }
}

