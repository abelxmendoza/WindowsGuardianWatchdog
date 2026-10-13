using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using WindowsGuardianWatchdog.ViewModels;
using WindowsGuardianWatchdog.Core.Hardening;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class HardeningView : Page
    {
        public HardeningView()
        {
            this.InitializeComponent();
            this.DataContext = new HardeningViewModel();
            
            // Bind button enabled states
            var viewModel = (HardeningViewModel)this.DataContext;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(HardeningViewModel.IsApplying) || 
                    e.PropertyName == nameof(HardeningViewModel.IsRollingBack))
                {
                    ApplyButton.IsEnabled = !viewModel.IsApplying && !viewModel.IsRollingBack;
                    RollbackButton.IsEnabled = !viewModel.IsApplying && !viewModel.IsRollingBack;
                    VerifyButton.IsEnabled = !viewModel.IsApplying && !viewModel.IsRollingBack;
                }
            };
        }
    }
}

