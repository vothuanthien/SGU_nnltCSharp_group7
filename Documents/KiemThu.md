# Kế hoạch và kết quả kiểm thử

Quy ước: `[x]` = đã thực sự chạy và có kết quả; `[ ]` = **chưa chạy**, cần nhóm thực hiện rồi chụp ảnh minh chứng. Không ghi "đạt" cho mục chưa chạy.

## A. Đã chạy khi soạn bản cập nhật
- [x] Sau khi gộp hai nhánh, build toàn solution thành công: 0 warning, 0 error (28/11/2026).
- [x] Chạy bộ kiểm tra console: 25 kiểm tra đạt (28/11/2026).
- [x] Build toàn solution bằng `dotnet build QuanLyThuVien.sln --no-restore -p:BaseOutputPath=.codex-build\ -p:UseAppHost=false`: 0 warning, 0 error (cập nhật 24/11/2026). Build chỉ xác nhận biên dịch; không thay thế kiểm thử nghiệp vụ.
- [x] Áp dụng migration `Database/10_ReturnCompensation.sql` lên LocalDB `LibraryManagement`; script chạy lại an toàn. Chưa chạy thử trả sách hỏng/mất qua giao diện.
- [x] Áp dụng migration `Database/11_ReaderPortal_Enhancements.sql` lên LocalDB `LibraryManagement`; script hoàn tất không lỗi.
- [x] `dotnet build` project `Library.Domain` (offline): 0 lỗi.
- [x] `dotnet run --project Tests/Library.Tests`: 25 kiểm tra đạt (LoanPolicy, PBKDF2, session store qua fake repository).
- [x] Build API với output riêng: 0 lỗi, 0 cảnh báo. Build thông thường bị khóa output bởi tiến trình `Library.Api` đang chạy; không dừng tiến trình của người dùng.
- [ ] Kiểm thử API với SQL Server sau khi áp dụng migrations: không token/token sai → 401; role không hợp lệ → 403; token hợp lệ → 200.

## B. Chưa chạy end-to-end
Build đã hoàn tất; các luồng API/SQL Server/WinForms/Web và Docker dưới đây chưa được kiểm thử nghiệp vụ đầy đủ.
### Kiểm thử demo ngày 19/11/2026

Môi trường Windows, Docker Desktop và SQL Server với dữ liệu mẫu. Khởi động theo [hướng dẫn chạy web](../CHAY-WEB.md).

- [x] Web tải được tại `http://localhost:5002`. API `/health/db` trả `database: connected` và 20 đầu sách.
- [x] Tìm `Clean Code` trên web có kết quả. Với `?page=999`, danh mục hiển thị trang cuối với 8 đầu sách.
- [x] Đăng nhập API bằng tài khoản demo, tra cứu thẻ `THE0001` và lập phiếu mượn bản sao `CSHARP-001`. Tra cứu lại thấy bản sao trong danh sách đang mượn.
- [x] Trả bản sao với tình trạng `Normal`. API trả `Available`, tiền phạt 0 và thẻ không còn hiển thị bản sao này. Số bản sao sẵn có của đầu sách qua API lần lượt là **2 → 1 → 2**.
- [x] Gửi yêu cầu trả lại cùng bản sao sau khi đã trả. API phản hồi `409 Conflict`.

Luồng mượn và trả được kiểm tra qua API. Các mục về giao diện WinForms, trả trễ, sách hư hỏng và mất sách vẫn cần kiểm tra riêng.

## B. Các kiểm tra còn lại

### B1. Build và kiểm tra logic
- [ ] Mở `QuanLyThuVien.sln` bằng Visual Studio 2022, Restore, Build Solution: 0 lỗi.
- [x] `dotnet run --project Tests/Library.Tests`: 25 kiểm tra đạt trong phiên làm việc này.

