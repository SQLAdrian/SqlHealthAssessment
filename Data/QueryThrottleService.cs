/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SQLTriage.Data
{
    /// <summary>
    /// Throttles concurrent SQL query execution to prevent connection pool exhaustion.
    /// Uses two tiers: heavy queries (TimeSeries — full table scans) are limited to
    /// <see cref="HeavyLimit"/> slots; lightweight queries (StatCard, BarGauge,
    /// CheckStatus, DataGrid) share a separate higher-concurrency pool.
    /// Limits are configurable via UserSettings and can be updated at runtime.
    /// </summary>
    public class QueryThrottleService : IDisposable
    {
        private int _heavyLimit;
        private int _lightLimit;

        private SemaphoreSlim? _heavySemaphore;
        private SemaphoreSlim? _lightSemaphore;
        private bool _disposed;

        /// <summary>Maximum concurrent heavy queries (TimeSeries). Default 5.</summary>
        public int HeavyLimit => _heavyLimit;

        /// <summary>Maximum concurrent light queries (StatCard, BarGauge, etc.). Default 10.</summary>
        public int LightLimit => _lightLimit;

        /// <summary>
        /// Creates a new QueryThrottleService with settings from UserSettings.
        /// </summary>
        public QueryThrottleService(UserSettingsService userSettings)
        {
            if (userSettings == null) throw new ArgumentNullException(nameof(userSettings));
            _heavyLimit = userSettings.GetMaxHeavyConcurrent();
            _lightLimit = userSettings.GetMaxLightConcurrent();
            _heavySemaphore = new SemaphoreSlim(_heavyLimit, _heavyLimit);
            _lightSemaphore = new SemaphoreSlim(_lightLimit, _lightLimit);
        }

        /// <summary>
        /// Updates concurrency limits at runtime. Creates new semaphores with the
        /// specified limits and swaps them atomically. Existing operations continue
        /// using the old semaphores; new operations use the new ones.
        /// Note: Old semaphores are not disposed (to avoid race conditions) and
        /// will be garbage-collected once no longer referenced.
        /// </summary>
        public void UpdateLimits(int heavyLimit, int lightLimit)
        {
            if (heavyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(heavyLimit));
            if (lightLimit <= 0) throw new ArgumentOutOfRangeException(nameof(lightLimit));

            var newHeavy = new SemaphoreSlim(heavyLimit, heavyLimit);
            var newLight = new SemaphoreSlim(lightLimit, lightLimit);

            Interlocked.Exchange(ref _heavySemaphore, newHeavy);
            Interlocked.Exchange(ref _lightSemaphore, newLight);
            _heavyLimit = heavyLimit;
            _lightLimit = lightLimit;
        }

        /// <summary>
        /// Executes an async operation with throttling.
        /// Pass <paramref name="isHeavy"/> = true for TimeSeries panels,
        /// false (default) for StatCard, BarGauge, CheckStatus, and DataGrid.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, bool isHeavy, CancellationToken cancellationToken = default)
        {
            var sem = isHeavy ? _heavySemaphore : _lightSemaphore;
            if (sem == null) throw new ObjectDisposedException(nameof(QueryThrottleService));
            await sem.WaitAsync(cancellationToken);
            try
            {
                return await operation();
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Overload that treats the query as lightweight (non-TimeSeries).
        /// Kept for backward compatibility with call sites that don't specify panel type.
        /// </summary>
        public Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
            => ExecuteAsync(operation, isHeavy: false, cancellationToken);

        public void Dispose()
        {
            if (!_disposed)
            {
                _heavySemaphore?.Dispose();
                _lightSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
