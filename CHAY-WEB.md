# Chạy thử web bằng Docker Desktop

Mở Docker Desktop, chuyển sang Linux containers. Mở PowerShell trong thư mục repo:

Cần có dung lượng trống cho image và database (nên để ít nhất 10 GB trên ổ chứa dữ liệu Docker; script kiểm tra tối thiểu 5 GB trên ổ dự án). Nếu Docker báo `read-only file system` hoặc `input/output error`, giải phóng dung lượng rồi khởi động lại Docker Desktop trước khi thử lại.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\start-web.ps1
```

Lần đầu tải image và build có thể mất vài phút. Script tạo `.env` cục bộ nếu chưa có, bật SQL Server, API và Web, rồi chờ kết nối database và trang chủ sẵn sàng.

- Web: http://localhost:5002
- Kiểm tra database: http://localhost:5001/health/db
- Bạn đọc chọn **Đăng ký** trên Web sau khi được thủ thư tạo hồ sơ và cấp thẻ; cần dùng số thẻ cùng email đã lưu tại thư viện. Sau đó đăng nhập để gửi yêu cầu đặt trước khi sách hết và xem lịch sử mượn.
- Tài khoản quản trị API/WinForms được tạo lần đầu: `admin` / `Admin@12345` (nếu dùng `.env` do script tạo). Web hiện là giao diện bạn đọc, không có trang đăng nhập quản trị.

Các cổng chỉ được mở trên máy cục bộ. Mật khẩu SQL được tạo ngẫu nhiên trong `.env` và file này bị Git bỏ qua. Giữ `.env` để lần chạy sau tiếp tục dùng đúng mật khẩu của database.

Dừng ứng dụng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\stop-web.ps1
```

Dữ liệu được giữ trong Docker volume khi dừng. Đọc log:

```powershell
docker compose -f docker-compose.yml -f docker-compose.web.yml logs --tail 100
```

Build solution trên Windows bằng `dotnet build QuanLyThuVien.sln`. Chạy bộ kiểm thử console bằng `dotnet run --project Tests/Library.Tests`. Nếu máy chỉ có .NET 10 runtime, có thể chạy kiểm thử sau khi build bằng `$env:DOTNET_ROLL_FORWARD='Major'; dotnet Tests/Library.Tests/bin/Debug/net8.0/Library.Tests.dll`. Các container ứng dụng vẫn dùng đúng .NET 8.
