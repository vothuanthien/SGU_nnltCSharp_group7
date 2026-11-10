using System.Net.Http.Json;

namespace Library.Admin.WinForms;

/// <summary>Màn hình đăng nhập; mật khẩu chỉ gửi qua API và không lưu ở máy khách.</summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill, Text = "admin" };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Label _message = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _login = new() { Text = "Đăng nhập", AutoSize = true };
    private readonly HttpClient _api = new() { BaseAddress = ApiSettings.BaseUri };

    public string? Token { get; private set; }
    public string Username => _username.Text.Trim();

    public LoginForm()
    {
        Text = "Đăng nhập quản trị thư viện"; Width = 430; Height = 270;
        StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "TÀI KHOẢN QUẢN TRỊ", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        layout.Controls.Add(new Label { Text = "Tài khoản", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); layout.Controls.Add(_username, 1, 1);
        layout.Controls.Add(new Label { Text = "Mật khẩu", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); layout.Controls.Add(_password, 1, 2);
        layout.Controls.Add(_message, 0, 3); layout.SetColumnSpan(_message, 2);
        layout.Controls.Add(_login, 1, 4); _login.Anchor = AnchorStyles.Right;
        Controls.Add(layout); AcceptButton = _login;
        _login.Click += async (_, _) => await SignInAsync();
    }

    // Gọi endpoint login; API kiểm tra hash và role trước khi trả bearer token.
    private async Task SignInAsync()
    {
        _login.Enabled = false; _message.Text = "Đang xác thực...";
        try
        {
            var response = await _api.PostAsJsonAsync("api/auth/login", new { Username = Username, Password = _password.Text });
            if (!response.IsSuccessStatusCode) { _message.Text = "Sai tài khoản/mật khẩu hoặc tài khoản chưa có quyền quản trị."; return; }
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null || string.IsNullOrWhiteSpace(result.Token)) { _message.Text = "API không trả về phiên đăng nhập."; return; }
            Token = result.Token; DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { _message.Text = "Không kết nối được API: " + ex.Message; }
        finally { _login.Enabled = true; }
    }

    private sealed record LoginResponse(string Token, string Username, string Role, DateTimeOffset ExpiresAt);
}
