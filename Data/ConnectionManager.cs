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
                    SaveConnections();
                }
            }
        }

        public void RemoveConnection(string id)
        {
            lock (_lock)
            {
                _connections.RemoveAll(c => c.Id == id);
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
