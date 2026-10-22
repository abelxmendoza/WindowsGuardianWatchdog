using WindowsGuardianWatchdog.Core.SelfCheck;

namespace WindowsGuardianWatchdog.Core.Sensors
{
    /// <summary>
    /// Runs the change watch every five minutes while the app is open, the way the Mac app uses launchd and the Linux
    /// app a systemd timer. Also does the daily upkeep (state file backups) the App health page relies on.
    /// </summary>
    public static class BackgroundWatch
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private static CancellationTokenSource? _stop;

        public static void Start()
        {
            if (_stop != null) return;
            _stop = new CancellationTokenSource();
            var token = _stop.Token;
            _ = Task.Run(async () =>
            {
                // Let the window finish opening before the first look.
                try { await Task.Delay(TimeSpan.FromSeconds(20), token); } catch (OperationCanceledException) { return; }
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        AppSelfCheck.Maintain();
                        ChangeWatch.Run();
                    }
                    catch (Exception ex)
                    {
                        App.LogCrash("Change watch", ex);
                    }
                    try { await Task.Delay(Interval, token); } catch (OperationCanceledException) { return; }
                }
            }, token);
        }

        public static void Stop()
        {
            _stop?.Cancel();
            _stop = null;
        }
    }
}
