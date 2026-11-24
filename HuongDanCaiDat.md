# Hướng dẫn chạy đồ án Quản lý thư viện

## 1. Điều kiện cần

- Windows 10/11, .NET SDK 8.0 và SQL Server LocalDB (instance `MSSQLLocalDB`).
- Cơ sở dữ liệu `LibraryManagement` đã được tạo và có dữ liệu mẫu.

Kiểm tra SDK:

```powershell
dotnet --list-sdks
```

## 2. Mở đồ án

Mở thư mục gốc của đồ án trong VS Code. Mở terminal tại thư mục này; các lệnh bên dưới dùng đường dẫn tương đối từ thư mục gốc.

## 3. Khởi động LocalDB và chuẩn bị dữ liệu

Trong PowerShell, chạy:

```powershell
SqlLocalDB start MSSQLLocalDB
```

Nếu cơ sở dữ liệu `LibraryManagement` chưa tồn tại, mở SQL Server Management Studio, kết nối tới `(localdb)\MSSQLLocalDB`, rồi chạy lần lượt các script `Database/01_CreateDatabase.sql` đến `Database/04_SeedData.sql`. Sau đó chạy `Database/07_ProfessionalSkills_Update.sql`, `Database/08_ReaderAccounts.sql`, `Database/10_AdminSessions.sql` và `Database/06_CreateDemoAdmin.sql`. Với database đã có từ trước, chạy `07`, `08` và `10` để cập nhật cấu trúc. Script `05_TestQueries.sql` chỉ dùng để xem các truy vấn kiểm tra. Script `07` có thể dừng nếu phát hiện yêu cầu đặt trước Pending trùng; cần rà soát dữ liệu trước khi tạo chỉ mục.

## 4. Chạy API

Mở terminal thứ nhất trong thư mục gốc. Các lệnh này chọn LocalDB rõ ràng, tránh connection string `HUNGTRAN` trong cấu hình Development:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5001"
$env:ConnectionStrings__Library = "Server=(localdb)\MSSQLLocalDB;Database=LibraryManagement;Trusted_Connection=True;TrustServerCertificate=True"
$env:Logging__EventLog__LogLevel__Default = "None"
dotnet run --project .\Source\Library.Api\Library.Api.csproj --no-launch-profile
```

Giữ terminal này mở trong suốt lúc demo. Kiểm tra tại `http://localhost:5001/health` và `http://localhost:5001/health/db`; trạng thái database cần là `connected`.

## 5. Chạy giao diện Web

Mở terminal thứ hai, vẫn ở thư mục gốc:

```powershell
dotnet run --project .\Source\Library.Web\Library.Web.csproj --launch-profile http
```

Mở `http://localhost:5002` trên trình duyệt. API ở terminal thứ nhất cần tiếp tục chạy.

Bạn đọc đăng ký tài khoản Web sau khi thủ thư đã tạo hồ sơ và cấp thẻ: chọn **Đăng ký**, nhập số thẻ, email đã lưu tại thư viện, rồi tự đặt tên đăng nhập và mật khẩu (ít nhất 10 ký tự). Sau đó đăng nhập để gửi yêu cầu đặt trước khi sách hết và xem lịch sử mượn. Khi đến quầy, thủ thư tra cứu thẻ và lập phiếu mượn trong màn hình **Mượn / Trả**.

## 6. Chạy ứng dụng quản trị Windows Forms

Mở terminal thứ ba, vẫn ở thư mục gốc:

```powershell
dotnet run --project .\Source\Library.Admin.WinForms\Library.Admin.WinForms.csproj
```

Tài khoản demo local: `admin`; mật khẩu: `Admin@12345`. API cần tiếp tục chạy để đăng nhập và dùng chức năng quản trị.

## 7. Dừng chương trình và xử lý lỗi

- Dừng API/Web bằng `Ctrl+C` trong terminal tương ứng; đóng cửa sổ Windows Forms để thoát.
- Nếu API không phản hồi, kiểm tra `http://localhost:5001/health` và terminal API.
- Nếu LocalDB không kết nối, chạy `SqlLocalDB info MSSQLLocalDB`, rồi thử `SqlLocalDB start MSSQLLocalDB`.
- Nếu cổng đang được dùng, dừng phiên cũ bằng `Ctrl+C` rồi chạy lại.
- Nếu đăng nhập không được, kiểm tra API còn chạy và tài khoản demo đã được tạo bằng script `06_CreateDemoAdmin.sql`.
- Nếu thiếu .NET SDK, cài .NET 8 SDK và kiểm tra bằng `dotnet --list-sdks`.

## Chạy bằng Docker (khuyến nghị cho demo)

Xem `Documents/Docker.md`. Tóm tắt: `copy .env.example .env`, sửa mật khẩu, `docker compose up -d --build`, kiểm tra `http://localhost:5001/health/db`, rồi chạy Web và WinForms trên Windows (đăng nhập `admin` với `ADMIN_PASSWORD`).

## Cập nhật về xác thực và địa chỉ API

- `POST /api/loans` và `POST /api/returns` yêu cầu đăng nhập bằng header `Authorization: Bearer <token>`; tên thủ thư lấy từ phiên, không nhận `staffUsername` từ client.
- Cơ chế xác thực là bearer token phiên, không phải JWT. Chạy local không Docker dùng script `06_CreateDemoAdmin.sql` để tạo tài khoản demo `admin`; chỉ dùng trong môi trường học tập trên localhost.
- Web đọc `LibraryApi:BaseUrl` (mặc định `http://localhost:5001/`); WinForms đọc biến môi trường `LIBRARY_API_URL` (cùng mặc định).
