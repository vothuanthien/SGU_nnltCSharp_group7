namespace Library.Domain;

/// <summary>
/// Tài khoản nhân viên được phép đăng nhập vào ứng dụng quản trị.
/// Role hiện dùng các giá trị Admin và Librarian.
/// </summary>
public sealed class Account
{
    public int AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Librarian";
    public bool IsActive { get; set; } = true;
}
