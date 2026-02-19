using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Rate limiter to prevent brute-force attacks.
    /// Limits to a maximum number of SQL queries per minute.
    /// </summary>
    public class RateLimiter
    {
        private readonly IConfiguration _configuration;
        
        /// <summary>
        /// Maximum queries per minute (default 50)
        /// </summary>
        public int MaxQueriesPerMinute { get; private set; } = 50;
        
        /// <summary>
        /// Current query count in the sliding window
        /// </summary>
        public int CurrentQueryCount { get; private set; }
        
        /// <summary>
        /// Whether the rate limit has been exceeded
        /// </summary>
        public bool IsRateLimited => CurrentQueryCount >= MaxQueriesPerMinute;
        
        /// <summary>
        /// Event raised when rate limit is exceeded
        /// </summary>
        public event EventHandler<RateLimitExceededEventArgs>? RateLimitExceeded;

        /// <summary>
        /// Queue of query timestamps for sliding window
        /// </summary>
        private readonly ConcurrentQueue<DateTime> _queryTimestamps = new();
        
        /// <summary>
        /// Lock for thread safety
        /// </summary>
        private readonly object _lock = new();

        public RateLimiter(IConfiguration configuration)
        {
            _configuration = configuration;
            LoadConfiguration();
            
            // Start cleanup timer
            var timer = new Timer(CleanupOldTimestamps, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private void LoadConfiguration()
        {
            MaxQueriesPerMinute = _configuration.GetValue<int>("RateLimiting:MaxQueriesPerMinute", 50);
        }

        /// <summary>
        /// Attempts to execute a query. Returns true if allowed, false if rate limited.
        /// </summary>
        public bool TryAcquire()
        {
            CleanupOldTimestamps(null);
            
            lock (_lock)
            {
                if (CurrentQueryCount >= MaxQueriesPerMinute)
                {
                    // Rate limit exceeded
                    RateLimitExceeded?.Invoke(this, new RateLimitExceededEventArgs
                    {
                        CurrentCount = CurrentQueryCount,
                        MaxAllowed = MaxQueriesPerMinute,
                        TimeToReset = GetTimeToReset()
                    });
                    return false;
                }
                
                // Record this query
                _queryTimestamps.Enqueue(DateTime.UtcNow);
                CurrentQueryCount = _queryTimestamps.Count;
                return true;
            }
        }

        /// <summary>
        /// Gets the remaining queries available in the current window
        /// </summary>
        public int GetRemainingQueries()
        {
            CleanupOldTimestamps(null);
            return Math.Max(0, MaxQueriesPerMinute - CurrentQueryCount);
        }

        /// <summary>
        /// Gets the time until the rate limit resets (oldest query expires)
        /// </summary>
        public TimeSpan GetTimeToReset()
        {
            if (_queryTimestamps.IsEmpty)
                return TimeSpan.Zero;
            
            if (_queryTimestamps.TryPeek(out var oldest))
            {
                var expires = oldest.AddMinutes(1);
                var remaining = expires - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Cleans up timestamps older than 1 minute (sliding window)
        /// </summary>
        private void CleanupOldTimestamps(object? state)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            
            while (_queryTimestamps.TryPeek(out var timestamp))
            {
                if (timestamp < cutoff)
                {
                    _queryTimestamps.TryDequeue(out _);
                }
                else
                {
                    break;
                }
            }
            
            CurrentQueryCount = _queryTimestamps.Count;
        }

        /// <summary>
        /// Resets the rate limiter (for testing or admin override)
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                while (_queryTimestamps.TryDequeue(out _)) { }
                CurrentQueryCount = 0;
            }
        }
    }

    public class RateLimitExceededEventArgs : EventArgs
    {
        public int CurrentCount { get; set; }
        public int MaxAllowed { get; set; }
        public TimeSpan TimeToReset { get; set; }
    }
}
