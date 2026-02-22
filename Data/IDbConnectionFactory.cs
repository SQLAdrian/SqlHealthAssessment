/* In the name of God, the Merciful, the Compassionate */

using System.Data;
using System.Threading.Tasks;

namespace SqlHealthAssessment.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
        string DataSourceType { get; } // "SqlServer" or "Sqlite"
        
        /// <summary>
        /// Creates and opens a new connection asynchronously.
        /// </summary>
        Task<IDbConnection> CreateConnectionAsync();
    }
}
