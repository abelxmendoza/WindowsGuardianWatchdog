using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Models;
using WindowsGuardianWatchdog.Core.ML;
using System.Linq;

namespace WindowsGuardianWatchdog.Services
{
    /// <summary>
    /// ML Service Client - Client for ML service communication
    /// </summary>
    public static class MLServiceClient
    {
        private static MLStatusProvider? _provider;
        private static MLSecurityEngine? _engine;

        public static void Initialize(MLSecurityEngine engine)
        {
            _engine = engine;
            _provider = new MLStatusProvider(engine);
        }

        public static async Task<MLStatusModel> GetStatusAsync()
        {
            if (_provider == null)
                return new MLStatusModel();

            return await Task.Run(() =>
            {
                var status = _provider.GetStatus();
                return new MLStatusModel
                {
                    AnomalyScore = status.AnomalyScore,
                    BaselineState = status.BaselineState,
                    RiskTrend = status.RiskTrend,
                    LastAnalysis = status.LastAnalysis
                };
            });
        }

        public static async Task<IEnumerable<AnomalyModel>> GetTopAnomaliesAsync(int limit = 5)
        {
            if (_provider == null)
                return Array.Empty<AnomalyModel>();

            return await Task.Run(() =>
            {
                var anomalies = _provider.GetTopAnomalies(limit);
                return anomalies.Select(a => new AnomalyModel
                {
                    Source = a.Source,
                    Description = a.Description,
                    Score = a.Score,
                    Timestamp = a.Timestamp,
                    Patterns = a.Patterns
                });
            });
        }

        public static async Task<WindowsGuardianWatchdog.Models.BaselineModel> GetBaselineStatusAsync()
        {
            if (_provider == null)
                return new WindowsGuardianWatchdog.Models.BaselineModel();

            return await Task.Run(() =>
            {
                var status = _provider.GetBaselineStatus();
                return new WindowsGuardianWatchdog.Models.BaselineModel
                {
                    State = status.State,
                    IsTrained = status.IsTrained,
                    IsLearning = status.IsLearning
                };
            });
        }

        public static async Task TriggerAnalysisAsync()
        {
            if (_engine == null)
                return;

            await _engine.AnalyzeBehaviorAsync();
        }
    }
}

