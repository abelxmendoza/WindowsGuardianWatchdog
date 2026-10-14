using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using WindowsGuardianWatchdog.Core.Files;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class FileSearchView : Page
    {
        private readonly ObservableCollection<FileHit> _results = new();
        private CancellationTokenSource? _search;

        public FileSearchView()
        {
            InitializeComponent();
            ResultsList.ItemsSource = _results;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var query = QueryBox.Text?.Trim() ?? string.Empty;
            if (query.Length < 2)
            {
                StatusText.Text = "Enter at least two characters.";
                return;
            }

            _search?.Cancel();
            _search = new CancellationTokenSource();
            var token = _search.Token;
            _results.Clear();
            SearchButton.IsEnabled = false;
            SearchProgress.Visibility = Visibility.Visible;
            StatusText.Text = AllDrivesBox.IsChecked == true ? "Searching every fixed drive…" : "Searching your user folder…";
            try
            {
                var progress = new Progress<string>(root => StatusText.Text = $"Searching {root}");
                var hits = await FileSearcher.SearchAsync(query, AllDrivesBox.IsChecked == true, progress, token);
                foreach (var hit in hits) _results.Add(hit);
                StatusText.Text = hits.Count >= FileSearcher.ResultLimit
                    ? $"Showing the first {hits.Count} matches. Narrow the name to see a shorter list."
                    : $"{hits.Count} file{(hits.Count == 1 ? "" : "s")} matched \"{query}\".";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Search cancelled.";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally
            {
                SearchProgress.Visibility = Visibility.Collapsed;
                SearchButton.IsEnabled = true;
            }
        }
    }
}
