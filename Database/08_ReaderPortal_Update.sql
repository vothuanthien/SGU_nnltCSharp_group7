SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO

/* Public reader portal credentials. Existing staff-created reader records remain valid without a portal password. */
IF COL_LENGTH(N'dbo.Readers', N'PasswordHash') IS NULL
    ALTER TABLE dbo.Readers ADD PasswordHash nvarchar(500) NULL;
GO

IF COL_LENGTH(N'dbo.Readers', N'Username') IS NULL
    ALTER TABLE dbo.Readers ADD Username nvarchar(80) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Readers_Username' AND object_id=OBJECT_ID(N'dbo.Readers'))
    CREATE UNIQUE INDEX UX_Readers_Username ON dbo.Readers(Username) WHERE Username IS NOT NULL;
GO

/* A ready reservation owns one physical copy until it is checked out or cancelled. */
IF COL_LENGTH(N'dbo.Reservations', N'HeldCopyId') IS NULL
    ALTER TABLE dbo.Reservations ADD HeldCopyId int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Reservations_HeldCopy')
    ALTER TABLE dbo.Reservations ADD CONSTRAINT FK_Reservations_HeldCopy
        FOREIGN KEY (HeldCopyId) REFERENCES dbo.BookCopies(CopyId);
GO

IF EXISTS
(
    SELECT 1 FROM dbo.Reservations
    WHERE Status IN ('Pending','Ready')
    GROUP BY ReaderId,BookId HAVING COUNT(*)>1
)
    THROW 51002, 'Duplicate active reservations exist. Review them before rebuilding the active reservation index.', 1;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Reservations_Pending_Reader_Book' AND object_id=OBJECT_ID(N'dbo.Reservations'))
    DROP INDEX UX_Reservations_Pending_Reader_Book ON dbo.Reservations;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Reservations_Active_Reader_Book' AND object_id=OBJECT_ID(N'dbo.Reservations'))
    CREATE UNIQUE INDEX UX_Reservations_Active_Reader_Book ON dbo.Reservations(ReaderId,BookId)
        WHERE Status IN ('Pending','Ready');
GO

