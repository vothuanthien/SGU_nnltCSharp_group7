# Hệ thống quản lý thư viện C#

Solution Visual Studio gồm ASP.NET Core Web API, ASP.NET Core MVC/Razor, Windows Forms, Entity Framework Core và SQL Server LocalDB. Dự án có thể chạy bằng LocalDB hoặc Docker Compose.

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
Xem `Documents/ImplementationStatus.md` (đã có / chưa có) và `Documents/KiemThu.md` (mục nào đã chạy thật). Cập nhật 24/11/2026: toàn solution build thành công (0 cảnh báo, 0 lỗi) và migrations 10–11 đã áp dụng vào LocalDB. 20 kiểm tra Domain/PBKDF2/phiên và kiểm tra handler xác thực đã đạt ở lượt trước; các luồng nghiệp vụ Web–API–SQL–WinForms và Docker vẫn cần chạy end-to-end theo checklist trước khi nộp.

## Docker
`docker compose up -d --build` chạy 2 container (SQL Server + Library.Api); Web và WinForms chạy trên Windows. Chi tiết: `Documents/Docker.md`.

## Chức năng chính
- Bạn đọc đăng ký tài khoản Web, gửi yêu cầu mượn/đặt trước, gia hạn một lần trước hạn, đổi mật khẩu, xem thông báo và lịch sử.
- Thủ thư quản lý danh mục, duyệt yêu cầu mượn (tạo phiếu và gán bản sao), xử lý đặt trước (giữ bản sao 3 ngày), lập phiếu mượn/trả, ghi nhận thu phạt và bồi thường. Admin xem nhật ký hoạt động.
- Dashboard xuất danh mục sách, lịch sử mượn theo thời gian và báo cáo bạn đọc quá hạn.
