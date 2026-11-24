using Library.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/admin/payments")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class PaymentsController(DbFactory db, ILogger<PaymentsController> logger) : ControllerBase
{
    public sealed record PaymentRequest(decimal Amount);

    [HttpGet("outstanding")]
    public async Task<IActionResult> Outstanding()
    {
        await using var connection=db.Create();
        await connection.OpenAsync();
        const string sql="""
            SELECT ret.ReturnId,r.CardNumber,r.FullName,b.Title,c.CopyCode,ret.FineAmount,
                   ISNULL(ret.OverdueFine,ret.FineAmount),ISNULL(ret.CompensationAmount,0),ISNULL(p.Paid,0),ret.ReturnedAt
            FROM [Returns] ret
            JOIN LoanDetails d ON d.LoanDetailId=ret.LoanDetailId JOIN Loans l ON l.LoanId=d.LoanId
            JOIN Readers r ON r.ReaderId=l.ReaderId JOIN BookCopies c ON c.CopyId=d.CopyId JOIN BookTitles b ON b.BookId=c.BookId
            OUTER APPLY(SELECT SUM(Amount) Paid FROM FinePayments fp WHERE fp.ReturnId=ret.ReturnId) p
            WHERE ret.FineAmount>ISNULL(p.Paid,0) ORDER BY ret.ReturnedAt DESC
            """;
        var rows=new List<object>();
        await using var command=new SqlCommand(sql,connection);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())
        {
            var fine=reader.GetDecimal(5); var overdueFine=reader.GetDecimal(6);
            var compensation=reader.GetDecimal(7); var paid=reader.GetDecimal(8);
            rows.Add(new { returnId=reader.GetInt32(0),cardNumber=reader.GetString(1),fullName=reader.GetString(2),
                title=reader.GetString(3),copyCode=reader.GetString(4),fineAmount=fine,overdueFine,compensationAmount=compensation,
                paidAmount=paid,balance=fine-paid,returnedAt=reader.GetDateTime(9).ToString("yyyy-MM-dd HH:mm") });
        }
        return Ok(rows);
    }

    [HttpPost("{returnId:int}")]
    public async Task<IActionResult> Record(int returnId,[FromBody] PaymentRequest request)
    {
        if(returnId<=0 || request.Amount<=0) return BadRequest(new { error="Số tiền thanh toán phải lớn hơn 0." });
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            decimal fine,paid;int readerId;
            await using(var find=new SqlCommand("SELECT l.ReaderId,ret.FineAmount,ISNULL((SELECT SUM(Amount) FROM FinePayments WITH (UPDLOCK,HOLDLOCK) WHERE ReturnId=ret.ReturnId),0) FROM [Returns] ret WITH (UPDLOCK,HOLDLOCK) JOIN LoanDetails d ON d.LoanDetailId=ret.LoanDetailId JOIN Loans l ON l.LoanId=d.LoanId WHERE ret.ReturnId=@id",connection,tx))
            {
                find.Parameters.Add("@id",SqlDbType.Int).Value=returnId;
                await using var reader=await find.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if(!await reader.ReadAsync()) return NotFound(new { error="Không tìm thấy khoản phạt." });
                readerId=reader.GetInt32(0);fine=reader.GetDecimal(1);paid=reader.GetDecimal(2);
            }
            var balance=fine-paid;
            if(balance<=0) return Conflict(new { error="Khoản phạt này đã được thanh toán đủ." });
            if(request.Amount>balance) return BadRequest(new { error=$"Số tiền vượt số còn phải thu ({balance:N0} đ)." });
            await using(var insert=new SqlCommand("INSERT INTO FinePayments(ReturnId,Amount,StaffUsername) VALUES(@return,@amount,@staff)",connection,tx))
            {
                insert.Parameters.Add("@return",SqlDbType.Int).Value=returnId;
                insert.Parameters.Add(new SqlParameter("@amount",SqlDbType.Decimal){Precision=12,Scale=2,Value=request.Amount});
                insert.Parameters.Add("@staff",SqlDbType.NVarChar,80).Value=User.Identity?.Name ?? "staff";
                await insert.ExecuteNonQueryAsync();
            }
            await ReaderNotificationStore.AddAsync(connection,tx,readerId,"FinePayment",$"Đã ghi nhận thanh toán {request.Amount:N0} đ. Số dư khoản phạt còn {balance-request.Amount:N0} đ.");
            await ReaderNotificationStore.AuditAsync(connection,tx,User.Identity?.Name??"staff","FinePayment.Recorded","Return",returnId.ToString());
            await tx.CommitAsync();
            return Ok(new { returnId,paidAmount=request.Amount,balanceAfter=balance-request.Amount });
        }
        catch(Exception ex)
        {
            await tx.RollbackAsync(); logger.LogError(ex,"Không thể ghi thanh toán cho khoản phạt {ReturnId}",returnId); throw;
        }
    }
}
