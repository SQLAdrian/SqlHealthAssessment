using System;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace SqlHealthAssessment.Data.Caching
{
    /// <summary>
    /// Runs periodic cache eviction on a timer (default: every 5 minutes).
    /// Removes rows from all SQLite cache tables where fetched_at is older
    /// than the configured threshold (default: 24 hours).
    /// </summary>
    public class CacheEvictionService : IDisposable
    {
        private readonly SqliteCacheStore _cache;
        private readonly TimeSpan _evictionThreshold;
        private readonly TimeSpan _interval;
        private Timer? _timer;
        private bool _disposed;

        public CacheEvictionService(SqliteCacheStore cache, IConfiguration config)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            var hours = 24;
            if (int.TryParse(config["CacheEvictionHours"], out var h) && h > 0)
                hours = h;
            _evictionThreshold = TimeSpan.FromHours(hours);

            // Run eviction every 5 minutes
            _interval = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Starts the periodic eviction timer.
        /// </summary>
        public void Start()
        {
            _timer = new Timer(OnTimerTick, null, _interval, _interval);
        }

        /// <summary>
        /// Stops the periodic eviction timer.
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
                await _cache.EvictOlderThanAsync(_evictionThreshold);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CacheEvictionService] Eviction error: {ex.Message}");
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
