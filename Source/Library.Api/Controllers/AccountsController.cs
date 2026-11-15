using Library.Api.Data;
using Library.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>
/// Quản lý tài khoản thủ thư và phân quyền.
/// CHỈ DÀNH RIÊNG CHO QUẢN TRỊ VIÊN (Role = Admin).
/// </summary>
[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = "Admin")]
public sealed class AccountsController(
    DbFactory db,
    AdminSessionStore sessions,
    ILogger<AccountsController> logger) : ControllerBase
{
    public sealed record AccountDto(int AccountId, string Username, string? FullName, string Role, bool IsActive, DateTime CreatedAt);
    public sealed record CreateAccountRequest(string? Username, string? FullName, string? Password, string? Role, bool IsActive = true);
    public sealed record UpdateStatusRequest(bool IsActive);
    public sealed record ResetPasswordRequest(string? NewPassword);

    // Lấy danh sách tài khoản (không bao giờ trả về PasswordHash)
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? role, [FromQuery] bool? isActive)
    {
        await using var connection = db.Create();
        await connection.OpenAsync();

        var sql = "SELECT AccountId, Username, FullName, Role, IsActive, CreatedAt FROM Accounts WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(q)) sql += " AND (Username LIKE @term OR FullName LIKE @term)";
        if (!string.IsNullOrWhiteSpace(role)) sql += " AND Role = @role";
        if (isActive.HasValue) sql += " AND IsActive = @active";
        sql += " ORDER BY AccountId ASC";

        await using var command = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(q))
            command.Parameters.Add("@term", SqlDbType.NVarChar, 200).Value = $"%{q.Trim()}%";
        if (!string.IsNullOrWhiteSpace(role))
            command.Parameters.Add("@role", SqlDbType.VarChar, 20).Value = role.Trim();
        if (isActive.HasValue)
            command.Parameters.Add("@active", SqlDbType.Bit).Value = isActive.Value;

        var list = new List<AccountDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AccountDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetDateTime(5)
            ));
        }

        return Ok(list);
    }

    // Tạo tài khoản mới, băm mật khẩu bằng PBKDF2-SHA256
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Vui lòng nhập tên đăng nhập, họ tên và mật khẩu ban đầu." });

        var username = request.Username.Trim();
        var fullName = request.FullName.Trim();
        if (username.Length > 80 || fullName.Length > 200)
            return BadRequest(new { error = "Tên đăng nhập tối đa 80 ký tự; họ tên tối đa 200 ký tự." });
        var role = string.IsNullOrWhiteSpace(request.Role) ? "Librarian" : request.Role.Trim();
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
            !role.Equals("Librarian", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Vai trò chỉ được chấp nhận là 'Admin' hoặc 'Librarian'." });

        if (request.Password.Length < 6)
            return BadRequest(new { error = "Mật khẩu ban đầu phải có tối thiểu 6 ký tự." });

        await using var connection = db.Create();
        await connection.OpenAsync();

        // Kiểm tra trùng username
        await using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Accounts WHERE Username = @username", connection))
        {
            checkCmd.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = username;
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                return Conflict(new { error = $"Tên tài khoản '{username}' đã tồn tại trong hệ thống." });
        }

        var hash = PasswordHashing.Hash(request.Password);
        int accountId;
        DateTime createdAt;
        try
        {
            await using var insertCmd = new SqlCommand(
                "INSERT INTO Accounts(Username, FullName, PasswordHash, Role, IsActive) " +
                "OUTPUT INSERTED.AccountId, INSERTED.CreatedAt VALUES(@u, @name, @h, @r, @a)", connection);
            insertCmd.Parameters.Add("@u", SqlDbType.NVarChar, 80).Value = username;
            insertCmd.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = fullName;
            insertCmd.Parameters.Add("@h", SqlDbType.NVarChar, 500).Value = hash;
            insertCmd.Parameters.Add("@r", SqlDbType.VarChar, 20).Value = role;
            insertCmd.Parameters.Add("@a", SqlDbType.Bit).Value = request.IsActive;
            await using var reader = await insertCmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            accountId = reader.GetInt32(0);
            createdAt = reader.GetDateTime(1);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new { error = $"Tên tài khoản '{username}' đã tồn tại trong hệ thống." });
        }

        logger.LogInformation("Admin {Admin} đã tạo tài khoản {User} (Role={Role})", User.Identity?.Name, username, role);
        return CreatedAtAction(nameof(List), new { id = accountId },
            new AccountDto(accountId, username, fullName, role, request.IsActive, createdAt));
    }

    // Khóa hoặc kích hoạt lại tài khoản
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var currentStaff = User.Identity?.Name;
        await using var connection = db.Create();
        await connection.OpenAsync();

        // Bảo vệ: Không cho phép Admin tự khóa chính mình
        string? targetUsername;
        await using (var checkCmd = new SqlCommand("SELECT Username FROM Accounts WHERE AccountId = @id", connection))
        {
            checkCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            targetUsername = (await checkCmd.ExecuteScalarAsync())?.ToString();
            if (targetUsername is null)
                return NotFound(new { error = "Không tìm thấy tài khoản." });

            if (targetUsername.Equals(currentStaff, StringComparison.OrdinalIgnoreCase) && !request.IsActive)
                return BadRequest(new { error = "Bạn không thể tự khóa tài khoản Admin đang đăng nhập." });
        }

        await using (var updateCmd = new SqlCommand("UPDATE Accounts SET IsActive = @active WHERE AccountId = @id", connection))
        {
            updateCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            updateCmd.Parameters.Add("@active", SqlDbType.Bit).Value = request.IsActive;
            await updateCmd.ExecuteNonQueryAsync();
        }

        if (!request.IsActive && targetUsername is not null)
            sessions.RevokeUserSessions(targetUsername);

        logger.LogInformation("Admin {Admin} đã thay đổi trạng thái tài khoản Id={Id} thành IsActive={Active}",
            currentStaff, id, request.IsActive);

        return NoContent();
    }

    // Đặt lại mật khẩu tài khoản
    [HttpPut("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { error = "Mật khẩu mới phải có tối thiểu 6 ký tự." });

        await using var connection = db.Create();
        await connection.OpenAsync();

        var newHash = PasswordHashing.Hash(request.NewPassword);
        string? targetUsername;
        await using (var cmd = new SqlCommand(
            "UPDATE Accounts SET PasswordHash = @hash OUTPUT INSERTED.Username WHERE AccountId = @id", connection))
        {
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = newHash;
            targetUsername = (await cmd.ExecuteScalarAsync())?.ToString();
        }

        if (targetUsername is null)
            return NotFound(new { error = "Không tìm thấy tài khoản." });

        sessions.RevokeUserSessions(targetUsername);

        logger.LogInformation("Admin {Admin} đã đặt lại mật khẩu cho tài khoản Id={Id}", User.Identity?.Name, id);
        return NoContent();
    }
}