### B2. API (Postman / file `.http`)
- [ ] `POST /api/auth/login` đúng mật khẩu → 200 có `token`; sai → 401.
- [ ] `GET /api/admin/catalog/books` không token → 401; token hết hạn/sai → 401.
- [ ] `POST /api/loans` và `POST /api/returns` không token → 401 (trước đây không được bảo vệ).
- [ ] Mượn/trả: tên thủ thư trong bảng `Loans.StaffUsername` / `Returns.StaffUsername` là tên tài khoản đăng nhập, dù client có gửi `staffUsername` khác.
- [ ] Mượn: thẻ hết hạn → 409; vượt `MaxLoans` → 409; bản sao không `Available` → 409 và rollback (không phát sinh phiếu).
- [ ] Hai request mượn cùng một bản sao đồng thời: chỉ một thành công.
- [ ] Trả đúng hạn (phạt 0), trả trễ (phạt = số ngày × `FinePerDay`), `Damaged`/`Lost` có phí bồi thường > 0 → khoản phải thu bằng phạt trễ + bồi thường, trạng thái bản sao đúng; thiếu phí hoặc nhập phí cho tình trạng bình thường → bị từ chối.
- [ ] Trả lại một bản sao đã trả → 409.
- [ ] CRUD sách, bản sao, bạn đọc, tác giả, thể loại, NXB: thêm/sửa/xóa, dữ liệu trùng (ISBN, mã bản sao) → 409, xóa dữ liệu đang được tham chiếu → 409.
- [ ] `PUT /api/admin/catalog/books/{id}/authors` thay danh sách tác giả; mã tác giả không tồn tại → 409.
- [ ] Đăng ký bạn đọc trên Web với username duy nhất → tạo `Readers` có PasswordHash; username/mật khẩu đúng → đăng nhập được; mật khẩu sai hoặc username trùng → bị từ chối.
- [ ] Bạn đọc đăng nhập xem hồ sơ/lịch sử/đặt trước; thử đổi reservation ID của người khác khi hủy → không thể sửa dữ liệu người khác.
- [ ] Bạn đọc yêu cầu mượn sách còn bản → xuất hiện trong WinForms; thủ thư duyệt → tạo phiếu và bản sao Borrowed; từ chối có lý do → hiện ở cổng bạn đọc.
- [ ] Yêu cầu mượn không tạo trùng; hết sách trước lúc duyệt → yêu cầu vẫn chờ; thẻ hết hạn/bị khóa hoặc vượt giới hạn → không lập phiếu.
- [ ] Bạn đọc đặt trước khi còn bản Available hoặc đã hết bản → Pending; đặt trùng Pending/Ready → 409; thủ thư giữ một bản trong 3 ngày khi xử lý Ready.
- [ ] Gia hạn: trước hạn, chưa gia hạn lần nào, không có yêu cầu đặt trước → DueDate tăng theo LoanDays; quá hạn/đã gia hạn/có người chờ/đã trả/thẻ hết hạn → bị từ chối.
- [ ] Đổi mật khẩu: mật khẩu hiện tại sai hoặc mật khẩu mới yếu/bằng mật khẩu cũ → từ chối; đổi đúng → hash cập nhật, token hiện tại được cấp lại và token cũ bị thu hồi.
- [ ] Thông báo sau khi thủ thư duyệt/từ chối mượn, xử lý đặt trước, gia hạn hoặc ghi thanh toán; chỉ chủ thẻ đọc được và có thể đánh dấu đã đọc.
- [ ] Thủ thư chuyển Pending → Ready: bản sao chuyển sang Reserved trong 3 ngày; Ready → Fulfilled tạo phiếu mượn và bản sao Borrowed; hủy/để quá hạn giải phóng bản sao.
- [ ] Ghi thanh toán khoản phạt/bồi thường một phần/nốt số dư; số tiền âm/0/vượt dư → bị từ chối; dashboard cập nhật tổng đã thu.
- [ ] Giao diện WinForms thu một phần và thu hết; bạn đọc nhận thông báo thanh toán, Admin thấy nhật ký tương ứng.
- [ ] Admin lọc nhật ký hoạt động; Librarian và Reader không được truy cập endpoint audit.
- [ ] Xuất báo cáo quá hạn và phiếu mượn theo khoảng ngày; ngày bắt đầu sau ngày kết thúc → 400.

