using System.ComponentModel.DataAnnotations;

namespace Library.Web.Models;

public sealed class ReaderLoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ReaderRegistrationViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập số thẻ.")]
    [StringLength(30)]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(80, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email đã đăng ký tại thư viện.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(256, MinimumLength = 10, ErrorMessage = "Mật khẩu phải có ít nhất 10 ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận chưa khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed record ReaderProfileViewModel(
    string ReaderCode,
    string CardNumber,
    string FullName,
    string? Email,
    string? Phone,
    string ExpiryDate,
    string Status);

public sealed record ReaderLoanViewModel(
    string LoanCode,
    string Title,
    string CopyCode,
    string LoanDate,
    string DueDate,
    string? ReturnedAt,
    bool IsOverdue);

public sealed record ReaderReservationViewModel(
    int ReservationId,
    string BookTitle,
    string RequestedAt,
    string Status,
    string? HoldUntil);

public sealed class ReaderHistoryViewModel
{
    public ReaderProfileViewModel? Profile { get; init; }
    public List<ReaderLoanViewModel> Loans { get; init; } = [];
    public List<ReaderReservationViewModel> Reservations { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
