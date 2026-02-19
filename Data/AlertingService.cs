using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using SqlHealthAssessment.Data.Models;
using Microsoft.Extensions.Logging;

namespace SqlHealthAssessment.Data
{
    public class AlertingService
    {
        private readonly ILogger<AlertingService> _logger;
        private readonly string _alertsFilePath;
        private List<AlertThreshold> _thresholds = new();
        private readonly ConcurrentQueue<AlertNotification> _notifications = new();
        private readonly object _lock = new();
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Cooldown tracker: key = "{instanceName}:{metric}" → last alert time.
        /// Prevents the same metric on the same instance from firing more than once
        /// within the cooldown window (default 5 minutes).
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTime = new(
            StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);

        public AlertingService(ILogger<AlertingService> logger)
        {
            _logger = logger;
            _alertsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert-configurations.json");
            LoadThresholds();
        }

        public List<AlertThreshold> GetThresholds()
        {
            lock (_lock)
            {
                return _thresholds.ToList();
            }
        }

        public void AddThreshold(AlertThreshold threshold)
        {
            lock (_lock)
            {
                _thresholds.Add(threshold);
                SaveThresholds();
            }
        }

        public void UpdateThreshold(AlertThreshold threshold)
        {
            lock (_lock)
            {
                var index = _thresholds.FindIndex(t => t.Id == threshold.Id);
                if (index >= 0)
                {
                    _thresholds[index] = threshold;
                    SaveThresholds();
                }
            }
        }

        public void RemoveThreshold(string id)
        {
            lock (_lock)
            {
                _thresholds.RemoveAll(t => t.Id == id);
                SaveThresholds();
            }
        }

        public List<AlertNotification> GetNotifications(int maxCount = 50)
        {
            return _notifications.Take(maxCount).ToList();
        }

        public int GetUnacknowledgedCount()
        {
            return _notifications.Count(n => !n.IsAcknowledged);
        }

        public void AcknowledgeNotification(string id)
        {
            // For simplicity, we'll just clear old notifications
            // In a real app, you'd track them by ID
            ClearNotifications();
        }

