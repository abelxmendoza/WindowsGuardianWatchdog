using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WindowsGuardianWatchdog.Core.ML;

namespace WindowsGuardianWatchdog.Core.IPC
{
    /// <summary>
    /// ML Command Handler - Handles ML-related IPC commands
    /// </summary>
    public class MLCommandHandler
    {
        private readonly MLStatusProvider _provider;

        public MLCommandHandler(MLStatusProvider provider)
        {
            _provider = provider;
        }

        public object Handle(string command, object? payload = null)
        {
            return command switch
            {
                "ml_status" => _provider.GetStatus(),
                "ml_top_anomalies" => HandleTopAnomalies(payload),
                "ml_baseline_status" => _provider.GetBaselineStatus(),
                _ => throw new InvalidOperationException($"Unknown ML command: {command}")
            };
        }

        private object HandleTopAnomalies(object? payload)
        {
            int limit = 5;
            
            if (payload != null)
            {
                var json = payload.ToString();
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("limit", out var limitElement))
                        {
                            limit = limitElement.GetInt32();
                        }
                    }
                    catch
                    {
                        // Use default limit
                    }
                }
            }

            return _provider.GetTopAnomalies(limit).ToList();
        }
    }
}

