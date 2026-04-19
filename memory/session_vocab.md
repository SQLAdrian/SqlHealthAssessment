---
name: Session Vocabulary
description: Active abbreviations for input compression — auto-updated by LLM each session
type: reference
---

# Session Vocabulary

**Rules for the LLM:**
- When you see a shorthand token below in user input, expand it using this table before reasoning.
- If you CANNOT expand a shorthand (not in table AND can't infer from context), flag it: `[unknown shorthand: XYZ]` and ask the user.
- When you notice a phrase repeating ≥3 times in a session, propose an abbreviation: add a row under "Proposed" with `[PROPOSED]` tag.
- Prefer abbreviations that are 1-2 BPE tokens. Uppercase acronyms usually tokenize well (e.g. USS = 1 token, UsrStSrv = 3 tokens).

## Active abbreviations

| Shorthand | Full phrase | Uses this session | Status |
|-----------|-------------|-------------------|--------|
| wl | worklist | 0 | active |
| nxt | what is next | 0 | active |
| USS | UserSettingsService | 0 | active |
| QPv2 | QueryPlanV2 | 0 | active |
| dcfg | dashboard-config.json | 0 | active |
| QC | QuickCheck | 0 | active |
| VA | VulnerabilityAssessment | 0 | active |
| dlk | deadlock | 0 | active |
| dmv | dynamic management view | 0 | active |
| xevent | extended events | 0 | active |
| SRV | ServerModeService | 0 | active |
| AEV | AlertEvaluationService | 0 | active |
| ADF | AlertDefinitionService | 0 | active |
| AHS | AlertHistoryService | 0 | active |
| ABS | AlertBaselineService | 0 | active |
| NCS | NotificationChannelService | 0 | active |
| TDF | ScheduledTaskDefinitionService | 0 | active |
| THS | ScheduledTaskHistoryService | 0 | active |
| RPC | ReportPageConfigService | 0 | active |
| ABE | AzureBlobExportService | 0 | active |
| SAS | SqlAssessmentService | 0 | active |
| SWD | SqlWatchDeploymentService | 0 | active |
| WIN | WindowsServiceHost | 0 | active |
| EPP | ExecutionPlanParser | 0 | active |
| HCS | HealthCheckService | 0 | active |
| ACH | AppCircuitHandler | 0 | active |
| CES | CheckExecutionService | 0 | active |
| CDS | DashboardConfigService | 0 | active |
| FAS | FullAuditStateService | 0 | active |
| VAS | VulnerabilityAssessmentStateService | 0 | active |
| XES | XEventService | 0 | active |
| CEC | CacheEvictionService | 0 | active |
| SES | sys.dm_exec_sessions | 0 | active |
| REQ | sys.dm_exec_requests | 0 | active |
| QST | sys.dm_exec_query_stats | 0 | active |
| QPL | sys.dm_exec_query_plan | 0 | active |
| CON | sys.dm_exec_connections | 0 | active |
| WST | sys.dm_os_wait_stats | 0 | active |
| CPL | sys.dm_exec_cached_plans | 0 | active |
| TRX | sys.dm_tran_active_transactions | 0 | active |
| IUS | sys.dm_db_index_usage_stats | 0 | active |
| MID | sys.dm_db_missing_index_details | 0 | active |
| WIA | sp_WhoIsActive | 0 | active |
| SWP | dbo.usp_sqlwatch_logger_performance | 0 | active |
| SWR | dbo.usp_sqlwatch_logger_requests_and_sessions | 0 | active |
| SWX | dbo.usp_sqlwatch_logger_xes_blockers | 0 | active |
| SWI | dbo.usp_sqlwatch_internal_add_performance_counter | 0 | active |
| QMG | sys.dm_exec_query_memory_grants | 0 | active |
| OPC | sys.dm_os_performance_counters | 0 | active |
| EPF | Execution plan parsing failed | 0 | active |


## Proposed (awaiting ≥3 uses to promote to active)

_(LLM adds rows here when a phrase repeats ≥3 times. After 3 uses in "Proposed", move the row to Active above.)_
