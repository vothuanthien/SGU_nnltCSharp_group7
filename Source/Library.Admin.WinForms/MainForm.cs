using System.Data;
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
        new("copies", "Bản sao", ["copyCode","bookId","shelfLocation","acquiredAt","status"], ["Mã bản sao","Mã đầu sách","Vị trí kệ","Ngày nhập (yyyy-MM-dd)","Trạng thái"], ["text","int","text","date","text"]),
        new("readers", "Bạn đọc", ["readerCode","cardNumber","fullName","email","phone","registeredAt","expiryDate","status"], ["Mã bạn đọc","Số thẻ","Họ tên","Email","Điện thoại","Ngày đăng ký (yyyy-MM-dd)","Ngày hết hạn (yyyy-MM-dd)","Trạng thái"], ["text","text","text","text","text","date","date","text"]),
        new("genres", "Thể loại", ["name","isActive"], ["Tên thể loại","Đang hoạt động"], ["text","bool"]),
        new("authors", "Tác giả", ["fullName"], ["Họ tên tác giả"], ["text"]),
        new("publishers", "Nhà xuất bản", ["name"], ["Tên nhà xuất bản"], ["text"])
    ];

    private readonly HttpClient _api = new() { BaseAddress = ApiSettings.BaseUri };
    private readonly ComboBox _entity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _search = new() { Width = 240, PlaceholderText = "Từ khóa tìm kiếm" };
    private readonly ComboBox _genreFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly ToolStripStatusLabel _status = new("Sẵn sàng") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly string _role;
    private readonly string _username;
    private bool _logoutRequested;

    public MainForm(string token, string username, string role)
    {
        _role = role;
        _username = username;
        // Gắn token vào header mặc định để mọi API quản trị được xác thực ở phía máy chủ.
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Text = $"Quản trị thư viện · {username} ({role})"; Width = 1280; Height = 800; MinimumSize = new Size(980, 620); StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 248, 246);
        _entity.Items.AddRange(Entities.Cast<object>().ToArray()); _entity.DisplayMember = "Caption"; _entity.SelectedIndex = 0;
        _genreFilter.Items.AddRange(["Tất cả thể loại"]);
        _genreFilter.SelectedIndex = 0;
        var heading = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(18, 12, 18, 8), BackColor = Color.White };
        heading.Controls.Add(new Label { Text = "Không gian quản trị", AutoSize = true, Location = new Point(18, 10), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(24, 50, 75) });
        heading.Controls.Add(new Label { Text = "Quản lý danh mục, bạn đọc và nghiệp vụ mượn trả", AutoSize = true, Location = new Point(20, 43), ForeColor = Color.DimGray });
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 102, Padding = new Padding(14, 12, 14, 10), WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(239, 244, 241) };
        _entity.Width = 160; _search.Width = 200;
        var toolbarControls = new List<Control>
        {
            Button("Trang chủ", OpenDashboardAsync),
            new Label { Text = "Phân hệ:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) }, _entity,
            new Label { Text = "Tìm sách / ISBN / mã cuốn:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) }, _search, _genreFilter,
            Button("Tìm kiếm", async () => await LoadRowsAsync()), Button("Làm mới", async () => await LoadRowsAsync()),
            Button("Thêm mới", async () => await CreateAsync()), Button("Chỉnh sửa", async () => await UpdateAsync()),
            Button("Ngừng hoạt động", async () => await DeactivateAsync()), Button("Xóa vĩnh viễn", async () => await DeleteAsync()),
            Button("Tác giả của sách", async () => await EditAuthorsAsync()),
            Button("Mượn / Trả", OpenCirculationAsync), Button("Báo cáo", OpenReportsAsync)
        };
        var logout = Button("Đăng xuất", LogoutAsync);
        logout.BackColor = Color.FromArgb(255, 242, 239);
        logout.ForeColor = Color.FromArgb(150, 55, 45);
        toolbarControls.Add(logout);
        if (_role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            toolbarControls.Add(Button("Tài khoản", () => { using var form = new AccountsForm(_api); form.ShowDialog(this); return Task.CompletedTask; }));
        toolbar.Controls.AddRange(toolbarControls.ToArray());
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(231, 240, 235);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(24, 50, 75);
        _grid.EnableHeadersVisualStyles = false;
        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = BackColor };
        gridHost.Controls.Add(_grid);
        var statusBar = new StatusStrip { BackColor = Color.White };
        statusBar.Items.Add(_status);
        Controls.Add(gridHost); Controls.Add(toolbar); Controls.Add(heading); Controls.Add(statusBar);
        _entity.SelectedIndexChanged += async (_, _) => await LoadRowsAsync();
        _genreFilter.SelectedIndexChanged += async (_, _) => { if (Current.Key == "books") await LoadRowsAsync(); };
        _search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadRowsAsync(); } };
        Load += async (_, _) => await LoadRowsAsync();
        Load += async (_, _) => await LoadGenresAsync();
        FormClosing += async (_, _) =>
        {
            if (!_logoutRequested) await RevokeSessionAsync();
            _api.Dispose();
        };
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += async (_, _) => await action(); return button;
    }

    private Entity Current => (Entity)_entity.SelectedItem!;

    private async Task LogoutAsync()
    {
        if (MessageBox.Show("Bạn muốn đăng xuất khỏi phiên làm việc hiện tại?", "Xác nhận đăng xuất",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _logoutRequested = true;
        await RevokeSessionAsync();
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task RevokeSessionAsync()
    {
        try
        {
            using var response = await _api.PostAsync("api/auth/logout", null);
            if (!response.IsSuccessStatusCode)
                MessageBox.Show("Không xác nhận được việc thu hồi phiên. Hãy đóng ứng dụng nếu cần kết thúc phiên ngay.",
                    "Đăng xuất chưa xác nhận", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (HttpRequestException)
        {
            MessageBox.Show("Không kết nối được dịch vụ để xác nhận đăng xuất. Phiên sẽ tự hết hạn theo chính sách.",
                "Không kết nối được dịch vụ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private Task OpenDashboardAsync()
    {
        using var form = new DashboardForm(_api, _username, _role, OpenModule);
        form.ShowDialog(this);
        return Task.CompletedTask;
    }

    private Task OpenCirculationAsync()
    {
        using var form = new CirculationForm(_api);
        form.ShowDialog(this);
        return Task.CompletedTask;
    }

    private Task OpenReportsAsync()
    {
        using var form = new DashboardForm(_api, _username, _role, OpenModule, reportsOnly: true);
        form.ShowDialog(this);
        return Task.CompletedTask;
    }

    private void OpenModule(string key)
    {
        var index = Array.FindIndex(Entities, entity => entity.Key == key);
        if (index >= 0) _entity.SelectedIndex = index;
    }

    // Đọc bản ghi của danh mục đang chọn và gắn vào bảng tra cứu.
    private async Task LoadRowsAsync()
    {
        try
        {
            _status.Text = "Đang tải dữ liệu...";
            var url = $"api/admin/catalog/{Current.Key}?q={Uri.EscapeDataString(_search.Text.Trim())}";
            if (Current.Key == "books" && _genreFilter.SelectedItem is GenreOption genre && genre.Id.HasValue)
                url += $"&genreId={genre.Id.Value}";
            var rows = await _api.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>(url) ?? [];
            _grid.DataSource = ToTable(rows);   // DataTable để lưới hiển thị đúng từng cột (không bind trực tiếp Dictionary)
            if (Current.Key == "books") ConfigureBookGrid();
            else _genreFilter.Enabled = false;
            _status.Text = rows.Count == 0 ? "Không tìm thấy dữ liệu phù hợp" : $"Sẵn sàng · {rows.Count} bản ghi";
        }
        catch (HttpRequestException) { _status.Text = "Không kết nối được dịch vụ"; MessageBox.Show("Không thể tải dữ liệu từ API. Kiểm tra kết nối rồi thử lại.", "Không tải được dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        catch (System.Text.Json.JsonException) { _status.Text = "Dữ liệu phản hồi không hợp lệ"; MessageBox.Show("Dịch vụ trả về dữ liệu không đúng định dạng. Vui lòng thử lại sau.", "Lỗi dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private sealed record GenreOption(int? Id, string Name)
    {
        public override string ToString() => Name;
    }

    private void ConfigureBookGrid()
    {
        _genreFilter.Enabled = true;
        if (_grid.Columns.Contains("id")) _grid.Columns["id"].HeaderText = "Mã đầu sách";
        SetColumn("title", "Tên sách"); SetColumn("isbn", "ISBN"); SetColumn("authors", "Tác giả");
        SetColumn("genre", "Thể loại"); SetColumn("totalCopies", "Tổng cuốn");
        SetColumn("borrowedCopies", "Đang mượn"); SetColumn("availableCopies", "Còn sẵn");
        foreach (var hidden in new[] { "description", "genreId", "publisherId", "publisher", "publishedYear", "isActive" })
            if (_grid.Columns.Contains(hidden)) _grid.Columns[hidden].Visible = false;
        foreach (var column in _grid.Columns.Cast<DataGridViewColumn>())
            if (column.Visible) column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void SetColumn(string key, string title)
    {
        if (_grid.Columns.Contains(key)) _grid.Columns[key].HeaderText = title;
    }

    private async Task LoadGenresAsync()
    {
        try
        {
            var rows = await _api.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>("api/admin/catalog/genres") ?? [];
            var options = new List<GenreOption> { new(null, "Tất cả thể loại") };
            foreach (var row in rows)
            {
                if (row.TryGetValue("id", out var id) && id.TryGetInt32(out var key) &&
                    row.TryGetValue("name", out var name) && name.ValueKind == JsonValueKind.String)
                    options.Add(new GenreOption(key, name.GetString() ?? ""));
            }
            _genreFilter.DataSource = options;
            _genreFilter.SelectedIndex = 0;
        }
        catch (HttpRequestException)
        {
            _status.Text = "Không tải được bộ lọc thể loại";
        }
    }

    // Mở biểu mẫu nhập, gửi POST và tải lại danh sách sau khi tạo thành công.
    private async Task CreateAsync()
    {
        using var editor = new RecordEditorForm(_api, Current, null);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        await SendAsync(() => _api.PostAsJsonAsync($"api/admin/catalog/{Current.Key}", editor.Values), "Đã thêm bản ghi.");
    }

    // Lấy ID đang chọn, mở biểu mẫu đã điền dữ liệu rồi gửi PUT cập nhật.
    private async Task UpdateAsync()
    {
        if (!SelectedId(out var id, out var current)) return;
        using var editor = new RecordEditorForm(_api, Current, current);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        await SendAsync(() => _api.PutAsJsonAsync($"api/admin/catalog/{Current.Key}/{id}", editor.Values), "Đã cập nhật bản ghi.");
    }

    // Ngừng phục vụ nhưng giữ hồ sơ và lịch sử nghiệp vụ.
    private async Task DeactivateAsync()
    {
        if (Current.Key is not ("books" or "copies" or "readers" or "genres"))
        {
            MessageBox.Show("Danh mục này không hỗ trợ ngừng hoạt động. Hãy dùng Xóa vĩnh viễn nếu cần.",
                "Không hỗ trợ thao tác", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!SelectedId(out var id, out _)) return;

        var label = Current.Key == "readers" ? "bạn đọc" : "bản ghi";
        if (MessageBox.Show($"Ngừng hoạt động {label} đang chọn? Hồ sơ và lịch sử sẽ được giữ lại.",
                "Xác nhận ngừng hoạt động", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        await SendAsync(() => _api.PutAsync($"api/admin/catalog/{Current.Key}/{id}/deactivate", null),
            "Đã ngừng hoạt động bản ghi.");
    }

    // Xóa cứng bản ghi; các khóa ngoại sẽ chặn xóa nếu bản ghi đã có lịch sử tham chiếu.
    private async Task DeleteAsync()
    {
        if (!SelectedId(out var id, out _)) return;
        if (MessageBox.Show("XÓA VĨNH VIỄN bản ghi đang chọn? Thao tác này không thể hoàn tác.",
                "Xác nhận xóa vĩnh viễn", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        await SendAsync(() => _api.DeleteAsync($"api/admin/catalog/{Current.Key}/{id}/permanent"),
            "Đã xóa vĩnh viễn bản ghi.");
    }

    // Kiểm tra phản hồi HTTP và hiển thị thông báo nghiệp vụ trả về từ API.
    private async Task SendAsync(Func<Task<HttpResponseMessage>> request, string success)
    {
        try
        {
            using var response = await request();
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadApiErrorAsync(response);
                MessageBox.Show(message, "Không thể lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _status.Text = success; await LoadRowsAsync();
        }
        catch (HttpRequestException) { MessageBox.Show("Chưa nhận được xác nhận từ dịch vụ. Kiểm tra dữ liệu hiện tại trước khi gửi lại.", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        catch (System.Text.Json.JsonException) { MessageBox.Show("Dịch vụ trả về dữ liệu không hợp lệ. Vui lòng thử lại sau.", "Lỗi dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString() ?? "Không thể hoàn tất thao tác. Vui lòng kiểm tra dữ liệu.";
        }
        catch (JsonException) { }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            System.Net.HttpStatusCode.Forbidden => "Tài khoản không có quyền thực hiện thao tác này.",
            System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity => "Dữ liệu không hợp lệ hoặc đã thay đổi. Vui lòng tải lại và kiểm tra.",
            _ => $"Không thể hoàn tất thao tác (mã HTTP {(int)response.StatusCode})."
        };
    }

    private bool SelectedId(out int id, out Dictionary<string, object?> values)
    {
        id = 0; values = new(StringComparer.OrdinalIgnoreCase);
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView view || !int.TryParse(view.Row["id"]?.ToString(), out id))
        { MessageBox.Show("Hãy chọn một bản ghi trong bảng trước.", "Chưa chọn dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
        foreach (DataColumn column in view.Row.Table.Columns)
            values[column.ColumnName] = view.Row.IsNull(column) ? null : view.Row[column];
        return true;
    }

    // Gán danh sách mã tác giả cho đầu sách đang chọn (bảng nối BookAuthors).
    private async Task EditAuthorsAsync()
    {
        if (Current.Key != "books") { MessageBox.Show("Hãy chọn danh mục Sách rồi chọn một đầu sách.", "Gán tác giả", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!SelectedId(out var id, out var current)) return;
        try
        {
            var title = current.GetValueOrDefault("title")?.ToString() ?? $"#{id}";
            using var form = new BookAuthorsForm(_api, id, title);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _status.Text = "Đã cập nhật tác giả cho sách.";
                await LoadRowsAsync();
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static DataTable ToTable(List<Dictionary<string, JsonElement>> rows)
    {
        var table = new DataTable();
        foreach (var key in rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase)) table.Columns.Add(key, typeof(string));
        foreach (var row in rows)
        {
            var item = table.NewRow();
            foreach (var pair in row)
            {
                var text = Display(pair.Value)?.ToString();
                item[pair.Key] = text is null ? DBNull.Value : text.EndsWith("T00:00:00") ? text[..^9] : text;
            }
            table.Rows.Add(item);
        }
        return table;
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
    private sealed record LookupOption(int? Id, string Name);

    private readonly HttpClient _api;
    private readonly MainForm.Entity _entity;
    private readonly Dictionary<string, Control> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Button _saveButton = new() { Text = "Lưu", AutoSize = true, DialogResult = DialogResult.None };
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RecordEditorForm(HttpClient api, MainForm.Entity entity, Dictionary<string, object?>? initial)
    {
        _api = api;
        _entity = entity; Text = initial is null ? $"Thêm {entity.Caption}" : $"Sửa {entity.Caption}";
        Width = 560; Height = Math.Min(720, 170 + entity.Fields.Length * 56); StartPosition = FormStartPosition.CenterParent;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < entity.Fields.Length; i++)
        {
            var field = entity.Fields[i]; var label = new Label { Text = entity.Labels[i], AutoSize = true, Anchor = AnchorStyles.Left };
            Control input = field is "genreId" or "publisherId"
                ? new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }
                : entity.Types[i] == "bool" ? new CheckBox { Text = "Có", AutoSize = true } : new TextBox { Dock = DockStyle.Fill };
            var current = initial is not null && initial.TryGetValue(field, out var value) ? value : null;
            if (input is CheckBox check) check.Checked = current is bool b ? b : current?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? (field == "isActive");
            if (input is TextBox text) text.Text = current?.ToString() ?? (field is "registeredAt" or "expiryDate" ? DateTime.Today.ToString("yyyy-MM-dd") : "");
            _inputs[field] = input; panel.Controls.Add(label, 0, i); panel.Controls.Add(input, 1, i);
        }
        _saveButton.Enabled = !entity.Fields.Any(field => field is "genreId" or "publisherId");
        _saveButton.Click += (_, _) => { if (CollectValues()) { DialogResult = DialogResult.OK; Close(); } };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        panel.Controls.Add(_saveButton, 0, entity.Fields.Length); panel.Controls.Add(cancel, 1, entity.Fields.Length);
        Controls.Add(panel); AcceptButton = _saveButton; CancelButton = cancel;
        Load += async (_, _) => await LoadCatalogOptionsAsync(initial);
    }

    private async Task LoadCatalogOptionsAsync(Dictionary<string, object?>? initial)
    {
        foreach (var field in new[] { "genreId", "publisherId" })
        {
            if (_inputs.GetValueOrDefault(field) is not ComboBox combo) continue;
            var currentId = ReadNullableInt(initial?.GetValueOrDefault(field));
            var resource = field == "genreId" ? "genres" : "publishers";
            var nameProperty = "name";
            var options = new List<LookupOption> { new(null, "— Không chọn —") };
            try
            {
                var rows = await _api.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>($"api/admin/catalog/{resource}") ?? [];
                foreach (var row in rows)
                {
                    if (!row.TryGetValue("id", out var idValue) || !idValue.TryGetInt32(out var id) ||
                        !row.TryGetValue(nameProperty, out var nameValue)) continue;
                    if (row.TryGetValue("isActive", out var activeValue) && activeValue.ValueKind == JsonValueKind.False) continue;
                    options.Add(new LookupOption(id, nameValue.GetString() ?? $"#{id}"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không tải được danh sách { (field == "genreId" ? "thể loại" : "nhà xuất bản") } từ API. " +
                    (currentId.HasValue ? "Giá trị hiện tại được giữ nguyên; bạn vẫn có thể sửa các trường khác." : "Bạn vẫn có thể lưu các trường khác.") +
                    $"\n\nChi tiết: {ex.Message}", "Không tải được danh mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (currentId.HasValue && options.All(option => option.Id != currentId))
                options.Add(new LookupOption(currentId, $"#{currentId} (giá trị hiện tại)"));
            combo.DisplayMember = nameof(LookupOption.Name);
            combo.ValueMember = nameof(LookupOption.Id);
            combo.DataSource = options;
            if (currentId.HasValue) combo.SelectedValue = currentId.Value;
            else combo.SelectedIndex = 0;
        }
        _saveButton.Enabled = true;
    }

    private static int? ReadNullableInt(object? value) =>
        value is not null && int.TryParse(value.ToString(), out var id) ? id : null;

    // Chuyển nội dung nhập thành JSON kiểu đúng; ngày/năm/số nguyên được kiểm tra trước khi gửi.
    private bool CollectValues()
    {
        Values.Clear();
        for (var i = 0; i < _entity.Fields.Length; i++)
        {
            var key = _entity.Fields[i]; var input = _inputs[key];
            if (input is CheckBox check) { Values[key] = check.Checked; continue; }
            if (input is ComboBox combo)
            {
                Values[key] = combo.SelectedValue is int selectedId ? selectedId : null;
                continue;
            }
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
            else
            {
                if (_entity.Key == "readers" && key == "phone" &&
                    (text.Length != 10 || text.Any(character => character is < '0' or > '9')))
                    return Invalid("Số điện thoại phải gồm đúng 10 chữ số.");
                Values[key] = text;
            }
        }
        return true;
    }

    private bool Invalid(string message) { MessageBox.Show(message, "Kiểm tra dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
}
