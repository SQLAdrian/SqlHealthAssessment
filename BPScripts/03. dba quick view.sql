/* بسم الله الرحمن الرحيم  */
/* In the name of God, the Merciful, the Compassionate */

USE [msdb]
GO

IF ('ManualOnly' ='Yes')
BEGIN
	DECLARE @schedule_id_99 int
	DECLARE @ScheduleName NVARCHAR(500)


	SET  @ScheduleName= N'Daily Cycle Errorlog'

	IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = @ScheduleName)
	BEGIN
		SELECT 'MISSING SCHEDULE'
	END
	ELSE
	BEGIN
		SELECT @schedule_id_99 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = @ScheduleName
	END


	DECLARE @ThisDomain NVARCHAR(100)
	BEGIN TRY
	EXEC master.dbo.xp_regread 'HKEY_LOCAL_MACHINE', 'SYSTEM\CurrentControlSet\services\Tcpip\Parameters', N'Domain',@ThisDomain OUTPUT
	END TRY
	BEGIN CATCH
		PRINT 'No access to regitry, which is fine. Using Default_domain()'
	END CATCH
	SET @ThisDomain = ISNULL(@ThisDomain, DEFAULT_DOMAIN())

	DECLARE @jobcmd NVARCHAR(4000)
	SET @jobcmd = N'msdb.dbo.SP_DBA_QUICKVIEW 
	@Client = ''' + @ThisDomain +''' -- Name of project or client 
	, @Recepients =''sqlalerts@sqldba.org'' -- Recepient(s) of this email (; separated in case of multiple recepients).
	, @subject  = ''DBA Quick View Email''
	, @SendEmail = 1
	, @SumJobs = 1'

	DECLARE @jobId BINARY(16)
	IF NOT EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  '998. SQLDBA Quickview')
	BEGIN
		EXEC  msdb.dbo.sp_add_job @job_name=N'998. SQLDBA Quickview', 
			@enabled=1, 
			@notify_level_eventlog=2, 
			@notify_level_email=2, 
			@notify_level_page=2, 
			@notify_email_operator_name=N'SQLDBA',
			@delete_level=0, 
			@category_name=N'[Uncategorized (Local)]', 
			@owner_login_name=N'sa', @job_id = @jobId OUTPUT

		EXEC msdb.dbo.sp_add_jobserver @job_id=@jobId, @server_name = @@SERVERNAME
		EXEC msdb.dbo.sp_attach_schedule @job_id=@jobId,@schedule_id=@schedule_id_99
	END
	ELSE 
	BEGIN
		SELECT @jobId = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  '998. SQLDBA Quickview'
	END

	IF NOT EXISTS(SELECT job_id FROM msdb.dbo.sysjobsteps WHERE [step_name] =  N'Run Quick View' AND job_id = @jobId) 
	EXEC msdb.dbo.sp_add_jobstep @job_name=N'998. SQLDBA Quickview', @step_name=N'Run Quick View', 
			@step_id=1, 
			@cmdexec_success_code=0, 
			@on_success_action=1, 
			@on_fail_action=2, 
			@retry_attempts=0, 
			@retry_interval=0, 
			@os_run_priority=0, @subsystem=N'TSQL', 
			@command=@jobcmd, 
			@database_name=N'master', 
			@flags=0

END



-----------------------------------------------------------------------------

/*
USE [msdb]
GO
CREATE NONCLUSTERED INDEX [IX_LEXEL_BackupSetIndex] ON [dbo].[backupset] ([type],[backup_start_date]) INCLUDE ([media_set_id],[backup_finish_date],[database_name])
GO
CREATE NONCLUSTERED INDEX [IX_LEXEL_BackupSetIndex] ON [dbo].[backupmediafamily] ([media_set_id]) 
*/
IF NOT EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND OBJECT_ID = OBJECT_ID('dbo.SP_DBA_QUICKVIEW'))
   exec('CREATE PROCEDURE [dbo].[SP_DBA_QUICKVIEW] AS BEGIN SET NOCOUNT ON; END')
GO

ALTER PROCEDURE [dbo].[SP_DBA_QUICKVIEW] 
/*Configuration*/
@Client VARCHAR(100) = 'Douglas Pharmaceuticals' -- Client Name. Get's appended to the Subject
, @OutputTable NVARCHAR(50) = 'sqldba_quickview' /*This is A PERSISTENT table, there might be schema clashes..*/
, @OutputSchema NVARCHAR(50) = 'dba' /*I prefer the DBA schema, as these are, well, DBA things*/
, @OutputDB NVARCHAR(50) = 'master' /*Defaults to master in any event*/
, @RetentionDays TINYINT = 60 /*Days back to clean out of output table*/
/*Script behaviour*/
, @SumJobs BIT  = 1 /*Failed Job Summary or Detail*/

/*Email Behaviour*/
, @SendEmail BIT = 1 /*Swith sending mail on/off*/
, @MailProfile VARCHAR(100) = 'DBA Mail Profile' -- If a specific Mail profile should be used
, @Recepients VARCHAR(2000) = '' -- Recepient(s) of this email (; separated in case of multiple recepients).
, @subject NVARCHAR(500) ='' -- The Email Subject

