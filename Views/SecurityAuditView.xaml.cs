using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class SecurityAuditView : Page
    {
        private readonly SecurityAudit _audit;
        private readonly ObservableCollection<AuditFinding> _findings;

        public SecurityAuditView()
        {
            this.InitializeComponent();
            _audit = new SecurityAudit();
            _findings = new ObservableCollection<AuditFinding>();
            FindingsListView.ItemsSource = _findings;
        }

        private async void RunAuditButton_Click(object sender, RoutedEventArgs e)
        {
            ScoreText.Text = "Running...";
            _findings.Clear();
            
            var report = await _audit.PerformFullAuditAsync();
            
            ScoreText.Text = $"{report.Score}/100";
            var color = report.Score >= 75 
                ? Microsoft.UI.Colors.Green 
                : report.Score >= 50 
                    ? Microsoft.UI.Colors.Orange 
                    : Microsoft.UI.Colors.Red;
            ScoreText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            
            foreach (var finding in report.Findings)
            {
                _findings.Add(finding);
            }
        }
    }
}

