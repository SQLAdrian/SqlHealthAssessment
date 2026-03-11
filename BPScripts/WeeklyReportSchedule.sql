/* بسم الله الرحمن الرحيم  */
/* In the name of God, the Merciful, the Compassionate */

/* Because I don't trust your ability to configure SQL mail servers, I thought I would script this one out.
Thanks to Tim Ford
Original link: http://sqlmag.com/database-administration/how-set-sql-server-database-mail-one-easy-script
*/
--==========================================================
-- Enable Database Mail
--==========================================================
USE master;
GO
sp_configure 'show advanced', 1
GO
RECONFIGURE
GO
sp_configure 'Database Mail XPs', 1
GO
RECONFIGURE
GO 

DECLARE @NeedMailSetup INT

SET @NeedMailSetup = 0
--==========================================================
-- Clean Up Operators
--==========================================================

/*Remove Lexel Service Desk Operator if exists*/
BEGIN TRY
EXEC msdb.dbo.sp_delete_operator @name=N'Lexel Service Desk'
END TRY
BEGIN CATCH
	PRINT 'No Lexel Service Desk Operator found'
END CATCH

/*Update Lexel Operator mail addres*/
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
	PRINT 'Error with Lexel DBA Operator'
END CATCH

/*Create SQLDBA Operator*/
BEGIN TRY
	EXEC msdb.dbo.sp_add_operator @name=N'SQLDBA', 
		@enabled=1, 
		@pager_days=0, 
		@email_address=N'alerts@sqldba.org'
END TRY
BEGIN CATCH
	PRINT 'Error with SQLDBA Operator'
END CATCH
--================================================================
-- DATABASE MAIL CONFIGURATION
--================================================================

DECLARE @ThisDomain NVARCHAR(100), @ThisServer NVARCHAR(100), @HostName NVARCHAR(100), @Address NVARCHAR(100), @MachineKey1 VARBINARY(100)  ,@MachineKey NVARCHAR(100)
DECLARE @CharToCheck NVARCHAR(5) , @Customer NVARCHAR(50)

--===============================================================================================================================
--===============================================================================================================================
--===============================================================================================================================
SET @Customer = 'Best and Less'/*CLIENT NAME HERE*/

--===============================================================================================================================
--===============================================================================================================================
--===============================================================================================================================



DECLARE @HashMe NVARCHAR(250)
SET @CharToCheck = CHAR(92)

EXEC master.dbo.xp_regread 'HKEY_LOCAL_MACHINE', 'SYSTEM\CurrentControlSet\services\Tcpip\Parameters', N'Domain',@ThisDomain OUTPUT
SET @ThisDomain = ISNULL(@ThisDomain, DEFAULT_DOMAIN())

SELECT @ThisServer = @@SERVERNAME
  IF (select CHARINDEX(@CharToCheck,@@SERVERNAME)) = 0
  /*Not named, use the NetBIOS name instead of @@ServerName*/
SELECT @ThisServer = CAST( Serverproperty( 'ComputerNamePhysicalNetBIOS' ) AS NVARCHAR(500))

SELECT  @HashMe = ISNULL(@ThisDomain,'') + 'SQLDBA.ORG' + ISNULL(hostname,'') + ISNULL(@ThisServer,'') + ISNULL(net_address ,'')
, @HostName = REPLACE(hostname,' ','')
, @Address = net_address
from master.dbo.sysprocesses
where 1=1--spid = @@SPID
AND program_name = 'SQLAgent - Generic Refresher'


SET @MachineKey1 = HASHBYTES('SHA1',@HashMe)
SET @MachineKey = UPPER(master.dbo.fn_varbintohexsubstring(0,@MachineKey1, 1, 0))


select @ThisDomain [Domain], @HostName [HostName],@ThisServer [SQLInstance] ,@Address [Address]
, HASHBYTES('SHA1 ',@HashMe) [MachineKey]
from master.dbo.sysprocesses
where spid = @@SPID

