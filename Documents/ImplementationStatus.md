# Đối chiếu yêu cầu và trạng thái

Cập nhật sau đợt rà soát (xem `KiemThu.md` để biết mục nào đã chạy). **"Đã viết" không có nghĩa "đã kiểm thử tích hợp"**: ngoài mục A trong `KiemThu.md`, mọi thứ dưới đây chưa được chạy với SQL Server.

| Nhóm | Trạng thái |
|---|---|
| Xác thực | Có: đăng nhập Admin/Thủ thư và bạn đọc; mật khẩu PBKDF2-SHA256. Bearer token ngẫu nhiên hết hạn sau 8 giờ, chỉ lưu SHA-256 hash trong `dbo.AdminSessions`; dùng chung SQL Server để tồn tại qua restart/nhiều API instance. Đây không phải JWT. |
| Phân quyền | `[Authorize(Roles="Admin,Librarian")]` trên API quản trị/mượn-trả; quản lý tài khoản nhân viên giới hạn Admin. Bạn đọc đăng ký bằng số thẻ và email đã lưu, rồi truy cập lịch sử/đặt trước của chính mình. |
| Mượn / trả | Đã viết lại: tên thủ thư lấy từ phiên đăng nhập, dùng `LoanPolicy` của Domain cho giới hạn mượn/hạn trả/phạt, ngày trả theo đồng hồ SQL Server. Chưa kiểm thử tích hợp. Mất sách: chỉ đổi trạng thái bản sao, **chưa tính phí bồi thường**. |
| CRUD | Sách, bản sao, bạn đọc, tác giả, thể loại, NXB (API + WinForms); gán tác giả cho sách. Xóa mềm với sách/bản sao/bạn đọc/thể loại. |
| Web bạn đọc | Danh mục, tìm kiếm, lọc, phân trang, chi tiết; đăng ký/đăng nhập; gửi yêu cầu đặt trước khi hết sách; xem lịch sử cá nhân. |
| Đặt trước | API chỉ ghi một lần; unique filtered index `UX_Reservations_Pending_Reader_Book` trên `(ReaderId, BookId)` ngăn trùng Pending đồng thời. Lỗi unique được trả `409 Conflict`. Cần chạy migration `07` và kiểm thử đồng thời trên SQL Server. |
| Thanh toán phạt, nhật ký xem, báo cáo, backup | Chưa triển khai. `AuditLogs` được ghi khi CRUD, chưa có màn hình xem. |
| Docker | Đã có cấu hình và tự áp dụng migrations 07, 08, 10. Cần chạy checklist Docker trong `KiemThu.md`. |
| Kiểm thử | Console suite: 25 kiểm tra đạt (Domain, PBKDF2, session-store qua fake repository). API build độc lập đạt; chưa có kiểm thử tích hợp SQL Server cho phiên/đặt trước. |
| Bảo mật còn thiếu | Chưa giới hạn số lần đăng nhập sai; chưa có đổi mật khẩu tự phục vụ (Admin có thể reset mật khẩu nhân viên); production cần HTTPS và bí mật triển khai an toàn. |

## Thay đổi trong đợt này
- Sửa lỗi bảo mật: `/api/loans` và `/api/returns` trước đây **không** yêu cầu đăng nhập và tin `staffUsername` do client gửi (tài liệu cũ ghi sai là đã bảo vệ).
- Sửa lỗi WinForms: lưới bind trực tiếp `Dictionary` nên hiển thị sai cột; chuyển sang `DataTable`.
- Bỏ CORS (Web gọi API từ phía máy chủ, WinForms là ứng dụng native nên không cần), bỏ middleware chuyển HTTPS gây lỗi trong container.
- Thêm xử lý lỗi toàn cục (`{ "error": ... }`, có log), `/health/db`.
- Xóa file mẫu thừa (`WeatherForecast*`, `Class1.cs`, `Form1*`).
- Đặt trước dùng chỉ mục unique để xử lý race condition, trả 409 khi bản ghi Pending đã tồn tại.
- Phiên bearer được lưu dạng hash trong SQL; vô hiệu hóa phiên cùng transaction khi khóa/đặt lại mật khẩu tài khoản hoặc ngừng hoạt động thẻ bạn đọc.
