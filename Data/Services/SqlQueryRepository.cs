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

namespace SQLTriage.Data.Services
{
    /// <summary>
    /// Runtime SQL loader with hot-reload support.
    /// Loads query definitions from Config/queries.json and SQL text from Data/Sql/*.sql.
    /// Watches for file changes with 500ms debounce.
    /// </summary>
    public interface ISqlQueryRepository
    {
        SqlQueryDefinition? Get(string id);
        IReadOnlyDictionary<string, SqlQueryDefinition> GetAll();
        IReadOnlyList<SqlQueryDefinition> GetByTag(string tag);
        IReadOnlyList<SqlQueryDefinition> GetQuickChecks();
        Task ReloadAsync();
    }

    /// <summary>
    /// Metadata for a single query loaded from queries.json.
    /// </summary>
    public sealed class SqlQueryDefinition
    {
        public required string Id { get; init; }
        public required string Sql { get; init; }
        public required string FilePath { get; init; }
        public string Description { get; init; } = "";
        public string Category { get; init; } = "";
        public string Severity { get; init; } = "MEDIUM";
        public string Status { get; init; } = "working";
        public bool Quick { get; init; } = false;
        public IReadOnlyList<string> Audience { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Controls { get; init; } = Array.Empty<string>();
        public int TimeoutSec { get; init; } = 30;
    }

    public sealed class SqlQueryRepository : ISqlQueryRepository, IDisposable
    {
        private readonly ILogger<SqlQueryRepository> _logger;
        private readonly string _sqlDirectory;
        private readonly string _queriesConfigPath;

        // Thread-safe storage
        private readonly ConcurrentDictionary<string, SqlQueryDefinition> _queries = new();
        private readonly ConcurrentDictionary<string, List<string>> _tagIndex = new();

        // File watching
        private FileSystemWatcher? _configWatcher;
        private FileSystemWatcher? _sqlWatcher;
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private CancellationTokenSource _debounceCts = new();

        public SqlQueryRepository(ILogger<SqlQueryRepository> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _sqlDirectory = Path.Combine(baseDir, "Data", "Sql");
            _queriesConfigPath = Path.Combine(baseDir, "Config", "queries.json");

            // Initial load
            _ = LoadAsync();

            // Set up file watchers
            SetupFileWatchers();
        }

        public SqlQueryDefinition? Get(string id)
        {
            return _queries.TryGetValue(id, out var query) ? query : null;
        }

        public IReadOnlyDictionary<string, SqlQueryDefinition> GetAll()
        {
            return _queries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IReadOnlyList<SqlQueryDefinition> GetByTag(string tag)
        {
            if (_tagIndex.TryGetValue(tag, out var ids))
            {
                return ids.Select(id => _queries[id]).Where(q => q != null).ToList();
            }
            return Array.Empty<SqlQueryDefinition>();
        }

        public IReadOnlyList<SqlQueryDefinition> GetQuickChecks()
        {
            return _queries.Values.Where(q => q.Quick).ToList();
        }

        public async Task ReloadAsync()
        {
            await _reloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await LoadAsync().ConfigureAwait(false);
                _logger.LogInformation("SQL query repository reloaded");
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                // Load metadata from queries.json
                var metadata = await LoadMetadataAsync().ConfigureAwait(false);

                // Load SQL files and combine with metadata
                await LoadSqlFilesAsync(metadata).ConfigureAwait(false);

                // Rebuild tag index
                BuildTagIndex();

                _logger.LogInformation("Loaded {Count} SQL queries from {SqlDir}",
                    _queries.Count, _sqlDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load SQL query repository");
            }
        }

        private async Task<Dictionary<string, QueryMetadata>> LoadMetadataAsync()
        {
            var metadata = new Dictionary<string, QueryMetadata>();

            if (!File.Exists(_queriesConfigPath))
            {
                _logger.LogWarning("Queries config file not found: {Path}", _queriesConfigPath);
                return metadata;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_queriesConfigPath).ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<QueriesConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config?.Queries != null)
                {
                    metadata = config.Queries;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load queries metadata from {Path}", _queriesConfigPath);
            }

            return metadata;
        }

