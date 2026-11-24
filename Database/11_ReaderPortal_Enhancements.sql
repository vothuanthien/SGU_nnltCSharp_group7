SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO

IF COL_LENGTH(N'dbo.LoanDetails',N'RenewalCount') IS NULL
    ALTER TABLE dbo.LoanDetails ADD RenewalCount int NOT NULL CONSTRAINT DF_LoanDetails_RenewalCount DEFAULT(0) WITH VALUES;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_LoanDetails_RenewalCount' AND parent_object_id=OBJECT_ID(N'dbo.LoanDetails'))
    ALTER TABLE dbo.LoanDetails ADD CONSTRAINT CK_LoanDetails_RenewalCount CHECK(RenewalCount BETWEEN 0 AND 1);
GO

IF OBJECT_ID(N'dbo.ReaderNotifications',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReaderNotifications(
        NotificationId int IDENTITY PRIMARY KEY,
        ReaderId int NOT NULL REFERENCES dbo.Readers(ReaderId),
        [Type] varchar(30) NOT NULL,
        Message nvarchar(500) NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ReaderNotifications_CreatedAt DEFAULT SYSDATETIME(),
        ReadAt datetime2 NULL
    );
    CREATE INDEX IX_ReaderNotifications_Reader_Created ON dbo.ReaderNotifications(ReaderId,CreatedAt DESC);
END;
GO
