# Hướng dẫn chạy đồ án Quản lý thư viện

## 1. Điều kiện cần
- Windows 10/11, .NET SDK 8.0 và SQL Server LocalDB (instance `MSSQLLocalDB`).
- Cơ sở dữ liệu `LibraryManagement` đã được tạo và có dữ liệu mẫu.

Kiểm tra SDK:
```powershell
dotnet --list-sdks
```

## 2. Mở đồ án
Mở Terminal trong VS Code và chuyển đến thư mục dự án:
```powershell
cd D:\SGU_nnltCSharp_group7
```

## 3. Khởi động LocalDB
```powershell
SqlLocalDB start MSSQLLocalDB
```
Nếu instance đã chạy, tiếp tục.

## 4. Chạy API
Mở Terminal riêng và chạy:
```powershell
cd D:\SGU_nnltCSharp_group7
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet run --project .\Source\Library.Api\Library.Api.csproj --launch-profile http
```
Giữ Terminal mở. API chạy tại `http://localhost:5001`; có thể kiểm tra tại `http://localhost:5001/health`.

## 5. Chạy giao diện Web
Mở Terminal riêng khác và chạy:
```powershell
cd D:\SGU_nnltCSharp_group7
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet run --project .\Source\Library.Web\Library.Web.csproj --launch-profile http
```
Mở `http://localhost:5002` trên trình duyệt. API ở bước trước cần tiếp tục chạy.

## 6. Chạy ứng dụng quản trị Windows Forms
Mở Terminal riêng khác và chạy:
```powershell
cd D:\SGU_nnltCSharp_group7
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet run --project .\Source\Library.Admin.WinForms\Library.Admin.WinForms.csproj
```
Đăng nhập bằng tài khoản quản trị đã có trong cơ sở dữ liệu. API cần tiếp tục chạy.

## 7. Dừng chương trình và xử lý lỗi
- Dừng API/Web bằng `Ctrl+C` trong Terminal tương ứng; đóng cửa sổ Windows Forms để thoát.
- Nếu API không phản hồi, kiểm tra `http://localhost:5001/health` và Terminal API.
- Nếu LocalDB không kết nối, kiểm tra `SqlLocalDB info MSSQLLocalDB`, rồi thử khởi động lại instance.
- Nếu cổng đang được dùng, dừng phiên cũ bằng `Ctrl+C` rồi chạy lại.
- Nếu thiếu .NET SDK, cài .NET 8 SDK và kiểm tra bằng `dotnet --list-sdks`.


## Chạy bằng Docker (khuyến nghị cho demo)
Xem `Documents/Docker.md`. Tóm tắt: `copy .env.example .env`, sửa mật khẩu, `docker compose up -d --build`, kiểm tra `http://localhost:5001/health/db`, rồi chạy Web và WinForms trên Windows (đăng nhập `admin` với `ADMIN_PASSWORD`).

## Cập nhật so với phần "Luồng hiện có" ở trên
- `POST /api/loans` và `POST /api/returns` **yêu cầu đăng nhập** (header `Authorization: Bearer <token>`) và **không còn nhận `staffUsername`**: tên thủ thư lấy từ phiên.
- Cơ chế xác thực là bearer token phiên (không phải JWT). Chạy local không Docker vẫn dùng script `06_CreateDemoAdmin.sql` (tài khoản demo `admin`, chỉ dùng localhost).
- Địa chỉ API: Web đọc `LibraryApi:BaseUrl` (mặc định `http://localhost:5001/`); WinForms đọc biến môi trường `LIBRARY_API_URL` (cùng mặc định).