DECLARE @serverdisplayname NVARCHAR(250);
DECLARE @replytostring NVARCHAR(250);
SET @serverdisplayname = 'SQL_Alert'
+ '|' + @ThisDomain 
+ '|' + @HostName
+ '|'+ REPLACE(@ThisServer,@CharToCheck,'_')
+ '|'+RIGHT(CONVERT(NVARCHAR(250),@MachineKey,1),7)
+ CONVERT(NVARCHAR(8),@MachineKey,1)
+ 'x'+SUBSTRING(CONVERT(NVARCHAR(100),@MachineKey,1),7,6)
SET @replytostring = 'SQL_Alert'
+ '_' + @ThisDomain 
+ '_' + @HostName
+ '_'+ REPLACE(@ThisServer,@CharToCheck,'-')
+ '_'+RIGHT(CONVERT(NVARCHAR(250),@MachineKey,1),7)
+ CONVERT(NVARCHAR(8),@MachineKey,1)
+ 'x'+SUBSTRING(CONVERT(NVARCHAR(100),@MachineKey,1),7,6)
+ '@sqldba.org'
SELECT @serverdisplayname

DECLARE @MailFrom NVARCHAR(50);
--SET @MailFrom = @ThisServer + '@NZAA.co.nz' 
SET @MailFrom = 'sqlalerts@sqldba.org' 

DECLARE @ProfileName NVARCHAR(50)
SET @ProfileName = 'DBA Mail Profile'

DECLARE @AccountName NVARCHAR(50)
SET @AccountName = 'DBA_Email_Account'

DECLARE @Testrecipients NVARCHAR(100)
SET @Testrecipients = 'sqlalerts@sqldba.org'


IF @NeedMailSetup = 1
BEGIN
	--==========================================================
	-- Create a Database Mail account
	--==========================================================
	DECLARE @SMTPTable TABLE( Id INT
	, [Type] VARCHAR(50)
	, Port INT
	, ServerName VARCHAR(500)
	)
	INSERT INTO @SMTPTable ( Id,[Type],Port, ServerName)
	VALUES
	(1, 'IMAP', 993, 'outlook.office365.com')
	,(2, 'POP', 995, 'outlook.office365.com')
	,(3, 'SMTP', 587,'smtp-mail.outlook.com')
	,(4, 'SMTP', 25,'smtp-mail.outlook.com')
	,(5, 'SMTP', 587,'smtp.outlook.com')
	,(6, 'SMTP', 25,'smtp.outlook.com')
	,(7, 'IMAP', 465,'outlook.office365.com')

	DECLARE @port INT, @smtpserver NVARCHAR(500), @ThePassword NVARCHAR(500), @TheAccount NVARCHAR(500)
	DECLARE @smtpsetting INT
	SET @smtpsetting = 3 /*3 default*/

	SELECT @port = Port
	, @smtpserver =ServerName
	FROM @SMTPTable 
	WHERE Id = @smtpsetting

	SET @ThePassword = 'SELECT*FROMUSER@02!'
	SET @TheAccount = 'sqlalerts@sqldba.org'
	/*
	When all else fails
	Server	smtp.sendgrid.net
	Ports	
	25, 587	(for unencrypted/TLS connections)
	465	(for SSL connections)
	Username	apikey
	Password	<REDACTED — replace with your SendGrid API key>

	*/
	/*
	IMAP server name outlook.office365.com, port 993, encryption method TLS
	POP server name outlook.office365.com, port 995, encryption method TLS
	SMTP server name smtp-mail.outlook.com, port 587, encryption method STARTTLS
	*/
	--==========================================================
	-- Create a Database Mail account
	--==========================================================
	IF EXISTS( SELECT 1 FROM msdb.dbo.sysmail_account WHERE name = @AccountName)
	BEGIN
	/*Likely that we have been here before
	Check if mails are going out*/
	IF (
		  select COUNT(1) from msdb.dbo.sysmail_event_log
		  WHERE last_mod_date > DATEADD(WEEK,-1,GETDATE())
		  and event_type IN ('information','warning')
		  /*There has been mails going out*/
		  ) > 0
		  AND (
		  select COUNT(1) from msdb.dbo.sysmail_event_log
		  WHERE last_mod_date > DATEADD(WEEK,-1,GETDATE())
		  and event_type IN ('error')
		  /*Cannot find any errors*/
		  ) = 0
	/*Assume it works, just update display name and reply to*/
		SELECT 'Please update "reply to" to:',@replytostring
	ELSE
	EXECUTE msdb.dbo.sysmail_update_account_sp @account_name = @AccountName,
		@description = 'Mail account used for system alerts',
		@email_address = @MailFrom,
		@replyto_address = @MailFrom,
		@display_name = @serverdisplayname,
		@mailserver_name = @smtpserver,
		@port = @port,
		@username = @TheAccount,
		@password = @ThePassword,
		@enable_ssl = 1
		;
	END
	ELSE
	BEGIN
	EXECUTE msdb.dbo.sysmail_add_account_sp
		@account_name = @AccountName,
		@description = 'Mail account used for system alerts',
		@email_address = @MailFrom,
		@replyto_address = @MailFrom,
		@display_name = @serverdisplayname,
		@mailserver_name = @smtpserver,
		@port = @port,
		@username = @TheAccount,
		@password = @ThePassword,
		@enable_ssl = 1
		;
	END

	DECLARE @subject NVARCHAR(250), @bodyText NVARCHAR(2000)
	SET @bodyText = 'This is a new test for this machine. 
		 Auth:ac9sv2acb6s;
		 Client:'+ @Customer+ ';
		 Address:' + @Address +';
		 MachineKey:'+ CONVERT(NVARCHAR(100),@MachineKey,1) +';'

	SET @subject = 'sqldba_new_alert_server_reg;' + @serverdisplayname
	EXEC msdb.dbo.sp_send_dbmail
		 @recipients = 'sqlalerts@sqldba.org',
		 @subject = @subject,
		 @body = @bodyText


	--==========================================================
	-- Create a Database Mail Profile
	--==========================================================
	DECLARE @profile_id INT, @profile_description sysname;
	SELECT @profile_id = COALESCE(MAX(profile_id),1) FROM msdb.dbo.sysmail_profile
	SELECT @profile_description = 'Database Mail Profile for ' + @@servername 

	IF NOT EXISTS ( SELECT * FROM msdb.dbo.sysmail_profile WHERE name = @ProfileName)
	BEGIN
		EXECUTE msdb.dbo.sysmail_add_profile_sp
		@profile_name = @ProfileName,
		@description = @profile_description;

		-- Add the account to the profile
		EXECUTE msdb.dbo.sysmail_add_profileaccount_sp
		@profile_name = @ProfileName,
		@account_name = @AccountName,
		@sequence_number = @profile_id;

		-- Grant access to the profile to the DBMailUsers role
		EXECUTE msdb.dbo.sysmail_add_principalprofile_sp
		@profile_name = @ProfileName,
		@principal_id = 0,
		@is_default = 0 ;

	END

