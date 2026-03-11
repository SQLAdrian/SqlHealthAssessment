/* بسم الله الرحمن الرحيم  */
/* In the name of God, the Merciful, the Compassionate */


/*This script's intent has changed from looking for alerts to incoporating all default stuff that we want on a server
1. Database mail
2. Alerts for Corruption 
3. Set Model DB defaults
4. Daily Adaptive Cycle Error Logs

Last Update: 2020-10-21. Changed to Resolution Script, added VLF fixes


Other things:
https://www.vmware.com/content/dam/digitalmarketing/vmware/en/pdf/solutions/sql-server-on-vmware-best-practices-guide.pdf
--Netsh int tcp show global
--netsh interface tcp set global rss=enabled
*/

USE [master]
GO

DECLARE @UpdateOla BIT
DECLARE @AddMappedDrive BIT
DECLARE @NeedEmptyFile BIT
DECLARE @DoDeadlocks BIT

SET @DoDeadlocks = 0
SET @UpdateOla = 0
SET @AddMappedDrive = 0
SET @NeedEmptyFile = 0

DECLARE @Raiseme NVARCHAR(500) 
SET @Raiseme = ''
RAISERROR (@Raiseme,0,1) WITH NOWAIT;

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Capture current configs
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
IF OBJECT_ID('master.dbo.DBA_SYS_DATABASES') IS NULL
BEGIN
	SELECT GETDATE() [timestamp],* 
	INTO master.dbo.DBA_SYS_DATABASES
	FROM sys.databases
END 
ELSE
BEGIN
	INSERT INTO master.dbo.DBA_SYS_DATABASES
	SELECT GETDATE() [timestamp],* 
	FROM sys.databases
END

IF OBJECT_ID('master.dbo.DBA_SYS_DATABASE_FILES') IS NULL
BEGIN
	SELECT GETDATE() [Timestamp], SD.name [DB], SF.*
	INTO master.dbo.DBA_SYS_DATABASE_FILES
	FROM sys.sysaltfiles SF
	INNER JOIN sys.databases SD ON SD.database_id = SF.dbid
END 
ELSE
BEGIN
	INSERT  INTO master.dbo.DBA_SYS_DATABASE_FILES
	SELECT GETDATE() [Timestamp], SD.name [DB], SF.*
	FROM sys.sysaltfiles SF
	INNER JOIN sys.databases SD ON SD.database_id = SF.dbid
END

IF OBJECT_ID('master.dbo.DBA_SYS_TRACEFLAGS') IS NULL
BEGIN
	create table master.dbo.DBA_SYS_TRACEFLAGS ([name] int, [status] int, [global] int, [session] int)
	insert into master.dbo.DBA_SYS_TRACEFLAGS exec('dbcc tracestatus()')
END 
ELSE
BEGIN
	insert into master.dbo.DBA_SYS_TRACEFLAGS exec('dbcc tracestatus()')
END

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Create mapped drive if needed
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

IF @AddMappedDrive = 1
BEGIN
	/*Need a mapped drive for the SQL service?*/
	DECLARE @MappedDriveCMD NVARCHAR(200)
	DECLARE @MappedDriveDomainName NVARCHAR(200)

	DECLARE @MappedDriveDomainPassword NVARCHAR(200)
	SET @MappedDriveDomainName = 'DOMAIN\administrator'
	SET @MappedDriveDomainPassword = 'PASSWORD'
	SET @MappedDriveCMD = 'net use s: \\server\Backup\SQLServer /USER:'+@MappedDriveDomainName+' '+@MappedDriveDomainPassword+' /p:yes'
	EXEC sp_configure 'show advanced options', 1;
	RECONFIGURE;
	EXEC sp_configure 'xp_cmdshell',1
	RECONFIGURE
	EXEC xp_cmdshell @MappedDriveCMD

END
IF @NeedEmptyFile = 1
BEGIN
	RAISERROR ( 'How about creating a 10GB empty file',0,1) WITH NOWAIT;
	EXEC xp_cmdshell ' fsutil file createnew C:\MyEmergencyFile.dat 10737418240'
END

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Update Ola Jobs
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

 IF @UpdateOla = 1
 BEGIN
/*Adrian*/
DECLARE @schedule_id_01 int
  DECLARE @schedule_id_02 int
  DECLARE @schedule_id_03 int
  DECLARE @schedule_id_04 int
  DECLARE @schedule_id_05 int
  DECLARE @schedule_id_06 int
  DECLARE @RandomStartHours BIGINT
  DECLARE @RandomStartMinutes BIGINT
  DECLARE @RandomStartSeconds BIGINT
  DECLARE @RandomTime BIGINT
  DECLARE @StartStamp NVARCHAR(20)
SELECT @StartStamp = LEFT(REPLACE(CONVERT(NVARCHAR,GETDATE(),120),'-',''),8)

--@Numberoffiles=5
  
  
  IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Daily Backups')
  BEGIN
  SET @RandomStartHours = CONVERT(BIGINT,RAND()*2)*10000 /*Between 12:00 and 03:00 am*/
  SET @RandomStartMinutes = CONVERT(BIGINT,RAND()*59)*100 /*Between 00 ~ 60 minutes*/
  SET @RandomStartSeconds = CONVERT(BIGINT,RAND()*59) /*Between 00 ~ 60 seconds*/
  SET @RandomTime = @RandomStartHours + @RandomStartMinutes + @RandomStartSeconds 
   EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Daily Backups', @enabled=1, @freq_type=4, @freq_interval=1
	  , @freq_subday_type=8, @freq_subday_interval=12, @freq_relative_interval=0, @freq_recurrence_factor=1
	  , @active_start_date=@StartStamp, @active_end_date=99991231, @active_start_time=@RandomTime, @active_end_time=235959, @schedule_id = @schedule_id_01 OUTPUT
  	END
	ELSE
	BEGIN
		SELECT @schedule_id_01 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Daily Backups'
	END
	
	IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Weekly Backups')
	  BEGIN
	  SET @RandomStartHours = CONVERT(BIGINT,RAND()*2)*10000 /*Between 12:00 and 03:00 am*/
	  SET @RandomStartMinutes = CONVERT(BIGINT,RAND()*59)*100 /*Between 00 ~ 60 minutes*/
	  SET @RandomStartSeconds = CONVERT(BIGINT,RAND()*59) /*Between 00 ~ 60 seconds*/
	  SET @RandomTime = @RandomStartHours + @RandomStartMinutes + @RandomStartSeconds 
	  EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Weekly Backups', @enabled=1, @freq_type=8, @freq_interval=64
  , @freq_subday_type=1, @freq_subday_interval=0, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp
  , @active_end_date=99991231, @active_start_time=100000, @active_end_time=235959, @schedule_id = @schedule_id_06 OUTPUT

  	END
	ELSE
	BEGIN
		SELECT @schedule_id_06 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Weekly Backups'
	END
	
  IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Log Backups')
  BEGIN
  EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Log Backups', @enabled=1, @freq_type=4, @freq_interval=1, @freq_subday_type=4
  , @freq_subday_interval=15, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp, @active_end_date=99991231
  , @active_start_time=0, @active_end_time=235959, @schedule_id = @schedule_id_02 OUTPUT
  	END
	ELSE
	BEGIN
		SELECT @schedule_id_02 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Log Backups'
	END

  IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Daily DBCC')
  BEGIN
  SET @RandomStartHours = CONVERT(BIGINT,((RAND()*4)+19))*10000 /*Between 19:00 and 00:00 am*/
  SET @RandomStartMinutes = CONVERT(BIGINT,RAND()*59)*100 /*Between 00 ~ 60 minutes*/
  SET @RandomStartSeconds = CONVERT(BIGINT,RAND()*59) /*Between 00 ~ 60 seconds*/
  SET @RandomTime = @RandomStartHours + @RandomStartMinutes + @RandomStartSeconds
  EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Daily DBCC', @enabled=1, @freq_type=4, @freq_interval=1
  , @freq_subday_type=1, @freq_subday_interval=0, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp
  , @active_end_date=99991231, @active_start_time=@RandomTime, @active_end_time=235959, @schedule_id = @schedule_id_03 OUTPUT
  	END
	ELSE
	BEGIN
		SELECT @schedule_id_03 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Daily DBCC'
	END

  IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Daily Maintenance')
  BEGIN 
  SET @RandomStartHours = CONVERT(BIGINT,((RAND()*2)+3))*10000 /*Between 03:00 and 05:00 am*/
  SET @RandomStartMinutes = CONVERT(BIGINT,RAND()*59)*100 /*Between 00 ~ 60 minutes*/
  SET @RandomStartSeconds = CONVERT(BIGINT,RAND()*59) /*Between 00 ~ 60 seconds*/
  SET @RandomTime = @RandomStartHours + @RandomStartMinutes + @RandomStartSeconds
  EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Daily Maintenance', @enabled=1, @freq_type=4, @freq_interval=1
  , @freq_subday_type=1, @freq_subday_interval=0, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp
  , @active_end_date=99991231, @active_start_time=@RandomTime, @active_end_time=235959, @schedule_id = @schedule_id_04 OUTPUT
  	END
	ELSE
	BEGIN
		SELECT @schedule_id_04 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Weekly Maintenance'
	END
  IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Weekly Maintenance')
  BEGIN
  SET @RandomStartHours = CONVERT(BIGINT,((RAND()*2)+1))*10000 /*Between 02:00 and 03:00 am*/
  SET @RandomStartMinutes = CONVERT(BIGINT,RAND()*59)*100 /*Between 00 ~ 60 minutes*/
  SET @RandomStartSeconds = CONVERT(BIGINT,RAND()*59) /*Between 00 ~ 60 seconds*/
  SET @RandomTime = @RandomStartHours + @RandomStartMinutes + @RandomStartSeconds
  EXEC msdb.dbo.sp_add_schedule @schedule_name=N'SQLDBA.ORG - Weekly Maintenance', @enabled=1, @freq_type=8, @freq_interval=64
  , @freq_subday_type=1, @freq_subday_interval=0, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp
  , @active_end_date=99991231, @active_start_time=@RandomTime, @active_end_time=235959, @schedule_id = @schedule_id_05 OUTPUT
	END
	ELSE
	BEGIN
		SELECT @schedule_id_05 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = N'SQLDBA.ORG - Weekly Maintenance'
	END

DECLARE @the_job_Id BINARY(16)
DECLARE @the_job_name NVARCHAR(200)

SET @the_job_name =  'IndexOptimize - USER_DATABASES'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
	EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_04
	EXEC msdb.dbo.sp_update_jobstep @job_id=@the_job_Id, @step_id=1 , 
		@command=N'EXECUTE [dbo].[IndexOptimize] @Databases = ''ALL_DATABASES'',@FragmentationHigh = ''INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE'', @FragmentationMedium = ''INDEX_REORGANIZE,INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE'', @FragmentationLevel1 = 5, @FragmentationLevel2 = 30,@OnlyModifiedStatistics = ''Y'', @UpdateStatistics=''ALL'', @LogToTable = ''Y'''
		--EXECUTE [dbo].[IndexOptimize] @Databases = 'ALL_DATABASES',@FragmentationHigh = 'INDEX_REORGANIZE,INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE', @FragmentationMedium = 'INDEX_REBUILD_ONLINE,INDEX_REBUILD_OFFLINE', @FragmentationLevel1 = 5, @FragmentationLevel2 = 30,@OnlyModifiedStatistics = 'Y', @UpdateStatistics='ALL', @LogToTable = 'Y'

