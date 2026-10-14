using System.Management;

namespace WindowsGuardianWatchdog.Core.Drivers
{
    public class DriverScanner
    {
        public IReadOnlyList<DriverInfo> Scan()
        {
            var health = ReadDeviceHealth();
            var drivers = new List<DriverInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, DeviceName, DriverVersion, DriverDate, InfName, DriverProviderName, IsSigned FROM Win32_PnPSignedDriver");

            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var deviceId = obj["DeviceID"]?.ToString() ?? string.Empty;
                    if (!seen.Add(deviceId)) continue;

                    var name = obj["DeviceName"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) name = obj["InfName"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    health.TryGetValue(deviceId, out var deviceHealth);
                    var driver = new DriverInfo
                    {
                        DeviceId = deviceId,
                        DeviceName = name,
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? string.Empty,
                        DriverDate = ParseDriverDate(obj["DriverDate"]?.ToString()),
                        InfName = obj["InfName"]?.ToString() ?? string.Empty,
                        DriverProviderName = obj["DriverProviderName"]?.ToString() ?? string.Empty,
                        IsSigned = ReadBool(obj["IsSigned"]),
                        ProblemCode = deviceHealth.Code,
                        DeviceStatus = deviceHealth.Status,
                        Health = Describe(deviceHealth.Code, deviceHealth.Status, ReadBool(obj["IsSigned"]))
                    };
                    drivers.Add(driver);
                }
            }

            foreach (var (deviceId, deviceHealth) in health)
            {
                if (!deviceHealth.NeedsAttention || !seen.Add(deviceId)) continue;
                drivers.Add(new DriverInfo
                {
                    DeviceId = deviceId,
                    DeviceName = string.IsNullOrWhiteSpace(deviceHealth.Name) ? deviceId : deviceHealth.Name,
                    DriverProviderName = deviceHealth.Manufacturer,
                    ProblemCode = deviceHealth.Code,
                    DeviceStatus = deviceHealth.Status,
                    IsSigned = true,
                    Health = Describe(deviceHealth.Code, deviceHealth.Status, isSigned: true)
                });
            }

            return drivers
                .OrderByDescending(driver => driver.NeedsAttention)
                .ThenBy(driver => driver.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, DeviceHealth> ReadDeviceHealth()
        {
            var health = new Dictionary<string, DeviceHealth>(StringComparer.OrdinalIgnoreCase);
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, Status, ConfigManagerErrorCode, Manufacturer FROM Win32_PnPEntity");
            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var id = obj["PNPDeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var code = ReadInt(obj["ConfigManagerErrorCode"]);
                    var status = obj["Status"]?.ToString() ?? string.Empty;
                    health[id] = new DeviceHealth(obj["Name"]?.ToString() ?? string.Empty, obj["Manufacturer"]?.ToString() ?? string.Empty, code, status);
                }
            }

            return health;
        }

        public static string Describe(int code, string? status, bool isSigned)
        {
            if (!isSigned && code == 0) return "Unsigned";
            return code switch
            {
                0 when status?.Equals("Degraded", StringComparison.OrdinalIgnoreCase) == true => "Degraded",
                0 when status?.Equals("Error", StringComparison.OrdinalIgnoreCase) == true => "Not working",
                0 => "Working",
                1 => "Not configured",
                10 => "Cannot start",
                14 => "Needs a restart",
                22 => "Disabled",
                24 => "Not present",
                28 => "Driver missing",
                31 => "Not working",
                43 => "Stopped by Windows",
                _ => $"Problem {code}"
            };
        }

        private static bool ReadBool(object? value) => value switch
        {
            bool flag => flag,
            string text => text.Equals("True", StringComparison.OrdinalIgnoreCase) || text == "1",
            _ => false
        };

        private static int ReadInt(object? value) => value switch
        {
            int number => number,
            uint number => (int)number,
            short number => number,
            ushort number => number,
            long number => (int)number,
            _ => int.TryParse(value?.ToString(), out var parsed) ? parsed : 0
        };

        private static DateTime? ParseDriverDate(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString) || dateString.Length < 8) return null;
            return DateTime.TryParseExact(dateString[..8], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
                ? date
                : null;
        }

        private readonly record struct DeviceHealth(string Name, string Manufacturer, int Code, string Status)
        {
            public bool NeedsAttention => Code != 0
                || Status.Equals("Error", StringComparison.OrdinalIgnoreCase)
                || Status.Equals("Degraded", StringComparison.OrdinalIgnoreCase);
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
        public int ProblemCode { get; set; }
        public string DeviceStatus { get; set; } = string.Empty;
        public string Health { get; set; } = "Working";
        public DriverStatus Status { get; set; } = DriverStatus.Unknown;

        public bool NeedsAttention => Health is not ("Working" or "Signed");
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
