/* بسم الله الرحمن الرحيم  */
/* In the name of God, the Merciful, the Compassionate */
/* ENHANCEMENTS TO DBA QUICK VIEW */

-- Add these sections to the existing SP_DBA_QUICKVIEW procedure

/*---------------------------------------------------*/
/* CHECK 1: Active Blocking Chains */
/*---------------------------------------------------*/
RAISERROR (N'Checking for active blocking chains',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT @now, DB_ID(db_name(blocked.database_id)), 'Active Blocking'
    , CAST(blocking.session_id AS VARCHAR) + ' blocking ' + CAST(blocked.session_id AS VARCHAR)
    , 'Duration: ' + CAST(DATEDIFF(SECOND, blocked.last_request_start_time, GETDATE()) AS VARCHAR) + 's'
    , LEFT(blocked_text.text, 500)
FROM sys.dm_exec_requests blocked
INNER JOIN sys.dm_exec_requests blocking ON blocked.blocking_session_id = blocking.session_id
CROSS APPLY sys.dm_exec_sql_text(blocked.sql_handle) blocked_text
WHERE blocked.blocking_session_id > 0
AND DATEDIFF(SECOND, blocked.last_request_start_time, GETDATE()) > 30;

/*---------------------------------------------------*/
/* CHECK 2: Recent Deadlocks (SQL 2012+) */
/*---------------------------------------------------*/
IF @SQLVersion >= 11
BEGIN
    RAISERROR (N'Checking for recent deadlocks',0,1) WITH NOWAIT;
    
    INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
    SELECT @now, 0, 'Recent Deadlocks'
        , CAST(COUNT(*) AS VARCHAR) + ' deadlock(s) in last 24 hours'
        , CONVERT(VARCHAR, MIN(creation_time), 120) + ' to ' + CONVERT(VARCHAR, MAX(creation_time), 120)
    FROM sys.dm_xe_sessions xs
    INNER JOIN sys.dm_xe_session_targets xt ON xs.address = xt.event_session_address
    WHERE xs.name = 'system_health'
    AND xt.target_name = 'ring_buffer'
    HAVING COUNT(*) > 0;
END

/*---------------------------------------------------*/
/* CHECK 3: Database Corruption (suspect pages) */
/*---------------------------------------------------*/
RAISERROR (N'Checking for suspect pages',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT @now, database_id, 'Database Corruption'
    , DB_NAME(database_id)
    , 'Page: ' + CAST(file_id AS VARCHAR) + ':' + CAST(page_id AS VARCHAR)
    , 'Event: ' + CAST(event_type AS VARCHAR) + ' at ' + CONVERT(VARCHAR, last_update_date, 120)
FROM msdb.dbo.suspect_pages
WHERE last_update_date > @filterdate;

/*---------------------------------------------------*/
/* CHECK 4: DBCC CHECKDB Status */
/*---------------------------------------------------*/
RAISERROR (N'Checking DBCC CHECKDB status',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
SELECT @now, database_id, 'DBCC CHECKDB Overdue'
    , name
    , CASE 
        WHEN DATABASEPROPERTYEX(name, 'LastGoodCheckDbTime') IS NULL THEN 'Never run'
        ELSE 'Last run: ' + CONVERT(VARCHAR, DATABASEPROPERTYEX(name, 'LastGoodCheckDbTime'), 120)
      END
FROM sys.databases
WHERE database_id > 4
AND state = 0
AND (DATABASEPROPERTYEX(name, 'LastGoodCheckDbTime') IS NULL 
     OR DATABASEPROPERTYEX(name, 'LastGoodCheckDbTime') < DATEADD(DAY, -7, GETDATE()));

/*---------------------------------------------------*/
/* CHECK 5: Excessive VLF Count */
/*---------------------------------------------------*/
RAISERROR (N'Checking for excessive VLF counts',0,1) WITH NOWAIT;

DECLARE @VLFInfo TABLE (
    DatabaseName NVARCHAR(128),
    VLFCount INT
);

DECLARE @dbname NVARCHAR(128);
DECLARE db_cursor CURSOR FOR 
SELECT name FROM sys.databases WHERE state = 0 AND database_id > 4;

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @dbname;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @VLFTemp TABLE (
        RecoveryUnitId INT,
        FileId INT,
        FileSize BIGINT,
        StartOffset BIGINT,
        FSeqNo BIGINT,
        Status INT,
        Parity INT,
        CreateLSN NUMERIC(38)
    );
    
    SET @sql = 'USE [' + @dbname + ']; INSERT INTO #VLFTemp EXEC sp_executesql N''DBCC LOGINFO WITH NO_INFOMSGS'';';
    
    BEGIN TRY
        EXEC sp_executesql @sql;
        INSERT INTO @VLFInfo SELECT @dbname, COUNT(*) FROM @VLFTemp;
        DELETE FROM @VLFTemp;
    END TRY
    BEGIN CATCH
        -- Skip databases we can't access
    END CATCH
    
    FETCH NEXT FROM db_cursor INTO @dbname;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
SELECT @now, DB_ID(DatabaseName), 'Excessive VLFs'
    , DatabaseName
    , CAST(VLFCount AS VARCHAR) + ' VLFs detected'
FROM @VLFInfo
WHERE VLFCount > 500;

/*---------------------------------------------------*/
/* CHECK 6: TempDB Contention */
/*---------------------------------------------------*/
RAISERROR (N'Checking for TempDB contention',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT @now, 2, 'TempDB Contention'
    , wait_type
    , CAST(waiting_tasks_count AS VARCHAR) + ' waits'
    , CAST(wait_time_ms / 1000.0 AS VARCHAR) + ' seconds total'
FROM sys.dm_os_wait_stats
WHERE wait_type IN ('PAGELATCH_UP', 'PAGELATCH_EX', 'PAGELATCH_SH')
AND waiting_tasks_count > 1000;

/*---------------------------------------------------*/
/* CHECK 7: Long Running Queries */
/*---------------------------------------------------*/
RAISERROR (N'Checking for long running queries',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT TOP 10 @now, DB_ID(DB_NAME(r.database_id)), 'Long Running Query'
    , 'Session ' + CAST(r.session_id AS VARCHAR) + ' - Duration: ' + CAST(DATEDIFF(SECOND, r.start_time, GETDATE()) AS VARCHAR) + 's'
    , 'Status: ' + r.status + ' | Wait: ' + ISNULL(r.wait_type, 'None')
    , LEFT(t.text, 500)
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id > 50
AND DATEDIFF(SECOND, r.start_time, GETDATE()) > 300
ORDER BY r.start_time;

/*---------------------------------------------------*/
/* CHECK 8: Top Wait Statistics */
/*---------------------------------------------------*/
RAISERROR (N'Checking top wait statistics',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT TOP 5 @now, 0, 'Top Wait Type'
    , wait_type
    , CAST(waiting_tasks_count AS VARCHAR) + ' waits'
    , CAST(wait_time_ms / 1000.0 / 60.0 AS VARCHAR) + ' minutes total'
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (
    'CLR_SEMAPHORE', 'LAZYWRITER_SLEEP', 'RESOURCE_QUEUE', 'SLEEP_TASK',
    'SLEEP_SYSTEMTASK', 'SQLTRACE_BUFFER_FLUSH', 'WAITFOR', 'LOGMGR_QUEUE',
    'CHECKPOINT_QUEUE', 'REQUEST_FOR_DEADLOCK_SEARCH', 'XE_TIMER_EVENT',
    'BROKER_TO_FLUSH', 'BROKER_TASK_STOP', 'CLR_MANUAL_EVENT',
    'CLR_AUTO_EVENT', 'DISPATCHER_QUEUE_SEMAPHORE', 'FT_IFTS_SCHEDULER_IDLE_WAIT',
    'XE_DISPATCHER_WAIT', 'XE_DISPATCHER_JOIN', 'SQLTRACE_INCREMENTAL_FLUSH_SLEEP'
)
AND wait_time_ms > 60000
ORDER BY wait_time_ms DESC;

/*---------------------------------------------------*/
/* CHECK 9: Memory Pressure */
/*---------------------------------------------------*/
RAISERROR (N'Checking for memory pressure',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
SELECT @now, 0, 'Memory Pressure'
    , 'Low memory notifications: ' + CAST(SUM(CASE WHEN type = 'MEMORYCLERK_SQLGENERAL' THEN 1 ELSE 0 END) AS VARCHAR)
    , 'Total memory clerks under pressure'
FROM sys.dm_os_memory_clerks
WHERE pages_kb < 1024
HAVING COUNT(*) > 10;

/*---------------------------------------------------*/
/* CHECK 10: AG Synchronization Health (SQL 2012+) */
/*---------------------------------------------------*/
IF @SQLVersion >= 11 AND EXISTS(SELECT 1 FROM sys.availability_groups)
BEGIN
    RAISERROR (N'Checking AG synchronization health',0,1) WITH NOWAIT;
    
    INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
    SELECT @now, DB_ID(dbcs.database_name), 'AG Sync Issue'
        , dbcs.database_name
        , 'State: ' + CAST(dbrs.synchronization_state_desc AS VARCHAR)
        , 'Lag: ' + CAST(dbrs.log_send_queue_size AS VARCHAR) + ' KB'
    FROM sys.dm_hadr_database_replica_states dbrs
    INNER JOIN sys.dm_hadr_database_replica_cluster_states dbcs ON dbrs.replica_id = dbcs.replica_id
    WHERE dbrs.synchronization_state NOT IN (2) -- Not SYNCHRONIZED
    AND dbrs.is_local = 1;
END

/*---------------------------------------------------*/
/* CHECK 11: Critical Error Log Entries */
/*---------------------------------------------------*/
RAISERROR (N'Checking for critical error log entries',0,1) WITH NOWAIT;

DECLARE @ErrorLogCritical TABLE (
    LogDate DATETIME,
    ProcessInfo NVARCHAR(150),
    [Text] NVARCHAR(MAX)
);

INSERT INTO @ErrorLogCritical
EXEC sp_readerrorlog 0, 1, 'severity', '17';

INSERT INTO @ErrorLogCritical
EXEC sp_readerrorlog 0, 1, 'severity', '18';

INSERT INTO @ErrorLogCritical
EXEC sp_readerrorlog 0, 1, 'severity', '19';

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT @now, 0, 'Critical Error'
    , CONVERT(VARCHAR, LogDate, 120)
    , ProcessInfo
    , LEFT([Text], 500)
FROM @ErrorLogCritical
WHERE LogDate > @filterdate;

/*---------------------------------------------------*/
/* CHECK 12: Outdated Statistics */
/*---------------------------------------------------*/
RAISERROR (N'Checking for outdated statistics',0,1) WITH NOWAIT;

INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
SELECT TOP 20 @now, s.database_id, 'Outdated Statistics'
    , DB_NAME(s.database_id) + '.' + OBJECT_NAME(s.object_id, s.database_id)
    , 'Stat: ' + s.name
    , 'Last updated: ' + ISNULL(CONVERT(VARCHAR, STATS_DATE(s.object_id, s.stats_id), 120), 'Never')
FROM sys.stats s
INNER JOIN sys.objects o ON s.object_id = o.object_id
WHERE o.type = 'U'
AND (STATS_DATE(s.object_id, s.stats_id) IS NULL 
     OR STATS_DATE(s.object_id, s.stats_id) < DATEADD(DAY, -7, GETDATE()))
ORDER BY STATS_DATE(s.object_id, s.stats_id);

-- Add output sections for new checks in the final output section
-- (Insert similar IF EXISTS blocks as shown in original script)
