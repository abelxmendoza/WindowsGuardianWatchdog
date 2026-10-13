using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Core.ML;

namespace WindowsGuardianWatchdog.Core.ML
{
    /// <summary>
    /// ML Security Engine for behavioral anomaly detection
    /// Matches MacGuardian's AI/ML capabilities using ML.NET
    /// </summary>
    public class MLSecurityEngine
    {
        private readonly AnomalyDetector _anomalyDetector;
        private readonly PatternRecognizer _patternRecognizer;
        private readonly BaselineLearner _baselineLearner;
        private readonly FeatureExtractor _featureExtractor;
        private readonly BaselineTrainer _baselineTrainer;
        private readonly AnomalyScorer _anomalyScorer;
        private readonly List<AnomalyAnalysis> _recentAnomalies;
        private bool _isLearning = false;
        
        public double CurrentAnomalyScore { get; private set; }
        public BaselineState BaselineState => _baselineTrainer.GetState();
        public string RiskTrend { get; private set; } = "Flat";

        public event EventHandler<AnomalyDetected>? AnomalyDetected;
        public event EventHandler<ThreatPrediction>? ThreatPredicted;

        public MLSecurityEngine()
        {
            _anomalyDetector = new AnomalyDetector();
            _patternRecognizer = new PatternRecognizer();
            _baselineLearner = new BaselineLearner();
            _featureExtractor = new FeatureExtractor();
            _baselineTrainer = new BaselineTrainer();
            _anomalyScorer = new AnomalyScorer();
            _recentAnomalies = new List<AnomalyAnalysis>();
        }

        /// <summary>
        /// Analyze system behavior for anomalies using Isolation Forest approach
        /// </summary>
        public async Task<AnomalyAnalysis> AnalyzeBehaviorAsync(SystemMetrics? metrics = null)
        {
            return await Task.Run(() =>
            {
                // Collect current metrics if not provided
                metrics ??= _featureExtractor.CollectCurrentMetrics();

                // Extract features
                var featureVector = _featureExtractor.Extract(metrics);
                var baseline = _baselineTrainer.GetBaseline();

                // Score anomaly
                var scoreResult = _anomalyScorer.Score(featureVector, baseline);
                CurrentAnomalyScore = scoreResult.CombinedScore;

                // Pattern recognition
                var patterns = _patternRecognizer.RecognizePatterns(featureVector.Features);

                var analysis = new AnomalyAnalysis
                {
                    Timestamp = DateTime.Now,
                    AnomalyScore = scoreResult.IsolationScore,
                    ZScore = scoreResult.ZScore,
                    IsAnomalous = scoreResult.IsAnomalous,
                    DetectedPatterns = patterns,
                    Features = featureVector.Features
                };

                // Update risk trend
                UpdateRiskTrend(analysis);

                // Store recent anomalies
                _recentAnomalies.Add(analysis);
                if (_recentAnomalies.Count > 100)
                {
                    _recentAnomalies.RemoveAt(0);
                }

                // Adapt baseline (online learning)
                if (_baselineTrainer.GetState() != BaselineState.NotTrained)
                {
                    _baselineTrainer.Adapt(featureVector);
                }

                if (analysis.IsAnomalous)
                {
                    OnAnomalyDetected(new AnomalyDetected
                    {
                        Analysis = analysis,
                        Severity = DetermineSeverity(scoreResult.IsolationScore, scoreResult.ZScore),
                        Recommendation = GenerateRecommendation(patterns)
                    });
                }

                return analysis;
            });
        }

        /// <summary>
        /// Train baseline from historical data
        /// </summary>
        public async Task TrainBaselineAsync(IEnumerable<SystemMetrics> historicalData)
        {
            await Task.Run(() =>
            {
                var featureVectors = historicalData.Select(m => _featureExtractor.Extract(m));
                _baselineTrainer.Train(featureVectors);
            });
        }

        /// <summary>
        /// Get recent anomalies
        /// </summary>
        public IEnumerable<AnomalyAnalysis> GetRecentAnomalies(int limit = 10)
        {
            return _recentAnomalies
                .OrderByDescending(a => a.AnomalyScore)
                .Take(limit);
        }

        private void UpdateRiskTrend(AnomalyAnalysis analysis)
        {
            if (_recentAnomalies.Count < 3)
            {
                RiskTrend = "Flat";
                return;
            }

            var recent = _recentAnomalies.TakeLast(3).ToList();
            var avgRecent = recent.Average(a => a.AnomalyScore);
            var avgOlder = _recentAnomalies.Skip(_recentAnomalies.Count - 6).Take(3).Average(a => a.AnomalyScore);

            if (avgRecent > avgOlder * 1.1)
                RiskTrend = "Up";
            else if (avgRecent < avgOlder * 0.9)
                RiskTrend = "Down";
            else
                RiskTrend = "Flat";
        }

        /// <summary>
        /// Learn baseline from historical data (online learning)
        /// </summary>
        public async Task LearnBaselineAsync(IEnumerable<SystemMetrics> historicalData)
        {
            await Task.Run(() =>
            {
                _isLearning = true;
                _baselineLearner.UpdateBaseline(historicalData);
                _isLearning = false;
            });
        }

        /// <summary>
        /// Predict potential threats based on current state
        /// </summary>
        public async Task<ThreatPrediction> PredictThreatAsync(SystemMetrics currentMetrics)
        {
            return await Task.Run(() =>
            {
                var features = _featureExtractor.Extract(currentMetrics);
                var prediction = _patternRecognizer.PredictThreat(features.Features);

                OnThreatPredicted(prediction);
                return prediction;
            });
        }


