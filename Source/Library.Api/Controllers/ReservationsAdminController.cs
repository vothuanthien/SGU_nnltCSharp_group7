using Library.Api.Data;
using Library.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/admin/reservations")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class ReservationsAdminController(DbFactory db, ILogger<ReservationsAdminController> logger) : ControllerBase
{
    public sealed record StatusRequest(string? Status);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        await using var connection = db.Create();
        await connection.OpenAsync();
        await ExpireHoldsAsync(connection);
        const string sql = """
            SELECT r.ReservationId,r.Status,r.RequestedAt,r.HoldUntil,p.ReaderCode,p.CardNumber,p.FullName,
                   b.BookId,b.Title,c.CopyCode
            FROM Reservations r JOIN Readers p ON p.ReaderId=r.ReaderId
            JOIN BookTitles b ON b.BookId=r.BookId
            LEFT JOIN BookCopies c ON c.CopyId=r.HeldCopyId
            WHERE (@status IS NULL OR r.Status=@status)
            ORDER BY CASE r.Status WHEN 'Pending' THEN 0 WHEN 'Ready' THEN 1 ELSE 2 END,r.RequestedAt
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim();
        var rows = new List<object>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new { reservationId=reader.GetInt32(0),status=reader.GetString(1),requestedAt=reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm"),
                holdUntil=reader.IsDBNull(3)?null:reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm"),readerCode=reader.GetString(4),
                cardNumber=reader.GetString(5),fullName=reader.GetString(6),bookId=reader.GetInt32(7),title=reader.GetString(8),
                copyCode=reader.IsDBNull(9)?null:reader.GetString(9) });
        return Ok(rows);
    }

    internal static async Task ExpireHoldsAsync(SqlConnection connection)
    {
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using (var release = new SqlCommand("""
                UPDATE c SET Status='Available'
                FROM BookCopies c JOIN Reservations r ON r.HeldCopyId=c.CopyId
                WHERE r.Status='Ready' AND r.HoldUntil<SYSDATETIME() AND c.Status='Reserved'
                """, connection, transaction)) await release.ExecuteNonQueryAsync();
            await using (var expire = new SqlCommand("UPDATE Reservations SET Status='Expired',HeldCopyId=NULL,HoldUntil=NULL WHERE Status='Ready' AND HoldUntil<SYSDATETIME()", connection, transaction))
                await expire.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusRequest request)
    {
        var requested = request.Status?.Trim();
        if (requested is not ("Ready" or "Cancelled" or "Fulfilled"))
            return BadRequest(new { error="Trạng thái chỉ nhận Ready, Cancelled hoặc Fulfilled." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int readerId, bookId;
            string current, cardNumber;
            int? heldCopyId;
            await using (var find = new SqlCommand(
                "SELECT ReaderId,BookId,Status,HeldCopyId FROM Reservations WITH (UPDLOCK,HOLDLOCK) WHERE ReservationId=@id", connection, tx))
            {
                find.Parameters.Add("@id", SqlDbType.Int).Value = id;
                await using var r = await find.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await r.ReadAsync()) return NotFound(new { error="Không tìm thấy yêu cầu đặt trước." });
                readerId=r.GetInt32(0); bookId=r.GetInt32(1); current=r.GetString(2); heldCopyId=r.IsDBNull(3)?null:r.GetInt32(3);
            }
            await using (var findReader = new SqlCommand("SELECT CardNumber,Status,ExpiryDate FROM Readers WHERE ReaderId=@id", connection, tx))
            {
                findReader.Parameters.Add("@id", SqlDbType.Int).Value = readerId;
                await using var r = await findReader.ExecuteReaderAsync(CommandBehavior.SingleRow);
                await r.ReadAsync(); cardNumber=r.GetString(0);
                if (requested == "Fulfilled" && (r.GetString(1)!="Active" || r.GetDateTime(2).Date<DateTime.Today))
                    return Conflict(new { error="Thẻ bạn đọc đang khóa hoặc đã hết hạn; không thể hoàn tất đặt trước." });
            }

            if (requested == "Ready")
            {
                if (current != "Pending") return Conflict(new { error="Chỉ yêu cầu Pending mới được chuyển sang Ready." });
                int copyId;
                await using (var pick = new SqlCommand(
                    "SELECT TOP(1) CopyId FROM BookCopies WITH (UPDLOCK,HOLDLOCK) WHERE BookId=@book AND Status='Available' ORDER BY CopyId", connection, tx))
                {
                    pick.Parameters.Add("@book", SqlDbType.Int).Value=bookId;
                    var value=await pick.ExecuteScalarAsync();
                    if (value is null) return Conflict(new { error="Chưa có bản sao sẵn có để giữ cho yêu cầu này." });
                    copyId=Convert.ToInt32(value);
                }
                await using (var hold = new SqlCommand("UPDATE BookCopies SET Status='Reserved' WHERE CopyId=@copy AND Status='Available'", connection, tx))
                {
                    hold.Parameters.Add("@copy", SqlDbType.Int).Value=copyId;
                    if (await hold.ExecuteNonQueryAsync()!=1) return Conflict(new { error="Bản sao vừa được xử lý bởi yêu cầu khác." });
                }
                await using var update = new SqlCommand("UPDATE Reservations SET Status='Ready',HeldCopyId=@copy,HoldUntil=DATEADD(day,3,SYSDATETIME()) WHERE ReservationId=@id", connection, tx);
                update.Parameters.Add("@copy", SqlDbType.Int).Value=copyId; update.Parameters.Add("@id", SqlDbType.Int).Value=id;
                await update.ExecuteNonQueryAsync();
            }
            else if (requested == "Cancelled")
            {
                if (current is not ("Pending" or "Ready")) return Conflict(new { error="Chỉ yêu cầu Pending hoặc Ready mới được hủy." });
                if (heldCopyId.HasValue)
                {
                    await using var release = new SqlCommand("UPDATE BookCopies SET Status='Available' WHERE CopyId=@copy AND Status='Reserved'", connection, tx);
                    release.Parameters.Add("@copy", SqlDbType.Int).Value=heldCopyId.Value;
                    await release.ExecuteNonQueryAsync();
                }
                await using var update = new SqlCommand("UPDATE Reservations SET Status='Cancelled',HeldCopyId=NULL,HoldUntil=NULL WHERE ReservationId=@id", connection, tx);
                update.Parameters.Add("@id", SqlDbType.Int).Value=id; await update.ExecuteNonQueryAsync();
            }
            else
            {
                if (current!="Ready" || !heldCopyId.HasValue) return Conflict(new { error="Chỉ yêu cầu Ready có bản sao đang giữ mới được hoàn tất." });
                var policy=await LoanPolicyStore.LoadAsync(connection,tx);
                int openLoans;
                await using (var count = new SqlCommand("SELECT COUNT(*) FROM LoanDetails d JOIN Loans l ON l.LoanId=d.LoanId WHERE l.ReaderId=@reader AND d.ReturnedAt IS NULL",connection,tx))
                { count.Parameters.Add("@reader",SqlDbType.Int).Value=readerId; openLoans=Convert.ToInt32(await count.ExecuteScalarAsync()); }
                try { policy.ValidateRequest(openLoans,1); }
                catch(InvalidOperationException ex) { return Conflict(new { error=ex.Message }); }

                var loanCode=$"PM{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10,99)}";
                int loanId;
                await using (var insertLoan=new SqlCommand("INSERT INTO Loans(LoanCode,ReaderId,StaffUsername,LoanDate) OUTPUT INSERTED.LoanId VALUES(@code,@reader,@staff,SYSDATETIME())",connection,tx))
                {
                    insertLoan.Parameters.Add("@code",SqlDbType.NVarChar,40).Value=loanCode;
                    insertLoan.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;
                    insertLoan.Parameters.Add("@staff",SqlDbType.NVarChar,80).Value=User.Identity?.Name ?? "staff";
                    loanId=Convert.ToInt32(await insertLoan.ExecuteScalarAsync());
                }
                await using (var detail=new SqlCommand("INSERT INTO LoanDetails(LoanId,CopyId,DueDate) VALUES(@loan,@copy,DATEADD(day,@days,CAST(SYSDATETIME() AS date)))",connection,tx))
                {
                    detail.Parameters.Add("@loan",SqlDbType.Int).Value=loanId; detail.Parameters.Add("@copy",SqlDbType.Int).Value=heldCopyId.Value;
                    detail.Parameters.Add("@days",SqlDbType.Int).Value=policy.LoanDays; await detail.ExecuteNonQueryAsync();
                }
                await using (var updateCopy=new SqlCommand("UPDATE BookCopies SET Status='Borrowed' WHERE CopyId=@copy AND Status='Reserved'",connection,tx))
                {
                    updateCopy.Parameters.Add("@copy",SqlDbType.Int).Value=heldCopyId.Value;
                    if(await updateCopy.ExecuteNonQueryAsync()!=1) throw new InvalidOperationException("Bản sao giữ trước không còn hợp lệ.");
                }
                await using var complete=new SqlCommand("UPDATE Reservations SET Status='Fulfilled',HeldCopyId=NULL,HoldUntil=NULL WHERE ReservationId=@id",connection,tx);
                complete.Parameters.Add("@id",SqlDbType.Int).Value=id; await complete.ExecuteNonQueryAsync();
            }

            var staff=User.Identity?.Name??"staff";
            var notificationMessage=requested switch
            {
                "Ready" => "Đặt trước đã sẵn sàng. Thư viện giữ sách cho bạn trong 3 ngày.",
                "Fulfilled" => "Yêu cầu đặt trước đã hoàn tất và được chuyển thành phiếu mượn.",
                _ => "Yêu cầu đặt trước đã được hủy."
            };
            await ReaderNotificationStore.AddAsync(connection,tx,readerId,"Reservation",notificationMessage);
            await ReaderNotificationStore.AuditAsync(connection,tx,staff,$"Reservation.{requested}","Reservation",id.ToString());

            await tx.CommitAsync();
            return NoContent();
        }
        catch(InvalidOperationException ex)
        {
            await tx.RollbackAsync(); logger.LogInformation(ex,"Không thể xử lý đặt trước {ReservationId}",id);
            return Conflict(new { error=ex.Message });
        }
        catch(Exception ex)
        {
            await tx.RollbackAsync(); logger.LogError(ex,"Không thể cập nhật đặt trước {ReservationId}",id); throw;
        }
    }
}
