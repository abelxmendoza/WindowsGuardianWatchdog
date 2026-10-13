using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class CleanupView : Page
    {
        private readonly CleanupViewModel _viewModel;

        public CleanupView()
        {
            this.InitializeComponent();
            _viewModel = new CleanupViewModel();
            this.DataContext = _viewModel;
        }

        private async void ScanButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.ScanAsync();
        }

        private async void DryRunButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.CleanAsync(dryRun: true);
        }

        private async void CleanButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.CleanAsync(dryRun: false);
        }
    }
}