END
EXECUTE msdb.dbo.sysmail_configure_sp 'MaxFileSize', '30000000'; /*To around 27MB.. which is huge*/

--EXEC master.dbo.xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'SOFTWARE\Microsoft\MSSQLServer\SQLServerAgent', N'DatabaseMailProfile', N'REG_SZ', N''
--EXEC master.dbo.xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'SOFTWARE\Microsoft\MSSQLServer\SQLServerAgent', N'UseDatabaseMail', N'REG_DWORD', 1
--GO

EXEC msdb.dbo.sp_set_sqlagent_properties @email_save_in_sent_folder = 0


--==========================================================
-- Test Database Mail
--==========================================================
DECLARE @sub VARCHAR(100)
DECLARE @body_text NVARCHAR(MAX)
SELECT @sub = 'Test from New SQL install on ' + @@servername + '.' + @smtpserver+ ':' + CONVERT(VARCHAR(15),@port)
PRINT @sub
SELECT @body_text = N'This is a test of Database Mail.' + CHAR(13) + CHAR(13) + 'SQL Server Version Info: ' + CAST(@@version AS VARCHAR(500))

EXEC msdb.dbo.[sp_send_dbmail] 
    @profile_name = @ProfileName
  , @recipients = @Testrecipients
  , @subject = @sub
  , @body = @body_text


--================================================================
-- SQL Agent Properties Configuration
--================================================================
EXEC msdb.dbo.sp_set_sqlagent_properties 
        @databasemail_profile =  @ProfileName
        , @use_databasemail=1


--==========================================================
-- Review Outcomes
--==========================================================
SELECT * FROM msdb.dbo.sysmail_profile;
SELECT * FROM msdb.dbo.sysmail_account;


