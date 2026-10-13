using System;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public class HardeningRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Func<Task>? ApplyAsync { get; set; }
        public Func<Task<bool>>? VerifyAsync { get; set; }
        public Func<Task>? RollbackAsync { get; set; }
        public bool RequiresAdmin { get; set; }
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


