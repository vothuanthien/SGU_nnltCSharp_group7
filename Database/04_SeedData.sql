SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO
-- Normalize demo text as UTF-8 so rerunning after a code-page issue repairs the sample rows.
UPDATE Genres SET Name=N'Lập trình' WHERE GenreId=1 AND NOT EXISTS(SELECT 1 FROM Genres WHERE Name=N'Lập trình');
UPDATE Genres SET Name=N'Kỹ năng' WHERE GenreId=2 AND NOT EXISTS(SELECT 1 FROM Genres WHERE Name=N'Kỹ năng');
UPDATE Genres SET Name=N'Văn học' WHERE GenreId=3 AND NOT EXISTS(SELECT 1 FROM Genres WHERE Name=N'Văn học');
UPDATE Publishers SET Name=N'Nhà xuất bản Giáo dục' WHERE PublisherId=1 AND NOT EXISTS(SELECT 1 FROM Publishers WHERE Name=N'Nhà xuất bản Giáo dục');
UPDATE Authors SET FullName=N'Nguyễn Văn A' WHERE AuthorId=1 AND NOT EXISTS(SELECT 1 FROM Authors WHERE FullName=N'Nguyễn Văn A');
UPDATE Authors SET FullName=N'Trần Thị B' WHERE AuthorId=2 AND NOT EXISTS(SELECT 1 FROM Authors WHERE FullName=N'Trần Thị B');
UPDATE BookTitles SET Title=N'Lập trình C# căn bản',Description=N'Tài liệu nhập môn C#.' WHERE ISBN=N'9780000000001';
UPDATE BookTitles SET Title=N'Tư duy lập trình',Description=N'Rèn luyện tư duy giải quyết vấn đề.' WHERE ISBN=N'9780000000002';
UPDATE Readers SET FullName=N'Nguyễn Minh An' WHERE CardNumber=N'THE0001';
IF NOT EXISTS(SELECT 1 FROM Genres WHERE Name=N'Lập trình') INSERT Genres(Name) VALUES(N'Lập trình'),(N'Kỹ năng'),(N'Văn học');
IF NOT EXISTS(SELECT 1 FROM Publishers WHERE Name=N'Nhà xuất bản Giáo dục') INSERT Publishers(Name) VALUES(N'Nhà xuất bản Giáo dục');
IF NOT EXISTS(SELECT 1 FROM Authors WHERE FullName=N'Nguyễn Văn A') INSERT Authors(FullName) VALUES(N'Nguyễn Văn A'),(N'Trần Thị B');
IF NOT EXISTS(SELECT 1 FROM BookTitles WHERE ISBN=N'9780000000001') INSERT BookTitles(Title,ISBN,PublishedYear,Description,GenreId,PublisherId) VALUES(N'Lập trình C# căn bản',N'9780000000001',2024,N'Tài liệu nhập môn C#.',1,1),(N'Tư duy lập trình',N'9780000000002',2023,N'Rèn luyện tư duy giải quyết vấn đề.',1,1);
IF NOT EXISTS(SELECT 1 FROM BookAuthors WHERE BookId=1 AND AuthorId=1) INSERT BookAuthors VALUES(1,1),(2,2);
IF NOT EXISTS(SELECT 1 FROM BookCopies WHERE CopyCode=N'CSHARP-001') INSERT BookCopies(CopyCode,BookId,ShelfLocation) VALUES(N'CSHARP-001',1,N'A-01'),(N'CSHARP-002',1,N'A-01'),(N'TD-001',2,N'B-02');
IF NOT EXISTS(SELECT 1 FROM Readers WHERE CardNumber=N'THE0001') INSERT Readers(ReaderCode,CardNumber,FullName,Email,Phone,RegisteredAt,ExpiryDate) VALUES(N'BD0001',N'THE0001',N'Nguyễn Minh An',N'an@example.test',N'0900000000',CAST(SYSDATETIME() AS date),DATEADD(year,1,CAST(SYSDATETIME() AS date)));
IF NOT EXISTS(SELECT 1 FROM Settings WHERE [Key]=N'LoanDays') INSERT Settings VALUES(N'LoanDays',N'14'),(N'FinePerDay',N'1000'),(N'MaxLoans',N'5');
-- Tài khoản seed chưa phát hành mật khẩu mặc định; tạo tài khoản an toàn theo HuongDanCaiDat.md.
