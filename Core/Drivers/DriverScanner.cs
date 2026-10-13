using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WindowsGuardianWatchdog.Core.Drivers
{
    public class DriverScanner
    {
        public IEnumerable<DriverInfo> Scan()
        {
            var drivers = new List<DriverInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPSignedDriver");
                
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var driver = new DriverInfo
                        {
                            DeviceId = obj["DeviceID"]?.ToString() ?? "",
                            DeviceName = obj["DeviceName"]?.ToString() ?? "",
                            DriverVersion = obj["DriverVersion"]?.ToString() ?? "",
                            DriverDate = ParseDriverDate(obj["DriverDate"]?.ToString()),
                            InfName = obj["InfName"]?.ToString() ?? "",
                            DriverProviderName = obj["DriverProviderName"]?.ToString() ?? "",
                            IsSigned = obj["IsSigned"] != null && (bool)obj["IsSigned"],
                            DriverType = obj["DriverType"]?.ToString() ?? ""
                        };

                        drivers.Add(driver);
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }
            }
            catch
            {
                // Handle WMI errors
            }

            return drivers;
        }

        private DateTime? ParseDriverDate(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return null;

            // Driver date format is typically YYYYMMDDHHMMSS.000000-000
            if (dateString.Length >= 8)
            {
                var datePart = dateString.Substring(0, 8);
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, 
                    System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }

            return null;
        }
    }

    public class DriverInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public DateTime? DriverDate { get; set; }
        public string InfName { get; set; } = string.Empty;
        public string DriverProviderName { get; set; } = string.Empty;
        public bool IsSigned { get; set; }
        public string DriverType { get; set; } = string.Empty;
        public bool IsUpdateAvailable { get; set; }
        public string? LatestVersion { get; set; }
        public DriverStatus Status { get; set; } = DriverStatus.Unknown;
    }

    public enum DriverStatus
    {
        Unknown,
        UpToDate,
        Outdated,
        Missing,
        Error
    }
}

