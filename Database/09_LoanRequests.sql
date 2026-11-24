SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO

IF OBJECT_ID(N'dbo.LoanRequests',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoanRequests
    (
        LoanRequestId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoanRequests PRIMARY KEY,
        ReaderId int NOT NULL CONSTRAINT FK_LoanRequests_Readers REFERENCES dbo.Readers(ReaderId),
        BookId int NOT NULL CONSTRAINT FK_LoanRequests_BookTitles REFERENCES dbo.BookTitles(BookId),
        Status varchar(20) NOT NULL CONSTRAINT DF_LoanRequests_Status DEFAULT 'Requested',
        RequestedAt datetime2(0) NOT NULL CONSTRAINT DF_LoanRequests_RequestedAt DEFAULT SYSDATETIME(),
        ProcessedAt datetime2(0) NULL,
        ProcessedBy nvarchar(80) NULL,
        CopyId int NULL CONSTRAINT FK_LoanRequests_BookCopies REFERENCES dbo.BookCopies(CopyId),
        LoanId int NULL CONSTRAINT FK_LoanRequests_Loans REFERENCES dbo.Loans(LoanId),
        RejectionReason nvarchar(300) NULL
    );
END;
GO

IF EXISTS
(
    SELECT 1 FROM dbo.LoanRequests WHERE Status='Requested'
    GROUP BY ReaderId,BookId HAVING COUNT(*)>1
)
    THROW 51003, 'Duplicate pending loan requests exist. Review them before creating UX_LoanRequests_Pending_Reader_Book.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_LoanRequests_Pending_Reader_Book' AND object_id=OBJECT_ID(N'dbo.LoanRequests'))
    CREATE UNIQUE INDEX UX_LoanRequests_Pending_Reader_Book ON dbo.LoanRequests(ReaderId,BookId)
        WHERE Status='Requested';
GO
