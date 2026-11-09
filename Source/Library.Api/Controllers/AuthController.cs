using Library.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>Đăng nhập quản trị, cấp bearer token ngắn hạn và hủy phiên khi đăng xuất.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(DbFactory db, AdminSessionStore sessions) : ControllerBase
{
    public sealed record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { error = "Vui lòng nhập tài khoản và mật khẩu." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT PasswordHash, Role FROM Accounts WHERE Username=@username AND IsActive=1", connection);
        command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = request.Username.Trim();

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync()) return Unauthorized(new { error = "Tài khoản hoặc mật khẩu không đúng." });

        var passwordHash = reader.GetString(0);
        var role = reader.GetString(1);
        if (!PasswordHashing.Verify(request.Password, passwordHash) ||
            !(role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
              role.Equals("Librarian", StringComparison.OrdinalIgnoreCase)))
            return Unauthorized(new { error = "Tài khoản hoặc mật khẩu không đúng." });

        var username = request.Username.Trim();
        var (token, session) = sessions.Create(username, role);
        return Ok(new { token, username, role = session.Role, expiresAt = session.ExpiresAt });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            sessions.Revoke(header[7..].Trim());

        return NoContent();
    }
}
