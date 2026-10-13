using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowsGuardianWatchdog.Core.ML
{
    /// <summary>
    /// Baseline Trainer - Trains and persists ML baseline
    /// Matches MacGuardian's baseline learning approach
    /// </summary>
    public class BaselineTrainer
    {
        private readonly string _baselinePath;
        private readonly List<FeatureVector> _trainingData;
        private BaselineModel? _baseline;

        public BaselineTrainer()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var mlDir = Path.Combine(appData, "WindowsGuardianWatchdog", "ml");
            Directory.CreateDirectory(mlDir);
            _baselinePath = Path.Combine(mlDir, "baseline.json");
            _trainingData = new List<FeatureVector>();
            
            LoadBaseline();
        }

        /// <summary>
        /// Train baseline from historical data
        /// </summary>
        public void Train(IEnumerable<FeatureVector> trainingData)
        {
            var data = trainingData.ToList();
            if (data.Count < 10)
            {
                throw new InvalidOperationException("Insufficient training data. Need at least 10 samples.");
            }

            _baseline = new BaselineModel
            {
                TrainedAt = DateTime.Now,
                SampleCount = data.Count,
                FeatureMeans = CalculateMeans(data),
                FeatureStdDevs = CalculateStdDevs(data, CalculateMeans(data)),
                FeatureMins = CalculateMins(data),
                FeatureMaxs = CalculateMaxs(data)
            };

            SaveBaseline();
        }

        /// <summary>
        /// Adapt baseline with new data (online learning)
        /// </summary>
        public void Adapt(FeatureVector newVector)
        {
            if (_baseline == null)
            {
                // Initialize baseline with first sample
                _baseline = new BaselineModel
                {
                    TrainedAt = DateTime.Now,
                    SampleCount = 1,
                    FeatureMeans = newVector.Features,
                    FeatureStdDevs = new double[newVector.Features.Length],
                    FeatureMins = newVector.Features,
                    FeatureMaxs = newVector.Features
                };
            }
            else
            {
                // Online learning: update statistics incrementally
                _baseline.SampleCount++;
                _baseline.FeatureMeans = UpdateMean(_baseline.FeatureMeans, newVector.Features, _baseline.SampleCount);
                _baseline.FeatureStdDevs = UpdateStdDev(_baseline, newVector.Features);
                _baseline.FeatureMins = UpdateMin(_baseline.FeatureMins, newVector.Features);
                _baseline.FeatureMaxs = UpdateMax(_baseline.FeatureMaxs, newVector.Features);
            }

            SaveBaseline();
        }

        public BaselineModel? GetBaseline()
        {
            return _baseline;
        }

        public BaselineState GetState()
        {
            if (_baseline == null)
                return BaselineState.NotTrained;

            if (_baseline.SampleCount < 50)
                return BaselineState.Learning;

            return BaselineState.Stable;
        }

        private double[] CalculateMeans(List<FeatureVector> data)
        {
            var featureCount = data[0].Features.Length;
            var means = new double[featureCount];

            for (int i = 0; i < featureCount; i++)
            {
                means[i] = data.Average(v => v.Features[i]);
            }

            return means;
        }

        private double[] CalculateStdDevs(List<FeatureVector> data, double[] means)
        {
            var featureCount = data[0].Features.Length;
            var stdDevs = new double[featureCount];

            for (int i = 0; i < featureCount; i++)
            {
                var variance = data.Average(v => Math.Pow(v.Features[i] - means[i], 2));
                stdDevs[i] = Math.Sqrt(variance);
            }

            return stdDevs;
        }

        private double[] CalculateMins(List<FeatureVector> data)
        {
            var featureCount = data[0].Features.Length;
            var mins = new double[featureCount];

            for (int i = 0; i < featureCount; i++)
            {
                mins[i] = data.Min(v => v.Features[i]);
            }

            return mins;
        }

        private double[] CalculateMaxs(List<FeatureVector> data)
        {
            var featureCount = data[0].Features.Length;
            var maxs = new double[featureCount];

            for (int i = 0; i < featureCount; i++)
            {
                maxs[i] = data.Max(v => v.Features[i]);
            }

            return maxs;
        }

        private double[] UpdateMean(double[] currentMeans, double[] newFeatures, int newCount)
        {
            var updated = new double[currentMeans.Length];
            for (int i = 0; i < currentMeans.Length; i++)
            {
                // Incremental mean update: new_mean = (old_mean * (n-1) + new_value) / n
                updated[i] = ((currentMeans[i] * (newCount - 1)) + newFeatures[i]) / newCount;
            }
            return updated;
        }

        private double[] UpdateStdDev(BaselineModel baseline, double[] newFeatures)
        {
            // Simplified std dev update - in production would use Welford's algorithm
            return baseline.FeatureStdDevs; // Keep existing for now
        }

        private double[] UpdateMin(double[] currentMins, double[] newFeatures)
        {
            var updated = new double[currentMins.Length];
            for (int i = 0; i < currentMins.Length; i++)
            {
                updated[i] = Math.Min(currentMins[i], newFeatures[i]);
            }
            return updated;
        }

        private double[] UpdateMax(double[] currentMaxs, double[] newFeatures)
        {
            var updated = new double[currentMaxs.Length];
            for (int i = 0; i < currentMaxs.Length; i++)
            {
                updated[i] = Math.Max(currentMaxs[i], newFeatures[i]);
            }
            return updated;
        }

        private void LoadBaseline()
        {
            try
            {
                if (File.Exists(_baselinePath))
                {
                    var json = File.ReadAllText(_baselinePath);
                    _baseline = JsonSerializer.Deserialize<BaselineModel>(json);
                }
            }
            catch
            {
                // Start fresh if baseline is corrupted
            }
        }

        private void SaveBaseline()
        {
            try
            {
                if (_baseline != null)
                {
                    var json = JsonSerializer.Serialize(_baseline, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    File.WriteAllText(_baselinePath, json);
                }
            }
            catch
            {
                // Handle save errors
            }
        }
    }

    public class BaselineModel
    {
        public DateTime TrainedAt { get; set; }
        public int SampleCount { get; set; }
        public double[] FeatureMeans { get; set; } = Array.Empty<double>();
        public double[] FeatureStdDevs { get; set; } = Array.Empty<double>();
        public double[] FeatureMins { get; set; } = Array.Empty<double>();
        public double[] FeatureMaxs { get; set; } = Array.Empty<double>();
    }

    public enum BaselineState
    {
        NotTrained,
        Learning,
        Stable,
        Drift
    }
}

