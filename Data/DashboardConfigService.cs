/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Loads, saves, and queries dashboard configuration from a JSON file.
    /// Falls back to DefaultConfigGenerator when the file is missing or corrupt.
    /// Uses an O(1) dictionary cache for query lookups instead of scanning all panels.
    /// </summary>
    public class DashboardConfigService
    {
        private readonly string _configPath;
        private readonly string _backupPath;
        private DashboardConfigRoot _config;

        /// <summary>
        /// O(1) lookup cache mapping queryId -> QueryPair.
        /// Rebuilt on Load(), Save(), and ResetToDefault().
        /// </summary>
        private Dictionary<string, QueryPair> _queryCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// O(1) lookup cache mapping queryId -> PanelType string.
        /// Avoids O(dashboards × panels) scan on every delta-fetch call.
        /// </summary>
        private Dictionary<string, string> _panelTypeCache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Event raised after the configuration has been saved and should be re-rendered.
        /// </summary>
        public event Action? OnConfigChanged;

        /// <summary>
        /// The currently loaded dashboard configuration.
        /// </summary>
        public DashboardConfigRoot Config => _config;

        /// <summary>
        /// Updates the in-memory configuration, saves to disk, and notifies subscribers of the change.
        /// </summary>
        public void UpdateConfig(DashboardConfigRoot newConfig)
        {
            _config = newConfig;
            RebuildQueryCache(_config);
            Save();
            NotifyChanged();
        }

        public DashboardConfigService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard-config.json");
            _backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard-config.backup.json");
            _config = Load();
        }

        /// <summary>
        /// Loads configuration from disk. If the file does not exist or deserialization fails,
        /// generates defaults, persists them, and returns the default configuration.
        /// </summary>
        private DashboardConfigRoot Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<DashboardConfigRoot>(json, SerializerOptions);
                    if (config != null)
                    {
                        _config = config;
                        if (PatchMissingDashboards(config))
                        {
                            Save();
                        }
                        RebuildQueryCache(config);
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    // Enterprise Polish: Backup the corrupt file for analysis instead of just swallowing the error
                    var corruptPath = _configPath + $".corrupt.{DateTime.Now:yyyyMMddHHmmss}.json";
                    try { File.Copy(_configPath, corruptPath, true); } catch { /* Best effort */ }

                    System.Diagnostics.Debug.WriteLine(
                        $"[DashboardConfigService] Error deserializing config from '{_configPath}': {ex.Message}. " +
                        $"Corrupt file backed up to '{corruptPath}'. Falling back to defaults.");
                }
            }

            var defaultConfig = DefaultConfigGenerator.Generate();
            _config = defaultConfig;
            RebuildQueryCache(defaultConfig);
            Save();
            return defaultConfig;
        }

        /// <summary>
        /// Ensures that dashboards defined in the default configuration exist in the loaded configuration.
        /// Returns true if any dashboards were added.
        /// </summary>
        private bool PatchMissingDashboards(DashboardConfigRoot loadedConfig)
        {
            var defaultConfig = DefaultConfigGenerator.Generate();
            bool modified = false;

            if (loadedConfig.Dashboards == null) loadedConfig.Dashboards = new List<DashboardDefinition>();
            if (loadedConfig.SupportQueries == null) loadedConfig.SupportQueries = new Dictionary<string, QueryPair>();

            foreach (var defaultDashboard in defaultConfig.Dashboards)
            {
                if (!loadedConfig.Dashboards.Any(d => string.Equals(d.Id, defaultDashboard.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    loadedConfig.Dashboards.Add(defaultDashboard);
                    modified = true;
                }
            }
            return modified;
        }

        /// <summary>
        /// Builds the O(1) query lookup cache from panel definitions and support queries.
        /// Called once on load and after each save/reset.
        /// </summary>
        private void RebuildQueryCache(DashboardConfigRoot config)
        {
            var cache = new Dictionary<string, QueryPair>(StringComparer.OrdinalIgnoreCase);
            var typeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Index all panels across all dashboards
            foreach (var dashboard in config.Dashboards)
            {
                foreach (var panel in dashboard.Panels)
                {
                    if (!string.IsNullOrEmpty(panel.Id))
                    {
                        cache[panel.Id] = panel.Query;
                        typeCache[panel.Id] = panel.PanelType;
                    }
                }
            }

            // Index all support queries (support queries override panels if there's a collision)
            foreach (var kvp in config.SupportQueries)
            {
                cache[kvp.Key] = kvp.Value;
            }

            _queryCache = cache;
            _panelTypeCache = typeCache;
        }

        /// <summary>
        /// Persists the current configuration to disk. Creates a backup of the previous file first.
        /// </summary>
        public void Save()
        {
            if (File.Exists(_configPath))
            {
                File.Copy(_configPath, _backupPath, overwrite: true);
            }

            var json = JsonSerializer.Serialize(_config, SerializerOptions);
            File.WriteAllText(_configPath, json);

            // Rebuild cache after save in case config was mutated
            RebuildQueryCache(_config);
        }

        /// <summary>
        /// Fires the <see cref="OnConfigChanged"/> event so subscribers (dashboards) can re-render.
        /// </summary>
        public void NotifyChanged()
        {
            OnConfigChanged?.Invoke();
        }

        /// <summary>
        /// Saves the current configuration and notifies subscribers of the change.
        /// </summary>
        public void SaveAndNotify()
        {
            Save();
            NotifyChanged();
        }

        /// <summary>
        /// Replaces the current configuration with the default, saves it, and notifies subscribers.
        /// </summary>
        public void ResetToDefault()
        {
            _config = DefaultConfigGenerator.Generate();
            RebuildQueryCache(_config);
            Save();
            NotifyChanged();
        }

        /// <summary>
        /// Resolves the SQL query text for a given query identifier and data source type.
        /// Uses O(1) dictionary lookup instead of scanning all panels.
        /// </summary>
        /// <param name="queryId">The query/panel identifier (e.g., "repo.perf_counters").</param>
        /// <param name="dataSourceType">"SqlServer" or "Sqlite".</param>
        /// <returns>The SQL query string for the specified data source.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the query ID is not found.</exception>
        public string GetQuery(string queryId, string dataSourceType)
        {
            if (_queryCache.TryGetValue(queryId, out var queryPair))
            {
                return dataSourceType == "LiveQueries" ? queryPair.LiveQueries : queryPair.SqlServer;
            }

            throw new KeyNotFoundException($"Query '{queryId}' not found in dashboard configuration.");
        }

        /// <summary>
        /// Returns true if the query ID exists in the cache.
        /// </summary>
        /// <param name="queryId">The query/panel identifier to look up.</param>
        public bool HasQuery(string queryId)
        {
            return _queryCache.ContainsKey(queryId);
        }

        /// <summary>
        /// O(1) lookup of a panel's type string by query ID.
        /// Returns "Unknown" if the query ID is not found.
        /// </summary>
        public string GetPanelType(string queryId)
        {
            return _panelTypeCache.TryGetValue(queryId, out var type) ? type : "Unknown";
        }
    }
}
