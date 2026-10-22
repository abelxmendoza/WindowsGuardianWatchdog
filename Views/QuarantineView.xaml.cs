using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core.Quarantine;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class QuarantineView : Page
    {
        public QuarantineView()
        {
            this.InitializeComponent();
            Loaded += async (_, _) => await RenderAsync();
        }

        private async Task RenderAsync()
        {
            var entries = await Task.Run(() => QuarantineVault.List());
            ItemsPanel.Children.Clear();
            if (entries.Count == 0)
            {
                ItemsPanel.Children.Add(CardKit.Empty("Quarantine is empty."));
                return;
            }

            foreach (var entry in entries)
            {
                var card = CardKit.Card(entry.FileName, CardKit.Warning, entry.SizeText,
                    $"From {entry.OriginalPath}", $"Put away {entry.WhenText}. {entry.Reason}", $"SHA-256 {entry.Sha256}");
                CardKit.AddButton(card, "Restore", async (_, _) => await RestoreAsync(entry));
                CardKit.AddButton(card, "Delete for good", async (_, _) => await DeleteAsync(entry), danger: true);
                ItemsPanel.Children.Add(card);
            }
        }

        private async void QuarantineButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text.Trim().Trim('"');
            if (path.Length == 0) { StatusText.Text = "Paste the path of a file first."; return; }
            if (!await CardKit.ConfirmAsync(XamlRoot, "Quarantine this file?",
                    $"{path}\n\nIt will stop working until you restore it. Any program that needs it may show an error.", "Quarantine"))
                return;
            await RunAsync(() => QuarantineVault.QuarantineFile(path, "Moved here by you"), $"Quarantined {Path.GetFileName(path)}.");
            PathBox.Text = string.Empty;
        }

        private async Task RestoreAsync(QuarantineEntry entry)
        {
            if (!await CardKit.ConfirmAsync(XamlRoot, "Restore this file?",
                    $"{entry.FileName} goes back to {entry.OriginalPath}. Only do this if you are sure it is safe.", "Restore"))
                return;
            await RunAsync(() => QuarantineVault.Restore(entry.Id), $"Restored {entry.FileName}.");
        }

        private async Task DeleteAsync(QuarantineEntry entry)
        {
            if (!await CardKit.ConfirmAsync(XamlRoot, "Delete for good?",
                    $"{entry.FileName} will be deleted and cannot be restored.", "Delete"))
                return;
            await RunAsync(() => QuarantineVault.Delete(entry.Id), $"Deleted {entry.FileName}.");
        }

        private async Task RunAsync(Action action, string done)
        {
            try
            {
                await Task.Run(action);
                StatusText.Text = done;
            }
            catch (QuarantineException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"That did not work: {ex.Message}";
            }
            await RenderAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RenderAsync();

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(QuarantineVault.VaultDirectory);
            CardKit.OpenInWindows(QuarantineVault.VaultDirectory);
        }
    }
}
