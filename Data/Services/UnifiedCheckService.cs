/* In the name of God, the Merciful, the Compassionate */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SQLTriage.Data.Models;

namespace SQLTriage.Data.Services
{
    public class UnifiedCheckService
    {
        private readonly ILogger<UnifiedCheckService> _logger;
        private ImmutableList<SqlCheck> _unifiedChecks = ImmutableList<SqlCheck>.Empty;
        private readonly string _unifiedChecksPath;

        public UnifiedCheckService(ILogger<UnifiedCheckService> logger)
        {
            _logger = logger;
            _unifiedChecksPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".ignore", "checks", "categories_unified");
        }

        public async Task<List<SqlCheck>> LoadUnifiedChecksAsync()
        {
            var checks = new List<SqlCheck>();

            if (!Directory.Exists(_unifiedChecksPath))
            {
                _logger.LogWarning("Unified checks directory not found: {Path}", _unifiedChecksPath);
                return checks;
            }

            var categoryDirs = Directory.GetDirectories(_unifiedChecksPath);

            foreach (var categoryDir in categoryDirs)
            {
                var categoryName = Path.GetFileName(categoryDir);
                var jsonFiles = Directory.GetFiles(categoryDir, "*.json");

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(jsonFile).ConfigureAwait(false);
                        var check = JsonSerializer.Deserialize<SqlCheck>(jsonContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (check != null)
                        {
                            check.Category = categoryName;
                            checks.Add(check);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading unified check from {File}", jsonFile);
                    }
                }
            }

            var snapshot = checks.OrderBy(c => c.Id).ToImmutableList();
            Interlocked.Exchange(ref _unifiedChecks, snapshot);
            _logger.LogInformation("Loaded {Count} unified checks from {Categories} categories",
                snapshot.Count, categoryDirs.Length);

            return checks;
        }

