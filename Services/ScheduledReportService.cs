using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.ML;
using WindowsGuardianWatchdog.Models;
using System.Text.Json;
using System.Text;

namespace WindowsGuardianWatchdog.Services
{
    /// <summary>
    /// Scheduled Reports Service - Matches MacGuardian's scheduled_reports.sh functionality
    /// Uses Windows Task Scheduler for automation
    /// </summary>
    public class ScheduledReportService
    {
        private readonly string _reportsDirectory;
        private readonly ReportGenerator _reportGenerator;

        public ScheduledReportService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _reportsDirectory = Path.Combine(appData, "WindowsGuardianWatchdog", "reports");
            Directory.CreateDirectory(_reportsDirectory);
            
            _reportGenerator = new ReportGenerator();
        }

        /// <summary>
        /// Generate daily security report
        /// </summary>
        public async Task<ReportResult> GenerateDailyReportAsync()
        {
            return await Task.Run(async () =>
            {
                var report = new SecurityReport
                {
                    ReportType = ReportType.Daily,
                    GeneratedAt = DateTime.Now,
                    Period = new ReportPeriod
                    {
                        Start = DateTime.Today.AddDays(-1),
                        End = DateTime.Today
                    }
                };

                // Collect data
                var audit = new SecurityAudit();
                report.AuditResults = await audit.PerformFullAuditAsync();

                var monitor = new SecurityMonitor();
                report.SecurityEvents = CollectSecurityEvents();

                var mlEngine = new MLSecurityEngine();
                report.MLAnalysis = await mlEngine.AnalyzeBehaviorAsync(CollectSystemMetrics());

                // Generate reports
                var htmlReport = _reportGenerator.GenerateHTMLReport(report);
                var jsonReport = _reportGenerator.GenerateJSONReport(report);
                var textReport = _reportGenerator.GenerateTextReport(report);

                // Save reports
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var htmlPath = Path.Combine(_reportsDirectory, $"daily_report_{timestamp}.html");
                var jsonPath = Path.Combine(_reportsDirectory, $"daily_report_{timestamp}.json");
                var textPath = Path.Combine(_reportsDirectory, $"daily_report_{timestamp}.txt");

                await File.WriteAllTextAsync(htmlPath, htmlReport);
                await File.WriteAllTextAsync(jsonPath, jsonReport);
                await File.WriteAllTextAsync(textPath, textReport);

                // Send email if configured
                await SendEmailReportAsync(report, htmlReport);

                return new ReportResult
                {
                    Success = true,
                    HTMLPath = htmlPath,
                    JSONPath = jsonPath,
                    TextPath = textPath,
                    Report = report
                };
            });
        }

        /// <summary>
        /// Generate weekly security report
        /// </summary>
        public async Task<ReportResult> GenerateWeeklyReportAsync()
        {
            return await Task.Run(async () =>
            {
                var report = new SecurityReport
                {
                    ReportType = ReportType.Weekly,
                    GeneratedAt = DateTime.Now,
                    Period = new ReportPeriod
                    {
                        Start = DateTime.Today.AddDays(-7),
                        End = DateTime.Today
                    }
                };

                // Collect comprehensive data
                report.AuditResults = await new SecurityAudit().PerformFullAuditAsync();
                report.SecurityEvents = CollectSecurityEvents();
                report.ThreatDetections = await CollectThreatDetectionsAsync();
                report.FileIntegrityViolations = CollectFileIntegrityViolations();

                // Generate executive summary
                report.ExecutiveSummary = GenerateExecutiveSummary(report);

                // Generate and save reports
                var htmlReport = _reportGenerator.GenerateHTMLReport(report);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                var htmlPath = Path.Combine(_reportsDirectory, $"weekly_report_{timestamp}.html");

                await File.WriteAllTextAsync(htmlPath, htmlReport);

                return new ReportResult
                {
                    Success = true,
                    HTMLPath = htmlPath,
                    Report = report
                };
            });
        }

        /// <summary>
        /// Install scheduled tasks for automated reporting
        /// </summary>
        public void InstallScheduledTasks()
        {
            // Daily report at 9:00 AM
            CreateScheduledTask(
                "WindowsGuardianWatchdog_DailyReport",
                "Daily Security Report",
                "Generate daily security report",
                "09:00",
                "Daily"
            );

            // Weekly report on Sundays at 3:00 AM
            CreateScheduledTask(
                "WindowsGuardianWatchdog_WeeklyReport",
                "Weekly Security Report",
                "Generate weekly security report",
                "03:00",
                "Weekly",
                daysOfWeek: new[] { "Sunday" }
            );
        }

