namespace Library.Admin.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Chỉ mở màn hình quản trị sau khi API xác thực tài khoản và cấp phiên hợp lệ.
        using var login = new LoginForm();
        if (login.ShowDialog() == DialogResult.OK && login.Token is not null)
            Application.Run(new MainForm(login.Token, login.Username));
    }
}
