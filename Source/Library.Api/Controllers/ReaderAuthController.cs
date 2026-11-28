using Library.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController, Route("api/readers/auth")]
public sealed class ReaderAuthController(DbFactory db, AdminSessionStore sessions) : ControllerBase
{
    public sealed record RegisterRequest(string? CardNumber, string? Username, string? Email, string? Password);
    public sealed record LoginRequest(string? Username, string? Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var card = request.CardNumber?.Trim(); var username = request.Username?.Trim(); var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(card) || card.Length > 30 || string.IsNullOrWhiteSpace(username) || username.Length > 80 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 254 || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
            return BadRequest(new { error = "Nhập đúng số thẻ, tên đăng nhập, email và mật khẩu tối thiểu 10 ký tự." });
        await using var c = db.Create(); await c.OpenAsync();
        int readerId;
        await using (var check = new SqlCommand("SELECT ReaderId FROM Readers WHERE CardNumber=@card AND Email=@email AND Status='Active' AND ExpiryDate>=CAST(SYSDATETIME() AS date)", c))
        {
            check.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = card;
            check.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
            var value = await check.ExecuteScalarAsync();
            if (value is null) return BadRequest(new { error = "Không xác minh được thẻ và email đang lưu tại thư viện. Hãy liên hệ thủ thư để cập nhật thông tin." });
            readerId = Convert.ToInt32(value);
        }
        try
        {
            await using var insert = new SqlCommand("INSERT INTO ReaderAccounts(ReaderId,Username,PasswordHash) VALUES(@id,@user,@hash)", c);
            insert.Parameters.Add("@id", SqlDbType.Int).Value = readerId;
            insert.Parameters.Add("@user", SqlDbType.NVarChar, 80).Value = username;
            insert.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = PasswordHashing.Hash(request.Password);
            await insert.ExecuteNonQueryAsync();
            return Ok(new { message = "Tạo tài khoản thành công. Bạn có thể đăng nhập." });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Conflict(new { error = "Thẻ hoặc tên đăng nhập đã được liên kết với tài khoản." }); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password)) return BadRequest(new { error = "Nhập tên đăng nhập và mật khẩu." });
        await using var c = db.Create(); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT a.PasswordHash,r.CardNumber,r.ReaderId FROM ReaderAccounts a JOIN Readers r ON r.ReaderId=a.ReaderId WHERE a.Username=@u AND a.IsActive=1 AND r.Status='Active' AND r.ExpiryDate>=CAST(SYSDATETIME() AS date)", c);
        cmd.Parameters.Add("@u", SqlDbType.NVarChar, 80).Value = request.Username.Trim();
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync() || !PasswordHashing.Verify(request.Password, reader.GetString(0))) return Unauthorized(new { error = "Tên đăng nhập hoặc mật khẩu không đúng." });
        var card = reader.GetString(1); var readerId = reader.GetInt32(2);
        var (token, session) = await sessions.CreateAsync(card, "Reader", readerId, HttpContext.RequestAborted);
        return Ok(new { token, username = request.Username.Trim(), expiresAt = session.ExpiresAt });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            await sessions.RevokeAsync(header[7..].Trim(), HttpContext.RequestAborted);
        return NoContent();
    }
}
