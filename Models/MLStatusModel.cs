namespace WindowsGuardianWatchdog.Models
{
    public class MLStatusModel
    {
        public int AnomalyScore { get; set; } // 0-100
        public string BaselineState { get; set; } = string.Empty; // Stable | Adapting | Drift | NotTrained
        public string RiskTrend { get; set; } = string.Empty; // Up | Down | Flat
        public DateTime LastAnalysis { get; set; }
    }

    public class AnomalyModel
    {
        public string Source { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Patterns { get; set; } = new();
    }

    public class BaselineModel
    {
        public string State { get; set; } = string.Empty;
        public bool IsTrained { get; set; }
        public bool IsLearning { get; set; }
    }
}

