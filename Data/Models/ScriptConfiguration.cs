/* In the name of God, the Merciful, the Compassionate */

using System.Text.Json.Serialization;

namespace SqlHealthAssessment.Data.Models
{
    public class ScriptConfiguration
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("ScriptPath")]
        public string ScriptPath { get; set; } = string.Empty;

        [JsonPropertyName("ExecutionParameters")]
        public string ExecutionParameters { get; set; } = string.Empty;

        [JsonPropertyName("SqlQueryForOutput")]
        public string SqlQueryForOutput { get; set; } = string.Empty;

        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("TimeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 300;

        [JsonPropertyName("Category")]
        public string Category { get; set; } = "Diagnostic";

        [JsonPropertyName("ExecutionOrder")]
        public int ExecutionOrder { get; set; }

        [JsonPropertyName("ExportToCsv")]
        public bool ExportToCsv { get; set; }
    }

    public class ScriptExecutionResult
    {
        public string ScriptName { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int RowsAffected { get; set; }
        public List<Dictionary<string, object>>? Results { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}
