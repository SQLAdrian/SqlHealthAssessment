/* بسم الله الرحمن الرحيم  */
/* In the name of God, the Merciful, the Compassionate */
/* ENHANCED DBA QUICK VIEW - Add these sections after line 680 (after Login Modification section) */

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 1: Active Blocking Chains */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for active blocking chains',0,1) WITH NOWAIT;

BEGIN TRY
	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
	SELECT @now, DB_ID(DB_NAME(blocked.database_id)), 'Active Blocking'
		, CAST(blocking.session_id AS VARCHAR) + ' blocking ' + CAST(blocked.session_id AS VARCHAR)
		, 'Duration: ' + CAST(DATEDIFF(SECOND, blocked.last_request_start_time, GETDATE()) AS VARCHAR) + 's'
		, LEFT(blocked_text.text, 500)
	FROM sys.dm_exec_requests blocked
	INNER JOIN sys.dm_exec_requests blocking ON blocked.blocking_session_id = blocking.session_id
	CROSS APPLY sys.dm_exec_sql_text(blocked.sql_handle) blocked_text
	WHERE blocked.blocking_session_id > 0
	AND DATEDIFF(SECOND, blocked.last_request_start_time, GETDATE()) > 30;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking blocking chains',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 2: Database Corruption (suspect pages) */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for suspect pages',0,1) WITH NOWAIT;

BEGIN TRY
	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
	SELECT @now, database_id, 'Database Corruption'
		, DB_NAME(database_id)
		, 'Page: ' + CAST(file_id AS VARCHAR) + ':' + CAST(page_id AS VARCHAR)
		, 'Event: ' + CAST(event_type AS VARCHAR) + ' at ' + CONVERT(VARCHAR, last_update_date, 120)
	FROM msdb.dbo.suspect_pages
	WHERE last_update_date > @filterdate;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking suspect pages',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 3: DBCC CHECKDB Status */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking DBCC CHECKDB status',0,1) WITH NOWAIT;

BEGIN TRY
	DECLARE @dbname_checkdb NVARCHAR(128);
	DECLARE @lastcheckdb DATETIME;
	DECLARE db_cursor_checkdb CURSOR FOR 
	SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0;

	OPEN db_cursor_checkdb;
	FETCH NEXT FROM db_cursor_checkdb INTO @dbname_checkdb;

	WHILE @@FETCH_STATUS = 0
	BEGIN
		BEGIN TRY
			SET @lastcheckdb = CAST(DATABASEPROPERTYEX(@dbname_checkdb, 'LastGoodCheckDbTime') AS DATETIME);
			
			IF @lastcheckdb IS NULL OR @lastcheckdb < DATEADD(DAY, -7, GETDATE())
			BEGIN
				INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
				SELECT @now, DB_ID(@dbname_checkdb), 'DBCC CHECKDB Overdue'
					, @dbname_checkdb
					, CASE WHEN @lastcheckdb IS NULL THEN 'Never run' 
						   ELSE 'Last run: ' + CONVERT(VARCHAR, @lastcheckdb, 120) END;
			END
		END TRY
		BEGIN CATCH
			-- Skip databases we can't access
		END CATCH
		
		FETCH NEXT FROM db_cursor_checkdb INTO @dbname_checkdb;
	END

	CLOSE db_cursor_checkdb;
	DEALLOCATE db_cursor_checkdb;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking DBCC CHECKDB status',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 4: TempDB Contention */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for TempDB contention',0,1) WITH NOWAIT;

BEGIN TRY
	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
	SELECT @now, 2, 'TempDB Contention'
		, wait_type
		, CAST(waiting_tasks_count AS VARCHAR) + ' waits'
		, CAST(wait_time_ms / 1000.0 AS VARCHAR) + ' seconds total'
	FROM sys.dm_os_wait_stats
	WHERE wait_type IN ('PAGELATCH_UP', 'PAGELATCH_EX', 'PAGELATCH_SH')
	AND waiting_tasks_count > 1000;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking TempDB contention',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 5: Long Running Queries */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for long running queries',0,1) WITH NOWAIT;

BEGIN TRY
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
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking long running queries',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 6: Top Wait Statistics */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking top wait statistics',0,1) WITH NOWAIT;

BEGIN TRY
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
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking wait statistics',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 7: AG Synchronization Health (SQL 2012+) */
/*---------------------------------------------------*//*---------------------------------------------------*/
IF @SQLVersion >= 11 AND EXISTS(SELECT 1 FROM sys.availability_groups)
BEGIN
	RAISERROR (N'Checking AG synchronization health',0,1) WITH NOWAIT;
	
	BEGIN TRY
		INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
		SELECT @now, DB_ID(dbcs.database_name), 'AG Sync Issue'
			, dbcs.database_name
			, 'State: ' + CAST(dbrs.synchronization_state_desc AS VARCHAR)
			, 'Lag: ' + CAST(dbrs.log_send_queue_size AS VARCHAR) + ' KB'
		FROM sys.dm_hadr_database_replica_states dbrs
		INNER JOIN sys.dm_hadr_database_replica_cluster_states dbcs ON dbrs.replica_id = dbcs.replica_id
		WHERE dbrs.synchronization_state NOT IN (2) -- Not SYNCHRONIZED
		AND dbrs.is_local = 1;
	END TRY
	BEGIN CATCH
		RAISERROR (N'Error checking AG sync health',0,1) WITH NOWAIT;
	END CATCH
