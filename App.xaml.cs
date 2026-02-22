/* In the name of God, the Merciful, the Compassionate */

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SqlHealthAssessment.Data;
using SqlHealthAssessment.Data.Caching;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment
{
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddWpfBlazorWebView();

            // Register ServerConnectionManager first - it will be used by SqlServerConnectionFactory
            services.AddSingleton<ServerConnectionManager>();

            // Register SQL Server connection - uses ServerConnectionManager for dynamic server selection
            var connStr = configuration.GetConnectionString("SqlServer") ?? "Server=.;Database=SQLWATCH;Integrated Security=true;";
            var trustServerCert = configuration.GetValue<bool>("TrustServerCertificate", false);
            
            // Create a temporary factory to get the registered ServerConnectionManager
            services.AddSingleton<IDbConnectionFactory>(sp =>
            {
                var serverManager = sp.GetRequiredService<ServerConnectionManager>();
                return new SqlServerConnectionFactory(serverManager, connStr, trustServerCert);
            });
            services.AddSingleton<SqlServerConnectionFactory>(sp =>
            {
                var serverManager = sp.GetRequiredService<ServerConnectionManager>();
                return new SqlServerConnectionFactory(serverManager, connStr, trustServerCert);
            });

            services.AddSingleton<DashboardConfigService>();
            services.AddSingleton<QueryStore>();
            services.AddSingleton<QueryExecutor>();
            services.AddScoped<DashboardDataService>();
            services.AddSingleton<AutoRefreshService>();
            services.AddSingleton<GlobalInstanceSelector>();
            services.AddSingleton<CheckRepositoryService>();
            services.AddSingleton<DiagnosticScriptRunner>();
            services.AddSingleton<FullAuditStateService>();
            services.AddSingleton<AuditLogService>();
            services.AddSingleton<AlertingService>();
            services.AddSingleton<HealthCheckService>();
            services.AddSingleton<CheckExecutionService>();
            services.AddSingleton<SQLiteTableService>();
            services.AddSingleton<SessionManager>();
            services.AddSingleton<RateLimiter>();
            services.AddSingleton<UserSettingsService>();
            services.AddSingleton<SessionDataService>();

            // Local log service for debug logging
            var maxLogSize = configuration.GetValue<long>("LogMaxFileSizeBytes", 5 * 1024 * 1024);
            services.AddSingleton(new LocalLogService(maxLogSize));

            // SQLite caching layer — delta-fetch + offline resilience
            services.AddSingleton<SqliteCacheStore>();
            services.AddSingleton<CacheStateTracker>();
            services.AddSingleton<CachingQueryExecutor>();
            services.AddSingleton<CacheEvictionService>();
            services.AddSingleton<SqliteMaintenanceService>();

            Services = services.BuildServiceProvider();

            // Start cache eviction timer (runs every 5 minutes)
            Services.GetService<CacheEvictionService>()?.Start();

            // Start SQLite maintenance timer (VACUUM + optimize, default every 4 hours)
            Services.GetService<SqliteMaintenanceService>()?.Start();

            // Log application start for audit trail
            var auditLog = Services.GetService<AuditLogService>();
            auditLog?.LogApplicationStart();

            // Ensure SQLite tables exist for all panels on startup (best-effort, non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var tableService = Services?.GetService<SQLiteTableService>();
                    var connFactory = Services?.GetService<IDbConnectionFactory>();
                    var configService = Services?.GetService<DashboardConfigService>();

                    if (tableService != null && connFactory != null && configService != null)
                    {
                        var (succeeded, failed, errors) = await tableService.EnsureTablesForAllPanelsAsync(
                            connFactory, configService.Config);

                        System.Diagnostics.Debug.WriteLine(
                            $"[App] SQLite table provisioning: {succeeded} OK, {failed} failed.");
                        foreach (var err in errors)
                            System.Diagnostics.Debug.WriteLine($"[App]   - {err}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] SQLite table provisioning error: {ex.Message}");
                }
            });
        }
    }
}
