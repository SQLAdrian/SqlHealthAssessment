using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Executes parameterized queries from the QueryStore against the configured database.
    /// Uses SQL Server via Microsoft.Data.SqlClient with async I/O for optimal performance.
    /// </summary>
    public class QueryExecutor
    {
        // Default command timeout: 2 minutes for long-running queries
        private const int DefaultCommandTimeout = 120;

        private readonly IDbConnectionFactory _connectionFactory;
        private readonly QueryStore _queryStore;

        public QueryExecutor(IDbConnectionFactory connectionFactory, QueryStore queryStore)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _queryStore = queryStore ?? throw new ArgumentNullException(nameof(queryStore));
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
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType);
            var dt = new DataTable();

            // Use direct async - I/O operations don't need Task.Run() wrapper
            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = DefaultCommandTimeout;
            cmd.Connection = conn;

            AddFilterParameters(cmd, filter);

            if (additionalParams != null)
            {
                foreach (var kvp in additionalParams)
                {
                    AddParameter(cmd, kvp.Key, kvp.Value);
                }
            }

            // Use async reader for non-blocking I/O
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            dt.Load(reader); // DataTable.Load is synchronous but fast

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
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType);
            var results = new List<T>();

            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = DefaultCommandTimeout;
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
            var sql = _queryStore.GetQuery(queryId, _connectionFactory.DataSourceType);

            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            using var cmd = (SqlCommand)conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = DefaultCommandTimeout;
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
