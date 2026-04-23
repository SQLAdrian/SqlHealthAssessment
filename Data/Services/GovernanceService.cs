/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLTriage.Data.Models;

namespace SQLTriage.Data.Services
{
    /// <summary>
    /// Computes governance scores from check results with configurable weights,
    /// per-finding caps, and per-category ceilings. Supports both indicative
    /// (quick-check) and full (vulnerability-assessment) scoring modes.
    /// </summary>
    public interface IGovernanceService
    {
        /// <summary>
        /// Compute a quick indicative score from a subset of checks (≤60s run).
        /// </summary>
        Task<GovernanceScore> ComputeIndicativeAsync(
            IEnumerable<CheckResult> results,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Compute a full governance score from a complete vulnerability assessment.
        /// </summary>
        Task<GovernanceScore> ComputeFullAsync(
            IEnumerable<CheckResult> results,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Governance scoring implementation with three-level clamping:
    /// 1. Per-finding score capped at <see cref="GovernanceWeights.Caps.PerFinding"/>.
    /// 2. Per-category sum capped at <see cref="GovernanceWeights.Caps.PerCategory"/> × weight.
    /// 3. Overall sum capped at <see cref="GovernanceWeights.Caps.Overall"/>.
    /// </summary>
    public sealed class GovernanceService : IGovernanceService
    {
        private readonly ILogger<GovernanceService> _logger;
        private readonly IOptionsMonitor<GovernanceWeights> _weightsMonitor;

        public GovernanceService(
            ILogger<GovernanceService> logger,
            IOptionsMonitor<GovernanceWeights> weightsMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _weightsMonitor = weightsMonitor ?? throw new ArgumentNullException(nameof(weightsMonitor));
        }

        public Task<GovernanceScore> ComputeIndicativeAsync(
            IEnumerable<CheckResult> results,
            CancellationToken cancellationToken = default)
        {
            var score = Compute(results, isIndicative: true);
            return Task.FromResult(score);
        }

        public Task<GovernanceScore> ComputeFullAsync(
            IEnumerable<CheckResult> results,
            CancellationToken cancellationToken = default)
        {
            var score = Compute(results, isIndicative: false);
            return Task.FromResult(score);
        }

        private GovernanceScore Compute(IEnumerable<CheckResult> results, bool isIndicative)
        {
            var weights = _weightsMonitor.CurrentValue;
            var list = results?.ToList() ?? new List<CheckResult>();

            if (list.Count == 0)
            {
                return new GovernanceScore
                {
                    IsIndicative = isIndicative,
                    Overall = 0,
                    Band = ScoreBand.Emerging,
                    Categories = new Dictionary<string, CategoryScore>(StringComparer.OrdinalIgnoreCase)
                };
            }

            // 1. Compute base score per finding
            var findingScores = new List<(string Dimension, double Score)>();
            foreach (var r in list)
            {
                var dim = MapCategory(r.Category, weights.CategoryMapping);
                var baseScore = GetBaseScore(r);
                var capped = Math.Min(baseScore, weights.Caps.PerFinding);
                findingScores.Add((dim, r.Passed ? capped : 0.0));
            }

            // 2. Aggregate per category with cap
            var categoryScores = new Dictionary<string, CategoryScore>(StringComparer.OrdinalIgnoreCase);
            foreach (var dim in weights.Categories.Keys)
            {
                var sum = findingScores.Where(f => f.Dimension.Equals(dim, StringComparison.OrdinalIgnoreCase)).Sum(f => f.Score);
                var weight = weights.Categories[dim];
                var ceiling = weights.Caps.PerCategory * weight;
                var capped = Math.Min(sum, ceiling);

                categoryScores[dim] = new CategoryScore
                {
                    Dimension = dim,
                    Weight = weight,
                    RawScore = sum,
                    CappedScore = capped,
                    Ceiling = ceiling,
                    FindingCount = findingScores.Count(f => f.Dimension.Equals(dim, StringComparison.OrdinalIgnoreCase)),
                    PassedCount = list.Count(r => MapCategory(r.Category, weights.CategoryMapping).Equals(dim, StringComparison.OrdinalIgnoreCase) && r.Passed)
                };
            }

            // 3. Overall score with cap
            var overallRaw = categoryScores.Values.Sum(c => c.CappedScore);
            var overall = Math.Min(overallRaw, weights.Caps.Overall);

            var band = GetBand(overall, weights.Bands);

            _logger.LogInformation(
                "Governance score computed: Overall={Overall:F1}, Band={Band}, IsIndicative={IsIndicative}, Findings={Count}",
                overall, band, isIndicative, list.Count);

            return new GovernanceScore
            {
                IsIndicative = isIndicative,
                Overall = overall,
                Band = band,
                Categories = categoryScores,
                TotalFindings = list.Count,
                PassedFindings = list.Count(r => r.Passed),
                FailedFindings = list.Count(r => !r.Passed)
            };
        }

        private static string MapCategory(string checkCategory, Dictionary<string, string> mapping)
        {
            if (string.IsNullOrWhiteSpace(checkCategory))
                return "Reliability";

            if (mapping.TryGetValue(checkCategory, out var mapped))
                return mapped;

            // Fallback: if the category itself is a governance dimension, use it directly
            var dims = new[] { "Security", "Performance", "Reliability", "Cost", "Compliance" };
            if (dims.Contains(checkCategory, StringComparer.OrdinalIgnoreCase))
                return checkCategory;

            return "Reliability";
        }

        /// <summary>
        /// Derive a base score from the check result severity.
        /// These values are chosen so that 3 critical findings spread across
        /// high-weight categories respect the ≤60 indicative ceiling under default caps.
        /// </summary>
        private static double GetBaseScore(CheckResult result)
        {
            return result.Severity?.ToUpperInvariant() switch
            {
                "CRITICAL" => 20.0,
                "HIGH" => 15.0,
                "MEDIUM" => 10.0,
                "LOW" => 5.0,
                "INFO" => 2.0,
                _ => 10.0
            };
        }

        private static ScoreBand GetBand(double overall, Dictionary<string, int[]> bands)
        {
            foreach (var kv in bands)
            {
                var range = kv.Value;
                if (range.Length >= 2 && overall >= range[0] && overall <= range[1])
                {
                    return Enum.TryParse<ScoreBand>(kv.Key, out var band) ? band : ScoreBand.Emerging;
                }
            }
            return ScoreBand.Emerging;
        }
    }

    /// <summary>
    /// Governance weights loaded from Config/governance-weights.json.
    /// Bound via IOptionsMonitor for runtime reload.
    /// </summary>
    public class GovernanceWeights
    {
        public Dictionary<string, double> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Security"] = 0.25,
            ["Performance"] = 0.20,
            ["Reliability"] = 0.20,
            ["Cost"] = 0.15,
            ["Compliance"] = 0.20
        };

        public GovernanceCaps Caps { get; set; } = new();

        public Dictionary<string, int[]> Bands { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Emerging"] = new[] { 0, 20 },
            ["Bronze"] = new[] { 21, 40 },
            ["Silver"] = new[] { 41, 60 },
            ["Gold"] = new[] { 61, 80 },
            ["Platinum"] = new[] { 81, 100 }
        };

        public Dictionary<string, string> CategoryMapping { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Security"] = "Security",
            ["Authentication"] = "Security",
            ["Authorization"] = "Security",
            ["Encryption"] = "Security",
            ["Auditing"] = "Compliance",
            ["Compliance"] = "Compliance",
            ["Data_Protection"] = "Compliance",
            ["Surface_Area"] = "Security",
            ["Configuration"] = "Reliability",
            ["DefaultRuleset"] = "Reliability",
            ["Backup"] = "Reliability",
            ["Monitoring"] = "Reliability",
            ["Reliability"] = "Reliability",
            ["Performance"] = "Performance",
            ["Memory"] = "Performance",
            ["Network"] = "Performance",
            ["Patching"] = "Compliance",
            ["Cost"] = "Cost",
            ["Custom"] = "Reliability"
        };
    }

    public class GovernanceCaps
    {
        public int PerFinding { get; set; } = 40;
        public int PerCategory { get; set; } = 100;
        public int Overall { get; set; } = 100;
    }

    /// <summary>
    /// Final governance score output.
    /// </summary>
    public class GovernanceScore
    {
        public bool IsIndicative { get; set; }
        public double Overall { get; set; }
        public ScoreBand Band { get; set; }
        public Dictionary<string, CategoryScore> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalFindings { get; set; }
        public int PassedFindings { get; set; }
        public int FailedFindings { get; set; }
    }

    public class CategoryScore
    {
        public string Dimension { get; set; } = string.Empty;
        public double Weight { get; set; }
        public double RawScore { get; set; }
        public double CappedScore { get; set; }
        public double Ceiling { get; set; }
        public int FindingCount { get; set; }
        public int PassedCount { get; set; }
        public double PassRate => FindingCount == 0 ? 0 : (PassedCount * 100.0 / FindingCount);
    }

    public enum ScoreBand
    {
        Emerging = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,
        Platinum = 4
    }
}
