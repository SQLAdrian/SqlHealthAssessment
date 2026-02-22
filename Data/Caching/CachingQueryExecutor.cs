/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlHealthAssessment.Data.Models;
using Microsoft.Extensions.Configuration;

namespace SqlHealthAssessment.Data.Caching
{
    /// <summary>
    /// Decorator around <see cref="QueryExecutor"/> that adds local SQLite caching
    /// with delta-fetch support.
    ///
    /// Caching strategy per panel type:
    ///
    ///   TimeSeries — Delta fetch: on steady-state refresh, only fetch rows newer than
    ///                the last successful fetch, merge into cache, serve full window from cache.
    ///
    ///   StatCard, BarGauge, CheckStatus, DataGrid, TextCard — Always try SQL Server first
    ///                (these are cheap: TOP 1 or small aggregates), cache the result, and
    ///                fall back to the cached value on SQL Server failure.
    /// </summary>
    public class CachingQueryExecutor
    {
        private readonly QueryExecutor _inner;
        private readonly SqliteCacheStore _cache;
        private readonly CacheStateTracker _stateTracker;
        private readonly DashboardConfigService _configService;
        private readonly TimeSpan _evictionThreshold;
        private readonly SemaphoreSlim _invalidationLock = new(1, 1);

        /// <summary>
        /// True when the most recent SQL Server query failed and we are serving stale cached data.
        /// </summary>
        public bool IsServingStaleData => _stateTracker.IsOffline;

        /// <summary>
        /// Timestamp of the last successful SQL Server fetch, displayed when serving stale data.
        /// </summary>
        public DateTime? LastSuccessfulFetch => _stateTracker.LastSuccessfulFetch;

        public CachingQueryExecutor(
            QueryExecutor inner,
            SqliteCacheStore cache,
            CacheStateTracker stateTracker,
            DashboardConfigService configService,
            IConfiguration configuration)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _stateTracker = stateTracker ?? throw new ArgumentNullException(nameof(stateTracker));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            var hours = configuration.GetValue<int>("CacheEvictionHours", 24);
            _evictionThreshold = TimeSpan.FromHours(hours);
        }

        // ──────────────────────── Refresh Cycle Preparation ─────────────

        /// <summary>
        /// Called once per LoadData() cycle, before any panel queries.
        /// Handles:
        ///   1. Detecting filter changes (time range or instance) that require full invalidation.
        ///   2. Periodic cache eviction of very old data.
        /// </summary>
        public async Task PrepareRefreshCycle(string dashboardId, int timeRangeMinutes, string selectedInstance)
        {
            await _invalidationLock.WaitAsync();
            try
            {
                if (_stateTracker.RequiresFullReload(dashboardId, timeRangeMinutes, selectedInstance))
                {
                    await _cache.InvalidateAllAsync();
                }
                _stateTracker.RecordFilterState(dashboardId, timeRangeMinutes, selectedInstance);
            }
            finally
            {
                _invalidationLock.Release();
            }
        }

        /// <summary>
        /// Runs periodic eviction of cached data older than the configured threshold.
        /// Called by CacheEvictionService on a timer.
        /// </summary>
        public Task EvictStaleDataAsync() => _cache.EvictOlderThanAsync(_evictionThreshold);

        // ──────────────────────── ExecuteQueryAsync (DataTable) ──────────

        /// <summary>
        /// Cached version of <see cref="QueryExecutor.ExecuteQueryAsync(string, DashboardFilter, Dictionary{string, object}?, CancellationToken)"/>.
        /// Used by StatCard, DataGrid, and TextCard panels.
        /// Strategy: try SQL Server, cache result, fall back to cache on failure.
        /// </summary>
        public async Task<DataTable> ExecuteQueryAsync(
            string queryId,
            DashboardFilter filter,
            Dictionary<string, object>? additionalParams = null,
            CancellationToken cancellationToken = default)
        {
            var instanceKey = BuildInstanceKey(filter);

            try
            {
                // Always try SQL Server first for DataTable queries (StatCard, DataGrid, TextCard)
                var result = await _inner.ExecuteQueryAsync(queryId, filter, additionalParams, cancellationToken);
                _stateTracker.RecordSuccess();

                // Cache the result for offline fallback
                await _cache.UpsertDataTableAsync(queryId, instanceKey, result, DateTime.UtcNow);
                await _cache.SetLastFetchTimeAsync(queryId, instanceKey, DateTime.UtcNow);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw; // Don't cache cancellation as offline
            }
            catch (Exception)
            {
                // SQL Server failed — try serving from cache
                _stateTracker.RecordFailure();

                var cached = await _cache.GetDataTableAsync(queryId, instanceKey);
                if (cached != null)
                    return cached;

                throw; // No cache available, propagate the original error
            }
        }

        // ──────────────────────── ExecuteQueryAsync<T> (typed) ──────────

