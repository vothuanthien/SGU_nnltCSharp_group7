# Hướng dẫn cài đặt và chạy thử đồ án quản lý thư viện

Tài liệu này hướng dẫn chạy bản hiện có trên Windows. Nên làm theo thứ tự: cài công cụ → tạo cơ sở dữ liệu → cấu hình chuỗi kết nối → chạy API → chạy Web/Desktop → thử các nghiệp vụ. Không chạy lại các script tạo bảng trên cùng database.

## 1. Bản hiện tại có những gì

- `Library.Api`: API tra cứu sách, lập phiếu mượn và nhận trả.
- `Library.Web`: trang tra cứu sách gọi API.
- `Library.Admin.WinForms`: ứng dụng Windows Forms, hiện có màn hình tra cứu sách.
- `Library.Domain`, `Library.Infrastructure`: entity, chính sách mượn và EF Core/SQL Server.
- `Database`: script khởi tạo schema và dữ liệu demo.
- Đăng nhập, phân quyền, CRUD quản trị, lịch sử cá nhân, đặt trước, thanh toán phạt, báo cáo và sao lưu chưa hoàn tất. API mượn/trả hiện chưa yêu cầu đăng nhập; chỉ dùng trên máy cá nhân để demo, không mở ra mạng hoặc dùng dữ liệu thật. Các profile HTTP chỉ bind localhost trong cấu hình demo; cấu hình triển khai phải bật HTTPS.

## 2. Cài phần mềm cần thiết

1. Cài **Visual Studio 2022**.
2. Trong Visual Studio Installer, chọn workload:
   - **ASP.NET and web development**.
   - **.NET desktop development** (để build Windows Forms).
3. Cài **.NET 8 SDK** nếu máy chưa có.
4. Cài một lựa chọn SQL Server:
   - **SQL Server Express/Developer**; hoặc
   - **SQL Server LocalDB** (thường đi kèm Visual Studio).
5. Cài **SQL Server Management Studio (SSMS)** để chạy các file `.sql`.
6. Lần đầu mở solution, cần Internet để tải các gói NuGet. Trong Visual Studio chọn **Restore NuGet Packages** nếu tự động khôi phục chưa chạy.

## 3. Mở solution và khôi phục gói

1. Mở `D:\NNLTC\Do_An_CK\QuanLyThuVien.sln` bằng Visual Studio.
2. Chờ quá trình tải project hoàn tất.
3. Nếu báo thiếu package: bấm phải solution **QuanLyThuVien** → **Restore NuGet Packages**.
4. Build thử: menu **Build → Build Solution** hoặc `Ctrl+Shift+B`. Cấu hình đã xác minh dùng .NET 8; lần build cuối thành công trên máy tạo gói.

Nếu mở bằng terminal tại thư mục `D:\NNLTC\Do_An_CK`, chạy:

```powershell
dotnet restore .\QuanLyThuVien.sln
dotnet build .\QuanLyThuVien.sln
```

## 4. Tạo database và dữ liệu mẫu

### 4.1. Kết nối SSMS

- Nếu dùng LocalDB, thử tên server: `(localdb)\MSSQLLocalDB`, Authentication: **Windows Authentication**.
- Nếu dùng SQL Server Express, thường là `localhost\SQLEXPRESS` hoặc `.\SQLEXPRESS`.
- Nếu dùng SQL Server Developer mặc định, thường là `localhost`.

Tên server tùy cách bạn cài SQL Server. Nếu không kết nối được, mở SQL Server Configuration Manager/Visual Studio Installer để kiểm tra LocalDB hoặc instance đã cài.

### 4.2. Chạy các script đúng thứ tự

Trong SSMS, mở từng file trong thư mục `D:\NNLTC\Do_An_CK\Database`, chọn đúng database theo ghi chú bên dưới, rồi bấm **Execute (F5)**:

1. `01_CreateDatabase.sql` — tạo database `LibraryManagement`.
2. `02_CreateTables.sql` — tạo bảng, khóa và chỉ mục cơ bản.
3. `03_Constraints_Indexes.sql` — thêm unique/index cho tra cứu.
4. `04_SeedData.sql` — thêm sách, bản sao, bạn đọc và cấu hình demo.
5. `05_TestQueries.sql` — chạy các truy vấn xem dữ liệu mẫu và danh sách quá hạn.

Các script từ 02 trở đi có `USE LibraryManagement`. Bắt đầu trên database mới. Script `01` tạo database nếu chưa có; scripts `02`–`04` có kiểm tra tồn tại để có thể chạy lại sau khi sửa lỗi. Nếu đang có dữ liệu thật, không chạy seed mẫu tùy tiện. `04_SeedData.sql` không tạo tài khoản/mật khẩu đăng nhập. Các file SQL cần giữ nguyên encoding UTF-8 để tên tiếng Việt không bị lỗi.

Dữ liệu mẫu để thử:

