using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Systems
{
    public class SystemHealthMonitor
    {
        public class SystemHealth
        {
            public double CpuUsage { get; set; }
            public double MemoryUsage { get; set; }
            public double DiskUsage { get; set; }
            public long TotalMemory { get; set; }
            public long AvailableMemory { get; set; }
            public long UsedMemory { get; set; }
            public HealthStatus Status { get; set; }
            public DateTime LastUpdate { get; set; }
        }

        public enum HealthStatus
        {
            Healthy,
            Warning,
            Critical
        }

        private PerformanceCounter? _cpuCounter;
        private bool _initialized = false;

        public SystemHealthMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call returns 0
                _initialized = true;
            }
            catch
            {
                _initialized = false;
            }
        }

        public async Task<SystemHealth> GetHealthAsync()
        {
            return await Task.Run(() =>
            {
                var health = new SystemHealth
                {
                    LastUpdate = DateTime.Now
                };

                try
                {
                    // CPU Usage
                    if (_initialized && _cpuCounter != null)
                    {
                        health.CpuUsage = _cpuCounter.NextValue();
                    }
                    else
                    {
                        // Fallback method using WMI
                        using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            health.CpuUsage = Convert.ToDouble(obj["LoadPercentage"]);
                            break;
                        }
                    }

                    // Memory Usage
                    using var memSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory, AvailablePhysicalMemory FROM Win32_ComputerSystem");
                    foreach (ManagementObject obj in memSearcher.Get())
                    {
                        health.TotalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                        break;
                    }

                    using var availMemSearcher = new ManagementObjectSearcher("SELECT AvailableBytes FROM Win32_PerfRawData_PerfOS_Memory");
                    foreach (ManagementObject obj in availMemSearcher.Get())
                    {
                        health.AvailableMemory = Convert.ToInt64(obj["AvailableBytes"]);
                        break;
                    }

                    if (health.TotalMemory > 0)
                    {
                        health.UsedMemory = health.TotalMemory - health.AvailableMemory;
                        health.MemoryUsage = (double)health.UsedMemory / health.TotalMemory * 100;
                    }

                    // Disk Usage (C: drive)
                    var drive = new System.IO.DriveInfo("C:");
                    if (drive.IsReady)
                    {
                        var totalSpace = drive.TotalSize;
                        var freeSpace = drive.AvailableFreeSpace;
                        var usedSpace = totalSpace - freeSpace;
                        health.DiskUsage = (double)usedSpace / totalSpace * 100;
                    }

                    // Determine overall health status
                    health.Status = DetermineHealthStatus(health);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting system health: {ex.Message}");
                    health.Status = HealthStatus.Warning;
                }

                return health;
            });
        }

        private HealthStatus DetermineHealthStatus(SystemHealth health)
        {
            if (health.CpuUsage > 90 || health.MemoryUsage > 90 || health.DiskUsage > 90)
                return HealthStatus.Critical;
            
            if (health.CpuUsage > 70 || health.MemoryUsage > 70 || health.DiskUsage > 80)
                return HealthStatus.Warning;

            return HealthStatus.Healthy;
        }

        public void Dispose()
        {
            _cpuCounter?.Dispose();
        }
    }
}

