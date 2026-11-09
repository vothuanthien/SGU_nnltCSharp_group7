using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

/// <summary>Không gian quản trị dữ liệu; mọi yêu cầu gửi bearer token để API xác minh quyền.</summary>
public sealed class MainForm : Form
{
    internal sealed record Entity(string Key, string Caption, string[] Fields, string[] Labels, string[] Types);
    private static readonly Entity[] Entities =
    [
        new("books", "Sách", ["title","isbn","publishedYear","description","genreId","publisherId","isActive"], ["Tên sách","ISBN","Năm XB","Mô tả","Mã thể loại","Mã NXB","Đang hoạt động"], ["text","text","int","text","int","int","bool"]),
        new("readers", "Bạn đọc", ["readerCode","cardNumber","fullName","email","phone","registeredAt","expiryDate","status"], ["Mã bạn đọc","Số thẻ","Họ tên","Email","Điện thoại","Ngày đăng ký (yyyy-MM-dd)","Ngày hết hạn (yyyy-MM-dd)","Trạng thái"], ["text","text","text","text","text","date","date","text"]),
        new("genres", "Thể loại", ["name","isActive"], ["Tên thể loại","Đang hoạt động"], ["text","bool"]),
        new("authors", "Tác giả", ["fullName"], ["Họ tên tác giả"], ["text"]),
        new("publishers", "Nhà xuất bản", ["name"], ["Tên nhà xuất bản"], ["text"])
    ];

    private readonly HttpClient _api = new() { BaseAddress = new Uri("http://localhost:5001/") };
    private readonly ComboBox _entity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _search = new() { Width = 240, PlaceholderText = "Từ khóa tìm kiếm" };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly ToolStripStatusLabel _status = new("Sẵn sàng");

