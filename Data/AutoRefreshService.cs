/* In the name of God, the Merciful, the Compassionate */

namespace SqlHealthAssessment.Data
{
    public class AutoRefreshService : IDisposable
    {
        private Timer? _timer;
        private int _intervalMs;
        private bool _isRunning;
        private readonly object _lock = new();

        public event Action? OnRefresh;

        public bool IsRunning
        {
            get { lock (_lock) { return _isRunning; } }
        }

        public AutoRefreshService(Microsoft.Extensions.Configuration.IConfiguration config)
        {
            var seconds = int.TryParse(config["RefreshIntervalSeconds"], out var s) ? s : 5;
            _intervalMs = seconds * 1000;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
                _timer = new Timer(_ => OnRefresh?.Invoke(), null, _intervalMs, _intervalMs);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _timer?.Dispose();
                _timer = null;
            }
        }

        public void SetInterval(int seconds)
        {
            lock (_lock)
            {
                _intervalMs = seconds * 1000;
                if (_isRunning)
                {
                    // Atomically stop and restart within the same lock to prevent race conditions
                    _timer?.Dispose();
                    _timer = new Timer(_ => OnRefresh?.Invoke(), null, _intervalMs, _intervalMs);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
