using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class HardeningView : Page
    {
        private readonly HardeningViewModel _viewModel;

        public HardeningView()
        {
            InitializeComponent();
            _viewModel = new HardeningViewModel();
            DataContext = _viewModel;
            _viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(HardeningViewModel.IsApplying) or nameof(HardeningViewModel.IsRollingBack) or nameof(HardeningViewModel.IsBusy))
                {
                    ApplyButton.IsEnabled = !_viewModel.IsBusy;
                    ApplyAllButton.IsEnabled = !_viewModel.IsBusy;
                    RollbackButton.IsEnabled = !_viewModel.IsBusy;
                    VerifyButton.IsEnabled = !_viewModel.IsBusy;
                }
            };
        }

        private async void RollbackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Roll back the baseline?",
                Content = "This restores the controls you applied from Windows Guardian, including firewall, Defender, and User Account Control if those were changed.",
                PrimaryButtonText = "Roll back",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _viewModel.RollbackAsync();
            }
        }
    }
}
