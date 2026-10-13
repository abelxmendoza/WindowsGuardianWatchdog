using Microsoft.UI.Xaml;

namespace WindowsGuardianWatchdog
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Create and activate main window
            if (_window == null)
            {
                _window = new MainWindow();
            }
            
            _window.Activate();
        }
    }
}

