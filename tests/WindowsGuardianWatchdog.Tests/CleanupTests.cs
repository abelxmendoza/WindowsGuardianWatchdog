using WindowsGuardianWatchdog.Core.Cleanup;
using Xunit;

namespace WindowsGuardianWatchdog.Tests
{
    public class CleanupTests
    {
        [Fact]
        public void Temp_cleanup_removes_only_old_files_and_counts_what_it_freed()
        {
            var folder = TestData.NewFolder();
            var cutoff = DateTime.Now.AddDays(-7);
            var old1 = Write(folder, "old1.tmp", 1000, DateTime.Now.AddDays(-30));
            var nested = Directory.CreateDirectory(Path.Combine(folder, "setup123")).FullName;
            Directory.SetCreationTime(nested, DateTime.Now.AddDays(-30));
            var old2 = Write(nested, "old2.tmp", 500, DateTime.Now.AddDays(-10));
            var fresh = Write(folder, "fresh.tmp", 300, DateTime.Now);

            var cleaner = new TempFileCleaner();
            var scan = cleaner.Scan(new[] { folder }, cutoff);
            Assert.Single(scan.Items);
            Assert.Equal(1500, scan.TotalSize);
            Assert.Contains("2 files", scan.Items[0].Description);

            var outcome = cleaner.Execute(scan, cutoff);
            Assert.Equal(1500, outcome.FreedBytes);
            Assert.Equal(2, outcome.Removed);
            Assert.Equal(0, outcome.Skipped);
            Assert.False(File.Exists(old1));
            Assert.False(File.Exists(old2));
            Assert.False(Directory.Exists(nested));
            Assert.True(File.Exists(fresh));
            Directory.Delete(folder, true);
        }

        [Fact]
        public void A_file_in_use_is_skipped_and_counted()
        {
            var folder = TestData.NewFolder();
            var locked = Write(folder, "locked.tmp", 100, DateTime.Now.AddDays(-30));
            var outcome = new CleanupOutcome();
            using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                outcome.DeleteFile(locked);
            }
            Assert.Equal(1, outcome.Skipped);
            Assert.Equal(0, outcome.Removed);
            Assert.Equal(0, outcome.FreedBytes);
            Assert.True(File.Exists(locked));
            Directory.Delete(folder, true);
        }

        [Fact]
        public void A_read_only_file_is_still_removed()
        {
            var folder = TestData.NewFolder();
            var file = Write(folder, "ro.tmp", 10, DateTime.Now.AddDays(-30));
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var outcome = new CleanupOutcome();
            outcome.DeleteFile(file);
            Assert.Equal(1, outcome.Removed);
            Assert.False(File.Exists(file));
            Directory.Delete(folder, true);
        }

        [Fact]
        public void Crash_reports_are_found_and_removed()
        {
            var folder = TestData.NewFolder();
            Write(folder, "app.dmp", 2048, DateTime.Now);
            var cleaner = new DiskCleaner();
            var scan = cleaner.Scan(new[] { (folder, "App crash dumps") });
            Assert.Equal(2048, scan.TotalSize);
            var outcome = cleaner.Execute(scan);
            Assert.Equal(2048, outcome.FreedBytes);
            Assert.Empty(Directory.GetFiles(folder));
            Directory.Delete(folder, true);
        }

        [Fact]
        public async Task Preview_deletes_nothing()
        {
            var folder = TestData.NewFolder();
            var file = Write(folder, "keep.tmp", 100, DateTime.Now.AddDays(-30));
            var report = new CleanupReport { TotalSize = 100, ItemCount = 1 };
            report.Results.Add(new TempFileCleaner().Scan(new[] { folder }, DateTime.Now.AddDays(-7)));
            var outcome = await new CleanupOrchestrator().ExecuteCleanupAsync(report, dryRun: true);
            Assert.True(outcome.Preview);
            Assert.Equal(100, outcome.FreedBytes);
            Assert.True(File.Exists(file));
            Directory.Delete(folder, true);
        }

        [Fact]
        public void Temp_locations_have_no_duplicates()
        {
            var locations = TempFileCleaner.Locations();
            Assert.Equal(locations.Count, locations.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        [Trait("Category", "Device")]
        public async Task Scanning_this_pc_reports_every_category_and_deletes_nothing()
        {
            var report = await new CleanupOrchestrator().ScanAllAsync();
            Assert.Equal(new[] { TempFileCleaner.CategoryName, DiskCleaner.CategoryName, BrowserCleaner.CategoryName, RecycleBinCleaner.CategoryName },
                report.Results.Select(r => r.Category));
            Assert.Equal(report.Results.Sum(r => r.TotalSize), report.TotalSize);
            var (bytes, items) = RecycleBinCleaner.Query();
            Assert.True(bytes >= 0 && items >= 0);
        }

        private static string Write(string folder, string name, int bytes, DateTime modified)
        {
            var path = Path.Combine(folder, name);
            File.WriteAllBytes(path, new byte[bytes]);
            File.SetLastWriteTime(path, modified);
            return path;
        }
    }
}
