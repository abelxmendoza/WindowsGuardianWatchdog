using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core;

namespace WindowsGuardianWatchdog.Core.Alerts
{
    /// <summary>
    /// Advanced Alert Engine - Omega Guardian
    /// Matches MacGuardian's advanced_alerting.sh and OmegaGuardian SwiftUI components
    /// </summary>
    public class AlertEngine
    {
        private readonly List<AlertRule> _rules;
        private readonly Dictionary<string, DateTime> _lastAlertTime;
        private readonly Dictionary<string, int> _alertCounts;
        private readonly IncidentStore _incidentStore;

        public event EventHandler<Alert>? AlertRaised;
        public event EventHandler<Incident>? IncidentCreated;

        public AlertEngine()
        {
            _rules = new List<AlertRule>();
            _lastAlertTime = new Dictionary<string, DateTime>();
            _alertCounts = new Dictionary<string, int>();
            _incidentStore = new IncidentStore();
            
            LoadDefaultRules();
        }

        /// <summary>
        /// Process event through alert rules
        /// </summary>
        public async Task ProcessEventAsync(AlertSecurityEvent securityEvent)
        {
            await Task.Run(() =>
            {
                foreach (var rule in _rules.Where(r => r.IsEnabled && r.Matches(securityEvent)))
                {
                    // Check throttling/cooldown
                    if (IsThrottled(rule))
                        continue;

                    // Create alert
                    var alert = new Alert
                    {
                        Id = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Severity = rule.Severity,
                        Message = rule.FormatMessage(securityEvent),
                        Category = securityEvent.Category,
                        Timestamp = DateTime.Now,
                        Event = securityEvent
                    };

                    // Update throttling counters
                    UpdateThrottling(rule);

                    // Raise alert
                    OnAlertRaised(alert);

                    // Create incident if severity is high enough
                    if (rule.Severity >= AlertSeverity.High)
                    {
                        var incident = _incidentStore.CreateIncident(alert);
                        OnIncidentCreated(incident);
                    }
                }
            });
        }

        /// <summary>
        /// Add custom alert rule
        /// </summary>
        public void AddRule(AlertRule rule)
        {
            _rules.Add(rule);
        }

        /// <summary>
        /// Remove alert rule
        /// </summary>
        public void RemoveRule(string ruleId)
        {
            _rules.RemoveAll(r => r.Id == ruleId);
        }

        /// <summary>
        /// Get all active incidents
        /// </summary>
        public List<Incident> GetActiveIncidents()
        {
            return _incidentStore.GetActiveIncidents();
        }

        private void LoadDefaultRules()
        {
            // Critical security events
            AddRule(new AlertRule
            {
                Id = "critical_security_breach",
                Name = "Critical Security Breach",
                Severity = AlertSeverity.Critical,
                Condition = e => e.Severity == "Critical",
                ThrottleMinutes = 0, // No throttling for critical
                MessageFormat = "CRITICAL: {Message}"
            });

            // High severity threats
            AddRule(new AlertRule
            {
                Id = "high_severity_threat",
                Name = "High Severity Threat",
                Severity = AlertSeverity.High,
                Condition = e => e.Severity == "High",
                ThrottleMinutes = 5,
                MessageFormat = "HIGH: {Message}"
            });

            // File integrity violations
            AddRule(new AlertRule
            {
                Id = "file_integrity_violation",
                Name = "File Integrity Violation",
                Severity = AlertSeverity.Medium,
                Condition = e => e.Category == "FileIntegrity",
                ThrottleMinutes = 15,
                MessageFormat = "File Integrity: {Message}"
            });

            // Process anomalies
            AddRule(new AlertRule
            {
                Id = "process_anomaly",
                Name = "Process Anomaly",
                Severity = AlertSeverity.Medium,
                Condition = e => e.Category == "Process" && e.Message.Contains("suspicious", StringComparison.OrdinalIgnoreCase),
                ThrottleMinutes = 10,
                MessageFormat = "Process Alert: {Message}"
            });

            // Network anomalies
            AddRule(new AlertRule
            {
                Id = "network_anomaly",
                Name = "Network Anomaly",
                Severity = AlertSeverity.Medium,
                Condition = e => e.Category == "Network",
                ThrottleMinutes = 10,
                MessageFormat = "Network Alert: {Message}"
            });
        }

        private bool IsThrottled(AlertRule rule)
        {
            if (rule.ThrottleMinutes == 0)
                return false;

            if (_lastAlertTime.TryGetValue(rule.Id, out var lastTime))
            {
                var timeSinceLastAlert = DateTime.Now - lastTime;
                if (timeSinceLastAlert.TotalMinutes < rule.ThrottleMinutes)
                    return true;
            }

            return false;
        }

        private void UpdateThrottling(AlertRule rule)
        {
            _lastAlertTime[rule.Id] = DateTime.Now;
            _alertCounts[rule.Id] = _alertCounts.GetValueOrDefault(rule.Id, 0) + 1;
        }

        protected virtual void OnAlertRaised(Alert alert)
        {
            AlertRaised?.Invoke(this, alert);
        }

        protected virtual void OnIncidentCreated(Incident incident)
        {
            IncidentCreated?.Invoke(this, incident);
        }
    }

    public class AlertRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public Func<AlertSecurityEvent, bool> Condition { get; set; } = _ => false;
        public int ThrottleMinutes { get; set; }
        public string MessageFormat { get; set; } = "{Message}";
        public bool IsEnabled { get; set; } = true;

        public bool Matches(AlertSecurityEvent securityEvent)
        {
            return Condition(securityEvent);
        }

        public string FormatMessage(AlertSecurityEvent securityEvent)
        {
            return MessageFormat
                .Replace("{Message}", securityEvent.Message)
                .Replace("{Category}", securityEvent.Category)
                .Replace("{Severity}", securityEvent.Severity);
        }
    }

    public class Alert
    {
        public string Id { get; set; } = string.Empty;
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public AlertSecurityEvent? Event { get; set; }
    }

    public class Incident
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public IncidentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public List<Alert> Alerts { get; set; } = new();
        public string Description { get; set; } = string.Empty;
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum IncidentStatus
    {
        Open,
        Investigating,
        Resolved,
        FalsePositive
    }

    public class AlertSecurityEvent
    {
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class IncidentStore
    {
        private readonly List<Incident> _incidents = new();

        public Incident CreateIncident(Alert alert)
        {
            var incident = new Incident
            {
                Id = Guid.NewGuid().ToString(),
                Title = alert.Message,
                Severity = alert.Severity,
                Status = IncidentStatus.Open,
                CreatedAt = DateTime.Now,
                Alerts = new List<Alert> { alert },
                Description = $"Incident created from alert: {alert.RuleName}"
            };

            _incidents.Add(incident);
            return incident;
        }

        public List<Incident> GetActiveIncidents()
        {
            return _incidents.Where(i => i.Status != IncidentStatus.Resolved).ToList();
        }

        public void ResolveIncident(string incidentId)
        {
            var incident = _incidents.FirstOrDefault(i => i.Id == incidentId);
            if (incident != null)
            {
                incident.Status = IncidentStatus.Resolved;
                incident.ResolvedAt = DateTime.Now;
            }
        }
    }
}

