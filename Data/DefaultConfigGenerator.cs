/* In the name of God, the Merciful, the Compassionate */

using System.Collections.Generic;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Generates the default dashboard configuration that matches the current hardcoded behavior exactly.
    /// This is used to produce the initial dashboard-config.json content.
    /// </summary>
    public static class DefaultConfigGenerator
    {
        public static DashboardConfigRoot Generate()
        {
            return new DashboardConfigRoot
            {
                Version = 1,
                Dashboards = new List<DashboardDefinition>
                {
                    BuildRepositoryDashboard(),
                    BuildInstanceDashboard()
                },
                SupportQueries = BuildSupportQueries()
            };
        }

        // ================================================================
        // Helper methods for panel creation
        // ================================================================

        private static PanelDefinition TimeSeriesPanel(string id, string title, int column, int order, string sqlServer, string sqlite, string chartType = "Line", bool span = false)
        {
            return new PanelDefinition
            {
                Id = id,
                Title = title,
                PanelType = "TimeSeries",
                ChartType = chartType,
                Layout = new PanelLayout { Column = column, Order = order, SpanColumns = span },
                Query = new QueryPair { SqlServer = sqlServer, Sqlite = sqlite }
            };
        }

        private static PanelDefinition StatCardPanel(string id, string title, string unit, int order, string sqlServer, string sqlite, string? statThresholdKey = null, List<ColorThresholdRule>? colorThresholds = null)
        {
            return new PanelDefinition
            {
                Id = id,
                Title = title,
                PanelType = "StatCard",
                StatUnit = unit,
                StatThresholdKey = statThresholdKey,
                Layout = new PanelLayout { Column = 0, Order = order },
                Query = new QueryPair { SqlServer = sqlServer, Sqlite = sqlite },
                ColorThresholds = colorThresholds ?? new List<ColorThresholdRule>()
            };
        }

        private static PanelDefinition CheckStatusPanel(string id, string title, int order, string sqlServer, string sqlite)
        {
            return new PanelDefinition
            {
                Id = id,
                Title = title,
                PanelType = "CheckStatus",
                Layout = new PanelLayout { Column = 0, Order = order },
                Query = new QueryPair { SqlServer = sqlServer, Sqlite = sqlite }
            };
        }

        private static PanelDefinition BarGaugePanel(string id, string title, string thresholdKey, string unitSuffix, int column, int order, string sqlServer, string sqlite, bool span = false, List<ColorThresholdRule>? colorThresholds = null)
        {
            return new PanelDefinition
            {
                Id = id,
                Title = title,
                PanelType = "BarGauge",
                BarGaugeThresholdKey = thresholdKey,
                BarGaugeUnitSuffix = unitSuffix,
                Layout = new PanelLayout { Column = column, Order = order, SpanColumns = span },
                Query = new QueryPair { SqlServer = sqlServer, Sqlite = sqlite },
                ColorThresholds = colorThresholds ?? new List<ColorThresholdRule>()
            };
        }

        private static PanelDefinition DataGridPanel(string id, string title, int order, string sqlServer, string sqlite, bool span = false)
        {
            return new PanelDefinition
            {
                Id = id,
                Title = title,
                PanelType = "DataGrid",
                Layout = new PanelLayout { Column = 1, Order = order, SpanColumns = span },
                Query = new QueryPair { SqlServer = sqlServer, Sqlite = sqlite }
            };
        }

        // ================================================================
        // REPOSITORY DASHBOARD
        // ================================================================

        private static DashboardDefinition BuildRepositoryDashboard()
        {
            return new DashboardDefinition
            {
                Id = "repository",
                Title = "Repository",
                Route = "/",
                ShowAllOption = true,
                Panels = new List<PanelDefinition>
                {
                    // --- StatCard panels (column=0) ---

                    StatCardPanel(
                        id: "repo.instance_count",
                        title: "Instances",
                        unit: "",
                        order: 1,
                        sqlServer: @"
SELECT COUNT(DISTINCT sql_instance) AS [Value]
FROM dbo.sqlwatch_config_sql_instance WITH (NOLOCK)
WHERE repo_collector_is_active = 1",
                        sqlite: @"
SELECT COUNT(DISTINCT sql_instance) AS [Value]
FROM sqlwatch_config_sql_instance
WHERE repo_collector_is_active = 1"
                    ),

                    StatCardPanel(
                        id: "repo.process_memory",
                        title: "SQL Memory",
                        unit: "%",
                        order: 2,
                        statThresholdKey: "SQL Memory",
                        colorThresholds: new List<ColorThresholdRule>
                        {
                            new() { Operator = ">=", Value = 0,  Color = "#1b5e20", Label = "Normal" },
                            new() { Operator = ">=", Value = 85, Color = "#ff9800", Label = "Warning" },
                            new() { Operator = ">=", Value = 95, Color = "#f44336", Label = "Critical" }
                        },
                        sqlServer: @"
SELECT TOP 1
    h.report_time AS [Time],
    'Memory %' AS [Series],
    d.memory_utilization_percentage AS [Value]
FROM dbo.sqlwatch_logger_perf_os_process_memory d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time DESC",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Memory %' AS [Series],
    d.memory_utilization_percentage AS [Value]
FROM sqlwatch_logger_perf_os_process_memory d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY h.report_time DESC
LIMIT 1"
                    ),

                    StatCardPanel(
                        id: "repo.schedulers",
                        title: "Pending Queue",
                        unit: "",
                        order: 3,
                        statThresholdKey: "Pending",
                        colorThresholds: new List<ColorThresholdRule>
                        {
                            new() { Operator = ">=", Value = 0, Color = "#1b5e20", Label = "Normal" },
                            new() { Operator = ">=", Value = 1, Color = "#f44336", Label = "Critical" }
                        },
                        sqlServer: @"
SELECT TOP 1
    h.report_time AS [Time],
    'Pending' AS [Series],
    d.work_queue_count AS [Value]
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time DESC",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Pending' AS [Series],
    d.work_queue_count AS [Value]
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY h.report_time DESC
LIMIT 1"
                    ),

                    // --- CheckStatus panel (column=0) ---

                    CheckStatusPanel(
                        id: "repo.checks",
                        title: "Health Checks",
                        order: 4,
                        sqlServer: @"
SELECT
    d.check_status AS [Status],
    COUNT(*) AS [Count]
FROM dbo.sqlwatch_logger_check d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY d.check_status",
                        sqlite: @"
SELECT
    d.check_status AS [Status],
    COUNT(*) AS [Count]
FROM sqlwatch_logger_check d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY d.check_status"
                    ),

                    // --- TimeSeries charts (columns 1 and 2, alternating) ---

                    TimeSeriesPanel(
                        id: "repo.cpu",
                        title: "CPU Utilization",
                        column: 1,
                        order: 1,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name IN ('Processor Time %','Privileged Time %','User Time %')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name IN ('Processor Time %','Privileged Time %','User Time %')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "repo.perf_counters",
                        title: "Performance Counters",
                        column: 2,
                        order: 2,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name IN ('Batch Requests/Sec','Readahead pages/sec','Transactions/sec','Processes blocked')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name IN ('Batch Requests/Sec','Readahead pages/sec','Transactions/sec','Processes blocked')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "repo.wait_stats",
                        title: "Wait Stats",
                        column: 1,
                        order: 3,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Wait Time (ms)' AS [Series],
    SUM(d.wait_time_ms_delta) AS [Value]
FROM dbo.sqlwatch_logger_perf_os_wait_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Wait Time (ms)' AS [Series],
    SUM(d.wait_time_ms_delta) AS [Value]
FROM sqlwatch_logger_perf_os_wait_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "repo.file_stats",
                        title: "File IO Throughput",
                        column: 2,
                        order: 4,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Bytes Read/s' AS [Series],
    SUM(CASE WHEN ISNULL(d.delta_seconds,0) > 0 THEN d.num_of_bytes_read_delta / d.delta_seconds ELSE 0 END) AS [Value]
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time

UNION ALL

SELECT
    h.report_time AS [Time],
    'Bytes Written/s' AS [Series],
    SUM(CASE WHEN ISNULL(d.delta_seconds,0) > 0 THEN d.num_of_bytes_written_delta / d.delta_seconds ELSE 0 END) AS [Value]
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Bytes Read/s' AS [Series],
    SUM(CASE WHEN COALESCE(d.delta_seconds,0) > 0 THEN d.num_of_bytes_read_delta / d.delta_seconds ELSE 0 END) AS [Value]
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time

UNION ALL

SELECT
    h.report_time AS [Time],
    'Bytes Written/s' AS [Series],
    SUM(CASE WHEN COALESCE(d.delta_seconds,0) > 0 THEN d.num_of_bytes_written_delta / d.delta_seconds ELSE 0 END) AS [Value]
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "repo.long_queries",
                        title: "Long Running Queries",
                        column: 1,
                        order: 5,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Long Queries' AS [Series],
    COUNT(*) AS [Value]
FROM dbo.sqlwatch_logger_xes_long_queries d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Long Queries' AS [Series],
    COUNT(*) AS [Value]
FROM sqlwatch_logger_xes_long_queries d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time
ORDER BY h.report_time"
                    )
                }
            };
        }

        // ================================================================
        // INSTANCE DASHBOARD
        // ================================================================

        private static DashboardDefinition BuildInstanceDashboard()
        {
            return new DashboardDefinition
            {
                Id = "instance",
                Title = "Instance Overview",
                Route = "/instance",
                ShowAllOption = false,
                Panels = new List<PanelDefinition>
                {
                    // --- StatCard panels (column=0) ---

                    StatCardPanel(
                        id: "repo.instance_count",
                        title: "Instances",
                        unit: "",
                        order: 1,
                        sqlServer: @"
SELECT COUNT(DISTINCT sql_instance) AS [Value]
FROM dbo.sqlwatch_config_sql_instance WITH (NOLOCK)
WHERE repo_collector_is_active = 1",
                        sqlite: @"
SELECT COUNT(DISTINCT sql_instance) AS [Value]
FROM sqlwatch_config_sql_instance
WHERE repo_collector_is_active = 1"
                    ),

                    StatCardPanel(
                        id: "repo.process_memory",
                        title: "SQL Memory",
                        unit: "%",
                        order: 2,
                        statThresholdKey: "SQL Memory",
                        colorThresholds: new List<ColorThresholdRule>
                        {
                            new() { Operator = ">=", Value = 0,  Color = "#1b5e20", Label = "Normal" },
                            new() { Operator = ">=", Value = 85, Color = "#ff9800", Label = "Warning" },
                            new() { Operator = ">=", Value = 95, Color = "#f44336", Label = "Critical" }
                        },
                        sqlServer: @"
SELECT TOP 1
    h.report_time AS [Time],
    'Memory %' AS [Series],
    d.memory_utilization_percentage AS [Value]
FROM dbo.sqlwatch_logger_perf_os_process_memory d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time DESC",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Memory %' AS [Series],
    d.memory_utilization_percentage AS [Value]
FROM sqlwatch_logger_perf_os_process_memory d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY h.report_time DESC
LIMIT 1"
                    ),

                    StatCardPanel(
                        id: "repo.schedulers",
                        title: "Pending Queue",
                        unit: "",
                        order: 3,
                        statThresholdKey: "Pending",
                        colorThresholds: new List<ColorThresholdRule>
                        {
                            new() { Operator = ">=", Value = 0, Color = "#1b5e20", Label = "Normal" },
                            new() { Operator = ">=", Value = 1, Color = "#f44336", Label = "Critical" }
                        },
                        sqlServer: @"
SELECT TOP 1
    h.report_time AS [Time],
    'Pending' AS [Series],
    d.work_queue_count AS [Value]
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time DESC",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Pending' AS [Series],
    d.work_queue_count AS [Value]
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY h.report_time DESC
LIMIT 1"
                    ),

                    // --- BarGauge panel (column=0, spanColumns) ---

                    BarGaugePanel(
                        id: "instance.disk_space",
                        title: "Disk Space",
                        thresholdKey: "Disk Space Used %",
                        unitSuffix: "%",
                        column: 0,
                        order: 10,
                        span: true,
                        colorThresholds: new List<ColorThresholdRule>
                        {
                            new() { Operator = ">=", Value = 0,  Color = "#1b5e20", Label = "Normal" },
                            new() { Operator = ">=", Value = 80, Color = "#ff9800", Label = "Warning" },
                            new() { Operator = ">=", Value = 90, Color = "#f44336", Label = "Critical" }
                        },
                        sqlServer: @"
SELECT
    v.volume_name AS [Label],
    CASE WHEN d.volume_total_space_bytes > 0
        THEN CAST((1.0 - (1.0 * d.volume_free_space_bytes / d.volume_total_space_bytes)) * 100.0 AS decimal(5,1))
        ELSE 0 END AS [Value],
    '%' AS [Unit],
    d.sql_instance AS [Instance]
FROM dbo.sqlwatch_logger_disk_utilisation_volume d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_os_volume v WITH (NOLOCK)
    ON v.sql_instance = d.sql_instance
    AND v.sqlwatch_volume_id = d.sqlwatch_volume_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND h.snapshot_time = (
        SELECT MAX(h2.snapshot_time)
        FROM dbo.sqlwatch_logger_snapshot_header h2 WITH (NOLOCK)
        WHERE h2.report_time BETWEEN @TimeFrom AND @TimeTo
            AND h2.sql_instance = d.sql_instance
            AND h2.snapshot_type_id = d.snapshot_type_id
    )",
                        sqlite: @"
SELECT
    v.volume_name AS [Label],
    CASE WHEN d.volume_total_space_bytes > 0
        THEN CAST((1.0 - (1.0 * d.volume_free_space_bytes / d.volume_total_space_bytes)) * 100.0 AS REAL)
        ELSE 0 END AS [Value],
    '%' AS [Unit],
    d.sql_instance AS [Instance]
FROM sqlwatch_logger_disk_utilisation_volume d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_os_volume v
    ON v.sql_instance = d.sql_instance
    AND v.sqlwatch_volume_id = d.sqlwatch_volume_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND h.snapshot_time = (
        SELECT MAX(h2.snapshot_time)
        FROM sqlwatch_logger_snapshot_header h2
        WHERE h2.report_time BETWEEN @TimeFrom AND @TimeTo
            AND h2.sql_instance = d.sql_instance
            AND h2.snapshot_type_id = d.snapshot_type_id
    )"
                    ),

                    // --- TimeSeries charts (alternating columns 1 and 2) ---

                    TimeSeriesPanel(
                        id: "instance.cpu",
                        title: "CPU Utilization",
                        column: 1,
                        order: 1,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name IN ('Processor Time %','Privileged Time %','User Time %')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name IN ('Processor Time %','Privileged Time %','User Time %')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.activity",
                        title: "Activity (Batch Requests, Logins, etc.)",
                        column: 2,
                        order: 2,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name IN ('Batch Requests/Sec','Logins/sec','Transactions/sec','SQL Compilations/sec','User Connections')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name IN ('Batch Requests/Sec','Logins/sec','Transactions/sec','SQL Compilations/sec','User Connections')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.waits_category",
                        title: "Wait Events by Category",
                        column: 1,
                        order: 3,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    ISNULL(ws.wait_category, 'Other') AS [Series],
    SUM(d.wait_time_ms_delta) AS [Value]
FROM dbo.sqlwatch_logger_perf_os_wait_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN dbo.vw_sqlwatch_meta_wait_stats_category ws WITH (NOLOCK)
    ON ws.sql_instance = d.sql_instance
    AND ws.wait_type_id = d.wait_type_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time, ISNULL(ws.wait_category, 'Other')
HAVING SUM(d.wait_time_ms_delta) > 0
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    COALESCE(cw.wait_category,
        CASE
            WHEN mw.wait_type LIKE 'LCK_%' THEN 'Lock'
            WHEN mw.wait_type LIKE 'LATCH_%' THEN 'Latch'
            WHEN mw.wait_type LIKE 'PAGELATCH_%' THEN 'Buffer Latch'
            WHEN mw.wait_type LIKE 'PAGEIOLATCH_%' THEN 'Buffer IO'
            WHEN mw.wait_type IN ('CXPACKET','EXCHANGE','CXCONSUMER') THEN 'CPU - Parallelism'
            WHEN mw.wait_type IN ('ASYNC_NETWORK_IO','NET_WAITFOR_PACKET') THEN 'Network IO'
            WHEN mw.wait_type IN ('LOGMGR','LOGBUFFER','WRITELOG') THEN 'Tran Log IO'
            WHEN mw.wait_type LIKE 'HADR_%' THEN 'Always On'
            ELSE 'Other'
        END
    ) AS [Series],
    SUM(d.wait_time_ms_delta) AS [Value]
FROM sqlwatch_logger_perf_os_wait_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN sqlwatch_meta_wait_stats mw
    ON mw.sql_instance = d.sql_instance
    AND mw.wait_type_id = d.wait_type_id
LEFT JOIN sqlwatch_config_wait_stats cw
    ON cw.wait_type = mw.wait_type
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time, [Series]
HAVING SUM(d.wait_time_ms_delta) > 0
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.memory",
                        title: "Memory",
                        column: 2,
                        order: 4,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Physical Memory In Use (KB)' AS [Series],
    d.physical_memory_in_use_kb AS [Value]
FROM dbo.sqlwatch_logger_perf_os_process_memory d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))

UNION ALL

SELECT
    h.report_time,
    'Memory Utilization %',
    d.memory_utilization_percentage
FROM dbo.sqlwatch_logger_perf_os_process_memory d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Physical Memory In Use (KB)' AS [Series],
    d.physical_memory_in_use_kb AS [Value]
FROM sqlwatch_logger_perf_os_process_memory d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance

UNION ALL

SELECT
    h.report_time,
    'Memory Utilization %',
    d.memory_utilization_percentage
FROM sqlwatch_logger_perf_os_process_memory d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.sessions",
                        title: "Sessions",
                        column: 1,
                        order: 5,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Running' AS [Series], d.running AS [Value]
FROM dbo.sqlwatch_logger_dm_exec_sessions_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Sleeping', d.sleeping
FROM dbo.sqlwatch_logger_dm_exec_sessions_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Dormant', d.dormant
FROM dbo.sqlwatch_logger_dm_exec_sessions_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Running' AS [Series], d.running AS [Value]
FROM sqlwatch_logger_dm_exec_sessions_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Sleeping', d.sleeping
FROM sqlwatch_logger_dm_exec_sessions_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Dormant', d.dormant
FROM sqlwatch_logger_dm_exec_sessions_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND d.type = 1
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.requests",
                        title: "Requests",
                        column: 2,
                        order: 6,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Background' AS [Series], d.background AS [Value]
FROM dbo.sqlwatch_logger_dm_exec_requests_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Running', d.running
FROM dbo.sqlwatch_logger_dm_exec_requests_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Runnable', d.runnable
FROM dbo.sqlwatch_logger_dm_exec_requests_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Sleeping', d.sleeping
FROM dbo.sqlwatch_logger_dm_exec_requests_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Suspended', d.suspended
FROM dbo.sqlwatch_logger_dm_exec_requests_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.type = 1
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Background' AS [Series], d.background AS [Value]
FROM sqlwatch_logger_dm_exec_requests_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND d.type = 1

UNION ALL

SELECT h.report_time, 'Running', d.running
FROM sqlwatch_logger_dm_exec_requests_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance AND d.type = 1

UNION ALL

SELECT h.report_time, 'Runnable', d.runnable
FROM sqlwatch_logger_dm_exec_requests_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance AND d.type = 1

UNION ALL

SELECT h.report_time, 'Sleeping', d.sleeping
FROM sqlwatch_logger_dm_exec_requests_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance AND d.type = 1

UNION ALL

SELECT h.report_time, 'Suspended', d.suspended
FROM sqlwatch_logger_dm_exec_requests_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance AND d.type = 1
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.file_stats_latency",
                        title: "File Stats \u2014 Latency (ms)",
                        column: 1,
                        order: 7,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    ISNULL(f.database_name,'?') + ' - ' + ISNULL(mf.file_name,'?') AS [Series],
    CASE WHEN d.num_of_reads_delta > 0 THEN d.io_stall_read_ms_delta / d.num_of_reads_delta ELSE 0 END
    + CASE WHEN d.num_of_writes_delta > 0 THEN d.io_stall_write_ms_delta / d.num_of_writes_delta ELSE 0 END AS [Value]
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN dbo.sqlwatch_meta_master_file mf WITH (NOLOCK)
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN dbo.sqlwatch_meta_database f WITH (NOLOCK)
    ON f.sql_instance = mf.sql_instance
    AND f.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    COALESCE(db.database_name,'?') || ' - ' || COALESCE(mf.file_name,'?') AS [Series],
    CASE WHEN d.num_of_reads_delta > 0 THEN d.io_stall_read_ms_delta / d.num_of_reads_delta ELSE 0 END
    + CASE WHEN d.num_of_writes_delta > 0 THEN d.io_stall_write_ms_delta / d.num_of_writes_delta ELSE 0 END AS [Value]
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN sqlwatch_meta_master_file mf
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN sqlwatch_meta_database db
    ON db.sql_instance = mf.sql_instance
    AND db.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.file_stats_throughput",
                        title: "File Stats \u2014 Throughput",
                        column: 2,
                        order: 8,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    ISNULL(f.database_name,'?') + ' Read B/s' AS [Series],
    CASE WHEN ISNULL(d.delta_seconds,0) > 0 THEN d.num_of_bytes_read_delta / d.delta_seconds ELSE 0 END AS [Value]
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN dbo.sqlwatch_meta_master_file mf WITH (NOLOCK)
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN dbo.sqlwatch_meta_database f WITH (NOLOCK)
    ON f.sql_instance = mf.sql_instance
    AND f.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))

UNION ALL

SELECT
    h.report_time,
    ISNULL(f.database_name,'?') + ' Write B/s',
    CASE WHEN ISNULL(d.delta_seconds,0) > 0 THEN d.num_of_bytes_written_delta / d.delta_seconds ELSE 0 END
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN dbo.sqlwatch_meta_master_file mf WITH (NOLOCK)
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN dbo.sqlwatch_meta_database f WITH (NOLOCK)
    ON f.sql_instance = mf.sql_instance
    AND f.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    COALESCE(db.database_name,'?') || ' Read B/s' AS [Series],
    CASE WHEN COALESCE(d.delta_seconds,0) > 0 THEN d.num_of_bytes_read_delta / d.delta_seconds ELSE 0 END AS [Value]
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN sqlwatch_meta_master_file mf
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN sqlwatch_meta_database db
    ON db.sql_instance = mf.sql_instance
    AND db.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance

UNION ALL

SELECT
    h.report_time,
    COALESCE(db.database_name,'?') || ' Write B/s',
    CASE WHEN COALESCE(d.delta_seconds,0) > 0 THEN d.num_of_bytes_written_delta / d.delta_seconds ELSE 0 END
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
LEFT JOIN sqlwatch_meta_master_file mf
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
LEFT JOIN sqlwatch_meta_database db
    ON db.sql_instance = mf.sql_instance
    AND db.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.schedulers",
                        title: "Scheduler Stats",
                        column: 1,
                        order: 9,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Current Tasks' AS [Series], d.current_tasks_count AS [Value]
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))