        private async Task LoadSqlFilesAsync(Dictionary<string, QueryMetadata> metadata)
        {
            var newQueries = new ConcurrentDictionary<string, SqlQueryDefinition>();

            if (!Directory.Exists(_sqlDirectory))
            {
                _logger.LogWarning("SQL directory not found: {Path}", _sqlDirectory);
                Directory.CreateDirectory(_sqlDirectory);
                return;
            }

            var sqlFiles = Directory.GetFiles(_sqlDirectory, "*.sql", SearchOption.AllDirectories);

            foreach (var sqlFile in sqlFiles)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(_sqlDirectory, sqlFile).Replace('\\', '/');
                    var id = Path.GetFileNameWithoutExtension(sqlFile);
                    var sql = await File.ReadAllTextAsync(sqlFile).ConfigureAwait(false);

                    // Get metadata or create defaults
                    var queryMetadata = metadata.GetValueOrDefault(id, new QueryMetadata());

                    var definition = new SqlQueryDefinition
                    {
                        Id = id,
                        Sql = sql,
                        FilePath = sqlFile,
                        Description = queryMetadata.Description ?? "",
                        Category = queryMetadata.Category ?? "",
                        Severity = queryMetadata.Severity ?? "MEDIUM",
                        Status = queryMetadata.Status ?? "working",
                        Quick = queryMetadata.Quick,
                        Audience = queryMetadata.Audience ?? Array.Empty<string>(),
                        Controls = queryMetadata.Controls ?? Array.Empty<string>(),
                        TimeoutSec = queryMetadata.TimeoutSec > 0 ? queryMetadata.TimeoutSec : 30
                    };

                    newQueries[id] = definition;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load SQL file {Path}", sqlFile);
                }
            }

            // Atomic update
            _queries.Clear();
            foreach (var kvp in newQueries)
            {
                _queries[kvp.Key] = kvp.Value;
            }
        }

        private void BuildTagIndex()
        {
            var newIndex = new ConcurrentDictionary<string, List<string>>();

            foreach (var query in _queries.Values)
            {
                // Index by category
                if (!string.IsNullOrEmpty(query.Category))
                {
                    AddToIndex(newIndex, query.Category.ToLower(), query.Id);
                }

                // Index by audience
                foreach (var audience in query.Audience)
                {
                    AddToIndex(newIndex, audience.ToLower(), query.Id);
                }

                // Index by severity
                if (!string.IsNullOrEmpty(query.Severity))
                {
                    AddToIndex(newIndex, query.Severity.ToLower(), query.Id);
                }

                // Special tags
                if (query.Quick)
                {
                    AddToIndex(newIndex, "quick", query.Id);
                }

                if (query.Status == "working")
                {
                    AddToIndex(newIndex, "working", query.Id);
                }
            }

            _tagIndex.Clear();
            foreach (var kvp in newIndex)
            {
                _tagIndex[kvp.Key] = kvp.Value;
            }
        }

        private static void AddToIndex(ConcurrentDictionary<string, List<string>> index, string tag, string id)
        {
            index.AddOrUpdate(tag,
                _ => new List<string> { id },
                (_, list) => { list.Add(id); return list; });
        }

        private void SetupFileWatchers()
        {
            try
            {
                // Watch queries.json
                if (File.Exists(_queriesConfigPath))
                {
                    _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(_queriesConfigPath)!)
                    {
                        Filter = "queries.json",
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                    };
                    _configWatcher.Changed += OnConfigChanged;
                    _configWatcher.Created += OnConfigChanged;
                    _configWatcher.Deleted += OnConfigChanged;
                    _configWatcher.EnableRaisingEvents = true;
                }

                // Watch SQL directory
                if (Directory.Exists(_sqlDirectory))
                {
                    _sqlWatcher = new FileSystemWatcher(_sqlDirectory)
                    {
                        Filter = "*.sql",
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        IncludeSubdirectories = true
                    };
                    _sqlWatcher.Changed += OnSqlFileChanged;
                    _sqlWatcher.Created += OnSqlFileChanged;
                    _sqlWatcher.Deleted += OnSqlFileChanged;
                    _sqlWatcher.Renamed += OnSqlFileRenamed;
                    _sqlWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup file watchers");
            }
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            DebounceReload();
        }

        private void OnSqlFileChanged(object sender, FileSystemEventArgs e)
        {
            DebounceReload();
        }

        private void OnSqlFileRenamed(object sender, RenamedEventArgs e)
        {
            DebounceReload();
        }

        private void DebounceReload()
        {
            // Cancel previous debounce
            _debounceCts.Cancel();
            _debounceCts = new CancellationTokenSource();

            // Start new debounced reload
            _ = Task.Delay(500, _debounceCts.Token)
                .ContinueWith(_ => ReloadAsync(), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public void Dispose()
        {
            _debounceCts.Cancel();
            _debounceCts.Dispose();
            _reloadLock.Dispose();

            _configWatcher?.Dispose();
            _sqlWatcher?.Dispose();
        }

        // Internal classes for deserialization
        private class QueriesConfig
        {
            public string? Comment { get; set; }
            public Dictionary<string, object>? Schema { get; set; }
            public int SchemaVersion { get; set; }
            public Dictionary<string, QueryMetadata>? Queries { get; set; }
        }

        private class QueryMetadata
        {
            public string? File { get; set; }
            public string? Description { get; set; }
            public string? Category { get; set; }
            public string? Severity { get; set; }
            public string? Status { get; set; }
            public bool Quick { get; set; }
            public string[]? Audience { get; set; }
            public string[]? Controls { get; set; }
            public int TimeoutSec { get; set; }
        }
    }
}