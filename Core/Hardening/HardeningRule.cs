namespace WindowsGuardianWatchdog.Core.Hardening
{
    public class HardeningRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ApplyScript { get; set; }
        public string? RollbackScript { get; set; }
        public string? VerifyExpression { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool Recommended { get; set; } = true;
        public string Category { get; set; } = "General";
        public HardeningSeverity Severity { get; set; } = HardeningSeverity.Medium;
    }

    public enum HardeningSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
