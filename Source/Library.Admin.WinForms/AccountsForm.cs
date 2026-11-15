using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

/// <summary>Quản lý tài khoản quản trị qua API; không đọc hoặc hiển thị password hash.</summary>
internal sealed class AccountsForm : Form
{
    private sealed record AccountRow(int AccountId, string Username, string? FullName, string Role, bool IsActive, DateTime CreatedAt);

    private readonly HttpClient _api;
    private readonly TextBox _search = new() { Width = 190, PlaceholderText = "Tên đăng nhập hoặc họ tên" };
    private readonly ComboBox _role = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _status = new() { Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false,
        RowHeadersVisible = false
    };
    private readonly ToolStripStatusLabel _message = new("Sẵn sàng");

    public AccountsForm(HttpClient api)
    {
        _api = api;
        Text = "Quản lý tài khoản";
        Width = 980;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;

        _role.Items.AddRange(["Tất cả vai trò", "Admin", "Librarian"]);
        _role.SelectedIndex = 0;
        _status.Items.AddRange(["Mọi trạng thái", "Đang hoạt động", "Đã khóa"]);
        _status.SelectedIndex = 0;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8), WrapContents = false, AutoScroll = true };
        toolbar.Controls.AddRange([
            _search, _role, _status,
            MakeButton("Lọc / Tải lại", async () => await LoadAccountsAsync()),
            MakeButton("Thêm thủ thư", async () => await CreateAccountAsync()),
            MakeButton("Khóa / Kích hoạt", async () => await ToggleStatusAsync()),
            MakeButton("Đặt lại mật khẩu", async () => await ResetPasswordAsync())
        ]);
        var statusBar = new StatusStrip();
        statusBar.Items.Add(_message);
        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(statusBar);
        Load += async (_, _) => await LoadAccountsAsync();
        _search.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadAccountsAsync();
            }
        };
    }

    private static Button MakeButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += async (_, _) => await action();
        return button;
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(_search.Text)) query.Add($"q={Uri.EscapeDataString(_search.Text.Trim())}");
            if (_role.SelectedIndex > 0) query.Add($"role={Uri.EscapeDataString(_role.Text)}");
            if (_status.SelectedIndex > 0) query.Add($"isActive={(_status.SelectedIndex == 1 ? "true" : "false")}");
            var url = "api/admin/accounts" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
            var accounts = await _api.GetFromJsonAsync<List<AccountRow>>(url) ?? [];
            _grid.DataSource = accounts.Select(account => new
            {
                account.AccountId,
                account.Username,
                FullName = string.IsNullOrWhiteSpace(account.FullName) ? "—" : account.FullName,
                account.Role,
                Status = account.IsActive ? "Đang hoạt động" : "Đã khóa",
                CreatedAt = account.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            }).ToList();
            _message.Text = $"Đã tải {accounts.Count} tài khoản";
        }
        catch (Exception ex)
        {
            _message.Text = "Không tải được danh sách tài khoản";
            MessageBox.Show(ex.Message, "Lỗi API", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CreateAccountAsync()
    {
        using var dialog = new CreateAccountDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var response = await _api.PostAsJsonAsync("api/admin/accounts", new
            {
                username = dialog.Username,
                fullName = dialog.FullName,
                password = dialog.Password,
                role = dialog.Role,
                isActive = true
            });
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(await ReadErrorAsync(response), "Không thể tạo tài khoản", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _message.Text = $"Đã tạo tài khoản {dialog.Username}";
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ToggleStatusAsync()
    {
        if (!TrySelected(out var accountId, out var isActive)) return;
        var verb = isActive ? "khóa" : "kích hoạt";
        if (MessageBox.Show($"Bạn muốn {verb} tài khoản đang chọn?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            using var response = await _api.PutAsJsonAsync($"api/admin/accounts/{accountId}/status", new { isActive = !isActive });
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(await ReadErrorAsync(response), "Không thể đổi trạng thái", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _message.Text = $"Đã {verb} tài khoản";
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ResetPasswordAsync()
    {
        if (!TrySelected(out var accountId, out _)) return;
        using var dialog = new NewPasswordDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var response = await _api.PutAsJsonAsync($"api/admin/accounts/{accountId}/reset-password", new { newPassword = dialog.Password });
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(await ReadErrorAsync(response), "Không thể đặt lại mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _message.Text = "Đã đặt lại mật khẩu; các phiên hiện tại của tài khoản đã bị thu hồi.";
            MessageBox.Show(_message.Text, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TrySelected(out int accountId, out bool isActive)
    {
        accountId = 0;
        isActive = false;
        var view = _grid.CurrentRow;
        if (view is null || !int.TryParse(view.Cells[nameof(AccountRow.AccountId)].Value?.ToString(), out accountId))
        {
            MessageBox.Show("Chọn một tài khoản trước.", "Chưa chọn tài khoản", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        isActive = view.Cells["Status"].Value?.ToString() == "Đang hoạt động";
        return true;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try { return JsonDocument.Parse(body).RootElement.GetProperty("error").GetString() ?? body; }
        catch (JsonException) { return body; }
    }
}

internal sealed class CreateAccountDialog : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill, MaxLength = 80 };
    private readonly TextBox _fullName = new() { Dock = DockStyle.Fill, MaxLength = 200 };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox _role = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    public string Username => _username.Text.Trim();
    public string FullName => _fullName.Text.Trim();
    public string Password => _password.Text;
    public string Role => _role.SelectedItem?.ToString() ?? "Librarian";

    public CreateAccountDialog()
    {
        Text = "Tạo tài khoản";
        Width = 470;
        Height = 300;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        _role.Items.AddRange(["Librarian", "Admin"]);
        _role.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "Tên đăng nhập", _username, 0);
        AddRow(layout, "Họ tên", _fullName, 1);
        AddRow(layout, "Mật khẩu ban đầu", _password, 2);
        AddRow(layout, "Vai trò", _role, 3);
        var save = new Button { Text = "Tạo", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (Username.Length == 0 || FullName.Length == 0 || Password.Length < 6)
            {
                MessageBox.Show("Nhập tên đăng nhập, họ tên và mật khẩu ít nhất 6 ký tự.", "Kiểm tra dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, save]);
        layout.Controls.Add(buttons, 1, 4);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel layout, string caption, Control control, int row)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}

internal sealed class NewPasswordDialog : Form
{
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    public string Password => _password.Text;

    public NewPasswordDialog()
    {
        Text = "Đặt lại mật khẩu";
        Width = 420;
        Height = 160;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Mật khẩu mới", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_password, 1, 0);
        var save = new Button { Text = "Đặt lại", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (Password.Length < 6)
            {
                MessageBox.Show("Mật khẩu mới cần ít nhất 6 ký tự.", "Kiểm tra dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, save]);
        layout.Controls.Add(buttons, 1, 1);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
