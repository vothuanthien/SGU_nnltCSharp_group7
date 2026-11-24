using System.Data;
using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

/// <summary>
/// Màn hình mượn/trả sách dành cho thủ thư; tích hợp tra cứu nhanh thẻ bạn đọc,
/// kiểm tra điều kiện mượn và hỗ trợ nhận trả trực quan.
/// </summary>
internal sealed class CirculationForm : Form
{
    private sealed record ConditionItem(string Value, string Caption);

    private readonly HttpClient _api;
    private readonly TextBox _card = new() { Width = 180, PlaceholderText = "Ví dụ: THE0001" };
    private readonly Button _lookupBtn = new() { Text = "🔍 Tra cứu thẻ", AutoSize = true };
    private readonly Label _readerInfo = new() { Text = "Chưa có thông tin bạn đọc. Nhập số thẻ và bấm 'Tra cứu thẻ'.", AutoSize = true, ForeColor = Color.DimGray, Padding = new Padding(0, 4, 0, 4) };

    private readonly TextBox _copies = new() { Dock = DockStyle.Fill, PlaceholderText = "Mã bản sao, cách nhau dấu phẩy hoặc khoảng trắng (Ví dụ: CS001, CS002)" };
    private readonly Button _loanButton = new() { Text = "Lập phiếu mượn", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

    private readonly TextBox _returnCopy = new() { Dock = DockStyle.Fill, PlaceholderText = "Mã bản sao đang mượn (hoặc nhấp đúp từ danh sách trên)" };
    private readonly ComboBox _condition = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Caption" };
    private readonly Button _returnButton = new() { Text = "Nhận trả sách", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

    private readonly DataGridView _gridLoans = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false,
        RowHeadersVisible = false,
        Height = 150
    };

    private readonly TextBox _result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private bool _canBorrow = false;
    private int _openLoans;
    private int _maxLoans;

    public CirculationForm(HttpClient api)
    {
        _api = api;
        Text = "Mượn / Trả sách & Tra cứu thẻ bạn đọc";
        Width = 880;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;

        _condition.Items.AddRange(new object[]
        {
            new ConditionItem("Normal", "Bình thường"),
            new ConditionItem("Damaged", "Hư hỏng"),
            new ConditionItem("Lost", "Mất sách")
        });
        _condition.SelectedIndex = 0;

        _lookupBtn.Click += async (_, _) => await LookupReaderAsync();
        _loanButton.Click += async (_, _) => await CreateLoanAsync();
        _returnButton.Click += async (_, _) => await ReturnAsync();

        // Nhấp vào một bản sao đang mượn sẽ tự động điền vào ô nhận trả
        _gridLoans.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _gridLoans.Rows[e.RowIndex].DataBoundItem is DataRowView row)
            {
                var copyCode = row["Mã cuốn"]?.ToString();
                if (!string.IsNullOrWhiteSpace(copyCode))
                {
                    _returnCopy.Text = copyCode;
                    _result.Text = $"Đã chọn bản sao [{copyCode}] để nhận trả. Vui lòng chọn tình trạng sách và bấm 'Nhận trả sách'.";
                }
            }
        };