        private ThreatSeverity DetermineSeverity(double anomalyScore, double zScore)
        {
            if (anomalyScore > 0.9 || Math.Abs(zScore) > 3.5)
                return ThreatSeverity.Critical;
            if (anomalyScore > 0.8 || Math.Abs(zScore) > 2.5)
                return ThreatSeverity.High;
            if (anomalyScore > 0.7 || Math.Abs(zScore) > 2.0)
                return ThreatSeverity.Medium;
            return ThreatSeverity.Low;
        }

        private string GenerateRecommendation(List<string> patterns)
        {
            if (patterns.Contains("sudden_process_spike"))
                return "Investigate sudden process count increase - possible malware execution";
            if (patterns.Contains("network_anomaly"))
                return "Review network connections - potential data exfiltration";
            if (patterns.Contains("file_system_anomaly"))
                return "Check file system changes - possible unauthorized modifications";
            
            return "Review system metrics for unusual patterns";
        }

        protected virtual void OnAnomalyDetected(AnomalyDetected anomaly)
        {
            AnomalyDetected?.Invoke(this, anomaly);
        }

        protected virtual void OnThreatPredicted(ThreatPrediction prediction)
        {
            ThreatPredicted?.Invoke(this, prediction);
        }
    }

    // Supporting classes
    public class AnomalyDetector
    {
        private readonly List<double[]> _baseline = new();

        public double DetectAnomaly(double[] features)
        {
            // Simplified Isolation Forest implementation
            // In production, use ML.NET's IsolationForest or Python.NET for scikit-learn
            
            if (_baseline.Count == 0)
                return 0.5; // Neutral if no baseline

            // Calculate distance from baseline
            var distances = _baseline.Select(b => CalculateDistance(features, b)).ToList();
            var avgDistance = distances.Average();
            var maxDistance = distances.Max();

            // Normalize to 0-1 anomaly score
            var anomalyScore = Math.Min(1.0, avgDistance / maxDistance);
            return anomalyScore;
        }

        private double CalculateDistance(double[] a, double[] b)
        {
            if (a.Length != b.Length) return double.MaxValue;
            return Math.Sqrt(a.Zip(b, (x, y) => Math.Pow(x - y, 2)).Sum());
        }
    }

    public class PatternRecognizer
    {
        public List<string> RecognizePatterns(double[] features)
        {
            var patterns = new List<string>();

            // Detect common attack patterns
            if (features[0] > 200) // Process count spike
                patterns.Add("sudden_process_spike");
            
            if (features[1] > 100) // Network connections spike
                patterns.Add("network_anomaly");
            
            if (features[6] > 50) // Registry changes spike
                patterns.Add("registry_anomaly");
            
            if (features[5] > 100) // File system changes
                patterns.Add("file_system_anomaly");

            return patterns;
        }

        public ThreatPrediction PredictThreat(double[] features)
        {
            var patterns = RecognizePatterns(features);
            var riskScore = CalculateRiskScore(features, patterns);

            return new ThreatPrediction
            {
                Timestamp = DateTime.Now,
                RiskScore = riskScore,
                PredictedThreats = patterns,
                Confidence = CalculateConfidence(patterns),
                TimeToThreat = EstimateTimeToThreat(riskScore)
            };
        }

        private double CalculateRiskScore(double[] features, List<string> patterns)
        {
            var baseScore = features.Average() / 100.0; // Normalize
            var patternMultiplier = patterns.Count * 0.2;
            return Math.Min(1.0, baseScore + patternMultiplier);
        }

        private double CalculateConfidence(List<string> patterns)
        {
            return Math.Min(1.0, patterns.Count * 0.3);
        }

        private TimeSpan EstimateTimeToThreat(double riskScore)
        {
            // Higher risk = shorter time to potential threat
            var hours = (1.0 - riskScore) * 24;
            return TimeSpan.FromHours(Math.Max(1, hours));
        }
    }

    public class BaselineLearner
    {
        private readonly List<double[]> _historicalFeatures = new();
        private const int MaxHistorySize = 1000;

        public void UpdateBaseline(IEnumerable<SystemMetrics> data)
        {
            foreach (var metrics in data)
            {
                var features = new double[]
                {
                    metrics.ProcessCount,
                    metrics.NetworkConnections,
                    metrics.CpuUsage,
                    metrics.MemoryUsage,
                    metrics.DiskActivity,
                    metrics.FileSystemChanges,
                    metrics.RegistryChanges,
                    metrics.ActiveUsers
                };

                _historicalFeatures.Add(features);

                // Keep only recent history
                if (_historicalFeatures.Count > MaxHistorySize)
                {
                    _historicalFeatures.RemoveAt(0);
                }
            }
        }
    }

    // Data models
    public class SystemMetrics
    {
        public DateTime Timestamp { get; set; }
        public int ProcessCount { get; set; }
        public int NetworkConnections { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskActivity { get; set; }
        public int FileSystemChanges { get; set; }
        public int RegistryChanges { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class AnomalyAnalysis
    {
        public DateTime Timestamp { get; set; }
        public double AnomalyScore { get; set; }
        public double ZScore { get; set; }
        public bool IsAnomalous { get; set; }
        public List<string> DetectedPatterns { get; set; } = new();
        public double[] Features { get; set; } = Array.Empty<double>();
    }

    public class AnomalyDetected
    {
        public AnomalyAnalysis Analysis { get; set; } = new();
        public ThreatSeverity Severity { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ThreatPrediction
    {
        public DateTime Timestamp { get; set; }
        public double RiskScore { get; set; }
        public List<string> PredictedThreats { get; set; } = new();
        public double Confidence { get; set; }
        public TimeSpan TimeToThreat { get; set; }
    }

    public enum ThreatSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}

