using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.ML
{
    /// <summary>
    /// ML Status Provider - Provides ML status for UI display
    /// </summary>
    public class MLStatusProvider
    {
        private readonly MLSecurityEngine _engine;

        public MLStatusProvider(MLSecurityEngine engine)
        {
            _engine = engine;
        }

        public MLStatus GetStatus()
        {
            return new MLStatus
            {
                AnomalyScore = (int)(_engine.CurrentAnomalyScore * 100), // Convert to 0-100
                BaselineState = _engine.BaselineState.ToString(),
                RiskTrend = _engine.RiskTrend,
                LastAnalysis = DateTime.Now
            };
        }

        public IEnumerable<Anomaly> GetTopAnomalies(int limit = 5)
        {
            return _engine.GetRecentAnomalies(limit)
                .Select(a => new Anomaly
                {
                    Source = "ML Analysis",
                    Description = $"Anomaly detected (Score: {a.AnomalyScore:F2})",
                    Score = a.AnomalyScore,
                    Timestamp = a.Timestamp,
                    Patterns = a.DetectedPatterns
                });
        }

        public BaselineStatus GetBaselineStatus()
        {
            return new BaselineStatus
            {
                State = _engine.BaselineState.ToString(),
                IsTrained = _engine.BaselineState != BaselineState.NotTrained,
                IsLearning = _engine.BaselineState == BaselineState.Learning
            };
        }
    }

    public class MLStatus
    {
        public int AnomalyScore { get; set; } // 0-100
        public string BaselineState { get; set; } = string.Empty; // Stable | Adapting | Drift | NotTrained
        public string RiskTrend { get; set; } = string.Empty; // Up | Down | Flat
        public DateTime LastAnalysis { get; set; }
    }

    public class Anomaly
    {
        public string Source { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Patterns { get; set; } = new();
    }

    public class BaselineStatus
    {
        public string State { get; set; } = string.Empty;
        public bool IsTrained { get; set; }
        public bool IsLearning { get; set; }
    }
}