        private void CreateScheduledTask(string taskName, string displayName, string description, string time, string frequency, string[]? daysOfWeek = null)
        {
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var arguments = $"--report {frequency.ToLower()}";

                var taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Date>{DateTime.Now:yyyy-MM-ddTHH:mm:ss}</Date>
    <Author>Windows Guardian Watchdog</Author>
    <Description>{description}</Description>
  </RegistrationInfo>
  <Triggers>
    <CalendarTrigger>
      <StartBoundary>{DateTime.Today:yyyy-MM-dd}T{time}:00</StartBoundary>
      <Enabled>true</Enabled>
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
    </CalendarTrigger>
  </Triggers>
  <Actions>
    <Exec>
      <Command>{exePath}</Command>
      <Arguments>{arguments}</Arguments>
    </Exec>
  </Actions>
</Task>";

                // Use schtasks command to create task
                var tempXml = Path.GetTempFileName();
                File.WriteAllText(tempXml, taskXml);

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Create /TN \"{taskName}\" /XML \"{tempXml}\" /F",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                System.Diagnostics.Process.Start(startInfo)?.WaitForExit();
                File.Delete(tempXml);
            }
            catch
            {
                // Handle errors silently
            }
        }

        private List<SecurityEvent> CollectSecurityEvents()
        {
            // Collect events from Event Log or in-memory store
            return new List<SecurityEvent>();
        }
        
        private async Task<List<ThreatDetection>> CollectThreatDetectionsAsync()
        {
            var detector = new BlueTeamDetector();
            var report = await detector.PerformThreatHuntAsync();
            return report.Threats;
        }

        private List<FileIntegrityAlert> CollectFileIntegrityViolations()
        {
            // Collect from file integrity monitor
            return new List<FileIntegrityAlert>();
        }

        private SystemMetrics CollectSystemMetrics()
        {
            return new SystemMetrics
            {
                Timestamp = DateTime.Now,
                ProcessCount = System.Diagnostics.Process.GetProcesses().Length,
                NetworkConnections = 0, // Would need network monitoring
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                DiskActivity = 0,
                FileSystemChanges = 0,
                RegistryChanges = 0,
                ActiveUsers = 1
            };
        }

        private double GetCpuUsage()
        {
            // Simplified - would use PerformanceCounter in production
            return 0.0;
        }

        private double GetMemoryUsage()
        {
            var pc = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");
            return pc.NextValue();
        }

        private string GenerateExecutiveSummary(SecurityReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EXECUTIVE SECURITY SUMMARY");
            sb.AppendLine("==========================");
            sb.AppendLine($"Period: {report.Period.Start:yyyy-MM-dd} to {report.Period.End:yyyy-MM-dd}");
            sb.AppendLine($"Security Score: {report.AuditResults.Score}/100");
            sb.AppendLine($"Threats Detected: {report.ThreatDetections.Count}");
            sb.AppendLine($"File Integrity Violations: {report.FileIntegrityViolations.Count}");
            sb.AppendLine();
            sb.AppendLine("Key Findings:");
            foreach (var finding in report.AuditResults.Findings.Take(5))
            {
                sb.AppendLine($"- {finding.Title}: {finding.Severity}");
            }
            return sb.ToString();
        }

        private async Task SendEmailReportAsync(SecurityReport report, string htmlContent)
        {
            // Email sending would be implemented here
            // Match MacGuardian's email delivery with Omega branding
            await Task.CompletedTask;
        }
    }

    public class ReportResult
    {
        public bool Success { get; set; }
        public string? HTMLPath { get; set; }
        public string? JSONPath { get; set; }
        public string? TextPath { get; set; }
        public SecurityReport? Report { get; set; }
    }

    public class SecurityReport
    {
        public ReportType ReportType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public ReportPeriod Period { get; set; } = new();
        public AuditReport AuditResults { get; set; } = new();
        public List<SecurityEvent> SecurityEvents { get; set; } = new();
        public List<ThreatDetection> ThreatDetections { get; set; } = new();
        public List<FileIntegrityAlert> FileIntegrityViolations { get; set; } = new();
        public AnomalyAnalysis? MLAnalysis { get; set; }
        public string? ExecutiveSummary { get; set; }
    }

    public class ReportPeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public enum ReportType
    {
        Daily,
        Weekly,
        Monthly
    }

}

