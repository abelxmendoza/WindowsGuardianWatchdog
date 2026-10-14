using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.ThreatIntel;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class ThreatDetectionView : Page
    {
        private readonly BlueTeamDetector _detector;
        private readonly ObservableCollection<ThreatDetection> _threats;
        private readonly ObservableCollection<ExploitAwareness> _exploits = new();
        private bool _catalogBusy;

        public ThreatDetectionView()
        {
            this.InitializeComponent();
            _detector = new BlueTeamDetector();
            _threats = new ObservableCollection<ThreatDetection>();
            ThreatsListView.ItemsSource = _threats;
            ExploitListView.ItemsSource = _exploits;
            _detector.ThreatDetected += Detector_ThreatDetected;
            Loaded += async (_, _) => await LoadCachedCatalogAsync();
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

        private async Task LoadCachedCatalogAsync()
        {
            if (ExploitDbCatalog.TryLoadCache()) ShowCatalogStatus("Cached Exploit-DB catalog loaded.");
            else CatalogStatusText.Text = "Catalog not loaded. Update it to download the public index.";
            await Task.CompletedTask;
        }

        private async void RefreshCatalogButton_Click(object sender, RoutedEventArgs e) => await RunCatalogAsync(async () =>
        {
            CatalogStatusText.Text = "Downloading the Exploit-DB catalog…";
            var count = await ExploitDbCatalog.RefreshAsync(force: true);
            ShowCatalogStatus($"Catalog updated. {count:N0} public entries.");
        });

        private async void MatchPcButton_Click(object sender, RoutedEventArgs e) => await RunCatalogAsync(async () =>
        {
            await EnsureCatalogAsync();
            var matches = await Task.Run(() => ExploitDbCatalog.MatchInstalledSoftware());
            ShowExploits(matches);
            CatalogStatusText.Text = matches.Count == 0
                ? $"{ExploitDbCatalog.Count:N0} entries loaded. No installed app matched a Windows entry."
                : $"{matches.Count} installed app match{(matches.Count == 1 ? "" : "es")} in the catalog.";
        });

        private async void SearchCatalogButton_Click(object sender, RoutedEventArgs e) => await RunCatalogAsync(async () =>
        {
            await EnsureCatalogAsync();
            var query = CatalogSearchBox.Text ?? string.Empty;
            var matches = await Task.Run(() => ExploitDbCatalog.Search(query));
            ShowExploits(matches);
            CatalogStatusText.Text = string.IsNullOrWhiteSpace(query)
                ? "Enter a product name or CVE."
                : $"{matches.Count} catalog result{(matches.Count == 1 ? "" : "s")} for \"{query}\".";
        });

        private async Task EnsureCatalogAsync()
        {
            if (ExploitDbCatalog.IsLoaded) return;
            if (!ExploitDbCatalog.TryLoadCache())
            {
                CatalogStatusText.Text = "Downloading the Exploit-DB catalog…";
                await ExploitDbCatalog.RefreshAsync();
            }
        }

        private async Task RunCatalogAsync(Func<Task> action)
        {
            if (_catalogBusy) return;
            _catalogBusy = true;
            CatalogProgress.Visibility = Visibility.Visible;
            RefreshCatalogButton.IsEnabled = false;
            MatchPcButton.IsEnabled = false;
            SearchCatalogButton.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                CatalogStatusText.Text = "Exploit-DB could not be reached. " + ex.Message;
            }
            finally
            {
                CatalogProgress.Visibility = Visibility.Collapsed;
                RefreshCatalogButton.IsEnabled = true;
                MatchPcButton.IsEnabled = true;
                SearchCatalogButton.IsEnabled = true;
                _catalogBusy = false;
            }
        }

        private void ShowExploits(IEnumerable<ExploitAwareness> matches)
        {
            _exploits.Clear();
            foreach (var match in matches) _exploits.Add(match);
        }

        private void ShowCatalogStatus(string prefix)
        {
            var when = ExploitDbCatalog.RefreshedUtc?.ToLocalTime().ToString("g") ?? "unknown";
            CatalogStatusText.Text = $"{prefix} Last refresh {when}.";
        }
    }
}
