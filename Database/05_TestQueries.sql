USE LibraryManagement;
GO
SELECT COUNT(*) AS SoDauSach FROM BookTitles; SELECT Status,COUNT(*) AS SoBanSao FROM BookCopies GROUP BY Status;
SELECT b.Title,c.CopyCode,l.LoanDate,d.DueDate FROM LoanDetails d JOIN Loans l ON l.LoanId=d.LoanId JOIN BookCopies c ON c.CopyId=d.CopyId JOIN BookTitles b ON b.BookId=c.BookId LEFT JOIN Returns r ON r.LoanDetailId=d.LoanDetailId WHERE r.ReturnId IS NULL AND d.DueDate<CAST(SYSDATETIME() AS date);