DECLARE @SQLsn NVARCHAR(128);
DECLARE @SQLVersion INT
SELECT @SQLVersion = @@MicrosoftVersion / 0x01000000  OPTION (RECOMPILE)-- Get major version
/*WHERE servicename like 'SQL Server%';*/
BEGIN TRY
DECLARE @DBEngineLogin VARCHAR(100)
DECLARE @DBAgentLogin VARCHAR(100)
	
	BEGIN 

	EXECUTE master.dbo.xp_instance_regread
	   @rootkey = N'HKEY_LOCAL_MACHINE',
	   @key = N'SYSTEM\CurrentControlSet\Services\MSSQLServer',
	   @value_name = N'ObjectName',
	   @value = @DBEngineLogin OUTPUT

	
	EXECUTE master.dbo.xp_instance_regread
	   @rootkey = N'HKEY_LOCAL_MACHINE',
	   @key = N'SYSTEM\CurrentControlSet\Services\SQLSERVERAGENT',
	   @value_name = N'ObjectName',
	   @value = @DBAgentLogin OUTPUT
	END
END TRY
BEGIN CATCH
	RAISERROR (N'Trouble with SQL Services',0,1) WITH NOWAIT;
END CATCH



IF @DBEngineLogin <> @DBAgentLogin 
BEGIN 
	DECLARE @RunMe NVARCHAR(2000)
	SET @RunMe = '
	USE [master]
	CREATE LOGIN  [' + @DBAgentLogin + '] FROM WINDOWS WITH DEFAULT_DATABASE=[master]' 
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH
	
	SET @RunMe = 'USE [master]
	CREATE USER [' + @DBAgentLogin + '] FOR LOGIN  [' + @DBAgentLogin + ']' 
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	SET @RunMe = 'USE [master]
	ALTER ROLE [db_datareader] ADD MEMBER [' + @DBAgentLogin + ']
	ALTER ROLE [db_datawriter] ADD MEMBER [' + @DBAgentLogin + ']' 
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	SET @RunMe = 'USE [master]
	EXEC sp_addrolemember N''db_datareader'', N''' + @DBAgentLogin + ''' 
	EXEC sp_addrolemember N''db_datawriter'', N''' + @DBAgentLogin + ''' '
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	SET @RunMe = 'USE [msdb]
	CREATE USER [' + @DBAgentLogin + '] FOR LOGIN [' + @DBAgentLogin + '] '
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	SET @RunMe = 'USE [msdb]
	ALTER ROLE [DatabaseMailUserRole] ADD MEMBER [' + @DBAgentLogin + ']
	ALTER ROLE [db_owner] ADD MEMBER [' + @DBAgentLogin + ']
	ALTER ROLE [ServerGroupAdministratorRole] ADD MEMBER [' + @DBAgentLogin + ']'
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	SET @RunMe = 'USE [msdb]
	EXEC sp_addrolemember N''DatabaseMailUserRole'', N''' + @DBAgentLogin + ''' '
	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH

	PRINT @RunMe
	BEGIN TRY
	EXEC sp_executesql @RunMe
	END TRY
	BEGIN CATCH
		PRINT 'Error'
	END CATCH
END 



