using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class DiskCleaner
    {
        public CleanupResult Scan()
        {
            var result = new CleanupResult
            {
                Category = "Disk Cleanup",
                Items = new List<CleanupItem>()
            };

            // Windows Temp
            ScanDirectory(Path.GetTempPath(), "Windows Temp", result);
            
            // User Temp
            var userTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
            if (Directory.Exists(userTemp))
            {
                ScanDirectory(userTemp, "User Temp", result);
            }

            // Prefetch
            var prefetch = @"C:\Windows\Prefetch";
            if (Directory.Exists(prefetch))
            {
                ScanDirectory(prefetch, "Prefetch", result);
            }

            // Windows Logs
            var logs = @"C:\Windows\Logs";
            if (Directory.Exists(logs))
            {
                ScanDirectory(logs, "Windows Logs", result);
            }

            // Crash dumps
            var crashDumps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
            if (Directory.Exists(crashDumps))
            {
                ScanDirectory(crashDumps, "Crash Dumps", result);
            }

            result.TotalSize = result.Items.Sum(i => i.Size);
            return result;
        }

        private void ScanDirectory(string path, string category, CleanupResult result)
        {
            try
            {
                if (!Directory.Exists(path)) return;

                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                
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
                                Category = category,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                Type = CleanupItemType.File
                            });
                        }
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
            }
            catch
            {
                // Handle directory access errors
            }
        }

        public void Execute(CleanupResult result, bool dryRun)
        {
            if (dryRun)
            {
                return; // Just scan, don't delete
            }

            foreach (var item in result.Items)
            {
                try
                {
                    if (item.Type == CleanupItemType.File && File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                    }
                    else if (item.Type == CleanupItemType.Directory && Directory.Exists(item.Path))
                    {
                        Directory.Delete(item.Path, true);
                    }
                }
                catch
                {
                    // Log error but continue
                }
            }
        }
    }
}

