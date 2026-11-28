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
IF NOT EXISTS(SELECT 1 FROM Genres WHERE Name=N'Lập trình') INSERT Genres(Name) VALUES(N'Lập trình'),(N'Kỹ năng'),(N'Văn học');
IF NOT EXISTS(SELECT 1 FROM Publishers WHERE Name=N'Nhà xuất bản Giáo dục') INSERT Publishers(Name) VALUES(N'Nhà xuất bản Giáo dục');
IF NOT EXISTS(SELECT 1 FROM Authors WHERE FullName=N'Nguyễn Văn A') INSERT Authors(FullName) VALUES(N'Nguyễn Văn A'),(N'Trần Thị B');
IF NOT EXISTS(SELECT 1 FROM BookTitles WHERE ISBN=N'9780000000001') INSERT BookTitles(Title,ISBN,PublishedYear,Description,GenreId,PublisherId) VALUES(N'Lập trình C# căn bản',N'9780000000001',2024,N'Tài liệu nhập môn C#.',1,1),(N'Tư duy lập trình',N'9780000000002',2023,N'Rèn luyện tư duy giải quyết vấn đề.',1,1);
IF NOT EXISTS(SELECT 1 FROM BookAuthors WHERE BookId=1 AND AuthorId=1) INSERT BookAuthors VALUES(1,1),(2,2);
IF NOT EXISTS(SELECT 1 FROM BookCopies WHERE CopyCode=N'CSHARP-001') INSERT BookCopies(CopyCode,BookId,ShelfLocation) VALUES(N'CSHARP-001',1,N'A-01'),(N'CSHARP-002',1,N'A-01'),(N'TD-001',2,N'B-02');
IF NOT EXISTS(SELECT 1 FROM Settings WHERE [Key]=N'LoanDays') INSERT Settings VALUES(N'LoanDays',N'14'),(N'FinePerDay',N'1000'),(N'MaxLoans',N'5');
-- Tài khoản seed chưa phát hành mật khẩu mặc định; tạo tài khoản an toàn theo HuongDanCaiDat.md.

-- Bộ sách mở rộng cho trang tra cứu; có thể chạy lại mà không tạo bản ghi trùng.
INSERT Genres(Name)
SELECT g.Name FROM (VALUES (N'Công nghệ'),(N'Kinh doanh'),(N'Khoa học'),(N'Tâm lý')) g(Name)
WHERE NOT EXISTS(SELECT 1 FROM Genres x WHERE x.Name=g.Name);

INSERT Publishers(Name)
SELECT p.Name FROM (VALUES (N'Nhà xuất bản Trẻ'),(N'Nhà xuất bản Tổng hợp TP.HCM'),(N'Nhà xuất bản Thế Giới'),(N'Nhà xuất bản Lao Động'),(N'Nhà xuất bản Kim Đồng'),(N'Nhà xuất bản Hội Nhà Văn')) p(Name)
WHERE NOT EXISTS(SELECT 1 FROM Publishers x WHERE x.Name=p.Name);

DECLARE @Catalog TABLE
(
    ISBN nvarchar(20) NOT NULL PRIMARY KEY,
    Title nvarchar(300) NOT NULL,
    PublishedYear int NOT NULL,
    Description nvarchar(1000) NOT NULL,
    GenreName nvarchar(100) NOT NULL,
    AuthorName nvarchar(200) NOT NULL,
    PublisherName nvarchar(200) NOT NULL,
    CopyPrefix nvarchar(30) NOT NULL,
    CopyCount int NOT NULL
);

