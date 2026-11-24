# Hướng dẫn chạy đồ án Quản lý thư viện

## 1. Điều kiện cần

- Windows 10/11, .NET SDK 8.0 và SQL Server LocalDB (instance `MSSQLLocalDB`).
- Cơ sở dữ liệu `LibraryManagement` đã được tạo và có dữ liệu mẫu.
- Nếu database đã tồn tại, chạy các migration `Database/08_ReaderPortal_Update.sql` đến `Database/11_ReaderPortal_Enhancements.sql` để bật cổng bạn đọc, yêu cầu mượn, phí bồi thường, gia hạn, thông báo và nhật ký.

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

Nếu cơ sở dữ liệu `LibraryManagement` chưa tồn tại, mở SQL Server Management Studio, kết nối tới `(localdb)\MSSQLLocalDB`, rồi chạy lần lượt các script `Database/01_CreateDatabase.sql` đến `Database/04_SeedData.sql`. Sau đó chạy `Database/07_ProfessionalSkills_Update.sql`, các migration `Database/08_ReaderPortal_Update.sql` đến `Database/11_ReaderPortal_Enhancements.sql` để bật tài khoản bạn đọc, đặt trước, yêu cầu mượn, phí bồi thường, gia hạn và thông báo; cuối cùng chạy `Database/06_CreateDemoAdmin.sql`. Script `05_TestQueries.sql` chỉ dùng để xem các truy vấn kiểm tra. Các script cập nhật có thể chạy lại an toàn; nếu phát hiện yêu cầu đang hoạt động bị trùng, script dừng để dữ liệu được rà soát.

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

## 6. Chạy ứng dụng quản trị Windows Forms

Mở terminal tại thư mục gốc và chạy script tiện ích:

```powershell
.\ChayAdmin.ps1
```

Script tự khởi động LocalDB, kiểm tra API/SQL Server; nếu API chưa chạy thì tự bật API ở chế độ nền rồi mở WinForms. API nền tiếp tục chạy sau khi đóng WinForms để Web có thể dùng chung. Những lần chạy sau chỉ cần chạy lại script. Nếu Windows chặn script, dùng `powershell -ExecutionPolicy Bypass -File .\ChayAdmin.ps1` trong terminal tại thư mục gốc.

Tài khoản demo local: `admin`; mật khẩu: `Admin@12345`.

## 7. Dừng chương trình và xử lý lỗi

- Dừng API/Web bằng `Ctrl+C` trong terminal tương ứng; đóng cửa sổ Windows Forms để thoát.
- API do `ChayAdmin.ps1` mở chạy nền; muốn dừng thì tìm tiến trình API trong Task Manager hoặc khởi động lại máy. Nếu API đã chạy, script dùng lại tiến trình đó.
- Nếu API không phản hồi, kiểm tra `http://localhost:5001/health` và terminal API.
- Nếu build Web báo `MSB3021` và file `Library.Web.exe` đang được tiến trình khác sử dụng, Web đã được mở trước đó: mở `http://localhost:5002` để dùng phiên đang chạy, hoặc nhấn `Ctrl+C` ở terminal Web cũ rồi chạy lại lệnh `dotnet run`.
- Nếu LocalDB không kết nối, chạy `SqlLocalDB info MSSQLLocalDB`, rồi thử `SqlLocalDB start MSSQLLocalDB`.
- Nếu cổng đang được dùng, dừng phiên cũ bằng `Ctrl+C` rồi chạy lại.
- Nếu đăng nhập không được, kiểm tra API còn chạy và tài khoản demo đã được tạo bằng script `06_CreateDemoAdmin.sql`.
- Nếu thiếu .NET SDK, cài .NET 8 SDK và kiểm tra bằng `dotnet --list-sdks`.

## Chạy bằng Docker (khuyến nghị cho demo)

Xem `Documents/Docker.md`. Tóm tắt: `copy .env.example .env`, sửa mật khẩu, `docker compose up -d --build`, kiểm tra `http://localhost:5001/health/db`, rồi chạy Web và WinForms trên Windows (đăng nhập `admin` với `ADMIN_PASSWORD`).

## Cập nhật về xác thực và địa chỉ API

- `POST /api/loans` và `POST /api/returns` yêu cầu đăng nhập bằng header `Authorization: Bearer <token>`; tên thủ thư lấy từ phiên, không nhận `staffUsername` từ client.
- Cơ chế xác thực là bearer token phiên, không phải JWT. Chạy local không Docker dùng script `06_CreateDemoAdmin.sql` để tạo tài khoản demo `admin`; chỉ dùng trong môi trường học tập trên localhost.
- Bạn đọc đăng ký trên Web bằng tên đăng nhập tự chọn, họ tên, email/điện thoại và mật khẩu (tối thiểu 8 ký tự). Dùng tên đăng nhập và mật khẩu để vào cổng bạn đọc; số thẻ tự cấp dùng khi mượn/trả.
- Bạn đọc có thể gửi yêu cầu mượn từ trang chi tiết sách đang còn bản sao. Thủ thư duyệt trong WinForms để hệ thống gán cuốn và lập phiếu mượn.
- Nếu chạy LocalDB, áp dụng các script `08_ReaderPortal_Update.sql` đến `11_ReaderPortal_Enhancements.sql` sau `07_ProfessionalSkills_Update.sql`; Docker tự áp dụng migration khi API khởi động.
- Web đọc `LibraryApi:BaseUrl` (mặc định `http://localhost:5001/`); WinForms đọc biến môi trường `LIBRARY_API_URL` (cùng mặc định).
