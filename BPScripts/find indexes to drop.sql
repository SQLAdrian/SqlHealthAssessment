DECLARE @dynamicSQL NVARCHAR(4000);
DECLARE @Databasei_Max TINYINT;
DECLARE @Databasei_Count TINYINT;
DECLARE @DatabaseName NVARCHAR(500)

DECLARE @LexelIndexs TABLE
	(
		DBName NVARCHAR(200)
		, TableName NVARCHAR(200)
		, IndexName NVARCHAR(500)
		, CreateStatement NVARCHAR(4000)
		, DropStatement NVARCHAR(4000)
		, RebuildStatement NVARCHAR(4000)
	)
DECLARE @Databases TABLE
	(
		id INT IDENTITY(1,1)
		, databasename VARCHAR(250)
		, [compatibility_level] BIGINT
		, user_access BIGINT
		, user_access_desc VARCHAR(50)
		, [state] BIGINT
		, state_desc  VARCHAR(50)
		, recovery_model BIGINT
		, recovery_model_desc  VARCHAR(50)
		, create_date DATETIME
	);
	SET @dynamicSQL = 'SELECT 
	db.name
	, db.compatibility_level
	, db.user_access
	, db.user_access_desc
	, db.state
	, db.state_desc
	, db.recovery_model
	, db.recovery_model_desc
	, db.create_date
	FROM sys.databases db ';
	IF 'Yes please dont do the system databases' IS NOT NULL
	BEGIN
		SET @dynamicSQL = @dynamicSQL + ' WHERE database_id > 4 AND state NOT IN (1,2,3,6) AND user_access = 0 ';
	END
	SET @dynamicSQL = @dynamicSQL + ' OPTION (RECOMPILE)'
	INSERT INTO @Databases 

EXEC sp_executesql @dynamicSQL ;
SET @Databasei_Max = (SELECT MAX(id) FROM @Databases );

SET @Databasei_Count = 1; 
WHILE @Databasei_Count <= @Databasei_Max 
BEGIN 
/*Thanks Mr R. https://stackoverflow.com/users/1831734/mr-r*/
		SELECT @DatabaseName = d.databasename FROM @Databases d WHERE id = @Databasei_Count AND d.state NOT IN (2,6) OPTION (RECOMPILE)
		SET @dynamicSQL = 'USE [' + @DatabaseName + '];


		select ''' +  @DatabaseName + ''',t.name, i.name,
''
IF EXISTS (SELECT 1 FROM sys.indexes AS si JOIN sys.objects AS so on si.object_id=so.object_id JOIN sys.schemas AS sc on so.schema_id=sc.schema_id 
WHERE sc.name=''''''+ sc.name+'''''' AND so.name =''''''+ t.name+'''''' AND si.name=''''''+ i.name+'''''' /* Index */)
BEGIN CREATE '' + 
CASE WHEN is_primary_key=1 THEN ''CLUSTERED'' 
WHEN is_primary_key=0 and is_unique_constraint=0 THEN ''NONCLUSTERED''
WHEN is_primary_key=0 and is_unique_constraint=1 THEN ''UNIQUE'' END  
+ '' INDEX '' +
QUOTENAME(i.name)COLLATE DATABASE_DEFAULT + '' ON '' +
QUOTENAME(t.name)COLLATE DATABASE_DEFAULT + '' ( ''  + 
STUFF(REPLACE(REPLACE((
        SELECT QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN '' DESC'' ELSE '''' END AS [data()]
        FROM sys.index_columns AS ic
        INNER JOIN sys.columns AS c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH
    ), ''<row>'', '', ''), ''</row>'', ''''), 1, 2, '''') + '' ) ''  -- keycols
+ ISNULL(COALESCE('' INCLUDE ( '' +
    STUFF(REPLACE(REPLACE((
        SELECT QUOTENAME(c.name) AS [data()]
        FROM sys.index_columns AS ic
        INNER JOIN sys.columns AS c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
        ORDER BY ic.index_column_id
        FOR XML PATH
    ), ''<row>'', '', ''), ''</row>'', ''''), 1, 2, '''') + '' ) '',    -- included cols
    ''''),'''') 	+ CASE 
	WHEN filter_definition IS NULL THEN '''' 
	ELSE ''WHERE '' + filter_definition
	END 
	+ '' WITH(DATA_COMPRESSION=PAGE,ONLINE=ON)
END'' as [Create],
''DROP INDEX '' + QUOTENAME(i.name) + '' ON '' + QUOTENAME('''+ @DatabaseName + ''') + ''.'' + QUOTENAME(sc.name) + ''.'' + QUOTENAME(t.name) as [Drop],
''ALTER INDEX '' + QUOTENAME(i.name)  + '' ON '' + QUOTENAME('''+ @DatabaseName + ''') + ''.'' + QUOTENAME(sc.name) + QUOTENAME(t.name) + '' REBUILD WITH(DATA_COMPRESSION=PAGE,ONLINE=ON)'' as [Rebuild]
FROM sys.tables AS t
INNER JOIN sys.indexes AS i ON t.object_id = i.object_id
JOIN sys.objects AS so on i.object_id=so.object_id
JOIN sys.schemas AS sc on so.schema_id=sc.schema_id
LEFT JOIN sys.dm_db_index_usage_stats AS u ON i.object_id = u.object_id AND i.index_id = u.index_id
WHERE t.is_ms_shipped = 0
AND i.name like ''%sqldba%''
AND i.type <> 0
order by QUOTENAME(t.name), is_primary_key desc

		'
		PRINT @dynamicSQL
		INSERT @LexelIndexs
		EXEC sp_executesql @dynamicSQL;		
--		filter_definition
--([created]>'2019-06-01')
			
	SET @Databasei_Count = @Databasei_Count + 1

END
SELECT * 
FROM @LexelIndexs
