using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class DriversView : Page
    {
        private readonly DriversViewModel _viewModel;

        public DriversView()
        {
            this.InitializeComponent();
            _viewModel = new DriversViewModel();
            this.DataContext = _viewModel;
            this.Loaded += DriversView_Loaded;
        }

        private async void DriversView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_viewModel.Drivers.Count == 0)
            {
                await _viewModel.ScanAsync();
            }
        }

        private async void HardwareButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.ScanHardwareAsync();
        }

        private async void ScanButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.ScanAsync();
        }

        private async void UpdateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DriverViewModel driver)
            {
                await _viewModel.UpdateDriverAsync(driver);
            }
        }

        private async void CheckButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.CheckForUpdatesAsync();
        }

        private async void InstallButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.InstallAvailableUpdatesAsync();
        }
    }
}