END



---@availabilitygroupDirectoryStructure='{servername}${instancename}{directoryseparator}{databaseName}{directoryseparator}{backuptype}'               




SET @the_job_name =  'DatabaseBackup - SYSTEM_DATABASES - FULL'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name 
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_01
END

SET @the_job_name =  'DatabaseBackup - USER_DATABASES - FULL'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_06
END

SET @the_job_name =  'DatabaseBackup - USER_DATABASES - DIFF'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
  SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_01
END

SET @the_job_name =  'DatabaseBackup - USER_DATABASES - LOG'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_02
END

SET @the_job_name =  'DatabaseIntegrityCheck - SYSTEM_DATABASES'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_03
END

SET @the_job_name =  'DatabaseIntegrityCheck - USER_DATABASES'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_03
END

SET @the_job_name =  'sp_delete_backuphistory'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
	SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
	EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_05
  	EXEC msdb.dbo.sp_update_jobstep @job_id=@the_job_Id, @step_id=1 , 
	@command=N'DECLARE @CleanupDate datetime 
	SET @CleanupDate = DATEADD(dd,-90,GETDATE())
	EXECUTE dbo.sp_delete_backuphistory @oldest_date = @CleanupDate', @subsystem=N'TSQL'
END

SET @the_job_name =  'sp_purge_jobhistory'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
	SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
	EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_05
END

SET @the_job_name =  'Output File Cleanup'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN  
	SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
	EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_04
END

SET @the_job_name =  'CommandLog Cleanup'
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN 
		SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_05
END

  /* <<< OLA stuff up to here*/
END

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Creating deadlock capture thingy
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

IF @DoDeadlocks = 1 
BEGIN
	RAISERROR ('Creating deadlock capture thingy',0,1) WITH NOWAIT;
	/*https://social.technet.microsoft.com/wiki/contents/articles/38068.sql-server-capture-database-deadlock-by-xevent-job-alerts-and-send-email-to-you.aspx*/
	Use master 
	
	IF OBJECT_ID('master.dbo.DBA_DEADLOCKS') IS NULL
	BEGIN
		CREATE TABLE [master].[dbo].[DBA_DEADLOCKS](
		[no] [int] IDENTITY(1,1) NOT NULL,
		[deadlock_timeout] [datetime] NULL,
		[deadlock1_id] NVARCHAR(100) NULL,
		[deadlock1_duration] [float] NULL,
		[deadlock1_transactionname] NVARCHAR(100) NULL,
		[deadlock1_locktype] NVARCHAR(50) NULL,
		[deadlock1_clientapp] NVARCHAR(200) NULL,
		[deadlock1_hostname] NVARCHAR(50) NULL,
		[deadlock1_loginname] NVARCHAR(50) NULL,
		[deadlock1_query] NVARCHAR(max) NULL,
		[deadlock2_id] NVARCHAR(100) NULL,
		[deadlock2_duration] [float] NULL,
		[deadlock2_transactionname] NVARCHAR(100) NULL,
		[deadlock2_locktype] NVARCHAR(50) NULL,
		[deadlock2_clientapp] NVARCHAR(200) NULL,
		[deadlock2_hostname] NVARCHAR(50) NULL,
		[deadlock2_loginname] NVARCHAR(50) NULL,
		[deadlock2_query] NVARCHAR(max) NULL,
		PRIMARY KEY CLUSTERED
		(
		[no] ASC
		) ON [PRIMARY]
		) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
	

		CREATE NONCLUSTERED INDEX [IX_SQLDBA_deadlocktimeout] ON [dbo].[DBA_DEADLOCKS]
		(
		[deadlock_timeout] ASC
		) ON [PRIMARY]
	END

	IF NOT EXISTS (SELECT * FROM master.sys.objects WHERE type = 'P' AND OBJECT_ID = OBJECT_ID('dbo.spDeadLockReport'))
	BEGIN
		EXEC('CREATE PROCEDURE [dbo].[spDeadLockReport] AS BEGIN SET NOCOUNT ON; END')
	END
	EXEC('ALTER PROCEDURE [dbo].[spDeadLockReport]
	AS
	BEGIN
		DECLARE @mStartDate DATETIME;
		SET @mStartDate=(SELECT isnull(MAX([deadlock_timeout]),GETDATE()-1) FROM master.dbo.[DBA_DEADLOCKS])
	
		INSERT INTO master.dbo.[DBA_DEADLOCKS]([deadlock_timeout], [deadlock1_id], [deadlock1_duration], [deadlock1_transactionname], [deadlock1_locktype], [deadlock1_clientapp], [deadlock1_hostname], [deadlock1_loginname], [deadlock1_query], [deadlock2_id], [deadlock2_duration], [deadlock2_transactionname], [deadlock2_locktype], [deadlock2_clientapp], [deadlock2_hostname], [deadlock2_loginname], [deadlock2_query])
		SELECT x.y.value(''(@timestamp)[1]'', ''datetime'')                                                        ''[deadlock_timeout]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@id)[1]'', ''NVARCHAR(100)'')               ''[deadlock1_id]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@waittime)[1]'', ''float'') / 1000          ''[deadlock1_duration]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@transactionname)[1]'', N''NVARCHAR(100)'') AS ''[deadlock1_transactionname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@lockMode)[1]'', N''NVARCHAR(50)'')         AS ''[deadlock1_locktype]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@clientapp)[1]'', N''NVARCHAR(200)'')       AS ''[deadlock1_clientapp]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@hostname)[1]'', N''NVARCHAR(50)'')         AS ''[deadlock1_hostname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@loginname)[1]'', N''NVARCHAR(50)'')        AS ''[deadlock1_loginname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/inputbuf)[1]'', N''NVARCHAR(max)'')         AS ''[deadlock1_query]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@id)[2]'', ''NVARCHAR(100)'')               ''[deadlock2_id]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@waittime)[2]'', ''float'') / 1000          ''[deadlock2_duration]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@transactionname)[2]'', N''NVARCHAR(100)'') AS ''[deadlock2_transactionname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@lockMode)[2]'', N''NVARCHAR(50)'')         AS ''[deadlock2_locktype]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@clientapp)[2]'', N''NVARCHAR(200)'')       AS ''[deadlock2_clientapp]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@hostname)[2]'', N''NVARCHAR(50)'')         AS ''[deadlock2_hostname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/@loginname)[2]'', N''NVARCHAR(50)'')        AS ''[deadlock2_loginname]'',
		x.y.value(''(./data/value/deadlock/process-list/process/inputbuf)[2]'', N''NVARCHAR(max)'')         AS ''[deadlock2_query]''
		FROM   (SELECT Cast([target_data] AS XML) [target_data]
			FROM   sys.dm_xe_session_targets AS st
				INNER JOIN sys.dm_xe_sessions AS s
						ON s.[address] = st.[event_session_address]
			WHERE  s.[name] = ''DeadlockReport'') AS [deadlock]
		CROSS APPLY [target_data].nodes(''/RingBufferTarget/event'') AS x(y)
		WHERE  x.y.query(''.'').exist(''/event[@timestamp > sql:variable("@mStartDate") and @name="xml_deadlock_report"]'') = 1
	END')
	--CREATE DEADLOCK XEVENT
	IF NOT EXISTS (SELECT *
		FROM sys.server_event_sessions
		WHERE name = 'DeadlockReport')
	BEGIN

		CREATE EVENT SESSION [DeadlockReport] ON SERVER
		ADD EVENT sqlserver.xml_deadlock_report
		ADD TARGET package0.ring_buffer
		WITH (MAX_MEMORY=4096 KB,EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS,
		MAX_DISPATCH_LATENCY=1 SECONDS,MAX_EVENT_SIZE=0 KB,MEMORY_PARTITION_MODE=NONE,
		TRACK_CAUSALITY=OFF,STARTUP_STATE=OFF)
	END

	IF EXISTS (SELECT *
		FROM sys.server_event_sessions
		WHERE name = 'DeadlockReport')
	BEGIN TRY
	--XEVENT START
		ALTER EVENT SESSION [DeadlockReport] ON SERVER
		STATE = START
	END TRY
	BEGIN CATCH
		RAISERROR ('DeadlockReport Already started.. it seems',0,1) WITH NOWAIT;
	END CATCH
END


USE [msdb]
GO



DECLARE @ReturnCode INT
SELECT @ReturnCode = 0
DECLARE @the_job_Id BINARY(16)
DECLARE @the_job_name NVARCHAR(200)
SET @the_job_name =  '996. Deadlocks'

SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name 

IF NOT EXISTS (SELECT name FROM msdb.dbo.syscategories WHERE name=N'[Uncategorized (Local)]' AND category_class=1)
	EXEC @ReturnCode = msdb.dbo.sp_add_category @class=N'JOB', @type=N'LOCAL', @name=N'[Uncategorized (Local)]'
 
-- Get the server name
DECLARE @ServerName sysname 
SET @ServerName = (SELECT @@SERVERNAME);

IF NOT EXISTS(SELECT job_id,*  FROM msdb.dbo.sysjobs WHERE [name] =  '996. Deadlocks')
BEGIN
	DECLARE @jobId BINARY(16)
	EXEC @ReturnCode =  msdb.dbo.sp_add_job @job_name=N'996. Deadlocks',
	@enabled=1,
	@notify_level_eventlog=2,
	@notify_level_email=0,
	@notify_level_netsend=0,
	@notify_level_page=0,
	@delete_level=0,
	@description=N'no description',
	@category_name=N'[Uncategorized (Local)]',
	@owner_login_name=N'sa', @job_id = @jobId OUTPUT

	EXEC @ReturnCode = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'deadlock',
	@step_id=1,
	@cmdexec_success_code=0,
	@on_success_action=1,
	@on_success_step_id=0,
	@on_fail_action=2,
	@on_fail_step_id=0,
	@retry_attempts=0,
	@retry_interval=0,
	@os_run_priority=0, @subsystem=N'TSQL',
	@command=N'EXEC master.dbo.spDeadLockReport',
	@database_name=N'master',
	@flags=0
 
	EXEC @ReturnCode = msdb.dbo.sp_update_job @job_id = @jobId, @start_step_id = 1
	EXEC @ReturnCode = msdb.dbo.sp_add_jobserver @job_id = @jobId, @server_name = N'(local)'
END

SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name 
BEGIN TRY
	EXEC msdb.dbo.sp_add_alert @name=N'[SQLDBA]Deadlock Alerts', 
		@message_id=0, 
		@severity=0, 
		@enabled=1, 
		@delay_between_responses=0, 
		@include_event_description_in=0, 
		@category_name=N'[Uncategorized]', 
		@performance_condition=N'Locks|Number of Deadlocks/sec|_Total|>|0', 
		@job_id=@the_job_Id
	EXEC msdb.dbo.sp_update_notification @alert_name=N'[SQLDBA]Deadlock Alerts', @operator_name=N'SQLDBA', @notification_method = 1

