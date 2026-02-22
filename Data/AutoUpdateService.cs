/* In the name of God, the Merciful, the Compassionate */

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SqlHealthAssessment.Data
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTime ReleasedAt { get; set; }
        public bool IsRequired { get; set; }
    }

    public class AutoUpdateService
    {
        private readonly ILogger<AutoUpdateService> _logger;
        private readonly HttpClient _httpClient;
        private string _currentVersion = "1.0.0";
        private int _buildNumber = 0;
        private string _updateCheckUrl = "https://api.github.com/repos/SQLAdrian/SqlHealthAssessment/releases/latest";

        public AutoUpdateService(ILogger<AutoUpdateService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SqlHealthAssessment");
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                var versionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                if (File.Exists(versionPath))
                {
                    var json = File.ReadAllText(versionPath);
                    var doc = JsonDocument.Parse(json);
                    _currentVersion = doc.RootElement.GetProperty("version").GetString() ?? "1.0.0";
                    if (doc.RootElement.TryGetProperty("buildNumber", out var buildNum))
                        _buildNumber = buildNum.GetInt32();
                    if (doc.RootElement.TryGetProperty("updateCheckUrl", out var url))
                        _updateCheckUrl = url.GetString() ?? _updateCheckUrl;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load version.json, using defaults");
            }
        }

        public async Task<(bool Available, UpdateInfo? Info)> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(_updateCheckUrl);
                var release = JsonDocument.Parse(response).RootElement;

                var latestVersion = release.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
                var downloadUrl = "";
                
                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe") || name.EndsWith(".zip"))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            break;
                        }
                    }
                }

                if (IsNewerVersion(latestVersion, _currentVersion))
                {
                    var updateInfo = new UpdateInfo
                    {
                        Version = latestVersion,
                        DownloadUrl = downloadUrl,
                        ReleaseNotes = release.GetProperty("body").GetString() ?? "",
                        ReleasedAt = release.GetProperty("published_at").GetDateTime()
                    };

                    _logger.LogInformation("Update available: {Version}", latestVersion);
                    return (true, updateInfo);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                return (false, null);
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            var latestParts = latest.Split('.');
            var currentParts = current.Split('.');

            for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (int.TryParse(latestParts[i], out var latestNum) && 
                    int.TryParse(currentParts[i], out var currentNum))
                {
                    if (latestNum > currentNum) return true;
                    if (latestNum < currentNum) return false;
                }
            }

            return latestParts.Length > currentParts.Length;
        }

        public string GetCurrentVersion() => _buildNumber > 0 ? $"{_currentVersion}.{_buildNumber}" : _currentVersion;
    }
}