--==========================================================
-- Add Export to String thing for weekly reports
--==========================================================
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
	DECLARE @A_Z NVARCHAR(500)
	DECLARE @compressoutput BIT
	SET @compressoutput = 1
	SET @qrs = '~';--char(9);
	SET @M_R ='scriptoutput@sqldba.org'
	SET @Es = 'Sqldba_sqlmagic_data for ' +@TD + ' '+@T_S + '' + REPLACE(REPLACE(REPLACE(@evaldate,'-','_'),':',''),' ','');
	SET @A_F = 'sqldba_sqlmagic_data__' +REPLACE(@TD,'.','_') + '_'+ REPLACE(@T_S,@Ctt,'_') + '_' + REPLACE(REPLACE(REPLACE(@evaldate,'-','_'),':',''),' ','') +'.csv' 
	SET @A_Z = 'sqldba_sqlmagic_data__' +REPLACE(@TD,'.','_') + '_'+ REPLACE(@T_S,@Ctt,'_') +'.zip' 
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
	--IF @compressoutput = 1
	--BEGIN
		--# https://blog.roostech.se/posts/compress-files-to-zip-with-powershell/'
		--'#First we add the .Net framework class needed for file compression.'
		SET @pstext = @pstext + 'Add-Type -As System.IO.Compression.FileSystem;'
		--'# Then we need a variable of the type System.IO.Compression.CompressionLevel. '
		--'# The options for compression level are "Fastest", "Optimal" and "NoCompression".'
		SET @pstext = @pstext + '[System.IO.Compression.CompressionLevel]$compression = "Optimal";'
		--# Which file do you want to compress?'
		--SET @pstext = @pstext + '$file = $outfile;'
		--'# Set the path to where you want the zip file to be created.'
		SET @pstext = @pstext + '$zippath = ($outfile).Replace(".csv","_csv.zip");'
		--# Open the zip file and set the mode. Options for mode are "Create", "Read" and "Update".'
		SET @pstext = @pstext + '$ziparchive = [System.IO.Compression.ZipFile]::Open( $zippath, "Update" );'
		--# The compression function likes relative file paths, so lets do that.'
		--SET @pstext = @pstext + '$relativefilepath = (Resolve-Path "$file" -Relative).TrimStart(".\");'
		--# This is where the magic happens. '
		--# Compress the file with the variables you just created as parameters.'
		SET @pstext = @pstext + '$null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($ziparchive, $outfile, "' + @A_F + '", $compression);'
		--# Release the zip file. '
		--# Otherwise the file will still be in read only if you are using Powershell ISE.'
		SET @pstext = @pstext + '$ziparchive.Dispose();'
		--zip up main archive @A_Z
		SET @pstext = @pstext + '$ziparchive = [System.IO.Compression.ZipFile]::Open( "' + @bu+'\'+ @A_Z +'", "Update" );'
		SET @pstext = @pstext + '$null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($ziparchive, $outfile, "' + @A_F + '", $compression);'
		--# Release the zip file. '
		--# Otherwise the file will still be in read only if you are using Powershell ISE.'
		SET @pstext = @pstext + '$ziparchive.Dispose();'

	--END
	
		
	SET @pstext = REPLACE(REPLACE(@pstext,'"','"""'),';;',';')
	SET @pstext = 'powershell.exe -ExecutionPolicy Bypass -NoLogo -NonInteractive -NoProfile -Command "' + @pstext + '" '
	PRINT @pstext
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

--==========================================================
-- Now time for the SQL Jobs
--==========================================================

USE [msdb]
GO


-----------
DECLARE @schedule_id_99 int, @Oldschedule_id_99 INT
DECLARE @ScheduleName NVARCHAR(500),@OldScheduleName NVARCHAR(500)
DECLARE @the_job_name NVARCHAR(500)
DECLARE @RandomStartHours BIGINT
DECLARE @RandomStartMinutes BIGINT
DECLARE @RandomStartSeconds BIGINT
DECLARE @RandomTime INT
DECLARE @StartStamp NVARCHAR(20)
DECLARE @the_job_Id BINARY(16)

SET @the_job_name = N'999. Send Weekly Stats'
SET  @ScheduleName= N'SQLDBA.ORG - Weekly Reports'
SET @OldScheduleName = N'LEXEL.CO.NZ - Weekly Reports'

SELECT @StartStamp = LEFT(REPLACE(CONVERT(NVARCHAR,GETDATE(),120),'-',''),8)
IF NOT EXISTS (SELECT * FROM msdb.dbo.sysschedules WHERE [name] = @ScheduleName)
	  BEGIN
	 
	SET @RandomStartHours = CONVERT(TINYINT,((RAND()*3)+3)) /*3 hours random plus 3 hours more than midnight*/
	SET @RandomStartMinutes = CONVERT(TINYINT,RAND()*59) /*Between 00 ~ 60 minutes*/
	SET @RandomStartSeconds = CONVERT(TINYINT,RAND()*59) /*Between 00 ~ 60 seconds*/
	 
	SET @RandomTime = @RandomStartHours * 10000 +@RandomStartMinutes * 100+ @RandomStartSeconds 

	  EXEC msdb.dbo.sp_add_schedule @schedule_name=@ScheduleName, @enabled=1, @freq_type=8, @freq_interval=64
  , @freq_subday_type=1, @freq_subday_interval=0, @freq_relative_interval=0, @freq_recurrence_factor=1, @active_start_date=@StartStamp
  , @active_end_date=99991231, @active_start_time=@RandomTime, @active_end_time=235959, @schedule_id = @schedule_id_99 OUTPUT
  	END
	ELSE
	BEGIN
		SELECT @schedule_id_99 =  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = @ScheduleName
	END
-----------------

