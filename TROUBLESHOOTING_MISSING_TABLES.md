# Troubleshooting: Missing SQLWATCH Tables

## Error
```
Invalid object name 'dbo.sqlwatch_logger_perf_os_process_memory'
Invalid object name 'dbo.sqlwatch_logger_perf_os_performance_counters'
```

## Cause
The SQLWATCH database has not been deployed to the SQL Server instance you're trying to monitor, or the tables are missing.

## Solution

### Option 1: Deploy via Application (Recommended)

1. **Navigate to Database Deploy Page**
   - Click "Monitoring new server" in the navigation menu
   - Or press `Ctrl+D` (if configured)

2. **Enter Connection Details**
   - Server Name: Your SQL Server instance
   - Authentication: Windows or SQL Server
   - Database Name: SQLWATCH (default)

3. **Deploy**
   - Click "Deploy" button
   - Wait for deployment to complete
   - The application will create all necessary tables and objects

### Option 2: Manual Deployment

1. **Locate SQL Scripts**
   - Navigate to application directory
   - Find `SQLWATCH_db` folder

2. **Run Scripts in Order**
   ```sql
   -- Step 1: Create database and tables
   :r 01_CreateSQLWATCHDB.sql
   
   -- Step 2: Create post-deployment objects
   :r 02_PostSQLWATCHDBcreate.sql
   ```

3. **Verify Deployment**
   ```sql
   USE SQLWATCH;
   GO
   
   -- Check if tables exist
   SELECT name 
   FROM sys.tables 
   WHERE name LIKE 'sqlwatch_logger%'
   ORDER BY name;
   ```

### Option 3: Use Servers Page

1. **Go to Servers Page** (Ctrl+8)
2. **Add Server** if not already added
3. **Click "Deploy SQLWATCH"** button next to the server
4. **Wait for deployment** to complete

## Verification

After deployment, verify the tables exist:

```sql
USE SQLWATCH;
GO

-- Should return rows
SELECT COUNT(*) as TableCount
FROM sys.tables 
WHERE name LIKE 'sqlwatch_%';

-- Should return > 50 tables
```

## Required Tables for Instance Overview

The Instance Overview dashboard requires these key tables:
- `dbo.sqlwatch_logger_perf_os_process_memory`
- `dbo.sqlwatch_logger_perf_os_performance_counters`
- `dbo.sqlwatch_logger_perf_os_schedulers`
- `dbo.sqlwatch_logger_perf_file_stats`
- `dbo.sqlwatch_logger_perf_os_wait_stats`

## Permissions Required

The deployment account needs:
- `CREATE DATABASE` permission (for new database)
- `db_owner` role on SQLWATCH database
- `VIEW SERVER STATE` permission

## Common Issues

### Issue: "Database already exists but tables are missing"
**Solution**: Drop and recreate the database, or run the table creation scripts manually.

### Issue: "Permission denied"
**Solution**: Ensure the account has sufficient permissions. Use a sysadmin account for initial deployment.

### Issue: "Deployment fails partway through"
**Solution**: Check SQL Server error log for details. May need to manually clean up and retry.

## Data Collection

After deployment, data collection happens automatically:
- **Real-time queries**: Executed on-demand when viewing dashboards
- **Historical data**: Collected by SQL Agent jobs (if configured)
- **Default interval**: Every 1 minute

## Next Steps

1. Deploy SQLWATCH database
2. Refresh the Instance Overview dashboard
3. Wait 1-2 minutes for initial data collection
4. Verify data is appearing in charts

## Support

If deployment fails, check:
1. SQL Server version (2016+ required)
2. Network connectivity
3. Firewall settings
4. SQL Server authentication mode
5. Application logs in `logs/` folder
