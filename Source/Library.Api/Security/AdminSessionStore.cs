using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Library.Api.Security;

/// <summary>Lưu phiên quản trị ngắn hạn trong bộ nhớ của API.</summary>
public sealed class AdminSessionStore
{
    public const string ContextItemKey = "Library.AdminSession";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();

    /// <summary>Tạo bearer token ngẫu nhiên, không chứa username hay role bên trong.</summary>
    public (string Token, AdminSession Session) Create(string username, string role)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var session = new AdminSession(username, role, DateTimeOffset.UtcNow.Add(SessionLifetime));
        _sessions[token] = session;
        return (token, session);
    }

    /// <summary>Trả về phiên còn hạn; token hết hạn bị xóa ngay khi được kiểm tra.</summary>
    public bool TryGet(string token, out AdminSession session)
    {
        if (_sessions.TryGetValue(token, out session!))
        {
            if (session.ExpiresAt > DateTimeOffset.UtcNow) return true;
            _sessions.TryRemove(token, out _);
        }

        session = null!;
        return false;
    }

    /// <summary>Hủy phiên khi người dùng đăng xuất.</summary>
    public void Revoke(string token) => _sessions.TryRemove(token, out _);

    /// <summary>Hủy mọi phiên của tài khoản sau khi tài khoản bị khóa hoặc đổi mật khẩu.</summary>
    public void RevokeUserSessions(string username)
    {
        foreach (var entry in _sessions)
        {
            if (entry.Value.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                _sessions.TryRemove(entry.Key, out _);
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record AdminSession(string Username, string Role, DateTimeOffset ExpiresAt);

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
