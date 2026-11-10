using Library.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>Lập phiếu mượn. Chỉ Admin/Librarian; tên thủ thư lấy từ phiên đã xác thực, không nhận từ client.</summary>
[ApiController]
[Route("api/loans")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class LoansController(DbFactory db, ILogger<LoansController> logger) : ControllerBase
{
    public sealed record CreateLoan(string? CardNumber, string[]? CopyCodes);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoan request)
    {
        var staff = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(staff)) return Unauthorized(new { error = "Vui lòng đăng nhập quản trị." });
        if (string.IsNullOrWhiteSpace(request.CardNumber))
            return BadRequest(new { error = "Vui lòng nhập số thẻ bạn đọc." });

        var copyCodes = (request.CopyCodes ?? [])
            .Select(code => code?.Trim())
            .Where(code => !string.IsNullOrEmpty(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>().ToArray();
        if (copyCodes.Length == 0)
            return BadRequest(new { error = "Phiếu mượn phải có ít nhất một bản sao." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var readerId = await FindActiveReaderAsync(connection, tx, request.CardNumber.Trim());
            var policy = await LoanPolicyStore.LoadAsync(connection, tx);
            var openLoans = await CountOpenLoansAsync(connection, tx, readerId);
            policy.ValidateRequest(openLoans, copyCodes.Length);   // quy tắc nghiệp vụ nằm ở lớp Domain

            var loanCode = $"PM{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10, 99)}";
            int loanId;
            await using (var insert = new SqlCommand(
                "INSERT INTO Loans(LoanCode, ReaderId, StaffUsername, LoanDate) OUTPUT INSERTED.LoanId " +
                "VALUES(@code, @reader, @staff, SYSDATETIME())", connection, tx))
            {
                insert.Parameters.AddWithValue("@code", loanCode);
                insert.Parameters.AddWithValue("@reader", readerId);
                insert.Parameters.AddWithValue("@staff", staff);
                loanId = Convert.ToInt32(await insert.ExecuteScalarAsync());
            }

            foreach (var code in copyCodes)
            {
                // Khóa bản sao và đổi trạng thái trong một câu lệnh để hai người không mượn cùng một cuốn.
                await using var lockCopy = new SqlCommand(
                    "UPDATE BookCopies WITH (UPDLOCK, HOLDLOCK) SET Status = 'Borrowed' OUTPUT INSERTED.CopyId " +
                    "WHERE CopyCode = @code AND Status = 'Available'", connection, tx);
                lockCopy.Parameters.AddWithValue("@code", code);
                var copyId = await lockCopy.ExecuteScalarAsync()
                    ?? throw new InvalidOperationException($"Bản sao {code} không tồn tại hoặc không sẵn sàng.");

                await using var detail = new SqlCommand(
                    "INSERT INTO LoanDetails(LoanId, CopyId, DueDate) " +
                    "VALUES(@loan, @copy, DATEADD(day, @days, CAST(SYSDATETIME() AS date)))", connection, tx);
                detail.Parameters.AddWithValue("@loan", loanId);
                detail.Parameters.AddWithValue("@copy", copyId);
                detail.Parameters.AddWithValue("@days", policy.LoanDays);
                await detail.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { loanId, loanCode, copies = copyCodes.Length, loanDays = policy.LoanDays });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Lập phiếu mượn thất bại, thẻ {Card}", request.CardNumber);
            throw;   // middleware toàn cục trả 500 { error }
        }
    }

    private static async Task<int> FindActiveReaderAsync(SqlConnection c, SqlTransaction tx, string cardNumber)
    {
        await using var command = new SqlCommand(
            "SELECT ReaderId FROM Readers WHERE CardNumber = @card AND Status = 'Active' " +
            "AND ExpiryDate >= CAST(SYSDATETIME() AS date)", c, tx);
        command.Parameters.AddWithValue("@card", cardNumber);
        var value = await command.ExecuteScalarAsync();
        return value is null ? throw new InvalidOperationException("Thẻ bạn đọc không hợp lệ hoặc đã hết hạn.") : Convert.ToInt32(value);
    }

    private static async Task<int> CountOpenLoansAsync(SqlConnection c, SqlTransaction tx, int readerId)
    {
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM LoanDetails d JOIN Loans l ON l.LoanId = d.LoanId " +
            "WHERE l.ReaderId = @reader AND d.ReturnedAt IS NULL", c, tx);
        command.Parameters.AddWithValue("@reader", readerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
