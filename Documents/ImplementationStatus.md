# Đối chiếu yêu cầu và trạng thái

Cập nhật sau đợt rà soát (xem `KiemThu.md` để biết mục nào đã chạy). **"Đã viết" không có nghĩa "đã kiểm thử tích hợp"**: ngoài mục A trong `KiemThu.md`, mọi thứ dưới đây chưa được chạy với SQL Server.

| Nhóm | Trạng thái |
|---|---|
| Xác thực | Có: `POST /api/auth/login`, mật khẩu PBKDF2-SHA256, **bearer token ngẫu nhiên lưu trong bộ nhớ API, hết hạn sau 8 giờ. Đây không phải JWT.** Handler `SessionAuthenticationHandler` đã chạy thử (401/403/200). |
| Phân quyền | `[Authorize(Roles="Admin,Librarian")]` trên `/api/admin/catalog`, `/api/loans`, `/api/returns`. Hiện Admin và Librarian có quyền như nhau; chưa có chức năng chỉ riêng Admin (quản lý tài khoản chưa làm). |
| Mượn / trả | Đã viết lại: tên thủ thư lấy từ phiên đăng nhập, dùng `LoanPolicy` của Domain cho giới hạn mượn/hạn trả/phạt, ngày trả theo đồng hồ SQL Server. Chưa kiểm thử tích hợp. Mất sách: chỉ đổi trạng thái bản sao, **chưa tính phí bồi thường**. |
| CRUD | Sách, bản sao, bạn đọc, tác giả, thể loại, NXB (API + WinForms); gán tác giả cho sách. Xóa mềm với sách/bản sao/bạn đọc/thể loại. |
| Web bạn đọc | Danh mục, tìm kiếm, lọc thể loại, phân trang, chi tiết sách, trang hướng dẫn. **Chưa có** đăng nhập bạn đọc, đặt trước, lịch sử cá nhân. |
| Đặt trước, thanh toán phạt, nhật ký xem, báo cáo, backup | **Chưa triển khai.** Bảng `Reservations`, `FinePayments` có trong schema nhưng chưa có API/giao diện. `AuditLogs` chỉ được ghi khi CRUD, chưa có màn hình xem. |
| Docker | Đã viết `Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`, khởi tạo DB tự động. **Chưa chạy thử** (xem `Docker.md`). |
| Kiểm thử | Console test 20 kiểm tra đạt (Domain, băm mật khẩu, kho phiên). Chưa dùng xUnit; chưa có test tích hợp. |
| Bảo mật còn thiếu | Chưa giới hạn số lần đăng nhập sai; chưa có đổi mật khẩu/tạo tài khoản; chỉ HTTP trên localhost (không dùng triển khai thật). |

## Thay đổi trong đợt này
- Sửa lỗi bảo mật: `/api/loans` và `/api/returns` trước đây **không** yêu cầu đăng nhập và tin `staffUsername` do client gửi (tài liệu cũ ghi sai là đã bảo vệ).
- Sửa lỗi WinForms: lưới bind trực tiếp `Dictionary` nên hiển thị sai cột; chuyển sang `DataTable`.
- Bỏ CORS (Web gọi API từ phía máy chủ, WinForms là ứng dụng native nên không cần), bỏ middleware chuyển HTTPS gây lỗi trong container.
- Thêm xử lý lỗi toàn cục (`{ "error": ... }`, có log), `/health/db`.
- Xóa file mẫu thừa (`WeatherForecast*`, `Class1.cs`, `Form1*`).