        /// <summary>
        /// Cached version of <see cref="QueryExecutor.ExecuteQueryAsync{T}(string, DashboardFilter, Func{IDataReader, T}, Dictionary{string, object}?, CancellationToken)"/>.
        /// Used by TimeSeries, BarGauge, and CheckStatus panels.
        /// Strategy depends on the panel type (delta for TimeSeries, full-replace for others).
        /// </summary>
        public async Task<List<T>> ExecuteQueryAsync<T>(
            string queryId,
            DashboardFilter filter,
            Func<IDataReader, T> mapper,
            Dictionary<string, object>? additionalParams = null,
            CancellationToken cancellationToken = default)
        {
            var panelType = GetPanelType(queryId);
            var instanceKey = BuildInstanceKey(filter);

            return panelType switch
            {
                "TimeSeries" => await DeltaFetchTimeSeriesAsync(queryId, filter, instanceKey, mapper, cancellationToken),
                "BarGauge" => await FetchWithFallbackBarGaugeAsync(queryId, filter, instanceKey, mapper, cancellationToken),
                "CheckStatus" => await FetchWithFallbackCheckStatusAsync(queryId, filter, instanceKey, mapper, cancellationToken),
                _ => await FetchDirectAsync(queryId, filter, mapper, additionalParams, cancellationToken)
            };
        }

        /// <summary>
        /// Cached version of <see cref="QueryExecutor.ExecuteScalarAsync{T}"/>.
        /// Falls back to default(T) on failure if no cache exists.
        /// </summary>
        public async Task<T?> ExecuteScalarAsync<T>(
            string queryId,
            DashboardFilter filter,
            Dictionary<string, object>? additionalParams = null,
            CancellationToken cancellationToken = default)
        {
            // Scalar queries are simple — no caching, just pass through
            return await _inner.ExecuteScalarAsync<T>(queryId, filter, additionalParams, cancellationToken);
        }

        // ──────────────────────── Delta Fetch (TimeSeries) ──────────────

        /// <summary>
        /// Core delta-fetch algorithm for TimeSeries panels:
        ///
        ///   1. Look up last_fetch from cache_metadata.
        ///   2. If no prior fetch → full load from SQL Server, write to cache.
        ///   3. If prior fetch → modify filter.TimeFrom to last_fetch, fetch delta only.
        ///   4. Upsert delta rows into SQLite.
        ///   5. Trim cache rows older than filter.TimeFrom.
        ///   6. Read full window from SQLite and return.
        ///   7. On SQL Server failure → serve from cache (stale data).
        /// </summary>
        private async Task<List<T>> DeltaFetchTimeSeriesAsync<T>(
            string queryId,
            DashboardFilter filter,
            string instanceKey,
            Func<IDataReader, T> mapper,
            CancellationToken cancellationToken)
        {
            var lastFetch = await _cache.GetLastFetchTimeAsync(queryId, instanceKey);

            if (lastFetch == null)
            {
                // First fetch ever for this query+instance — full load
                return await FullFetchTimeSeriesAsync(queryId, filter, instanceKey, mapper, cancellationToken);
            }

            // Delta fetch: only get rows newer than last fetch
            try
            {
                var deltaFilter = new DashboardFilter
                {
                    TimeFrom = lastFetch.Value,
                    TimeTo = filter.TimeTo,
                    Instances = filter.Instances,
                    Database = filter.Database,
                    WaitGrouping = filter.WaitGrouping,
                    AggregationMinutes = filter.AggregationMinutes
                };

                var deltaRows = await _inner.ExecuteQueryAsync(queryId, deltaFilter, mapper, null, cancellationToken);
                _stateTracker.RecordSuccess();

                // Convert to TimeSeriesPoint for cache storage
                if (deltaRows.Count > 0 && deltaRows is List<TimeSeriesPoint> tsPoints)
                {
                    await _cache.UpsertTimeSeriesAsync(queryId, instanceKey, tsPoints, DateTime.UtcNow);
                }

                await _cache.SetLastFetchTimeAsync(queryId, instanceKey, DateTime.UtcNow);

                // Trim old data outside the current time window
                await _cache.TrimTimeSeriesAsync(queryId, instanceKey, filter.TimeFrom);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // SQL Server failed — fall through to serve from cache
                _stateTracker.RecordFailure();
            }

            // Serve full window from cache
            var cachedRows = await _cache.GetTimeSeriesAsync(queryId, instanceKey, filter.TimeFrom, filter.TimeTo);

            if (cachedRows.Count > 0 && typeof(T) == typeof(TimeSeriesPoint))
            {
                return (List<T>)(object)cachedRows;
            }

            // Cache is empty and SQL Server is down — return empty list
            return new List<T>();
        }