        // Bố cục giao diện
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Tra cứu thẻ
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170)); // Danh sách sách đang mượn
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105)); // Lập phiếu mượn
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105)); // Nhận trả sách
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Khung kết quả

        // 1. Group Tra cứu thẻ
        var lookupGroup = new GroupBox { Text = "1. Tra cứu thẻ bạn đọc", Dock = DockStyle.Fill };
        var lookupFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6) };
        lookupFlow.Controls.AddRange([new Label { Text = "Số thẻ:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) }, _card, _lookupBtn]);
        lookupGroup.Controls.Add(_readerInfo);
        lookupGroup.Controls.Add(lookupFlow);
        _readerInfo.Dock = DockStyle.Bottom;
        mainLayout.Controls.Add(lookupGroup, 0, 0);

        // 2. Group Sách đang mượn
        var loansGroup = new GroupBox { Text = "Sách đang mượn của bạn đọc (nhấp đúp để chọn nhận trả)", Dock = DockStyle.Fill };
        loansGroup.Controls.Add(_gridLoans);
        mainLayout.Controls.Add(loansGroup, 0, 1);

        // 3. Group Lập phiếu mượn
        var loanGroup = new GroupBox { Text = "2. Lập phiếu mượn mới", Dock = DockStyle.Fill };
        var loanLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 3 };
        loanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        loanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        loanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        loanLayout.Controls.Add(new Label { Text = "Mã bản sao:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        loanLayout.Controls.Add(_copies, 1, 0);
        loanLayout.Controls.Add(_loanButton, 2, 0);
        loanGroup.Controls.Add(loanLayout);
        mainLayout.Controls.Add(loanGroup, 0, 2);

        // 4. Group Nhận trả sách
        var returnGroup = new GroupBox { Text = "3. Nhận trả sách", Dock = DockStyle.Fill };
        var returnLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 5 };
        returnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        returnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        returnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        returnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        returnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        returnLayout.Controls.Add(new Label { Text = "Mã cuốn:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        returnLayout.Controls.Add(_returnCopy, 1, 0);
        returnLayout.Controls.Add(new Label { Text = "Tình trạng:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        returnLayout.Controls.Add(_condition, 3, 0);
        returnLayout.Controls.Add(_returnButton, 4, 0);
        returnGroup.Controls.Add(returnLayout);
        mainLayout.Controls.Add(returnGroup, 0, 3);

        // 5. Khung thông báo kết quả
        var resultGroup = new GroupBox { Text = "Thông báo kết quả xử lý", Dock = DockStyle.Fill };
        resultGroup.Controls.Add(_result);
        mainLayout.Controls.Add(resultGroup, 0, 4);

        Controls.Add(mainLayout);
        _loanButton.Enabled = false;
        _card.TextChanged += (_, _) =>
        {
            _canBorrow = false;
            _loanButton.Enabled = false;
            _readerInfo.Text = "Số thẻ đã thay đổi. Hãy tra cứu lại trước khi lập phiếu mượn.";
            _gridLoans.DataSource = null;
        };
        _card.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LookupReaderAsync();
            }
        };
    }

    private async Task LookupReaderAsync()
    {
        var card = _card.Text.Trim();
        if (string.IsNullOrWhiteSpace(card))
        {
            MessageBox.Show("Vui lòng nhập số thẻ bạn đọc để tra cứu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _lookupBtn.Enabled = false;
        _readerInfo.Text = "Đang tra cứu dữ liệu thẻ bạn đọc...";
        try
        {
            var response = await _api.GetAsync($"api/readers/card/{Uri.EscapeDataString(card)}");
            if (!response.IsSuccessStatusCode)
            {
                _canBorrow = false;
                _loanButton.Enabled = false;
                _readerInfo.ForeColor = Color.Firebrick;
                _readerInfo.Text = $"Không tìm thấy bạn đọc với số thẻ '{card}' hoặc thẻ không hợp lệ.";
                _gridLoans.DataSource = null;
                return;
            }

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            var fullName = doc.GetProperty("fullName").GetString();
            var status = doc.GetProperty("status").GetString();
            var expiry = doc.GetProperty("expiryDate").GetString();
            var isExpired = doc.GetProperty("isExpired").GetBoolean();
            _canBorrow = doc.GetProperty("canBorrow").GetBoolean();
            _openLoans = doc.GetProperty("openLoansCount").GetInt32();
            _maxLoans = doc.GetProperty("maxLoans").GetInt32();
            _loanButton.Enabled = _canBorrow;

            var statusDesc = status == "Active" ? (isExpired ? "ĐÃ HẾT HẠN" : "ĐANG HOẠT ĐỘNG") : "BỊ KHÓA";
            if (_openLoans >= _maxLoans) statusDesc = "ĐÃ ĐẠT GIỚI HẠN MƯỢN";
            _readerInfo.ForeColor = _canBorrow ? Color.ForestGreen : Color.Firebrick;
            _readerInfo.Text = $"Bạn đọc: {fullName} | Thẻ: {card} | Hạn thẻ: {expiry} ({statusDesc}) | Đang mượn: {_openLoans}/{_maxLoans} cuốn";

            // Hiển thị danh sách sách đang mượn
            var table = new DataTable();
            table.Columns.Add("Mã cuốn", typeof(string));
            table.Columns.Add("Tên sách", typeof(string));
            table.Columns.Add("Ngày mượn", typeof(string));
            table.Columns.Add("Hạn trả", typeof(string));
            table.Columns.Add("Tình trạng", typeof(string));
            table.Columns.Add("Phạt dự kiến", typeof(string));

            var books = doc.GetProperty("borrowedBooks").EnumerateArray();
            foreach (var b in books)
            {
                var copyCode = b.GetProperty("copyCode").GetString();
                var title = b.GetProperty("title").GetString();
                var loanDate = b.GetProperty("loanDate").GetString();
                var dueDate = b.GetProperty("dueDate").GetString();
                var overdueDays = b.GetProperty("overdueDays").GetInt32();
                var fine = b.GetProperty("fineExpected").GetDecimal();

                var statusText = overdueDays > 0 ? $"Quá hạn {overdueDays} ngày" : "Trong hạn";
                table.Rows.Add(copyCode, title, loanDate, dueDate, statusText, fine > 0 ? $"{fine:N0} đ" : "0 đ");
            }

            _gridLoans.DataSource = table;
            _result.Text = $"Tra cứu thành công. Bạn đọc hiện đang mượn {_openLoans} cuốn sách.";
        }
        catch (HttpRequestException)
        {
            _readerInfo.ForeColor = Color.Firebrick;
            _readerInfo.Text = "Không kết nối được dịch vụ tra cứu. Vui lòng thử lại sau.";
            _canBorrow = false;
            _loanButton.Enabled = false;
        }
        catch (JsonException)
        {
            _readerInfo.ForeColor = Color.Firebrick;
            _readerInfo.Text = "Dịch vụ trả về dữ liệu không hợp lệ. Vui lòng thử lại sau.";
            _canBorrow = false;
            _loanButton.Enabled = false;
        }
        finally
        {
            _lookupBtn.Enabled = true;
        }
    }

    private async Task CreateLoanAsync()
    {
        var card = _card.Text.Trim();
        var codes = _copies.Text.Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(card) || codes.Length == 0)
        {
            _result.Text = "Hãy nhập số thẻ bạn đọc và ít nhất một mã bản sao cần mượn.";
            return;
        }

        if (!_canBorrow)
        {
            _result.Text = "Hãy tra cứu thẻ hợp lệ trước khi lập phiếu. Thẻ hết hạn, bị khóa hoặc đã đạt giới hạn sẽ không được mượn thêm.";
            return;
        }

        if (_openLoans + codes.Length > _maxLoans)
        {
            _result.Text = $"Vượt giới hạn mượn. Hiện có {_openLoans} cuốn; yêu cầu thêm {codes.Length}; giới hạn là {_maxLoans}.";
            return;
        }

        _loanButton.Enabled = false;
        try
        {
            using var response = await PostAsync("api/loans", new { cardNumber = card, copyCodes = codes });
            if (response is null) return;

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            _result.Text = $"✓ Đã lập phiếu mượn [{data.GetProperty("loanCode").GetString()}] thành công!\n" +
                           $"Số lượng sách: {data.GetProperty("copies").GetInt32()} cuốn. Thời hạn mượn: {data.GetProperty("loanDays").GetInt32()} ngày.";
            _copies.Clear();

            // Tải lại thông tin bạn đọc sau khi mượn
            await LookupReaderAsync();
        }
        finally
        {
            _loanButton.Enabled = _canBorrow;
        }
    }

    private async Task ReturnAsync()
    {
        var copyCode = _returnCopy.Text.Trim();
        if (string.IsNullOrWhiteSpace(copyCode))
        {
            _result.Text = "Hãy nhập hoặc nhấp chọn mã bản sao cần trả.";
            return;
        }

        var condition = ((ConditionItem)_condition.SelectedItem!).Value;
        _returnButton.Enabled = false;
        try
        {
            using var response = await PostAsync("api/returns", new { copyCode, condition });
            if (response is null) return;

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            var overdueDays = data.GetProperty("overdueDays").GetInt32();
            var fine = data.GetProperty("fineAmount").GetDecimal();
            var status = data.GetProperty("status").GetString();

            _result.Text = $"✓ Nhận trả sách thành công cho cuốn [{copyCode}]!\n" +
                           $"Trạng thái cuốn: {status}. Số ngày trễ: {overdueDays} ngày. Tiền phạt: {fine:N0} VNĐ.";
            _returnCopy.Clear();

            // Nếu đang tra cứu số thẻ thì tự động cập nhật lại danh sách sách đang mượn
            if (!string.IsNullOrWhiteSpace(_card.Text))
            {
                await LookupReaderAsync();
            }
        }
        finally
        {
            _returnButton.Enabled = true;
        }
    }

    private async Task<HttpResponseMessage?> PostAsync(string url, object body)
    {
        try
        {
            var response = await _api.PostAsJsonAsync(url, body);
            if (response.IsSuccessStatusCode) return response;
            _result.Text = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn, hãy đăng nhập lại.",
                System.Net.HttpStatusCode.Forbidden => "Tài khoản không có quyền thực hiện thao tác này.",
                System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout => "Dịch vụ đang tạm thời gián đoạn. Hãy kiểm tra trạng thái giao dịch trước khi gửi lại.",
                _ => await ReadErrorAsync(response)
            };
            response.Dispose();
            return null;
        }
        catch (HttpRequestException)
        {
            _result.Text = "Chưa nhận được xác nhận từ dịch vụ. Hãy kiểm tra trạng thái giao dịch trước khi gửi lại.";
            return null;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString() ?? "Không thể hoàn tất thao tác. Vui lòng kiểm tra dữ liệu.";
            return "Không thể hoàn tất thao tác. Vui lòng kiểm tra dữ liệu và thử lại.";
        }
        catch (JsonException)
        {
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.UnprocessableEntity => "Dữ liệu chưa hợp lệ hoặc trạng thái đã thay đổi. Hãy kiểm tra lại thông tin.",
                _ => "Không thể hoàn tất thao tác. Mã tham chiếu HTTP " + (int)response.StatusCode + "."
            };
        }
    }
}
