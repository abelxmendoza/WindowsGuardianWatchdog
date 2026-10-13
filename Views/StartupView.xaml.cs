using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class StartupView : Page
    {
        private readonly StartupViewModel _viewModel;

        public StartupView()
        {
            this.InitializeComponent();
            _viewModel = new StartupViewModel();
            this.DataContext = _viewModel;
        }

        private async void RefreshButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }

        private async void DisableButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StartupItemViewModel item)
            {
                await _viewModel.DisableAsync(item);
            }
        }
    }
}

