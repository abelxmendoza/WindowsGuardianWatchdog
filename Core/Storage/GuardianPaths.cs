namespace WindowsGuardianWatchdog.Core.Storage
{
    /// <summary>
    /// Where the app keeps what it learns about this PC: %LOCALAPPDATA%\WindowsGuardianWatchdog.
    /// The Mac app uses ~/.macguardian and the Linux app ~/.linuxguardian for the same job.
    /// </summary>
    public static class GuardianPaths
    {
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsGuardianWatchdog");

        // Older parts of the app (config, baselines, reports) write under %APPDATA%.
        public static string RoamingDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsGuardianWatchdog");

        public static string ReportsDirectory => Path.Combine(RoamingDirectory, "reports");

        public static string File(string name)
        {
            Directory.CreateDirectory(DataDirectory);
            return Path.Combine(DataDirectory, name);
        }

        /// <summary>Writes through a temporary file so a crash mid-write never leaves half a file behind.</summary>
        public static void WriteAtomically(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            System.IO.File.WriteAllText(temp, contents);
            System.IO.File.Move(temp, path, overwrite: true);
        }
    }
}
