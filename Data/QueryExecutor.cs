/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using SqlHealthAssessment.Data.Models;
using Microsoft.Extensions.Configuration;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Executes parameterized queries from the QueryStore against the configured database.
    /// Uses SQL Server via Microsoft.Data.SqlClient with async I/O for optimal performance.
    /// </summary>
    public class QueryExecutor
    {
        private readonly int _commandTimeout;
        private readonly int _maxRows;
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly QueryStore _queryStore;
        private readonly AuditLogService? _auditLog;
        private readonly ServerConnectionManager _connectionManager;

        public QueryExecutor(IDbConnectionFactory connectionFactory, QueryStore queryStore, IConfiguration configuration, ServerConnectionManager connectionManager, AuditLogService? auditLog = null)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _queryStore = queryStore ?? throw new ArgumentNullException(nameof(queryStore));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _auditLog = auditLog;
            
            _commandTimeout = configuration.GetValue<int>("QueryTimeoutSeconds", 60);
            _maxRows = configuration.GetValue<int>("MaxQueryRows", 10000);
        }

        /// <summary>
        /// Executes a query by ID with the given filter parameters and returns a DataTable.
        /// Uses native async I/O - no Task.Run() wrapper needed for I/O-bound operations.
        /// </summary>
        public async Task<DataTable> ExecuteQueryAsync(
            string queryId,
            DashboardFilter filter,
            Dictionary<string, object>? additionalParams = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var useLiveQueries = _connectionManager.CurrentServer?.HasSqlWatch == false;
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType, useLiveQueries);
            var dt = new DataTable();
            var startTime = DateTime.UtcNow;

            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _commandTimeout;
            cmd.Connection = conn;

            AddFilterParameters(cmd, filter);

            if (additionalParams != null)
            {
                foreach (var kvp in additionalParams)
                {
                    AddParameter(cmd, kvp.Key, kvp.Value);
                }
            }

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            int rowCount = 0;
            dt.BeginLoadData();

            var schemaTable = reader.GetSchemaTable();
            if (schemaTable != null)
            {
                foreach (DataRow schemaRow in schemaTable.Rows)
                {
                    dt.Columns.Add(schemaRow["ColumnName"].ToString(), (Type)schemaRow["DataType"]);
                }
            }

            // Rent a reusable buffer for column values to avoid per-row array allocations.
            var fieldCount = reader.FieldCount;
            var pool = ArrayPool<object>.Shared;
            var buffer = pool.Rent(fieldCount);
            try
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (++rowCount > _maxRows)
                    {
                        dt.EndLoadData();
                        _auditLog?.LogQueryExecution(queryId, (_connectionFactory as SqlServerConnectionFactory)?.ServerName ?? "unknown", false, DateTime.UtcNow - startTime, rowCount, $"Exceeded max rows ({_maxRows})");
                        throw new InvalidOperationException(
                            $"Query '{queryId}' returned more than {_maxRows} rows. Consider adding filters or pagination.");
                    }

                    reader.GetValues(buffer);
                    var row = dt.NewRow();
                    for (int i = 0; i < fieldCount; i++)
                    {
                        row[i] = buffer[i] is DBNull ? DBNull.Value : buffer[i];
                    }
                    dt.Rows.Add(row);
                }
            }
            finally
            {
                pool.Return(buffer, clearArray: true);
            }

            dt.EndLoadData();
            _auditLog?.LogQueryExecution(queryId, (_connectionFactory as SqlServerConnectionFactory)?.ServerName ?? "unknown", true, DateTime.UtcNow - startTime, rowCount);
            return dt;
        }

        /// <summary>
        /// Executes a query by ID and maps each row to a typed object using the provided mapper function.
        /// </summary>
        public async Task<List<T>> ExecuteQueryAsync<T>(
            string queryId,
            DashboardFilter filter,
            Func<IDataReader, T> mapper,
            Dictionary<string, object>? additionalParams = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var useLiveQueries = _connectionManager.CurrentServer?.HasSqlWatch == false;
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType, useLiveQueries);
            var results = new List<T>();

            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _commandTimeout;
            cmd.Connection = conn;

            AddFilterParameters(cmd, filter);

            if (additionalParams != null)
            {
                foreach (var kvp in additionalParams)
                {
                    AddParameter(cmd, kvp.Key, kvp.Value);
                }
            }

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(mapper(reader));
            }

            return results;
        }

        /// <summary>
        /// Executes a query and returns a single scalar value.
        /// </summary>
        public async Task<T?> ExecuteScalarAsync<T>(
            string queryId,
            DashboardFilter filter,
            Dictionary<string, object>? additionalParams = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var useLiveQueries = _connectionManager.CurrentServer?.HasSqlWatch == false;
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType, useLiveQueries);

            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _commandTimeout;
            cmd.Connection = conn;

            AddFilterParameters(cmd, filter);

            if (additionalParams != null)
            {
                foreach (var kvp in additionalParams)
                {
                    AddParameter(cmd, kvp.Key, kvp.Value);
                }
            }

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Adds the standard dashboard filter parameters to the command.
        /// @SqlInstance is a comma-separated string used with STRING_SPLIT in SQL Server queries.
        /// </summary>
        private void AddFilterParameters(IDbCommand cmd, DashboardFilter filter)
        {
            AddParameter(cmd, "@TimeFrom", filter.TimeFrom.ToString("yyyy-MM-dd HH:mm:ss"));
            AddParameter(cmd, "@TimeTo", filter.TimeTo.ToString("yyyy-MM-dd HH:mm:ss"));

            // SQL Server queries use STRING_SPLIT(@SqlInstance, ',')
            var joined = filter.Instances.Length > 0 ? string.Join(",", filter.Instances) : "";
            AddParameter(cmd, "@SqlInstance", joined);

            AddParameter(cmd, "@AggMin", filter.AggregationMinutes);
        }

        /// <summary>
        /// Adds a parameter to the command in a provider-agnostic way.
        /// </summary>
        private static void AddParameter(IDbCommand cmd, string name, object value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }
    }
}
