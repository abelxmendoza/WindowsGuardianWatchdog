namespace WindowsGuardianWatchdog.Core.Cleanup
{
    /// <summary>
    /// Temporary files older than a week, in your Temp folder and Windows' own. Anything newer may belong to a setup
    /// or an app that is running, so it is left alone.
    /// </summary>
    public class TempFileCleaner
    {
        public const string CategoryName = "Temporary files";
        public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(7);

        public static IReadOnlyList<string> Locations()
        {
            var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            return new[]
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    windowsTemp
                }
                .Where(Directory.Exists)
                .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public CleanupResult Scan() => Scan(Locations(), DateTime.Now - MinimumAge);

        public CleanupResult Scan(IEnumerable<string> locations, DateTime cutoff)
        {
            var result = new CleanupResult { Category = CategoryName };
            foreach (var location in locations)
            {
                long size = 0;
                var count = 0;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(location, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            if (info.LastWriteTime >= cutoff) continue;
                            size += info.Length;
                            count++;
                        }
                        catch { /* Gone or locked. */ }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (count == 0) continue;
                result.Items.Add(new CleanupItem
                {
                    Path = location,
                    Category = CategoryName,
                    Size = size,
                    LastModified = cutoff,
                    Type = CleanupItemType.Directory,
                    Description = $"{count} files older than {MinimumAge.Days} days"
                });
            }

            result.TotalSize = result.Items.Sum(i => i.Size);
            return result;
        }

        public CleanupOutcome Execute(CleanupResult result) => Execute(result, DateTime.Now - MinimumAge);

        public CleanupOutcome Execute(CleanupResult result, DateTime cutoff)
        {
            var outcome = new CleanupOutcome();
            foreach (var item in result.Items) outcome.DeleteContents(item.Path, cutoff);
            return outcome;
        }
    }
}
