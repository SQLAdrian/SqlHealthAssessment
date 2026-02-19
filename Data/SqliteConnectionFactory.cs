using System.Data;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Connection factory for SQLite databases.
    /// Not used by the caching layer directly (SqliteCacheStore manages its own connections),
    /// but available for the existing QueryPair.Sqlite query path if needed.
    /// </summary>
    public class SqliteConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection() => new SqliteConnection(_connectionString);
        public string DataSourceType => "Sqlite";

        public async Task<IDbConnection> CreateConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
