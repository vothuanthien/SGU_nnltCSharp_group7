SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_Book_ISBN' AND object_id=OBJECT_ID(N'dbo.BookTitles')) CREATE UNIQUE INDEX UX_Book_ISBN ON BookTitles(ISBN) WHERE ISBN IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Loan_ReaderDate' AND object_id=OBJECT_ID(N'dbo.Loans')) CREATE INDEX IX_Loan_ReaderDate ON Loans(ReaderId,LoanDate DESC);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Reservation_ReaderStatus' AND object_id=OBJECT_ID(N'dbo.Reservations')) CREATE INDEX IX_Reservation_ReaderStatus ON Reservations(ReaderId,Status);
