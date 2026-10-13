using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class RemediationView : Page
    {
        private readonly RemediationEngine _engine;
        private readonly ObservableCollection<RemediationResult> _results;

        public RemediationView()
        {
            this.InitializeComponent();
            _engine = new RemediationEngine();
            _results = new ObservableCollection<RemediationResult>();
            ResultsListView.ItemsSource = _results;
            
            _engine.RemediationCompleted += Engine_RemediationCompleted;
        }

        private void Engine_RemediationCompleted(object? sender, RemediationResult result)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _results.Insert(0, result);
            });
        }

        private async void EnableDefenderButton_Click(object sender, RoutedEventArgs e)
        {
            var action = new RemediationAction { Type = RemediationType.EnableWindowsDefender };
            await _engine.RemediateAsync(action, DryRunCheckBox.IsChecked == true);
        }

        private async void EnableFirewallButton_Click(object sender, RoutedEventArgs e)
        {
            var action = new RemediationAction { Type = RemediationType.EnableFirewall };
            await _engine.RemediateAsync(action, DryRunCheckBox.IsChecked == true);
        }

        private async void EnableUACButton_Click(object sender, RoutedEventArgs e)
        {
            var action = new RemediationAction { Type = RemediationType.EnableUAC };
            await _engine.RemediateAsync(action, DryRunCheckBox.IsChecked == true);
        }
    }
}

