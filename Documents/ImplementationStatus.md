# Đối chiếu yêu cầu và trạng thái

Tài liệu môn học yêu cầu ứng dụng Web C#, ứng dụng quản trị Windows Forms, dùng chung SQL Server; báo cáo lần 1 cung cấp phân tích thiết kế chứ không chứng minh chức năng đã chạy.

| Nhóm | Mã giữ nguyên | Trạng thái trong gói hiện tại |
|---|---|---|
| Đăng nhập/quyền | FR-01..02, UC-01, UC-08, US tương ứng | Bản bổ sung có API login JWT, PBKDF2, role Admin/Librarian và tài khoản demo tạo bằng script 06; cần build/test trên máy phát triển. |
| Sách/bản sao/danh mục | FR-03..05, UC-02, UC-05, US-01..02 | Bản bổ sung có API CRUD đầu sách và form WinForms; quản lý bản sao và danh mục phụ chưa hoàn tất; cần build/test. |
| Mượn | FR-06..07, UC-03, BR-03..06, BR-12 | Nghiệp vụ nền đã chạy trên LocalDB; bản bổ sung thêm `[Authorize]` role Admin/Librarian và lấy StaffUsername từ JWT; cần chạy lại kiểm thử sau bảo vệ endpoint. Chưa kiểm thử đồng thời hai thủ thư. |
| Trả/phạt/quá hạn | FR liên quan, UC-04, BR-07..09, US liên quan | Nghiệp vụ nền đã nhận trả bình thường trên LocalDB; bản bổ sung thêm `[Authorize]` role Admin/Librarian và lấy StaffUsername từ JWT; cần chạy lại kiểm thử sau bảo vệ endpoint. Chưa có UI mượn/trả. |
| Bạn đọc/đặt trước/lịch sử | FR liên quan, UC-06, BR-11 | Schema cơ sở; endpoint xác thực/ủy quyền chưa hoàn tất. |
| Báo cáo/nhật ký/backup | UC-07, UC-09, NFR-07, NFR-09 | Bảng nền; quy trình và màn hình chưa hoàn tất. |
| Chất lượng | NFR-01..10 | Mục tiêu thiết kế; chưa đo hiệu năng, an toàn, khả năng truy cập hay khôi phục. |

## Bất định và giả định
- Số ngày mượn, số cuốn tối đa, mức phạt và ưu tiên giữ chỗ cần thư viện xác nhận; schema dùng cấu hình cho LoanDays/FinePerDay/MaxLoans.
- Báo cáo gốc mô tả mô hình giả định, chưa có khảo sát thực địa.
- Đã build solution đạt 0 cảnh báo/0 lỗi; chạy unit checks đạt; LocalDB chạy được, scripts 01–05 hoàn tất, API health/tra cứu và Web/Desktop đã đọc dữ liệu mẫu. Chưa kiểm thử concurrency, bảo mật, backup/restore hoặc toàn bộ giao diện/nghiệp vụ.

Lưu ý bảo mật: bản bổ sung đã đặt `[Authorize(Roles = "Admin,Librarian")]` cho API mượn/trả và lấy tên thủ thư từ claim. Do chưa build/chạy lại sau khi thêm JWT, cần kiểm thử 401/200 và các giao dịch mượn/trả trên máy phát triển trước khi coi là hoàn tất.

## Chạy demo đã xác minh
- SQL Server LocalDB instance `(localdb)\MSSQLLocalDB`; database `LibraryManagement`.
- API health `http://localhost:5001/health`; Web `http://localhost:5002`; Windows Forms đã mở.
- Có 1 phiếu mượn + 1 lần trả demo trong database; hiện các bản sao đều Available.


## Bổ sung mã nguồn sau bản ZIP gốc

Đã thêm các file login JWT, PasswordHasher PBKDF2, TokenService, API CRUD đầu sách, form LoginForm và BookManagementForm; cập nhật API mượn/trả để yêu cầu token. Tài khoản demo được tạo bởi `Database/06_CreateDemoAdmin.sql`. Đây là trạng thái mã nguồn đã bổ sung, **chưa phải kết quả kiểm thử**: cần restore NuGet, build solution và chạy checklist trong `Documents/BoSung_DangNhap_CRUD.md`.