WITH ENCRYPTION
AS
begin
   ---builds a generic #dba_issues table
   /*Procedure Name :SP_DBA_QUICKVIEW*/
   /*Create Date:15-Nov-2021*/
   /*Purpose: Daily Quick View Report for DBA monitoring*/
   /*Modifications: added toggling of xp_cmdshell*/
		   
   --contains code from 
   --https://www.mssqltips.com/sqlservertip/6098/find-the-last-windows-server-reboot-time-and-last-sql-server-restart/
   -- for os_restarttimes
   
   --https://www.mssqltips.com/sqlservertip/4941/find-all-failed-sql-server-logins/
   --failed logins
	DECLARE @dynamicSQL NVARCHAR(4000) ;
	SET @dynamicSQL = N'';
	DECLARE @SQLVersion INT;
	DECLARE @starttime DATETIME;
	DECLARE @evaldate NVARCHAR(20);
	DECLARE @TotalIODailyWorkload MONEY ;
	SET @evaldate = CONVERT(VARCHAR(20),GETDATE(),120);

	SET @starttime = GETDATE()

	SELECT @SQLVersion = @@MicrosoftVersion / 0x01000000  OPTION (RECOMPILE)-- Get major version
	DECLARE @sqlrun NVARCHAR(4000), @rebuildonline NVARCHAR(30), @isEnterprise INT, @i_Count INT, @i_Max INT;
	
	DECLARE @errMessage VARCHAR(MAX) 
SET @errMessage = ERROR_MESSAGE()

DECLARE @ThisServer NVARCHAR(500)
DECLARE @CharToCheck NVARCHAR(5) 
SET @CharToCheck = CHAR(92)
BEGIN TRY
  IF (select CHARINDEX(@CharToCheck,@@SERVERNAME)) > 0
  /*Named instance will always use NetBIOS name*/
    SELECT @ThisServer = @@SERVERNAME
  IF (select CHARINDEX(@CharToCheck,@@SERVERNAME)) = 0
  /*Not named, use the NetBIOS name instead of @@ServerName*/
    SELECT @ThisServer = CAST( Serverproperty( 'ComputerNamePhysicalNetBIOS' ) AS NVARCHAR(500))
END TRY
BEGIN CATCH
  SELECT @errMessage  = ERROR_MESSAGE()
		RAISERROR (@errMessage,0,1) WITH NOWAIT; 
END CATCH

DECLARE @Databases TABLE
	(
		id INT IDENTITY(1,1)
		, database_id INT
		, databasename NVARCHAR(250)
		, [compatibility_level] BIGINT
		, user_access BIGINT
		, user_access_desc NVARCHAR(50)
		, [state] BIGINT
		, state_desc  NVARCHAR(50)
		, recovery_model BIGINT
		, recovery_model_desc  NVARCHAR(50)
		, create_date DATETIME
		, AGReplicaRole INT
		, [BackupPref] NVARCHAR(250)
		, [CurrentLocation] NVARCHAR(250)
		, AGName NVARCHAR(250)
		, [ReadSecondary] NVARCHAR(250)
		
	);
	SET @dynamicSQL = 'SELECT 
	db.database_id
	, db.name
	, db.compatibility_level
	, db.user_access
	, db.user_access_desc
	, db.state
	, db.state_desc
	, db.recovery_model
	, db.recovery_model_desc
	, db.create_date
	
	, 1
	, NULL
	, NULL
	, NULL
	, NULL
	FROM 
	sys.databases db 
	WHERE 1 = 1 '
	If @SQLVersion >= 11 
	BEGIN 
		SET @dynamicSQL = @dynamicSQL + ' AND replica_id IS NULL /*Don''t touch anything AG related*/'
	END
	
	SET @dynamicSQL = @dynamicSQL + ' AND db.database_id > 4 AND db.user_access = 0 AND db.State = 0 '
	
	
	BEGIN TRY

	If @SQLVersion >= 11 BEGIN

	IF EXISTS(SELECT OBJECT_ID('master.sys.availability_groups', 'V')) /*You have active AGs*/
	SET @dynamicSQL = @dynamicSQL + '
	UNION ALL
	SELECT 
	db.database_id
	, db.name
	, db.compatibility_level
	, db.user_access
	, db.user_access_desc
	, db.state
	, db.state_desc
	, db.recovery_model
	, db.recovery_model_desc
	, db.create_date
	, LocalReplicaRole
	, [BackupPref]
	, [CurrentLocation]
	, AGName
	, [ReadSecondary]
	FROM 
	sys.databases db 
	LEFT OUTER JOIN(
	
	SELECT top 100 percent
	AG.name AS [AvailabilityGroupName],
	ISNULL(agstates.primary_replica, NULL) AS [PrimaryReplicaServerName],
	ISNULL(arstates.role, 3) AS [LocalReplicaRole],
	dbcs.database_name AS [DatabaseName],
	ISNULL(dbrs.synchronization_state, 0) AS [SynchronizationState],
	ISNULL(dbrs.is_suspended, 0) AS [IsSuspended],
	ISNULL(dbcs.is_database_joined, 0) AS [IsJoined]
	, AG.automated_backup_preference_desc [BackupPref]
	, AR.availability_mode_desc
	, agstates.primary_replica [CurrentLocation]

	, AG.name AGName
	, AR.secondary_role_allow_connections_desc [ReadSecondary]
	FROM master.sys.availability_groups AS AG
	LEFT OUTER JOIN master.sys.dm_hadr_availability_group_states as agstates
	   ON AG.group_id = agstates.group_id
	INNER JOIN master.sys.availability_replicas AS AR
	   ON AG.group_id = AR.group_id
	INNER JOIN master.sys.dm_hadr_availability_replica_states AS arstates
	   ON AR.replica_id = arstates.replica_id AND arstates.is_local = 1
	INNER JOIN master.sys.dm_hadr_database_replica_cluster_states AS dbcs
	   ON arstates.replica_id = dbcs.replica_id
	LEFT OUTER JOIN master.sys.dm_hadr_database_replica_states AS dbrs
	   ON dbcs.replica_id = dbrs.replica_id AND dbcs.group_database_id = dbrs.group_database_id
	WHERE dbcs.is_database_joined = 1 /*AND agstates.primary_replica = '''+@ThisServer+'''*/
	ORDER BY AG.name ASC, dbcs.database_name
	
	) t1 on t1.DatabaseName = db.name 
	WHERE db.database_id > 4 AND db.user_access = 0 AND db.State = 0 
	AND t1.LocalReplicaRole IS NOT NULL
	'
	END
	END TRY
	BEGIN CATCH
			RAISERROR (N'Trouble with Availability Group database list',0,1) WITH NOWAIT;
	END CATCH
	SET @dynamicSQL = @dynamicSQL + ' OPTION (RECOMPILE);'
	INSERT INTO @Databases 
	EXEC sp_executesql @dynamicSQL ;




