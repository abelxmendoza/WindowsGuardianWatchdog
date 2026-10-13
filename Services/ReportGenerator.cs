using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Services;

namespace WindowsGuardianWatchdog.Services
{
    /// <summary>
    /// Report Generator - Matches MacGuardian's report format and Omega branding
    /// </summary>
    public class ReportGenerator
    {
        public string GenerateHTMLReport(SecurityReport report)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine("    <title>Windows Guardian Watchdog - Security Report</title>");
            html.AppendLine("    <style>");
            html.AppendLine(GetOmegaThemeCSS());
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // Header with Omega branding
            html.AppendLine("    <div class=\"header\">");
            html.AppendLine("        <h1>WINDOWSGUARDIAN WATCHDOG</h1>");
            html.AppendLine("        <h2>Security Report</h2>");
            html.AppendLine($"        <p class=\"report-meta\">Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"        <p class=\"report-meta\">Period: {report.Period.Start:yyyy-MM-dd} to {report.Period.End:yyyy-MM-dd}</p>");
            html.AppendLine("    </div>");
            
            // Executive Summary
            if (!string.IsNullOrEmpty(report.ExecutiveSummary))
            {
                html.AppendLine("    <div class=\"section\">");
                html.AppendLine("        <h3>Executive Summary</h3>");
                html.AppendLine($"        <pre class=\"summary\">{report.ExecutiveSummary}</pre>");
                html.AppendLine("    </div>");
            }
            
            // Security Score
            html.AppendLine("    <div class=\"section\">");
            html.AppendLine("        <h3>Security Score</h3>");
            html.AppendLine($"        <div class=\"score\">{report.AuditResults.Score}/100</div>");
            html.AppendLine($"        <div class=\"status\">{report.AuditResults.OverallStatus}</div>");
            html.AppendLine("    </div>");
            
            // Audit Findings
            html.AppendLine("    <div class=\"section\">");
            html.AppendLine("        <h3>Audit Findings</h3>");
            html.AppendLine("        <table>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>Category</th>");
            html.AppendLine("                    <th>Title</th>");
            html.AppendLine("                    <th>Severity</th>");
            html.AppendLine("                    <th>Recommendation</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");
            
            foreach (var finding in report.AuditResults.Findings)
            {
                html.AppendLine("                <tr>");
                html.AppendLine($"                    <td>{finding.Category}</td>");
                html.AppendLine($"                    <td>{finding.Title}</td>");
                html.AppendLine($"                    <td class=\"severity-{finding.Severity.ToString().ToLower()}\">{finding.Severity}</td>");
                html.AppendLine($"                    <td>{finding.Recommendation}</td>");
                html.AppendLine("                </tr>");
            }
            
            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");
            html.AppendLine("    </div>");
            
            // Threats Detected
            if (report.ThreatDetections.Count > 0)
            {
                html.AppendLine("    <div class=\"section\">");
                html.AppendLine("        <h3>Threats Detected</h3>");
                html.AppendLine("        <ul class=\"threat-list\">");
                foreach (var threat in report.ThreatDetections)
                {
                    html.AppendLine($"            <li class=\"threat-{threat.Severity.ToString().ToLower()}\">");
                    html.AppendLine($"                <strong>{threat.Description}</strong><br>");
                    html.AppendLine($"                <span class=\"details\">{threat.Details}</span><br>");
                    html.AppendLine($"                <span class=\"timestamp\">{threat.Timestamp:yyyy-MM-dd HH:mm:ss}</span>");
                    html.AppendLine("            </li>");
                }
                html.AppendLine("        </ul>");
                html.AppendLine("    </div>");
            }
            
