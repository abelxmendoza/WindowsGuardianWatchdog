using Microsoft.UI.Xaml.Controls;
using WindowsGuardianWatchdog.Core;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class AlertsView : Page
    {
        private readonly ObservableCollection<SecurityAlert> _alerts;

        public AlertsView()
        {
            this.InitializeComponent();
            _alerts = new ObservableCollection<SecurityAlert>();
            AlertsListView.ItemsSource = _alerts;
        }
    }
}

