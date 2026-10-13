using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsGuardianWatchdog.Models;
using WindowsGuardianWatchdog.Services;
using WindowsGuardianWatchdog.ViewModels;

namespace WindowsGuardianWatchdog.ViewModels
{
    public class MLInsightsViewModel : ViewModelBase
    {
        private MLStatusModel _status = new();
        private BaselineModel _baselineStatus = new();
        private bool _isLoading;
        private string _statusText = "Ready";

        public MLInsightsViewModel()
        {
            TopAnomalies = new ObservableCollection<AnomalyModel>();
        }

        public MLStatusModel Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public BaselineModel BaselineStatus
        {
            get => _baselineStatus;
            set => SetProperty(ref _baselineStatus, value);
        }

        public ObservableCollection<AnomalyModel> TopAnomalies { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string AnomalyScoreFormatted => $"{Status.AnomalyScore}/100";

        public string RiskTrendIcon
        {
            get
            {
                return Status.RiskTrend switch
                {
                    "Up" => "↑",
                    "Down" => "↓",
                    _ => "→"
                };
            }
        }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            StatusText = "Loading ML insights...";

            try
            {
                // Load status
                Status = await MLServiceClient.GetStatusAsync();
                OnPropertyChanged(nameof(AnomalyScoreFormatted));
                OnPropertyChanged(nameof(RiskTrendIcon));

                // Load baseline status
                BaselineStatus = await MLServiceClient.GetBaselineStatusAsync();

                // Load top anomalies
                TopAnomalies.Clear();
                var anomalies = await MLServiceClient.GetTopAnomaliesAsync(5);
                foreach (var anomaly in anomalies)
                {
                    TopAnomalies.Add(anomaly);
                }

                StatusText = $"Last updated: {Status.LastAnalysis:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task TriggerAnalysisAsync()
        {
            StatusText = "Running ML analysis...";
            await MLServiceClient.TriggerAnalysisAsync();
            await RefreshAsync();
        }
    }
}