    public MainForm(string token, string username)
    {
        // Gắn token vào header mặc định để mọi API quản trị được xác thực ở phía máy chủ.
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Text = $"Quản trị thư viện · {username}"; Width = 1180; Height = 730; StartPosition = FormStartPosition.CenterScreen;
        _entity.Items.AddRange(Entities.Cast<object>().ToArray()); _entity.DisplayMember = "Caption"; _entity.SelectedIndex = 0;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10), WrapContents = false };
        toolbar.Controls.AddRange([new Label { Text = "Quản lý:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) }, _entity, _search,
            Button("Tìm kiếm", async () => await LoadRowsAsync()), Button("Tải lại", async () => await LoadRowsAsync()),
            Button("Thêm", async () => await CreateAsync()), Button("Sửa", async () => await UpdateAsync()), Button("Xóa", async () => await DeleteAsync())]);
        var statusBar = new StatusStrip(); statusBar.Items.Add(_status);
        Controls.Add(_grid); Controls.Add(toolbar); Controls.Add(statusBar);
        _entity.SelectedIndexChanged += async (_, _) => await LoadRowsAsync();
        _search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadRowsAsync(); } };
        Load += async (_, _) => await LoadRowsAsync();
        FormClosing += async (_, _) => { try { await _api.PostAsync("api/auth/logout", null); } catch { /* Phiên vẫn tự hết hạn sau 8 giờ. */ } _api.Dispose(); };
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += async (_, _) => await action(); return button;
    }

    private Entity Current => (Entity)_entity.SelectedItem!;

    // Đọc bản ghi của danh mục đang chọn và gắn vào bảng tra cứu.
    private async Task LoadRowsAsync()
    {
        try
        {
            _status.Text = "Đang tải dữ liệu...";
            var url = $"api/admin/catalog/{Current.Key}?q={Uri.EscapeDataString(_search.Text.Trim())}";
            var rows = await _api.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>(url) ?? [];
            _grid.DataSource = rows.Select(row => row.ToDictionary(pair => pair.Key, pair => Display(pair.Value))).ToList();
            _status.Text = $"Đã tải {rows.Count} bản ghi";
        }
        catch (Exception ex) { _status.Text = "Không tải được dữ liệu"; MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // Mở biểu mẫu nhập, gửi POST và tải lại danh sách sau khi tạo thành công.
    private async Task CreateAsync()
    {
        using var editor = new RecordEditorForm(Current, null);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        await SendAsync(() => _api.PostAsJsonAsync($"api/admin/catalog/{Current.Key}", editor.Values), "Đã thêm bản ghi.");
    }

    // Lấy ID đang chọn, mở biểu mẫu đã điền dữ liệu rồi gửi PUT cập nhật.
    private async Task UpdateAsync()
    {
        if (!SelectedId(out var id, out var current)) return;
        using var editor = new RecordEditorForm(Current, current);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        await SendAsync(() => _api.PutAsJsonAsync($"api/admin/catalog/{Current.Key}/{id}", editor.Values), "Đã cập nhật bản ghi.");
    }

    // Hỏi xác nhận trước khi xóa; sách, thể loại và bạn đọc được ngừng hoạt động để giữ lịch sử.
    private async Task DeleteAsync()
    {
        if (!SelectedId(out var id, out _)) return;
        if (MessageBox.Show("Xóa/ngừng hoạt động bản ghi đang chọn?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await SendAsync(() => _api.DeleteAsync($"api/admin/catalog/{Current.Key}/{id}"), "Đã xử lý xóa bản ghi.");
    }

    // Kiểm tra phản hồi HTTP và hiển thị thông báo nghiệp vụ trả về từ API.
    private async Task SendAsync(Func<Task<HttpResponseMessage>> request, string success)
    {
        try
        {
            using var response = await request();
            if (!response.IsSuccessStatusCode) { var body = await response.Content.ReadAsStringAsync(); MessageBox.Show(body, "Không thể lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            _status.Text = success; await LoadRowsAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private bool SelectedId(out int id, out Dictionary<string, object?> values)
    {
        id = 0; values = new(StringComparer.OrdinalIgnoreCase);
        if (_grid.CurrentRow?.DataBoundItem is not Dictionary<string, object?> row || !row.TryGetValue("id", out var raw) || !int.TryParse(raw?.ToString(), out id))
        { MessageBox.Show("Hãy chọn một bản ghi trong bảng trước.", "Chưa chọn dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
        values = row; return true;
    }

    private static object? Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null, JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.String => value.GetString(), JsonValueKind.Number when value.TryGetInt32(out var number) => number,
        _ => value.ToString()
    };
}

/// <summary>Biểu mẫu tạo/cập nhật dùng chung; kiểu dữ liệu mỗi trường khai báo theo danh mục.</summary>
internal sealed class RecordEditorForm : Form
{
    private readonly MainForm.Entity _entity;
    private readonly Dictionary<string, Control> _inputs = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RecordEditorForm(MainForm.Entity entity, Dictionary<string, object?>? initial)
    {
        _entity = entity; Text = initial is null ? $"Thêm {entity.Caption}" : $"Sửa {entity.Caption}";
        Width = 560; Height = Math.Min(720, 170 + entity.Fields.Length * 56); StartPosition = FormStartPosition.CenterParent;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < entity.Fields.Length; i++)
        {
            var field = entity.Fields[i]; var label = new Label { Text = entity.Labels[i], AutoSize = true, Anchor = AnchorStyles.Left };
            Control input = entity.Types[i] == "bool" ? new CheckBox { Text = "Có", AutoSize = true } : new TextBox { Dock = DockStyle.Fill };
            var current = initial is not null && initial.TryGetValue(field, out var value) ? value : null;
            if (input is CheckBox check) check.Checked = current is bool b ? b : current?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? (field == "isActive");
            if (input is TextBox text) text.Text = current?.ToString() ?? (field is "registeredAt" or "expiryDate" ? DateTime.Today.ToString("yyyy-MM-dd") : "");
            _inputs[field] = input; panel.Controls.Add(label, 0, i); panel.Controls.Add(input, 1, i);
        }
        var save = new Button { Text = "Lưu", AutoSize = true, DialogResult = DialogResult.None };
        save.Click += (_, _) => { if (CollectValues()) { DialogResult = DialogResult.OK; Close(); } };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        panel.Controls.Add(save, 0, entity.Fields.Length); panel.Controls.Add(cancel, 1, entity.Fields.Length);
        Controls.Add(panel); AcceptButton = save; CancelButton = cancel;
    }

    // Chuyển nội dung nhập thành JSON kiểu đúng; ngày/năm/số nguyên được kiểm tra trước khi gửi.
    private bool CollectValues()
    {
        Values.Clear();
        for (var i = 0; i < _entity.Fields.Length; i++)
        {
            var key = _entity.Fields[i]; var input = _inputs[key];
            if (input is CheckBox check) { Values[key] = check.Checked; continue; }
            var text = ((TextBox)input).Text.Trim();
            if (text.Length == 0) { Values[key] = null; continue; }
            if (_entity.Types[i] == "int")
            {
                if (!int.TryParse(text, out var number)) return Invalid(_entity.Labels[i] + " phải là số nguyên.");
                Values[key] = number;
            }
            else if (_entity.Types[i] == "date")
            {
                if (!DateTime.TryParse(text, out var date)) return Invalid(_entity.Labels[i] + " không hợp lệ.");
                Values[key] = date.ToString("yyyy-MM-dd");
            }
            else Values[key] = text;
        }
        return true;
    }

    private bool Invalid(string message) { MessageBox.Show(message, "Kiểm tra dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
}
