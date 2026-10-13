using System;

namespace WindowsGuardianWatchdog.Models
{
    /// <summary>
    /// Represents a security event log entry
    /// </summary>
    public class SecurityEvent
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string? Source { get; set; }
    }
}
