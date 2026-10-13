using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class MLInsightsView : Page
    {
        private readonly MLInsightsViewModel _viewModel;

        public MLInsightsView()
        {
            this.InitializeComponent();
            _viewModel = new MLInsightsViewModel();
            this.DataContext = _viewModel;
            
            Loaded += MLInsightsView_Loaded;
        }

        private async void MLInsightsView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
        }

        private async void RefreshButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.RefreshAsync();
        }

        private async void RunAnalysisButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await _viewModel.TriggerAnalysisAsync();
        }
    }
}

