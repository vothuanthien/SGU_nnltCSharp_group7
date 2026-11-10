using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Library.Api.Security;

public static class SessionAuthenticationDefaults
{
    public const string Scheme = "SessionBearer";
}

/// <summary>
/// Xác thực bearer token phiên lưu trong AdminSessionStore (không phải JWT).
/// Handler dựng ClaimsPrincipal gồm tên và vai trò để [Authorize(Roles=...)] kiểm soát mọi endpoint.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AdminSessionStore sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = header[7..].Trim();
        if (!sessions.TryGet(token, out var session))
            return Task.FromResult(AuthenticateResult.Fail("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

        // Chuẩn hóa vai trò để so khớp chính xác với [Authorize(Roles = "Admin,Librarian")].
        var role = session.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin"
                 : session.Role.Equals("Librarian", StringComparison.OrdinalIgnoreCase) ? "Librarian"
                 : session.Role;
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, session.Username), new Claim(ClaimTypes.Role, role) },
            SessionAuthenticationDefaults.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SessionAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // Chưa đăng nhập hoặc token sai/hết hạn: 401 cùng định dạng lỗi { error }.
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new { error = "Vui lòng đăng nhập quản trị." });
    }

    // Đã đăng nhập nhưng vai trò không đủ quyền: 403.
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new { error = "Tài khoản không có quyền thực hiện thao tác này." });
    }
}
