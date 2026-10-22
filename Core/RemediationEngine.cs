using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core
{
    public class RemediationEngine
    {
        public event EventHandler<RemediationResult>? RemediationCompleted;

        public async Task<RemediationResult> RemediateAsync(RemediationAction action, bool dryRun = true)
        {
            if (dryRun)
            {
                return new RemediationResult
                {
                    Action = action,
                    Success = true,
                    Message = $"Dry run: Would execute {action.Type}",
                    Timestamp = DateTime.Now
                };
            }

            return action.Type switch
            {
                RemediationType.EnableWindowsDefender => await EnableWindowsDefenderAsync(),
                RemediationType.EnableFirewall => await EnableFirewallAsync(),
                RemediationType.EnableUAC => await EnableUACAsync(),
                RemediationType.KillProcess => await KillProcessAsync(action.Target),
                RemediationType.QuarantineFile => await QuarantineFileAsync(action.Target),
                RemediationType.RemoveStartupEntry => await RemoveStartupEntryAsync(action.Target),
                _ => new RemediationResult
                {
                    Action = action,
                    Success = false,
                    Message = "Unknown remediation type",
                    Timestamp = DateTime.Now
                }
            };
        }

        private async Task<RemediationResult> EnableWindowsDefenderAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-Command \"Set-MpPreference -DisableRealtimeMonitoring $false\"",
                        Verb = "runas", // Run as administrator
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    process?.WaitForExit();

                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableWindowsDefender },
                        Success = process?.ExitCode == 0,
                        Message = process?.ExitCode == 0 
                            ? "Windows Defender enabled successfully" 
                            : "Failed to enable Windows Defender",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableWindowsDefender },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        private async Task<RemediationResult> EnableFirewallAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "advfirewall set allprofiles state on",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    process?.WaitForExit();

                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableFirewall },
                        Success = process?.ExitCode == 0,
                        Message = process?.ExitCode == 0 
                            ? "Firewall enabled successfully" 
                            : "Failed to enable firewall",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableFirewall },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        private async Task<RemediationResult> EnableUACAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                    
                    if (key != null)
                    {
                        key.SetValue("EnableLUA", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        key.Close();

                        var result = new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.EnableUAC },
                            Success = true,
                            Message = "UAC enabled successfully (restart required)",
                            Timestamp = DateTime.Now
                        };

                        OnRemediationCompleted(result);
                        return result;
                    }

                    return new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableUAC },
                        Success = false,
                        Message = "Failed to access registry",
                        Timestamp = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.EnableUAC },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        private async Task<RemediationResult> KillProcessAsync(string? processIdOrName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(processIdOrName))
                    {
                        return new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.KillProcess, Target = processIdOrName },
                            Success = false,
                            Message = "Process ID or name not provided",
                            Timestamp = DateTime.Now
                        };
                    }

                    Process? process = null;

                    if (int.TryParse(processIdOrName, out int pid))
                    {
                        process = Process.GetProcessById(pid);
                    }
                    else
                    {
                        var processes = Process.GetProcessesByName(processIdOrName);
                        if (processes.Length > 0)
                        {
                            process = processes[0];
                        }
                    }

                    if (process != null)
                    {
                        process.Kill();
                        process.WaitForExit(5000);

                        var result = new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.KillProcess, Target = processIdOrName },
                            Success = true,
                            Message = $"Process {process.ProcessName} (PID: {process.Id}) terminated",
                            Timestamp = DateTime.Now
                        };

                        OnRemediationCompleted(result);
                        return result;
                    }

                    return new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.KillProcess, Target = processIdOrName },
                        Success = false,
                        Message = "Process not found",
                        Timestamp = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.KillProcess, Target = processIdOrName },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        private async Task<RemediationResult> QuarantineFileAsync(string? filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        return new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.QuarantineFile, Target = filePath },
                            Success = false,
                            Message = "File not found",
                            Timestamp = DateTime.Now
                        };
                    }

                    // Reversible: the Quarantine page can put it back (see QuarantineVault).
                    var entry = WindowsGuardianWatchdog.Core.Quarantine.QuarantineVault.QuarantineFile(filePath, "Quarantined from Fix a problem");

                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.QuarantineFile, Target = filePath },
                        Success = true,
                        Message = $"File quarantined. Restore it from the Quarantine page if it was safe (item {entry.Id}).",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.QuarantineFile, Target = filePath },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        private async Task<RemediationResult> RemoveStartupEntryAsync(string? entryName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(entryName))
                    {
                        return new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.RemoveStartupEntry, Target = entryName },
                            Success = false,
                            Message = "Entry name not provided",
                            Timestamp = DateTime.Now
                        };
                    }

                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                    if (key != null)
                    {
                        key.DeleteValue(entryName, false);
                        key.Close();

                        var result = new RemediationResult
                        {
                            Action = new RemediationAction { Type = RemediationType.RemoveStartupEntry, Target = entryName },
                            Success = true,
                            Message = $"Startup entry '{entryName}' removed",
                            Timestamp = DateTime.Now
                        };

                        OnRemediationCompleted(result);
                        return result;
                    }

                    return new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.RemoveStartupEntry, Target = entryName },
                        Success = false,
                        Message = "Failed to access registry",
                        Timestamp = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    var result = new RemediationResult
                    {
                        Action = new RemediationAction { Type = RemediationType.RemoveStartupEntry, Target = entryName },
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        Timestamp = DateTime.Now
                    };

                    OnRemediationCompleted(result);
                    return result;
                }
            });
        }

        protected virtual void OnRemediationCompleted(RemediationResult result)
        {
            RemediationCompleted?.Invoke(this, result);
        }
    }

    public class RemediationAction
    {
        public RemediationType Type { get; set; }
        public string? Target { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }

    public enum RemediationType
    {
        EnableWindowsDefender,
        EnableFirewall,
        EnableUAC,
        KillProcess,
        QuarantineFile,
        RemoveStartupEntry
    }

    public class RemediationResult
    {
        public RemediationAction Action { get; set; } = new();
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}

