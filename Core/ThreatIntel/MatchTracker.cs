using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowsGuardianWatchdog.Core.ThreatIntel
{
    /// <summary>
    /// Match Tracker - Tracks IOC matches over time
    /// </summary>
    public class MatchTracker
    {
        private readonly string _matchesPath;
        private readonly List<ThreatMatch> _matches;

        public MatchTracker()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var intelDir = Path.Combine(appData, "WindowsGuardianWatchdog", "threat_intel");
            Directory.CreateDirectory(intelDir);
            _matchesPath = Path.Combine(intelDir, "matches.json");
            _matches = new List<ThreatMatch>();
            
            LoadMatches();
        }

        /// <summary>
        /// Record a threat match
        /// </summary>
        public void RecordMatch(ThreatMatchResult result, string context)
        {
            if (!result.IsMatch || result.IOC == null)
                return;

            var match = new ThreatMatch
            {
                Id = Guid.NewGuid().ToString(),
                IOC = result.IOC,
                MatchedAt = result.MatchedAt,
                Context = context,
                Severity = result.Severity,
                Description = result.Description
            };

            _matches.Add(match);
            SaveMatches();
        }

        /// <summary>
        /// Get all matches
        /// </summary>
        public List<ThreatMatch> GetMatches()
        {
            return _matches.OrderByDescending(m => m.MatchedAt).ToList();
        }

        /// <summary>
        /// Get matches by severity
        /// </summary>
        public List<ThreatMatch> GetMatchesBySeverity(ThreatSeverity severity)
        {
            return _matches.Where(m => m.Severity == severity)
                          .OrderByDescending(m => m.MatchedAt)
                          .ToList();
        }

        /// <summary>
        /// Get match statistics
        /// </summary>
        public MatchStatistics GetStatistics()
        {
            return new MatchStatistics
            {
                TotalMatches = _matches.Count,
                CriticalMatches = _matches.Count(m => m.Severity == ThreatSeverity.Critical),
                HighMatches = _matches.Count(m => m.Severity == ThreatSeverity.High),
                MediumMatches = _matches.Count(m => m.Severity == ThreatSeverity.Medium),
                LowMatches = _matches.Count(m => m.Severity == ThreatSeverity.Low),
                LastMatch = _matches.OrderByDescending(m => m.MatchedAt).FirstOrDefault()
            };
        }

        private void LoadMatches()
        {
            try
            {
                if (File.Exists(_matchesPath))
                {
                    var json = File.ReadAllText(_matchesPath);
                    var loaded = JsonSerializer.Deserialize<List<ThreatMatch>>(json);
                    if (loaded != null)
                    {
                        _matches.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // Start fresh if matches file is corrupted
            }
        }

        private void SaveMatches()
        {
            try
            {
                var json = JsonSerializer.Serialize(_matches, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_matchesPath, json);
            }
            catch
            {
                // Handle save errors
            }
        }
    }

    public class ThreatMatch
    {
        public string Id { get; set; } = string.Empty;
        public IOC IOC { get; set; } = new();
        public DateTime MatchedAt { get; set; }
        public string Context { get; set; } = string.Empty;
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class MatchStatistics
    {
        public int TotalMatches { get; set; }
        public int CriticalMatches { get; set; }
        public int HighMatches { get; set; }
        public int MediumMatches { get; set; }
        public int LowMatches { get; set; }
        public ThreatMatch? LastMatch { get; set; }
    }
}

