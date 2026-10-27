# Đối chiếu yêu cầu và trạng thái

Tài liệu môn học yêu cầu ứng dụng Web C#, ứng dụng quản trị Windows Forms, dùng chung SQL Server; báo cáo lần 1 cung cấp phân tích thiết kế chứ không chứng minh chức năng đã chạy.

| Nhóm | Mã giữ nguyên | Trạng thái trong gói hiện tại |
|---|---|---|
| Đăng nhập/quyền | FR-01..02, UC-01, UC-08, US tương ứng | Chưa triển khai đăng nhập hoàn chỉnh; không có tài khoản mặc định. |
| Sách/bản sao/danh mục | FR-03..05, UC-02, UC-05, US-01..02 | Schema + API tra cứu bằng EF Core; CRUD chưa hoàn tất. |
| Mượn | FR-06..07, UC-03, BR-03..06, BR-12 | Đã chạy trên LocalDB: tạo phiếu thật thành công; gửi cuốn hợp lệ kèm mã sai bị rollback, không tạo phiếu dở dang. Chưa kiểm thử đồng thời hai thủ thư. |
| Trả/phạt/quá hạn | FR liên quan, UC-04, BR-07..09, US liên quan | Đã nhận trả bình thường trên LocalDB; tiền phạt 0 đúng hạn và bản sao về Available. Chưa kiểm thử quá hạn/hỏng qua endpoint hoặc tích hợp UI. |
| Bạn đọc/đặt trước/lịch sử | FR liên quan, UC-06, BR-11 | Schema cơ sở; endpoint xác thực/ủy quyền chưa hoàn tất. |
| Báo cáo/nhật ký/backup | UC-07, UC-09, NFR-07, NFR-09 | Bảng nền; quy trình và màn hình chưa hoàn tất. |
| Chất lượng | NFR-01..10 | Mục tiêu thiết kế; chưa đo hiệu năng, an toàn, khả năng truy cập hay khôi phục. |

## Bất định và giả định
- Số ngày mượn, số cuốn tối đa, mức phạt và ưu tiên giữ chỗ cần thư viện xác nhận; schema dùng cấu hình cho LoanDays/FinePerDay/MaxLoans.
- Báo cáo gốc mô tả mô hình giả định, chưa có khảo sát thực địa.
- Đã build solution đạt 0 cảnh báo/0 lỗi; chạy unit checks đạt; LocalDB chạy được, scripts 01–05 hoàn tất, API health/tra cứu và Web/Desktop đã đọc dữ liệu mẫu. Chưa kiểm thử concurrency, bảo mật, backup/restore hoặc toàn bộ giao diện/nghiệp vụ.

Lưu ý bảo mật: API mượn/trả hiện chưa có xác thực/ủy quyền; chỉ phù hợp demo local, không triển khai thật trước khi bổ sung bảo vệ endpoint.

## Chạy demo đã xác minh
- SQL Server LocalDB instance `(localdb)\MSSQLLocalDB`; database `LibraryManagement`.
- API health `http://localhost:5001/health`; Web `http://localhost:5002`; Windows Forms đã mở.
- Có 1 phiếu mượn + 1 lần trả demo trong database; hiện các bản sao đều Available.
