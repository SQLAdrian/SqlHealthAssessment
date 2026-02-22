/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlHealthAssessment.Data;

namespace SqlHealthAssessment.Data.Models
{
    public class ServerConnection
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("ServerNames")]
        public string ServerNames { get; set; } = string.Empty;

        [JsonPropertyName("Database")]
        public string Database { get; set; } = "master";

        [JsonPropertyName("UseWindowsAuthentication")]
        public bool UseWindowsAuthentication { get; set; } = true;

        /// <summary>
        /// Authentication method: "Windows", "SqlServer", or "EntraMFA".
        /// When null, falls back to UseWindowsAuthentication for backward compat.
        /// </summary>
        [JsonPropertyName("AuthenticationType")]
        public string? AuthenticationType { get; set; }

        /// <summary>
        /// Resolves the effective authentication type, falling back to the legacy
        /// UseWindowsAuthentication flag when AuthenticationType is not set.
        /// </summary>
        [JsonIgnore]
        public string EffectiveAuthType => AuthenticationType
            ?? (UseWindowsAuthentication ? AuthenticationTypes.Windows : AuthenticationTypes.SqlServer);

        /// <summary>
        /// Display-friendly authentication type label.
        /// </summary>
        [JsonIgnore]
        public string AuthenticationDisplay => EffectiveAuthType switch
        {
            AuthenticationTypes.EntraMFA => "Microsoft Entra MFA",
            AuthenticationTypes.SqlServer => "SQL Authentication",
            _ => "Windows Authentication"
        };

        [JsonPropertyName("Username")]
        public string? Username { get; set; }

        /// <summary>
        /// Trust server certificate (for self-signed SSL certificates)
        /// </summary>
        [JsonPropertyName("TrustServerCertificate")]
        public bool TrustServerCertificate { get; set; } = false;

        /// <summary>
        /// Stored encrypted via DPAPI. Use SetPassword/GetDecryptedPassword for access.
        /// Legacy plaintext values are auto-migrated on first read.
        /// </summary>
        [JsonPropertyName("Password")]
        public string? Password { get; set; }

        /// <summary>
        /// Sets the password, encrypting it with DPAPI before storage.
        /// </summary>
        public void SetPassword(string? plainTextPassword)
        {
            if (string.IsNullOrEmpty(plainTextPassword))
            {
                Password = null;
                return;
            }
            Password = CredentialProtector.Encrypt(plainTextPassword);
        }

        /// <summary>
        /// Gets the decrypted password. Handles both encrypted (enc:...) and legacy plaintext values.
        /// Methods are not serialized by System.Text.Json, so no [JsonIgnore] needed.
        /// </summary>
        public string GetDecryptedPassword()
        {
            return CredentialProtector.Decrypt(Password);
        }

        [JsonPropertyName("ConnectionTimeout")]
        public int ConnectionTimeout { get; set; } = 15;

        [JsonPropertyName("LastConnected")]
        public DateTime? LastConnected { get; set; }

        [JsonPropertyName("IsConnected")]
        public bool IsConnected { get; set; }

        [JsonPropertyName("SuccessfulServers")]
        public List<string> SuccessfulServers { get; set; } = new();

        /// <summary>
        /// When false, this connection is excluded from check execution and health monitoring.
        /// The connection is kept for reference but not actively used.
        /// </summary>
        [JsonPropertyName("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Environment classification (e.g., "Production", "Development", "Staging", "QA").
        /// Used for grouping and filtering in the UI.
        /// </summary>
        [JsonPropertyName("Environment")]
        public string? Environment { get; set; }

        /// <summary>
        /// Freeform tags for categorisation and filtering
        /// (e.g., "critical", "east-us", "finance", "ag-primary").
        /// </summary>
        [JsonPropertyName("Tags")]
        public List<string> Tags { get; set; } = new();

        public int GetServerCount() => GetServerList().Count;

        public List<string> GetServerList()
        {
            return ServerNames.Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .Where(s => !string.IsNullOrEmpty(s))
                           .ToList();
        }

        public string GetConnectionString(string serverName)
        {
            return GetConnectionString(serverName, Database);
        }

        public string GetConnectionString(string serverName, string database)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = database,
                ConnectTimeout = ConnectionTimeout,
                PersistSecurityInfo = false,
                TrustServerCertificate = TrustServerCertificate
            };

            switch (EffectiveAuthType)
            {
                case AuthenticationTypes.EntraMFA:
                    builder.IntegratedSecurity = false;
                    builder.Authentication = Microsoft.Data.SqlClient.SqlAuthenticationMethod.ActiveDirectoryInteractive;
                    if (!string.IsNullOrWhiteSpace(Username))
                        builder.UserID = Username;
                    break;

                case AuthenticationTypes.SqlServer:
                    builder.IntegratedSecurity = false;
                    builder.UserID = Username;
                    builder.Password = GetDecryptedPassword();
                    break;

                default: // Windows
                    builder.IntegratedSecurity = true;
                    break;
            }

            return builder.ConnectionString;
        }
    }

    public class ServerConnectionManager
    {
        private readonly string _connectionsFilePath;
        private List<ServerConnection> _connections = new();
        private readonly object _lock = new();
        private string? _currentServerId;

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
            lock (_lock)
            {
                return _connections.ToList();
            }
        }

        /// <summary>
        /// Returns only connections where IsEnabled is true.
        /// </summary>
        public List<ServerConnection> GetEnabledConnections()
        {
            lock (_lock)
            {
                return _connections.Where(c => c.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Returns an array of server names from all enabled connections.
        /// Used by dashboards to populate the instance dropdown.
        /// </summary>
        public string[] GetEnabledServerNames()
        {
            lock (_lock)
            {
                return _connections
                    .Where(c => c.IsEnabled)
                    .SelectMany(c => c.GetServerList())
                    .ToArray();
            }
        }

        public ServerConnection? GetConnection(string id)
        {
            lock (_lock)
            {
                return _connections.FirstOrDefault(c => c.Id == id);
            }
        }

        public ServerConnection? GetDefaultConnection()
        {
            lock (_lock)
            {
                return _connections.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets the currently selected server context.
        /// </summary>
        public ServerConnection? CurrentServer
        {
            get
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(_currentServerId))
                        return null;
                    return _connections.FirstOrDefault(c => c.Id == _currentServerId);
                }
            }
        }

        /// <summary>
        /// Sets the current server context for all dashboards.
        /// </summary>
        public void SetCurrentServer(string? serverId)
        {
            lock (_lock)
            {
                _currentServerId = serverId;
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
                    _connections = JsonSerializer.Deserialize<List<ServerConnection>>(json, DeserializeOptions)
                        ?? new List<ServerConnection>();

                    // Migrate any legacy plaintext passwords to DPAPI-encrypted
                    MigrateLegacyPasswords();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServerConnectionManager] Error loading connections: {ex.Message}");
                _connections = new List<ServerConnection>();
            }
        }

        /// <summary>
        /// Migrates any legacy plaintext passwords to DPAPI-encrypted format.
        /// Detects plaintext by checking for the absence of the "enc:" prefix.
        /// </summary>
        private void MigrateLegacyPasswords()
        {
            bool migrated = false;
            foreach (var conn in _connections)
            {
                if (!string.IsNullOrEmpty(conn.Password) && !CredentialProtector.IsEncrypted(conn.Password))
                {
                    // This is a legacy plaintext password - encrypt it
                    conn.SetPassword(conn.Password);
                    migrated = true;
                }
            }
            if (migrated)
            {
                SaveConnections();
                System.Diagnostics.Debug.WriteLine("[ServerConnectionManager] Migrated legacy plaintext passwords to encrypted storage.");
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
                System.Diagnostics.Debug.WriteLine($"[ServerConnectionManager] Error saving connections: {ex.Message}");
            }
        }
    }
}
