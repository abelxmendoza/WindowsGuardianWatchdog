using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace WindowsGuardianWatchdog
{
    public partial class App : Application
    {
        public const string AppUserModelId = "WindowsGuardianWatchdog.Desktop";
        private Window? _window;

        public App()
        {
            // Give the unpackaged desktop app a stable Windows shell identity. This keeps
            // taskbar grouping, shortcuts, notifications, and pinning tied to this app.
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            this.InitializeComponent();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

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

