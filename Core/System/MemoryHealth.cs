using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace WindowsGuardianWatchdog.Core.Systems
{
    public class MemoryHealth
    {
        public MemoryHealthReport GetHealthReport()
        {
            var report = new MemoryHealthReport
            {
                ScanTime = DateTime.Now
            };

            try
            {
                var totalMemory = GetTotalPhysicalMemory();
                var availableMemory = GetAvailableMemory();
                var usedMemory = totalMemory - availableMemory;

                report.TotalMemory = totalMemory;
                report.AvailableMemory = availableMemory;
                report.UsedMemory = usedMemory;
                report.UsagePercent = (double)usedMemory / totalMemory * 100;

                // Get top memory consumers
                report.TopProcesses = GetTopMemoryProcesses(10);

                // Determine status
                report.Status = report.UsagePercent switch
                {
                    > 90 => HealthStatus.Critical,
                    > 75 => HealthStatus.Warning,
                    _ => HealthStatus.Healthy
                };
            }
            catch
            {
                report.Status = HealthStatus.Healthy;
            }

            return report;
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]);
                }
            }
            catch
            {
            }

            return 0;
        }

        private long GetAvailableMemory()
        {
            try
            {
                var pc = new PerformanceCounter("Memory", "Available Bytes");
                return (long)pc.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private List<ProcessMemoryInfo> GetTopMemoryProcesses(int count)
        {
            var processes = Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        return new ProcessMemoryInfo
                        {
                            Name = p.ProcessName,
                            Id = p.Id,
                            MemoryUsage = p.WorkingSet64
                        };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(p => p != null)
                .OrderByDescending(p => p!.MemoryUsage)
                .Take(count)
                .ToList();

            return processes!;
        }
    }

    public class MemoryHealthReport
    {
        public DateTime ScanTime { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public long UsedMemory { get; set; }
        public double UsagePercent { get; set; }
        public List<ProcessMemoryInfo> TopProcesses { get; set; } = new();
        public HealthStatus Status { get; set; }
    }

    public class ProcessMemoryInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public long MemoryUsage { get; set; }
    }
}