- Thẻ bạn đọc: `THE0001` (đang hoạt động, còn hạn theo ngày tạo database).
- Mã cuốn sách: `CSHARP-001`, `CSHARP-002`, `TD-001`.
- Cấu hình: thời hạn mượn 14 ngày, tối đa 5 cuốn, phạt 1.000 đồng/ngày. Đây là giá trị demo, sửa trong bảng `Settings` nếu cần.

## 5. Cấu hình kết nối API đến SQL Server

API lấy chuỗi kết nối từ cấu hình `ConnectionStrings:Library`. Cách khuyên dùng là **User Secrets** để không ghi thông tin nhạy cảm vào source:

1. Trong Visual Studio, bấm phải project `Library.Api` → **Manage User Secrets**.
2. Thêm cấu hình JSON. Với LocalDB:

```json
{
  "ConnectionStrings": {
    "Library": "Server=(localdb)\\MSSQLLocalDB;Database=LibraryManagement;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Với SQL Express, thay `Server` bằng instance máy bạn, ví dụ `Server=.\\SQLEXPRESS;Database=LibraryManagement;Trusted_Connection=True;TrustServerCertificate=True`.

Nếu SSMS của bạn dùng SQL Login, có thể dùng `User Id=...;Password=...;` trong User Secrets. Không gửi mật khẩu vào báo cáo, ảnh chụp hoặc Git. Không sửa file `appsettings.json` để lưu mật khẩu thật.

## 6. Chạy API, Web và Desktop

### 6.1. Chạy API trước

1. Trong Solution Explorer, bấm phải `Library.Api` → **Set as Startup Project**.
2. Chọn profile `http` trong danh sách chạy.
3. Bấm **F5** hoặc **Ctrl+F5**.
4. Cửa sổ trình duyệt/API mở địa chỉ health. Nếu không tự mở, vào `http://localhost:5001/health`.
5. Kết quả đúng khi thấy JSON tương tự:

```json
{"status":"ok","service":"Library.Api"}
```

Health chỉ xác nhận API khởi động; nó **không** xác nhận SQL đã kết nối. Kiểm tra database bằng cách gọi endpoint danh mục ở phần 7.

### 6.2. Chạy trang Web

1. Để API tiếp tục chạy.
2. Bấm phải `Library.Web` → **Set as Startup Project**.
3. Chọn profile `http`, bấm **F5**. Web dùng cổng `5002`; mở `http://localhost:5002`.
4. Trang chủ có ô tìm sách. Tìm `C#`, `Tư duy`, hoặc `9780000000001`.
5. Kết quả cần hiện tên sách, thể loại, ISBN/năm và số bản sao đang Available.

Nếu API chạy ở cổng khác, sửa `LibraryApi:BaseUrl` trong `Source/Library.Web/appsettings.json` và danh sách origin trong `Source/Library.Api/Program.cs` cho khớp. Profile HTTP chỉ để demo trên localhost; khi triển khai thật phải bật HTTPS và cấu hình chứng chỉ hợp lệ.

### 6.3. Chạy Windows Forms

1. Giữ API đang chạy.
2. Bấm phải `Library.Admin.WinForms` → **Set as Startup Project**.
3. Chọn cấu hình `Debug`, bấm **F5**.
4. Màn hình `Danh mục sách` mở. Nhập tên/ISBN vào ô tìm kiếm và bấm **Tra cứu**.

Nếu Windows Forms báo không kết nối API, kiểm tra API đang chạy tại `http://localhost:5001`. Màn hình desktop hiện chỉ có tra cứu; các form quản lý, mượn/trả chưa được nối vào giao diện.

### 6.4. Chạy API và Web cùng lúc (tùy chọn)

Bấm phải solution → **Configure Startup Projects** → chọn **Multiple startup projects** → đặt `Library.Api` và `Library.Web` thành **Start** (API ở trên Web) → OK → đặt cả hai profile là `http` → F5. Chạy desktop riêng sau khi API đã chạy.

## 7. Thử tìm kiếm sách

Có thể dùng trình duyệt hoặc lệnh sau trong PowerShell. Các lệnh dùng HTTP loopback cho môi trường demo localhost:

```powershell
curl.exe "http://localhost:5001/api/catalog/books?q=C%23&page=1&pageSize=12"
```

Thử thêm:

```powershell
curl.exe "http://localhost:5001/api/catalog/books?q=9780000000001"
curl.exe "http://localhost:5001/api/catalog/books?q=Nguyễn Văn A"
```

API trả danh sách JSON. Nếu Web hiện cảnh báo chưa kết nối API, trước hết gọi `http://localhost:5001/health`, sau đó gọi endpoint danh mục để kiểm tra SQL và seed data.

## 8. Thử lập phiếu mượn

Dùng một cuốn còn Available, ví dụ `CSHARP-001`. Mở PowerShell và gửi:

```powershell
curl.exe -X POST "http://localhost:5001/api/loans" `
  -H "Content-Type: application/json" `
  -d '{"cardNumber":"THE0001","staffUsername":"demo-thuthu","copyCodes":["CSHARP-001"]}'
```

Thành công sẽ trả mã phiếu (`loanCode`) và `loanId`. Mở SSMS, chạy:

```sql
USE LibraryManagement;
SELECT * FROM Loans ORDER BY LoanId DESC;
SELECT * FROM LoanDetails ORDER BY LoanDetailId DESC;
SELECT CopyCode, Status FROM BookCopies WHERE CopyCode = N'CSHARP-001';
```

Trạng thái mã cuốn vừa mượn phải là `Borrowed`; hạn trả được lưu ở `LoanDetails.DueDate`.

Thử các lỗi nghiệp vụ:

- Dùng thẻ không tồn tại/hết hạn → API từ chối.
- Mượn lại `CSHARP-001` khi đang Borrowed → API từ chối vì cuốn không sẵn sàng.
- Gửi `"copyCodes":[]` → API từ chối vì phiếu phải có ít nhất một cuốn.
- Gửi một mã hợp lệ cùng một mã không tồn tại → API trả lỗi; kiểm tra database để xác nhận giao dịch rollback, cuốn hợp lệ không bị kẹt trạng thái Borrowed và không có phiếu dở dang.

## 9. Thử nhận trả sách và tiền phạt

Nhận trả mã cuốn đang Borrowed:

```powershell
curl.exe -X POST "http://localhost:5001/api/returns" `
  -H "Content-Type: application/json" `
  -d '{"copyCode":"CSHARP-001","condition":"Normal","staffUsername":"demo-thuthu"}'
```

Kết quả có `overdueDays`, `fineAmount` và trạng thái mới. Trả bình thường chuyển bản sao về `Available`; `Damaged` hoặc `Lost` giữ sách ở trạng thái tương ứng. Gọi trả lần thứ hai cho cùng mã khi không còn lượt mượn mở sẽ bị từ chối.

Có thể thử nhánh sách hỏng: mượn `TD-001`, sau đó nhận trả với `"condition":"Damaged"`; kiểm tra `BookCopies.Status` không trở thành Available.

Tiền phạt được tính theo `Settings.FinePerDay`. Luồng thanh toán khoản phạt chưa có endpoint/giao diện.

## 10. Chạy kiểm tra logic và build

Từ thư mục `D:\NNLTC\Do_An_CK`:

```powershell
dotnet build .\QuanLyThuVien.sln
dotnet run --project .\Tests\Library.Tests
```

Bộ kiểm tra tự động hiện có xác minh các quy tắc miền: giới hạn số cuốn, phiếu rỗng, ngày quá hạn và phép tính phạt. Ngoài ra, bản demo đã được kiểm tra thủ công với LocalDB cho tra cứu, một lượt mượn/trả và rollback khi có mã cuốn sai; chưa kiểm thử đồng thời, bảo mật hoặc toàn bộ nghiệp vụ. Build thành công cũng không có nghĩa database đã được khởi tạo.

## 11. Lỗi thường gặp

| Hiện tượng | Cách kiểm tra/khắc phục |
|---|---|
| `Cannot open database` hoặc login failed | Kiểm tra SQL Server instance đang chạy; thử kết nối cùng server trong SSMS; xác nhận đã chạy script 01–04; kiểm tra User Secrets của đúng project API. |
| API mở nhưng `/health` không được | Xác nhận profile `http` và cổng `5001`; xem Output của Visual Studio để đọc lỗi khởi động. |
| `/health` được nhưng `/api/catalog/books` lỗi | API đã chạy nhưng SQL có thể chưa kết nối, chưa có bảng hoặc connection string sai. Xem Output của API và thử `SELECT` trong SSMS. |
| Web báo chưa kết nối API | Để API chạy; kiểm tra BaseUrl là `http://localhost:5001/`; kiểm tra profile `http` và CORS khớp launch settings. |
| Không mở được localhost | Kiểm tra API/Web đang chạy với profile `http`, đúng cổng 5001/5002 và firewall không chặn loopback. |
| Build báo thiếu package | Kết nối Internet, bấm phải solution → Restore NuGet Packages, sau đó build lại. |
| Trùng dữ liệu/bảng khi chạy script lần nữa | Script tạo database chỉ tạo mới khi database chưa có; bảng/index và seed mẫu có kiểm tra tồn tại. Tuy vậy, khi đang có dữ liệu thật, không chạy lại seed tùy tiện. |
| Desktop báo lỗi SSL/API | Xác nhận API đang chạy theo profile `http` tại cổng 5001. |

## 12. Giới hạn trước khi demo/nghiệm thu

Bản hiện tại dùng dữ liệu seed hư cấu và chưa tạo tài khoản đăng nhập. API mượn/trả chưa xác thực nhân viên hoặc phân quyền, vì vậy chỉ chạy trên localhost với dữ liệu demo. Chưa triển khai CRUD quản trị, đăng nhập, hồ sơ/lịch sử bạn đọc, đặt trước, thanh toán phạt, báo cáo, nhật ký nghiệp vụ và sao lưu/khôi phục. LocalDB đã được khởi tạo và chạy thử trên máy tạo gói. Khi chuyển sang máy khác, vẫn cần cài/kết nối SQL Server, chạy script và kiểm tra lại; kết quả này không thay thế kiểm thử concurrency, bảo mật hoặc backup/restore.