DECLARE @ReturnCode INT
SELECT @ReturnCode = 0
IF NOT EXISTS (SELECT name FROM msdb.dbo.syscategories WHERE name=N'SQL Reports' AND category_class=1)
BEGIN
EXEC @ReturnCode = msdb.dbo.sp_add_category @class=N'JOB', @type=N'LOCAL', @name=N'SQL Reports'

END

DECLARE @jobId BINARY(16)
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
	SELECT @jobId = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name
END
ELSE 
BEGIN
EXEC @ReturnCode =  msdb.dbo.sp_add_job @job_name=@the_job_name, 
		@enabled=1, 
		@notify_level_eventlog=2, 
		@notify_level_email=2, 
		@notify_level_netsend=0, 
		@notify_level_page=0, 
		@delete_level=0, 
		@description=N'Weekly SQL report mailed to SQLDBA.ORG',
		@category_name=N'SQL Reports', 
		@owner_login_name=N'sa', 
		@notify_email_operator_name=N'SQLDBA', @job_id = @jobId OUTPUT

		EXEC dbo.sp_add_jobserver  
    	@job_name = @the_job_name ;  
END



/****** Remove old Lexel step ******/
IF EXISTS (SELECT  schedule_id FROM msdb.dbo.sysschedules WHERE [name] = @OldScheduleName)
BEGIN
SELECT @Oldschedule_id_99 = schedule_id FROM msdb.dbo.sysschedules WHERE [name] = @OldScheduleName

EXEC msdb.dbo.sp_detach_schedule @job_id=@jobId, @schedule_id=@Oldschedule_id_99, @delete_unused_schedule=1
END


IF NOT EXISTS(SELECT job_id FROM msdb.dbo.sysjobsteps WHERE [step_name] =  N'Run weekly stats - sp_Blitz') 
BEGIN
EXEC @ReturnCode = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Run weekly stats - sp_Blitz', 
		@step_id=1, 
		@cmdexec_success_code=0, 
		@on_success_action=3, 
		@on_success_step_id=0, 
		@on_fail_action=2, 
		@on_fail_step_id=0, 
		@retry_attempts=0, 
		@retry_interval=0, 
		@os_run_priority=0, @subsystem=N'TSQL', 
		@command=N'EXEC [dbo].[sqldba_sp_Blitz] @CheckUserDatabaseObjects = 1
, @CheckProcedureCache = 1
, @OutputType = ''TABLE''
, @OutputProcedureCache = 0
, @CheckProcedureCacheFilter = NULL
, @CheckServerInfo = 1
, @OutputDatabaseName = ''master''
, @OutputSchemaName = ''dbo''
, @OutputTableName = ''sqldba_sp_Blitz_output''
, @BringThePain = 1;
			', 
		@database_name=N'master', 
		@flags=0
END
ELSE
BEGIN
	EXEC msdb.dbo.sp_update_jobstep @job_id=@jobId, @step_id=1 , @on_success_action=3, @command=N'EXEC [dbo].[sqldba_sp_Blitz] @CheckUserDatabaseObjects = 1
, @CheckProcedureCache = 1
, @OutputType = ''TABLE''
, @OutputProcedureCache = 0
, @CheckProcedureCacheFilter = NULL
, @CheckServerInfo = 1
, @OutputDatabaseName = ''master''
, @OutputSchemaName = ''dbo''
, @OutputTableName = ''sqldba_sp_Blitz_output''
, @BringThePain = 1;
			'
END
IF NOT EXISTS(SELECT job_id FROM msdb.dbo.sysjobsteps WHERE [step_name] =  N'Run weekly stats - SecurityChecks') 
BEGIN

EXEC msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Run weekly stats - SecurityChecks', 
		@step_id=2, 
		@cmdexec_success_code=0, 
		@on_success_action=3, 
		@on_fail_action=2, 
		@retry_attempts=0, 
		@retry_interval=0, 
		@os_run_priority=0, @subsystem=N'TSQL', 
		@command=N'EXEC [dbo].[sqldba_stpSecurity_Checklist]', 
		@database_name=N'master', 
		@flags=0
	PRINT 'Create Step 2'
END