SET @errMessage= ERROR_MESSAGE()

BEGIN TRY
  IF (select CHARINDEX('\',@@SERVERNAME)) > 0
  /*Named instance will always use NetBIOS name*/
    SELECT @ThisServer = @@SERVERNAME
  IF (select CHARINDEX('\',@@SERVERNAME)) = 0
  /*Not named, use the NetBIOS name instead of @@ServerName*/
    SELECT @ThisServer = CAST( Serverproperty( 'ComputerNamePhysicalNetBIOS' ) AS NVARCHAR(500))
END TRY
BEGIN CATCH
  
  RAISERROR ('Error',0,1) WITH NOWAIT; 
END CATCH


 IF @MailProfile = ''
 BEGIN

 SELECT TOP 1 @MailProfile = T1.[Profile_Name] --as [Profile_Name]
FROM (
SELECT DISTINCT mp.name [Profile_Name] --, sa.*, ss.*, si.* 
FROM msdb.dbo.sysmail_sentitems si
INNER JOIN msdb.dbo.sysmail_account sa ON  sa.account_id = si.sent_account_id
INNER JOIN msdb.dbo.sysmail_server ss ON  si.sent_account_id = ss.account_id
INNER JOIN msdb.dbo.sysmail_profile mp ON mp.profile_id = si.profile_id
WHERE sent_status = 'sent'
AND sent_date > DATEADD(WEEK,-3,GETDATE())
) T1

END


   declare @filterdate datetime
   DECLARE @rebootDT NVARCHAR (20)
   DECLARE @SQLServiceLastRestrartDT DATETIME
   DECLARE @dosStmt NVARCHAR (200)
   DECLARE @dosResult TABLE (line NVARCHAR (500))
 
   SET NOCOUNT ON
 

declare @svrName varchar(255)
declare @sql Nvarchar(4000)

--one day old
set @filterdate=GETDATE()-1
--note backup checks go back an addtional day



--failed login variables
   DECLARE @ErrorLogCount INT 
   DECLARE @LastLogDate DATETIME

   DECLARE @ErrorLogInfo TABLE (
       LogDate DATETIME
      ,ProcessInfo NVARCHAR (150)
      ,[Text] NVARCHAR (MAX)
      )
   
   DECLARE @EnumErrorLogs TABLE (
       [Archive#] INT
      ,[Date] DATETIME
      ,LogFileSizeMB INT
      )



declare @advanced_options int
declare @xp_cmdshell int

SELECT @advanced_options = cast(value_in_use as int)
FROM sys.configurations
WHERE name = 'show advanced options'




if @advanced_options=0
begin
	exec sp_configure 'show advanced options'   ,1;
	   RECONFIGURE
		WITH OVERRIDE;
end



SELECT @xp_cmdshell = cast(value_in_use  as int)
FROM sys.configurations
WHERE name = 'xp_cmdshell'


if @xp_cmdshell=0

BEGIN
    PRINT 'xp_cmdshell show advanced option is set to 0 ... setting it to 1'
	      EXEC sp_configure 'xp_cmdshell'   ,1;

    RECONFIGURE
    WITH OVERRIDE;
END







IF OBJECT_ID('tempdb..#DBA_issues') IS NOT NULL
	DROP TABLE #DBA_issues


   CREATE TABLE #DBA_issues (id INT IDENTITY(1,1)
	, SQLInstance NVARCHAR(50) 
	,datesnapshot NVARCHAR(20)
	,database_id NVARCHAR(20)
	,issue_type varchar(150)
	,issue_classification1 varchar(1550)
	,issue_classification2 varchar(1550)
	,issue_classification3 VARCHAR(6000)
	,issue_classification4 varchar(50)
   )
   CREATE CLUSTERED INDEX Ix_001 ON #DBA_issues( issue_type,id)
  
  DECLARE @now NVARCHAR(20)
  SELECT @now = CONVERT(VARCHAR,getdate(),120)

IF OBJECT_ID('tempdb..#DBA_issues_Out') IS NOT NULL
	DROP TABLE #DBA_issues_Out

  /*Table for output*/
   CREATE TABLE #DBA_issues_Out (id INT IDENTITY(1,1)
   ,[Domain] NVARCHAR(500) 
	, [SQLInstance] NVARCHAR(500) 
	,[evaldate] DATETIME
	,[Database] NVARCHAR(2000)
	,[Section] NVARCHAR(2000)
	,[Details] NVARCHAR(4000)
	, [Next Action] NVARCHAR(2000)
   )

/*---------------------------------------------------*//*---------------------------------------------------*/
/*the following databases have not had backups within the past 2 days (excludes logs)*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking for databases with no backups in the last 24 hours',0,1) WITH NOWAIT;

   INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
   SELECT 'datesnapshot','database_id','issue_type','database_name','LastBackup','LastBackupType'

DECLARE @backuptable TABLE (database_name NVARCHAR(250), backup_finish_date DATETIME, type VARCHAR(20))
INSERT @backuptable (database_name , backup_finish_date , type)
	SELECT database_name
			, backup_finish_date
			, CASE WHEN  type = 'D' THEN 'Full'    
			  WHEN  type = 'I' THEN 'Differential'                
			  WHEN  type = 'L' THEN 'Transaction Log'                
			  WHEN  type = 'F' THEN 'File'                
			  WHEN  type = 'G' THEN 'Differential File'                
			  WHEN  type = 'P' THEN 'Partial'                
			  WHEN  type = 'Q' THEN 'Differential partial'   
			  END AS type 
		FROM msdb.dbo.backupset x  
		WHERE 1=1-- backup_finish_date <= @filterdate
		and backup_finish_date = (
			SELECT max(backup_finish_date) 
			FROM msdb.dbo.backupset 
			WHERE database_name =   x.database_name 
		) 


INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
   SELECT 'datesnapshot','database_id','issue_type','database_name','LastBackup','LastBackupType'


 insert into #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
		SELECT @now,D.database_id,'No Log Backup',D.[name] AS [database_name], D.[recovery_model_desc]
FROM sys.databases D LEFT JOIN 
   (
   SELECT BS.[database_name], 
       MAX(BS.[backup_finish_date]) AS [last_log_backup_date]
   FROM msdb.dbo.backupset BS 
   
   WHERE BS.type = 'L'
   GROUP BY BS.[database_name]
   ) BS1 ON D.[name] = BS1.[database_name]
   INNER JOIN @Databases DT ON DT.databasename = D.name
WHERE D.[recovery_model_desc] <> 'SIMPLE'
   AND BS1.[last_log_backup_date] IS NULL
    AND  D.[name] <> 'model'
		AND (
	( [CurrentLocation] = @ThisServer AND  [BackupPref] = 'primary') 
	OR ( [CurrentLocation] <> @ThisServer AND  [BackupPref] = 'secondary')
	OR ( [BackupPref] = 'none')
	OR ( [BackupPref] IS NULL)
	)
ORDER BY D.[name]
OPTION (RECOMPILE); 
	RAISERROR (N'Checked for failed backups',0,1) WITH NOWAIT;

/*---------------------------------------------------*//*---------------------------------------------------*/
/*never been backed up*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking for databases that have never been backed up',0,1) WITH NOWAIT;

  insert into #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1)
 
   select @now,sdb.dbid,'Missing Database Backup',sdb.name 
   from master..sysdatabases sdb 
   left outer join msdb.dbo.backupset bs on sdb.name=bs.database_name
   	INNER JOIN @Databases D ON D.databasename = sdb.name
   where bs.database_name is null and dbid<>2
     AND (
	( [CurrentLocation] = @@SERVERNAME AND  [BackupPref] = 'primary') 
	OR ( [CurrentLocation] <> @@SERVERNAME AND  [BackupPref] = 'secondary')
	OR ( [BackupPref] = 'none')
	OR ( [BackupPref] IS NULL)
	)

/*---------------------------------------------------*//*---------------------------------------------------*/
/*Select all jobs with an error in the past 24 hours*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking for all jobs with an error in the past 24 hours',0,1) WITH NOWAIT;

 insert into #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3)
 
SELECT @now,0,'Failed Job',msdb.dbo.agent_datetime(jh.run_date,jh.run_time) as failure_date_time
    ,j.name as job_name,LEFT(jh.message,3900) as error_message
    FROM msdb.dbo.sysjobs AS j
   -- INNER JOIN msdb.dbo.sysjobsteps AS js ON js.job_id = j.job_id
    INNER JOIN msdb.dbo.sysjobhistory AS jh ON jh.job_id = j.job_id 
    WHERE jh.run_status = 0 AND msdb.dbo.agent_datetime(jh.run_date,jh.run_time) >= @filterdate and step_id=0
    ORDER BY msdb.dbo.agent_datetime(jh.run_date,jh.run_time) DESC
 
	
	
/*---------------------------------------------------*//*---------------------------------------------------*/
/*get drive space reports*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking at drive space usage',0,1) WITH NOWAIT;
--

--by default it will take the current server name, we can the set the server name as well
set @svrName = @@SERVERNAME
set @sql = 'powershell.exe -c "Get-WmiObject -ComputerName ' + QUOTENAME(@svrName,'''') + ' -Class Win32_Volume -Filter ''DriveType = 3'' | select name,capacity,freespace | foreach{$_.name+''|''+$_.capacity/1048576+''%''+$_.freespace/1048576+''*''}"'
--creating a temporary table

CREATE TABLE #output_diskstuff_dba_view
(line varchar(255))
--inserting disk name, total space and free space value in to temporary table

begin try
	insert #output_diskstuff_dba_view
	EXEC xp_cmdshell @sql

	insert into #DBA_issues(datesnapshot,issue_type,issue_classification1,issue_classification2,issue_classification3)

		select @now,'FreeSpace Report',rtrim(ltrim(SUBSTRING(line,1,CHARINDEX('|',line) -1))) as drivename
   ,round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('|',line)+1,
   (CHARINDEX('%',line) -1)-CHARINDEX('|',line)) )) as Float)/1024,0) as 'capacity(GB)'
   ,round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('%',line)+1,
   (CHARINDEX('*',line) -1)-CHARINDEX('%',line)) )) as Float) /1024 ,0)as 'freespace(GB)'
	from #output_diskstuff_dba_view
	where line like '[A-Z][:]%'
	AND 
	(round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('%',line)+1,
   (CHARINDEX('*',line) -1)-CHARINDEX('%',line)) )) as Float) /1024 ,0)
   / 
   round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('|',line)+1,
   (CHARINDEX('%',line) -1)-CHARINDEX('|',line)) )) as Float)/1024,0)
   < 0.05)
	order by drivename
end try
begin catch
     SELECT ERROR_NUMBER() as ErrorNumber, ERROR_MESSAGE() as ErrorMessage;
 
end catch


begin try

   SET @dosStmt = 'wmic os get lastbootuptime'
 
   INSERT INTO @dosResult EXEC sys.xp_cmdShell @dosStmt
 
   SELECT @rebootDT =  (
         SUBSTRING (line, 1, 4) + '-'+
         SUBSTRING (line, 5, 2) + '-'+
         SUBSTRING (line, 7, 2) + ' '+
         SUBSTRING (line, 9, 2) + ':'+
         SUBSTRING (line, 11, 2)+ ':'+
         SUBSTRING (line, 13, 2)
         )
   FROM @dosResult
   WHERE CHARINDEX ('.', line, 1) > 0
 
   SELECT @SQLServiceLastRestrartDT = CONVERT(VARCHAR,create_date,120)
   FROM sys.databases
   WHERE rtrim(ltrim(upper([name]))) = 'TEMPDB'
   



/*---------------------------------------------------*//*---------------------------------------------------*/
/*last SQL Restart*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking at last SQL Restart',0,1) WITH NOWAIT;

  insert into #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
 
   SELECT @now,0,'Last Restart Info',@rebootDT as OSServerRebootDateTime, @SQLServiceLastRestrartDT as SQLServiceRestartDateTime
   WHERE DATEDIFF(HOUR,@SQLServiceLastRestrartDT, GETDATE()) < 24
 end try  
 begin catch

 end catch
--script to retrieve the values in MB from PS Script output
--select rtrim(ltrim(SUBSTRING(line,1,CHARINDEX('|',line) -1))) as drivename
--   ,round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('|',line)+1,
--   (CHARINDEX('%',line) -1)-CHARINDEX('|',line)) )) as Float),0) as 'capacity(MB)'
--   ,round(cast(rtrim(ltrim(SUBSTRING(line,CHARINDEX('%',line)+1,
--   (CHARINDEX('*',line) -1)-CHARINDEX('%',line)) )) as Float),0) as 'freespace(MB)'
--from #output
--where line like '[A-Z][:]%'
--order by drivename
--script to retrieve the values in GB from PS Script output

--script to drop the temporary table
drop table #output_diskstuff_dba_view







/*---------------------------------------------------*//*---------------------------------------------------*/
/*failed login section*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Looking at failed logins',0,1) WITH NOWAIT;

--from https://www.mssqltips.com/sqlservertip/4941/find-all-failed-sql-server-logins/
   SET NOCOUNT ON


   INSERT INTO @EnumErrorLogs
   EXEC sp_enumerrorlogs

   SELECT @ErrorLogCount = MIN([Archive#]), @LastLogDate = MAX([Date])
   FROM @EnumErrorLogs

   WHILE @ErrorLogCount IS NOT NULL
   BEGIN
	
	/*Filter Log and insert chunks*/
      INSERT INTO @ErrorLogInfo
      EXEC sp_readerrorlog @ErrorLogCount, 1, 'Logon', 'fail' 

	  INSERT INTO @ErrorLogInfo
      EXEC sp_readerrorlog @ErrorLogCount, 1, 'Placeium Somethingith Errorus', 'herous' 


      SELECT @ErrorLogCount = MIN([Archive#]), @LastLogDate = MAX([Date])
      FROM @EnumErrorLogs
      WHERE [Archive#] > @ErrorLogCount
      AND @LastLogDate > getdate() - 7 
  
   END



   -- List all last week failed logins count of attempts and the Login failure message


   
insert into #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2,issue_classification3,issue_classification4)
 
SELECT @now
   ,0
   ,'Failed Logins'
   ,COUNT (Text) AS NumberOfAttempts
   , Text AS Details
   , CONVERT(VARCHAR,MIN(LogDate),120) as MinLogDate
   , CONVERT(VARCHAR,MAX(LogDate),120) as MaxLogDate
   FROM @ErrorLogInfo
   WHERE ProcessInfo = 'Logon'
      AND Text LIKE '%fail%'
      AND LogDate > @filterdate - 6
   GROUP BY Text
   ORDER BY NumberOfAttempts DESC
   -----end of failed login section

 
 IF OBJECT_ID('msdb..SecurityChangeLog') IS NOT NULL
BEGIN
SET @sql = '
IF EXISTS(SELECT 1   FROM msdb.dbo.SecurityChangeLog 
	  WHERE DDLCommand like ''%sysadmin%''
	  AND EventTime>''' + CONVERT(NVARCHAR,@filterdate,120) + ''')
	  BEGIN
select ''' + CONVERT(NVARCHAR,@now,120) + ''' [datesnapshot]
	  ,''0'' [database_id]
	  ,''Login Modification'' [issue_type]
	  ,DDLCommand [issue_classification1]
	  ,EventTime [issue_classification2]
	  FROM msdb.dbo.SecurityChangeLog 
	  WHERE DDLCommand like ''%sysadmin%''
	  --AND EventTime>''' + CONVERT(NVARCHAR,@filterdate,120) + '''
	  END
	  '
	INSERT INTO #DBA_issues(datesnapshot,database_id,issue_type,issue_classification1,issue_classification2)
	EXEC sp_executesql @sql;

	   
END


/*---------------------------------------------------*//*---------------------------------------------------*/
/*Select output table*/
/*---------------------------------------------------*//*---------------------------------------------------*/
RAISERROR (N'Final output',0,1) WITH NOWAIT;
/**/

IF @SumJobs = 1 AND EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Failed Job')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
select DEFAULT_DOMAIN() [Domain],@ThisServer [SQLInstance], @now [evaldate],'----' [Database],'Failed Job Summary' [Section],'Count;JobName;error_message;First failure;Last failure' [Details],'' [Next Action]
	UNION ALL
	select DISTINCT DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'','Failed Job Summary'
	, CONVERT(VARCHAR(5),COUNT(issue_classification1 ) OVER (  PARTITION BY issue_classification2 ))
	+';'+ ISNULL(issue_classification2,'')
	+';'+ ISNULL(issue_classification3,'')
	+';'+ CONVERT(VARCHAR,MIN(issue_classification1 ) OVER (  PARTITION BY issue_classification2 ),120)
	+';'+ CONVERT(VARCHAR,MAX(issue_classification1 ) OVER (  PARTITION BY issue_classification2 ),120)
	, 'Assign to DBA'[Next Action]
	FROM #DBA_issues where issue_type='Failed Job'
END

IF @SumJobs = 0 AND EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Failed Job')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Failed Job','failure_date;JobName;error_message','' [Next Action]
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'',issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Summarise and assign to DBA'[Next Action]
	FROM #DBA_issues where issue_type='Failed Job'
END


IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Missing Database Backup')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	--online only
	Select  DEFAULT_DOMAIN(),@ThisServer, @now,'----','Missing Database Backup','database_name;issue_classification2;issue_classification3',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(sd.database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Assign to DBA'[Next Action]
	FROM #DBA_issues di
	join sys.databases sd on di.database_id=sd.database_id
	where issue_type= 'Missing Database Backup'  and sd.state=0
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Old Database Backup')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	  --online only
	Select DEFAULT_DOMAIN(),@ThisServer, @now,'----','Old Database Backup','database_name;LastBackup;LastBackupType',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(sd.database_id)),issue_type
	,ISNULL(issue_classification1,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Follow up with client'[Next Action]
	from #DBA_issues di
	join sys.databases sd on di.database_id=sd.database_id
	where issue_type= 'Old Database Backup'  and sd.state=0
END


IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='No Log Backup')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer,  @now,'----','No Log Backup','database_name;LastBackup;LastBackupType',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(sd.database_id)),issue_type
	,ISNULL(issue_classification1 ,'')
	+';'+ISNULL(issue_classification2,'')
	+';'+ISNULL(issue_classification3,'')
	, 'Follow up with client' [Next Action]
	FROM #DBA_issues di
	join sys.databases sd on di.database_id=sd.database_id
	where issue_type='No Log Backup'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Freespace Report')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select  DEFAULT_DOMAIN(),@ThisServer, @now,'----','Freespace Report','database_name;capacity(GB);Freespace(GB)',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,'',issue_type
	,ISNULL(issue_classification1 ,'')
	+';'+ISNULL(issue_classification2  ,'')
	+';'+ISNULL(issue_classification3 ,'')
	, 'Check by DBA'[Next Action]
	FROM #DBA_issues di
	where issue_type='Freespace Report'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Last Restart Info')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select DEFAULT_DOMAIN(),@ThisServer,  @now,'----','Last Restart Info','Last_SQL_Restart;Last_windows_Restart',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification2 ,'')
	+';'+ISNULL(issue_classification1 ,'')
	, 'None'[Next Action]
	FROM #DBA_issues di
	where issue_type='Last Restart Info'
END

IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Failed Logins')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
 	Select  DEFAULT_DOMAIN(),@ThisServer, @now,'----','Failed Logins','Failure Count;Failure Text;MinFailureDate;MaxFailureDate',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1 ,'')
	+';'+ISNULL(issue_classification2 ,'')
	+';'+ISNULL(issue_classification3,'')
	+';'+ISNULL(issue_classification4,'')
	, 'Raise with client'[Next Action]
	FROM #DBA_issues di
	where issue_type='Failed Logins'  
END
		--select * from #DBA_issues di
IF EXISTS (SELECT 1 FROM #DBA_issues di where issue_type='Login Modification')
BEGIN
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select  DEFAULT_DOMAIN(),@ThisServer, @now,'----','Login Modification' ,'Failure Count;Failure Text;MinFailureDate;MaxFailureDate',''
	UNION ALL
	select DEFAULT_DOMAIN(),@ThisServer, datesnapshot,CONVERT(NVARCHAR(50),db_name(database_id)),issue_type
	,ISNULL(issue_classification1 ,'')
	+';'+ISNULL(issue_classification2 ,'')
	+';'+ISNULL(issue_classification3  ,'')
	+';'+ISNULL(issue_classification4  ,'')
	, 'Raise with client'[Next Action]
	FROM #DBA_issues di
	where issue_type='Login Modification'  
END
INSERT INTO #DBA_issues_Out ( [Domain],[SQLInstance],[evaldate],[Database],[Section],[Details],[Next Action])
	Select  DEFAULT_DOMAIN(),@ThisServer, @now,'----','Check In' ,'',''


	DECLARE @TableHTML NVARCHAR(MAX)
	DECLARE @CSSHTML NVARCHAR(MAX) /*Because, me, I'm not a dirty little DBA, I'm a million $$ DBA*/
	DECLARE @OUTHTML NVARCHAR(MAX)
	SET @CSSHTML = '<style type="text/css">
   body {   padding: 0 2em;   font-family: Verdana, sans-serif;   -webkit-font-smoothing: antialiased;   text-rendering: optimizeLegibility;   color: #444;   background: #eee; }  
   table {   background: #34495E;   color: #fff;   border-radius: .4em;   overflow: hidden; margin: 1em 0;   min-width: 300px;} 
   tr {   border-top: 1px solid #ddd;   border-bottom: 1px solid #ddd; } 
   tr {   border-color: #46637f; } 
   th, caption, td:before {   font-weight: bold !important;   letter-spacing: -1px;   color: #FFFF00 !important; font-weight: bold !important;;} 
   td:before {   font-weight: bold;   width: 6.5em;   display: inline-block; } 
   td {   display: block; color: #FFFFFF} 
   th, td {   text-align: left; margin: 4px 6pxem; } 
   td:first-child {   padding-top: .5em; } 
   td:last-child {   padding-bottom: .5em; } 
   @media (min-width: 480px) {   
   td:before {     display: none;   } 
   th,  td {     display: table-cell;     padding: .25em .5em; padding: 4px !important;   }    
   th:first-child, td:first-child {     padding-left: 0;   }   
   th:last-child, td:last-child {     padding-right: 0;   } 
   }
</style>'

	SET @TableHTML = (
	SELECT
    (SELECT 'Table I of I which is a daily check' FOR XML PATH(''),TYPE) AS 'caption',
    (SELECT '[Domain]' AS th
	, '[SQLInstance]' AS th 
	, '[evaldate]' AS th 
	, '[Database]' AS th 
	, '[Section]' AS th 
	, '[Details]' AS th 
	, '[Next Action]' AS th
	FOR XML raw('tr'),ELEMENTS, TYPE) AS 'thead',
    --(SELECT 'sum' AS th, 'twenty' AS th FOR XML raw('tr'),ELEMENTS, TYPE) AS 'tfoot',
    (SELECT --F.unus AS td, F.duo AS td
	[Domain] AS td 
	,[SQLInstance] AS td 
	,[evaldate] AS td 
	,[Database] AS td 
	,[Section] AS td 
	,[Details] AS td 
	,[Next Action] AS td
       FROM #DBA_issues_Out
       --  (VALUES
       --     ('one', 'two'),
       --     ('three', 'four'),
       --     ('five', 'six'),
       --     ('seven', 'eight')
       --  ) F(unus, duo)
    FOR XML RAW('tr'), ELEMENTS, TYPE
    ) AS 'tbody'
  FOR XML PATH(''), ROOT('table')
  )




if @xp_cmdshell=0
BEGIN
    PRINT 'xp_cmdshell show  was set to 0 ... setting it back to 0'
	      EXEC sp_configure 'xp_cmdshell'   ,0;

    RECONFIGURE
    WITH OVERRIDE;
END



if @advanced_options=0
begin
    PRINT 'xp_cmdshell show advanced option was 0 ... setting it back to 0'
	exec sp_configure 'show advanced options'   ,0;
	   RECONFIGURE
		WITH OVERRIDE;
end

/*Recreate output table*/
DECLARE @Fulloutput NVARCHAR(500)
SET @Fulloutput = @OutputDB + '.' + @OutputSchema + '.' + @OutputTable

BEGIN
	/*Create output table*/
	SET @sql = '
	USE ['+@OutputDB+']
IF OBJECT_ID(''' + @Fulloutput +''') IS NOT NULL
	BEGIN
		DROP TABLE ' + @Fulloutput + '
	END'
	
	/*EXEC ONLY IF THERE ARE SCHEMA clashes sp_executesql @sql;*/

SET @sql = 'USE ['+@OutputDB+']
IF NOT EXISTS ( SELECT  * FROM sys.schemas WHERE name = N''' + @OutputSchema + ''' )
    EXEC( ''CREATE SCHEMA [' + @OutputSchema +']'');
	'
	EXEC sp_executesql @sql;


SET @sql = 'USE ['+@OutputDB+']
IF OBJECT_ID(''' + @Fulloutput +''') IS NULL
CREATE TABLE ' +  @Fulloutput + ' (id INT
    ,[Domain] NVARCHAR(500) 
	, [SQLInstance] NVARCHAR(500) 
	, [evaldate] DATETIME
	, [Database] NVARCHAR(2000)
	, [Section] NVARCHAR(2000)
	, [Details] NVARCHAR(4000)
	, [Next Action] NVARCHAR(2000)
   )'
   EXEC sp_executesql @sql;


   /*What is a table with a view*/
   IF OBJECT_ID( 'msdb.dbo.viewof_dba_quickview') IS NULL
   BEGIN
   SET @sql = 'CREATE VIEW dbo.viewof_dba_quickview
AS
SELECT        qv.id, qv.Domain, qv.SQLInstance, qv.evaldate, qv.[Database], qv.Section, qv.Details, qv.[Next Action]
FROM            '+ @OutputDB +'.' + @OutputSchema + '.' + @OutputTable + ' AS qv INNER JOIN
(SELECT        MAX(evaldate) AS Maxevaldate
FROM             '+ @OutputDB +'.' + @OutputSchema + '.' + @OutputTable + ') AS qv_latest ON qv.evaldate = qv_latest.Maxevaldate
   '
 
	EXEC sp_executesql @sql;
	END
SET @sql = '
INSERT INTO ' + @Fulloutput + '
  SELECT id
	, [Domain] 
	,[SQLInstance]
	,[evaldate] 
	,[Database] 
	,[Section] 
	,[Details] 
	,[Next Action]
      FROM #DBA_issues_Out
	  ORDER BY id
	'
	EXEC sp_executesql @sql;
END

SET @OUTHTML = @CSSHTML + ' ' +@TableHTML
IF(@SendEmail = 0) AND EXISTS(SELECT 1 FROM #DBA_issues_Out)/**/
BEGIN
SELECT --F.unus AS td, F.duo AS td
	[Domain] 
	,[SQLInstance]
	,Convert(VARCHAR,CONVERT(VARCHAR,[evaldate],120 ),23)[evaldate] 
	,[Database] 
	,[Section] 
	,[Details] 
	,[Next Action]
      FROM #DBA_issues_Out
	  ORDER BY id
END
SET @subject = @Client + '. ' + @subject + '. ' + CONVERT(NVARCHAR,GETDATE(),120)
IF (@SendEmail = 1) AND EXISTS(SELECT 1 FROM #DBA_issues_Out)
BEGIN
	EXEC msdb.dbo.sp_send_dbmail  
	@profile_name = @MailProfile,    
	@recipients=@Recepients,    
	@subject = @subject  ,    
	@body = @OUTHTML,    
	@body_format = 'HTML' ;   

END


SET @sql = '
DELETE ' + @Fulloutput + '
WHERE evaldate < DATEADD(DAY,-' + CONVERT(NVARCHAR(5), @RetentionDays) +',GETDATE())'
	EXEC sp_executesql @sql;


/*Good scripts always clean up after themselves*/
IF OBJECT_ID('tempdb..#DBA_issues') IS NOT NULL
	DROP TABLE #DBA_issues
IF OBJECT_ID('tempdb..#DBA_issues_Out') IS NOT NULL
	DROP TABLE #DBA_issues_Out
IF OBJECT_ID('tempdb..#output_diskstuff_dba_view') IS NOT NULL
	DROP TABLE #output_diskstuff_dba_view
	
END
GO

/*

SP_DBA_QUICKVIEW 
@Client = '<Client>' -- Name of project or cleint 
, @Recepients ='adrian.sullivan@lexel.co.nz' -- Recepient(s) of this email (; separated in case of multiple recepients).
, @subject  = 'DBA Quick View Email'
, @SendEmail = 0
, @SumJobs = 1

*/

/*
msdb.dbo.SP_DBA_QUICKVIEW 
@Client = 'Douglas Pharmaceuticals' -- Name of project or client 
, @Recepients ='sqlalerts@sqldba.org' -- Recepient(s) of this email (; separated in case of multiple recepients).
, @subject  = 'DBA Quick View Email'
, @SendEmail = 1
, @SumJobs = 1



*/

/* Not used in this example
, @OutputTable NVARCHAR(50) = 'dba_quickview' /*Full Schema, DB.schema.table please, ideally the dba schema, master.dba.dba_quickview*/
, @OutputSchema NVARCHAR(50) = 'dba' /*I prefer the DBA schema, as these are, well, DBA things*/
, @OutputDB NVARCHAR(50) = 'master' /*Defaults to master in any event*/
, @MailProfile  = '' -- Mail profile name which exists on the target database server
*/







