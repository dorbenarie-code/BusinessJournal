-- Creates dbo.Appointments if it does not exist. Safe to run again: existing rows are never
-- dropped or changed.
--
-- When the table already exists, its structure is verified first: exact columns, primary key,
-- foreign key to dbo.Customers, CHECK and DEFAULT constraints, and the key columns of both
-- indexes. Any mismatch stops the script with error 50001 and nothing is changed. A missing
-- index with the expected name is the only thing added to an existing table.
--
-- One batch inside one transaction (XACT_ABORT ON): any error rolls back everything.
-- Requires dbo.Customers (001_create_customers.sql).

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Appointments
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CustomerId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        StartsAt DATETIME2 NOT NULL,
        EndsAt DATETIME2 NOT NULL,
        Notes NVARCHAR(1000) NULL,
        IsCancelled BIT NOT NULL CONSTRAINT DF_Appointments_IsCancelled DEFAULT 0,

        CONSTRAINT FK_Appointments_Customers
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),

        CONSTRAINT CK_Appointments_TimeRange
            CHECK (EndsAt > StartsAt)
    );

    PRINT N'Created dbo.Appointments.';
END;

DECLARE @table INT = OBJECT_ID(N'dbo.Appointments', N'U');
DECLARE @problems NVARCHAR(1000) = N'';

-- Columns: exactly these, with these types and nullability (max_length is in bytes).
IF EXISTS (
        SELECT name, type_name, max_length, scale, is_nullable
        FROM (VALUES
            (N'Id',          N'uniqueidentifier', 16,   0, 0),
            (N'CustomerId',  N'uniqueidentifier', 16,   0, 0),
            (N'Title',       N'nvarchar',         400,  0, 0),
            (N'StartsAt',    N'datetime2',        8,    7, 0),
            (N'EndsAt',      N'datetime2',        8,    7, 0),
            (N'Notes',       N'nvarchar',         2000, 0, 1),
            (N'IsCancelled', N'bit',              1,    0, 0)
        ) AS expected (name, type_name, max_length, scale, is_nullable)
        EXCEPT
        SELECT c.name COLLATE DATABASE_DEFAULT, t.name COLLATE DATABASE_DEFAULT,
               c.max_length, c.scale, c.is_nullable
        FROM sys.columns AS c
        JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = @table)
    OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = @table) <> 7
    SET @problems += N' columns;';

-- Primary key on Id only.
IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints AS pk
        JOIN sys.index_columns AS ic
            ON ic.object_id = pk.parent_object_id AND ic.index_id = pk.unique_index_id
        WHERE pk.parent_object_id = @table
          AND pk.type = 'PK'
          AND COL_NAME(ic.object_id, ic.column_id) = N'Id'
          AND (SELECT COUNT(*) FROM sys.index_columns AS all_ic
               WHERE all_ic.object_id = pk.parent_object_id
                 AND all_ic.index_id = pk.unique_index_id) = 1)
    SET @problems += N' primary key;';

-- Enabled foreign key CustomerId -> dbo.Customers(Id).
IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys AS fk
        JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
        WHERE fk.parent_object_id = @table
          AND fk.referenced_object_id = OBJECT_ID(N'dbo.Customers', N'U')
          AND fk.is_disabled = 0
          AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'CustomerId'
          AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) = N'Id')
    SET @problems += N' foreign key;';

-- Enabled CHECK (EndsAt > StartsAt), as normalized by SQL Server.
IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = @table
          AND is_disabled = 0
          AND definition = N'([EndsAt]>[StartsAt])')
    SET @problems += N' check constraint;';

-- DEFAULT 0 on IsCancelled.
IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints
        WHERE parent_object_id = @table
          AND parent_column_id = COLUMNPROPERTY(@table, N'IsCancelled', 'ColumnId')
          AND definition = N'((0))')
    SET @problems += N' default constraint;';

-- An index that already uses one of the expected names must have the expected key columns.
WITH expected_keys AS (
    SELECT index_name, key_ordinal, column_name
    FROM (VALUES
        (N'IX_Appointments_CustomerId', 1, N'CustomerId'),
        (N'IX_Appointments_TimeWindow', 1, N'StartsAt'),
        (N'IX_Appointments_TimeWindow', 2, N'EndsAt')
    ) AS v (index_name, key_ordinal, column_name)
),
actual_keys AS (
    SELECT i.name COLLATE DATABASE_DEFAULT AS index_name,
           CAST(ic.key_ordinal AS INT) AS key_ordinal,
           c.name COLLATE DATABASE_DEFAULT AS column_name
    FROM sys.indexes AS i
    JOIN sys.index_columns AS ic
        ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
    JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = @table
      AND i.name IN (N'IX_Appointments_CustomerId', N'IX_Appointments_TimeWindow')
)
SELECT @problems += N' index key columns;'
WHERE EXISTS (SELECT * FROM actual_keys EXCEPT SELECT * FROM expected_keys)
   OR EXISTS (SELECT * FROM expected_keys
              WHERE index_name IN (SELECT index_name FROM actual_keys)
              EXCEPT SELECT * FROM actual_keys);

IF @problems <> N''
BEGIN
    DECLARE @message NVARCHAR(2048) =
        N'dbo.Appointments exists but does not match the expected schema (' + LTRIM(@problems)
        + N'). No changes were made.';
    THROW 50001, @message, 1;
END;

IF INDEXPROPERTY(@table, N'IX_Appointments_CustomerId', 'IndexID') IS NULL
BEGIN
    CREATE INDEX IX_Appointments_CustomerId ON dbo.Appointments(CustomerId);
    PRINT N'Created index IX_Appointments_CustomerId.';
END;

IF INDEXPROPERTY(@table, N'IX_Appointments_TimeWindow', 'IndexID') IS NULL
BEGIN
    CREATE INDEX IX_Appointments_TimeWindow ON dbo.Appointments(StartsAt, EndsAt);
    PRINT N'Created index IX_Appointments_TimeWindow.';
END;

COMMIT TRANSACTION;
GO