        public IEnumerable<SqlCheck> GetChecksByCategory(string category)
        {
            var snapshot = _unifiedChecks;
            return snapshot.Where(c => (c.Category ?? "").Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        public SqlCheck? GetCheckById(string id)
        {
            var snapshot = _unifiedChecks;
            return snapshot.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> GetCategories()
        {
            var snapshot = _unifiedChecks;
            return snapshot.Select(c => c.Category ?? "").Distinct().OrderBy(c => c);
        }

        public async Task<int> ImportToLiveRepositoryAsync(CheckRepositoryService liveRepo, IEnumerable<string>? categoryFilter = null)
        {
            var snapshot = _unifiedChecks;
            var checksToImport = categoryFilter != null
                ? snapshot.Where(c => categoryFilter.Contains(c.Category ?? "", StringComparer.OrdinalIgnoreCase))
                : snapshot;

            var importedCount = 0;

            foreach (var unifiedCheck in checksToImport)
            {
                try
                {
                    // Check if check already exists
                    var existingCheck = liveRepo.Checks.FirstOrDefault(c => c.Id == unifiedCheck.Id);

                    if (existingCheck != null)
                    {
                        // Update existing check
                        existingCheck.Name = unifiedCheck.Name;
                        existingCheck.Description = unifiedCheck.Description;
                        existingCheck.Category = unifiedCheck.Category;
                        existingCheck.Severity = unifiedCheck.Severity;
                        existingCheck.SqlQuery = unifiedCheck.SqlQuery;
                        existingCheck.Enabled = unifiedCheck.Enabled;
                        existingCheck.RecommendedAction = unifiedCheck.RecommendedAction;
                        existingCheck.Source = "unified-import";
                        existingCheck.ExecutionType = unifiedCheck.ExecutionType;
                        existingCheck.Priority = unifiedCheck.Priority;
                        existingCheck.SeverityScore = unifiedCheck.SeverityScore;
                    }
                    else
                    {
                        // Add new check
                        var newCheck = new SqlCheck
                        {
                            Id = unifiedCheck.Id,
                            Name = unifiedCheck.Name,
                            Description = unifiedCheck.Description,
                            Category = unifiedCheck.Category,
                            Severity = unifiedCheck.Severity,
                            SqlQuery = unifiedCheck.SqlQuery,
                            Enabled = true, // Enable by default for imports
                            RecommendedAction = unifiedCheck.RecommendedAction,
                            Source = "unified-import",
                            ExecutionType = unifiedCheck.ExecutionType,
                            Priority = unifiedCheck.Priority,
                            SeverityScore = unifiedCheck.SeverityScore
                        };

                        liveRepo.AddCheck(newCheck);
                    }

                    importedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing check {CheckId}", unifiedCheck.Id);
                }
            }

            // Save the live repository
            await liveRepo.SaveChecksAsync().ConfigureAwait(false);

            _logger.LogInformation("Imported {Count} checks to live repository", importedCount);
            return importedCount;
        }

        /// <summary>
        /// Export checks from live repository to unified format
        /// </summary>
        public async Task<int> ExportFromLiveRepositoryAsync(CheckRepositoryService liveRepo, IEnumerable<string>? categoryFilter = null)
        {
            var checksToExport = categoryFilter != null
                ? liveRepo.Checks.Where(c => categoryFilter.Contains(c.Category, StringComparer.OrdinalIgnoreCase))
                : liveRepo.Checks;

            var exportedCount = 0;

            foreach (var liveCheck in checksToExport)
            {
                try
                {
                    // Ensure category directory exists
                    var categoryDir = Path.Combine(_unifiedChecksPath, liveCheck.Category.ToLower());
                    Directory.CreateDirectory(categoryDir);

                    // Create unified check file
                    var checkFile = Path.Combine(categoryDir, $"{liveCheck.Id}.json");

                    // Convert to unified format (add any missing fields)
                    var unifiedCheck = new
                    {
                        id = liveCheck.Id,
                        originalIds = new[] { $"live-{liveCheck.Id}" },
                        name = liveCheck.Name,
                        title = liveCheck.Name,
                        description = liveCheck.Description,
                        category = liveCheck.Category,
                        severity = liveCheck.Severity,
                        priority = liveCheck.Priority.ToString(),
                        prerequisites = new
                        {
                            minimumSqlVersion = "2008",
                            requiredEdition = (string?)null,
                            requiredFeatures = new string[0],
                            skipBehavior = "PASS when prerequisites not met",
                            requiredPermissions = new[] { "VIEW SERVER STATE" }
                        },
                        queryAnalysis = new
                        {
                            complexity = "Medium",
                            requiresDatabaseIteration = false,
                            performanceImpact = "Low",
                            recommendedFrequency = "Daily",
                            executionTimeoutSeconds = 30
                        },
                        technicalDetails = new
                        {
                            configuration = "",
                            impact = "",
                            recommendations = new string[0],
                            relatedConfigurations = new string[0]
                        },
                        evidenceBasedFindings = new
                        {
                            microsoftGuidance = "",
                            featureAvailability = "All editions, versions from SQL Server 2008.",
                            validationSources = new object[0]
                        },
                        versionCompatibility = new
                        {
                            sql_2008 = "Full support",
                            sql_2012 = "Full support",
                            sql_2014 = "Full support",
                            sql_2016 = "Full support",
                            sql_2019 = "Full support",
                            sql_2022 = "Full support"
                        },
                        sqlQuery = liveCheck.SqlQuery,
                        resultProcessing = new
                        {
                            outputFormat = "SingleValue",
                            successCriteria = new
                            {
                                @operator = "Equals",
                                value = "PASS",
                                column = "result"
                            },
                            errorHandling = new
                            {
                                timeoutBehavior = "Fail",
                                permissionErrorBehavior = "Fail",
                                connectionErrorBehavior = "Fail"
                            },
                            messageColumn = "message",
                            resultColumn = "result"
                        },
                        executionHistory = new object[0],
                        metadata = new
                        {
                            created = DateTime.Now.ToString("yyyy-MM-dd"),
                            lastModified = DateTime.Now.ToString("yyyy-MM-dd"),
                            author = "Live Export",
                            source = "live-export",
                            tags = new[] { liveCheck.Category.ToLower(), liveCheck.Severity.ToLower() },
                            relatedChecks = new string[0],
                            documentationLinks = new string[0]
                        },
                        adaptiveFeatures = new
                        {
                            enabled = true,
                            contextAwareness = new object(),
                            dynamicThresholds = new object()
                        }
                    };

                    var jsonContent = JsonSerializer.Serialize(unifiedCheck, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await File.WriteAllTextAsync(checkFile, jsonContent).ConfigureAwait(false);
                    exportedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error exporting check {CheckId}", liveCheck.Id);
                }
            }

            _logger.LogInformation("Exported {Count} checks from live repository", exportedCount);
            return exportedCount;
        }
    }
}