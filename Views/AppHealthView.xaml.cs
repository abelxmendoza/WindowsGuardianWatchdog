using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core.SelfCheck;
using WindowsGuardianWatchdog.Core.Storage;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class AppHealthView : Page
    {
        public AppHealthView()
        {
            this.InitializeComponent();
            Loaded += async (_, _) => await CheckAsync();
        }

        private async Task CheckAsync(string? note = null)
        {
            CheckButton.IsEnabled = false;
            StatusText.Text = "Checking…";
            try
            {
                var checks = await AppSelfCheck.RunAsync();
                var problems = checks.Count(c => c.Status != CheckStatus.Ok);
                StatusText.Text = (note == null ? string.Empty : note + " ")
                                  + (problems == 0 ? "Everything checked out." : $"{problems} item(s) need a look.");
                ChecksPanel.Children.Clear();
                foreach (var check in checks.OrderByDescending(c => c.Status))
                {
                    var accent = check.Status switch { CheckStatus.Problem => CardKit.Critical, CheckStatus.Warning => CardKit.Warning, _ => CardKit.Good };
                    var card = CardKit.Card(check.Name, accent, check.StatusText, check.Detail,
                        string.IsNullOrEmpty(check.Advice) ? null : "What to do: " + check.Advice);
                    if (check.Repair is string repair)
                        CardKit.AddButton(card, check.RepairLabel ?? "Repair", async (_, _) => await RepairAsync(repair, check.RepairLabel ?? "Repair"));
                    ChecksPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"The check could not finish: {ex.Message}";
            }
            finally
            {
                CheckButton.IsEnabled = true;
            }
        }

        private async Task RepairAsync(string repair, string label)
        {
            if (!await CardKit.ConfirmAsync(XamlRoot, label + "?", "The current file is kept beside it with .damaged added to its name, in case you need it.", label))
                return;
            string result;
            try { result = await Task.Run(() => AppSelfCheck.Repair(repair)); }
            catch (Exception ex) { result = $"The repair did not work: {ex.Message}"; }
            await CheckAsync(result);
        }

        private async void CheckButton_Click(object sender, RoutedEventArgs e) => await CheckAsync();

        private void OpenDataButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(GuardianPaths.DataDirectory);
            CardKit.OpenInWindows(GuardianPaths.DataDirectory);
        }
    }
}
