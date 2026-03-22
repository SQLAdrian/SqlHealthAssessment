/* In the name of God, the Merciful, the Compassionate */

using System.Text.Json.Serialization;

namespace SqlHealthAssessment.Data.Models
{
    /// <summary>
    /// Root configuration for all outbound notification channels.
    /// Persisted to Config/notification-channels.json.
    /// </summary>
    public class NotificationChannelConfig
    {
        [JsonPropertyName("smtp")]
        public SmtpChannelConfig Smtp { get; set; } = new();

        [JsonPropertyName("teamsWebhook")]
        public TeamsWebhookChannelConfig TeamsWebhook { get; set; } = new();
    }

    public class SmtpChannelConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        [JsonPropertyName("port")]
        public int Port { get; set; } = 587;

        [JsonPropertyName("useTls")]
        public bool UseTls { get; set; } = true;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted via CredentialProtector. Never stored in plaintext.
        /// </summary>
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("fromAddress")]
        public string FromAddress { get; set; } = string.Empty;

        [JsonPropertyName("fromName")]
        public string FromName { get; set; } = "SQL Health Assessment";

        [JsonPropertyName("replyToAddress")]
        public string ReplyToAddress { get; set; } = string.Empty;

        [JsonPropertyName("toAddresses")]
        public List<string> ToAddresses { get; set; } = new();

        /// <summary>
        /// Minimum severity to send email: info, warning, critical
        /// </summary>
        [JsonPropertyName("minimumSeverity")]
        public string MinimumSeverity { get; set; } = "warning";
    }

    public class TeamsWebhookChannelConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Microsoft Teams Incoming Webhook URL. Encrypted via CredentialProtector.
        /// </summary>
        [JsonPropertyName("webhookUrl")]
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>
        /// Minimum severity to post to Teams: info, warning, critical
        /// </summary>
        [JsonPropertyName("minimumSeverity")]
        public string MinimumSeverity { get; set; } = "warning";
    }
}
