using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

/// <summary>Màn hình mượn/trả dành cho thủ thư; gọi /api/loans và /api/returns bằng phiên đã đăng nhập.</summary>
internal sealed class CirculationForm : Form
{
    private sealed record ConditionItem(string Value, string Caption);

    private readonly HttpClient _api;
    private readonly TextBox _card = new() { Dock = DockStyle.Fill, PlaceholderText = "Số thẻ, ví dụ THE0001" };
    private readonly TextBox _copies = new() { Dock = DockStyle.Fill, PlaceholderText = "Mã bản sao, cách nhau dấu phẩy" };
    private readonly TextBox _returnCopy = new() { Dock = DockStyle.Fill, PlaceholderText = "Mã bản sao đang được mượn" };
    private readonly ComboBox _condition = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Caption" };
    private readonly TextBox _result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    public CirculationForm(HttpClient api)
    {
        _api = api;
        Text = "Mượn / Trả sách"; Width = 640; Height = 520; StartPosition = FormStartPosition.CenterParent;
        _condition.Items.AddRange(new object[] { new ConditionItem("Normal", "Bình thường"), new ConditionItem("Damaged", "Hư hỏng"), new ConditionItem("Lost", "Mất sách") });
        _condition.SelectedIndex = 0;

        var loanButton = new Button { Text = "Lập phiếu mượn", AutoSize = true };
        loanButton.Click += async (_, _) => await CreateLoanAsync();
        var returnButton = new Button { Text = "Nhận trả", AutoSize = true };
        returnButton.Click += async (_, _) => await ReturnAsync();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddHeader(layout, "LẬP PHIẾU MƯỢN", 0);
        AddRow(layout, "Số thẻ bạn đọc", _card, 1);
        AddRow(layout, "Bản sao", _copies, 2);
        layout.Controls.Add(loanButton, 1, 3);
        AddHeader(layout, "NHẬN TRẢ SÁCH", 4);
        AddRow(layout, "Bản sao", _returnCopy, 5);
        AddRow(layout, "Tình trạng", _condition, 6);
        layout.Controls.Add(returnButton, 1, 7);

        var resultGroup = new GroupBox { Text = "Kết quả", Dock = DockStyle.Bottom, Height = 150 };
        resultGroup.Controls.Add(_result);
        Controls.Add(layout); Controls.Add(resultGroup);
    }

    private static void AddHeader(TableLayoutPanel layout, string text, int row)
    {
        var label = new Label { Text = text, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Padding = new Padding(0, 8, 0, 4) };
        layout.Controls.Add(label, 0, row); layout.SetColumnSpan(label, 2);
    }

    private static void AddRow(TableLayoutPanel layout, string caption, Control input, int row)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private async Task CreateLoanAsync()
    {
        var codes = _copies.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(_card.Text) || codes.Length == 0)
        { _result.Text = "Hãy nhập số thẻ và ít nhất một mã bản sao."; return; }

        // Không gửi tên thủ thư: API lấy từ phiên đăng nhập.
        var response = await PostAsync("api/loans", new { cardNumber = _card.Text.Trim(), copyCodes = codes });
        if (response is null) return;
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        _result.Text = $"Đã lập phiếu {data.GetProperty("loanCode").GetString()} gồm {data.GetProperty("copies").GetInt32()} cuốn, " +
                       $"thời hạn mượn {data.GetProperty("loanDays").GetInt32()} ngày.";
        _copies.Clear();
    }

    private async Task ReturnAsync()
    {
        if (string.IsNullOrWhiteSpace(_returnCopy.Text)) { _result.Text = "Hãy nhập mã bản sao cần trả."; return; }
        var condition = ((ConditionItem)_condition.SelectedItem!).Value;
        var response = await PostAsync("api/returns", new { copyCode = _returnCopy.Text.Trim(), condition });
        if (response is null) return;
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        _result.Text = $"Đã nhận trả. Trễ {data.GetProperty("overdueDays").GetInt32()} ngày, " +
                       $"tiền phạt {data.GetProperty("fineAmount").GetDecimal():N0}, trạng thái bản sao: {data.GetProperty("status").GetString()}.";
        _returnCopy.Clear();
    }

    // Gửi POST; lỗi nghiệp vụ/kết nối hiển thị lên khung kết quả, trả null nếu không thành công.
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
                _ => await ReadErrorAsync(response)
            };
            return null;
        }
        catch (HttpRequestException ex) { _result.Text = "Không kết nối được API: " + ex.Message; return null; }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try { return JsonDocument.Parse(body).RootElement.GetProperty("error").GetString() ?? body; }
        catch { return $"Lỗi {(int)response.StatusCode}: {body}"; }
    }
}

/// <summary>Hộp thoại nhập một dòng văn bản.</summary>
internal sealed class PromptForm : Form
{
    private readonly TextBox _input = new() { Dock = DockStyle.Top };
    public string Value => _input.Text.Trim();

    public PromptForm(string title, string message, string initial)
    {
        Text = title; Width = 520; Height = 170; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        _input.Text = initial;
        var ok = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 90 };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 90 };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 40 }; buttons.Controls.AddRange([cancel, ok]);
        var label = new Label { Text = message, Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 6, 0, 0) };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        content.Controls.Add(_input); content.Controls.Add(label);
        Controls.Add(content); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
    }
}
