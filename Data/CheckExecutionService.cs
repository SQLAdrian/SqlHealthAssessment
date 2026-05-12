/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SQLTriage.Data.Models;
using SQLTriage.Data.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SQLTriage.Data
{
    /// <summary>
    /// Executes enabled SQL checks from the check repository against configured
    /// SQL Server instances.  Results are tracked per-instance and per-check
    /// with a configurable history depth.
    ///
    /// Key design choices (inspired by PerformanceMonitor):
    ///   - Per-instance query throttling via SemaphoreSlim to avoid overwhelming
    ///     any single server.
    ///   - Thread-safe result storage using ConcurrentDictionary.
    ///   - Fire-and-forget timer or on-demand execution.
    /// </summary>
    public class CheckExecutionService : IDisposable
    {
        private readonly ILogger<CheckExecutionService> _logger;
        private readonly CheckRepositoryService _checkRepo;
        private readonly ServerConnectionManager _connectionManager;
        private readonly IConfiguration _configuration;
        private readonly GovernanceHistoryService? _historyService;
        private readonly QuickCheckResultStore? _resultStore;

        /// <summary>Max concurrent queries per instance (mirrors PerformanceMonitor's SemaphoreSlim(7)).</summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _instanceThrottles = new();
        private const int MaxConcurrentQueriesPerInstance = 7;

        /// <summary>Per-instance, per-check result history (most recent first).</summary>
        private readonly ConcurrentDictionary<string, List<CheckResult>> _resultsByInstance = new();

        /// <summary>Per-instance execution summaries.</summary>
        private readonly ConcurrentDictionary<string, CheckExecutionSummary> _lastSummary = new();

        private readonly int _maxResultsPerInstance;
        private const int CheckCommandTimeoutSeconds = 30;

        private bool _disposed;

        /// <summary>Raised after a full execution run completes for an instance.</summary>
        public event Action<CheckExecutionSummary>? OnExecutionCompleted;

        /// <summary>Raised after each individual check completes (for diagnostic logging).</summary>
        public event Action<CheckResult>? OnCheckCompleted;

        public CheckExecutionService(
            ILogger<CheckExecutionService> logger,
            CheckRepositoryService checkRepo,
            ServerConnectionManager connectionManager,
            IConfiguration configuration,
            GovernanceHistoryService? historyService = null,
            QuickCheckResultStore? resultStore = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _checkRepo = checkRepo ?? throw new ArgumentNullException(nameof(checkRepo));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _historyService = historyService;
            _resultStore = resultStore;
            // 2026-05-12: lowered hot cache from 500→50 per instance to cut RAM pressure
            // on 25-server runs (~3 GB resident observed). SQLite history retains the full
            // record via GovernanceHistoryService; consumers re-hydrate from disk on demand.
            _maxResultsPerInstance = _configuration.GetValue("CheckExecution:MaxResultsPerInstance", 50);
        }

        // ────────────────────── Execution ──────────────────────

        /// <summary>
        /// Runs all enabled checks against a single SQL Server instance.
        /// Queries are throttled to <see cref="MaxConcurrentQueriesPerInstance"/>.
        /// </summary>
        public async Task<CheckExecutionSummary> ExecuteChecksAsync(
            ServerConnection connection, string serverName, CancellationToken ct = default)
        {
            var summary = new CheckExecutionSummary
            {
                InstanceName = serverName,
                StartedAt = DateTime.UtcNow
            };

            var enabledChecks = _checkRepo.GetEnabledChecks();
            summary.TotalChecks = enabledChecks.Count;

            if (enabledChecks.Count == 0)
            {
                summary.CompletedAt = DateTime.UtcNow;
                return summary;
            }

            var throttle = _instanceThrottles.GetOrAdd(serverName,
                _ => new SemaphoreSlim(MaxConcurrentQueriesPerInstance));

            // Use master database for check execution to allow running checks
            // even when SQLWATCH is not installed
            var connectionString = connection.GetConnectionString(serverName, "master");

            var tasks = enabledChecks.Select(check =>
                ExecuteSingleCheckAsync(check, connectionString, serverName, throttle, ct));

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.ErrorMessage != null)
                    summary.Errors++;
                else if (result.Passed)
                    summary.Passed++;
                else
                    summary.Failed++;

                StoreResult(serverName, result);
            }

            summary.CompletedAt = DateTime.UtcNow;
            _lastSummary[serverName] = summary;

            // Persist the full run to disk so consumers can re-hydrate without
            // keeping the entire result set in RAM (worklist item 2026-05-12 #4).
            _resultStore?.WriteRun(serverName, results);

            _logger.LogInformation("{Server}: {Passed} passed, {Failed} failed, {Errors} errors in {Duration:F1}s",
                LogAnon.S(serverName), summary.Passed, summary.Failed, summary.Errors, summary.Duration.TotalSeconds);

            OnExecutionCompleted?.Invoke(summary);
            return summary;
        }

        /// <summary>
        /// Runs a filtered subset of enabled checks against a single SQL Server instance.
        /// Use this for Quick Check mode (e.g., Critical + Warning severity only).
        /// </summary>
        public async Task<CheckExecutionSummary> ExecuteChecksAsync(
            ServerConnection connection, string serverName,
            Func<SqlCheck, bool> filter, CancellationToken ct = default)
        {
            var summary = new CheckExecutionSummary
            {
                InstanceName = serverName,
                StartedAt = DateTime.UtcNow
            };

            var filteredChecks = _checkRepo.GetEnabledChecks().Where(filter).ToList();
            summary.TotalChecks = filteredChecks.Count;

            if (filteredChecks.Count == 0)
            {
                summary.CompletedAt = DateTime.UtcNow;
                return summary;
            }

            var throttle = _instanceThrottles.GetOrAdd(serverName,
                _ => new SemaphoreSlim(MaxConcurrentQueriesPerInstance));

            // Use master database for check execution to allow running checks
            // even when SQLWATCH is not installed
            var connectionString = connection.GetConnectionString(serverName, "master");

            var tasks = filteredChecks.Select(check =>
                ExecuteSingleCheckAsync(check, connectionString, serverName, throttle, ct));

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.ErrorMessage != null)
                    summary.Errors++;
                else if (result.Passed)
                    summary.Passed++;
                else
                    summary.Failed++;

                StoreResult(serverName, result);
            }

            summary.CompletedAt = DateTime.UtcNow;
            _lastSummary[serverName] = summary;

            // Persist the run to disk; consumers re-hydrate via the store.
            _resultStore?.WriteRun(serverName, results);

            _logger.LogInformation("{Server}: {Passed} passed, {Failed} failed, {Errors} errors in {Duration:F1}s",
                LogAnon.S(serverName), summary.Passed, summary.Failed, summary.Errors, summary.Duration.TotalSeconds);

            OnExecutionCompleted?.Invoke(summary);
            return summary;
        }

        /// <summary>
        /// Runs all enabled checks against every enabled server connection.
        /// </summary>
        public async Task<List<CheckExecutionSummary>> ExecuteChecksAllInstancesAsync(
            CancellationToken ct = default)
        {
            var summaries = new List<CheckExecutionSummary>();
            var connections = _connectionManager.GetEnabledConnections();

            foreach (var conn in connections)
            {
                foreach (var server in conn.GetServerList())
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var summary = await ExecuteChecksAsync(conn, server, ct);
                        summaries.Add(summary);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing checks on {Server}", LogAnon.S(server));

                        summaries.Add(new CheckExecutionSummary
                        {
                            InstanceName = server,
                            StartedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow,
                            Errors = _checkRepo.GetEnabledChecks().Count
                        });
                    }
                }
            }

            return summaries;
        }

        // ────────────────────── Single Check ──────────────────────

        private async Task<CheckResult> ExecuteSingleCheckAsync(
            SqlCheck check, string connectionString, string serverName,
            SemaphoreSlim throttle, CancellationToken ct)
        {
            var result = new CheckResult
            {
                CheckId = check.Id,
                CheckName = check.Name,
                Category = check.Category,
                Severity = check.Severity,
                ExpectedValue = check.ExpectedValue,
                EffortHours = check.EffortHours,
                InstanceName = serverName,
                RecommendedAction = check.RecommendedAction,
                Description = check.Description
            };

            if (string.IsNullOrWhiteSpace(check.SqlQuery))
            {
                result.ErrorMessage = "Check has no SQL query defined";
                return result;
            }

            var sw = Stopwatch.StartNew();

            await throttle.WaitAsync(ct);
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = check.SqlQuery;
                cmd.CommandTimeout = CheckCommandTimeoutSeconds;

                // Determine execution type:
                //   "Binary" / "scalar" / null → ExecuteScalar, compare to ExpectedValue
                //   "RowCount"                 → count rows, evaluate via RowCountCondition
                var execType = (check.ExecutionType ?? "scalar").ToLowerInvariant();

                if (execType == "rowcount")
                {
                    // Row-count based check — count returned rows and evaluate
                    using var reader = await cmd.ExecuteReaderAsync(ct);
                    int rowCount = 0;
                    while (await reader.ReadAsync(ct)) rowCount++;
                    result.ActualValue = rowCount;
                    result.Passed = EvaluateRowCount(rowCount, check);
                }
                else
                {
                    // Check if this check has a text-based ResultInterpretation
                    var ri = check.ResultInterpretation;
                    if (!string.IsNullOrEmpty(ri) && ri.Contains("Pass", StringComparison.OrdinalIgnoreCase))
                    {
                        // Text-based result: e.g., PassFail, PassInfo, PassWarnFail, PassFailSkip
                        // SQL should return a 'result' column with PASS/FAIL/INFO/WARN/SKIP
                        using var reader = await cmd.ExecuteReaderAsync(ct);
                        string resultText = "";
                        if (await reader.ReadAsync(ct))
                        {
                            try { resultText = reader["result"]?.ToString() ?? ""; }
                            catch { resultText = reader[0]?.ToString() ?? ""; }
                        }

                        result.ActualValue = 0; // text results don't map to numeric
                        result.Message = resultText;
                        result.Passed = resultText.Equals("PASS", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        // Numeric check (default): SELECT CASE WHEN EXISTS (...) THEN 1 ELSE 0 END
                        var scalar = await cmd.ExecuteScalarAsync(ct);
                        result.ActualValue = scalar != null && scalar != DBNull.Value
                            ? Convert.ToInt32(scalar)
                            : 0;

                        if (check.Severity.Equals("Info", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Passed = true;
                        }
                        else
                        {
                            result.Passed = result.ActualValue == check.ExpectedValue;
                        }
                        result.Message = result.Passed
                            ? $"Check passed (value={result.ActualValue})"
                            : $"Check failed: got {result.ActualValue}, expected {check.ExpectedValue}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Message = $"Error: {ex.Message}";
                result.Passed = false;
            }
            finally
            {
                throttle.Release();
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;
                result.ExecutedAt = DateTime.UtcNow;
            }

            // Fire per-check diagnostic event
            try { OnCheckCompleted?.Invoke(result); }
            catch (Exception ex) { _logger.LogWarning(ex, "OnCheckCompleted subscriber threw for check {CheckId}", result.CheckId); }

            return result;
        }

        private static bool EvaluateRowCount(int rowCount, SqlCheck check)
        {
            var condition = (check.RowCountCondition ?? "equals").ToLowerInvariant();
            return condition switch
            {
                // SQLTriage format (snake_case)
                "equals" => rowCount == check.ExpectedValue,
                "greater_than" => rowCount > check.ExpectedValue,
                "less_than" => rowCount < check.ExpectedValue,
                "not_equals" => rowCount != check.ExpectedValue,

                // SQLMonitoring format (PascalCase with embedded value)
                "equals0" => rowCount == 0,
                "greaterthan0" => rowCount > 0,
                "lessthan" => rowCount < check.ExpectedValue,
                "notequals0" => rowCount != 0,

                _ => rowCount == check.ExpectedValue
            };
        }

        // ────────────────────── Result Storage ──────────────────────

        private void StoreResult(string instanceName, CheckResult result)
        {
            var list = _resultsByInstance.GetOrAdd(instanceName, _ => new List<CheckResult>());

            lock (list)
            {
                list.Insert(0, result);
                while (list.Count > _maxResultsPerInstance)
                    list.RemoveAt(list.Count - 1);
            }

            // Persist for restart survival
            try { _historyService?.RecordCheckResult(instanceName, result); } catch { /* best-effort */ }
        }

        // ────────────────────── Queries ──────────────────────

        /// <summary>
        /// Gets the most recent results for an instance, optionally filtered.
        /// Hot cache holds the most recent run; older data is fetched from SQLite
        /// (GovernanceHistoryService) on demand to keep RAM bounded.
        /// </summary>
        public List<CheckResult> GetResults(string instanceName, int maxCount = 50,
            string? category = null, bool? passedOnly = null)
        {
            List<CheckResult> hot;
            if (_resultsByInstance.TryGetValue(instanceName, out var list))
            {
                lock (list)
                {
                    hot = new List<CheckResult>(list);
                }
            }
            else
            {
                hot = new List<CheckResult>();
            }

            // Re-hydrate from disk if the hot cache is short of what was asked.
            // Most consumers ask for 500–1000 to render a dashboard; the hot cache
            // is capped at 50 to keep RAM down, so the dashboard depends on persisted
            // data for the rest. Order of preference: JSON file (richest), SQLite (fallback).
            if (hot.Count < maxCount)
            {
                var seen = new HashSet<string>(hot.Select(r => r.CheckId), StringComparer.OrdinalIgnoreCase);

                // Primary: JSON per-server store
                if (_resultStore != null)
                {
                    try
                    {
                        var fromJson = _resultStore.ReadLatestRun(instanceName);
                        if (fromJson != null)
                        {
                            foreach (var r in fromJson)
                                if (seen.Add(r.CheckId)) hot.Add(r);
                        }
                    }
                    catch { /* best-effort */ }
                }

                // Fallback: SQLite history (used when JSON file is missing for this server)
                if (hot.Count < maxCount && _historyService != null)
                {
                    try
                    {
                        var fromHistory = _historyService.LoadLatestCheckResults(instanceName);
                        foreach (var r in fromHistory)
                            if (seen.Add(r.CheckId)) hot.Add(r);
                    }
                    catch { /* best-effort */ }
                }
            }

            IEnumerable<CheckResult> query = hot;
            if (category != null)
                query = query.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (passedOnly.HasValue)
                query = query.Where(r => r.Passed == passedOnly.Value);
            return query.Take(maxCount).ToList();
        }

        /// <summary>
        /// Gets the most recent result for a specific check on a specific instance.
        /// </summary>
        public CheckResult? GetLatestResult(string instanceName, string checkId)
        {
            if (!_resultsByInstance.TryGetValue(instanceName, out var list))
                return null;

            lock (list)
            {
                return list.FirstOrDefault(r => r.CheckId == checkId);
            }
        }

        /// <summary>
        /// Gets the last execution summary for an instance.
        /// </summary>
        public CheckExecutionSummary? GetLastSummary(string instanceName)
        {
            _lastSummary.TryGetValue(instanceName, out var summary);
            return summary;
        }

        /// <summary>
        /// Gets summaries for all instances that have been checked.
        /// </summary>
        public Dictionary<string, CheckExecutionSummary> GetAllSummaries()
        {
            return new Dictionary<string, CheckExecutionSummary>(_lastSummary);
        }

        /// <summary>
        /// Gets all instance names that have results.
        /// </summary>
        public List<string> GetMonitoredInstances()
        {
            return _resultsByInstance.Keys.ToList();
        }

        /// <summary>
        /// Clears all stored results for a specific instance.
        /// </summary>
        public void ClearResults(string instanceName)
        {
            if (_resultsByInstance.TryRemove(instanceName, out _))
            {
                _logger.LogDebug("Cleared results for {Instance}", instanceName);
            }
        }

        /// <summary>
        /// Clears all stored results for all instances.
        /// </summary>
        public void ClearAllResults()
        {
            _resultsByInstance.Clear();
            _lastSummary.Clear();
            _logger.LogDebug("Cleared all check results");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var throttle in _instanceThrottles.Values)
                    throttle.Dispose();
                _disposed = true;
            }
        }
    }
}