END

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 8: Critical Error Log Entries */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for critical error log entries',0,1) WITH NOWAIT;

BEGIN TRY
	DECLARE @ErrorLogCritical TABLE (
		LogDate DATETIME,
		ProcessInfo NVARCHAR(150),
		[Text] NVARCHAR(MAX)
	);

	INSERT INTO @ErrorLogCritical EXEC sp_readerrorlog 0, 1, 'severity', '17';
	INSERT INTO @ErrorLogCritical EXEC sp_readerrorlog 0, 1, 'severity', '18';
	INSERT INTO @ErrorLogCritical EXEC sp_readerrorlog 0, 1, 'severity', '19';

	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
	SELECT @now, 0, 'Critical Error'
		, CONVERT(VARCHAR, LogDate, 120)
		, ProcessInfo
		, LEFT([Text], 500)
	FROM @ErrorLogCritical
	WHERE LogDate > @filterdate;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking critical errors',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 9: Outdated Statistics */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for outdated statistics',0,1) WITH NOWAIT;

BEGIN TRY
	DECLARE @dbname_stats NVARCHAR(128);
	DECLARE db_cursor_stats CURSOR FOR 
	SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0;

	OPEN db_cursor_stats;
	FETCH NEXT FROM db_cursor_stats INTO @dbname_stats;

	WHILE @@FETCH_STATUS = 0
	BEGIN
		BEGIN TRY
			SET @sql = N'USE [' + @dbname_stats + N'];
			INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
			SELECT TOP 20 @now, DB_ID(), ''Outdated Statistics''
				, DB_NAME() + ''.'' + OBJECT_NAME(s.object_id)
				, ''Stat: '' + s.name
				, ''Last updated: '' + ISNULL(CONVERT(VARCHAR, STATS_DATE(s.object_id, s.stats_id), 120), ''Never'')
			FROM sys.stats s
			INNER JOIN sys.objects o ON s.object_id = o.object_id
			WHERE o.type = ''U''
			AND (STATS_DATE(s.object_id, s.stats_id) IS NULL 
				 OR STATS_DATE(s.object_id, s.stats_id) < DATEADD(DAY, -7, GETDATE()))
			ORDER BY STATS_DATE(s.object_id, s.stats_id);';
			
			EXEC sp_executesql @sql, N'@now NVARCHAR(20)', @now;
		END TRY
		BEGIN CATCH
			-- Skip databases we can't access
		END CATCH
		
		FETCH NEXT FROM db_cursor_stats INTO @dbname_stats;
	END

	CLOSE db_cursor_stats;
	DEALLOCATE db_cursor_stats;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking outdated statistics',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 10: Memory Pressure */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for memory pressure',0,1) WITH NOWAIT;

BEGIN TRY
	IF EXISTS(SELECT 1 FROM sys.dm_os_memory_clerks WHERE pages_kb < 1024 GROUP BY type HAVING COUNT(*) > 10)
	BEGIN
		INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
		SELECT @now, 0, 'Memory Pressure'
			, 'Low memory clerks detected'
			, CAST(COUNT(*) AS VARCHAR) + ' memory clerks under pressure'
		FROM sys.dm_os_memory_clerks
		WHERE pages_kb < 1024;
	END
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking memory pressure',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 11: Index Fragmentation */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for heavily fragmented indexes',0,1) WITH NOWAIT;

BEGIN TRY
	DECLARE @dbname_frag NVARCHAR(128);
	DECLARE db_cursor_frag CURSOR FOR 
	SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0;

	OPEN db_cursor_frag;
	FETCH NEXT FROM db_cursor_frag INTO @dbname_frag;

	WHILE @@FETCH_STATUS = 0
	BEGIN
		BEGIN TRY
			SET @sql = N'USE [' + @dbname_frag + N'];
			INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
			SELECT TOP 10 @now, DB_ID(), ''Heavy Fragmentation''
				, DB_NAME() + ''.'' + OBJECT_NAME(ips.object_id) + ''.'' + i.name
				, ''Fragmentation: '' + CAST(CAST(ips.avg_fragmentation_in_percent AS INT) AS VARCHAR) + ''%''
				, ''Pages: '' + CAST(ips.page_count AS VARCHAR)
			FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, ''LIMITED'') ips
			INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
			WHERE ips.avg_fragmentation_in_percent > 30
			AND ips.page_count > 1000
			ORDER BY ips.avg_fragmentation_in_percent DESC;';
			
			EXEC sp_executesql @sql, N'@now NVARCHAR(20)', @now;
		END TRY
		BEGIN CATCH
			-- Skip databases we can't access
		END CATCH
		
		FETCH NEXT FROM db_cursor_frag INTO @dbname_frag;
	END

	CLOSE db_cursor_frag;
	DEALLOCATE db_cursor_frag;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking index fragmentation',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* CHECK 12: Excessive VLF Count */
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Checking for excessive VLF counts',0,1) WITH NOWAIT;