UNION ALL

SELECT h.report_time, 'Runnable Tasks', d.runnable_tasks_count
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))

UNION ALL

SELECT h.report_time, 'Work Queue', d.work_queue_count
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))

UNION ALL

SELECT h.report_time, 'Pending Disk IO', d.pending_disk_io_count
FROM dbo.sqlwatch_logger_perf_os_schedulers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Current Tasks' AS [Series], d.current_tasks_count AS [Value]
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance

UNION ALL

SELECT h.report_time, 'Runnable Tasks', d.runnable_tasks_count
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance

UNION ALL

SELECT h.report_time, 'Work Queue', d.work_queue_count
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance

UNION ALL

SELECT h.report_time, 'Pending Disk IO', d.pending_disk_io_count
FROM sqlwatch_logger_perf_os_schedulers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time AND h.snapshot_type_id = d.snapshot_type_id AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo AND d.sql_instance = @SqlInstance
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.blocking",
                        title: "Blocking",
                        column: 2,
                        order: 10,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Blocked Sessions' AS [Series],
    COUNT(DISTINCT d.blocked_spid) AS [Value]
FROM dbo.sqlwatch_logger_xes_blockers d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Blocked Sessions' AS [Series],
    COUNT(DISTINCT d.blocked_spid) AS [Value]
