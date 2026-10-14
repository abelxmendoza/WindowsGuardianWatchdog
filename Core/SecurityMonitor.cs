using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core
{
    public class SecurityMonitor
    {
        private readonly List<ProcessInfo> _monitoredProcesses;
        private readonly Timer _monitoringTimer;
        private bool _isMonitoring = false;

        public bool IsMonitoring => _isMonitoring;

        public event EventHandler<SecurityAlert>? AlertRaised;
        public event EventHandler<ProcessInfo>? ProcessDetected;

        public SecurityMonitor()
        {
            _monitoredProcesses = new List<ProcessInfo>();
            _monitoringTimer = new Timer(MonitorTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartMonitoring(int intervalMs = 5000)
        {
            if (_isMonitoring) return;
            
            _isMonitoring = true;
            _monitoringTimer.Change(0, intervalMs);
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void MonitorTick(object? state)
        {
            if (!_isMonitoring) return;

            try
            {
                MonitorProcesses();
                MonitorNetworkConnections();
                CheckSystemIntegrity();
            }
            catch (Exception ex)
            {
                OnAlertRaised(new SecurityAlert
                {
                    Severity = AlertSeverity.Warning,
                    Category = "Monitoring",
                    Message = $"Monitoring error: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
        }

        private void MonitorProcesses()
        {
            var currentProcesses = Process.GetProcesses()
                .Select(p => new ProcessInfo
                {
                    Id = p.Id,
                    Name = p.ProcessName,
                    Path = GetProcessPath(p),
                    StartTime = GetProcessStartTime(p),
                    MemoryUsage = p.WorkingSet64,
                    CpuTime = GetCpuTime(p),
                    PriorityText = ProcessInfo.ReadPriority(p),
                    Importance = ProcessInfo.DescribeImportance(p),
                    IsProtected = ProcessInfo.IsProtectedProcess(p.ProcessName) || p.Id == Environment.ProcessId
                })
                .ToList();

            // Detect new suspicious processes
            foreach (var proc in currentProcesses)
            {
                if (!_monitoredProcesses.Any(p => p.Id == proc.Id))
                {
                    ProcessDetected?.Invoke(this, proc);
                    
                    // Check for suspicious characteristics
                    if (IsSuspiciousProcess(proc))
                    {
                        OnAlertRaised(new SecurityAlert
                        {
                            Severity = AlertSeverity.High,
                            Category = "Process Monitoring",
                            Message = $"Suspicious process detected: {proc.Name} (PID: {proc.Id})",
                            Details = $"Path: {proc.Path}",
                            Timestamp = DateTime.Now
                        });
                    }
                }
            }

            _monitoredProcesses.Clear();
            _monitoredProcesses.AddRange(currentProcesses);
        }

        private void MonitorNetworkConnections()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkConnection");
                
                var connections = searcher.Get();
                // Network monitoring logic here
            }
            catch
            {
                // Silently handle errors
            }
        }

        private void CheckSystemIntegrity()
        {
            // Check Windows Defender status
            try
            {
                var defenderStatus = GetWindowsDefenderStatus();
                if (!defenderStatus.IsEnabled)
                {
                    OnAlertRaised(new SecurityAlert
                    {
                        Severity = AlertSeverity.Critical,
                        Category = "System Integrity",
                        Message = "Windows Defender is not enabled",
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch
            {
                // Handle errors
            }
        }

        private bool IsSuspiciousProcess(ProcessInfo proc)
        {
            // Check for suspicious paths
            var suspiciousPaths = new[]
            {
                @"\Temp\",
                @"\AppData\Local\Temp\",
                @"\Users\Public\"
            };

            if (suspiciousPaths.Any(path => proc.Path.Contains(path, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check for processes with no company name
            try
            {
                var fileInfo = new System.IO.FileInfo(proc.Path);
                if (fileInfo.Exists)
                {
                    // Additional checks can be added here
                }
            }
            catch
            {
                return true; // If we can't verify, consider suspicious
            }

            return false;
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private DateTime GetProcessStartTime(Process process)
        {
            try
            {
                return process.StartTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private TimeSpan GetCpuTime(Process process)
        {
            try
            {
                return process.TotalProcessorTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
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

        protected virtual void OnAlertRaised(SecurityAlert alert)
        {
            AlertRaised?.Invoke(this, alert);
        }

        public void Dispose()
        {
            StopMonitoring();
            _monitoringTimer?.Dispose();
        }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public long MemoryUsage { get; set; }
        public TimeSpan CpuTime { get; set; }
        
        // WinUI 3 compatible formatted property (replaces StringFormat)
        public string MemoryUsageText => FormatSize(MemoryUsage);
        public string PriorityText { get; set; } = "Normal";
        public string Importance { get; set; } = "User";
        public bool IsProtected { get; set; }

        public static bool IsProtectedProcess(string processName)
        {
            return ProtectedNames.Contains(processName);
        }

        public static string ReadPriority(Process process)
        {
            try
            {
                return process.PriorityClass.ToString();
            }
            catch
            {
                return "System";
            }
        }

        public static string DescribeImportance(Process process)
        {
            if (ProtectedNames.Contains(process.ProcessName) || process.Id == Environment.ProcessId)
            {
                return "Protected";
            }

            try
            {
                return process.PriorityClass switch
                {
                    ProcessPriorityClass.RealTime or ProcessPriorityClass.High => "High",
                    ProcessPriorityClass.AboveNormal => "Above normal",
                    ProcessPriorityClass.BelowNormal or ProcessPriorityClass.Idle => "Background",
                    _ => "Normal"
                };
            }
            catch
            {
                return "System";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024 / 1024:0.0} GB";
            if (bytes >= 1024L * 1024) return $"{bytes / 1024d / 1024:0.0} MB";
            return $"{bytes / 1024d:0} KB";
        }

        private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Registry", "Idle", "Secure System", "Memory Compression",
            "smss", "csrss", "wininit", "services", "lsass", "winlogon", "dwm",
            "fontdrvhost", "svchost", "LsaIso", "SecurityHealthService", "MsMpEng",
            "sihost", "ctfmon", "RuntimeBroker", "explorer", "SearchHost",
            "StartMenuExperienceHost", "ShellExperienceHost", "TextInputHost",
            "WindowsGuardianWatchdog"
        };
    }

    public class SecurityAlert
    {
        public AlertSeverity Severity { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum AlertSeverity
    {
        Info,
        Low,
        Warning,
        High,
        Critical
    }
}

