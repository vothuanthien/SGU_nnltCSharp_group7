# Hệ thống quản lý thư viện C#

Solution Visual Studio cho đề tài quản lý thư viện. Có ASP.NET Core Web API, ASP.NET Core MVC/Razor, Windows Forms, Entity Framework Core và SQL Server dùng chung.

## Mở và chạy
Chạy profile `http` của API (`http://localhost:5001`), rồi Web (`http://localhost:5002`) và Windows Forms. Hướng dẫn endpoint và thử nghiệp vụ nằm trong `HuongDanCaiDat.md`.

## Cấu trúc
- `Source/Library.Domain`: entities và chính sách nghiệp vụ.
- `Source/Library.Infrastructure`: EF Core/SQL Server.
- `Source/Library.Api`: API, nghiệp vụ mượn-trả và truy cập dữ liệu.
- `Source/Library.Web`: giao diện bạn đọc responsive.
- `Source/Library.Admin.WinForms`: ứng dụng desktop Windows.
- `Database`: schema, indexes, seed data, truy vấn kiểm tra.
- `Tests/Library.Tests`: smoke checks của chính sách nghiệp vụ.
- `Documents`: sơ đồ kiến trúc và báo cáo cập nhật có ma trận FR/UC/US/NFR/BR.

## Trạng thái
Build .NET 8 solution đạt; unit checks quy tắc miền đạt. LocalDB đã được khởi tạo; health, tra cứu Web/Desktop, một lượt mượn/trả và rollback đã chạy thử. Chưa hoàn tất đăng nhập/phân quyền và các nghiệp vụ được liệt kê trong hướng dẫn. API demo chỉ dùng trên localhost.
