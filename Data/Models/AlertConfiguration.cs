/* In the name of God, the Merciful, the Compassionate */

using System.Text.Json.Serialization;

namespace SqlHealthAssessment.Data.Models
{
    public class AlertThreshold
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("metric")]
        public string Metric { get; set; } = string.Empty; // e.g., "cpu", "memory", "connections", "deadlocks"

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = "greater_than"; // greater_than, less_than, equals

        [JsonPropertyName("thresholdValue")]
        public double ThresholdValue { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "warning"; // info, warning, critical

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class AlertNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlertName { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.Now;
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// The instance this notification was triggered for (empty = global).
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;
    }

    public class AlertEvaluationResult
    {
        public bool IsTriggered { get; set; }
        public string AlertId { get; set; } = string.Empty;
        public string AlertName { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The instance this result relates to (empty = global).
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;
    }
}
