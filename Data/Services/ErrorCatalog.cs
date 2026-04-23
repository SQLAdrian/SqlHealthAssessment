/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SQLTriage.Data.Models;

namespace SQLTriage.Data.Services
{
    /// <summary>
    /// Provides human-friendly error guidance loaded from Config/error-catalog.json.
    /// Supports runtime reload via FileSystemWatcher (500ms debounce) so operators
    /// can tune messages without restarting the application.
    /// </summary>
    public interface IErrorCatalog
    {
        /// <summary>Retrieve a single entry by its stable error code.</summary>
        ErrorCatalogEntry? Get(string errorCode);

        /// <summary>Get all entries in a category.</summary>
        IReadOnlyList<ErrorCatalogEntry> GetByCategory(string category);

        /// <summary>Search entries by keyword in UserMessage or Remediation.</summary>
        IReadOnlyList<ErrorCatalogEntry> Search(string keyword);

        /// <summary>Get the formatted message for a specific audience.</summary>
        string GetMessage(string errorCode, string audience = ErrorAudiences.Dba, params object?[] args);

        /// <summary>Reload catalog from disk.</summary>
        Task ReloadAsync();

        /// <summary>Total entries currently loaded.</summary>
        int Count { get; }
    }

    public sealed class ErrorCatalog : IErrorCatalog, IDisposable
    {
        private readonly ILogger<ErrorCatalog> _logger;
        private readonly string _catalogPath;
        private readonly ConcurrentDictionary<string, ErrorCatalogEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _reloadDebounceTimer;
        private bool _disposed;

        public int Count => _entries.Count;

        public ErrorCatalog(ILogger<ErrorCatalog> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var baseDir = AppContext.BaseDirectory;
            _catalogPath = Path.Combine(baseDir, configuration.GetValue("ErrorCatalog:Path", "Config/error-catalog.json") ?? "Config/error-catalog.json");

            // Initial load (fire-and-forget is safe; callers can await ReloadAsync if needed)
            _ = LoadAsync().ConfigureAwait(false);

            var configDir = Path.GetDirectoryName(_catalogPath)!;
            Directory.CreateDirectory(configDir);
            _watcher = new FileSystemWatcher(configDir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileChanged;

            _reloadDebounceTimer = new Timer(_ =>
            {
                _ = LoadAsync();
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.Equals(_catalogPath, StringComparison.OrdinalIgnoreCase)) return;
            _reloadDebounceTimer.Change(500, Timeout.Infinite);
        }

        public ErrorCatalogEntry? Get(string errorCode)
            => _entries.TryGetValue(errorCode, out var entry) ? entry : null;

        public IReadOnlyList<ErrorCatalogEntry> GetByCategory(string category)
            => _entries.Values.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        public IReadOnlyList<ErrorCatalogEntry> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<ErrorCatalogEntry>();
            var k = keyword.Trim();
            return _entries.Values.Where(e =>
                e.ErrorCode.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                e.UserMessage.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                e.Remediation.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                e.GovernanceImpact.Contains(k, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        public string GetMessage(string errorCode, string audience = ErrorAudiences.Dba, params object?[] args)
        {
            if (!_entries.TryGetValue(errorCode, out var entry))
            {
                _logger.LogWarning("ErrorCatalog missing entry for {ErrorCode}", errorCode);
                return $"[{errorCode}] An error occurred.";
            }

            var template = entry.AudienceMessages.TryGetValue(audience, out var msg)
                ? msg
                : entry.UserMessage;

            try
            {
                return args.Length > 0 ? string.Format(template, args) : template;
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Format failed for {ErrorCode} with {ArgCount} args", errorCode, args.Length);
                return template;
            }
        }

        public Task ReloadAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_catalogPath))
                {
                    _logger.LogWarning("error-catalog.json not found at {Path}; no entries loaded", _catalogPath);
                    return;
                }

                var json = await File.ReadAllTextAsync(_catalogPath).ConfigureAwait(false);
                var wrapper = JsonSerializer.Deserialize<ErrorCatalogFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var entries = wrapper?.Entries;
                if (entries == null)
                {
                    _logger.LogWarning("error-catalog.json deserialized to null or missing 'entries'; keeping existing entries");
                    return;
                }

                var newDict = new Dictionary<string, ErrorCatalogEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in entries)
                {
                    if (string.IsNullOrWhiteSpace(e.ErrorCode))
                    {
                        _logger.LogWarning("Skipping catalog entry with missing ErrorCode");
                        continue;
                    }
                    newDict[e.ErrorCode] = e;
                }

                _entries.Clear();
                foreach (var kvp in newDict)
                    _entries[kvp.Key] = kvp.Value;

                _logger.LogInformation("Loaded {Count} error-catalog entries from {Path}", _entries.Count, _catalogPath);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Malformed error-catalog.json — keeping existing entry set");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load error-catalog.json");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _watcher?.Dispose();
                _reloadDebounceTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
