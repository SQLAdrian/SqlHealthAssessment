/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Threading;
using SqlHealthAssessment.Data.Models;
using Microsoft.Extensions.Configuration;

namespace SqlHealthAssessment.Data.Caching
{
    /// <summary>
    /// Runs periodic liveQueries database maintenance on a timer.
    /// Each cycle: purges data older than the retention period, runs
    /// PRAGMA optimize and VACUUM, and optionally PRAGMA integrity_check.
    /// </summary>
    public class liveQueriesMaintenanceService : IDisposable
    {
        private readonly liveQueriesCacheStore _cache;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _retentionPeriod;
        private readonly int _integrityCheckEveryNRuns;
        private Timer? _timer;
        private int _runCount;
        private bool _disposed;

        /// <summary>
        /// Raised after each maintenance run completes (success or failure).
        /// </summary>
        public event Action<MaintenanceResult>? OnMaintenanceCompleted;

        public MaintenanceResult? LastResult { get; private set; }

        public liveQueriesMaintenanceService(liveQueriesCacheStore cache, IConfiguration config)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // Default: run maintenance every 4 hours
            var intervalHours = 4;
            if (int.TryParse(config["liveQueriesMaintenanceIntervalHours"], out var h) && h > 0)
                intervalHours = h;
            _interval = TimeSpan.FromHours(intervalHours);

            // Default: purge data older than 30 days
            var retentionDays = 30;
            if (int.TryParse(config["DataRetentionDays"], out var d) && d > 0)
                retentionDays = d;
            _retentionPeriod = TimeSpan.FromDays(retentionDays);

            // Default: run integrity check every 6th maintenance run (i.e. once per day at 4h intervals)
            _integrityCheckEveryNRuns = 6;
            if (int.TryParse(config["liveQueriesIntegrityCheckEveryNRuns"], out var n) && n > 0)
                _integrityCheckEveryNRuns = n;
        }

        /// <summary>
        /// Starts the periodic maintenance timer.
        /// </summary>
        public void Start()
        {
            // First run after one interval; then repeat at the configured interval
            _timer = new Timer(OnTimerTick, null, _interval, _interval);
        }

        /// <summary>
        /// Stops the periodic maintenance timer.
        /// </summary>
        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private async void OnTimerTick(object? state)
        {
            try
            {
                _runCount++;
                var includeIntegrity = (_runCount % _integrityCheckEveryNRuns) == 0;

                // 1. Purge data beyond the retention period (default 30 days)
                var rowsPurged = await _cache.PurgeOlderThanAsync(_retentionPeriod);

                // 2. Run optimize + vacuum (+ optional integrity check)
                var result = await _cache.RunMaintenanceAsync(includeIntegrity);
                result.RowsPurged = rowsPurged;
                LastResult = result;

                System.Diagnostics.Debug.WriteLine(
                    $"[liveQueriesMaintenanceService] Maintenance completed in {result.Duration.TotalSeconds:F1}s " +
                    $"(purged={rowsPurged}, optimize={result.OptimizeCompleted}, vacuum={result.VacuumCompleted}" +
                    (includeIntegrity ? $", integrity={result.IntegrityCheckResult}" : "") + ")");

                OnMaintenanceCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[liveQueriesMaintenanceService] Maintenance error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer?.Dispose();
                _disposed = true;
            }
        }
    }
}