        /// <summary>
        /// Full initial fetch for a TimeSeries query. Writes all results to cache.
        /// </summary>
        private async Task<List<T>> FullFetchTimeSeriesAsync<T>(
            string queryId,
            DashboardFilter filter,
            string instanceKey,
            Func<IDataReader, T> mapper,
            CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _inner.ExecuteQueryAsync(queryId, filter, mapper, null, cancellationToken);
                _stateTracker.RecordSuccess();

                // Cache the results
                if (rows is List<TimeSeriesPoint> tsPoints && tsPoints.Count > 0)
                {
                    await _cache.UpsertTimeSeriesAsync(queryId, instanceKey, tsPoints, DateTime.UtcNow);
                }
                await _cache.SetLastFetchTimeAsync(queryId, instanceKey, DateTime.UtcNow);

                return rows;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // SQL Server failed on initial load — check if cache has any data
                _stateTracker.RecordFailure();

                var cached = await _cache.GetTimeSeriesAsync(queryId, instanceKey, filter.TimeFrom, filter.TimeTo);
                if (cached.Count > 0 && typeof(T) == typeof(TimeSeriesPoint))
                {
                    return (List<T>)(object)cached;
                }

                throw; // No cache, propagate error
            }
        }

        // ──────────────────────── Fetch-with-Fallback (BarGauge) ────────

        private async Task<List<T>> FetchWithFallbackBarGaugeAsync<T>(
            string queryId,
            DashboardFilter filter,
            string instanceKey,
            Func<IDataReader, T> mapper,
            CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _inner.ExecuteQueryAsync(queryId, filter, mapper, null, cancellationToken);
                _stateTracker.RecordSuccess();

                // Cache for offline fallback
                if (rows is List<StatValue> statRows)
                {
                    await _cache.UpsertBarGaugeAsync(queryId, instanceKey, statRows, DateTime.UtcNow);
                    await _cache.SetLastFetchTimeAsync(queryId, instanceKey, DateTime.UtcNow);
                }

                return rows;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                _stateTracker.RecordFailure();

                var cached = await _cache.GetBarGaugeAsync(queryId, instanceKey);
                if (cached.Count > 0 && typeof(T) == typeof(StatValue))
                {
                    return (List<T>)(object)cached;
                }

                throw;
            }
        }

        // ──────────────────────── Fetch-with-Fallback (CheckStatus) ─────

        private async Task<List<T>> FetchWithFallbackCheckStatusAsync<T>(
            string queryId,
            DashboardFilter filter,
            string instanceKey,
            Func<IDataReader, T> mapper,
            CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _inner.ExecuteQueryAsync(queryId, filter, mapper, null, cancellationToken);
                _stateTracker.RecordSuccess();

                // Cache for offline fallback
                if (rows is List<CheckStatus> checkRows)
                {
                    await _cache.UpsertCheckStatusAsync(queryId, instanceKey, checkRows, DateTime.UtcNow);
                    await _cache.SetLastFetchTimeAsync(queryId, instanceKey, DateTime.UtcNow);
                }

                return rows;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                _stateTracker.RecordFailure();

                var cached = await _cache.GetCheckStatusAsync(queryId, instanceKey);
                if (cached.Count > 0 && typeof(T) == typeof(CheckStatus))
                {
                    return (List<T>)(object)cached;
                }

                throw;
            }
        }

        // ──────────────────────── Direct Passthrough ────────────────────

        /// <summary>
        /// Passthrough for unknown panel types — no caching.
        /// </summary>
        private async Task<List<T>> FetchDirectAsync<T>(
            string queryId,
            DashboardFilter filter,
            Func<IDataReader, T> mapper,
            Dictionary<string, object>? additionalParams,
            CancellationToken cancellationToken)
        {
            return await _inner.ExecuteQueryAsync(queryId, filter, mapper, additionalParams, cancellationToken);
        }

        // ──────────────────────── Helpers ───────────────────────────────

        /// <summary>
        /// Determines the panel type for a given queryId by searching the dashboard config.
        /// Returns the PanelType string ("TimeSeries", "StatCard", etc.) or "Unknown".
        /// </summary>
        private string GetPanelType(string queryId)
        {
            foreach (var dashboard in _configService.Config.Dashboards)
            {
                var panel = dashboard.Panels.FirstOrDefault(p => p.Id == queryId);
                if (panel != null)
                    return panel.PanelType;
            }
            return "Unknown";
        }

        /// <summary>
        /// Builds a consistent cache key from the instance selection in the filter.
        /// Sorts instance names alphabetically to ensure the same set always maps
        /// to the same key regardless of ordering.
        /// </summary>
        public static string BuildInstanceKey(DashboardFilter filter)
        {
            if (filter.Instances == null || filter.Instances.Length == 0)
                return "__all__";

            var sorted = filter.Instances
                .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return string.Join(",", sorted);
        }
    }
}
