namespace WindowsGuardianWatchdog.Core.Cleanup
{
    /// <summary>
    /// "Free up space": temporary files, crash reports, browser caches and the Recycle Bin.
    ///
    /// There is deliberately no registry cleaner. Removing "orphaned" registry entries frees no space, can break
    /// uninstallers, and the old one here didn't actually delete anything while reporting that it had.
    /// Prefetch and C:\Windows\Logs are also left alone: Windows rebuilds Prefetch (making the next starts slower)
    /// and the logs are what support needs when something goes wrong.
    /// </summary>
    public class CleanupOrchestrator
    {
        private readonly TempFileCleaner _tempCleaner = new();
        private readonly DiskCleaner _diskCleaner = new();
        private readonly BrowserCleaner _browserCleaner = new();
        private readonly RecycleBinCleaner _recycleBinCleaner = new();

        public event EventHandler<CleanupProgress>? ProgressUpdated;

        private IEnumerable<(string Category, Func<CleanupResult> Scan, Func<CleanupResult, CleanupOutcome> Execute)> Cleaners()
        {
            yield return (TempFileCleaner.CategoryName, _tempCleaner.Scan, _tempCleaner.Execute);
            yield return (DiskCleaner.CategoryName, _diskCleaner.Scan, _diskCleaner.Execute);
            yield return (BrowserCleaner.CategoryName, _browserCleaner.Scan, _browserCleaner.Execute);
            yield return (RecycleBinCleaner.CategoryName, _recycleBinCleaner.Scan, _recycleBinCleaner.Execute);
        }

        public async Task<CleanupReport> ScanAllAsync()
        {
            var report = new CleanupReport { ScanTime = DateTime.Now };
            var cleaners = Cleaners().ToList();
            for (var i = 0; i < cleaners.Count; i++)
            {
                var (category, scan, _) = cleaners[i];
                OnProgressUpdated(new CleanupProgress { Category = category, Progress = i * 100 / cleaners.Count, Status = "Looking…" });
                try
                {
                    report.Results.Add(await Task.Run(scan));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cleanup scan {category} failed: {ex.Message}");
                    report.Results.Add(new CleanupResult { Category = category });
                }
            }

            report.TotalSize = report.Results.Sum(r => r.TotalSize);
            report.ItemCount = report.Results.Sum(r => r.Items.Count);
            return report;
        }

        /// <summary>Deletes what the scan found and says what really happened. A preview deletes nothing.</summary>
        public async Task<CleanupOutcome> ExecuteCleanupAsync(CleanupReport report, bool dryRun)
        {
            if (dryRun)
            {
                return new CleanupOutcome { FreedBytes = report.TotalSize, Removed = report.ItemCount, Preview = true };
            }

            var outcome = new CleanupOutcome();
            var executors = Cleaners().ToDictionary(c => c.Category, c => c.Execute);
            var done = 0;
            foreach (var result in report.Results)
            {
                OnProgressUpdated(new CleanupProgress { Category = result.Category, Progress = done * 100 / Math.Max(1, report.Results.Count), Status = "Cleaning…" });
                if (executors.TryGetValue(result.Category, out var execute) && result.Items.Count > 0)
                {
                    outcome.Add(await Task.Run(() => execute(result)));
                }
                done++;
            }
            return outcome;
        }

        protected virtual void OnProgressUpdated(CleanupProgress progress)
        {
            ProgressUpdated?.Invoke(this, progress);
        }
    }

    public class CleanupResult
    {
        public string Category { get; set; } = string.Empty;
        public List<CleanupItem> Items { get; set; } = new();
        public long TotalSize { get; set; }
    }

    public class CleanupItem
    {
        public string Path { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public CleanupItemType Type { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum CleanupItemType
    {
        File,
        Directory,
        Registry
    }

    public class CleanupReport
    {
        public DateTime ScanTime { get; set; }
        public List<CleanupResult> Results { get; set; } = new();
        public long TotalSize { get; set; }
        public int ItemCount { get; set; }
    }

    public class CleanupProgress
    {
        public string Category { get; set; } = string.Empty;
        public int Progress { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>What a cleanup actually did, counted file by file.</summary>
    public sealed class CleanupOutcome
    {
        public long FreedBytes { get; set; }
        public int Removed { get; set; }
        /// <summary>Files that were in use or protected and were left where they are.</summary>
        public int Skipped { get; set; }
        public bool Preview { get; init; }

        public void Add(CleanupOutcome other)
        {
            FreedBytes += other.FreedBytes;
            Removed += other.Removed;
            Skipped += other.Skipped;
        }

        /// <summary>Deletes one file and counts it either way.</summary>
        public void DeleteFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return;
                var size = info.Length;
                if (info.IsReadOnly) info.IsReadOnly = false;
                info.Delete();
                FreedBytes += size;
                Removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Skipped++;
            }
        }

        /// <summary>Deletes everything inside a folder that isn't in use, keeping the folder itself.</summary>
        public void DeleteContents(string directory, DateTime? olderThan = null)
        {
            if (!Directory.Exists(directory)) return;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).ToList(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Skipped++; return; }

            foreach (var file in files)
            {
                if (olderThan is DateTime cutoff)
                {
                    try { if (File.GetLastWriteTime(file) >= cutoff) continue; } catch { continue; }
                }
                DeleteFile(file);
            }

            // Remove folders left empty, deepest first. A folder something still uses stays.
            try
            {
                foreach (var folder in Directory.EnumerateDirectories(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                             .OrderByDescending(f => f.Length).ToList())
                {
                    try
                    {
                        if (olderThan is DateTime cutoff && Directory.GetCreationTime(folder) >= cutoff) continue;
                        if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
                    }
                    catch { /* In use. */ }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
