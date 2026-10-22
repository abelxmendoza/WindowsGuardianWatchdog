using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core.Incidents;
using WindowsGuardianWatchdog.Core.Sensors;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class AlertsView : Page
    {
        public AlertsView()
        {
            this.InitializeComponent();
            Loaded += (_, _) =>
            {
                EventTimeline.Changed += Timeline_Changed;
                Render();
            };
            Unloaded += (_, _) => EventTimeline.Changed -= Timeline_Changed;
        }

        private void Timeline_Changed(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Render);

        private void Render()
        {
            var notices = EventTimeline.ReadAll();
            var shown = UnreadOnlySwitch.IsOn ? notices.Where(n => !n.Seen).ToList() : notices.ToList();
            var last = ChangeWatch.LastRun;
            StatusText.Text = (last == null ? "The change watch has not run yet." : $"Last checked {last.Value:g}.")
                              + $" {notices.Count(n => n.NeedsAttention)} unread.";

            NoticesPanel.Children.Clear();
            if (shown.Count == 0)
            {
                NoticesPanel.Children.Add(CardKit.Empty("Nothing here. That is the normal state."));
                return;
            }

            foreach (var notice in shown)
            {
                var accent = notice.Severity switch
                {
                    NoticeSeverity.Critical => CardKit.Critical,
                    NoticeSeverity.Warning => CardKit.Warning,
                    _ => CardKit.Muted
                };
                var card = CardKit.Card(notice.Title, accent, notice.Seen ? "Read" : notice.SeverityText,
                    $"{notice.WhenText} · {notice.Source}", notice.Detail,
                    string.IsNullOrEmpty(notice.Advice) ? null : "What to do: " + notice.Advice);
                card.Opacity = notice.Seen ? 0.65 : 1;
                if (!notice.Seen)
                {
                    var id = notice.Id;
                    CardKit.AddButton(card, "Mark as read", (_, _) => EventTimeline.MarkSeen(id));
                }
                NoticesPanel.Children.Add(card);
            }
        }

        private async void CheckNowButton_Click(object sender, RoutedEventArgs e)
        {
            CheckNowButton.IsEnabled = false;
            StatusText.Text = "Looking at the watched settings…";
            try
            {
                var result = await ChangeWatch.RunAsync();
                Render();
                StatusText.Text = result.FirstLook
                    ? $"First look: {result.Watched} settings recorded as normal. Changes from now on will show here."
                    : result.Changes.Count == 0
                        ? $"Checked {result.Watched} settings. Nothing changed since the last look."
                        : $"Checked {result.Watched} settings. {result.Changes.Count} change(s) found; see below.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"The check could not finish: {ex.Message}";
            }
            finally
            {
                CheckNowButton.IsEnabled = true;
            }
        }

        private void MarkAllButton_Click(object sender, RoutedEventArgs e) => EventTimeline.MarkAllSeen();

        private void ClearReadButton_Click(object sender, RoutedEventArgs e) => EventTimeline.ClearRead();

        private void UnreadOnlySwitch_Toggled(object sender, RoutedEventArgs e) => Render();
    }
}
