namespace WindowsGuardianWatchdog.Core.Hardening
{
    public class HardeningResult
    {
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
        public bool WasApplied { get; set; }
        public string Category { get; set; } = "General";
        public HardeningSeverity Severity { get; set; } = HardeningSeverity.Medium;
        public string StatusText { get; set; } = "Unknown";
    }
}
