# Phần bổ sung: đăng nhập, phân quyền và CRUD đầu sách



## 1. Chức năng trong phần bổ sung

### Đăng nhập và phân quyền

- `POST /api/auth/login`: kiểm tra username/password từ bảng `Accounts`.
- Mật khẩu được băm/kiểm tra bằng PBKDF2-SHA256; không lưu mật khẩu thuần.
- API phát JWT có username và role.
- API CRUD đầu sách, mượn và trả chỉ cho phép role `Admin` hoặc `Librarian`.
- Tên thủ thư ghi vào `Loans` và `Returns` lấy từ claim trong JWT, không lấy từ dữ liệu client gửi lên.

### CRUD đầu sách trên Desktop

- Xem/tìm kiếm đầu sách.
- Thêm đầu sách.
- Cập nhật tên, ISBN, năm xuất bản, mô tả, mã thể loại và danh sách mã tác giả.
- Ngừng hiển thị đầu sách bằng xóa mềm (`IsActive = 0`), giữ dữ liệu và lịch sử mượn.

Lưu ý: form này mới quản lý **đầu sách** (`BookTitles`), chưa quản lý các bản sao vật lý trong `BookCopies`. Mã thể loại và mã tác giả nhập theo ID đã có trong database.

## 2. Chạy lần đầu

1. Chạy các script SQL 01–05 như hướng dẫn chính.
2. Chạy thêm `Database/06_CreateDemoAdmin.sql` trên database `LibraryManagement`.
3. Mở `QuanLyThuVien.sln`, restore NuGet Packages và Build Solution.
4. Chạy `Library.Api` bằng profile `http`.
5. Chạy `Library.Admin.WinForms`.
6. Đăng nhập demo:
   - Username: `admin`
   - Password: `Admin@12345`
   - Role: `Admin`

Tài khoản/mật khẩu này là thông tin demo công khai trong source, chỉ dùng localhost. Không dùng cho dữ liệu thật hoặc triển khai công khai.

## 3. API mẫu

Đăng nhập:

```http
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{"username":"admin","password":"Admin@12345"}
```

Danh sách đầu sách (thay `TOKEN` bằng `accessToken` nhận được):

```http
GET http://localhost:5001/api/admin/books
Authorization: Bearer TOKEN
```

Thêm sách:

```http
POST http://localhost:5001/api/admin/books
Authorization: Bearer TOKEN
Content-Type: application/json

{
  "title": "Nhập môn cơ sở dữ liệu",
  "isbn": "9786040000000",
  "publishedYear": 2025,
  "description": "Tài liệu học tập mẫu.",
  "genreId": 1,
  "authorIds": [1]
}
```

## 4. Checklist kiểm tra sau khi build

- [ ] Chạy script 06 tạo được tài khoản `admin`.
- [ ] Đăng nhập đúng mật khẩu nhận HTTP 200 và có `accessToken`.
- [ ] Đăng nhập sai mật khẩu nhận HTTP 401.
- [ ] Gọi `GET /api/admin/books` không có token nhận HTTP 401.
- [ ] Gọi `GET /api/admin/books` có token Admin nhận HTTP 200.
- [ ] Desktop bắt buộc hiển thị form đăng nhập trước MainForm.
- [ ] Thêm đầu sách từ Desktop và thấy dữ liệu trong SQL Server.
- [ ] Cập nhật đầu sách từ Desktop và kiểm tra thay đổi trong SQL Server.
- [ ] Ngừng hiển thị sách và xác nhận `BookTitles.IsActive = 0`, dữ liệu không bị xóa.
- [ ] Gọi API mượn/trả không có token nhận HTTP 401.
- [ ] Gọi API mượn/trả có token Admin/Librarian vẫn giữ đúng nghiệp vụ hiện có.

## 5. Giới hạn hiện tại

- Chưa có giao diện đổi mật khẩu/tạo tài khoản nhân viên.
- Chưa có màn hình quản lý tác giả/thể loại/nhà xuất bản; khi thêm sách, nhập ID đã có trong SQL Server.
- Chưa có CRUD bản sao, bạn đọc, đặt trước, lịch sử cá nhân, thanh toán phạt, dashboard, báo cáo hoặc sao lưu.
- Khóa JWT trong `appsettings.Development.json` là khóa demo. Khi triển khai phải đặt khóa riêng qua User Secrets/biến môi trường, bật HTTPS và không commit bí mật lên Git.
- Phần bổ sung chưa được build/chạy xác nhận trong môi trường tạo gói do không có .NET SDK tại đây. Cần chạy checklist ở trên trong Visual Studio 2022 hoặc trên máy đã cài .NET 8 SDK.