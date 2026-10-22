using Microsoft.UI.Xaml;

namespace WindowsGuardianWatchdog
{
    public partial class App : Application
    {
        private Window? _window;

        // %ProgramData%\WindowsGuardianWatchdog\crash.log, so a launch that closes right away leaves a reason behind.
        // ProgramData, unlike %LOCALAPPDATA%, isn't redirected inside the MSIX package or tied to whichever admin approved UAC.
        public static string CrashLogPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsGuardianWatchdog", "crash.log");

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash("AppDomain", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) => { LogCrash("Task", e.Exception); e.SetObserved(); };
            UnhandledException += (_, e) => LogCrash("XAML", e.Exception);

            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // Create and activate main window
                if (_window == null)
                {
                    _window = new MainWindow();
                }

                _window.Activate();
                Core.Sensors.BackgroundWatch.Start();
            }
            catch (Exception ex)
            {
                LogCrash("Launch", ex);
                throw;
            }
        }

        public static void LogCrash(string source, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
                File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never be the reason the app fails.
            }
        }
    }
}