END TRY
BEGIN CATCH
	RAISERROR ('[SQLDBA]Deadlock Alerts already exists',0,1) WITH NOWAIT;
	EXEC msdb.dbo.sp_update_alert @name=N'[SQLDBA]Deadlock Alerts', 
			@message_id=0, 
			@severity=0, 
			@enabled=1, 
			@delay_between_responses=0, 
			@include_event_description_in=1, 
			@database_name=N'', 
			@notification_message=N'Deadlock triggered', 
			@event_description_keyword=N'', 
			@performance_condition=N'Locks|Number of Deadlocks/sec|_Total|>|0', 
			@wmi_namespace=N'', 
			@wmi_query=N'', 
			@job_id=@the_job_Id
	EXEC msdb.dbo.sp_update_notification @alert_name=N'[SQLDBA]Deadlock Alerts', @operator_name=N'SQLDBA', @notification_method = 1


END CATCH

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Enable Database Mail
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
/* We will assume you have Datbase Mail enabled and configured*/
/*Thanks:
Brent Ozar Unlimited, https://www.brentozar.com/blitz/configure-sql-server-alerts/
@KeefOnToast and Chuck
*/


GO
/*Enable Advanced options*/
EXEC sys.sp_configure N'show advanced options', N'1'
RECONFIGURE WITH OVERRIDE
GO
/*Enable Database Mail*/
EXEC sp_configure 'Database Mail XPs', 1
RECONFIGURE WITH OVERRIDE
RAISERROR ('Action: Enabled database mail',0,1) WITH NOWAIT;
GO


USE [master]
GO
-- Get the server name
DECLARE @ServerName sysname 
SET @ServerName = (SELECT @@SERVERNAME);
/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Now create alerts
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
---------------------------------------------------------------------------------------------------------
/* Adapted by Adrian Sullivan from Glenn Berry's script*/

-- Add important SQL Agent Alerts to your instance

-- This will work with SQL Server 2008 and newer
-- Glenn Berry
-- SQLskills.com
-- Last Modified: August 11, 2014
-- http://sqlserverperformance.wordpress.com/
-- http://sqlskills.com/blogs/glenn/
-- Twitter: GlennAlanBerry

-- Listen to my Pluralsight courses
-- http://www.pluralsight.com/author/glenn-berry


DECLARE @Operator NVARCHAR(500);
DECLARE @DynamicSQL NVARCHAR(4000);
DECLARE @Severity TINYINT;
DECLARE @AlertName NVARCHAR(500);
DECLARE @StepDescription NVARCHAR(500);
DECLARE @WhereToSend TINYINT;
DECLARE @SQLDBANotification NVARCHAR(200);
SET @SQLDBANotification= N'Possible P2/P3. Assign to SQL Engineers/DBA';

-- Change @OperatorName as needed
DECLARE @OperatorName sysname 
SET @OperatorName= N'SQLDBA';


