/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Real-time session data service that queries sys.dm_exec_sessions and
    /// sys.dm_exec_requests directly against the currently selected SQL Server.
    /// This bypasses CachingQueryExecutor — sessions are always live, never cached.
    /// </summary>
    public class SessionDataService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        private const string LiveSessionsQuery = @"
            SELECT
                s.session_id AS SPID,
                s.login_name AS LoginName,
                ISNULL(s.host_name, '') AS HostName,
                ISNULL(DB_NAME(s.database_id), '') AS DatabaseName,
                s.status AS SessionStatus,
                s.cpu_time AS CpuTime,
                s.reads AS LogicalReads,
                s.writes AS Writes,
                s.open_transaction_count AS OpenTransactionCount,
                r.status AS RequestStatus,
                r.command AS Command,
                r.wait_type AS WaitType,
                ISNULL(r.wait_time, 0) AS WaitTime,
                ISNULL(r.blocking_session_id, 0) AS BlockingSessionId,
                ISNULL(r.total_elapsed_time, 0) AS TotalElapsedTime,
                ISNULL(s.program_name, '') AS ProgramName,
                t.text AS QueryText
            FROM sys.dm_exec_sessions s WITH (NOLOCK)
            LEFT JOIN sys.dm_exec_requests r WITH (NOLOCK)
                ON s.session_id = r.session_id
            OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t
            WHERE s.is_user_process = 1
            ORDER BY s.cpu_time + s.reads DESC";

        public SessionDataService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Fetches all live user sessions from the currently connected SQL Server instance.
        /// </summary>
        public async Task<List<SessionInfo>> GetLiveSessionsAsync()
        {
            var sessions = new List<SessionInfo>();

            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = ((SqlConnection)conn).CreateCommand();
            cmd.CommandText = LiveSessionsQuery;
            cmd.CommandTimeout = 30;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sessions.Add(new SessionInfo
                {
                    SPID = reader.GetInt32(reader.GetOrdinal("SPID")),
                    LoginName = reader.GetString(reader.GetOrdinal("LoginName")),
                    HostName = reader.GetString(reader.GetOrdinal("HostName")),
                    DatabaseName = reader.GetString(reader.GetOrdinal("DatabaseName")),
                    SessionStatus = reader.GetString(reader.GetOrdinal("SessionStatus")),
                    CpuTime = reader.GetInt32(reader.GetOrdinal("CpuTime")),
                    LogicalReads = reader.GetInt64(reader.GetOrdinal("LogicalReads")),
                    Writes = reader.GetInt64(reader.GetOrdinal("Writes")),
                    OpenTransactionCount = reader.GetInt32(reader.GetOrdinal("OpenTransactionCount")),
                    RequestStatus = reader.IsDBNull(reader.GetOrdinal("RequestStatus")) ? null : reader.GetString(reader.GetOrdinal("RequestStatus")),
                    Command = reader.IsDBNull(reader.GetOrdinal("Command")) ? null : reader.GetString(reader.GetOrdinal("Command")),
                    WaitType = reader.IsDBNull(reader.GetOrdinal("WaitType")) ? null : reader.GetString(reader.GetOrdinal("WaitType")),
                    WaitTime = reader.GetInt32(reader.GetOrdinal("WaitTime")),
                    BlockingSessionId = reader.GetInt32(reader.GetOrdinal("BlockingSessionId")),
                    TotalElapsedTime = reader.GetInt32(reader.GetOrdinal("TotalElapsedTime")),
                    ProgramName = reader.GetString(reader.GetOrdinal("ProgramName")),
                    QueryText = reader.IsDBNull(reader.GetOrdinal("QueryText")) ? null : reader.GetString(reader.GetOrdinal("QueryText"))
                });
            }

            return sessions;
        }
    }
}
