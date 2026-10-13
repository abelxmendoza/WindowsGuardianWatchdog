using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace WindowsGuardianWatchdog.Core.Systems
{
    public class DiskHealth
    {
        public DiskHealthReport GetHealthReport()
        {
            var report = new DiskHealthReport
            {
                ScanTime = DateTime.Now,
                Drives = new List<DriveHealth>()
            };

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

            foreach (var drive in drives)
            {
                try
                {
                    var driveHealth = new DriveHealth
                    {
                        Name = drive.Name,
                        Label = drive.VolumeLabel,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.AvailableFreeSpace,
                        UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                        FreeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100,
                        FileSystem = drive.DriveFormat
                    };

                    // Determine health status
                    driveHealth.Status = driveHealth.FreeSpacePercent switch
                    {
                        < 10 => HealthStatus.Critical,
                        < 20 => HealthStatus.Warning,
                        _ => HealthStatus.Healthy
                    };

                    report.Drives.Add(driveHealth);
                }
                catch
                {
                    // Skip inaccessible drives
                }
            }

            report.OverallStatus = report.Drives.Any(d => d.Status == HealthStatus.Critical)
                ? HealthStatus.Critical
                : report.Drives.Any(d => d.Status == HealthStatus.Warning)
                    ? HealthStatus.Warning
                    : HealthStatus.Healthy;

            return report;
        }
    }

    public class DiskHealthReport
    {
        public DateTime ScanTime { get; set; }
        public List<DriveHealth> Drives { get; set; } = new();
        public HealthStatus OverallStatus { get; set; }
    }

    public class DriveHealth
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public double FreeSpacePercent { get; set; }
        public string FileSystem { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical
    }
}