SET @WhereToSend = 1 /*Change this to 7 to cover Email(1), Pager(2) and Net Send(4)*/
SET @Operator = @OperatorName;
SET @DynamicSQL =' EXEC msdb.dbo.sp_add_operator @name=N''' + @Operator + ''', 
@enabled=1, 
@weekday_pager_start_time=90000, 
@weekday_pager_end_time=180000, 
@saturday_pager_start_time=90000, 
@saturday_pager_end_time=180000, 
@sunday_pager_start_time=90000, 
@sunday_pager_end_time=180000, 
@pager_days=0, 
@email_address=N''alerts@sqldba.org'', 
@category_name=N''[Uncategorized]''; '
BEGIN TRY
		EXEC sp_executesql @DynamicSQL;
		SET @StepDescription = 'Operator created: ' + @Operator;
END TRY
BEGIN CATCH
		SET @StepDescription = 'Operator already exists for: ' + @Operator;
		EXEC msdb.dbo.sp_update_operator @name=N'SQLDBA', 
		@enabled=1, 
		@pager_days=0, 
		@email_address=N'alerts@sqldba.org', 
		@pager_address=N''

END CATCH
RAISERROR (@StepDescription,0,1) WITH NOWAIT;

-- Make sure you have an Agent Operator defined that matches the name you supplied
IF NOT EXISTS(SELECT * FROM msdb.dbo.sysoperators WHERE name = @OperatorName)
    BEGIN
        RAISERROR ('There is no SQL Operator with a name of %s' , 18 , 16 , @OperatorName);
        RETURN;
    END

-- Change @CategoryName as needed
DECLARE @CategoryName sysname 
SET @CategoryName = N'SQL Server Agent Alerts';


-- Add Alert Category if it does not exist
IF NOT EXISTS (SELECT *
               FROM msdb.dbo.syscategories
               WHERE category_class = 2  -- ALERT
               AND category_type = 3
               AND name = @CategoryName)
    BEGIN
        EXEC msdb.dbo.sp_add_category @class = N'ALERT', @type = N'NONE', @name = @CategoryName;
    END




-- Alert Names start with the name of the server 



DECLARE @AlertTable TABLE (ID INT IDENTITY(1,1), AlertType NVARCHAR(50), TheNumber INT,AlertName sysname)
INSERT INTO @AlertTable VALUES ('Severity',19, 	@ServerName + N' Alert - Sev 19 Error: Fatal Error in Resource')
INSERT INTO @AlertTable VALUES ('Severity',20, 	@ServerName + N' Alert - Sev 20 Error: Fatal Error in Current Process')
INSERT INTO @AlertTable VALUES ('Severity',21, 	@ServerName + N' Alert - Sev 21 Error: Fatal Error in Database Process')
INSERT INTO @AlertTable VALUES ('Severity',22, 	@ServerName + N' Alert - Sev 22 Error: Fatal Error: Table Integrity Suspect')
INSERT INTO @AlertTable VALUES ('Severity',23, 	@ServerName + N' Alert - Sev 23 Error: Fatal Error Database Integrity Suspect')
INSERT INTO @AlertTable VALUES ('Severity',24, 	@ServerName + N' Alert - Sev 24 Error: Fatal Hardware Error')
INSERT INTO @AlertTable VALUES ('Severity',25, 	@ServerName + N' Alert - Sev 25 Error: Fatal Error')



INSERT INTO @AlertTable VALUES ('Error',823, 	@ServerName + N' Alert - Error 823: The operating system returned an error')
INSERT INTO @AlertTable VALUES ('Error',824, 	@ServerName + N' Alert - Error 824: Logical consistency-based I/O error')
INSERT INTO @AlertTable VALUES ('Error',825, 	@ServerName + N' Alert - Error 825: Read-Retry Required')
INSERT INTO @AlertTable VALUES ('Error',832, 	@ServerName + N' Alert - Error 832: Constant page has changed')
INSERT INTO @AlertTable VALUES ('Error',855, 	@ServerName + N' Alert - Error 855: Uncorrectable hardware memory corruption detected')
INSERT INTO @AlertTable VALUES ('Error',856, 	@ServerName + N' Alert - Error 856: SQL Server has detected hardware memory corruption, but has recovered the page')
INSERT INTO @AlertTable VALUES ('Error',1205, 	@ServerName + N' Alert - Error 1205: Deadlock')
INSERT INTO @AlertTable VALUES ('Error',3928, 	@ServerName + N' Alert - Error 3928: Deadlock')
 
 
INSERT INTO @AlertTable VALUES ('Error',35265, 	@ServerName + N' Alert - AG 35265: AG Data Movement - Resumed')
INSERT INTO @AlertTable VALUES ('Error',35264, 	@ServerName + N' Alert - AG 35264: AG Data Movement - Suspended')
INSERT INTO @AlertTable VALUES ('Error',28034, 	@ServerName + N' Alert - AG 28034: Connection handshake on broker')
INSERT INTO @AlertTable VALUES ('Error',1480, 	@ServerName + N' Alert - AG 1480: AG Role Change' )
INSERT INTO @AlertTable VALUES ('Error',41091, 	@ServerName + N' Alert - AG 41091: Replica Going Offline')
INSERT INTO @AlertTable VALUES ('Error',41131, 	@ServerName + N' Alert - AG 41131: Failed to Bring AG ONLINE')
INSERT INTO @AlertTable VALUES ('Error',41142, 	@ServerName + N' Alert - AG 41142: Replica Cannot become primary')
INSERT INTO @AlertTable VALUES ('Error',41406, 	@ServerName + N' Alert - AG 41406: AG not Ready for Auto Failover')
INSERT INTO @AlertTable VALUES ('Error',41414, 	@ServerName + N' Alert - AG 41414: Secondary not Connected')
INSERT INTO @AlertTable VALUES ('Error',35264, 	@ServerName + N' Alert - Error 35264: AG data movement for database has been suspended ' )
INSERT INTO @AlertTable VALUES ('Error',983, 	@ServerName + N' Alert - Error 983: Unable to access availability database'   )
INSERT INTO @AlertTable VALUES ('Error',35276, 	@ServerName + N' Alert - Error 35276: Failed to allocate and schedule an AG task for database'  )
INSERT INTO @AlertTable VALUES ('Error',41091, 	@ServerName + N' Alert - Error 41091: AG lease expired or lease renewal failed'  )
INSERT INTO @AlertTable VALUES ('Error',41406, 	@ServerName + N' Alert - Error 41406: The availability group is not ready for automatic failover.'  )

INSERT INTO @AlertTable VALUES ('Error',19407, 	@ServerName + N' Alert - AG - Cluster connectivity issue.' )-- The lease between the SQL availability group and the Windows Server Failover Cluster has expired.'  )
INSERT INTO @AlertTable VALUES ('Error',19419, 	@ServerName + N' Alert - AG - Cluster to SQL lease timeout.' )--  Failover Cluster did not receive a process event signal from SQL Server hosting availability group within the lease timeout period.'  )
INSERT INTO @AlertTable VALUES ('Error',19421, 	@ServerName + N' Alert - AG - SQL to Cluster lease timeout.' )--  SQL availability group did not receive a process event signal from the Failover Cluster within the lease timeout period.'  )
INSERT INTO @AlertTable VALUES ('Error',19422, 	@ServerName + N' Alert - AG - AG lease renewal failed.' )--  SQL availability group and the Windows Server Failover Cluster failed because SQL Server encountered Windows error with error code.'  )
INSERT INTO @AlertTable VALUES ('Error',41143, 	@ServerName + N' Alert - AG - AG replica is in a failed state.' )--   A previous operation to read or update persisted configuration data for the availability group has failed.  To recover from this failure, either restart the local Windows Server Failover Clustering (WSFC) service or restart the local instance of SQL Server.'  )
INSERT INTO @AlertTable VALUES ('Error',41005, 	@ServerName + N' Alert - AG - Failed to obtain Failover Clustering (WSFC) resource handle.' )--  The WSFC service may not be running or may not be accessible in its current state.'  )
INSERT INTO @AlertTable VALUES ('Error',41144, 	@ServerName + N' Alert - AG - Local AG replica is in a failed state.' )--   The replica failed to read or update the persisted configuration data. To recover from this failure, either restart the local Windows Server Failover Clustering (WSFC) service or restart the local instance of SQL Server.'  )


INSERT INTO @AlertTable VALUES ('Error',9002, 	@ServerName + N' Alert - Error 9002: Log File FULL')
INSERT INTO @AlertTable VALUES ('Error',1101,	@ServerName + N' Alert - Error 1101: Database filegroup out of space')
INSERT INTO @AlertTable VALUES ('Error',3041,	@ServerName + N' Alert - Error 3041: - BACKUP failed to complete. Check the backup application log for detailed messages.')
INSERT INTO @AlertTable VALUES ('Error',12412,	@ServerName + N' Alert - Error 12412:- Internal table access error. Failed to access the Query Store internal table.')
INSERT INTO @AlertTable VALUES ('Error',18210,	@ServerName + N' Alert - Error 18210:- Failure on backup device. Operating system error.')

INSERT INTO @AlertTable VALUES ('Error',28036, 	@ServerName + N' Alert - Error 28036: Connection handshake failed.' )--  The certificate used by this endpoint was not found')
INSERT INTO @AlertTable VALUES ('Error',610, 	@ServerName + N' Alert - Error 610: Invalid header value from a page.' )--  Run DBCC CHECKDB to check for a data corruption.'   )
INSERT INTO @AlertTable VALUES ('Error',2511, 	@ServerName + N' Alert - Error 2511: Table error. Keys out of order on page.' )-- 
INSERT INTO @AlertTable VALUES ('Error',5228, 	@ServerName + N' Alert - Error 5228: Table error. DBCC detected incomplete cleanup.' )-- 
INSERT INTO @AlertTable VALUES ('Error',5229, 	@ServerName + N' Alert - Error 5229: Table error. contains an anti-matter column.' )-- 
INSERT INTO @AlertTable VALUES ('Error',5242, 	@ServerName + N' Alert - Error 5242: An inconsistency was detected during an internal operation in database.' )-- 
INSERT INTO @AlertTable VALUES ('Error',5243, 	@ServerName + N' Alert - Error 5243: An inconsistency was detected during an internal operation.' )-- 
INSERT INTO @AlertTable VALUES ('Error',5250, 	@ServerName + N' Alert - Error 5250: Database error. This error cannot be repaired.' )-- 


DECLARE @MaxAlerts TINYINT
DECLARE @AlertCounter TINYINT 
SET @AlertCounter = 1
DECLARE @ThisName sysname
DECLARE @ThisAlert INT
DECLARE @ThisMessage INT
DECLARE @ThisAlertType NVARCHAR(50)
SELECT @MaxAlerts = MAX(ID) FROM @AlertTable
USE msdb
WHILE @AlertCounter < @MaxAlerts
BEGIN
	SELECT @ThisName = AlertName
	, @ThisAlert = TheNumber
	, @ThisAlertType = AlertType
	FROM @AlertTable WHERE ID = @AlertCounter
	IF @ThisAlertType = 'Error'
	BEGIN
		SET @ThisMessage = @ThisAlert
		SET @ThisAlert = 0
	END
	IF NOT EXISTS (SELECT name FROM msdb.dbo.sysalerts WHERE name = @ThisName)
	BEGIN
		IF @ThisAlert <> 0
			RAISERROR (@ThisAlert,0,1) WITH NOWAIT;
		BEGIN TRY
	    EXEC msdb.dbo.sp_add_alert @name = @ThisName, 
	                  @message_id = @ThisMessage, @severity = @ThisAlert, @enabled = 1, 
	                  @delay_between_responses = 900, @include_event_description_in = 1,@notification_message=@SQLDBANotification,
	                  @category_name = @CategoryName, 
	                 @job_id = N'00000000-0000-0000-0000-000000000000';
		IF NOT EXISTS(SELECT *
              FROM msdb.dbo.sysalerts AS sa
              INNER JOIN msdb.dbo.sysnotifications AS sn
              ON sa.id = sn.alert_id
              WHERE (sa.name = @ThisName
			  OR sa.message_id = @ThisAlert))
		BEGIN
			EXEC msdb.dbo.sp_add_notification @alert_name = @ThisName, @operator_name = @OperatorName, @notification_method = 1;
			--EXEC msdb.dbo.sp_add_notification @alert_name = @ThisName, @operator_name = @ServiceDesk, @notification_method = 1;
			SET @ThisName = 'Configure alert - ' + @ThisName
			
			/*Ensure these errprs log for SCOM*/
			IF @ThisAlertType = 'Error'
				EXEC sp_altermessage @ThisAlert, 'WITH_LOG', 'true'

			RAISERROR (@ThisName,0,1) WITH NOWAIT;
		END

		END TRY
		BEGIN CATCH
			SET @ThisName = 'Failed to configure alert - ' + @ThisName
			RAISERROR (@ThisName,16,1) WITH NOWAIT;
		END CATCH
	END		  
	SET @AlertCounter = @AlertCounter + 1
END


-- Error 823: Operating System Error
-- How to troubleshoot a Msg 823 error in SQL Server	
-- http://support.microsoft.com/kb/2015755

-- Error 824: Logical consistency-based I/O error
-- How to troubleshoot Msg 824 in SQL Server
-- http://support.microsoft.com/kb/2015756

-- Error 825: Read-Retry Required
-- How to troubleshoot Msg 825 (read retry) in SQL Server
-- http://support.microsoft.com/kb/2015757

-- Error 832: Constant page has changed
-- http://www.sqlskills.com/blogs/paul/dont-confuse-error-823-and-error-832/
-- http://support.microsoft.com/kb/2015759


-- Memory Error Correction alerts added on 10/30/2013

-- Mitigation of RAM Hardware Errors
-- When SQL Server 2012 Enterprise Edition is installed on a Windows 2012 operating system with hardware that supports bad memory diagnostics, 
-- you will notice new error messages like 854, 855, and 856 instead of the 832 errors that LazyWriter usually generates.
-- Error 854 is just informing you that your instance supports memory error correction

-- Using SQL Server in Windows 8 and Windows Server 2012 environments
-- http://support.microsoft.com/kb/2681562









/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- CYCLE daily Error Logs
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
IF  EXISTS (SELECT job_id FROM msdb.dbo.sysjobs_view WHERE name = N'Daily Cycle Errorlog')
EXEC msdb.dbo.sp_delete_job @job_name=N'Daily Cycle Errorlog', @delete_unused_schedule=1
BEGIN TRANSACTION

-- Set the Operator name to receive notifications, if any. Set the job owner, if not sa.
DECLARE @jobowner sysname
SET @jobowner = 'sa'

DECLARE @ReturnCode INT
SELECT @ReturnCode = 0

IF NOT EXISTS (SELECT name FROM msdb.dbo.syscategories WHERE name=N'Database Maintenance' AND category_class=1)
BEGIN
EXEC @ReturnCode = msdb.dbo.sp_add_category @class=N'JOB', @type=N'LOCAL', @name=N'Database Maintenance'
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback

END

DECLARE @jobId BINARY(16)
IF EXISTS (SELECT name FROM msdb.dbo.sysoperators WHERE name = @OperatorName)
BEGIN
	EXEC @ReturnCode =  msdb.dbo.sp_add_job @job_name=N'Daily Cycle Errorlog', 
		@enabled=1, 
		@notify_level_eventlog=2, 
		@notify_level_email=2, 
		@notify_level_netsend=2, 
		@notify_level_page=2, 
		@delete_level=0, 
		@description=N'Cycles Errorlog when its size is over 20MB or its age over 15 days.', 
		@category_name=N'Database Maintenance', 
		@owner_login_name=@jobowner, 
		@notify_email_operator_name=@OperatorName,
		@job_id = @jobId OUTPUT
	IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
END
ELSE
BEGIN
	EXEC @ReturnCode =  msdb.dbo.sp_add_job @job_name=N'Daily Cycle Errorlog', 
		@enabled=1, 
		@notify_level_eventlog=2, 
		@notify_level_email=3, 
		@notify_level_netsend=0, 
		@notify_level_page=0, 
		@delete_level=0, 
		@description=N'Cycles Errorlog when its size is over 20MB or its age over 15 days.', 
		@category_name=N'Database Maintenance', 
		@owner_login_name=@jobowner,
		@job_id = @jobId OUTPUT
	IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
END

EXEC @ReturnCode = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Adaptive Cycle Errorlog', 
		@step_id=1, 
		@cmdexec_success_code=0, 
		@on_success_action=1, 
		@on_success_step_id=0, 
		@on_fail_action=2, 
		@on_fail_step_id=0, 
		@retry_attempts=0, 
		@retry_interval=0, 
		@os_run_priority=0, @subsystem=N'TSQL', 
		@command=N'SET NOCOUNT ON;
DECLARE @CycleMessage VARCHAR(255), @return_value int, @Output VARCHAR(32)
DECLARE @ErrorLogs TABLE (ArchiveNumber tinyint, DateCreated DATETIME, LogFileSizeBytes int)
INSERT into @ErrorLogs (ArchiveNumber, DateCreated, LogFileSizeBytes )
EXEC master.dbo.sp_enumerrorlogs

SELECT @CycleMessage = ''Current SQL Server ErrorLog was created on '' + CONVERT(VARCHAR, DateCreated , 105) + '' and is using '' +
CASE WHEN LogFileSizeBytes BETWEEN 1024 AND 1048575 THEN CAST(LogFileSizeBytes/1024 AS VARCHAR(10)) + '' KB.''
WHEN LogFileSizeBytes > 1048575 THEN CAST((LogFileSizeBytes/1024)/1024 AS VARCHAR(10)) + '' MB.''
ELSE CAST(LogFileSizeBytes AS VARCHAR(4)) + '' Bytes.''
END 
+ CASE WHEN LogFileSizeBytes > 20971520 THEN '' The ErrorLog will be cycled because of its size.'' -- over 20MB
WHEN DateCreated <= DATEADD(dd, -15,GETDATE()) THEN '' The ErrorLog will be cycled because of its age.'' -- over 15 days
ELSE '' The ErrorLog will not be cycled.'' end
FROM @ErrorLogs where ArchiveNumber = 1

RAISERROR (@CycleMessage,0,1) WITH NOWAIT;

IF @CycleMessage LIKE ''%will be cycled%''
BEGIN
	EXEC @return_value = sp_cycle_errorlog
	SELECT @Output = CASE WHEN @return_value = 0 THEN ''ErrorLog was sucessfully cycled.'' ELSE ''Failure cycling Errorlog.'' END
	RAISERROR (@Output,0,1) WITH NOWAIT;
END', 
		@database_name=N'master', 
		@flags=4
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_update_job @job_id = @jobId, @start_step_id = 1
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_add_jobschedule @job_id=@jobId, @name=N'Daily Cycle Errorlog', 
		@enabled=1, 
		@freq_type=4, 
		@freq_interval=1, 
		@freq_subday_type=1, 
		@freq_subday_interval=0, 
		@freq_relative_interval=0, 
		@freq_recurrence_factor=0, 
		@active_start_date=20120529, 
		@active_end_date=99991231, 
		@active_start_time=235900, 
		@active_end_time=235959
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
EXEC @ReturnCode = msdb.dbo.sp_add_jobserver @job_id = @jobId, @server_name = N'(local)'
IF (@@ERROR <> 0 OR @ReturnCode <> 0) GOTO QuitWithRollback
COMMIT TRANSACTION
GOTO EndSave
QuitWithRollback:
    IF (@@TRANCOUNT > 0) ROLLBACK TRANSACTION
EndSave:

RAISERROR ( 'Daily Cycle Log job created',0,1) WITH NOWAIT;



-- Limit error logs
EXEC xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\MSSQLServer', N'NumErrorLogs', REG_DWORD, 100
GO
RAISERROR ('Action: Set Errorlogs to 100',0,1) WITH NOWAIT;
-- Set sp_configure settings

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Set RAC, repl size, recovery interval and agent history
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
EXEC sys.sp_configure N'show advanced options', N'1'
RECONFIGURE WITH OVERRIDE

EXEC sys.sp_configure N'remote admin connections', N'1'
RECONFIGURE WITH OVERRIDE

RAISERROR ('Action: Enabled remote admin mail',0,1) WITH NOWAIT;


EXEC sys.sp_configure N'recovery interval (min)', N'1'
RAISERROR ('Action: Set recovery interval',0,1) WITH NOWAIT;

EXEC sys.sp_configure N'max text repl size (B)', N'-1'
RAISERROR ('Action: Set max text replication size',0,1) WITH NOWAIT;


/*Let's set , don't want no huge msdb please*/
EXEC msdb.dbo.sp_set_sqlagent_properties 
	@jobhistory_max_rows=100000, 
	@jobhistory_max_rows_per_job=1000,
	@email_save_in_sent_folder=1, 
	@cpu_poller_enabled=1
/*Configure Agent alert conditions*/
EXEC master.dbo.sp_MSsetalertinfo @failsafeoperator=N'SQLDBA', 
		@notificationmethod=1	

RAISERROR ('Action: Set jobhistory to 100k, rows per job 1k',0,1) WITH NOWAIT;




/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Set backup checksum, contained databases, backup compression and ad hoc workloads
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/
/*Check if server version > SQL 2008R2*/
DECLARE @SQLVersion INT
SELECT @SQLVersion = @@MicrosoftVersion / 0x01000000  OPTION (RECOMPILE)-- Get major version

IF @SQLVersion >= 10
BEGIN
	BEGIN TRY
		EXEC sys.sp_configure N'backup checksum default', N'1'
		RECONFIGURE WITH OVERRIDE
		RAISERROR ('Action: Set backup checksum',0,1) WITH NOWAIT;
		
	END TRY
	BEGIN CATCH 
		RAISERROR ('Failed to configure backup checksum default',0,1) WITH NOWAIT;
	END CATCH
	EXEC sys.sp_configure N'contained database authentication', N'1'
	RECONFIGURE WITH OVERRIDE
	RAISERROR ( 'Action: Enabled contained DBs',0,1) WITH NOWAIT;
END
-- Use 'backup compression default' when server is NOT CPU bound
IF CONVERT(int, (@@microsoftversion / 0x1000000) & 0xff) >= 10
EXEC sys.sp_configure N'backup compression default', N'1'
RECONFIGURE WITH OVERRIDE

RAISERROR ('Action: Set backup compression',0,1) WITH NOWAIT;
-- Use 'optimize for ad hoc workloads' for OLTP workloads ONLY
IF CONVERT(int, (@@microsoftversion / 0x1000000) & 0xff) >= 10
EXEC sys.sp_configure N'optimize for ad hoc workloads', N'1'
RECONFIGURE WITH OVERRIDE
RAISERROR ('Action: Set optimize for ad hoc',0,1) WITH NOWAIT;

EXEC sys.sp_configure N'show advanced options', N'0'
RECONFIGURE WITH OVERRIDE
GO



/*Configure memory
BATCH_MODE_ON_ROWSTORE

DECLARE @CPUcount INT;
DECLARE @CPUsocketcount INT;
DECLARE @CPUHyperthreadratio MONEY;
	SELECT @CPUcount = cpu_count 
	, @CPUsocketcount = [cpu_count] / [hyperthread_ratio]
	, @CPUHyperthreadratio = [hyperthread_ratio]
	FROM sys.dm_os_sys_info;
*/


/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Set database level settings
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

DECLARE @DatabaseName SYSNAME;
DECLARE @Databasei_Count INT;
DECLARE @Databasei_Max INT;
DECLARE @DynamicSQL NVARCHAR(4000);
DECLARE @DynamicSQLforDB NVARCHAR(4000);
DECLARE @state TINYINT
DECLARE @AutoClose TINYINT
DECLARE @AutoShrink TINYINT
DECLARE @AutoStats TINYINT
DECLARE @AutoCreateStats TINYINT
DECLARE @AutoStatsAsync TINYINT
DECLARE @AutoStatsInc TINYINT
DECLARE @RecoveryModel TINYINT
DECLARE @PageVerify TINYINT
DECLARE @QueryStore TINYINT



DECLARE @Databases TABLE
	(
		id INT IDENTITY(1,1)
		, databasename VARCHAR(250)
		, [compatibility_level] BIGINT
		, user_access BIGINT
		, user_access_desc VARCHAR(50)
		, [state] BIGINT
		, state_desc  VARCHAR(50)
		, recovery_model INT
		, recovery_model_desc  VARCHAR(50)
		, create_date DATETIME
		, is_auto_close_on TINYINT
		, is_auto_shrink_on TINYINT
		, is_auto_create_stats_on TINYINT
		, is_auto_update_stats_on TINYINT
		, is_auto_update_stats_async_on TINYINT
		, is_auto_create_stats_incremental_on TINYINT
		, page_verify_option TINYINT
		, is_query_store_on TINYINT
	);
SET @DynamicSQL = 'SELECT 
	db.name
	, db.compatibility_level
	, db.user_access
	, db.user_access_desc
	, db.state
	, db.state_desc
	, db.recovery_model
	, db.recovery_model_desc
	, db.create_date
	, db.is_auto_close_on
	, db.is_auto_shrink_on
	, db.is_auto_create_stats_on
	, db.is_auto_update_stats_on
	, db.is_auto_update_stats_async_on
	' + CASE WHEN SERVERPROPERTY('productversion') < 12 THEN ', NULL' ELSE ', db.is_auto_create_stats_incremental_on' END +'
	, db.page_verify_option
	' + CASE WHEN SERVERPROPERTY('productversion') > 13 THEN ', NULL' ELSE ', db.is_query_store_on' END +'
	FROM sys.databases db ';

IF 'Yes please dont do the system databases' IS NOT NULL
BEGIN
	SET @DynamicSQL = @DynamicSQL + ' WHERE database_id > 4 AND state NOT IN (1,2,3,6)';
END
SET @DynamicSQL = @DynamicSQL + ' OPTION (RECOMPILE)'
INSERT INTO @Databases 
EXEC sp_executesql @DynamicSQL ;
SET @Databasei_Max = (SELECT MAX(id) FROM @Databases );


SET @Databasei_Count = 1; 
WHILE @Databasei_Count <= @Databasei_Max 
BEGIN 
	SET @DynamicSQLforDB = '';
	SELECT 
	@state = state
	, @DatabaseName = d.databasename
	, @AutoClose = is_auto_close_on
	, @AutoShrink = is_auto_shrink_on
	, @AutoCreateStats = is_auto_create_stats_on
	, @AutoStats = is_auto_update_stats_on
	, @AutoStatsAsync = is_auto_update_stats_async_on
	, @AutoStatsInc = is_auto_create_stats_incremental_on
	, @RecoveryModel = recovery_model --3 = simple
	, @PageVerify = page_verify_option --2 = checksum	
	, @QueryStore = is_query_store_on
	FROM @Databases d WHERE id = @Databasei_Count

	IF @AutoClose = 1 
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET AUTO_CLOSE OFF WITH NO_WAIT'
	IF @AutoShrink = 1
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET AUTO_SHRINK OFF WITH NO_WAIT'
	IF @PageVerify <> 2
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET PAGE_VERIFY CHECKSUM WITH NO_WAIT'
	IF @AutoCreateStats = 0
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET AUTO_CREATE_STATISTICS ON WITH NO_WAIT'
	IF @AutoStats = 0
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET AUTO_UPDATE_STATISTICS ON WITH NO_WAIT'
	IF @QueryStore = 0
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET QUERY_STORE = ON WITH NO_WAIT'
	/*
	
	IF @AutoStatsAsync = 0
		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET AUTO_UPDATE_STATISTICS_ASYNC ON WITH NO_WAIT'
	*/
--	IF @RecoveryModel <> 1
--		SET @DynamicSQLforDB = @DynamicSQLforDB + CHAR(13)+CHAR(10) + 'ALTER DATABASE [' + @DatabaseName + '] SET RECOVERY FULL WITH NO_WAIT'
	IF @DynamicSQLforDB <> ''
	BEGIN
		RAISERROR ('Committing database change',0,1) WITH NOWAIT;
		RAISERROR (@DynamicSQLforDB,0,1) WITH NOWAIT;
		EXECUTE( @DynamicSQLforDB)
	END
	SET @Databasei_Count = @Databasei_Count + 1
END

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += CASE
  WHEN (ag.role = N'PRIMARY' AND ag.ag_status = N'READ_WRITE') OR ag.role IS NULL THEN N'
    ALTER DATABASE ' + QUOTENAME(d.name) + N' SET TARGET_RECOVERY_TIME = 60 SECONDS;' 
  ELSE N'
    RAISERROR ( N''-- fix ' + QUOTENAME(d.name) + N' on Primary.'',0,1) WITH NOWAIT;' 
  END
FROM sys.databases AS d 
OUTER APPLY
(
  SELECT role = s.role_desc, 
    ag_status = DATABASEPROPERTYEX(c.database_name, N'Updateability')
    FROM sys.dm_hadr_availability_replica_states AS s
    INNER JOIN sys.availability_databases_cluster AS c
       ON s.group_id = c.group_id 
       AND d.name = c.database_name
    WHERE s.is_local = 1
) AS ag
WHERE d.target_recovery_time_in_seconds <> 60
  AND d.database_id > 4 
  AND d.[state] = 0 
  AND d.is_in_standby = 0 
  AND d.is_read_only = 0;
/*SELECT DatabaseCount = @@ROWCOUNT, Version = @@VERSION, cmd = @sql;*/
EXEC sys.sp_executesql @sql;


/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Set database autogrowth
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

/*Now to recommend code to fix autogrowth*/
/*
****MODIFICATION REQUIRED for AUTOGROWTH -- See line 64 below****
1) Use this script to change the auto growth setting of
   for all databases
2) If you want to exclude any database add the DBs in the
   WHERE Clause -- See line 50 below
3) Tested in 2012 and 2014 SQL Servers
*/
DECLARE @TargetDATAGrowth NVARCHAR(50)
SET @TargetDATAGrowth = '512'
DECLARE @TargetLOGGrowth NVARCHAR(50)
SET @TargetLOGGrowth = '256'


DECLARE @ConfigAutoGrowth TABLE
(
	iDBID INT
	, sDBName SYSNAME
	, vFileName NVARCHAR(2000)
	, [MB Growth] MONEY
	, [File Type] VARCHAR(20)
	, vGrowthOption VARCHAR(12)
	, [Option] NVARCHAR(500)
	, [Action Needed] TINYINT
	, [Command] NVARCHAR(500)
)

-- Inserting data into staging table
INSERT INTO @ConfigAutoGrowth
SELECT
	SD.database_id
	,SD.name
	,SF.name
	, SF.growth /128 [MB Growth]
	, CASE SF.status
		WHEN 2 THEN 'Data'
		WHEN 1048578 THEN 'Data'
		WHEN 66 THEN 'Log'
		WHEN 1048642 THEN 'Log'
	END [File Type]
	, CASE SF.status
		WHEN 1048578 THEN '%'
		WHEN 1048642 THEN '%'
	ELSE 'MB' END AS 'GROWTH Option'
	
	, CASE 
		WHEN SF.status = 2			AND (SF.growth /128)+2 < @TargetDATAGrowth THEN 'Increase Data file to ' + @TargetDATAGrowth
		WHEN SF.status = 1048578	THEN 'Change Data file to MB growth'
		WHEN SF.status = 66			AND (SF.growth /128)+2 < @TargetLOGGrowth THEN 'Increase Log file to ' + @TargetLOGGrowth /*Add 2 MB to counter rounding issues, if any*/
		WHEN SF.status = 1048642	THEN 'Change Log file to MB growth'
		ELSE 'No change it seems'
	END [Option]
	,CASE 
		WHEN SF.status = 2			AND (SF.growth /128)+2 <= @TargetDATAGrowth THEN 1
		WHEN SF.status = 1048578	THEN 1
		WHEN SF.status = 66			AND (SF.growth /128)+2 <= @TargetLOGGrowth THEN 1 /*Add 2 MB to counter rounding issues, if any*/
		WHEN SF.status = 1048642	THEN 1
		ELSE 0
	END [Action Needed]
	, CASE 
		WHEN SF.status = 2			AND (SF.growth /128)+2 < @TargetDATAGrowth THEN 'ALTER DATABASE '+ '[' + SD.name + ']' +' MODIFY FILE (NAME = '+ '[' +SF.name + ']' +',FILEGROWTH = '+ @TargetDATAGrowth + 'MB)'
		WHEN SF.status = 1048578	THEN 'ALTER DATABASE '+ '[' + SD.name + ']' +' MODIFY FILE (NAME = '+ '[' +SF.name + ']' +',FILEGROWTH = '+ @TargetDATAGrowth + 'MB)'
		WHEN SF.status = 66			AND (SF.growth /128)+2 < @TargetLOGGrowth THEN 'ALTER DATABASE '+ '[' + SD.name + ']' +' MODIFY FILE (NAME = '+ '[' +SF.name + ']' +',FILEGROWTH = '+ @TargetLOGGrowth + 'MB)'
		WHEN SF.status = 1048642	THEN 'ALTER DATABASE '+ '[' + SD.name + ']' +' MODIFY FILE (NAME = '+ '[' +SF.name + ']' +',FILEGROWTH = '+ @TargetLOGGrowth + 'MB)'
		ELSE ''
	END [Command]

FROM sys.sysaltfiles SF
INNER JOIN sys.databases SD ON SD.database_id = SF.dbid

 
-- Dynamically alters the file to set auto growth option to fixed mb
DECLARE @name VARCHAR ( 500 ) -- Database Name
DECLARE @dbid INT -- DBID
DECLARE @vFileName VARCHAR ( 500 ) -- Logical file name
DECLARE @vGrowthOption VARCHAR ( 500 ) -- Growth option
DECLARE @Query NVARCHAR(2000) -- Variable to store dynamic sql
DECLARE @Option NVARCHAR(500)
DECLARE @Raiseme NVARCHAR(500)
DECLARE db_cursor CURSOR FOR
SELECT
iDBID,sDBName,vFileName,vGrowthOption, [Command], [Option]
FROM @ConfigAutoGrowth T
WHERE [Action Needed] = 1
--sdbname NOT IN ( 'master' ,'msdb' ) --<<--ADD DBs TO EXCLUDE
--AND vgrowthoption IN( 'Percentage', 'Mb')
 
OPEN db_cursor
FETCH NEXT FROM db_cursor INTO @dbid,@name,@vFileName,@vGrowthOption, @Query, @Option
WHILE @@FETCH_STATUS = 0
BEGIN

	SET @Raiseme = '/*DB: '+ UPPER(@name) +'. ' + @Option + '*/'
	RAISERROR (@Raiseme,0,1) WITH NOWAIT;
	SET @Raiseme = ''
	BEGIN TRY
		EXEC sp_executesql @Query
		SET @Query = 'Committed - ' + @Query
		RAISERROR (@Query,0,1) WITH NOWAIT;
	END TRY
	BEGIN CATCH
		RAISERROR ('Could not alter database',0,1) WITH NOWAIT;
	END CATCH
 
	FETCH NEXT FROM db_cursor INTO @dbid,@name,@vFileName,@vGrowthOption, @Query, @Option
END
CLOSE db_cursor -- Closing the curson
DEALLOCATE db_cursor -- deallocating the cursor
 

-- Querying system views to see if the changes are applied
DECLARE @sname VARCHAR(3)
SET @SQL=' USE [?]
SELECT ''?'' [Dbname]
,[name] [Filename]
,CASE is_percent_growth
WHEN 1 THEN CONVERT(VARCHAR(5),growth)+''%''
ELSE CONVERT(VARCHAR(20),(growth/128))+'' MB''
END [Autogrow_Value]
,CASE max_size
WHEN -1 THEN CASE growth
WHEN 0 THEN CONVERT(VARCHAR(30),''Restricted'')
ELSE CONVERT(VARCHAR(30),''Unlimited'') END
ELSE CONVERT(VARCHAR(25),max_size/128)
END [Max_Size]
FROM [?].sys.database_files'
 

DECLARE @Fdetails TABLE 
(Dbname VARCHAR(500),Filename VARCHAR(500),Autogrow_Value VARCHAR(15),Max_Size VARCHAR(30))
INSERT INTO @Fdetails 
EXEC sp_MSforeachdb @SQL



GO




/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Now let us add traceflags
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

/******************************************************************************
Source link: https://blog.waynesheffield.com/wayne/archive/2017/09/registry-sql-server-startup-parameters/
Author: Wayne Sheffield

Globally enable / disable the specified trace flags.
Use DBCC TRACEON/TRACEOFF to enable disable globally trace flags, then adjust
the SQL Server instance startup parameters for these trace flags.
 
SQL Server startup parameters are stored in the registry at:
HKLM\Software\Microsoft\Microsoft SQL Server\MSSQL12.SQL2014\MSSQLServer\Parameters
 
To use the xp_instance_reg... XPs, use:
HKLM\Software\Microsoft\MSSQLSERVER\MSSQLServer\Parameters.
 
To use:
1. Add the Trace Flags that you want modified to the @TraceFlags table variable.
2. Set the @DebugLevel variable to 1 to see what will happen on your system first.
3. When satisified what will happen, set @DebugLevel to 0 to actually execute the statements.
********************************************************************************
                               MODIFICATION LOG
********************************************************************************
2016-08-03 WGS Initial Creation.
*******************************************************************************/
SET NOCOUNT ON;
-- Declare and initialize variables.
-- To use with SQL 2005, cannot set the variables in the declare statement.
DECLARE @MaxValue   INTEGER,
        @SQLCMD     VARCHAR(MAX),
        @RegHive    VARCHAR(50),
        @RegKey     VARCHAR(100),
        @DebugLevel TINYINT;
 
SET @RegHive = 'HKEY_LOCAL_MACHINE';
SET @RegKey  = 'Software\Microsoft\MSSQLSERVER\MSSQLServer\Parameters';
SET @DebugLevel = 0;  -- only makes changes if set to zero!
DECLARE @CurrentTF INT
DECLARE @CurrentTFWarn NVARCHAR(500)
SELECT @CurrentTF = COUNT(*)
FROM sys.dm_server_registry
WHERE registry_key LIKE N'%Parameters'
AND CONVERT(VARCHAR(250),value_data) LIKE '%-T%'

-- Add the trace flags that you want changed here.
-- If enable = 1, DBCC TRACEON will be run; if enable = 0 then DBCC TRACEOFF will be run.
-- If enable_on_startup = 1, then this TF will be added to start up on service restart; 
-- If enable_on_startup - 0, then this TF will be removed from starting up service restart
DECLARE @TraceFlags TABLE (
    TF                  INTEGER,
    enable              BIT,
    enable_on_startup   BIT,
    TF2                 AS '-T' + CONVERT(VARCHAR(15), TF)
);

-- To work with SQL 2005, cannot use a table value constructor.
-- So, use SELECT statements with UNION ALL for each TF to modify.
DECLARE @SQLVersion INT
SELECT @SQLVersion = @@MicrosoftVersion / 0x01000000  OPTION (RECOMPILE)-- Get major version

INSERT INTO @TraceFlags (TF, enable, enable_on_startup)
SELECT 1204, 1, 1 /*Deadlock related*/
UNION ALL 
SELECT 1222, 1, 1 /*Deadlock related*/
--SELECT 1224, 1, 1  /*Lock escalation - NOT WORTH IT*/

/*
1488, Enables Replication to continue when the secondary node is down
*/
IF @SQLVersion >= 10
BEGIN
	INSERT INTO @TraceFlags (TF, enable, enable_on_startup)
	SELECT 1117, 1, 1
	 UNION ALL
	SELECT 1118, 1, 1 
	UNION ALL
	SELECT 2371, 1, 1 /*Statistics update threshold fixer*/
	UNION ALL 
	SELECT 3226, 1, 1  /*Supress backup log information in SQL event log*/
	
END
DECLARE @sqledition NVARCHAR(500)
SELECT @sqledition = CONVERT(NVARCHAR(500),SERVERPROPERTY('edition') )
IF @SQLVersion >= 10 AND @sqledition LIKE '%Express%'
BEGIN
	INSERT INTO @TraceFlags (TF, enable, enable_on_startup)
	SELECT 7806,1,1 /*Always enable DAC for SQL Express*/
END


IF @SQLVersion >= 11
BEGIN
	INSERT INTO @TraceFlags (TF, enable, enable_on_startup)
	SELECT 2453, 1, 1 /*Table variable fix*/
	UNION ALL 
	SELECT 1800, 1, 1 /*Assume the machine is likely a VM, so for 4K Transaction log cluster size*/
	UNION ALL 
	SELECT 9488, 1, 1 /*Table valued function fixed estimation*/
	UNION ALL
	SELECT 174,1,1	/*Increase plan cache bucket count, https://www.sqlskills.com/blogs/erin/sql-server-plan-cache-limits/ */
END


IF @SQLVersion >= 13
BEGIN
	INSERT INTO @TraceFlags (TF, enable, enable_on_startup)
	SELECT 9567, 1, 1  /*Compress AG seed workload*/
	UNION ALL
	SELECT 4199,1,1 /* Trace Flag 4199 is not enabled to control multiple query optimizer changes
--https://www.mssqltips.com/sqlservertip/3320/enabling-sql-server-trace-flag-for-a-poor-performing-query-using-querytraceon/
	--4136 IGNORE STATISTICS
*/
END

-- Get all of the arguments / parameters when starting up the service.
DECLARE @SQLArgs TABLE (
    Value   VARCHAR(50),
    Data    VARCHAR(500),
    ArgNum  AS CONVERT(INTEGER, REPLACE(Value, 'SQLArg', '')));
INSERT INTO @SQLArgs
    EXECUTE master.sys.xp_instance_regenumvalues @RegHive, @RegKey;


-- Get the highest argument number that is currently set
SELECT  @MaxValue = MAX(ArgNum) 
FROM    @SQLArgs;
--RAISERROR('MaxValue: %i', 10, 1, @MaxValue) WITH NOWAIT;
 
-- Disable specified trace flags
SELECT  @SQLCMD = 'DBCC TRACEOFF(' + 
        STUFF((SELECT ',' + CONVERT(VARCHAR(15), TF)
               FROM   @TraceFlags
               WHERE  enable = 0
               ORDER BY TF
               FOR XML PATH(''), TYPE).value('.','varchar(max)')
              ,1,1,'') + ', -1);'

IF @DebugLevel = 0 
EXECUTE (@SQLCMD);
RAISERROR('Manual - Disable TFs Command: "%s"', 10, 1, @SQLCMD) WITH NOWAIT;

-- Enable specified trace flags
SELECT  @SQLCMD = 'DBCC TRACEON(' + 
        STUFF((SELECT ',' + CONVERT(VARCHAR(15), TF)
               FROM   @TraceFlags
               WHERE  enable = 1
               ORDER BY TF
               FOR XML PATH(''), TYPE).value('.','varchar(max)')
              ,1,1,'') + ', -1);'
 
IF @DebugLevel = 0 EXECUTE (@SQLCMD);
RAISERROR('Manual - Enable TFs Command:  "%s"', 10, 1, @SQLCMD) WITH NOWAIT;

DECLARE cSQLParams CURSOR LOCAL FAST_FORWARD FOR
WITH cte AS
(
    -- Current arguments, with new TFs added at the end. Get a row number to sort by.
    SELECT  *,
            ROW_NUMBER() OVER (ORDER BY ISNULL(ArgNum, 999999999), TF) - 1 AS RN
    FROM    @SQLArgs arg
    FULL OUTER JOIN @TraceFlags tf ON arg.Data = tf.TF2
), cte2 AS
(
    -- Use the row number to calc the SQLArg# for new TFs. 
    -- Use the original Value (SQLArg#) and Data for all rows if possible, 
    -- Otherwise use the calculated SQLArg# and the calculated TF2 column.
    -- Only get the original non-TF-matched parameters, and the TFs set to be enabled
    -- (existing startup TFs not in @TraceFlags are left alone).
    SELECT  ca.Value,
            ca.Data
            -- in case any TFs are removed, calculate new row numbers in order 
            -- to renumber the SQLArg values
            , ROW_NUMBER() OVER (ORDER BY RN) - 1 AS RN2
    FROM    cte
            -- Again, for SQL 2005, use SELECT statement instead of VALUES.
            CROSS APPLY (SELECT ISNULL(Value, 'SQLArg' + CONVERT(VARCHAR(15), RN)), 
                                ISNULL(Data, TF2) ) ca(Value, Data)
    WHERE   ISNULL(enable_on_startup, 1) = 1  -- ISNULL handles non-TF parameters
)
-- The first three parameters are the location of the errorlog directory,
-- and the master database file locations. Ignore these.
-- This returns the remaining parameters that should be set.
-- Also return the highest number of parameters, so can determine if any need to be deleted.
SELECT  'SQLArg' + CONVERT(VARCHAR(15), RN2) AS Value,
        Data,
        MAX(RN2) OVER () AS MaxRN2
FROM    cte2
WHERE   RN2 > 2
ORDER BY RN2;
 
DECLARE @Value VARCHAR(50)
DECLARE @Data  VARCHAR(500)
DECLARE @MaxRN2 INT
RAISERROR('', 10, 1)
RAISERROR('----------------------------------------------------', 10, 1)
IF @CurrentTF < (SELECT COUNT(*) FROM @TraceFlags)
BEGIN
	SET @CurrentTFWarn  = 'Current startup TF count is: ' + CONVERT(VARCHAR(5),@CurrentTF)
	RAISERROR(@CurrentTFWarn, 0, 1) WITH NOWAIT;
	SET @CurrentTFWarn  = 'Expected startup TF count is: ' + CONVERT(VARCHAR(5),(SELECT COUNT(*) FROM @TraceFlags))
	RAISERROR(@CurrentTFWarn, 0, 1) WITH NOWAIT;
	RAISERROR('You will need to set some traceflags manually', 16, 1) WITH NOWAIT;
END

RAISERROR('WARNING! Attempting Registry Access', 10, 1)
RAISERROR('WARNING! It is okay if it fails. Just do it manually.', 10, 1)
OPEN cSQLParams;
FETCH NEXT FROM cSQLParams INTO @Value, @Data, @MaxRN2;
WHILE @@FETCH_STATUS = 0 
BEGIN
    IF @DebugLevel = 0 
	BEGIN
		BEGIN TRY
			EXECUTE master.sys.xp_instance_regwrite @RegHive, @RegKey, @Value, 'REG_SZ', @Data;
		END TRY
		BEGIN CATCH
			RAISERROR('WARNING! Registry Access is Denied', 10, 1)
		END CATCH
	END
    --RAISERROR('EXECUTE master.sys.xp_instance_regwrite ''%s'', ''%s'', ''%s'', ''REG_SZ'', ''%s''', 10, 1, @RegHive, @RegKey, @Value, @Data) WITH NOWAIT;
    FETCH NEXT FROM cSQLParams INTO @Value, @Data, @MaxRN2;
