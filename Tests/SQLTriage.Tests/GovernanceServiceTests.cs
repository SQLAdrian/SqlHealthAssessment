/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SQLTriage.Data.Models;
using SQLTriage.Data.Services;
using Xunit;

namespace SQLTriage.Tests
{
    public class GovernanceServiceTests
    {
        private static GovernanceService CreateService(GovernanceWeights? weights = null)
        {
            var w = weights ?? new GovernanceWeights();
            var monitor = new TestOptionsMonitor<GovernanceWeights>(w);
            return new GovernanceService(NullLogger<GovernanceService>.Instance, monitor);
        }

        [Fact]
        public async Task EmptyResults_ReturnsZeroEmerging()
        {
            var svc = CreateService();
            var score = await svc.ComputeIndicativeAsync(Array.Empty<CheckResult>());

            Assert.Equal(0, score.Overall);
            Assert.Equal(ScoreBand.Emerging, score.Band);
            Assert.True(score.IsIndicative);
            Assert.Empty(score.Categories);
        }

        [Fact]
        public async Task AllPassed_ReturnsPlatinum()
        {
            var svc = CreateService();
            var categories = new[] { "Security", "Security", "Security", "Performance", "Performance", "Reliability", "Reliability", "Cost", "Cost", "Compliance", "Compliance" };
            var results = categories.Select((cat, i) => new CheckResult
            {
                CheckId = $"CHK-{i:D3}",
                Category = cat,
                Severity = "MEDIUM",
                Passed = true
            });

            var score = await svc.ComputeFullAsync(results);

            // Security 3×10=30→cap25, Perf 2×10=20→20, Rel 2×10=20→20, Cost 2×10=20→cap15, Comp 2×10=20→20 = 100
            Assert.Equal(100, score.Overall);
            Assert.Equal(ScoreBand.Platinum, score.Band);
            Assert.Equal(11, score.PassedFindings);
            Assert.Equal(0, score.FailedFindings);
        }

        [Fact]
        public async Task ThreeCriticalFindings_Spread_DoesNotExceedSixty()
        {
            var weights = new GovernanceWeights
            {
                Categories = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Security"] = 0.25,
                    ["Performance"] = 0.20,
                    ["Reliability"] = 0.20,
                    ["Cost"] = 0.15,
                    ["Compliance"] = 0.20
                },
                Caps = new GovernanceCaps { PerFinding = 40, PerCategory = 100, Overall = 100 }
            };

            var svc = CreateService(weights);
            var results = new[]
            {
                new CheckResult { CheckId = "C1", Category = "Security", Severity = "CRITICAL", Passed = true },
                new CheckResult { CheckId = "C2", Category = "Performance", Severity = "CRITICAL", Passed = true },
                new CheckResult { CheckId = "C3", Category = "Reliability", Severity = "CRITICAL", Passed = true }
            };

            var score = await svc.ComputeIndicativeAsync(results);

            // 3 critical findings (base 20 each) spread across 3 categories:
            // Security cap = 100*0.25 = 25 -> min(20,25) = 20
            // Performance cap = 100*0.20 = 20 -> min(20,20) = 20
            // Reliability cap = 100*0.20 = 20 -> min(20,20) = 20
            // Overall = 60
            Assert.True(score.Overall <= 60, $"Expected ≤60 but got {score.Overall}");
            Assert.Equal(60, score.Overall);
        }

        [Fact]
        public async Task PerFindingCap_IsRespected()
        {
            var weights = new GovernanceWeights
            {
                Caps = new GovernanceCaps { PerFinding = 10, PerCategory = 100, Overall = 100 }
            };

            var svc = CreateService(weights);
            var results = new[]
            {
                new CheckResult { CheckId = "C1", Category = "Security", Severity = "CRITICAL", Passed = true }
            };

            var score = await svc.ComputeFullAsync(results);

            // Base score for CRITICAL is 20, but per-finding cap is 10
            var sec = score.Categories["Security"];
            Assert.Equal(10, sec.RawScore);
            Assert.Equal(10, sec.CappedScore); // min(10, 25 ceiling) — capped score is already weighted
        }

        [Fact]
        public async Task CategoryMapping_FallsBackToReliability()
        {
            var svc = CreateService();
            var results = new[]
            {
                new CheckResult { CheckId = "C1", Category = "UnknownCategory", Severity = "LOW", Passed = true }
            };

            var score = await svc.ComputeIndicativeAsync(results);

            Assert.True(score.Categories.ContainsKey("Reliability"));
            Assert.False(score.Categories.ContainsKey("UnknownCategory"));
        }

        [Fact]
        public async Task IsIndicative_FlagSetCorrectly()
        {
            var svc = CreateService();
            var results = new[] { new CheckResult { CheckId = "C1", Category = "Security", Passed = true } };

            var indicative = await svc.ComputeIndicativeAsync(results);
            var full = await svc.ComputeFullAsync(results);

            Assert.True(indicative.IsIndicative);
            Assert.False(full.IsIndicative);
        }

        [Fact]
        public async Task FailedFindings_ContributeZero()
        {
            var svc = CreateService();
            var results = new[]
            {
                new CheckResult { CheckId = "C1", Category = "Security", Severity = "CRITICAL", Passed = false },
                new CheckResult { CheckId = "C2", Category = "Security", Severity = "CRITICAL", Passed = true }
            };

            var score = await svc.ComputeFullAsync(results);

            Assert.Equal(1, score.PassedFindings);
            Assert.Equal(1, score.FailedFindings);
            var sec = score.Categories["Security"];
            Assert.Equal(1, sec.PassedCount);
            Assert.Equal(2, sec.FindingCount);
        }

        private class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
        {
            private readonly T _value;
            public TestOptionsMonitor(T value) => _value = value;
            public T CurrentValue => _value;
            public T Get(string? name) => _value;
            public IDisposable? OnChange(Action<T, string?> listener) => null;
        }
    }
}
