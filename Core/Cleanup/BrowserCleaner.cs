using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class BrowserCleaner
    {
        public CleanupResult Scan()
        {
            var result = new CleanupResult
            {
                Category = "Browser Cache",
                Items = new List<CleanupItem>()
            };

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // Chrome
            ScanBrowserCache(Path.Combine(appData, "Google", "Chrome", "User Data", "Default", "Cache"), "Chrome Cache", result);
            ScanBrowserCache(Path.Combine(appData, "Google", "Chrome", "User Data", "Default", "Code Cache"), "Chrome Code Cache", result);
            
            // Edge
            ScanBrowserCache(Path.Combine(appData, "Microsoft", "Edge", "User Data", "Default", "Cache"), "Edge Cache", result);
            ScanBrowserCache(Path.Combine(appData, "Microsoft", "Edge", "User Data", "Default", "Code Cache"), "Edge Code Cache", result);
            
            // Firefox
            var firefoxProfiles = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                var profiles = Directory.GetDirectories(firefoxProfiles);
                foreach (var profile in profiles)
                {
                    ScanBrowserCache(Path.Combine(profile, "cache2"), "Firefox Cache", result);
                }
            }

            result.TotalSize = result.Items.Sum(i => i.Size);
            return result;
        }

        private void ScanBrowserCache(string path, string category, CleanupResult result)
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

