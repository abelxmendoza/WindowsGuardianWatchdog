using System;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.ML
{
    /// <summary>
    /// Anomaly Scorer - Scores feature vectors for anomalies
    /// Uses Isolation Forest approach and Z-score analysis
    /// </summary>
    public class AnomalyScorer
    {
        /// <summary>
        /// Score anomaly using Isolation Forest approach
        /// </summary>
        public double ScoreIsolationForest(FeatureVector vector, BaselineModel? baseline)
        {
            if (baseline == null || baseline.SampleCount < 10)
                return 0.5; // Neutral if no baseline

            // Simplified Isolation Forest scoring
            // In production, use ML.NET IsolationForest or Python.NET scikit-learn
            
            var anomalyScore = 0.0;
            var featureCount = vector.Features.Length;

            for (int i = 0; i < featureCount; i++)
            {
                var feature = vector.Features[i];
                var mean = baseline.FeatureMeans[i];
                var stdDev = baseline.FeatureStdDevs[i];

                if (stdDev == 0)
                    continue;

                // Calculate distance from mean (normalized)
                var distance = Math.Abs(feature - mean) / stdDev;
                
                // Convert to anomaly contribution (0-1)
                var contribution = Math.Min(1.0, distance / 3.0); // 3 sigma = high anomaly
                anomalyScore += contribution;
            }

            // Average across all features
            return anomalyScore / featureCount;
        }

        /// <summary>
        /// Calculate Z-score for feature vector
        /// </summary>
        public double CalculateZScore(FeatureVector vector, BaselineModel? baseline)
        {
            if (baseline == null || baseline.SampleCount < 10)
                return 0.0;

            var zScores = new double[vector.Features.Length];
            
            for (int i = 0; i < vector.Features.Length; i++)
            {
                var feature = vector.Features[i];
                var mean = baseline.FeatureMeans[i];
                var stdDev = baseline.FeatureStdDevs[i];

                if (stdDev == 0)
                {
                    zScores[i] = 0.0;
                }
                else
                {
                    zScores[i] = (feature - mean) / stdDev;
                }
            }

            // Return maximum absolute Z-score (most anomalous feature)
            return zScores.Select(Math.Abs).Max();
        }

        /// <summary>
        /// Combined anomaly score (Isolation Forest + Z-score)
        /// </summary>
        public AnomalyScoreResult Score(FeatureVector vector, BaselineModel? baseline)
        {
            var isolationScore = ScoreIsolationForest(vector, baseline);
            var zScore = CalculateZScore(vector, baseline);

            // Combine scores (weighted average)
            var combinedScore = (isolationScore * 0.7) + (Math.Min(1.0, zScore / 3.0) * 0.3);

            return new AnomalyScoreResult
            {
                IsolationScore = isolationScore,
                ZScore = zScore,
                CombinedScore = combinedScore,
                IsAnomalous = combinedScore > 0.7 || Math.Abs(zScore) > 2.5
            };
        }
    }

    public class AnomalyScoreResult
    {
        public double IsolationScore { get; set; }
        public double ZScore { get; set; }
        public double CombinedScore { get; set; }
        public bool IsAnomalous { get; set; }
    }
}

