# Hệ thống quản lý thư viện C#

Solution Visual Studio gồm ASP.NET Core Web API, ASP.NET Core MVC/Razor, Windows Forms, Entity Framework Core và SQL Server LocalDB. Dự án chạy bằng .NET và LocalDB; hiện không có cấu hình Docker Compose.

## Mở và chạy

Mở `QuanLyThuVien.sln` bằng Visual Studio hoặc làm theo [hướng dẫn chạy](HuongDanCaiDat.md). API dùng `http://localhost:5001`, Web dùng `http://localhost:5002`.

## Cấu trúc thư mục

- `Source/Library.Domain`: entities và chính sách nghiệp vụ.
- `Source/Library.Infrastructure`: EF Core và SQL Server.
- `Source/Library.Api`: API và nghiệp vụ mượn-trả.
- `Source/Library.Web`: giao diện Web MVC.
- `Source/Library.Admin.WinForms`: giao diện desktop Windows.
- `Database`: schema, ràng buộc, indexes, seed data và truy vấn kiểm tra.
- `Tests/Library.Tests`: kiểm tra chính sách nghiệp vụ.
- `docs/reports`: báo cáo đồ án và bản lưu nguồn để tạo lại báo cáo.
- `docs/diagrams`: sơ đồ trong báo cáo và hình tham khảo đã trích xuất.
- `tools/reporting`: script tạo báo cáo Word.
- `logs`: log chạy ứng dụng, log lịch sử và chẩn đoán build.
- `.dotnet-home`: cache/config .NET cục bộ, được Git bỏ qua.

## Trạng thái
Xem `Documents/ImplementationStatus.md` (đã có / chưa có) và `Documents/KiemThu.md` (mục nào đã chạy thật). Tóm tắt: Domain build và 20 kiểm tra đơn vị đạt; handler xác thực đã chạy thử; **chưa** build toàn solution, chưa chạy với SQL Server và Docker trong môi trường soạn bản này, nên cần chạy checklist trước khi nộp.

## Docker
`docker compose up -d --build` chạy 2 container (SQL Server + Library.Api); Web và WinForms chạy trên Windows. Chi tiết: `Documents/Docker.md`.
