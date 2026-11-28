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
var sessionRepository = new InMemoryAdminSessionRepository();
var store = new AdminSessionStore(sessionRepository);
var (token, _) = await store.CreateAsync("thuthu1", "Librarian");
var found = await store.TryGetAsync(token);
Check("token hợp lệ tra được phiên", found is { Username: "thuthu1", Role: "Librarian" });
Check("phiên dùng chung vẫn đọc được từ một store instance khác",
    await new AdminSessionStore(sessionRepository).TryGetAsync(token) is not null);
Check("kho chỉ nhận hash token, không nhận token gốc",
    !token.Contains("thuthu1") && !token.Contains("Librarian") && !sessionRepository.ContainsRawToken(token));
Check("token lạ bị từ chối", await store.TryGetAsync("token-gia") is null);
await store.RevokeAsync(token);
Check("đăng xuất hủy phiên", await store.TryGetAsync(token) is null);
var (readerToken, _) = await store.CreateAsync("THE0001", "Reader", readerId: 42);
var readerSession = await store.TryGetAsync(readerToken);
Check("phiên bạn đọc giữ ReaderId", readerSession?.ReaderId == 42);
Check("phiên bạn đọc giữ role Reader", readerSession?.Role == "Reader");
var (lockedToken, _) = await store.CreateAsync("thuthu-bi-khoa", "Librarian");
var (activeToken, _) = await store.CreateAsync("thuthu-hoat-dong", "Librarian");
await store.RevokeUserSessionsAsync("THUTHU-BI-KHOA");
Check("khóa tài khoản thu hồi các phiên đang tồn tại", await store.TryGetAsync(lockedToken) is null);
Check("thu hồi phiên không ảnh hưởng tài khoản khác", await store.TryGetAsync(activeToken) is not null);
await store.RevokeAsync(readerToken);
await store.RevokeAsync(activeToken);

Console.WriteLine($"Passed: {passed} kiểm tra.");
Console.WriteLine("Lưu ý: chương trình kiểm thử console không thay thế kiểm thử tích hợp SQL Server/API.");

sealed class InMemoryAdminSessionRepository : IAdminSessionRepository
{
    private readonly Dictionary<string, AdminSession> _sessions = new(StringComparer.Ordinal);

    public Task SaveAsync(byte[] tokenHash, AdminSession session, CancellationToken cancellationToken)
    {
        _sessions[Convert.ToHexString(tokenHash)] = session;
        return Task.CompletedTask;
    }

    public Task<AdminSession?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        _sessions.TryGetValue(Convert.ToHexString(tokenHash), out var session);
        return Task.FromResult(session);
    }

    public Task DeleteAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        _sessions.Remove(Convert.ToHexString(tokenHash));
        return Task.CompletedTask;
    }

    public Task DeleteUserSessionsAsync(string username, CancellationToken cancellationToken)
    {
        foreach (var key in _sessions
            .Where(x => x.Value.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .ToArray())
            _sessions.Remove(key);
        return Task.CompletedTask;
    }

    public bool ContainsRawToken(string token) => _sessions.ContainsKey(token);
}
