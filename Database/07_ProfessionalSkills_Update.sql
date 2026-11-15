SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO

/* Additive changes for account profile data; existing account data is preserved. */
IF COL_LENGTH(N'dbo.Accounts', N'FullName') IS NULL
    ALTER TABLE dbo.Accounts ADD FullName nvarchar(200) NULL;
GO

IF COL_LENGTH(N'dbo.Accounts', N'CreatedAt') IS NULL
    ALTER TABLE dbo.Accounts
        ADD CreatedAt datetime2(0) NOT NULL
            CONSTRAINT DF_Accounts_CreatedAt DEFAULT SYSDATETIME() WITH VALUES;
GO

/* A filtered unique index prevents concurrent requests from creating duplicate pending holds. */
IF EXISTS
(
    SELECT 1
    FROM dbo.Reservations
    WHERE Status = 'Pending'
    GROUP BY ReaderId, BookId
    HAVING COUNT(*) > 1
)
    THROW 51001, 'Duplicate pending reservations exist. Review them before creating UX_Reservations_Pending_Reader_Book.', 1;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Reservations_Pending_Reader_Book'
      AND object_id = OBJECT_ID(N'dbo.Reservations')
)
    CREATE UNIQUE INDEX UX_Reservations_Pending_Reader_Book
        ON dbo.Reservations(ReaderId, BookId)
        WHERE Status = 'Pending';
GO
