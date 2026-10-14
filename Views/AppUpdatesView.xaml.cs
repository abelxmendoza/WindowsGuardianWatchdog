using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class AppUpdatesView : Page
    {
        private readonly AppUpdatesViewModel _viewModel;

        public AppUpdatesView()
        {
            InitializeComponent();
            _viewModel = new AppUpdatesViewModel();
            DataContext = _viewModel;
            _viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AppUpdatesViewModel.IsBusy))
                {
                    BusyProgress.Visibility = _viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                    CheckButton.IsEnabled = !_viewModel.IsBusy;
                    UpgradeButton.IsEnabled = !_viewModel.IsBusy;
                    WindowsButton.IsEnabled = !_viewModel.IsBusy;
                }
            };
        }
    }
}
