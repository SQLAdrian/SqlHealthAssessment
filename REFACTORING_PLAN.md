# Refactoring Plan for Longevity

## Phase 1: Quick Wins (1-2 hours)
- [ ] Delete duplicate config files
- [ ] Remove .tmp, .zip, $null files from root
- [ ] Clean obj/ folder of wpftmp files
- [ ] Add .editorconfig for consistent formatting
- [ ] Document service responsibilities in README

## Phase 2: Service Consolidation (4-6 hours)
- [ ] Merge QueryExecutor + CachingQueryExecutor
- [ ] Merge AutoRefreshService + QueryThrottleService
- [ ] Merge DashboardConfigService + DashboardDataService
- [ ] Merge CheckExecutionService + CheckRepositoryService
- [ ] Split ServerConnection.cs into two files

## Phase 3: Error Handling (2-3 hours)
- [ ] Add ILogger to all services
- [ ] Replace empty catch blocks with logging
- [ ] Add structured error responses
- [ ] Implement global exception handler

## Phase 4: Testing (8-10 hours)
- [ ] Create test project
- [ ] Add unit tests for core services
- [ ] Add integration tests for SQL queries
- [ ] Add UI component tests

## Phase 5: Configuration (2-3 hours)
- [ ] Consolidate all config into appsettings.json
- [ ] Use IOptions<T> pattern
- [ ] Remove duplicate config files
- [ ] Add config validation on startup

## Metrics
- Current Services: 30+
- Target Services: 15-20
- Current Config Files: 8+
- Target Config Files: 3 (appsettings.json + 2 overrides)
- Current Lines of Code: ~15,000
- Target Lines of Code: ~12,000 (20% reduction)
