using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

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

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roots = new (string Name, string Path)[]
            {
                ("Chrome", Path.Combine(local, "Google", "Chrome", "User Data")),
                ("Edge", Path.Combine(local, "Microsoft", "Edge", "User Data")),
                ("Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")),
                ("Vivaldi", Path.Combine(local, "Vivaldi", "User Data")),
                ("Chromium", Path.Combine(local, "Chromium", "User Data")),
                ("Opera", Path.Combine(local, "Opera Software", "Opera Stable")),
                ("Opera GX", Path.Combine(local, "Opera Software", "Opera GX Stable"))
            };

            foreach (var (name, path) in roots)
            {
                ScanChromium(name, path, result);
            }

            ScanFirefox(Path.Combine(local, "Mozilla", "Firefox", "Profiles"), result);
            AddWindowsSearchHistory(result);

            result.TotalSize = result.Items.Sum(item => item.Size);
            return result;
        }

        public void Execute(CleanupResult result, bool dryRun)
        {
            if (dryRun) return;

            foreach (var item in result.Items)
            {
                try
                {
                    if (item.Type == CleanupItemType.Registry)
                    {
                        ClearWindowsSearchHistory();
                    }
                    else if (item.Category.Contains("Firefox history", StringComparison.OrdinalIgnoreCase))
                    {
                        ClearFirefoxHistory(item.Path);
                    }
                    else if (Directory.Exists(item.Path))
                    {
                        DeleteChildren(item.Path);
                    }
                    else if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                    }
                }
                catch
                {
                    // A browser that is still open keeps its history database locked.
                }
            }
        }

        private static void ScanChromium(string name, string root, CleanupResult result)
        {
            if (!Directory.Exists(root)) return;
            foreach (var profile in Directory.GetDirectories(root))
            {
                var folder = Path.GetFileName(profile);
                if (folder is "Crashpad" or "ShaderCache" or "GrShaderCache" or "BrowserMetrics" or "Safe Browsing" or "Crowd Deny") continue;
                AddDirectory(Path.Combine(profile, "Cache"), $"{name} cache", result);
                AddDirectory(Path.Combine(profile, "Code Cache"), $"{name} code cache", result);
                AddDirectory(Path.Combine(profile, "GPUCache"), $"{name} GPU cache", result);
                AddDirectory(Path.Combine(profile, "Service Worker", "CacheStorage"), $"{name} site cache", result);
                AddFile(Path.Combine(profile, "History"), $"{name} history and search history", result);
                AddFile(Path.Combine(profile, "History-journal"), $"{name} history and search history", result);
                AddFile(Path.Combine(profile, "Archived History"), $"{name} history and search history", result);
            }
        }

        private static void ScanFirefox(string profilesRoot, CleanupResult result)
        {
            if (!Directory.Exists(profilesRoot)) return;
            foreach (var profile in Directory.GetDirectories(profilesRoot))
            {
                AddDirectory(Path.Combine(profile, "cache2"), "Firefox cache", result);
                AddDirectory(Path.Combine(profile, "startupCache"), "Firefox cache", result);
                var places = Path.Combine(profile, "places.sqlite");
                if (File.Exists(places))
                {
                    result.Items.Add(new CleanupItem
                    {
                        Path = places,
                        Category = "Firefox history and search history",
                        Size = new FileInfo(places).Length,
                        LastModified = File.GetLastWriteTime(places),
                        Type = CleanupItemType.File,
                        Description = "History and search terms are cleared. Bookmarks stay."
                    });
                }
            }
        }

        private static void AddWindowsSearchHistory(CleanupResult result)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery");
                if (key == null || key.GetValueNames().Length == 0) return;
                result.Items.Add(new CleanupItem
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery",
                    Category = "Windows search history",
                    Size = 0,
                    LastModified = DateTime.Now,
                    Type = CleanupItemType.Registry,
                    Description = "Start and Explorer search history"
                });
            }
            catch
            {
                // The search history key may not exist.
            }
        }

        private static void AddDirectory(string path, string category, CleanupResult result)
        {
            if (!Directory.Exists(path)) return;
            long size = 0;
            var count = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                        count++;
                    }
                    catch
                    {
                        // Skip a locked cache file.
                    }
                }
            }
            catch
            {
                return;
            }

            if (count == 0) return;
            result.Items.Add(new CleanupItem
            {
                Path = path,
                Category = category,
                Size = size,
                LastModified = Directory.GetLastWriteTime(path),
                Type = CleanupItemType.Directory,
                Description = $"{count} files"
            });
        }

        private static void AddFile(string path, string category, CleanupResult result)
        {
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            result.Items.Add(new CleanupItem
            {
                Path = path,
                Category = category,
                Size = info.Length,
                LastModified = info.LastWriteTime,
                Type = CleanupItemType.File
            });
        }

        private static void DeleteChildren(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { /* Locked cache files are left in place. */ }
            }
        }

        private static void ClearWindowsSearchHistory()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery", writable: true);
            if (key == null) return;
            foreach (var name in key.GetValueNames()) key.DeleteValue(name, throwOnMissingValue: false);
        }

        private static void ClearFirefoxHistory(string placesDatabase)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = placesDatabase,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            foreach (var statement in new[]
            {
                "DELETE FROM moz_historyvisits;",
                "DELETE FROM moz_inputhistory;",
                "DELETE FROM moz_places WHERE id NOT IN (SELECT IFNULL(fk, -1) FROM moz_bookmarks) AND foreign_count = 0;"
            })
            {
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }
    }
}
