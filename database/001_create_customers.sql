IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        FullName NVARCHAR(200) NOT NULL,
        PhoneNumber NVARCHAR(50) NOT NULL,
        Email NVARCHAR(320) NULL
    );
END;
GO