USE LibraryManagement;
GO
IF OBJECT_ID(N'dbo.AdminSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminSessions
    (
        TokenHash varbinary(32) NOT NULL CONSTRAINT PK_AdminSessions PRIMARY KEY,
        Username nvarchar(80) NOT NULL,
        Role varchar(20) NOT NULL,
        ExpiresAt datetimeoffset(7) NOT NULL,
        ReaderId int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_AdminSessions_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AdminSessions_ExpiresAt'
      AND object_id = OBJECT_ID(N'dbo.AdminSessions')
)
    CREATE INDEX IX_AdminSessions_ExpiresAt ON dbo.AdminSessions(ExpiresAt);
GO
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AdminSessions_Username'
      AND object_id = OBJECT_ID(N'dbo.AdminSessions')
)
    CREATE INDEX IX_AdminSessions_Username ON dbo.AdminSessions(Username);
GO
