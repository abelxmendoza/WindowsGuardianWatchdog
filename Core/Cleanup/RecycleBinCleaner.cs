using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class RecycleBinCleaner
    {
        public CleanupResult Scan()
        {
            var result = new CleanupResult
            {
                Category = "Recycle Bin",
                Items = new List<CleanupItem>()
            };

            try
            {
                var drives = DriveInfo.GetDrives();
                
                foreach (var drive in drives)
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        var recycleBin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                        if (Directory.Exists(recycleBin))
                        {
                            ScanRecycleBin(recycleBin, result);
                        }
                    }
                }
            }
            catch
            {
                // Handle errors
            }

            result.TotalSize = result.Items.Sum(i => i.Size);
            return result;
        }

        private void ScanRecycleBin(string path, CleanupResult result)
        {
            try
            {
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
                                Category = "Recycle Bin",
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

            try
            {
                // Use SHEmptyRecycleBin Windows API via PowerShell
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"$shell = New-Object -ComObject Shell.Application; $shell.Namespace(0xA).Items() | ForEach-Object { $_.InvokeVerb('delete') }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                process?.WaitForExit();
            }
            catch
            {
                // Fallback: try to delete files directly
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
}

