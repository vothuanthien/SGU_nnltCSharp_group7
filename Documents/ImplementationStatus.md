# Đối chiếu yêu cầu và trạng thái

Cập nhật sau đợt rà soát (xem `KiemThu.md` để biết mục nào đã chạy). **"Đã viết" không có nghĩa "đã kiểm thử tích hợp"**: ngoài mục A trong `KiemThu.md`, mọi thứ dưới đây chưa được chạy với SQL Server.

| Nhóm | Trạng thái |
|---|---|
| Xác thực | Phiên bearer dùng token ngẫu nhiên; chỉ SHA-256 hash được lưu trong `dbo.AdminSessions` và phiên còn hiệu lực qua restart API. Bạn đọc đăng ký bằng số thẻ và email đã lưu; thông tin đăng nhập nằm trong `ReaderAccounts`, mật khẩu dùng PBKDF2-SHA256. Phiên hết hạn sau 8 giờ. |
| Phân quyền | API quản trị/mượn-trả giới hạn theo vai trò Admin/Librarian; quản lý tài khoản và audit log dành cho Admin. Bạn đọc chỉ đọc hoặc cập nhật dữ liệu gắn với tài khoản của mình. |
| Mượn / trả | Dùng `LoanPolicy` cho giới hạn mượn, hạn trả và phạt trễ. Thủ thư nhập phí bồi thường khi sách hỏng/mất; thanh toán ghi nhận một phần hoặc toàn bộ khoản phải thu. |
| CRUD | Quản lý sách, bản sao, bạn đọc, tác giả, thể loại, nhà xuất bản qua API và WinForms; hỗ trợ gán tác giả và xóa mềm các hồ sơ được hỗ trợ. |
| Web bạn đọc | Danh mục, tìm kiếm, lọc/sắp xếp, phân trang, đăng ký/đăng nhập, lịch sử mượn, yêu cầu mượn/đặt trước, gia hạn, đổi mật khẩu và thông báo. Dữ liệu lịch sử chỉ thuộc tài khoản đang đăng nhập. |
| Đặt trước | Bạn đọc gửi yêu cầu khi sách còn hoặc hết bản; thủ thư giữ bản sao tối đa 3 ngày, hủy hoặc hoàn tất thành phiếu mượn. Unique index và kiểm tra API ngăn yêu cầu hoạt động trùng. |
| Yêu cầu mượn | Bạn đọc gửi yêu cầu; thủ thư duyệt/từ chối trong WinForms. Khi duyệt, API kiểm tra thẻ/giới hạn và tạo phiếu cùng lúc gán bản sao trong transaction. |
| Thanh toán, báo cáo, nhật ký | API và WinForms hỗ trợ ghi nhận thanh toán; Dashboard xuất báo cáo; Admin xem nhật ký hoạt động. |
| Docker | Đã có Dockerfile, compose và tự khởi tạo database/migrations; cần chạy checklist Docker trước khi nộp. |
| Kiểm thử | Build solution đã xác nhận 0 warning/0 error. Bộ kiểm tra console hiện có: 25 kiểm tra đạt. Chưa xác nhận các luồng tích hợp SQL Server, Web–API–WinForms hoặc Docker trong lượt này. |
| Bảo mật còn thiếu | Chưa giới hạn số lần đăng nhập sai hoặc có luồng quên mật khẩu qua email; triển khai thật cần HTTPS và bí mật cấu hình an toàn. |
## Thay đổi trong đợt này
- Sửa lỗi bảo mật: `/api/loans` và `/api/returns` trước đây **không** yêu cầu đăng nhập và tin `staffUsername` do client gửi (tài liệu cũ ghi sai là đã bảo vệ).
- Sửa lỗi WinForms: lưới bind trực tiếp `Dictionary` nên hiển thị sai cột; chuyển sang `DataTable`.
- Bỏ CORS (Web gọi API từ phía máy chủ, WinForms là ứng dụng native nên không cần), bỏ middleware chuyển HTTPS gây lỗi trong container.
- Thêm xử lý lỗi toàn cục (`{ "error": ... }`, có log), `/health/db`.
- Xóa file mẫu thừa (`WeatherForecast*`, `Class1.cs`, `Form1*`).
- Đặt trước dùng chỉ mục unique để xử lý race condition, trả 409 khi bản ghi Pending đã tồn tại.
- Phiên bearer được lưu dạng hash trong SQL; vô hiệu hóa phiên cùng transaction khi khóa/đặt lại mật khẩu tài khoản hoặc ngừng hoạt động thẻ bạn đọc.
