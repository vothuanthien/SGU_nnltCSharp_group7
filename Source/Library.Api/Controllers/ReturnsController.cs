using Library.Api.Data;
using Library.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>Nhận trả sách. Chỉ Admin/Librarian; tên thủ thư lấy từ phiên đã xác thực.</summary>
[ApiController]
[Route("api/returns")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class ReturnsController(DbFactory db, ILogger<ReturnsController> logger) : ControllerBase
{
    public sealed record ReturnRequest(string? CopyCode, string? Condition, decimal CompensationAmount = 0);

    private static readonly string[] Conditions = ["Normal", "Damaged", "Lost"];

    [HttpPost]
    public async Task<IActionResult> Return([FromBody] ReturnRequest request)
    {
        var staff = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(staff)) return Unauthorized(new { error = "Vui lòng đăng nhập quản trị." });
        if (string.IsNullOrWhiteSpace(request.CopyCode) || request.Condition is null || !Conditions.Contains(request.Condition))
            return BadRequest(new { error = "Mã bản sao hoặc tình trạng sách không hợp lệ (Normal, Damaged, Lost)." });
        if (request.CompensationAmount < 0 || (request.Condition == "Normal" && request.CompensationAmount != 0) ||
            (request.Condition is "Damaged" or "Lost" && request.CompensationAmount <= 0))
            return BadRequest(new { error = "Sách hỏng hoặc mất cần nhập phí sửa chữa/thay thế lớn hơn 0; sách bình thường không có phí bồi thường." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int detailId, copyId; DateOnly due, today;
            await using (var find = new SqlCommand(
                "SELECT TOP(1) d.LoanDetailId, d.CopyId, d.DueDate, CAST(SYSDATETIME() AS date) " +
                "FROM LoanDetails d JOIN BookCopies b ON b.CopyId = d.CopyId " +
                "WHERE b.CopyCode = @code AND d.ReturnedAt IS NULL ORDER BY d.LoanDetailId", connection, tx))
            {
                find.Parameters.AddWithValue("@code", request.CopyCode.Trim());
                await using var reader = await find.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("Không tìm thấy lượt mượn đang mở cho mã cuốn này.");
                detailId = reader.GetInt32(0);
                copyId = reader.GetInt32(1);
                due = DateOnly.FromDateTime(reader.GetDateTime(2));
                today = DateOnly.FromDateTime(reader.GetDateTime(3));   // ngày theo đồng hồ SQL Server
            }

            var policy = await LoanPolicyStore.LoadAsync(connection, tx);
            var overdueDays = policy.OverdueDays(due, today);
            var overdueFine = policy.Fine(due, today);
            var compensationAmount = request.CompensationAmount;
            var fine = overdueFine + compensationAmount;
            var copyStatus = request.Condition == "Normal" ? "Available" : request.Condition;

            await using (var close = new SqlCommand(
                "UPDATE LoanDetails SET ReturnedAt = SYSDATETIME() WHERE LoanDetailId = @id AND ReturnedAt IS NULL", connection, tx))
            {
                close.Parameters.AddWithValue("@id", detailId);
                if (await close.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("Chi tiết mượn đã được xử lý trả.");
            }

            int returnId;
            await using (var insert = new SqlCommand(
                "INSERT INTO [Returns](LoanDetailId, ReturnedAt, Condition, OverdueDays, OverdueFine, CompensationAmount, FineAmount, StaffUsername) " +
                "OUTPUT INSERTED.ReturnId VALUES(@detail, SYSDATETIME(), @condition, @days, @overdueFine, @compensation, @fine, @staff)", connection, tx))
            {
                insert.Parameters.AddWithValue("@detail", detailId);
                insert.Parameters.AddWithValue("@condition", request.Condition);
                insert.Parameters.AddWithValue("@days", overdueDays);
                insert.Parameters.Add(new SqlParameter("@overdueFine", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = overdueFine });
                insert.Parameters.Add(new SqlParameter("@compensation", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = compensationAmount });
                insert.Parameters.Add(new SqlParameter("@fine", SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = fine });
                insert.Parameters.AddWithValue("@staff", staff);
                returnId = Convert.ToInt32(await insert.ExecuteScalarAsync());
            }

            await using (var update = new SqlCommand("UPDATE BookCopies SET Status = @status WHERE CopyId = @copy", connection, tx))
            {
                update.Parameters.AddWithValue("@status", copyStatus);
                update.Parameters.AddWithValue("@copy", copyId);
                await update.ExecuteNonQueryAsync();
            }

            await ReaderNotificationStore.AuditAsync(connection,tx,staff,$"Return.{request.Condition}","Return",returnId.ToString());

            await tx.CommitAsync();
            return Ok(new { returnId, overdueDays, overdueFine, compensationAmount, fineAmount = fine, status = copyStatus });
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Nhận trả thất bại, mã cuốn {Copy}", request.CopyCode);
            throw;
        }
    }
}
