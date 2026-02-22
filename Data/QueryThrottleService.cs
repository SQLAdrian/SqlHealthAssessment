/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Throttles concurrent SQL query execution to prevent connection pool exhaustion.
    /// Limits to a maximum of 5 concurrent queries by default.
    /// </summary>
    public class QueryThrottleService : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public QueryThrottleService(int maxConcurrentQueries = 5)
        {
            _semaphore = new SemaphoreSlim(maxConcurrentQueries, maxConcurrentQueries);
        }

        /// <summary>
        /// Executes an async operation with throttling.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return await operation();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
