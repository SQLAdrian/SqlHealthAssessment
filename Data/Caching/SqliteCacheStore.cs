using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqlHealthAssessment.Data.Models;
using Microsoft.Data.Sqlite;

namespace SqlHealthAssessment.Data.Caching
{
    /// <summary>
    /// Manages the local SQLite cache database for dashboard query results.
    /// All writes are serialized through a SemaphoreSlim; reads are lock-free (WAL mode).
    /// The database file is created automatically in the application's base directory.
    /// </summary>
    public sealed class SqliteCacheStore : IDisposable
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public SqliteCacheStore()
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlHealthAssessment-cache.db");
            _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
            InitializeSchema();
        }

        // ──────────────────────────── Schema ────────────────────────────

        private void InitializeSchema()
        {
            using var conn = CreateConnection();
            conn.Open();

            // Enable WAL mode for concurrent reads during writes
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                pragma.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS cache_timeseries (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    time_value   TEXT NOT NULL,
                    series       TEXT NOT NULL,
                    value        REAL NOT NULL,
                    fetched_at   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key, time_value, series)
                );

                CREATE INDEX IF NOT EXISTS idx_ts_query_time
                    ON cache_timeseries(query_id, instance_key, time_value);

                CREATE TABLE IF NOT EXISTS cache_stat (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    label        TEXT NOT NULL DEFAULT '',
                    value        REAL NOT NULL,
                    unit         TEXT NOT NULL DEFAULT '',
                    color        TEXT NOT NULL DEFAULT '',
                    fetched_at   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key)
                );

                CREATE TABLE IF NOT EXISTS cache_bargauge (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    label        TEXT NOT NULL,
                    value        REAL NOT NULL,
                    unit         TEXT NOT NULL DEFAULT '',
                    instance     TEXT NOT NULL DEFAULT '',
                    color        TEXT NOT NULL DEFAULT '',
                    fetched_at   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key, label, instance)
                );

                CREATE TABLE IF NOT EXISTS cache_datatable (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    json_data    TEXT NOT NULL,
                    fetched_at   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key)
                );

                CREATE TABLE IF NOT EXISTS cache_checkstatus (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    status       TEXT NOT NULL,
                    count        INTEGER NOT NULL,
                    fetched_at   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key, status)
                );

                CREATE TABLE IF NOT EXISTS cache_metadata (
                    query_id     TEXT NOT NULL,
                    instance_key TEXT NOT NULL,
                    last_fetch   TEXT NOT NULL,
                    PRIMARY KEY (query_id, instance_key)
                );
            ";
            cmd.ExecuteNonQuery();
        }

        // ──────────────────────── Write Operations ──────────────────────

        /// <summary>
        /// Upserts time-series rows into the cache. Uses INSERT OR REPLACE
        /// so duplicate (query_id, instance_key, time_value, series) rows
        /// are overwritten with the latest values.
        /// </summary>
        public async Task UpsertTimeSeriesAsync(string queryId, string instanceKey,
            List<TimeSeriesPoint> rows, DateTime fetchedAt)
        {
            if (rows.Count == 0) return;

            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO cache_timeseries
                        (query_id, instance_key, time_value, series, value, fetched_at)
                    VALUES (@qid, @ikey, @tv, @s, @v, @fa)";

                var pQid = cmd.Parameters.Add("@qid", SqliteType.Text);
                var pIkey = cmd.Parameters.Add("@ikey", SqliteType.Text);
                var pTv = cmd.Parameters.Add("@tv", SqliteType.Text);
                var pS = cmd.Parameters.Add("@s", SqliteType.Text);
                var pV = cmd.Parameters.Add("@v", SqliteType.Real);
                var pFa = cmd.Parameters.Add("@fa", SqliteType.Text);

                var fetchedStr = fetchedAt.ToString("o");

                foreach (var row in rows)
                {
                    pQid.Value = queryId;
                    pIkey.Value = instanceKey;
                    pTv.Value = row.Time.ToString("o");
                    pS.Value = row.Series;
                    pV.Value = row.Value;
                    pFa.Value = fetchedStr;
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Upserts a single stat value, replacing the previous cached value.
        /// </summary>
        public async Task UpsertStatValueAsync(string queryId, string instanceKey,
            StatValue value, DateTime fetchedAt)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO cache_stat
                        (query_id, instance_key, label, value, unit, color, fetched_at)
                    VALUES (@qid, @ikey, @label, @val, @unit, @color, @fa)";
                cmd.Parameters.AddWithValue("@qid", queryId);
                cmd.Parameters.AddWithValue("@ikey", instanceKey);
                cmd.Parameters.AddWithValue("@label", value.Label);
                cmd.Parameters.AddWithValue("@val", value.Value);
                cmd.Parameters.AddWithValue("@unit", value.Unit);
                cmd.Parameters.AddWithValue("@color", value.Color);
                cmd.Parameters.AddWithValue("@fa", fetchedAt.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Upserts bar gauge data, replacing the previous cached snapshot.
        /// </summary>
        public async Task UpsertBarGaugeAsync(string queryId, string instanceKey,
            List<StatValue> rows, DateTime fetchedAt)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                // Clear previous gauge data for this query
                using (var delCmd = conn.CreateCommand())
                {
                    delCmd.Transaction = transaction;
                    delCmd.CommandText = "DELETE FROM cache_bargauge WHERE query_id = @qid AND instance_key = @ikey";
                    delCmd.Parameters.AddWithValue("@qid", queryId);
                    delCmd.Parameters.AddWithValue("@ikey", instanceKey);
                    delCmd.ExecuteNonQuery();
                }

                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO cache_bargauge
                        (query_id, instance_key, label, value, unit, instance, color, fetched_at)
                    VALUES (@qid, @ikey, @label, @val, @unit, @inst, @color, @fa)";

                var pQid = cmd.Parameters.Add("@qid", SqliteType.Text);
                var pIkey = cmd.Parameters.Add("@ikey", SqliteType.Text);
                var pLabel = cmd.Parameters.Add("@label", SqliteType.Text);
                var pVal = cmd.Parameters.Add("@val", SqliteType.Real);
                var pUnit = cmd.Parameters.Add("@unit", SqliteType.Text);
                var pInst = cmd.Parameters.Add("@inst", SqliteType.Text);
                var pColor = cmd.Parameters.Add("@color", SqliteType.Text);
                var pFa = cmd.Parameters.Add("@fa", SqliteType.Text);

                var fetchedStr = fetchedAt.ToString("o");
                foreach (var row in rows)
                {
                    pQid.Value = queryId;
                    pIkey.Value = instanceKey;
                    pLabel.Value = row.Label;
                    pVal.Value = row.Value;
                    pUnit.Value = row.Unit;
                    pInst.Value = row.Instance;
                    pColor.Value = row.Color;
                    pFa.Value = fetchedStr;
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Stores a DataTable as a JSON blob, replacing any previous cached value.
        /// </summary>
        public async Task UpsertDataTableAsync(string queryId, string instanceKey,
            DataTable table, DateTime fetchedAt)
        {
            var json = SerializeDataTable(table);

            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO cache_datatable
                        (query_id, instance_key, json_data, fetched_at)
                    VALUES (@qid, @ikey, @json, @fa)";
                cmd.Parameters.AddWithValue("@qid", queryId);
                cmd.Parameters.AddWithValue("@ikey", instanceKey);
                cmd.Parameters.AddWithValue("@json", json);
                cmd.Parameters.AddWithValue("@fa", fetchedAt.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Upserts check status data, replacing the previous cached snapshot.
        /// </summary>
        public async Task UpsertCheckStatusAsync(string queryId, string instanceKey,
            List<CheckStatus> rows, DateTime fetchedAt)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                using (var delCmd = conn.CreateCommand())
                {
                    delCmd.Transaction = transaction;
                    delCmd.CommandText = "DELETE FROM cache_checkstatus WHERE query_id = @qid AND instance_key = @ikey";
                    delCmd.Parameters.AddWithValue("@qid", queryId);
                    delCmd.Parameters.AddWithValue("@ikey", instanceKey);
                    delCmd.ExecuteNonQuery();
                }

                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO cache_checkstatus
                        (query_id, instance_key, status, count, fetched_at)
                    VALUES (@qid, @ikey, @status, @count, @fa)";

                var pQid = cmd.Parameters.Add("@qid", SqliteType.Text);
                var pIkey = cmd.Parameters.Add("@ikey", SqliteType.Text);
                var pStatus = cmd.Parameters.Add("@status", SqliteType.Text);
                var pCount = cmd.Parameters.Add("@count", SqliteType.Integer);
                var pFa = cmd.Parameters.Add("@fa", SqliteType.Text);

                var fetchedStr = fetchedAt.ToString("o");
                foreach (var row in rows)
                {
                    pQid.Value = queryId;
                    pIkey.Value = instanceKey;
                    pStatus.Value = row.Status;
                    pCount.Value = row.Count;
                    pFa.Value = fetchedStr;
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Records the high-water mark (most recent successful fetch time) for a query.
        /// </summary>
        public async Task SetLastFetchTimeAsync(string queryId, string instanceKey, DateTime time)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO cache_metadata (query_id, instance_key, last_fetch)
                    VALUES (@qid, @ikey, @lf)";
                cmd.Parameters.AddWithValue("@qid", queryId);
                cmd.Parameters.AddWithValue("@ikey", instanceKey);
                cmd.Parameters.AddWithValue("@lf", time.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ──────────────────────── Read Operations ───────────────────────

        /// <summary>
        /// Reads cached time-series data within the specified time window.
        /// </summary>
        public async Task<List<TimeSeriesPoint>> GetTimeSeriesAsync(
            string queryId, string instanceKey, DateTime from, DateTime to)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT time_value, series, value
                FROM cache_timeseries
                WHERE query_id = @qid
                  AND instance_key = @ikey
                  AND time_value >= @from
                  AND time_value <= @to
                ORDER BY time_value";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);
            cmd.Parameters.AddWithValue("@from", from.ToString("o"));
            cmd.Parameters.AddWithValue("@to", to.ToString("o"));

            var results = new List<TimeSeriesPoint>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TimeSeriesPoint
                {
                    Time = DateTime.Parse(reader.GetString(0)),
                    Series = reader.GetString(1),
                    Value = reader.GetDouble(2)
                });
            }
            return results;
        }

        /// <summary>
        /// Reads the cached stat value for a query, or null if not cached.
        /// </summary>
        public async Task<StatValue?> GetStatValueAsync(string queryId, string instanceKey)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT label, value, unit, color
                FROM cache_stat
                WHERE query_id = @qid AND instance_key = @ikey";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StatValue
                {
                    Label = reader.GetString(0),
                    Value = reader.GetDouble(1),
                    Unit = reader.GetString(2),
                    Color = reader.GetString(3)
                };
            }
            return null;
        }

        /// <summary>
        /// Reads cached bar gauge data for a query.
        /// </summary>
        public async Task<List<StatValue>> GetBarGaugeAsync(string queryId, string instanceKey)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT label, value, unit, instance, color
                FROM cache_bargauge
                WHERE query_id = @qid AND instance_key = @ikey";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);

            var results = new List<StatValue>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new StatValue
                {
                    Label = reader.GetString(0),
                    Value = reader.GetDouble(1),
                    Unit = reader.GetString(2),
                    Instance = reader.GetString(3),
                    Color = reader.GetString(4)
                });
            }
            return results;
        }

        /// <summary>
        /// Reads a cached DataTable from JSON, or null if not cached.
        /// </summary>
        public async Task<DataTable?> GetDataTableAsync(string queryId, string instanceKey)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT json_data FROM cache_datatable
                WHERE query_id = @qid AND instance_key = @ikey";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);

            var json = (string?)await cmd.ExecuteScalarAsync();
            return json != null ? DeserializeDataTable(json) : null;
        }

        /// <summary>
        /// Reads cached check status data for a query.
        /// </summary>
        public async Task<List<CheckStatus>> GetCheckStatusAsync(string queryId, string instanceKey)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT status, count FROM cache_checkstatus
                WHERE query_id = @qid AND instance_key = @ikey";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);

            var results = new List<CheckStatus>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new CheckStatus
                {
                    Status = reader.GetString(0),
                    Count = reader.GetInt32(1)
                });
            }
            return results;
        }

        /// <summary>
        /// Returns the high-water mark (last successful SQL Server fetch time) for a query,
        /// or null if the query has never been cached.
        /// </summary>
        public async Task<DateTime?> GetLastFetchTimeAsync(string queryId, string instanceKey)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT last_fetch FROM cache_metadata
                WHERE query_id = @qid AND instance_key = @ikey";
            cmd.Parameters.AddWithValue("@qid", queryId);
            cmd.Parameters.AddWithValue("@ikey", instanceKey);

            var result = (string?)await cmd.ExecuteScalarAsync();
            return result != null ? DateTime.Parse(result) : null;
        }

        // ──────────────────────── Eviction / Invalidation ───────────────

        /// <summary>
        /// Removes all cached data older than the specified age across all tables.
        /// </summary>
        public async Task EvictOlderThanAsync(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow.Subtract(maxAge).ToString("o");

            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                var tables = new[] { "cache_timeseries", "cache_stat", "cache_bargauge",
                                     "cache_datatable", "cache_checkstatus" };

                foreach (var table in tables)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DELETE FROM {table} WHERE fetched_at < @cutoff";
                    cmd.Parameters.AddWithValue("@cutoff", cutoff);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Also clean metadata for queries with no remaining data
                using var metaCmd = conn.CreateCommand();
                metaCmd.CommandText = "DELETE FROM cache_metadata WHERE last_fetch < @cutoff";
                metaCmd.Parameters.AddWithValue("@cutoff", cutoff);
                await metaCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Hard retention purge: deletes all data older than the specified age
        /// across every cache table and returns the total number of rows removed.
        /// </summary>
        public async Task<long> PurgeOlderThanAsync(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow.Subtract(maxAge).ToString("o");
            long totalDeleted = 0;

            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                var tables = new[] { "cache_timeseries", "cache_stat", "cache_bargauge",
                                     "cache_datatable", "cache_checkstatus" };

                foreach (var table in tables)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DELETE FROM {table} WHERE fetched_at < @cutoff";
                    cmd.Parameters.AddWithValue("@cutoff", cutoff);
                    totalDeleted += await cmd.ExecuteNonQueryAsync();
                }

                using var metaCmd = conn.CreateCommand();
                metaCmd.CommandText = "DELETE FROM cache_metadata WHERE last_fetch < @cutoff";
                metaCmd.Parameters.AddWithValue("@cutoff", cutoff);
                totalDeleted += await metaCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }

            return totalDeleted;
        }

        /// <summary>
        /// Trims time-series cache rows older than the specified cutoff time
        /// for a specific query. Used to keep the cache tight to the active time window.
        /// </summary>
        public async Task TrimTimeSeriesAsync(string queryId, string instanceKey, DateTime olderThan)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM cache_timeseries
                    WHERE query_id = @qid
                      AND instance_key = @ikey
                      AND time_value < @cutoff";
                cmd.Parameters.AddWithValue("@qid", queryId);
                cmd.Parameters.AddWithValue("@ikey", instanceKey);
                cmd.Parameters.AddWithValue("@cutoff", olderThan.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Clears all cache tables. Called when the user changes time range or instance.
        /// </summary>
        public async Task InvalidateAllAsync()
        {
            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                var tables = new[] { "cache_timeseries", "cache_stat", "cache_bargauge",
                                     "cache_datatable", "cache_checkstatus", "cache_metadata" };
                foreach (var table in tables)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DELETE FROM {table}";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ──────────────────────── DataTable Serialization ───────────────

        private static string SerializeDataTable(DataTable dt)
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (DataRow row in dt.Rows)
            {
                var dict = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                rows.Add(dict);
            }
            return JsonSerializer.Serialize(rows, JsonOptions);
        }

        private static DataTable DeserializeDataTable(string json)
        {
            var dt = new DataTable();
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions);
            if (rows == null || rows.Count == 0) return dt;

            // Create columns from first row's keys
            foreach (var key in rows[0].Keys)
            {
                dt.Columns.Add(key);
            }

            // Populate rows
            foreach (var rowDict in rows)
            {
                var row = dt.NewRow();
                foreach (var kvp in rowDict)
                {
                    row[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.Null
                        ? DBNull.Value
                        : (object)kvp.Value.ToString()!;
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ──────────────────────── Maintenance ─────────────────────────

        /// <summary>
        /// Runs SQLite maintenance tasks: PRAGMA optimize (updates query planner
        /// statistics), VACUUM (reclaims free pages and defragments the database
        /// file), and optionally PRAGMA integrity_check.
        /// </summary>
        public async Task<MaintenanceResult> RunMaintenanceAsync(bool includeIntegrityCheck = false)
        {
            var result = new MaintenanceResult { StartedAt = DateTime.UtcNow };

            await _writeLock.WaitAsync();
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                // PRAGMA optimize — lets SQLite update internal statistics
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA optimize;";
                    await cmd.ExecuteNonQueryAsync();
                }
                result.OptimizeCompleted = true;

                // VACUUM — rebuilds the database file, reclaiming free pages
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "VACUUM;";
                    await cmd.ExecuteNonQueryAsync();
                }
                result.VacuumCompleted = true;

                // Optional integrity check
                if (includeIntegrityCheck)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA integrity_check;";
                    var checkResult = (string?)await cmd.ExecuteScalarAsync();
                    result.IntegrityCheckResult = checkResult ?? "unknown";
                    result.IntegrityOk = string.Equals(checkResult, "ok",
                        StringComparison.OrdinalIgnoreCase);
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Success = true;
            }
            finally
            {
                _writeLock.Release();
            }

            return result;
        }

        // ──────────────────────── Helpers ───────────────────────────────

        private SqliteConnection CreateConnection() => new(_connectionString);

        public void Dispose()
        {
            if (!_disposed)
            {
                _writeLock.Dispose();
                _disposed = true;
            }
        }
    }
}
