using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsGuardianWatchdog.Config
{
    /// <summary>
    /// Application Configuration - JSON format for cross-platform compatibility
    /// Matches MacGuardian's config structure
    /// </summary>
    public class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsGuardianWatchdog",
            "config.json");

        public MonitoringConfig Monitoring { get; set; } = new();
        public AlertsConfig Alerts { get; set; } = new();
        public ReportsConfig Reports { get; set; } = new();
        public CleanupConfig Cleanup { get; set; } = new();
        public MLConfig ML { get; set; } = new();
        public string Theme { get; set; } = "omega_tech_dark";

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch
            {
                // Return default config on error
            }

            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Handle save errors
            }
        }

        public static AppConfig Default()
        {
            return new AppConfig
            {
                Monitoring = new MonitoringConfig
                {
                    ProcessMonitoringInterval = 30,
                    FileIntegrityInterval = 300,
                    ThreatHuntInterval = 3600,
                    EnableRealTimeMonitoring = true
                },
                Alerts = new AlertsConfig
                {
                    EnableEmailAlerts = false,
                    EmailRecipients = Array.Empty<string>(),
                    EnableToastNotifications = true,
                    AlertThrottling = true
                },
                Reports = new ReportsConfig
                {
                    EnableDailyReports = true,
                    EnableWeeklyReports = true,
                    EnableMonthlyReports = false,
                    ReportFormat = "html",
                    EmailReports = false
                },
                Cleanup = new CleanupConfig
                {
                    AutoCleanupEnabled = false,
                    CleanupInterval = 86400, // 24 hours
                    KeepTempFilesDays = 7
                },
                ML = new MLConfig
                {
                    EnableMLAnalysis = true,
                    BaselineLearningEnabled = true,
                    AnomalyThreshold = 0.7
                }
            };
        }
    }

    public class MonitoringConfig
    {
        public int ProcessMonitoringInterval { get; set; } = 30; // seconds
        public int FileIntegrityInterval { get; set; } = 300; // 5 minutes
        public int ThreatHuntInterval { get; set; } = 3600; // 1 hour
        public bool EnableRealTimeMonitoring { get; set; } = true;
    }

    public class AlertsConfig
    {
        public bool EnableEmailAlerts { get; set; } = false;
        public string[] EmailRecipients { get; set; } = Array.Empty<string>();
        public bool EnableToastNotifications { get; set; } = true;
        public bool AlertThrottling { get; set; } = true;
    }

    public class ReportsConfig
    {
        public bool EnableDailyReports { get; set; } = true;
        public bool EnableWeeklyReports { get; set; } = true;
        public bool EnableMonthlyReports { get; set; } = false;
        public string ReportFormat { get; set; } = "html";
        public bool EmailReports { get; set; } = false;
    }

    public class CleanupConfig
    {
        public bool AutoCleanupEnabled { get; set; } = false;
        public int CleanupInterval { get; set; } = 86400; // seconds
        public int KeepTempFilesDays { get; set; } = 7;
    }

    public class MLConfig
    {
        public bool EnableMLAnalysis { get; set; } = true;
        public bool BaselineLearningEnabled { get; set; } = true;
        public double AnomalyThreshold { get; set; } = 0.7;
    }
}