END;
CLOSE cSQLParams;
DEALLOCATE cSQLParams;

-- In case deleting more TFs than added, there may be extra SQLArg values left behind. 
-- Need to delete the extras now.
WHILE @MaxValue > @MaxRN2
BEGIN
  SET @Value = 'SQLArg' + CONVERT(VARCHAR(15), @MaxValue);
  IF @DebugLevel = 0 EXECUTE master.sys.xp_instance_regdeletevalue @RegHive, @RegKey, @Value;
    RAISERROR('EXECUTE master.sys.xp_instance_regdeletevalue ''%s'', ''%s'', ''%s''', 10, 1, @RegHive, @RegKey, @Value) WITH NOWAIT;
  SET @MaxValue = @MaxValue - 1;
END;




/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Try some operator cleanup
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

IF EXISTS(
SELECT * 
FROM [msdb].[dbo].[sysoperators]
WHERE name = N'Lexel DBA')
BEGIN TRY
	EXEC msdb.dbo.sp_update_operator @name=N'Lexel DBA', 
		@enabled=1, 
		@pager_days=0, 
		@email_address=N'alerts@sqldba.org', 
		@pager_address=N''
		/*Rename*/
		EXEC msdb.dbo.sp_update_operator 
		@name = 'Lexel DBA', 
		@new_name = 'SQLDBA';
