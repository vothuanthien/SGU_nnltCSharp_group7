# Đối chiếu yêu cầu và trạng thái

Cập nhật sau đợt rà soát (xem `KiemThu.md` để biết mục nào đã chạy). **"Đã viết" không có nghĩa "đã kiểm thử tích hợp"**: ngoài mục A trong `KiemThu.md`, mọi thứ dưới đây chưa được chạy với SQL Server.

| Nhóm | Trạng thái |
|---|---|
| Xác thực | Nhân viên đăng nhập qua `POST /api/auth/login`; bạn đọc tự chọn username/mật khẩu và đăng ký Web qua `/api/readers/register`, đăng nhập qua `/api/readers/login`. Mật khẩu dùng PBKDF2-SHA256; username có unique index. Bearer token ngẫu nhiên lưu trong bộ nhớ API, hết hạn sau 8 giờ; khởi động lại API thì phải đăng nhập lại. |
| Phân quyền | `[Authorize(Roles="Admin,Librarian")]` cho CRUD, mượn/trả, đặt trước và thu phạt; quản lý tài khoản thủ thư chỉ dành cho Admin. Bạn đọc chỉ xem dữ liệu cá nhân và hủy yêu cầu của mình. |
| Mượn / trả | Tên thủ thư lấy từ phiên đăng nhập; dùng `LoanPolicy` cho giới hạn, hạn trả và phạt trễ. Khi nhận sách hỏng/mất, thủ thư nhập phí sửa chữa/thay thế theo quy định; tổng khoản phải thu gồm phạt trễ và bồi thường. Hoàn tất đặt trước tạo phiếu mượn. Chưa kiểm thử tích hợp. |
| CRUD | Sách, bản sao, bạn đọc, tác giả, thể loại, NXB (API + WinForms); gán tác giả cho sách. Xóa mềm với sách/bản sao/bạn đọc/thể loại. |
| Web bạn đọc | Danh mục, tìm kiếm, lọc/sắp xếp, phân trang, chi tiết, đăng ký tài khoản/thẻ, đăng nhập, gửi yêu cầu mượn, lịch sử mượn, gia hạn một lần trước hạn nếu không có người chờ, đổi mật khẩu, thông báo trong ứng dụng, danh sách yêu cầu/đặt trước và hủy yêu cầu đang chờ. Tra cứu lịch sử theo số thẻ yêu cầu phiên nhân viên; bạn đọc Web chỉ xem dữ liệu của tài khoản đã đăng nhập. Chưa tích hợp email/SMS. |
| Đặt trước | Bạn đọc gửi yêu cầu khi sách còn hoặc hết bản; thủ thư giữ một bản sao trong 3 ngày khi có thể, hủy yêu cầu hoặc hoàn tất thành phiếu mượn. Hệ thống tự giải phóng giữ chỗ hết hạn khi có truy vấn cổng bạn đọc/quản lý đặt trước; **chưa có thông báo email/SMS tự động**. |
| Yêu cầu mượn | Bạn đọc gửi yêu cầu khi sách còn bản sao; thủ thư duyệt/từ chối trong WinForms. Khi duyệt, API khóa và gán bản sao, kiểm tra thẻ/giới hạn rồi tạo phiếu mượn trong cùng transaction. |
| Thanh toán và báo cáo | API và WinForms ghi nhận thanh toán toàn bộ hoặc một phần khoản phải thu. Dashboard xuất danh sách sách, phiếu mượn theo khoảng ngày và báo cáo quá hạn. Admin có màn hình xem 500 nhật ký gần nhất; thao tác catalog, mượn/trả, gia hạn, đổi mật khẩu, duyệt yêu cầu và thanh toán được ghi log. |
| Docker | Đã viết `Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`, khởi tạo DB tự động. **Chưa chạy thử** (xem `Docker.md`). |
| Kiểm thử | Console test 20 kiểm tra đạt (Domain, băm mật khẩu, kho phiên). Chưa dùng xUnit; API, SQL Server, đặt trước, thanh toán và Web–WinForms chưa có test tích hợp. |
| Bảo mật còn thiếu | Chưa giới hạn số lần đăng nhập sai; chưa có quy trình quên mật khẩu qua email; session lưu trong RAM; chỉ HTTP trên localhost (không dùng triển khai thật). |

## Thay đổi trong đợt này
- Sửa lỗi bảo mật: `/api/loans` và `/api/returns` trước đây **không** yêu cầu đăng nhập và tin `staffUsername` do client gửi (tài liệu cũ ghi sai là đã bảo vệ).
- Sửa lỗi WinForms: lưới bind trực tiếp `Dictionary` nên hiển thị sai cột; chuyển sang `DataTable`.
- Bỏ CORS (Web gọi API từ phía máy chủ, WinForms là ứng dụng native nên không cần), bỏ middleware chuyển HTTPS gây lỗi trong container.
- Thêm xử lý lỗi toàn cục (`{ "error": ... }`, có log), `/health/db`.
- Xóa file mẫu thừa (`WeatherForecast*`, `Class1.cs`, `Form1*`).