INSERT @Catalog VALUES
(N'9780132350884',N'Clean Code: Mã sạch và nghệ thuật lập trình',2008,N'Những nguyên tắc giúp viết mã dễ đọc, dễ kiểm thử và dễ bảo trì.',N'Công nghệ',N'Robert C. Martin',N'Nhà xuất bản Thế Giới',N'CLEAN-CODE',3),
(N'9780135957059',N'The Pragmatic Programmer',2019,N'Kinh nghiệm thực hành để phát triển phần mềm hiệu quả và bền vững.',N'Công nghệ',N'David Thomas',N'Nhà xuất bản Thế Giới',N'PRAGMATIC',2),
(N'9781718502703',N'Python Crash Course',2019,N'Giáo trình thực hành Python qua bài tập và dự án nhỏ.',N'Công nghệ',N'Eric Matthes',N'Nhà xuất bản Thế Giới',N'PYTHON',3),
(N'DEMO-DB-0004',N'Cơ sở dữ liệu căn bản',2022,N'Giới thiệu mô hình dữ liệu quan hệ, SQL, khóa và chuẩn hóa.',N'Công nghệ',N'Nguyễn Kim Anh',N'Nhà xuất bản Giáo dục',N'DATABASE',2),
(N'DEMO-VH-0005',N'Dế Mèn phiêu lưu ký',1941,N'Tác phẩm văn học thiếu nhi kể về hành trình trưởng thành của chú Dế Mèn.',N'Văn học',N'Tô Hoài',N'Nhà xuất bản Kim Đồng',N'DEMEN',4),
(N'DEMO-VH-0006',N'Cho tôi xin một vé đi tuổi thơ',2008,N'Câu chuyện giàu hoài niệm về tuổi thơ và cách trẻ em nhìn thế giới.',N'Văn học',N'Nguyễn Nhật Ánh',N'Nhà xuất bản Trẻ',N'TUOITHO',3),
(N'DEMO-VH-0007',N'Mắt biếc',1990,N'Tiểu thuyết về tình bạn, tình yêu và những năm tháng tuổi trẻ.',N'Văn học',N'Nguyễn Nhật Ánh',N'Nhà xuất bản Trẻ',N'MATBIEC',2),
(N'DEMO-VH-0008',N'Nhà giả kim',1988,N'Tiểu thuyết hành trình về ước mơ, lựa chọn và khám phá bản thân.',N'Văn học',N'Paulo Coelho',N'Nhà xuất bản Thế Giới',N'ALCHEMIST',3),
(N'DEMO-KN-0009',N'Đắc nhân tâm',1936,N'Những gợi ý thực hành về giao tiếp và xây dựng quan hệ với mọi người.',N'Kỹ năng',N'Dale Carnegie',N'Nhà xuất bản Tổng hợp TP.HCM',N'DACNHANTAM',4),
(N'9780735211292',N'Atomic Habits: Thay đổi tí hon, hiệu quả bất ngờ',2018,N'Phương pháp xây dựng thói quen tốt bằng những thay đổi nhỏ mỗi ngày.',N'Tâm lý',N'James Clear',N'Nhà xuất bản Thế Giới',N'ATOMIC',3),
(N'9781455586691',N'Deep Work: Làm ra làm, chơi ra chơi',2016,N'Bàn về khả năng tập trung sâu và cách tạo không gian làm việc hiệu quả.',N'Kỹ năng',N'Cal Newport',N'Nhà xuất bản Lao Động',N'DEEPWORK',2),
(N'DEMO-TL-0012',N'Tư duy nhanh và chậm',2011,N'Khám phá hai hệ thống tư duy và những ảnh hưởng của chúng lên quyết định.',N'Tâm lý',N'Daniel Kahneman',N'Nhà xuất bản Thế Giới',N'TUDUY',2),
(N'DEMO-KD-0013',N'Người giàu có nhất thành Babylon',1926,N'Các câu chuyện ngắn về tiết kiệm, quản lý tiền và lập kế hoạch tài chính.',N'Kinh doanh',N'George S. Clason',N'Nhà xuất bản Lao Động',N'BABYLON',3),
(N'DEMO-KD-0014',N'Từ tốt đến vĩ đại',2001,N'Nghiên cứu các yếu tố giúp một tổ chức duy trì kết quả xuất sắc lâu dài.',N'Kinh doanh',N'Jim Collins',N'Nhà xuất bản Trẻ',N'GOODTOGREAT',2),
(N'DEMO-KH-0015',N'Lược sử thời gian',1988,N'Giới thiệu những câu hỏi lớn về vũ trụ, thời gian và các định luật vật lý.',N'Khoa học',N'Stephen Hawking',N'Nhà xuất bản Thế Giới',N'TIME',3),
(N'DEMO-KH-0016',N'Sapiens: Lược sử loài người',2011,N'Tổng quan lịch sử loài người từ thời tiền sử đến xã hội hiện đại.',N'Khoa học',N'Yuval Noah Harari',N'Nhà xuất bản Thế Giới',N'SAPIENS',3),
(N'DEMO-KN-0017',N'Tuổi trẻ đáng giá bao nhiêu?',2016,N'Những suy ngẫm và gợi ý để học tập, trải nghiệm và phát triển bản thân.',N'Kỹ năng',N'Rosie Nguyễn',N'Nhà xuất bản Hội Nhà Văn',N'TUOITRE',3),
(N'DEMO-VH-0018',N'Vừa nhắm mắt vừa mở cửa sổ',2004,N'Câu chuyện nhẹ nhàng về gia đình, tình bạn và những điều bình dị quanh ta.',N'Văn học',N'Nguyễn Ngọc Thuần',N'Nhà xuất bản Trẻ',N'CỬASỔ',2);

INSERT Authors(FullName)
SELECT DISTINCT c.AuthorName FROM @Catalog c
WHERE NOT EXISTS(SELECT 1 FROM Authors a WHERE a.FullName=c.AuthorName);

INSERT BookTitles(Title,ISBN,PublishedYear,Description,GenreId,PublisherId)
SELECT c.Title,c.ISBN,c.PublishedYear,c.Description,g.GenreId,p.PublisherId
FROM @Catalog c
JOIN Genres g ON g.Name=c.GenreName
JOIN Publishers p ON p.Name=c.PublisherName
WHERE NOT EXISTS(SELECT 1 FROM BookTitles b WHERE b.ISBN=c.ISBN);

INSERT BookAuthors(BookId,AuthorId)
SELECT b.BookId,a.AuthorId
FROM @Catalog c
JOIN BookTitles b ON b.ISBN=c.ISBN
JOIN Authors a ON a.FullName=c.AuthorName
WHERE NOT EXISTS(SELECT 1 FROM BookAuthors ba WHERE ba.BookId=b.BookId AND ba.AuthorId=a.AuthorId);

INSERT BookCopies(CopyCode,BookId,ShelfLocation,AcquiredAt,Status)
SELECT CONCAT(N'LIB-',c.CopyPrefix,N'-',RIGHT(CONCAT(N'0',n.CopyNumber),2)),b.BookId,CONCAT(N'Kệ ',c.GenreName),CAST(SYSDATETIME() AS date),'Available'
FROM @Catalog c
JOIN BookTitles b ON b.ISBN=c.ISBN
CROSS JOIN (VALUES (1),(2),(3),(4)) n(CopyNumber)
WHERE n.CopyNumber<=c.CopyCount
  AND NOT EXISTS(SELECT 1 FROM BookCopies bc WHERE bc.CopyCode=CONCAT(N'LIB-',c.CopyPrefix,N'-',RIGHT(CONCAT(N'0',n.CopyNumber),2)));