END TRY
BEGIN CATCH
	RAISERROR ( 'Problem with changing Lexel DBA Operator',0,1) WITH NOWAIT;
END CATCH



/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Create sp_triage®_view and export to text
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

Use [master]
IF OBJECT_ID('dbo.[sqldba_sp_triage®_view]') IS NULL
  EXEC ('CREATE PROCEDURE dbo.[sqldba_sp_triage®_view] AS RETURN 0;');
GO
ALTER PROCEDURE [dbo].[sqldba_sp_triage®_view]
AS
SELECT TOP (100) PERCENT 
ID, evaldate, domain, SQLInstance, SectionID, Section, Summary, Severity, Details, HoursToResolveWithTesting, QueryPlan

FROM 
 (SELECT         CONVERT(NVARCHAR(25), T1.ID) AS ID, REPLACE(T1.evaldate, '~', '-') AS evaldate, REPLACE(T1.domain, '~', '-') AS domain, REPLACE(T1.SQLInstance, '~', '-') 
AS SQLInstance, REPLACE(CONVERT(NVARCHAR(10), T1.SectionID), '~', '-') AS SectionID, REPLACE(T1.Section, '~', '-') AS Section, REPLACE(T1.Summary, '~', '-') AS Summary, 
 REPLACE(T1.Severity, '~', '-') AS Severity, REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(REPLACE(T1.Details, '~', '-'), N''), CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' '), ' ', ' ') AS Details, 
 REPLACE(CONVERT(NVARCHAR(10), T1.HoursToResolveWithTesting), '~', '-') AS HoursToResolveWithTesting, REPLACE(T1.QueryPlan, '~', '-') AS QueryPlan, T1.ID AS Sorter
