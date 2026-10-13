using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace WindowsGuardianWatchdog.Core.Cleanup
{
    public class RegistryCleaner
    {
        public CleanupResult Scan()
        {
            var result = new CleanupResult
            {
                Category = "Registry Cleanup",
                Items = new List<CleanupItem>()
            };

            // Scan for orphaned registry entries
            ScanOrphanedUninstallKeys(result);
            ScanOrphanedFileAssociations(result);
            ScanOrphanedStartupEntries(result);

            result.TotalSize = result.Items.Count; // Count as items, not size
            return result;
        }

        private void ScanOrphanedUninstallKeys(CleanupResult result)
        {
            try
            {
                var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallKey == null) return;

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = uninstallKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        var installLocation = subKey.GetValue("InstallLocation")?.ToString();

                        if (!string.IsNullOrEmpty(installLocation) && !System.IO.Directory.Exists(installLocation))
                        {
                            result.Items.Add(new CleanupItem
                            {
                                Path = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{subKeyName}",
                                Category = "Orphaned Uninstall",
                                Size = 0,
                                LastModified = DateTime.Now,
                                Type = CleanupItemType.Registry,
                                Description = $"Orphaned entry: {displayName ?? subKeyName}"
                            });
                        }
                    }
                    catch
                    {
                        // Skip errors
                    }
                }
            }
            catch
            {
                // Handle errors
            }
        }

        private void ScanOrphanedFileAssociations(CleanupResult result)
        {
            // TODO: Scan for orphaned file associations
        }

        private void ScanOrphanedStartupEntries(CleanupResult result)
        {
            // TODO: Scan for orphaned startup entries
        }

        public void Execute(CleanupResult result, bool dryRun)
        {
            if (dryRun) return;

            foreach (var item in result.Items.Where(i => i.Type == CleanupItemType.Registry))
            {
                try
                {
                    var parts = item.Path.Split('\\');
                    if (parts.Length < 2) continue;

                    var hive = parts[0];
                    var keyPath = string.Join("\\", parts.Skip(1).Take(parts.Length - 2));
                    var valueName = parts.Last();

                    RegistryKey? baseKey = hive switch
                    {
                        "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                        "HKEY_CURRENT_USER" => Registry.CurrentUser,
                        _ => null
                    };

                    if (baseKey != null)
                    {
                        using var key = baseKey.OpenSubKey(keyPath, true);
                        if (key != null && key.GetValue(valueName) != null)
                        {
                            key.DeleteValue(valueName, false);
                        }
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

