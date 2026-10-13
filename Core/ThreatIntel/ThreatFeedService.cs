using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace WindowsGuardianWatchdog.Core.ThreatIntel
{
    /// <summary>
    /// Threat Feed Service - Fetches IOCs from threat intelligence feeds
    /// </summary>
    public class ThreatFeedService
    {
        private readonly HttpClient _httpClient;
        private readonly List<string> _feedUrls;

        public ThreatFeedService()
        {
            _httpClient = new HttpClient();
            _feedUrls = new List<string>
            {
                // Add threat feed URLs here
                // Example: "https://feeds.example.com/iocs.json"
            };
        }

        /// <summary>
        /// Fetch IOCs from configured threat feeds
        /// </summary>
        public async Task<List<IOC>> FetchIOCsAsync()
        {
            var iocs = new List<IOC>();

            foreach (var url in _feedUrls)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    var feedIocs = ParseIOCFeed(response);
                    iocs.AddRange(feedIocs);
                }
                catch
                {
                    // Continue if feed fails
                }
            }

            return iocs;
        }

        /// <summary>
        /// Parse IOC feed JSON
        /// </summary>
        private List<IOC> ParseIOCFeed(string json)
        {
            var iocs = new List<IOC>();

            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("iocs", out var iocsElement))
                {
                    foreach (var iocElement in iocsElement.EnumerateArray())
                    {
                        var ioc = new IOC
                        {
                            Type = Enum.Parse<IOCType>(iocElement.GetProperty("type").GetString() ?? "FileHash"),
                            Value = iocElement.GetProperty("value").GetString() ?? "",
                            Description = iocElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            Severity = Enum.Parse<ThreatSeverity>(iocElement.GetProperty("severity").GetString() ?? "Medium"),
                            AddedAt = DateTime.Now,
                            Source = iocElement.TryGetProperty("source", out var source) ? source.GetString() : null
                        };
                        iocs.Add(ioc);
                    }
                }
            }
            catch
            {
                // Handle parse errors
            }

            return iocs;
        }

        /// <summary>
        /// Add custom threat feed URL
        /// </summary>
        public void AddFeedUrl(string url)
        {
            if (!_feedUrls.Contains(url))
            {
                _feedUrls.Add(url);
            }
        }
    }
}

