using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WindowsGuardianWatchdog.Core.ThreatIntel
{
    /// <summary>
    /// IOC (Indicator of Compromise) Checker
    /// Matches MacGuardian's IOC checking capabilities
    /// </summary>
    public class IOCChecker
    {
        private readonly List<IOC> _iocDatabase;
        private readonly ThreatFeedService _threatFeed;

        public IOCChecker()
        {
            _iocDatabase = new List<IOC>();
            _threatFeed = new ThreatFeedService();
        }

        /// <summary>
        /// Check if a file hash matches known IOCs
        /// </summary>
        public ThreatMatchResult CheckFile(string filePath)
        {
            try
            {
                var hash = CalculateFileHash(filePath);
                return CheckHash(hash);
            }
            catch
            {
                return new ThreatMatchResult { IsMatch = false };
            }
        }

        /// <summary>
        /// Check if a hash matches known IOCs
        /// </summary>
        public ThreatMatchResult CheckHash(byte[] hash)
        {
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return CheckHash(hashString);
        }

        /// <summary>
        /// Check if a hash string matches known IOCs
        /// </summary>
        public ThreatMatchResult CheckHash(string hashString)
        {
            var match = _iocDatabase.FirstOrDefault(ioc => 
                ioc.Type == IOCType.FileHash && 
                ioc.Value.Equals(hashString, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return new ThreatMatchResult
                {
                    IsMatch = true,
                    IOC = match,
                    Severity = match.Severity,
                    Description = $"File hash matches known IOC: {match.Description}"
                };
            }

            return new ThreatMatchResult { IsMatch = false };
        }

        /// <summary>
        /// Check if an IP address matches known IOCs
        /// </summary>
        public ThreatMatchResult CheckIP(string ipAddress)
        {
            var match = _iocDatabase.FirstOrDefault(ioc => 
                ioc.Type == IOCType.IPAddress && 
                ioc.Value.Equals(ipAddress, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return new ThreatMatchResult
                {
                    IsMatch = true,
                    IOC = match,
                    Severity = match.Severity,
                    Description = $"IP address matches known IOC: {match.Description}"
                };
            }

            return new ThreatMatchResult { IsMatch = false };
        }

        /// <summary>
        /// Check if a domain matches known IOCs
        /// </summary>
        public ThreatMatchResult CheckDomain(string domain)
        {
            var match = _iocDatabase.FirstOrDefault(ioc => 
                ioc.Type == IOCType.Domain && 
                (ioc.Value.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                 domain.EndsWith("." + ioc.Value, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                return new ThreatMatchResult
                {
                    IsMatch = true,
                    IOC = match,
                    Severity = match.Severity,
                    Description = $"Domain matches known IOC: {match.Description}"
                };
            }

            return new ThreatMatchResult { IsMatch = false };
        }

        /// <summary>
        /// Add IOC to database
        /// </summary>
        public void AddIOC(IOC ioc)
        {
            if (!_iocDatabase.Any(i => i.Type == ioc.Type && i.Value == ioc.Value))
            {
                _iocDatabase.Add(ioc);
            }
        }

        /// <summary>
        /// Load IOCs from threat feed
        /// </summary>
        public async Task LoadFromThreatFeedAsync()
        {
            var iocs = await _threatFeed.FetchIOCsAsync();
            foreach (var ioc in iocs)
            {
                AddIOC(ioc);
            }
        }

        private string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = System.IO.File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public class IOC
    {
        public IOCType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ThreatSeverity Severity { get; set; }
        public DateTime AddedAt { get; set; }
        public string? Source { get; set; }
    }

    public enum IOCType
    {
        FileHash,
        IPAddress,
        Domain,
        URL,
        Email
    }

    public class ThreatMatchResult
    {
        public bool IsMatch { get; set; }
        public IOC? IOC { get; set; }
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime MatchedAt { get; set; } = DateTime.Now;
    }

    public enum ThreatSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}

