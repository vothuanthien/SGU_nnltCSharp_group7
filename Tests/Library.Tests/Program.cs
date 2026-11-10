using Library.Api.Security;
using Library.Domain;

// Bộ kiểm tra dạng console (chưa dùng xUnit). Chạy: dotnet run --project Tests/Library.Tests
var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) { Console.WriteLine($"FAIL: {name}"); Environment.Exit(1); }
    passed++; Console.WriteLine($"  ok  {name}");
}
void Throws<T>(string name, Action action) where T : Exception
{
    try { action(); }
    catch (T) { passed++; Console.WriteLine($"  ok  {name}"); return; }
    Console.WriteLine($"FAIL: {name} (không có ngoại lệ {typeof(T).Name})"); Environment.Exit(1);
}

Console.WriteLine("LoanPolicy");
var policy = new LoanPolicy(14, 5, 1000m);
policy.ValidateRequest(3, 2);
Check("cho phép mượn đúng giới hạn (3 + 2 = 5)", true);
Throws<InvalidOperationException>("từ chối vượt giới hạn (4 + 2 > 5)", () => policy.ValidateRequest(4, 2));
Throws<InvalidOperationException>("từ chối phiếu không có cuốn nào", () => policy.ValidateRequest(0, 0));
Throws<InvalidOperationException>("từ chối cấu hình LoanDays <= 0", () => new LoanPolicy(0, 5, 1000m).ValidateRequest(0, 1));
Throws<InvalidOperationException>("từ chối cấu hình MaxLoans <= 0", () => new LoanPolicy(14, 0, 1000m).ValidateRequest(0, 1));
Throws<InvalidOperationException>("từ chối mức phạt âm", () => new LoanPolicy(14, 5, -1m).ValidateRequest(0, 1));
Check("trễ 3 ngày", policy.OverdueDays(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4)) == 3);
Check("trả đúng hạn: 0 ngày trễ", policy.OverdueDays(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)) == 0);
Check("trả sớm: không âm", policy.OverdueDays(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1)) == 0);
Check("phạt 3 ngày = 3000", policy.Fine(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4)) == 3000m);
Check("trả đúng hạn: phạt 0", policy.Fine(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)) == 0m);

Console.WriteLine("PasswordHashing");
var hash = PasswordHashing.Hash("Mat-khau-thu-123");
Check("đúng mật khẩu được chấp nhận", PasswordHashing.Verify("Mat-khau-thu-123", hash));
Check("sai mật khẩu bị từ chối", !PasswordHashing.Verify("sai-mat-khau", hash));
Check("hai lần băm cùng mật khẩu cho kết quả khác nhau (salt riêng)", hash != PasswordHashing.Hash("Mat-khau-thu-123"));
Check("hash rác không gây ngoại lệ", !PasswordHashing.Verify("x", "khong-phai-hash"));
Check("hash demo trong 06_CreateDemoAdmin.sql hợp lệ",
    PasswordHashing.Verify("Admin@12345", "PBKDF2-SHA256$210000$XyqcfR5LY4Cl2fIMezFG7g==$4lHJC4BcUsZZ0U4HeWhkL5MZVzbTok5NUmIpe5DABys="));

Console.WriteLine("AdminSessionStore");
var store = new AdminSessionStore();
var (token, session) = store.Create("thuthu1", "Librarian");
Check("token hợp lệ tra được phiên", store.TryGet(token, out var found) && found.Username == "thuthu1" && found.Role == "Librarian");
Check("token lạ bị từ chối", !store.TryGet("token-gia", out _));
Check("token không chứa username/role", !token.Contains("thuthu1") && !token.Contains("Librarian"));
store.Revoke(token);
Check("đăng xuất hủy phiên", !store.TryGet(token, out _));

Console.WriteLine($"Passed: {passed} kiểm tra.");
Console.WriteLine("Lưu ý: chưa có kiểm thử tích hợp với SQL Server/API; xem Documents/KiemThu.md.");
