using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class TempFileCleaner
    {
        public CleanupResult Scan()
        {
            var result = new CleanupResult
            {
                Category = "Temporary Files",
                Items = new List<CleanupItem>()
            };

            // Common temp locations
            var tempLocations = new[]
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Temp"),
                @"C:\Windows\Temp"
            };

            foreach (var location in tempLocations)
            {
                if (Directory.Exists(location))
                {
                    ScanTempDirectory(location, result);
                }
            }

            // Old temp files (older than 7 days)
            var cutoffDate = DateTime.Now.AddDays(-7);
            result.Items = result.Items.Where(i => i.LastModified < cutoffDate).ToList();
            result.TotalSize = result.Items.Sum(i => i.Size);

            return result;
        }

        private void ScanTempDirectory(string path, CleanupResult result)
        {
            try
            {
                var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Exists)
                        {
                            result.Items.Add(new CleanupItem
                            {
                                Path = file,
                                Category = "Temp Files",
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                Type = CleanupItemType.File
                            });
                        }
                    }
                    catch
                    {
                        // Skip inaccessible files
                    }
                }
            }
            catch
            {
                // Handle errors
            }
        }

        public void Execute(CleanupResult result, bool dryRun)
        {
            if (dryRun) return;

            foreach (var item in result.Items)
            {
                try
                {
                    if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                    }
                }
                catch
                {
                    // Continue on errors
                }
            }
        }
    }
}

