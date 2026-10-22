using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core.Network;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class ExposureView : Page
    {
        private ExposureReport? _report;

        public ExposureView()
        {
            this.InitializeComponent();
            Loaded += async (_, _) => await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Asking Windows what is listening…";
            try
            {
                _report = await ExposureInventory.TakeAsync();
                StatusText.Text = $"Looked at {_report.TakenAt:T}. {_report.Services.Count} listening programs found.";
                Render();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not read the list: {ex.Message}";
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private void Render()
        {
            if (_report == null) return;
            ReachableText.Text = _report.ReachableCount.ToString();
            RiskyText.Text = _report.RiskyCount.ToString();
            RiskyText.Foreground = _report.RiskyCount > 0 ? CardKit.Warning : CardKit.Good;
            var off = _report.FirewallProfiles.Where(p => p.Value == false).Select(p => p.Key).ToList();
            var unknown = _report.FirewallProfiles.Where(p => p.Value == null).Select(p => p.Key).ToList();
            FirewallText.Text = off.Count > 0 ? $"Off for {string.Join(", ", off)}"
                : unknown.Count > 0 ? $"Could not read {string.Join(", ", unknown)}"
                : "On for every network";
            FirewallText.Foreground = off.Count > 0 ? CardKit.Critical : unknown.Count > 0 ? CardKit.Warning : CardKit.Good;

            ServicesPanel.Children.Clear();
            var shown = _report.Services.Where(s => ShowLocalSwitch.IsOn || s.Reach != ExposureReach.ThisPcOnly).ToList();
            if (shown.Count == 0)
            {
                ServicesPanel.Children.Add(CardKit.Empty("Nothing on this PC accepts connections from other devices."));
                return;
            }

            foreach (var service in shown)
            {
                var accent = service.IsRisky ? CardKit.Warning : service.Reach == ExposureReach.ThisPcOnly ? CardKit.Muted : CardKit.Good;
                var label = service.IsRisky ? "Worth a look" : service.Reach == ExposureReach.ThisPcOnly ? "This PC only" : "Reachable";
                ServicesPanel.Children.Add(CardKit.Card(service.Title, accent, label,
                    service.ReachText + $" (listening on {service.Address}).",
                    $"Program: {service.ProcessName} (process {service.ProcessId})",
                    service.Advice));
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void ShowLocalSwitch_Toggled(object sender, RoutedEventArgs e) => Render();
    }
}
