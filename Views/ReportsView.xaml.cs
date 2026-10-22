using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core.Storage;
using WindowsGuardianWatchdog.Services;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class ReportsView : Page
    {
        public ReportsView()
        {
            this.InitializeComponent();
            Loaded += (_, _) => RenderList();
        }

        private void RenderList()
        {
            ReportsPanel.Children.Clear();
            var reports = Directory.Exists(GuardianPaths.ReportsDirectory)
                ? new DirectoryInfo(GuardianPaths.ReportsDirectory).GetFiles("*.html").OrderByDescending(f => f.LastWriteTime).Take(30).ToList()
                : new List<FileInfo>();
            if (reports.Count == 0)
            {
                ReportsPanel.Children.Add(CardKit.Empty("No reports yet. Make one with the button above."));
                return;
            }

            foreach (var report in reports)
            {
                var kind = report.Name.StartsWith("weekly", StringComparison.OrdinalIgnoreCase) ? "Weekly summary" : "Report";
                var card = CardKit.Card($"{kind} from {report.LastWriteTime:D}", CardKit.Good, report.LastWriteTime.ToString("t"), report.FullName);
                var path = report.FullName;
                CardKit.AddButton(card, "Open", (_, _) => CardKit.OpenInWindows(path));
                ReportsPanel.Children.Add(card);
            }
        }

        private async Task MakeAsync(Func<ScheduledReportService, Task<ReportResult>> make)
        {
            DailyButton.IsEnabled = WeeklyButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible;
            StatusText.Text = "Running the checkup and writing the report. This can take a minute…";
            try
            {
                var result = await make(new ScheduledReportService());
                StatusText.Text = result.Success && result.HTMLPath != null ? "Done. The report opened in your browser." : "The report could not be written.";
                if (result.HTMLPath != null) CardKit.OpenInWindows(result.HTMLPath);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"The report could not be made: {ex.Message}";
            }
            finally
            {
                Progress.Visibility = Visibility.Collapsed;
                DailyButton.IsEnabled = WeeklyButton.IsEnabled = true;
                RenderList();
            }
        }

        private async void DailyButton_Click(object sender, RoutedEventArgs e) => await MakeAsync(s => s.GenerateDailyReportAsync());

        private async void WeeklyButton_Click(object sender, RoutedEventArgs e) => await MakeAsync(s => s.GenerateWeeklyReportAsync());

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(GuardianPaths.ReportsDirectory);
            CardKit.OpenInWindows(GuardianPaths.ReportsDirectory);
        }
    }
}