        public void ClearNotifications()
        {
            while (_notifications.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Evaluates alert thresholds against the supplied metrics (backward-compatible, no instance context).
        /// </summary>
        public List<AlertEvaluationResult> EvaluateAlerts(Dictionary<string, double> metrics)
            => EvaluateAlerts(metrics, instanceName: string.Empty);

        /// <summary>
        /// Evaluates alert thresholds against the supplied metrics for a specific instance.
        /// Applies a per-metric, per-instance cooldown to avoid duplicate notifications.
        /// </summary>
        public List<AlertEvaluationResult> EvaluateAlerts(
            Dictionary<string, double> metrics, string instanceName)
        {
            var results = new List<AlertEvaluationResult>();

            lock (_lock)
            {
                foreach (var threshold in _thresholds.Where(t => t.Enabled))
                {
                    if (metrics.TryGetValue(threshold.Metric.ToLower(), out var currentValue))
                    {
                        var isTriggered = threshold.Condition switch
                        {
                            "greater_than" => currentValue > threshold.ThresholdValue,
                            "less_than" => currentValue < threshold.ThresholdValue,
                            "equals" => Math.Abs(currentValue - threshold.ThresholdValue) < 0.01,
                            _ => false
                        };

                        if (isTriggered)
                        {
                            var result = new AlertEvaluationResult
                            {
                                IsTriggered = true,
                                AlertId = threshold.Id,
                                AlertName = threshold.Name,
                                CurrentValue = currentValue,
                                ThresholdValue = threshold.ThresholdValue,
                                Severity = threshold.Severity,
                                InstanceName = instanceName,
                                Message = string.IsNullOrEmpty(instanceName)
                                    ? $"{threshold.Name}: {currentValue} {threshold.Condition.Replace("_", " ")} {threshold.ThresholdValue}"
                                    : $"[{instanceName}] {threshold.Name}: {currentValue} {threshold.Condition.Replace("_", " ")} {threshold.ThresholdValue}"
                            };
                            results.Add(result);

                            // Cooldown check: skip notification if same metric+instance fired recently
                            var cooldownKey = $"{instanceName}:{threshold.Metric}".ToLower();
                            if (_lastAlertTime.TryGetValue(cooldownKey, out var lastTime)
                                && (DateTime.UtcNow - lastTime) < AlertCooldown)
                            {
                                continue; // still in cooldown — skip notification, keep evaluation result
                            }

                            _lastAlertTime[cooldownKey] = DateTime.UtcNow;

                            var notification = new AlertNotification
                            {
                                AlertName = threshold.Name,
                                Metric = threshold.Metric,
                                CurrentValue = currentValue,
                                ThresholdValue = threshold.ThresholdValue,
                                Severity = threshold.Severity,
                                InstanceName = instanceName,
                                Message = result.Message
                            };
                            _notifications.Enqueue(notification);

                            // Keep only last 100 notifications
                            while (_notifications.Count > 100)
                            {
                                _notifications.TryDequeue(out _);
                            }
                        }
                    }
                }
            }

            if (results.Any(r => r.Severity == "critical"))
            {
                _logger.LogWarning("CRITICAL ALERTS TRIGGERED: {Count}", results.Count(r => r.Severity == "critical"));
            }

            return results;
        }

        private void LoadThresholds()
        {
            try
            {
                if (File.Exists(_alertsFilePath))
                {
                    var json = File.ReadAllText(_alertsFilePath);
                    _thresholds = JsonSerializer.Deserialize<List<AlertThreshold>>(json) ?? new List<AlertThreshold>();
                    _logger.LogInformation("Loaded {Count} alert thresholds", _thresholds.Count);
                }
                else
                {
                    // Create default thresholds
                    _thresholds = GetDefaultThresholds();
                    SaveThresholds();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading alert thresholds");
                _thresholds = new List<AlertThreshold>();
            }
        }

        private void SaveThresholds()
        {
            try
            {
                var json = JsonSerializer.Serialize(_thresholds, _jsonOptions);
                File.WriteAllText(_alertsFilePath, json);
                _logger.LogInformation("Saved {Count} alert thresholds", _thresholds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving alert thresholds");
            }
        }

        private List<AlertThreshold> GetDefaultThresholds()
        {
            return new List<AlertThreshold>
            {
                new AlertThreshold
                {
                    Name = "High CPU Usage",
                    Metric = "cpu",
                    Condition = "greater_than",
                    ThresholdValue = 80,
                    Enabled = true,
                    Severity = "warning",
                    Description = "Alert when CPU usage exceeds 80%"
                },
                new AlertThreshold
                {
                    Name = "Critical CPU Usage",
                    Metric = "cpu",
                    Condition = "greater_than",
                    ThresholdValue = 95,
                    Enabled = true,
                    Severity = "critical",
                    Description = "Alert when CPU usage exceeds 95%"
                },
                new AlertThreshold
                {
                    Name = "High Memory Usage",
                    Metric = "memory",
                    Condition = "greater_than",
                    ThresholdValue = 85,
                    Enabled = true,
                    Severity = "warning",
                    Description = "Alert when memory usage exceeds 85%"
                },
                new AlertThreshold
                {
                    Name = "High Connection Count",
                    Metric = "connections",
                    Condition = "greater_than",
                    ThresholdValue = 100,
                    Enabled = true,
                    Severity = "warning",
                    Description = "Alert when connection count exceeds 100"
                },
                new AlertThreshold
                {
                    Name = "Deadlock Detected",
                    Metric = "deadlocks",
                    Condition = "greater_than",
                    ThresholdValue = 0,
                    Enabled = true,
                    Severity = "critical",
                    Description = "Alert when any deadlock is detected"
                }
            };
        }
    }
}