### B3. Cơ sở dữ liệu
- [ ] Chạy script 01–04, 07–11, 06 theo thứ tự không lỗi; chạy lại migration và 06 không tạo trùng.
- [ ] Trả trễ (phạt = số ngày × `FinePerDay`), `Damaged`, `Lost` → trạng thái bản sao đúng.
- [ ] CRUD sách, bản sao, bạn đọc, tác giả, thể loại, NXB: thêm/sửa/xóa, dữ liệu trùng (ISBN, mã bản sao) → 409, xóa dữ liệu đang được tham chiếu → 409.
- [ ] `PUT /api/admin/catalog/books/{id}/authors` thay danh sách tác giả; mã tác giả không tồn tại → 409.
- [ ] Gửi hai yêu cầu đặt trước đồng thời cho cùng thẻ và đầu sách: chỉ có tối đa một bản ghi Pending; yêu cầu còn lại nhận 409.
- [ ] Gửi lại yêu cầu đặt trước sau khi đã có Pending: nhận 409; trạng thái khác Pending không bị chỉ mục này chặn.
- [ ] Tạo phiên, restart API nhưng giữ nguyên SQL Server/database: token còn hạn vẫn dùng được; kiểm tra tương tự khi chạy hai API instance.
- [ ] Đăng xuất, khóa/reset tài khoản nhân viên hoặc ngừng hoạt động thẻ: các phiên tương ứng bị thu hồi.

- [ ] Chạy scripts `01–04`, `07`, `08`, `10`; chạy lại migrations không tạo trùng hoặc lỗi.
- [ ] Migration `07` dừng an toàn nếu đã có yêu cầu Pending trùng; dọn dữ liệu có kiểm soát rồi chạy lại.
- [ ] Vi phạm CHECK (năm xuất bản, trạng thái bản sao), UNIQUE, khóa ngoại cho kết quả đúng.
- [ ] Rà soát 3NF (đối chiếu báo cáo, mục 3.7–3.8).

### B4. WinForms
- [ ] Mở ứng dụng: bắt buộc đăng nhập; sai mật khẩu bị từ chối; dừng API → báo lỗi kết nối rõ ràng.
- [ ] Lưới hiển thị đúng cột cho 6 danh mục (Sách, Bản sao, Bạn đọc, Thể loại, Tác giả, NXB).
- [ ] Thêm / Sửa / Xóa / Tìm kiếm từng danh mục; "Tác giả của sách"; màn hình "Mượn / Trả".
- [ ] Nhận trả sách `Damaged`/`Lost`: bắt buộc nhập bồi thường; kết quả và màn hình thu tiền hiển thị phạt trễ, bồi thường và tổng đúng.
- [ ] Ghi nhận thanh toán một phần/toàn bộ qua WinForms và xác minh thông tin còn nợ.
- [ ] Admin mở nhật ký hoạt động; tài khoản Librarian không thấy nút và API trả 403.

### B5. Web ↔ WinForms dùng chung dữ liệu
- [ ] Thêm sách ở WinForms → tải lại Web thấy sách mới (cùng thể loại, tác giả).
- [ ] Đăng ký bạn đọc trên Web → tìm thấy hồ sơ ở WinForms; mượn sách cho thẻ mới → bạn đọc thấy lượt mượn trong trang cá nhân.
- [ ] Lập phiếu mượn ở WinForms → Web hiển thị "bản sao sẵn có" giảm; trả sách → tăng lại.
- [ ] Tắt API: Web hiển thị thông báo, WinForms báo lỗi, không bị treo.

### B6. Docker
- [ ] `docker compose up -d --build` thành công; `docker compose ps` hiển thị 2 container (sqlserver healthy, api running).
- [ ] `GET http://localhost:5001/health` và `/health/db` trả `ok`.
- [ ] Web MVC và WinForms trên Windows gọi được API trong container.
- [ ] `docker compose stop` rồi `up -d`: dữ liệu (sách, phiếu mượn) còn nguyên.
- [ ] Khởi động `api` khi SQL Server chưa sẵn sàng: log có "SQL Server chưa sẵn sàng" rồi tự thành công.

## C. Minh chứng cần chụp cho báo cáo
Docker Desktop (2 container), `/health/db`, đăng nhập WinForms, CRUD, mượn/trả, Web hiển thị cùng dữ liệu, kết quả `dotnet run` của Tests.
