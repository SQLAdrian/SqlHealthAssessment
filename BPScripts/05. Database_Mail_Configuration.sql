/*
Author: Tim Ford
Original link: http://sqlmag.com/database-administration/how-set-sql-server-database-mail-one-easy-script
*/
--==========================================================
-- Enable Database Mail
--==========================================================
USE master;
GO

sp_CONFIGURE 'show advanced', 1
GO
RECONFIGURE
GO
sp_CONFIGURE 'Database Mail XPs', 1
GO
RECONFIGURE
GO 

--================================================================
-- DATABASE MAIL CONFIGURATION
--================================================================

DECLARE @thisserver NVARCHAR(50);
SET @thisserver = REPLACE(@@SERVERNAME,'\','_');

DECLARE @serverdisplayname NVARCHAR(50);
SET @serverdisplayname = @thisserver + ' SQL Notifications';

DECLARE @Domain NVARCHAR(50)
SET @Domain = DEFAULT_DOMAIN()

DECLARE @MailFrom NVARCHAR(50);
SET @MailFrom = @thisserver + '@NZAA.co.nz' 

DECLARE @ProfileName NVARCHAR(50)
SET @ProfileName = 'DBA Mail Profile'

DECLARE @AccountName NVARCHAR(50)
SET @AccountName = 'DBA_Email_Account'

DECLARE @Testrecipients NVARCHAR(100)
SET @Testrecipients = 'adrian.sullivan@lexel.co.nz'

--==========================================================
-- Create a Database Mail account
--==========================================================
EXECUTE msdb.dbo.sysmail_add_account_sp
	@account_name = @AccountName,
	@description = 'Mail account used for system alerts',
	@email_address = @MailFrom,
	@replyto_address = @MailFrom,
	@display_name = @serverdisplayname,
	@mailserver_name = 'mail.aadomain.co.nz',
	@port = 25;



--==========================================================
-- Create a Database Mail Profile
--==========================================================
DECLARE @profile_id INT, @profile_description sysname;
SELECT @profile_id = COALESCE(MAX(profile_id),1) FROM msdb.dbo.sysmail_profile
SELECT @profile_description = 'Database Mail Profile for ' + @@servername 


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
    @is_default = 1 ;





--EXEC master.dbo.xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'SOFTWARE\Microsoft\MSSQLServer\SQLServerAgent', N'DatabaseMailProfile', N'REG_SZ', N''
--EXEC master.dbo.xp_instance_regwrite N'HKEY_LOCAL_MACHINE', N'SOFTWARE\Microsoft\MSSQLServer\SQLServerAgent', N'UseDatabaseMail', N'REG_DWORD', 1
--GO

EXEC msdb.dbo.sp_set_sqlagent_properties @email_save_in_sent_folder = 0

--==========================================================
-- Test Database Mail
--==========================================================
DECLARE @sub VARCHAR(100)
DECLARE @body_text NVARCHAR(MAX)
SELECT @sub = 'Test from New SQL install on ' + @@servername
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