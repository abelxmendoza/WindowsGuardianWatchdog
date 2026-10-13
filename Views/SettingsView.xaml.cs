using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace WindowsGuardianWatchdog.Views
{
    public sealed partial class SettingsView : Page
    {
        private const string SafeModeKey = "SafeMode";
        public SettingsView()
        {
            InitializeComponent();
            var values = ApplicationData.Current.LocalSettings.Values;
            SafeModeToggle.IsOn = values[SafeModeKey] is not bool saved || saved;
        }

        private void SafeModeToggle_Toggled(object sender, RoutedEventArgs e) =>
            ApplicationData.Current.LocalSettings.Values[SafeModeKey] = SafeModeToggle.IsOn;
    }
}