IF NOT EXISTS(SELECT job_id FROM msdb.dbo.sysjobsteps WHERE [step_name] =  N'Run weekly stats - SQLDBA sp_triage®') 
BEGIN
EXEC @ReturnCode = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Run weekly stats - SQLDBA sp_triage®', 
		@step_id=3, 
		@cmdexec_success_code=0, 
		@on_success_action=3, 
		@on_success_step_id=0, 
		@on_fail_action=2, 
		@on_fail_step_id=0, 
		@retry_attempts=0, 
		@retry_interval=0, 
		@os_run_priority=0, @subsystem=N'TSQL', 
		@command=N'[dbo].[sp_triage®]  @MailResults = 0, @Debug = 1', 
		@database_name=N'master', 
		@flags=0
		PRINT 'Create Step 3'
END
ELSE /*Update Existing*/
BEGIN
	EXEC msdb.dbo.sp_update_jobstep @job_id=@jobId, @step_id=3 , @on_success_action=3, @command=N'[dbo].[sp_triage®]  @MailResults = 0, @Debug = 1'
	PRINT 'Update step 3'
END

DECLARE @stepcmd NVARCHAR(4000)
SET @stepcmd = N'
DECLARE @Es NVARCHAR(500)
DECLARE @M_R NVARCHAR(500) 
	DECLARE @MaxDate DATETIME
	SELECT @MaxDate = MAX(evaldate) FROM [master].[dbo].[sqldba_sp_triage®_output] 
