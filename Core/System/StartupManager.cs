using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Systems
{
    public class StartupManager
    {
        public async Task<List<StartupItem>> ListAsync()
        {
            return await Task.Run(() =>
            {
                var items = new List<StartupItem>();

                // Registry Run keys
                ScanRegistryStartup(items, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Current User");
                ScanRegistryStartup(items, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "All Users");
                ScanRegistryStartup(items, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Current User (Once)");
                ScanRegistryStartup(items, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "All Users (Once)");

                // Startup folders
                ScanStartupFolders(items);

                // Scheduled tasks
                ScanScheduledTasks(items);

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

        private void ScanScheduledTasks(List<StartupItem> items)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = "/query /fo LIST /v",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    // Parse scheduled tasks output
                    // This is simplified - full implementation would parse the output properly
                }
            }
            catch
            {
                // Handle errors
            }
        }

        public async Task<bool> DisableAsync(StartupItem item)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (item.Type == StartupItemType.Registry)
                    {
                        var parts = item.Location.Split('\\');
                        var baseKey = parts[0] == "Current User" ? Registry.CurrentUser : Registry.LocalMachine;
                        var subKey = parts[0] == "Current User" 
                            ? @"Software\Microsoft\Windows\CurrentVersion\Run" 
                            : @"Software\Microsoft\Windows\CurrentVersion\Run";

                        using var key = baseKey.OpenSubKey(subKey, true);
                        if (key != null && key.GetValue(item.Name) != null)
                        {
                            key.DeleteValue(item.Name, false);
                            return true;
                        }
                    }
                    else if (item.Type == StartupItemType.StartupFolder)
                    {
                        // Rename or move the file
                        if (File.Exists(item.Command))
                        {
                            var disabledPath = item.Command + ".disabled";
                            File.Move(item.Command, disabledPath);
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

