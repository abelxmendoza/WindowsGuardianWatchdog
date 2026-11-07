using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.Defender;
using WindowsGuardianWatchdog.Core.Incidents;
using WindowsGuardianWatchdog.Core.ThreatIntel;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class ThreatDetectionView : Page
    {
        private readonly BlueTeamDetector _detector;
        private readonly ObservableCollection<ThreatDetection> _threats;
        private readonly ObservableCollection<ExploitAwareness> _exploits = new();
        private readonly DefenderControl _defender = new();
        private bool _catalogBusy;
        private bool _defenderBusy;

        public ThreatDetectionView()
        {
            this.InitializeComponent();
            _detector = new BlueTeamDetector();
            _threats = new ObservableCollection<ThreatDetection>();
            ThreatsListView.ItemsSource = _threats;
            ExploitListView.ItemsSource = _exploits;
            _detector.ThreatDetected += Detector_ThreatDetected;
            Loaded += async (_, _) =>
            {
                await LoadCachedCatalogAsync();
                await RefreshDefenderAsync();
            };
        }

        private async Task RefreshDefenderAsync()
        {
            var status = await _defender.GetStatusAsync();
            DefenderSummaryText.Text = DescribeDefender(status);
            try
            {
                ShowDefenderThreats(await _defender.GetThreatsAsync());
            }
            catch (Exception ex)
            {
                DefenderThreatsPanel.Children.Clear();
                DefenderThreatsPanel.Children.Add(CardKit.Empty(ex.Message));
            }
        }

        private static string DescribeDefender(DefenderControl.DefenderStatus status)
        {
            if (!status.Available)
                return $"Microsoft Defender did not answer ({status.Problem}). If you use another antivirus, scan with that instead.";
            if (status.RunningMode.Contains("Passive", StringComparison.OrdinalIgnoreCase))
                return "Another antivirus is in charge, so Defender only runs scans you ask for here.";
            var protection = status.RealTimeProtectionEnabled ? "Real-time protection is on" : "Real-time protection is OFF";
            var lastScan = status.LastQuickScan?.ToLocalTime().ToString("g") ?? "never";
            var definitions = status.SignatureAgeDays switch
            {
                < 0 => "definitions age unknown",
                0 => "definitions updated today",
                1 => "definitions updated yesterday",
                var days => $"definitions are {days} days old"
            };
            return $"{protection}. Last quick scan: {lastScan}. Virus {definitions} (version {status.AntivirusSignatureVersion}).";
        }

        private void ShowDefenderThreats(IReadOnlyList<DefenderThreat> threats)
        {
            DefenderThreatsPanel.Children.Clear();
            if (threats.Count == 0)
            {
                DefenderThreatsPanel.Children.Add(CardKit.Empty("Defender has not found anything on this PC."));
                RemoveThreatsButton.Visibility = Visibility.Collapsed;
                return;
            }

            foreach (var threat in threats.Where(t => t.NeedsAction))
            {
                EventTimeline.Record(NoticeKind.Incident, NoticeSeverity.Critical, "Virus scan", $"Defender found {threat.Name}",
                    $"{threat.Status}. {threat.Resources}", "Open \"Scan for viruses\" and choose \"Remove what Defender found\".");
            }

            foreach (var threat in threats)
            {
                var accent = threat.NeedsAction ? CardKit.Critical : CardKit.Good;
                DefenderThreatsPanel.Children.Add(CardKit.Card(threat.Name, accent, threat.Status,
                    $"Severity {threat.Severity}. Found {threat.WhenText}.", threat.Resources));
            }
            RemoveThreatsButton.Visibility = threats.Any(t => t.NeedsAction) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void QuickScanButton_Click(object sender, RoutedEventArgs e) => await RunDefenderAsync(
            "Quick scan running. This usually takes a few minutes…", () => _defender.QuickScanAsync(), "Quick scan");

        private async void FullScanButton_Click(object sender, RoutedEventArgs e) => await RunDefenderAsync(
            "Starting a full scan…", () => Task.FromResult(_defender.StartFullScan()), "Full scan");

        private async void UpdateDefinitionsButton_Click(object sender, RoutedEventArgs e) => await RunDefenderAsync(
            "Downloading the latest virus definitions…", () => _defender.UpdateSignaturesAsync(), "Virus definitions");

        private async void ScanPathButton_Click(object sender, RoutedEventArgs e)
        {
            var path = (ScanPathBox.Text ?? string.Empty).Trim().Trim('"');
            if (path.Length == 0) { DefenderStatusText.Text = "Paste the path of a file or folder first."; return; }
            await RunDefenderAsync($"Scanning {path}…", () => _defender.ScanPathAsync(path), "Scan");
        }

        private async void RemoveThreatsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await CardKit.ConfirmAsync(XamlRoot, "Let Defender remove what it found?",
                    "Defender removes or quarantines each threat it found and has not dealt with yet. Use Windows Security > Protection history to restore something that was wrongly flagged.", "Remove"))
                return;
            await RunDefenderAsync("Removing threats…", () => _defender.RemoveActiveThreatsAsync(), "Threat removal");
        }

        private async Task RunDefenderAsync(string status, Func<Task<DefenderActionResult>> action, string what)
        {
            if (_defenderBusy) return;
            _defenderBusy = true;
            Button[] buttons = { QuickScanButton, FullScanButton, UpdateDefinitionsButton, ScanPathButton, RemoveThreatsButton };
            foreach (var button in buttons) button.IsEnabled = false;
            DefenderProgress.Visibility = Visibility.Visible;
            DefenderStatusText.Text = status;
            try
            {
                var result = await action();
                DefenderStatusText.Text = result.Message;
                EventTimeline.Record(NoticeKind.Information, result.Success ? NoticeSeverity.Info : NoticeSeverity.Warning,
                    "Virus scan", $"{what}: {(result.Success ? "done" : "did not work")}", result.Message);
                await RefreshDefenderAsync();
            }
            catch (Exception ex)
            {
                DefenderStatusText.Text = $"{what} did not work: {ex.Message}";
            }
            finally
            {
                DefenderProgress.Visibility = Visibility.Collapsed;
                foreach (var button in buttons) button.IsEnabled = true;
                _defenderBusy = false;
            }
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
            ThreatHuntButton.IsEnabled = false;
            ThreatReportText.Text = "Looking through this PC. This takes up to a minute…";
            _threats.Clear();
            try
            {
                var report = await _detector.PerformThreatHuntAsync();
                ThreatReportText.Text = report.ThreatCount == 0
                    ? $"Nothing stood out. Checked at {report.ScanTime:t}."
                    : $"{report.ThreatCount} warning sign{(report.ThreatCount == 1 ? "" : "s")} found, listed below. The most serious is {report.Severity}.";
            }
            catch (Exception ex)
            {
                ThreatReportText.Text = "The check did not finish: " + ex.Message;
            }
            finally
            {
                ThreatHuntButton.IsEnabled = true;
            }
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
