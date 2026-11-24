using System.Net.Http.Json;

namespace Library.Admin.WinForms;

/// <summary>Màn hình đăng nhập; mật khẩu chỉ gửi qua API và không lưu ở máy khách.</summary>
public sealed class LoginForm : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill, MaxLength = 80, PlaceholderText = "Nhập tên đăng nhập", AccessibleName = "Tên đăng nhập" };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 256, PlaceholderText = "Nhập mật khẩu", AccessibleName = "Mật khẩu" };
    private readonly Label _message = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(360, 0), Text = " " };
    private readonly Button _login = new() { Text = "Đăng nhập", AutoSize = true, MinimumSize = new Size(120, 38) };
    private readonly HttpClient _api = new() { BaseAddress = ApiSettings.BaseUri };

    public string? Token { get; private set; }
    public string? Role { get; private set; }
    public string Username => _username.Text.Trim();

    public LoginForm()
    {
        Text = "Đăng nhập · Thư viện Sài Gòn"; ClientSize = new Size(520, 520); MinimumSize = new Size(520, 520); MaximumSize = new Size(520, 520);
        StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(246, 248, 246);
        var shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28), BackColor = BackColor };
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 28, 30, 24), ColumnCount = 1, RowCount = 9, BackColor = Color.White };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(new Label { Text = "THƯ VIỆN SÀI GÒN", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(40, 112, 93), Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        card.Controls.Add(new Label { Text = "Đăng nhập quản trị", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(24, 50, 75), Font = new Font("Segoe UI", 19, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        card.Controls.Add(new Label { Text = "Dành cho quản trị viên và thủ thư", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        card.Controls.Add(new Label { Text = "Tên đăng nhập", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 3) }, 0, 3);
        card.Controls.Add(_username, 0, 4);
        card.Controls.Add(new Label { Text = "Mật khẩu", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 3) }, 0, 5);
        card.Controls.Add(_password, 0, 6);
        _login.Dock = DockStyle.Fill;
        _login.BackColor = Color.FromArgb(40, 112, 93);
        _login.ForeColor = Color.White;
        _login.FlatStyle = FlatStyle.Flat;
        card.Controls.Add(_login, 0, 7);
        _message.Dock = DockStyle.Fill;
        _message.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_message, 0, 8);
        shell.Controls.Add(card);
        Controls.Add(shell);
        AcceptButton = _login;
        Shown += (_, _) => _username.Focus();
        _login.Click += async (_, _) => await SignInAsync();
    }

    // Gọi endpoint login; API kiểm tra hash và role trước khi trả bearer token.
    private async Task SignInAsync()
    {
        var username = Username;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(_password.Text))
        {
            _message.ForeColor = Color.Firebrick;
            _message.Text = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            if (string.IsNullOrWhiteSpace(username)) _username.Focus(); else _password.Focus();
            return;
        }

        _login.Enabled = false;
        _message.ForeColor = Color.FromArgb(80, 100, 110);
        _message.Text = "Đang xác thực thông tin...";
        try
        {
            using var response = await _api.PostAsJsonAsync("api/auth/login", new { Username = username, Password = _password.Text });
            if (!response.IsSuccessStatusCode)
            {
                _message.ForeColor = Color.Firebrick;
                _message.Text = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "Tên đăng nhập hoặc mật khẩu không đúng."
                    : "Không thể xác thực lúc này. Vui lòng thử lại.";
                return;
            }
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
            {
                _message.ForeColor = Color.Firebrick;
                _message.Text = "Dịch vụ chưa tạo được phiên đăng nhập. Vui lòng thử lại.";
                return;
            }
            Token = result.Token; Role = result.Role; DialogResult = DialogResult.OK; Close();
        }
        catch (HttpRequestException)
        {
            _message.ForeColor = Color.Firebrick;
            _message.Text = "Không kết nối được dịch vụ. Kiểm tra API rồi thử lại.";
        }
        catch (TaskCanceledException)
        {
            _message.ForeColor = Color.Firebrick;
            _message.Text = "Dịch vụ phản hồi quá thời gian. Vui lòng thử lại.";
        }
        finally { _login.Enabled = true; }
    }

    private sealed record LoginResponse(string Token, string Username, string Role, DateTimeOffset ExpiresAt);
}