FROM            dbo.[sqldba_sp_triage®_output] AS T1 INNER JOIN
(SELECT        MAX(evaldate) AS evaldate
FROM            dbo.[sqldba_sp_triage®_output]) AS T2 ON T1.evaldate = T2.evaldate) AS T3
ORDER BY Sorter ASC
GO
Use [master]
IF OBJECT_ID('dbo.[sp_triage®_to_text]') IS NULL
  EXEC ('CREATE PROCEDURE dbo.[sp_triage®_to_text] AS RETURN 0;');
GO
ALTER PROCEDURE [dbo].[sp_triage®_to_text]
AS
BEGIN
	DECLARE @StateOfXP_CMDSHELL INT
	SELECT @StateOfXP_CMDSHELL = CONVERT(INT, ISNULL(value, value_in_use)) 
	FROM  sys.configurations
	WHERE  name = 'xp_cmdshell' ;

	IF @StateOfXP_CMDSHELL = 0 
	BEGIN
		-- To allow advanced options to be changed.
		EXEC sp_configure 'show advanced options', 1
		-- To update the currently configured value for advanced options.
		RECONFIGURE
		-- To enable the feature.
		EXEC sp_configure 'xp_cmdshell', 1
		-- To update the currently configured value for this feature.
		RECONFIGURE
	END	
	DECLARE @Es NVARCHAR(500)
	DECLARE @M_R NVARCHAR(500) 
	DECLARE @MaxDate DATETIME
	SELECT @MaxDate = MAX(evaldate) FROM [master].[dbo].[sqldba_sp_triage®_output]
	DECLARE @evaldate NVARCHAR(25)
	DECLARE @TD NVARCHAR(50)
	DECLARE @T_S NVARCHAR(50)
	DECLARE @Ctt CHAR(1)
	SET @Ctt = '\'
	SELECT @evaldate = evaldate, @TD = domain , @T_S = SQLInstance 
	FROM [master].[dbo].[sqldba_sp_triage®_output]
	WHERE evaldate = @MaxDate
	DECLARE @EmailBody NVARCHAR(500) 
	DECLARE @qrs NVARCHAR(50)
	DECLARE @S_Ex NVARCHAR(4000)
	DECLARE @EmailProfile NVARCHAR(500)
	DECLARE @A_F NVARCHAR(500)
	SET @qrs = '~';--char(9);
	SET @M_R ='scriptoutput@sqldba.org'
	SET @Es = 'Sqldba_sqlmagic_data for ' +@TD + ' '+@T_S + '' + REPLACE(REPLACE(REPLACE(@evaldate,'-','_'),':',''),' ','');
	SET @A_F = 'sqldba_sqlmagic_data__' +REPLACE(@TD,'.','_') + '_'+ REPLACE(@T_S,@Ctt,'_') + '_' + REPLACE(REPLACE(REPLACE(@evaldate,'-','_'),':',''),' ','') +'.csv' 
	DECLARE @xpSQL NVARCHAR(4000)
	DECLARE @bu NVARCHAR(400)
	DECLARE @SQLB TABLE (v1 NVARCHAR(50), d1 NVARCHAR(500))
	INSERT @SQLB
	EXECUTE [master].dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'SOFTWARE\Microsoft\MSSQLServer\MSSQLServer', N'BackupDirectory'
	SELECT @bu = d1 FROM @SQLB

	DECLARE @pstext NVARCHAR(4000)
	SET @pstext =''
	SET @pstext = @pstext + '$sqlConString = "Server = ' + @T_S +'; Database = master; Integrated Security = True;";' ;
	SET @pstext = @pstext + '$outfile = "' + @bu+'\' + @A_F + '";' ;
	SET @pstext = @pstext + '$SqlQueryM3 = "[dbo].[sqldba_sp_triage®_view];"  ;' ;
	SET @pstext = @pstext + '$SqlConnectionM3 = New-Object System.Data.SqlClient.SqlConnection  ;' ;
	SET @pstext = @pstext + '$SqlConnectionM3.ConnectionString = $sqlConString;' ;
	SET @pstext = @pstext + '$SqlCmdM3 = New-Object System.Data.SqlClient.SqlCommand  ;' ;
	SET @pstext = @pstext + '$SqlCmdM3.CommandText = $SqlQueryM3  ;' ;
	SET @pstext = @pstext + '$SqlCmdM3.Connection = $SqlConnectionM3  ;' ;
	SET @pstext = @pstext + '$SqlAdapterM3 = New-Object System.Data.SqlClient.SqlDataAdapter  ;' ;
	SET @pstext = @pstext + '$SqlAdapterM3.SelectCommand = $SqlCmdM3   ;' ;
	SET @pstext = @pstext + '$DataSetM3 = New-Object System.Data.DataSet  ;' ;
	SET @pstext = @pstext + '$SqlAdapterM3.SelectCommand.CommandTimeout = 1200;' ;
	SET @pstext = @pstext + '$SqlAdapterM3.Fill($DataSetM3) | Out-null ;' ;
	SET @pstext = @pstext + '$DataSetM3.Tables[0] | export-csv -Delimiter "~" -Path "$outfile" -NoTypeInformation -Encoding UTF8;' ;
	SET @pstext = @pstext + '$SqlConnectionM3.Close();' ;
		
	SET @pstext = REPLACE(REPLACE(@pstext,'"','"""'),';;',';')
	SET @pstext = 'powershell.exe -ExecutionPolicy Bypass -NoLogo -NonInteractive -NoProfile -Command "' + @pstext + '" '

	EXEC xp_cmdshell @pstext
	IF @StateOfXP_CMDSHELL = 0 /*It was originally disabled, then disable it*/
