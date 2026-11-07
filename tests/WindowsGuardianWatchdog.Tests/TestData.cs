using System.Runtime.CompilerServices;

namespace WindowsGuardianWatchdog.Tests
{
    internal static class TestData
    {
        /// <summary>Keeps quarantine, notices and change-watch files out of the real app data folder.</summary>
        [ModuleInitializer]
        internal static void UseScratchDataFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "wgw-tests-" + Environment.ProcessId);
            Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable("WGW_DATA_DIR", folder);
        }

        public static string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "wgw-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
