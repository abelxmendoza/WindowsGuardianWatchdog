using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Systems
{
    public class StartupManager
    {
        public async Task<List<StartupItem>> ListAsync()
        {
            return await Task.Run(() =>
            {
                var items = new List<StartupItem>();

                ScanRegistryStartup(items, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Current User");
                ScanRegistryStartup(items, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "All Users");
                ScanRegistryStartup(items, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Current User (Once)");
                ScanRegistryStartup(items, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "All Users (Once)");
                ScanStartupFolders(items);

                return items;
            });
        }

        private void ScanRegistryStartup(List<StartupItem> items, RegistryKey baseKey, string subKey, string location)
        {
            try
            {
                using var key = baseKey.OpenSubKey(subKey);
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        items.Add(new StartupItem
                        {
                            Name = valueName,
                            Command = value,
                            Location = location,
                            Type = StartupItemType.Registry,
                            IsEnabled = true
                        });
                    }
                }
            }
            catch
            {
                // Handle errors
            }
        }

        private void ScanStartupFolders(List<StartupItem> items)
        {
            var startupFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))
            };

            foreach (var folder in startupFolders)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        var files = Directory.GetFiles(folder);
                        foreach (var file in files)
                        {
                            items.Add(new StartupItem
                            {
                                Name = Path.GetFileName(file),
                                Command = file,
                                Location = folder,
                                Type = StartupItemType.StartupFolder,
                                IsEnabled = true
                            });
                        }
                    }
                    catch
                    {
                        // Handle errors
                    }
                }
            }
        }

        private static bool TryGetRegistryTarget(string location, out RegistryKey baseKey, out string subKey)
        {
            switch (location)
            {
                case "Current User":
                    baseKey = Registry.CurrentUser;
                    subKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                    return true;
                case "Current User (Once)":
                    baseKey = Registry.CurrentUser;
                    subKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
                    return true;
                case "All Users":
                    baseKey = Registry.LocalMachine;
                    subKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                    return true;
                case "All Users (Once)":
                    baseKey = Registry.LocalMachine;
                    subKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
                    return true;
                default:
                    baseKey = Registry.CurrentUser;
                    subKey = string.Empty;
                    return false;
            }
        }

        public async Task<bool> DisableAsync(StartupItem item)
        {
            try
            {
                if (item.Type == StartupItemType.Registry)
                {
                    if (!TryGetRegistryTarget(item.Location, out var baseKey, out var subKey)) return false;
                    if (baseKey == Registry.LocalMachine)
                    {
                        var name = item.Name.Replace("'", "''");
                        var result = await ScriptRunner.RunPowerShellAsync(
                            $"Remove-ItemProperty -Path 'HKLM:\\{subKey}' -Name '{name}' -ErrorAction Stop",
                            elevate: true);
                        return result.Success;
                    }

                    using var key = baseKey.OpenSubKey(subKey, writable: true);
                    if (key?.GetValue(item.Name) == null) return false;
                    key.DeleteValue(item.Name, throwOnMissingValue: false);
                    return true;
                }

                if (item.Type == StartupItemType.StartupFolder && File.Exists(item.Command))
                {
                    File.Move(item.Command, item.Command + ".disabled", overwrite: false);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public async Task<bool> EnableAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (item.Type == StartupItemType.StartupFolder)
                    {
                        var disabledPath = item.Command + ".disabled";
                        if (File.Exists(disabledPath))
                        {
                            File.Move(disabledPath, item.Command);
                            return true;
                        }
                    }
                }
                catch
                {
                    // Handle errors
                }

                return false;
            });
        }
    }

    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public StartupItemType Type { get; set; }
        public bool IsEnabled { get; set; }
    }

    public enum StartupItemType
    {
        Registry,
        StartupFolder,
        ScheduledTask
    }
}

