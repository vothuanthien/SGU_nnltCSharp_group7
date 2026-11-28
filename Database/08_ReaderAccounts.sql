USE LibraryManagement;
GO
IF OBJECT_ID(N'dbo.ReaderAccounts',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReaderAccounts(
        ReaderAccountId int IDENTITY PRIMARY KEY,
        ReaderId int NOT NULL UNIQUE REFERENCES dbo.Readers(ReaderId),
        Username nvarchar(80) NOT NULL UNIQUE,
        PasswordHash nvarchar(500) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_ReaderAccounts_IsActive DEFAULT 1,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ReaderAccounts_CreatedAt DEFAULT SYSDATETIME()
    );
END;
GO