BEGIN TRY
	DECLARE @VLFInfo TABLE (DatabaseName NVARCHAR(128), VLFCount INT);
	DECLARE @dbname_vlf NVARCHAR(128);
	DECLARE db_cursor_vlf CURSOR FOR 
	SELECT name FROM sys.databases WHERE state = 0 AND database_id > 4;

	CREATE TABLE #VLFTemp (
		RecoveryUnitId INT,
		FileId INT,
		FileSize BIGINT,
		StartOffset BIGINT,
		FSeqNo BIGINT,
		Status INT,
		Parity INT,
		CreateLSN NUMERIC(38)
	);

	OPEN db_cursor_vlf;
	FETCH NEXT FROM db_cursor_vlf INTO @dbname_vlf;

	WHILE @@FETCH_STATUS = 0
	BEGIN
		BEGIN TRY
			SET @sql = 'USE [' + @dbname_vlf + ']; INSERT INTO #VLFTemp EXEC sp_executesql N''DBCC LOGINFO WITH NO_INFOMSGS'';';
			EXEC sp_executesql @sql;
			INSERT INTO @VLFInfo SELECT @dbname_vlf, COUNT(*) FROM #VLFTemp;
			DELETE FROM #VLFTemp;
		END TRY
		BEGIN CATCH
			-- Skip databases we can't access
		END CATCH
		
		FETCH NEXT FROM db_cursor_vlf INTO @dbname_vlf;
	END

	CLOSE db_cursor_vlf;
	DEALLOCATE db_cursor_vlf;
	DROP TABLE #VLFTemp;

	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
	SELECT @now, DB_ID(DatabaseName), 'Excessive VLFs'
		, DatabaseName
		, CAST(VLFCount AS VARCHAR) + ' VLFs detected'
	FROM @VLFInfo
	WHERE VLFCount > 500;
END TRY
BEGIN CATCH
	RAISERROR (N'Error checking VLF counts',0,1) WITH NOWAIT;
END CATCH

/*---------------------------------------------------*//*---------------------------------------------------*/
/* ADD OUTPUT SECTIONS FOR NEW CHECKS */
/*---------------------------------------------------*//*---------------------------------------------------*/
/* Insert these before the "Check In" section around line 750 */

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Active Blocking')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Active Blocking','BlockingSession;Duration;Query',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Investigate immediately'[Next Action]
	FROM #DBA_issues di
	where issue_type='Active Blocking'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Database Corruption')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Database Corruption','Database;Page;Event',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Run DBCC CHECKDB immediately'[Next Action]
	FROM #DBA_issues di
	where issue_type='Database Corruption'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='DBCC CHECKDB Overdue')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','DBCC CHECKDB Overdue','Database;LastRun',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	, 'Schedule DBCC CHECKDB'[Next Action]
	FROM #DBA_issues di
	where issue_type='DBCC CHECKDB Overdue'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='TempDB Contention')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','TempDB Contention','WaitType;WaitCount;TotalTime',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'tempdb',issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Review TempDB configuration'[Next Action]
	FROM #DBA_issues di
	where issue_type='TempDB Contention'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Long Running Query')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Long Running Query','Session;Status;Query',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Investigate query performance'[Next Action]
	FROM #DBA_issues di
	where issue_type='Long Running Query'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Top Wait Type')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Top Wait Type','WaitType;WaitCount;TotalMinutes',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'',issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Analyze wait patterns'[Next Action]
	FROM #DBA_issues di
	where issue_type='Top Wait Type'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='AG Sync Issue')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','AG Sync Issue','Database;State;Lag',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Check AG health'[Next Action]
	FROM #DBA_issues di
	where issue_type='AG Sync Issue'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Critical Error')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Critical Error','LogDate;ProcessInfo;ErrorText',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'',issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Investigate error log'[Next Action]
	FROM #DBA_issues di
	where issue_type='Critical Error'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Outdated Statistics')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Outdated Statistics','Table;Statistic;LastUpdated',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Update statistics'[Next Action]
	FROM #DBA_issues di
	where issue_type='Outdated Statistics'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Memory Pressure')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Memory Pressure','Description;Count',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'',issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	, 'Review memory configuration'[Next Action]
	FROM #DBA_issues di
	where issue_type='Memory Pressure'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Heavy Fragmentation')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Heavy Fragmentation','Index;Fragmentation;Pages',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Rebuild/reorganize indexes'[Next Action]
	FROM #DBA_issues di
	where issue_type='Heavy Fragmentation'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Excessive VLFs')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Excessive VLFs','Database;VLFCount',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	, 'Shrink and regrow log file'[Next Action]
	FROM #DBA_issues di
	where issue_type='Excessive VLFs'
END