DECLARE @evaldate NVARCHAR(25)
DECLARE @TD NVARCHAR(50)
DECLARE @T_S NVARCHAR(50)
DECLARE @Ctt CHAR(1)
SET @Ctt = ''\''
SELECT @evaldate = evaldate, @TD = domain , @T_S = SQLInstance 
FROM [master].[dbo].[sqldba_sp_triage®_output]
WHERE evaldate = @MaxDate
DECLARE @EmailBody NVARCHAR(500) 
DECLARE @qrs NVARCHAR(50)
DECLARE @S_Ex NVARCHAR(4000)
DECLARE @EmailProfile NVARCHAR(500)
DECLARE @A_F NVARCHAR(500)
SET @qrs = ''~'';--char(9);
SET @M_R =''scriptoutput@sqldba.org''
SET @Es = ''Sqldba_sqlmagic_data for '' +@TD + '' ''+@T_S + '''' + REPLACE(REPLACE(REPLACE(@evaldate,''-'',''_''),'':'',''''),'' '','''');
SET @A_F = ''sqldba_sqlmagic_data__'' +REPLACE(@TD,''.'',''_'') + ''_''+ REPLACE(@T_S,@Ctt,''_'') + ''_'' + REPLACE(REPLACE(REPLACE(@evaldate,''-'',''_''),'':'',''''),'' '','''') +''.csv'' 
DECLARE @xpSQL NVARCHAR(4000)


SET @EmailProfile = ''DBA Mail Profile''
DECLARE @MprofileTable TABLE (profile_id INT, name NVARCHAR(250), description NVARCHAR(500))
INSERT @MprofileTable
EXEC msdb.dbo.sysmail_help_profile_sp
IF EXISTS(SELECT name from @MprofileTable)
BEGIN
	SELECT TOP 1 @EmailProfile = name from @MprofileTable
END
IF EXISTS(SELECT name from @MprofileTable WHERE name = ''DBA Mail Profile'')
BEGIN
	SET @EmailProfile = ''DBA Mail Profile''
END

BEGIN TRY
EXEC [master].[dbo].[sp_triage®_to_text]
END TRY
BEGIN CATCH
END CATCH
SET @S_Ex = ''
SET NOCOUNT ON;
SELECT ID,evaldate,domain,SQLInstance,SectionID,Section,Summary,Severity,Details,HoursToResolveWithTesting,QueryPlan
FROM (
SELECT 
CONVERT(NVARCHAR(25),		''''ID'''') ID
, CONVERT(NVARCHAR(50),		''''evaldate'''') evaldate
, CONVERT(NVARCHAR(50),		''''domain'''') domain
, CONVERT(NVARCHAR(50),		''''SQLInstance'''' ) SQLInstance
, CONVERT(NVARCHAR(10),		''''SectionID'''') SectionID
, CONVERT(NVARCHAR(1000),	''''Section'''') Section
, CONVERT(NVARCHAR(4000),	''''Summary'''') Summary
, CONVERT(NVARCHAR(15),		''''Severity'''') Severity
, CONVERT(NVARCHAR(4000),	''''Details'''') Details
, CONVERT(NVARCHAR(35),		''''HoursToResolveWithTesting'''') HoursToResolveWithTesting
, CONVERT(NVARCHAR(4000),	''''QueryPlan'''') QueryPlan
, 0 Sorter
UNION ALL
SELECT 
ID,evaldate,domain,SQLInstance,SectionID,Section,Summary,Severity,Details,HoursToResolveWithTesting,QueryPlan, Sorter
FROM 
(
SELECT TOP 100 PERCENT CONVERT(NVARCHAR(25),T1.ID) ID
, REPLACE(T1.evaldate,''''~'''',''''-'''') evaldate
, REPLACE(domain,''''~'''',''''-'''') domain
, REPLACE(SQLInstance ,''''~'''',''''-'''') SQLInstance
, REPLACE(CONVERT(NVARCHAR(10), SectionID),''''~'''',''''-'''') SectionID
, REPLACE(Section,''''~'''',''''-'''') Section
, REPLACE(Summary,''''~'''',''''-'''') Summary
, REPLACE(Severity,''''~'''',''''-'''') Severity
, replace(replace(replace(replace( ISNULL(REPLACE(Details,''''~'''',''''-''''),''''''''), CHAR(9), '''' ''''),CHAR(10),'''' ''''), CHAR(13), '''' ''''), '''' '''','''' '''') [Details]
, REPLACE(CONVERT(NVARCHAR(10),HoursToResolveWithTesting),''''~'''',''''-'''') HoursToResolveWithTesting
, REPLACE(QueryPlan,''''~'''',''''-'''')QueryPlan
,T1.ID Sorter
 FROM [master].[dbo].[sqldba_sp_triage®_output]
T1
INNER JOIN (
SELECT MAX(evaldate) evaldate FROM [master].[dbo].[sqldba_sp_triage®_output]
) T2
ON T1.evaldate = T2.evaldate
) T3
) T4
ORDER BY Sorter ASC
;SET NOCOUNT OFF;''
SET @EmailBody = @Es;
BEGIN
EXEC msdb.dbo.sp_send_dbmail
@profile_name = @EmailProfile,
@body_format = ''TEXT'',
@recipients = @M_R,
@subject = @Es,
@body = @EmailBody,
@query_attachment_filename =@A_F,
@attach_query_result_as_file = 1,
@query_result_header = 0,
@execute_query_database = ''master'', 
@query_result_width = 32767,
@append_query_error = 1,
@query_result_no_padding = 1,
@query_result_separator  = @qrs,
@query = @S_Ex EXECUTE AS LOGIN = N''sa'';
END'

IF NOT EXISTS(SELECT job_id,* FROM msdb.dbo.sysjobsteps WHERE [step_name] =  N'Mail Results Out' AND job_id=@jobId) 
BEGIN
EXEC  msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_name=N'Mail Results Out', 
		@step_id=4, 
		@cmdexec_success_code=0, 
		@on_success_action=1, 
		@on_success_step_id=0, 
		@on_fail_action=2, 
		@on_fail_step_id=0, 
		@retry_attempts=0, 
		@retry_interval=0, 
		@os_run_priority=0, @subsystem=N'TSQL', 
		@command=@stepcmd, 
		@database_name=N'master', 
		@flags=0

		PRINT 'Create Step 4'
END
ELSE
BEGIN
	EXEC msdb.dbo.sp_update_jobstep @job_id=@jobId, @step_id=4 , @command=@stepcmd
	PRINT 'Update Step 4'
END
EXEC @ReturnCode = msdb.dbo.sp_update_job @job_id = @jobId, @start_step_id = 1

--/*If there is a step 4, just delete it thanks*/
--BEGIN TRY
--	EXEC msdb.dbo.sp_delete_jobstep @job_id=@jobId, @step_id=4
--END TRY
--BEGIN CATCH
--	PRINT 'No step 4'
--END CATCH

SET @the_job_name =  @the_job_name
IF EXISTS(SELECT job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name)
BEGIN
SELECT @the_job_Id = job_id  FROM msdb.dbo.sysjobs WHERE [name] =  @the_job_name 
  EXEC msdb.dbo.sp_attach_schedule @job_id=@the_job_Id, @schedule_id = @schedule_id_99
END


EXEC dbo.sp_start_job @job_name  = N'999. Send Weekly Stats', @step_name = N'Mail Results Out';