FROM sqlwatch_logger_xes_blockers d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.lock_requests",
                        title: "Lock Requests",
                        column: 1,
                        order: 11,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name IN ('Lock Requests/sec','Lock Waits/sec','Lock Timeouts/sec','Number of Deadlocks/sec')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    m.counter_name AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name IN ('Lock Requests/sec','Lock Waits/sec','Lock Timeouts/sec','Number of Deadlocks/sec')
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.page_life",
                        title: "Page Life Expectancy",
                        column: 2,
                        order: 12,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Page Life Expectancy' AS [Series],
    d.cntr_value_calculated AS [Value]
FROM dbo.sqlwatch_logger_perf_os_performance_counters d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_performance_counter m WITH (NOLOCK)
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND m.counter_name = 'Page life expectancy'
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Page Life Expectancy' AS [Series],
    d.cntr_value_calculated AS [Value]
FROM sqlwatch_logger_perf_os_performance_counters d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_performance_counter m
    ON m.sql_instance = d.sql_instance
    AND m.performance_counter_id = d.performance_counter_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND m.counter_name = 'Page life expectancy'
    AND m.cntr_type <> 1073939712
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.memory_clerks",
                        title: "Memory Clerks",
                        column: 1,
                        order: 13,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    mc.clerk_name AS [Series],
    omc.allocated_kb AS [Value]
