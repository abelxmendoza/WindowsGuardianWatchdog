using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace WindowsGuardianWatchdog.Core.ML
{
    /// <summary>
    /// Feature Extractor - Extracts system metrics for ML analysis
    /// Matches MacGuardian's feature extraction approach
    /// </summary>
    public class FeatureExtractor
    {
        public FeatureVector Extract(SystemMetrics metrics)
        {
            return new FeatureVector
            {
                Timestamp = metrics.Timestamp,
                Features = new double[]
                {
                    NormalizeProcessCount(metrics.ProcessCount),
                    NormalizeNetworkConnections(metrics.NetworkConnections),
                    metrics.CpuUsage, // Already 0-100
                    NormalizeMemoryUsage(metrics.MemoryUsage),
                    NormalizeDiskActivity(metrics.DiskActivity),
                    NormalizeFileSystemChanges(metrics.FileSystemChanges),
                    NormalizeRegistryChanges(metrics.RegistryChanges),
                    NormalizeActiveUsers(metrics.ActiveUsers)
                },
                Metadata = new Dictionary<string, object>
                {
                    ["platform"] = "windows",
                    ["processCount"] = metrics.ProcessCount,
                    ["networkConnections"] = metrics.NetworkConnections,
                    ["cpuUsage"] = metrics.CpuUsage,
                    ["memoryUsage"] = metrics.MemoryUsage
                }
            };
        }

        public SystemMetrics CollectCurrentMetrics()
        {
            return new SystemMetrics
            {
                Timestamp = DateTime.Now,
                ProcessCount = Process.GetProcesses().Length,
                NetworkConnections = GetNetworkConnectionCount(),
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                DiskActivity = GetDiskActivity(),
                FileSystemChanges = 0, // Would track from file monitor
                RegistryChanges = 0, // Would track from registry monitor
                ActiveUsers = GetActiveUserCount()
            };
        }

        private double NormalizeProcessCount(int count)
        {
            // Normalize to 0-1 range (assuming 0-500 processes)
            return Math.Min(1.0, count / 500.0);
        }

        private double NormalizeNetworkConnections(int connections)
        {
            // Normalize to 0-1 range (assuming 0-200 connections)
            return Math.Min(1.0, connections / 200.0);
        }

        private double NormalizeMemoryUsage(double usageMB)
        {
            // Normalize to 0-1 range (assuming 0-32GB)
            return Math.Min(1.0, usageMB / 32768.0);
        }

        private double NormalizeDiskActivity(double activity)
        {
            // Normalize to 0-1 range
            return Math.Min(1.0, activity / 100.0);
        }

        private double NormalizeFileSystemChanges(int changes)
        {
            // Normalize to 0-1 range (assuming 0-1000 changes)
            return Math.Min(1.0, changes / 1000.0);
        }

        private double NormalizeRegistryChanges(int changes)
        {
            // Normalize to 0-1 range (assuming 0-100 changes)
            return Math.Min(1.0, changes / 100.0);
        }

        private double NormalizeActiveUsers(int users)
        {
            // Normalize to 0-1 range (assuming 0-10 users)
            return Math.Min(1.0, users / 10.0);
        }

        private int GetNetworkConnectionCount()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkConnection");
                return searcher.Get().Count;
            }
            catch
            {
                return 0;
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0
                System.Threading.Thread.Sleep(100);
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0.0;
            }
        }

        private double GetMemoryUsage()
        {
            try
            {
                using var memCounter = new PerformanceCounter("Memory", "Available MBytes");
                var totalMem = GetTotalPhysicalMemory() / (1024 * 1024); // Convert to MB
                var availableMem = memCounter.NextValue();
                return totalMem - availableMem; // Used memory
            }
            catch
            {
                return 0.0;
            }
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
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

        private double GetDiskActivity()
        {
            // Simplified - would use disk I/O counters
            return 0.0;
        }

        private int GetActiveUserCount()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var users = obj["NumberOfUsers"];
                    return users != null ? Convert.ToInt32(users) : 1;
                }
            }
            catch
            {
            }
            return 1;
        }
    }

    public class FeatureVector
    {
        public DateTime Timestamp { get; set; }
        public double[] Features { get; set; } = Array.Empty<double>();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

