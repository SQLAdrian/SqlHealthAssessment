/* In the name of God, the Merciful, the Compassionate */

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data.Services
{
    /// <summary>
    /// Dispatches alert notifications via configured outbound channels (SMTP email, Teams webhook).
    /// Configuration is persisted to Config/notification-channels.json with credentials encrypted
    /// via CredentialProtector.
    /// </summary>
    public class NotificationChannelService
    {
        private readonly ILogger<NotificationChannelService> _logger;
        private readonly string _configFilePath;
        private readonly object _lock = new();
        private NotificationChannelConfig _config = new();
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public NotificationChannelConfig Config
        {
            get { lock (_lock) return _config; }
        }

        public event Action? OnConfigChanged;

        public NotificationChannelService(ILogger<NotificationChannelService> logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(AppContext.BaseDirectory, "Config", "notification-channels.json");
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            LoadConfig();
        }

        // ──────────────── Configuration ────────────────

        public void UpdateConfig(NotificationChannelConfig config)
        {
            lock (_lock)
            {
                _config = config;
                SaveConfig();
            }
            OnConfigChanged?.Invoke();
        }

        private void LoadConfig()
        {
            try
            {
                _config = ConfigFileHelper.Load<NotificationChannelConfig>(_configFilePath);
                _logger.LogInformation("Loaded notification channel config: SMTP={SmtpEnabled}, Teams={TeamsEnabled}",
                    _config.Smtp.Enabled, _config.TeamsWebhook.Enabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load notification channel config");
                _config = new();
            }
        }

        private void SaveConfig()
        {
            try
            {
                // Defense-in-depth: ensure credentials are encrypted before persisting
                if (!string.IsNullOrEmpty(_config.Smtp.Password) && !CredentialProtector.IsEncrypted(_config.Smtp.Password))
                    _config.Smtp.Password = CredentialProtector.Encrypt(_config.Smtp.Password);

                if (!string.IsNullOrEmpty(_config.TeamsWebhook.WebhookUrl) && !CredentialProtector.IsEncrypted(_config.TeamsWebhook.WebhookUrl))
                    _config.TeamsWebhook.WebhookUrl = CredentialProtector.Encrypt(_config.TeamsWebhook.WebhookUrl);

                ConfigFileHelper.Save(_configFilePath, _config, _jsonOptions);
                _logger.LogInformation("Saved notification channel config");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save notification channel config");
            }
        }

        // ──────────────── Dispatch ────────────────

        /// <summary>
        /// Sends an alert notification to all enabled channels that meet the severity threshold.
        /// Fire-and-forget safe — errors are logged but never thrown.
        /// </summary>
        public async Task DispatchAsync(AlertNotification notification)
        {
            var tasks = new List<Task>();

            if (_config.Smtp.Enabled && MeetsSeverity(notification.Severity, _config.Smtp.MinimumSeverity))
            {
                tasks.Add(SendEmailAsync(notification));
            }

            if (_config.TeamsWebhook.Enabled && MeetsSeverity(notification.Severity, _config.TeamsWebhook.MinimumSeverity))
            {
                tasks.Add(SendTeamsAsync(notification));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        // ──────────────── SMTP Email ────────────────

        private async Task SendEmailAsync(AlertNotification notification)
        {
            try
            {
                var smtp = _config.Smtp;
                if (string.IsNullOrEmpty(smtp.Host) || smtp.ToAddresses.Count == 0)
                {
                    _logger.LogWarning("SMTP not fully configured — skipping email notification");
                    return;
                }

                var password = DecryptIfNeeded(smtp.Password);

                using var client = new SmtpClient(smtp.Host, smtp.Port)
                {
                    EnableSsl = smtp.UseTls,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                if (!string.IsNullOrEmpty(smtp.Username) && !string.IsNullOrEmpty(password))
                {
                    client.Credentials = new NetworkCredential(smtp.Username, password);
                }

                var from = new MailAddress(
                    string.IsNullOrEmpty(smtp.FromAddress) ? smtp.Username : smtp.FromAddress,
                    smtp.FromName);

                using var message = new MailMessage
                {
                    From = from,
                    Subject = $"[{notification.Severity.ToUpper()}] {notification.AlertName}",
                    IsBodyHtml = true,
                    Body = BuildEmailBody(notification)
                };

                foreach (var to in smtp.ToAddresses.Where(a => !string.IsNullOrWhiteSpace(a)))
                {
                    message.To.Add(to.Trim());
                }

                if (!string.IsNullOrWhiteSpace(smtp.ReplyToAddress))
                {
                    message.ReplyToList.Add(new MailAddress(smtp.ReplyToAddress.Trim()));
                }

                await client.SendMailAsync(message);
                _logger.LogInformation("Email alert sent: {AlertName} to {Recipients}",
                    notification.AlertName, string.Join(", ", smtp.ToAddresses));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email alert: {AlertName}", notification.AlertName);
            }
        }

        private static string BuildEmailBody(AlertNotification notification)
        {
            var severityColor = notification.Severity switch
            {
                "critical" => "#dc3545",
                "warning" => "#ffc107",
                _ => "#17a2b8"
            };

            return $@"
<div style=""font-family: 'Segoe UI', Tahoma, sans-serif; max-width: 600px; margin: 0 auto;"">
    <div style=""background: {severityColor}; color: white; padding: 12px 20px; border-radius: 6px 6px 0 0;"">
        <h2 style=""margin: 0; font-size: 18px;"">{WebUtility.HtmlEncode(notification.AlertName)}</h2>
    </div>
    <div style=""background: #1e1e2e; color: #e0e0e0; padding: 20px; border: 1px solid #333; border-radius: 0 0 6px 6px;"">
        <table style=""width: 100%; border-collapse: collapse;"">
            <tr><td style=""padding: 6px 0; color: #888;"">Severity</td><td style=""padding: 6px 0; font-weight: bold; color: {severityColor};"">{notification.Severity.ToUpper()}</td></tr>
            <tr><td style=""padding: 6px 0; color: #888;"">Metric</td><td style=""padding: 6px 0;"">{WebUtility.HtmlEncode(notification.Metric)}</td></tr>
            <tr><td style=""padding: 6px 0; color: #888;"">Current Value</td><td style=""padding: 6px 0; font-weight: bold;"">{notification.CurrentValue:N2}</td></tr>
            <tr><td style=""padding: 6px 0; color: #888;"">Threshold</td><td style=""padding: 6px 0;"">{notification.ThresholdValue:N2}</td></tr>
            {(string.IsNullOrEmpty(notification.InstanceName) ? "" : $@"<tr><td style=""padding: 6px 0; color: #888;"">Instance</td><td style=""padding: 6px 0;"">{WebUtility.HtmlEncode(notification.InstanceName)}</td></tr>")}
            <tr><td style=""padding: 6px 0; color: #888;"">Time (UTC)</td><td style=""padding: 6px 0;"">{notification.TriggeredAt:yyyy-MM-dd HH:mm:ss}</td></tr>
            <tr><td style=""padding: 6px 0; color: #888;"">Machine</td><td style=""padding: 6px 0;"">{Environment.MachineName}</td></tr>
        </table>
        <div style=""margin-top: 16px; padding: 10px; background: #0d1117; border-radius: 4px; font-size: 13px;"">
            {WebUtility.HtmlEncode(notification.Message)}
        </div>
        <p style=""margin-top: 16px; font-size: 11px; color: #666;"">Sent by SQL Health Assessment — {Environment.MachineName}</p>
    </div>
</div>";
        }

        /// <summary>
        /// Sends a test email to verify SMTP configuration.
        /// </summary>
        public async Task<(bool Success, string Message)> TestSmtpAsync()
        {
            var smtp = _config.Smtp;
            if (string.IsNullOrEmpty(smtp.Host))
                return (false, "SMTP host is not configured.");
            if (smtp.ToAddresses.Count == 0)
                return (false, "No recipient email addresses configured.");

            try
            {
                var testNotification = new AlertNotification
                {
                    AlertName = "SMTP Test",
                    Metric = "test",
                    CurrentValue = 0,
                    ThresholdValue = 0,
                    Severity = "info",
                    Message = "This is a test notification from SQL Health Assessment. If you received this email, SMTP is configured correctly.",
                    InstanceName = Environment.MachineName
                };

                await SendEmailAsync(testNotification);
                return (true, $"Test email sent to {string.Join(", ", smtp.ToAddresses)}");
            }
            catch (Exception ex)
            {
                return (false, $"SMTP test failed: {ex.Message}");
            }
        }

        // ──────────────── Teams Webhook ────────────────

        private async Task SendTeamsAsync(AlertNotification notification)
        {
            try
            {
                var webhookUrl = DecryptIfNeeded(_config.TeamsWebhook.WebhookUrl);
                if (string.IsNullOrEmpty(webhookUrl))
                {
                    _logger.LogWarning("Teams webhook URL not configured — skipping");
                    return;
                }

                var themeColor = notification.Severity switch
                {
                    "critical" => "dc3545",
                    "warning" => "ffc107",
                    _ => "17a2b8"
                };

                // Adaptive Card payload for Teams Incoming Webhook
                var payload = new
                {
                    type = "message",
                    attachments = new[]
                    {
                        new
                        {
                            contentType = "application/vnd.microsoft.card.adaptive",
                            contentUrl = (string?)null,
                            content = new
                            {
                                type = "AdaptiveCard",
                                body = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        size = "Medium",
                                        weight = "Bolder",
                                        text = $"\u26a0\ufe0f [{notification.Severity.ToUpper()}] {notification.AlertName}",
                                        color = notification.Severity == "critical" ? "Attention" : (notification.Severity == "warning" ? "Warning" : "Default")
                                    },
                                    new
                                    {
                                        type = "FactSet",
                                        facts = BuildTeamsFacts(notification)
                                    },
                                    new
                                    {
                                        type = "TextBlock",
                                        text = notification.Message,
                                        wrap = true,
                                        spacing = "Medium"
                                    }
                                },
                                schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                                version = "1.4"
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(webhookUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Teams alert sent: {AlertName}", notification.AlertName);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Teams webhook returned {StatusCode}: {Body}",
                        (int)response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Teams alert: {AlertName}", notification.AlertName);
            }
        }

        private static object[] BuildTeamsFacts(AlertNotification notification)
        {
            var facts = new List<object>
            {
                new { title = "Metric", value = notification.Metric },
                new { title = "Current Value", value = notification.CurrentValue.ToString("N2") },
                new { title = "Threshold", value = notification.ThresholdValue.ToString("N2") },
                new { title = "Severity", value = notification.Severity.ToUpper() },
                new { title = "Time (UTC)", value = notification.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss") },
                new { title = "Machine", value = Environment.MachineName }
            };

            if (!string.IsNullOrEmpty(notification.InstanceName))
                facts.Insert(0, new { title = "Instance", value = notification.InstanceName });

            return facts.ToArray();
        }

        /// <summary>
        /// Sends a test message to the configured Teams webhook.
        /// </summary>
        public async Task<(bool Success, string Message)> TestTeamsAsync()
        {
            var webhookUrl = DecryptIfNeeded(_config.TeamsWebhook.WebhookUrl);
            if (string.IsNullOrEmpty(webhookUrl))
                return (false, "Teams webhook URL is not configured.");

            try
            {
                var testNotification = new AlertNotification
                {
                    AlertName = "Teams Webhook Test",
                    Metric = "test",
                    CurrentValue = 0,
                    ThresholdValue = 0,
                    Severity = "info",
                    Message = "This is a test notification from SQL Health Assessment. If you see this message, Teams webhook is configured correctly.",
                    InstanceName = Environment.MachineName
                };

                await SendTeamsAsync(testNotification);
                return (true, "Test message sent to Teams channel.");
            }
            catch (Exception ex)
            {
                return (false, $"Teams webhook test failed: {ex.Message}");
            }
        }

        // ──────────────── Helpers ────────────────

        private static bool MeetsSeverity(string actual, string minimum)
        {
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["info"] = 0,
                ["warning"] = 1,
                ["critical"] = 2
            };

            if (!order.TryGetValue(actual, out var actualLevel)) actualLevel = 0;
            if (!order.TryGetValue(minimum, out var minLevel)) minLevel = 0;

            return actualLevel >= minLevel;
        }

        private static string DecryptIfNeeded(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (CredentialProtector.IsEncrypted(value))
                return CredentialProtector.Decrypt(value);
            return value;
        }
    }
}
