IF OBJECT_ID('dbo.Appointments', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Appointments;
END
GO

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
GO

CREATE INDEX IX_Appointments_CustomerId ON dbo.Appointments(CustomerId);
GO

CREATE INDEX IX_Appointments_TimeWindow ON dbo.Appointments(StartsAt, EndsAt);
GO