namespace WindowsGuardianWatchdog.Core.Cleanup
{
    /// <summary>Crash dumps and error reports that were already sent (or never will be).</summary>
    public class DiskCleaner
    {
        public const string CategoryName = "Crash reports";

        public static IReadOnlyList<(string Path, string Label)> Locations()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return new[]
            {
                (Path.Combine(local, "CrashDumps"), "App crash dumps"),
                (Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"), "Your error reports"),
                (Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"), "Your error reports"),
                (Path.Combine(common, "Microsoft", "Windows", "WER", "ReportArchive"), "Windows error reports"),
                (Path.Combine(common, "Microsoft", "Windows", "WER", "ReportQueue"), "Windows error reports"),
            };
        }

        public CleanupResult Scan() => Scan(Locations());

        public CleanupResult Scan(IEnumerable<(string Path, string Label)> locations)
        {
            var result = new CleanupResult { Category = CategoryName };
            foreach (var (path, label) in locations)
            {
                if (!Directory.Exists(path)) continue;
                long size = 0;
                var count = 0;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                    {
                        try { size += new FileInfo(file).Length; count++; } catch { /* Gone. */ }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

                if (count == 0) continue;
                result.Items.Add(new CleanupItem
                {
                    Path = path,
                    Category = label,
                    Size = size,
                    LastModified = Directory.GetLastWriteTime(path),
                    Type = CleanupItemType.Directory,
                    Description = $"{count} files"
                });
            }

            result.TotalSize = result.Items.Sum(i => i.Size);
            return result;
        }

        public CleanupOutcome Execute(CleanupResult result)
        {
            var outcome = new CleanupOutcome();
            foreach (var item in result.Items) outcome.DeleteContents(item.Path);
            return outcome;
        }
    }
}
