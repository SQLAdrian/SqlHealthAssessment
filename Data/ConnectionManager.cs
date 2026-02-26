/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    public class ServerConnectionManager
    {
        private readonly string _connectionsFilePath;
        private List<ServerConnection> _connections = new();
        private readonly object _lock = new();
        private string? _currentServerId;

        // Discovery cache — populated once after the first successful SQLWATCH scan.
        // Invalidated whenever connections are added, updated, or removed so that
        // the next dashboard load re-runs discovery and picks up the new topology.
        private bool _discoveryCompleted;
        private string[] _cachedInstances = Array.Empty<string>();
        private readonly Dictionary<string, string> _cachedInstanceToConnectionId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _cachedConnectionsWithSqlWatch = new();

        public event Action? OnConnectionChanged;

        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true
        };

        public ServerConnectionManager()
        {
            _connectionsFilePath = Path.Combine(AppContext.BaseDirectory, "server-connections.json");
            LoadConnections();
        }

        public List<ServerConnection> GetConnections()
        {
            lock (_lock) return _connections.ToList();
        }

        public List<ServerConnection> GetEnabledConnections()
        {
            lock (_lock) return _connections.Where(c => c.IsEnabled).ToList();
        }

        public string[] GetEnabledServerNames()
        {
            lock (_lock) return _connections.Where(c => c.IsEnabled).SelectMany(c => c.GetServerList()).ToArray();
        }

        public ServerConnection? GetConnection(string id)
        {
            lock (_lock) return _connections.FirstOrDefault(c => c.Id == id);
        }

        public ServerConnection? GetDefaultConnection()
        {
            lock (_lock) return _connections.FirstOrDefault();
        }

        public ServerConnection? CurrentServer
        {
            get
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(_currentServerId)) return null;
                    return _connections.FirstOrDefault(c => c.Id == _currentServerId);
                }
            }
        }

        public void SetCurrentServer(string? serverId)
        {
            lock (_lock)
            {
                _currentServerId = serverId;
                OnConnectionChanged?.Invoke();
            }
        }

        public void AddConnection(ServerConnection connection)
        {
            lock (_lock)
            {
                _connections.Add(connection);
                InvalidateDiscoveryCache();
                SaveConnections();
            }
        }

        public void UpdateConnection(ServerConnection connection)
        {
            lock (_lock)
            {
                var index = _connections.FindIndex(c => c.Id == connection.Id);
                if (index >= 0)
                {
                    _connections[index] = connection;
                    InvalidateDiscoveryCache();
                    SaveConnections();
                }
            }
        }

        public void RemoveConnection(string id)
        {
            lock (_lock)
            {
                _connections.RemoveAll(c => c.Id == id);
                InvalidateDiscoveryCache();
                SaveConnections();
            }
        }

        public void UpdateSuccessfulServers(string connectionId, List<string> successfulServers)
        {
            lock (_lock)
            {
                var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
                if (connection != null)
                {
                    connection.SuccessfulServers = successfulServers;
                    connection.LastConnected = DateTime.Now;
                    connection.IsConnected = successfulServers.Count > 0;
                    SaveConnections();
                }
            }
        }

        // ── Discovery cache ──────────────────────────────────────────────

        /// <summary>True after the first successful SQLWATCH instance discovery.</summary>
        public bool DiscoveryCompleted { get { lock (_lock) return _discoveryCompleted; } }

        /// <summary>
        /// Returns a snapshot of the discovery results for use by DynamicDashboard.
        /// All collections are copied so callers hold independent references.
        /// </summary>
        public (string[] instances,
                Dictionary<string, string> instanceToConnId,
                HashSet<string> connectionsWithSqlWatch) GetDiscoveryCache()
        {
            lock (_lock)
            {
                return (
                    _cachedInstances.ToArray(),
                    new Dictionary<string, string>(_cachedInstanceToConnectionId, StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(_cachedConnectionsWithSqlWatch)
                );
            }
        }

        /// <summary>
        /// Stores discovery results from DynamicDashboard so subsequent dashboard
        /// navigations can skip the SQL roundtrips.
        /// </summary>
        public void CacheDiscoveryResults(
            string[] instances,
            Dictionary<string, string> instanceToConnId,
            HashSet<string> connectionsWithSqlWatch)
        {
            lock (_lock)
            {
                _cachedInstances = instances.ToArray();

                _cachedInstanceToConnectionId.Clear();
                foreach (var kvp in instanceToConnId)
                    _cachedInstanceToConnectionId[kvp.Key] = kvp.Value;

                _cachedConnectionsWithSqlWatch.Clear();
                foreach (var id in connectionsWithSqlWatch)
                    _cachedConnectionsWithSqlWatch.Add(id);

                _discoveryCompleted = true;
            }
        }

        /// <summary>
        /// Clears the discovery cache. Must be called (inside _lock) whenever the
        /// connection topology changes so the next load re-discovers instance names.
        /// </summary>
        private void InvalidateDiscoveryCache()
        {
            _discoveryCompleted = false;
            _cachedInstances = Array.Empty<string>();
            _cachedInstanceToConnectionId.Clear();
            _cachedConnectionsWithSqlWatch.Clear();
        }

        private void LoadConnections()
        {
            try
            {
                if (File.Exists(_connectionsFilePath))
                {
                    var json = File.ReadAllText(_connectionsFilePath);
                    _connections = JsonSerializer.Deserialize<List<ServerConnection>>(json, DeserializeOptions) ?? new();
                    MigrateLegacyPasswords();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Load error: {ex.Message}");
                _connections = new();
            }
        }

        private void MigrateLegacyPasswords()
        {
            bool migrated = false;
            foreach (var conn in _connections.Where(c => !string.IsNullOrEmpty(c.Password) && !CredentialProtector.IsEncrypted(c.Password)))
            {
                conn.SetPassword(conn.Password);
                migrated = true;
            }
            if (migrated)
            {
                SaveConnections();
                System.Diagnostics.Debug.WriteLine("[ConnectionManager] Migrated legacy passwords");
            }
        }

        private void SaveConnections()
        {
            try
            {
                var json = JsonSerializer.Serialize(_connections, SerializeOptions);
                File.WriteAllText(_connectionsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Save error: {ex.Message}");
            }
        }
    }
}