FROM dbo.sqlwatch_logger_perf_os_memory_clerks omc WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = omc.snapshot_time
    AND h.snapshot_type_id = omc.snapshot_type_id
    AND h.sql_instance = omc.sql_instance
INNER JOIN dbo.sqlwatch_meta_memory_clerk mc WITH (NOLOCK)
    ON mc.sql_instance = omc.sql_instance
    AND mc.sqlwatch_mem_clerk_id = omc.sqlwatch_mem_clerk_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND omc.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    mc.clerk_name AS [Series],
    omc.allocated_kb AS [Value]
FROM sqlwatch_logger_perf_os_memory_clerks omc
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = omc.snapshot_time
    AND h.snapshot_type_id = omc.snapshot_type_id
    AND h.sql_instance = omc.sql_instance
INNER JOIN sqlwatch_meta_memory_clerk mc
    ON mc.sql_instance = omc.sql_instance
    AND mc.sqlwatch_mem_clerk_id = omc.sqlwatch_mem_clerk_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND omc.sql_instance = @SqlInstance
ORDER BY h.report_time"
                    ),

                    TimeSeriesPanel(
                        id: "instance.database_stats",
                        title: "Database Stats",
                        column: 2,
                        order: 14,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'Database Count' AS [Series],
    COUNT(DISTINCT d.sqlwatch_database_id) AS [Value]
FROM dbo.sqlwatch_logger_disk_utilisation_database d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time

UNION ALL

SELECT
    h.report_time,
    'Total Size (MB)',
    SUM(ISNULL(d.database_size_bytes,0)) / 1048576.0
FROM dbo.sqlwatch_logger_disk_utilisation_database d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
GROUP BY h.report_time
ORDER BY [Time]",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'Database Count' AS [Series],
    COUNT(DISTINCT d.sqlwatch_database_id) AS [Value]
FROM sqlwatch_logger_disk_utilisation_database d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time

UNION ALL

SELECT
    h.report_time,
    'Total Size (MB)',
    SUM(COALESCE(d.database_size_bytes,0)) / 1048576.0
FROM sqlwatch_logger_disk_utilisation_database d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
GROUP BY h.report_time
ORDER BY [Time]"
                    ),

                    TimeSeriesPanel(
                        id: "instance.tempdb",
                        title: "TempDB Size",
                        column: 1,
                        order: 15,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    'TempDB Size (MB)' AS [Series],
    SUM(d.size_on_disk_bytes) / 1048576.0 AS [Value]
FROM dbo.sqlwatch_logger_perf_file_stats d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_master_file mf WITH (NOLOCK)
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
INNER JOIN dbo.sqlwatch_meta_database db WITH (NOLOCK)
    ON db.sql_instance = mf.sql_instance
    AND db.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND db.database_name = 'tempdb'
GROUP BY h.report_time
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    'TempDB Size (MB)' AS [Series],
    SUM(d.size_on_disk_bytes) / 1048576.0 AS [Value]
FROM sqlwatch_logger_perf_file_stats d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_master_file mf
    ON mf.sql_instance = d.sql_instance
    AND mf.sqlwatch_database_id = d.sqlwatch_database_id
    AND mf.sqlwatch_master_file_id = d.sqlwatch_master_file_id
INNER JOIN sqlwatch_meta_database db
    ON db.sql_instance = mf.sql_instance
    AND db.sqlwatch_database_id = mf.sqlwatch_database_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND db.database_name = 'tempdb'
GROUP BY h.report_time
ORDER BY h.report_time"
                    ),

                    // --- Bar chart (spanColumns) ---

                    TimeSeriesPanel(
                        id: "instance.checks_history",
                        title: "Check Failures Over Time",
                        column: 1,
                        order: 20,
                        chartType: "Bar",
                        span: true,
                        sqlServer: @"
SELECT
    h.report_time AS [Time],
    d.check_status AS [Series],
    COUNT(*) AS [Value]
FROM dbo.sqlwatch_logger_check d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
    AND d.check_status <> 'OK'
GROUP BY h.report_time, d.check_status
ORDER BY h.report_time",
                        sqlite: @"
SELECT
    h.report_time AS [Time],
    d.check_status AS [Series],
    COUNT(*) AS [Value]
FROM sqlwatch_logger_check d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
    AND d.check_status <> 'OK'
GROUP BY h.report_time, d.check_status
ORDER BY h.report_time"
                    ),

                    // --- DataGrid (spanColumns) ---

                    DataGridPanel(
                        id: "instance.checks_detail",
                        title: "Check Details",
                        order: 21,
                        span: true,
                        sqlServer: @"
SELECT
    d.snapshot_time,
    c.check_name,
    c.check_description,
    d.check_value,
    d.check_status,
    d.check_exec_time_ms,
    d.sql_instance
FROM dbo.sqlwatch_logger_check d WITH (NOLOCK)
INNER JOIN dbo.sqlwatch_logger_snapshot_header h WITH (NOLOCK)
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN dbo.sqlwatch_meta_check c WITH (NOLOCK)
    ON c.sql_instance = d.sql_instance
    AND c.check_id = d.check_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))
