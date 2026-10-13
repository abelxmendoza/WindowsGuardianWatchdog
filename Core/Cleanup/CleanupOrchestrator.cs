using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class CleanupOrchestrator
    {
        private readonly DiskCleaner _diskCleaner;
        private readonly TempFileCleaner _tempCleaner;
        private readonly BrowserCleaner _browserCleaner;
        private readonly RecycleBinCleaner _recycleBinCleaner;
        private readonly RegistryCleaner _registryCleaner;

        public event EventHandler<CleanupProgress>? ProgressUpdated;
        public event EventHandler<CleanupResult>? CleanupCompleted;

        public CleanupOrchestrator()
        {
            _diskCleaner = new DiskCleaner();
            _tempCleaner = new TempFileCleaner();
            _browserCleaner = new BrowserCleaner();
            _recycleBinCleaner = new RecycleBinCleaner();
            _registryCleaner = new RegistryCleaner();
        }

        public async Task<CleanupReport> ScanAllAsync()
        {
            var report = new CleanupReport
            {
                ScanTime = DateTime.Now,
                Results = new List<CleanupResult>()
            };

            var cleaners = new (string, Func<Task<CleanupResult>>)[]
            {
                ("Disk Cleanup", () => Task.FromResult(_diskCleaner.Scan())),
                ("Temp Files", () => Task.FromResult(_tempCleaner.Scan())),
                ("Browser Cache", () => Task.FromResult(_browserCleaner.Scan())),
                ("Recycle Bin", () => Task.FromResult(_recycleBinCleaner.Scan())),
                ("Registry", () => Task.FromResult(_registryCleaner.Scan()))
            };

            int completed = 0;
            foreach (var (name, scanAction) in cleaners)
            {
                OnProgressUpdated(new CleanupProgress
                {
                    Category = name,
                    Progress = (completed * 100) / cleaners.Length,
                    Status = "Scanning..."
                });

                var result = await scanAction();
                report.Results.Add(result);
                completed++;

                OnProgressUpdated(new CleanupProgress
                {
                    Category = name,
                    Progress = (completed * 100) / cleaners.Length,
                    Status = "Completed"
                });
            }

            report.TotalSize = report.Results.Sum(r => r.TotalSize);
            report.ItemCount = report.Results.Sum(r => r.Items.Count);

            return report;
        }

        public async Task ExecuteCleanupAsync(CleanupReport report, bool dryRun)
        {
            int completed = 0;
            int total = report.Results.Count;

            foreach (var result in report.Results)
            {
                OnProgressUpdated(new CleanupProgress
                {
                    Category = result.Category,
                    Progress = (completed * 100) / total,
                    Status = dryRun ? "Dry Run" : "Cleaning..."
                });

                await Task.Run(() =>
                {
                    switch (result.Category)
                    {
                        case "Disk Cleanup":
                            _diskCleaner.Execute(result, dryRun);
                            break;
                        case "Temporary Files":
                            _tempCleaner.Execute(result, dryRun);
                            break;
                        case "Browser Cache":
                            _browserCleaner.Execute(result, dryRun);
                            break;
                        case "Recycle Bin":
                            _recycleBinCleaner.Execute(result, dryRun);
                            break;
                        case "Registry Cleanup":
                            _registryCleaner.Execute(result, dryRun);
                            break;
                    }
                });

                completed++;
            }

            OnCleanupCompleted(new CleanupResult
            {
                Category = "Complete",
                TotalSize = report.TotalSize,
                Items = report.Results.SelectMany(r => r.Items).ToList()
            });
        }

        protected virtual void OnProgressUpdated(CleanupProgress progress)
        {
            ProgressUpdated?.Invoke(this, progress);
        }

        protected virtual void OnCleanupCompleted(CleanupResult result)
        {
            CleanupCompleted?.Invoke(this, result);
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
}

