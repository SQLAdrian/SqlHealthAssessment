namespace SqlHealthAssessment.Data.Models
{
    /// <summary>
    /// Server diagnostic information
    /// </summary>
    public class ServerDiagnosticInfo
    {
        public string ServerName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public string ProductLevel { get; set; } = string.Empty;
        public int UserDatabaseCount { get; set; }
    }
}
