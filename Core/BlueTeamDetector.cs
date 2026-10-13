using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core
{
    public class BlueTeamDetector
    {
        private readonly List<ThreatIndicator> _threatIndicators;
        private readonly List<ThreatIntelligence> _threatIntelligence;

        public event EventHandler<ThreatDetection>? ThreatDetected;

        public BlueTeamDetector()
        {
            _threatIndicators = new List<ThreatIndicator>();
            _threatIntelligence = new List<ThreatIntelligence>();
            InitializeThreatIndicators();
        }

        private void InitializeThreatIndicators()
        {
            // Common malware indicators
            _threatIndicators.AddRange(new[]
            {
                new ThreatIndicator { Type = IndicatorType.ProcessName, Pattern = @"(?i)(malware|trojan|virus|backdoor|keylog)" },
                new ThreatIndicator { Type = IndicatorType.FilePath, Pattern = @"(?i)(temp|appdata\\local\\temp)" },
                new ThreatIndicator { Type = IndicatorType.Network, Pattern = @"(?i)(suspicious|malicious)" }
            });
        }

        public async Task<ThreatReport> PerformThreatHuntAsync()
        {
            var report = new ThreatReport
            {
                ScanTime = DateTime.Now,
                Threats = new List<ThreatDetection>()
            };

            // Check running processes
            var processThreats = await DetectProcessThreatsAsync();
            report.Threats.AddRange(processThreats);

            // Check network connections
            var networkThreats = await DetectNetworkThreatsAsync();
            report.Threats.AddRange(networkThreats);

            // Check system configuration
            var configThreats = await DetectConfigurationThreatsAsync();
            report.Threats.AddRange(configThreats);

            // Check registry for persistence mechanisms
            var registryThreats = await DetectRegistryThreatsAsync();
            report.Threats.AddRange(registryThreats);

            report.ThreatCount = report.Threats.Count;
            report.Severity = report.Threats.Any(t => t.Severity == ThreatSeverity.Critical) 
                ? ThreatSeverity.Critical 
                : report.Threats.Any(t => t.Severity == ThreatSeverity.High) 
                    ? ThreatSeverity.High 
                    : ThreatSeverity.Low;

            return report;
        }

        private async Task<List<ThreatDetection>> DetectProcessThreatsAsync()
        {
            var threats = new List<ThreatDetection>();

            await Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcesses();
                    
                    foreach (var process in processes)
                    {
                        try
                        {
                            var processName = process.ProcessName.ToLower();
                            var processPath = process.MainModule?.FileName?.ToLower() ?? "";

                            // Check against threat indicators
                            foreach (var indicator in _threatIndicators.Where(i => i.Type == IndicatorType.ProcessName))
                            {
                                if (Regex.IsMatch(processName, indicator.Pattern) || 
                                    Regex.IsMatch(processPath, indicator.Pattern))
                                {
                                    threats.Add(new ThreatDetection
                                    {
                                        Type = ThreatType.Process,
                                        Severity = ThreatSeverity.High,
                                        Description = $"Suspicious process detected: {process.ProcessName}",
                                        Details = $"PID: {process.Id}, Path: {processPath}",
                                        Timestamp = DateTime.Now
                                    });
                                    
                                    OnThreatDetected(threats.Last());
                                }
                            }

                            // Check for processes with no digital signature
                            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                            {
                                if (!HasValidDigitalSignature(processPath))
                                {
                                    threats.Add(new ThreatDetection
                                    {
                                        Type = ThreatType.Process,
                                        Severity = ThreatSeverity.Medium,
                                        Description = $"Unsigned process: {process.ProcessName}",
                                        Details = $"Path: {processPath}",
                                        Timestamp = DateTime.Now
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Skip processes we can't access
                        }
                    }
                }
                catch
                {
                    // Handle errors
                }
            });

            return threats;
        }

        private async Task<List<ThreatDetection>> DetectNetworkThreatsAsync()
        {
            var threats = new List<ThreatDetection>();

            await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_NetworkConnection WHERE LocalName IS NOT NULL");
                    
                    var connections = searcher.Get();
                    // Network threat detection logic
                }
                catch
                {
                    // Handle errors
                }
            });

            return threats;
        }

        private async Task<List<ThreatDetection>> DetectConfigurationThreatsAsync()
        {
            var threats = new List<ThreatDetection>();

            await Task.Run(() =>
            {
                // Check Windows Defender status
                try
                {
                    var defenderStatus = GetWindowsDefenderStatus();
                    if (!defenderStatus.IsEnabled)
                    {
                        threats.Add(new ThreatDetection
                        {
                            Type = ThreatType.Configuration,
                            Severity = ThreatSeverity.Critical,
                            Description = "Windows Defender is disabled",
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch
                {
                }

                // Check firewall status
                try
                {
                    var firewallStatus = GetFirewallStatus();
                    if (!firewallStatus.IsEnabled)
                    {
                        threats.Add(new ThreatDetection
                        {
                            Type = ThreatType.Configuration,
                            Severity = ThreatSeverity.High,
                            Description = "Windows Firewall is disabled",
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch
                {
                }
            });

            return threats;
        }

        private async Task<List<ThreatDetection>> DetectRegistryThreatsAsync()
        {
            var threats = new List<ThreatDetection>();

            await Task.Run(() =>
            {
                // Check common persistence locations
                var persistenceKeys = new[]
                {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
                    @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run"
                };

                foreach (var key in persistenceKeys)
                {
                    try
                    {
                        using var regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                            key.Replace("HKEY_CURRENT_USER\\", ""));
                        
                        if (regKey != null)
                        {
                            var values = regKey.GetValueNames();
                            // Check for suspicious entries
                        }
                    }
                    catch
                    {
                    }
                }
            });

            return threats;
        }

        private bool HasValidDigitalSignature(string filePath)
        {
            try
            {
                // Use PowerShell to check signature
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Get-AuthenticodeSignature '{filePath}' | Select-Object -ExpandProperty Status\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Trim().Equals("Valid", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }

            return false;
        }

        private (bool IsEnabled, string Status) GetWindowsDefenderStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "Get-MpComputerStatus | Select-Object -ExpandProperty AntivirusEnabled",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    bool enabled = output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                    return (enabled, enabled ? "Enabled" : "Disabled");
                }
            }
            catch
            {
            }

            return (false, "Unknown");
        }

        private (bool IsEnabled, string Status) GetFirewallStatus()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall show allprofiles state",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    bool enabled = output.Contains("ON", StringComparison.OrdinalIgnoreCase);
                    return (enabled, enabled ? "Enabled" : "Disabled");
                }
            }
            catch
            {
            }

            return (false, "Unknown");
        }

        protected virtual void OnThreatDetected(ThreatDetection threat)
        {
            ThreatDetected?.Invoke(this, threat);
        }
    }

    public class ThreatIndicator
    {
        public IndicatorType Type { get; set; }
        public string Pattern { get; set; } = string.Empty;
    }

    public enum IndicatorType
    {
        ProcessName,
        FilePath,
        Network,
        Registry
    }

    public class ThreatDetection
    {
        public ThreatType Type { get; set; }
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum ThreatType
    {
        Process,
        Network,
        File,
        Configuration,
        Registry
    }

    public enum ThreatSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class ThreatReport
    {
        public DateTime ScanTime { get; set; }
        public List<ThreatDetection> Threats { get; set; } = new();
        public int ThreatCount { get; set; }
        public ThreatSeverity Severity { get; set; }
    }

    public class ThreatIntelligence
    {
        public string Indicator { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ThreatSeverity Severity { get; set; }
    }
}