ORDER BY d.snapshot_time DESC",
                        sqlite: @"
SELECT
    d.snapshot_time,
    c.check_name,
    c.check_description,
    d.check_value,
    d.check_status,
    d.check_exec_time_ms,
    d.sql_instance
FROM sqlwatch_logger_check d
INNER JOIN sqlwatch_logger_snapshot_header h
    ON h.snapshot_time = d.snapshot_time
    AND h.snapshot_type_id = d.snapshot_type_id
    AND h.sql_instance = d.sql_instance
INNER JOIN sqlwatch_meta_check c
    ON c.sql_instance = d.sql_instance
    AND c.check_id = d.check_id
WHERE h.report_time BETWEEN @TimeFrom AND @TimeTo
    AND d.sql_instance = @SqlInstance
ORDER BY d.snapshot_time DESC"
                    )
                }
            };
        }

        // ================================================================
        // SUPPORT QUERIES
        // ================================================================

        private static Dictionary<string, QueryPair> BuildSupportQueries()
        {
            return new Dictionary<string, QueryPair>
            {
                ["instances.list"] = new QueryPair
                {
                    SqlServer = @"
SELECT sql_instance
FROM dbo.sqlwatch_config_sql_instance WITH (NOLOCK)
ORDER BY repo_collector_is_active DESC, sql_instance",
                    Sqlite = @"
SELECT sql_instance
FROM sqlwatch_config_sql_instance
ORDER BY repo_collector_is_active DESC, sql_instance"
                },

                ["longquery.sqltext"] = new QueryPair
                {
                    SqlServer = @"
SELECT
    COALESCE(qph.statement_for_query_plan_hash, '') AS sql_text,
    qph.query_plan_for_query_plan_hash AS query_plan
FROM dbo.sqlwatch_logger_xes_long_queries d WITH (NOLOCK)
LEFT JOIN dbo.sqlwatch_meta_query_plan qp WITH (NOLOCK)
    ON qp.sql_instance = d.sql_instance
    AND qp.plan_handle = d.plan_handle
    AND qp.statement_start_offset = d.statement_start_offset
    AND qp.statement_end_offset = d.statement_end_offset
LEFT JOIN dbo.sqlwatch_meta_query_plan_hash qph WITH (NOLOCK)
    ON qph.sql_instance = qp.sql_instance
    AND qph.query_plan_hash = qp.query_plan_hash
WHERE d.long_query_id = @QueryId
    AND d.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))",
                    Sqlite = @"
SELECT
    COALESCE(qph.statement_for_query_plan_hash, '') AS sql_text,
    qph.query_plan_for_query_plan_hash AS query_plan
FROM sqlwatch_logger_xes_long_queries d
LEFT JOIN sqlwatch_meta_query_plan qp
    ON qp.sql_instance = d.sql_instance
    AND qp.plan_handle = d.plan_handle
    AND qp.statement_start_offset = d.statement_start_offset
    AND qp.statement_end_offset = d.statement_end_offset
LEFT JOIN sqlwatch_meta_query_plan_hash qph
    ON qph.sql_instance = qp.sql_instance
    AND qph.query_plan_hash = qp.query_plan_hash
WHERE d.long_query_id = @QueryId
    AND d.sql_instance = @SqlInstance"
                },

                ["waits.sqltext"] = new QueryPair
                {
                    SqlServer = @"
SELECT
    COALESCE(qph.statement_for_query_plan_hash, '') AS sql_text,
    qph.query_plan_for_query_plan_hash AS query_plan
FROM dbo.sqlwatch_meta_query_plan_hash qph WITH (NOLOCK)
WHERE qph.query_plan_hash = @PlanHash
    AND qph.sql_instance IN (SELECT value FROM STRING_SPLIT(@SqlInstance, ','))",
                    Sqlite = @"
SELECT
    COALESCE(qph.statement_for_query_plan_hash, '') AS sql_text,
    qph.query_plan_for_query_plan_hash AS query_plan
FROM sqlwatch_meta_query_plan_hash qph
WHERE qph.query_plan_hash = @PlanHash
    AND qph.sql_instance = @SqlInstance"
                }
            };
        }
    }
}