BEGIN
	-- To allow advanced options to be changed.
	EXEC sp_configure 'show advanced options', 1
	-- To update the currently configured value for advanced options.
	RECONFIGURE
	-- To enable the feature.
	EXEC sp_configure 'xp_cmdshell', 0
	-- To update the currently configured value for this feature.
	RECONFIGURE
END

END
GO




/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Configure Failure Alerts for jobs with no alerts
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

DECLARE @the_job_Id BINARY(16)
DECLARE @jobs TABLE (id INT IDENTITY(1,1), job_id BINARY(16), [name]  NVARCHAR(500))
INSERT @jobs
SELECT
job_id,name  FROM msdb.dbo.sysjobs 
WHERE notify_level_email = 0 /*These ones need email alerts*/
AND enabled = 1
DECLARE @Thisjob INT
SET @Thisjob = 1
DECLARE @jobmax INT
SELECT @jobmax = COUNT(*) FROM @jobs
WHILE @Thisjob <= @jobmax
BEGIN
	SELECT @the_job_Id = job_id FROM @jobs WHERE id = @Thisjob
	EXEC msdb.dbo.sp_update_job @job_id=@the_job_Id
		, @notify_level_email=2
		, @notify_level_page=2
		, @notify_level_eventlog=2
		, @notify_email_operator_name=N'SQLDBA'
	SET @Thisjob = @Thisjob + 1
END





/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- SecurityChangeQueue - NOT IDEAL FOR JSUT ENABLING
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

--Event Notifications

--For modern versions of SQL we would use SQL Audit, but we need event notifications for backwards compatibility
--As mentioned before, we cannot use SQL Audit for SQL 2008 and R2 Standard Edition or SQL 2005. As an alternative, you can setup event notifications which will capture messages via Service Broker. The scripts below are based on the scripts of Aaron but I’ve added more events to it as I wanted to trace more than just “change password”

--code from https://pietervanhove.azurewebsites.net/?p=3798
--Create the following table in the msdb database

/*
USE [msdb];
GO
 if not exists (select * from sysobjects where name='SecurityChangeLog' and xtype='U')
 begin
CREATE TABLE dbo.SecurityChangeLog
(
    ChangeLogID          int IDENTITY(1,1),
    LoginName            SYSNAME,
    UserName             SYSNAME,
    DatabaseName         SYSNAME,
    SchemaName           SYSNAME,
    ObjectName           SYSNAME,
    ObjectType           VARCHAR(50),
    DDLCommand           VARCHAR(MAX),
    EventTime            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_ChangeLogID PRIMARY KEY (ChangeLogID)
);
 end

go
if not exists (select 1 from sys.service_queues where name='SecurityChangeQueue')

CREATE QUEUE SecurityChangeQueue;
GO
if not exists (select 1 from sys.services where name='SecurityChangeService')
 
CREATE SERVICE SecurityChangeService ON QUEUE SecurityChangeQueue
  ([http://schemas.microsoft.com/SQL/Notifications/PostEventNotification]);
GO
 

--Setup the event notificiation. If you check the “FOR”-clause, you will notice that these are the same actions as defined in the SQL Audit Specification.
-- Create Event Notification if not exists
IF NOT EXISTS (SELECT 1
    FROM sys.server_event_notifications
    WHERE name = 'CreateLoginNotification')
begin 
CREATE EVENT NOTIFICATION CreateLoginNotification
    ON SERVER WITH FAN_IN
    FOR CREATE_LOGIN,ALTER_LOGIN,DROP_LOGIN,CREATE_USER,ALTER_USER,DROP_USER,ADD_SERVER_ROLE_MEMBER,DROP_SERVER_ROLE_MEMBER,ADD_ROLE_MEMBER,DROP_ROLE_MEMBER
    TO SERVICE 'SecurityChangeService', 'current database';
end 
GO
 

--Install the following stored procedure to log all the event notifications into the table we’ve just created. You might notice in the loop that I’m checking the version of SQL Server for some events. This is because the event notification content is different for SQL 2008 (R2) and SQL 2005.

USE [msdb];
GO


IF NOT EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND object_id = OBJECT_ID('dbo.usp_LogSecurityChange'))
   exec('CREATE PROCEDURE [dbo].[usp_LogSecurityChange] AS BEGIN SET NOCOUNT ON; END')
GO
alter PROCEDURE [dbo].[usp_LogSecurityChange]
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @version int
    DECLARE @message_body XML;
    set @version = (SELECT convert (int,REPLACE (LEFT (CONVERT (varchar, SERVERPROPERTY ('ProductVersion')),2), '.', '')))
 
    WHILE (1 = 1)
    BEGIN
       WAITFOR 
       ( 
         RECEIVE TOP(1) @message_body = message_body
         FROM dbo.SecurityChangeQueue
       ), TIMEOUT 1000;
 
       IF (@@ROWCOUNT = 1)
       BEGIN
        if CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/EventType)')) in ('DROP_USER','CREATE_USER','ALTER_USER') or @version>9
        BEGIN
            INSERT dbo.SecurityChangeLog(LoginName,UserName,DatabaseName,SchemaName,ObjectName,ObjectType,DDLCommand) 
            SELECT CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/LoginName)')), 
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/UserName)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/DatabaseName)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/DefaultSchema)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/ObjectName)')),
                CONVERT(VARCHAR(50), @message_body.query('data(/EVENT_INSTANCE/ObjectType)')),
                CONVERT(VARCHAR(MAX), @message_body.query('data(/EVENT_INSTANCE/TSQLCommand/CommandText)'))
        END
        ELSE
        BEGIN
            INSERT dbo.SecurityChangeLog(LoginName,UserName,DatabaseName,SchemaName,ObjectName,ObjectType,DDLCommand) 
            SELECT CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/LoginName)')), 
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/UserName)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/DatabaseName)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/SchemaName)')),
                CONVERT(SYSNAME, @message_body.query('data(/EVENT_INSTANCE/ObjectName)')),
                CONVERT(VARCHAR(50), @message_body.query('data(/EVENT_INSTANCE/ObjectType)')),
                CONVERT(VARCHAR(MAX), @message_body.query('data(/EVENT_INSTANCE/EventType)')) + ' ' + 
                CONVERT(VARCHAR(MAX), @message_body.query('data(/EVENT_INSTANCE/RoleName)')) + ' FOR ' +
                CONVERT(VARCHAR(MAX), @message_body.query('data(/EVENT_INSTANCE/LoginType)')) + ' ' +
                CONVERT(VARCHAR(MAX), @message_body.query('data(/EVENT_INSTANCE/ObjectName)'))
        END
       END
    END
END
 go

--Last step is modifying the queue so that it will use the stored procedure and starts tracking the login and user changes.


if exists (select 1 from sys.service_queues where name='SecurityChangeQueue')

ALTER QUEUE SecurityChangeQueue
WITH ACTIVATION
(
   STATUS = ON,
   PROCEDURE_NAME = dbo.usp_LogSecurityChange,
   MAX_QUEUE_READERS = 1,
   EXECUTE AS OWNER
);
GO
*/

/*
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
--==========================================================
-- Fix endpoints assigned to user
--==========================================================
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
*/

/*Fix endpoints*/
IF EXISTS
( 
		SELECT 1
		FROM master.sys.endpoints e
		INNER JOIN master.sys.server_principals sp
		ON e.principal_id = sp.principal_id
		RIGHT OUTER JOIN ( VALUES ( 2, 'TSQL'),
		( 3, 'SERVICE_BROKER'), ( 4, 'DATABASE_MIRRORING') )
		AS et ( typeid, PayloadType )
		ON et.typeid = e.type
		WHERE sp.name <> 'sa'
		AND sp.name IS NOT NULL
)
SELECT e.name as EndpointName,
sp.name AS EndpointOwner,
et.PayloadType,
e.state_desc
, CASE 
WHEN et.PayloadType IN ('DATABASE_MIRRORING','SERVICE_BROKER')
THEN 'ALTER AUTHORIZATION ON ENDPOINT::' +e.name +' TO sa'
END
FROM master.sys.endpoints e
INNER JOIN master.sys.server_principals sp
ON e.principal_id = sp.principal_id
RIGHT OUTER JOIN ( VALUES ( 2, 'TSQL'),
( 3, 'SERVICE_BROKER'), ( 4, 'DATABASE_MIRRORING') )
AS et ( typeid, PayloadType )
ON et.typeid = e.type
WHERE sp.name <> 'sa'
AND sp.name IS NOT NULL


USE [master]
GO
RAISERROR ('Congratulations you awesome DBA you! Now go herd some more cats'  ,0,1) WITH NOWAIT;
