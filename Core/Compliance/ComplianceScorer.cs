using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsGuardianWatchdog.Core.Compliance
{
    /// <summary>
    /// Compliance Scorer - Calculates compliance scores
    /// </summary>
    public class ComplianceScorer
    {
        /// <summary>
        /// Calculate overall hardening score (0-100)
        /// </summary>
        public int CalculateScore(List<HardeningCheck> checks)
        {
            if (checks.Count == 0)
                return 0;

            int totalPoints = 0;
            int maxPoints = 0;

            foreach (var check in checks)
            {
                var points = GetPointsForCheck(check);
                maxPoints += points;
                
                if (check.Status == CheckStatus.Pass)
                {
                    totalPoints += points;
                }
                else if (check.Status == CheckStatus.Warning)
                {
                    totalPoints += points / 2; // Half points for warnings
                }
            }

            if (maxPoints == 0)
                return 0;

            return (int)((double)totalPoints / maxPoints * 100);
        }

        /// <summary>
        /// Determine overall compliance status
        /// </summary>
        public ComplianceStatus DetermineStatus(int score)
        {
            return score switch
            {
                >= 90 => ComplianceStatus.Excellent,
                >= 75 => ComplianceStatus.Good,
                >= 60 => ComplianceStatus.Fair,
                >= 40 => ComplianceStatus.Poor,
                _ => ComplianceStatus.Critical
            };
        }

        /// <summary>
        /// Calculate compliance score for specific profile (HIPAA, GDPR, PCI-DSS)
        /// </summary>
        public ComplianceProfileResult CalculateProfileScore(List<HardeningCheck> checks, ComplianceProfile profile)
        {
            var relevantChecks = checks.Where(c => profile.RequiredChecks.Contains(c.Name)).ToList();
            var score = CalculateScore(relevantChecks);
            var status = DetermineStatus(score);

            return new ComplianceProfileResult
            {
                Profile = profile.Name,
                Score = score,
                Status = status,
                Checks = relevantChecks,
                PassedChecks = relevantChecks.Count(c => c.Status == CheckStatus.Pass),
                FailedChecks = relevantChecks.Count(c => c.Status == CheckStatus.Fail),
                TotalChecks = relevantChecks.Count
            };
        }

        private int GetPointsForCheck(HardeningCheck check)
        {
            return check.Severity switch
            {
                FindingSeverity.Critical => 10,
                FindingSeverity.High => 7,
                FindingSeverity.Medium => 5,
                FindingSeverity.Low => 3,
                _ => 1
            };
        }
    }

    public class ComplianceProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> RequiredChecks { get; set; } = new();
    }

    public class ComplianceProfileResult
    {
        public string Profile { get; set; } = string.Empty;
        public int Score { get; set; }
        public ComplianceStatus Status { get; set; }
        public List<HardeningCheck> Checks { get; set; } = new();
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public int TotalChecks { get; set; }
    }
}