            // Footer
            html.AppendLine("    <div class=\"footer\">");
            html.AppendLine("        <p>Generated by Windows Guardian Watchdog - Omega Technologies</p>");
            html.AppendLine("    </div>");
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        public string GenerateJSONReport(SecurityReport report)
        {
            // Match MacGuardian's Event Spec v1.0.0 format
            var jsonEvent = new
            {
                eventSpec = "1.0.0",
                platform = "windows",
                timestamp = report.GeneratedAt.ToString("O"),
                type = "security_report",
                reportType = report.ReportType.ToString().ToLower(),
                period = new
                {
                    start = report.Period.Start.ToString("O"),
                    end = report.Period.End.ToString("O")
                },
                data = new
                {
                    securityScore = report.AuditResults.Score,
                    overallStatus = report.AuditResults.OverallStatus.ToString(),
                    findings = report.AuditResults.Findings.Select(f => new
                    {
                        category = f.Category,
                        title = f.Title,
                        severity = f.Severity.ToString(),
                        description = f.Description,
                        recommendation = f.Recommendation
                    }),
                    threats = report.ThreatDetections.Select(t => new
                    {
                        type = t.Type.ToString(),
                        severity = t.Severity.ToString(),
                        description = t.Description,
                        details = t.Details,
                        timestamp = t.Timestamp.ToString("O")
                    }),
                    fileIntegrityViolations = report.FileIntegrityViolations.Count
                }
            };

            return JsonSerializer.Serialize(jsonEvent, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        public string GenerateTextReport(SecurityReport report)
        {
            var text = new StringBuilder();
            
            text.AppendLine("========================================");
            text.AppendLine("WINDOWSGUARDIAN WATCHDOG");
            text.AppendLine("Security Report");
            text.AppendLine("========================================");
            text.AppendLine();
            text.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"Period: {report.Period.Start:yyyy-MM-dd} to {report.Period.End:yyyy-MM-dd}");
            text.AppendLine();
            
            if (!string.IsNullOrEmpty(report.ExecutiveSummary))
            {
                text.AppendLine(report.ExecutiveSummary);
                text.AppendLine();
            }
            
            text.AppendLine($"Security Score: {report.AuditResults.Score}/100");
            text.AppendLine($"Status: {report.AuditResults.OverallStatus}");
            text.AppendLine();
            
            text.AppendLine("Audit Findings:");
            text.AppendLine("---------------");
            foreach (var finding in report.AuditResults.Findings)
            {
                text.AppendLine($"[{finding.Severity}] {finding.Title}");
                text.AppendLine($"  Category: {finding.Category}");
                text.AppendLine($"  Description: {finding.Description}");
                text.AppendLine($"  Recommendation: {finding.Recommendation}");
                text.AppendLine();
            }
            
            if (report.ThreatDetections.Count > 0)
            {
                text.AppendLine("Threats Detected:");
                text.AppendLine("-----------------");
                foreach (var threat in report.ThreatDetections)
                {
                    text.AppendLine($"[{threat.Severity}] {threat.Description}");
                    text.AppendLine($"  Details: {threat.Details}");
                    text.AppendLine($"  Time: {threat.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    text.AppendLine();
                }
            }
            
            text.AppendLine("========================================");
            text.AppendLine("End of Report");
            text.AppendLine("========================================");
            
            return text.ToString();
        }

        private string GetOmegaThemeCSS()
        {
            return @"
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: 'Segoe UI', Arial, sans-serif;
                    background: #000000;
                    color: #E6E6E6;
                    padding: 20px;
                    line-height: 1.6;
                }
                .header {
                    background: #1A1A1A;
                    padding: 30px;
                    border-left: 4px solid #8A2BE2;
                    margin-bottom: 30px;
                }
                .header h1 {
                    color: #8A2BE2;
                    font-size: 32px;
                    font-weight: bold;
                    margin-bottom: 10px;
                }
                .header h2 {
                    color: #00FFFF;
                    font-size: 24px;
                    font-weight: semi-bold;
                    margin-bottom: 15px;
                }
                .report-meta {
                    color: #808080;
                    font-size: 14px;
                    margin: 5px 0;
                }
                .section {
                    background: #1A1A1A;
                    padding: 20px;
                    margin-bottom: 20px;
                    border-radius: 8px;
                    border: 1px solid #5900A6;
                }
                .section h3 {
                    color: #8A2BE2;
                    font-size: 20px;
                    margin-bottom: 15px;
                    border-bottom: 2px solid #8A2BE2;
                    padding-bottom: 10px;
                }
                .score {
                    font-size: 48px;
                    font-weight: bold;
                    color: #00FFFF;
                    text-align: center;
                    margin: 20px 0;
                }
                .status {
                    text-align: center;
                    color: #808080;
                    font-size: 18px;
                }
                table {
                    width: 100%;
                    border-collapse: collapse;
                    margin-top: 15px;
                }
                th {
                    background: #0A0A0A;
                    color: #8A2BE2;
                    padding: 12px;
                    text-align: left;
                    border-bottom: 2px solid #8A2BE2;
                }
                td {
                    padding: 10px;
                    border-bottom: 1px solid #333;
                }
                tr:hover {
                    background: #0A0A0A;
                }
                .severity-critical { color: #FF0044; font-weight: bold; }
                .severity-high { color: #FF0044; }
                .severity-medium { color: #FFFF00; }
                .severity-low { color: #808080; }
                .threat-list {
                    list-style: none;
                }
                .threat-list li {
                    background: #0A0A0A;
                    padding: 15px;
                    margin-bottom: 10px;
                    border-left: 4px solid #FF0044;
                    border-radius: 4px;
                }
                .threat-critical { border-left-color: #FF0044; }
                .threat-high { border-left-color: #FF0044; }
                .threat-medium { border-left-color: #FFFF00; }
                .threat-low { border-left-color: #808080; }
                .details {
                    color: #999999;
                    font-size: 12px;
                }
                .timestamp {
                    color: #00FFFF;
                    font-size: 11px;
                    font-family: 'Consolas', monospace;
                }
                .summary {
                    background: #0A0A0A;
                    padding: 15px;
                    border-radius: 4px;
                    color: #E6E6E6;
                    font-family: 'Consolas', monospace;
                    white-space: pre-wrap;
                }
                .footer {
                    text-align: center;
                    color: #808080;
                    margin-top: 40px;
                    padding-top: 20px;
                    border-top: 1px solid #333;
                }
            ";
        }
    }
}

