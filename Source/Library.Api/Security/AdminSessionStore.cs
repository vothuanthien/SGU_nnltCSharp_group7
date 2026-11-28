using System.Security.Cryptography;
using System.Text;

namespace Library.Api.Security;

public interface IAdminSessionRepository
{
    Task SaveAsync(byte[] tokenHash, AdminSession session, CancellationToken cancellationToken);
    Task<AdminSession?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken);
    Task DeleteAsync(byte[] tokenHash, CancellationToken cancellationToken);
    Task DeleteUserSessionsAsync(string username, CancellationToken cancellationToken);
}

/// <summary>Quản lý phiên bearer có thời hạn; dữ liệu phiên được lưu trong kho dùng chung.</summary>
public sealed class AdminSessionStore(IAdminSessionRepository repository)
{
    public const string ContextItemKey = "Library.AdminSession";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>Tạo bearer token ngẫu nhiên; chỉ hash token được gửi tới kho phiên.</summary>
    public async Task<(string Token, AdminSession Session)> CreateAsync(
        string username, string role, int? readerId = null, CancellationToken cancellationToken = default)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var session = new AdminSession(username, role, DateTimeOffset.UtcNow.Add(SessionLifetime), readerId);
        await repository.SaveAsync(HashToken(token), session, cancellationToken);
        return (token, session);
    }

    /// <summary>Trả về phiên còn hạn; phiên hết hạn bị xóa khỏi kho.</summary>
    public async Task<AdminSession?> TryGetAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);
        var session = await repository.FindAsync(tokenHash, cancellationToken);
        if (session is null) return null;
        if (session.ExpiresAt > DateTimeOffset.UtcNow) return session;

        await repository.DeleteAsync(tokenHash, cancellationToken);
        return null;
    }

    public Task RevokeAsync(string token, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(HashToken(token), cancellationToken);

    public Task RevokeUserSessionsAsync(string username, CancellationToken cancellationToken = default) =>
        repository.DeleteUserSessionsAsync(username, cancellationToken);

    private static byte[] HashToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record AdminSession(string Username, string Role, DateTimeOffset ExpiresAt, int? ReaderId = null);

/// <summary>Tạo và kiểm tra hash mật khẩu PBKDF2-SHA256 có salt riêng cho từng tài khoản.</summary>
public static class PasswordHashing
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || !parts[0].Equals("pbkdf2-sha256", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(parts[1], out var iterations) || iterations is < 100_000 or > 1_000_000)
                return false;

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < SaltSize || expected.Length != HashSize) return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
