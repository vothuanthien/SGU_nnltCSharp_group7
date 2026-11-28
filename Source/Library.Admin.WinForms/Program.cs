namespace Library.Admin.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Giữ ứng dụng chạy xuyên suốt các phiên; đăng xuất đưa người dùng về màn hình đăng nhập.
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK || login.Token is null)
                return;

            using var admin = new MainForm(login.Token, login.Username, login.Role ?? "Librarian");
            admin.ShowDialog();
            if (admin.DialogResult != DialogResult.OK)
                return;
        }
    }
}
