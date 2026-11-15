namespace Library.Domain;

/// <summary>
/// Tài khoản nhân viên được phép đăng nhập vào ứng dụng quản trị.
/// Role hiện dùng các giá trị Admin và Librarian.
/// </summary>
public sealed class Account
{
    /// <summary>Gets or sets the account identifier.</summary>
    public int AccountId { get; set; }

    /// <summary>Gets or sets the unique sign-in name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the PBKDF2 password hash; never expose it to clients.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the account holder's display name.</summary>
    public string? FullName { get; set; }

    /// <summary>Gets or sets the account's creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the authorization role.</summary>
    public string Role { get; set; } = "Librarian";

    /// <summary>Gets or sets whether the account may authenticate.</summary>
    public bool IsActive { get; set; } = true;
}
