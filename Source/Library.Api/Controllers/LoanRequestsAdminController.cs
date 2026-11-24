using Library.Api.Data;
using Library.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/admin/loan-requests")]
[Authorize(Roles="Admin,Librarian")]
public sealed class LoanRequestsAdminController(DbFactory db,ILogger<LoanRequestsAdminController> logger):ControllerBase
{
    public sealed record DecisionRequest(string? Status,string? RejectionReason);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string? status)
    {
        await using var connection=db.Create();
        await connection.OpenAsync();
        const string sql="""
            SELECT q.LoanRequestId,q.Status,q.RequestedAt,r.ReaderCode,r.CardNumber,r.FullName,b.BookId,b.Title,
                   (SELECT COUNT(*) FROM BookCopies c WHERE c.BookId=b.BookId AND c.Status='Available') AS AvailableCopies,
                   q.RejectionReason,c.CopyCode,l.LoanCode
            FROM LoanRequests q JOIN Readers r ON r.ReaderId=q.ReaderId JOIN BookTitles b ON b.BookId=q.BookId
            LEFT JOIN BookCopies c ON c.CopyId=q.CopyId LEFT JOIN Loans l ON l.LoanId=q.LoanId
            WHERE (@status IS NULL OR q.Status=@status)
            ORDER BY CASE q.Status WHEN 'Requested' THEN 0 ELSE 1 END,q.RequestedAt
            """;
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.Add("@status",SqlDbType.VarChar,20).Value=string.IsNullOrWhiteSpace(status)?DBNull.Value:status.Trim();
        var rows=new List<object>();
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())
            rows.Add(new { requestId=reader.GetInt32(0),status=reader.GetString(1),requestedAt=reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm"),
                readerCode=reader.GetString(3),cardNumber=reader.GetString(4),fullName=reader.GetString(5),bookId=reader.GetInt32(6),title=reader.GetString(7),
                availableCopies=reader.GetInt32(8),rejectionReason=reader.IsDBNull(9)?null:reader.GetString(9),
                copyCode=reader.IsDBNull(10)?null:reader.GetString(10),loanCode=reader.IsDBNull(11)?null:reader.GetString(11) });
        return Ok(rows);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Decide(int id,[FromBody]DecisionRequest request)
    {
        var status=request.Status?.Trim();
        if(status is not ("Approved" or "Rejected"))return BadRequest(new{error="Trạng thái yêu cầu chỉ nhận Approved hoặc Rejected."});
        var reason=request.RejectionReason?.Trim();
        if(reason?.Length>300)return BadRequest(new{error="Lý do từ chối tối đa 300 ký tự."});
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int readerId,bookId;string current,title;
            await using(var find=new SqlCommand("SELECT q.ReaderId,q.BookId,q.Status,b.Title FROM LoanRequests q WITH (UPDLOCK,HOLDLOCK) JOIN BookTitles b ON b.BookId=q.BookId WHERE q.LoanRequestId=@id",connection,tx))
            {
                find.Parameters.Add("@id",SqlDbType.Int).Value=id;
                await using var r=await find.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if(!await r.ReadAsync())return NotFound(new{error="Không tìm thấy yêu cầu mượn."});
                readerId=r.GetInt32(0);bookId=r.GetInt32(1);current=r.GetString(2);title=r.GetString(3);
            }
            if(current!="Requested")return Conflict(new{error="Yêu cầu này đã được xử lý hoặc đã bị bạn đọc hủy."});

            if(status=="Rejected")
            {
                await using var reject=new SqlCommand("UPDATE LoanRequests SET Status='Rejected',ProcessedAt=SYSDATETIME(),ProcessedBy=@staff,RejectionReason=@reason WHERE LoanRequestId=@id AND Status='Requested'",connection,tx);
                reject.Parameters.Add("@staff",SqlDbType.NVarChar,80).Value=User.Identity?.Name??"staff";
                reject.Parameters.Add("@reason",SqlDbType.NVarChar,300).Value=string.IsNullOrWhiteSpace(reason)?DBNull.Value:reason;
                reject.Parameters.Add("@id",SqlDbType.Int).Value=id;
                await reject.ExecuteNonQueryAsync();
            }
            else
            {
                await using(var readerStatus=new SqlCommand("SELECT CASE WHEN Status='Active' AND ExpiryDate>=CAST(SYSDATETIME() AS date) THEN 1 ELSE 0 END FROM Readers WHERE ReaderId=@reader",connection,tx))
                {
                    readerStatus.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;
                    if(Convert.ToInt32(await readerStatus.ExecuteScalarAsync())!=1)return Conflict(new{error="Thẻ bạn đọc đang khóa hoặc hết hạn; không thể duyệt."});
                }
                int openLoans;
                await using(var count=new SqlCommand("SELECT COUNT(*) FROM LoanDetails d JOIN Loans l ON l.LoanId=d.LoanId WHERE l.ReaderId=@reader AND d.ReturnedAt IS NULL",connection,tx))
                {count.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;openLoans=Convert.ToInt32(await count.ExecuteScalarAsync());}
                var policy=await LoanPolicyStore.LoadAsync(connection,tx);
                try{policy.ValidateRequest(openLoans,1);}catch(InvalidOperationException ex){return Conflict(new{error=ex.Message});}

                int copyId;
                await using(var pick=new SqlCommand("SELECT TOP(1) c.CopyId FROM BookCopies c WITH (UPDLOCK,HOLDLOCK) JOIN BookTitles b ON b.BookId=c.BookId WHERE c.BookId=@book AND c.Status='Available' AND b.IsActive=1 ORDER BY c.CopyId",connection,tx))
                {
                    pick.Parameters.Add("@book",SqlDbType.Int).Value=bookId;
                    var value=await pick.ExecuteScalarAsync();
                    if(value is null)return Conflict(new{error="Không còn bản sao sẵn có. Yêu cầu vẫn đang chờ; bạn đọc có thể đặt trước sách."});
                    copyId=Convert.ToInt32(value);
                }
                var loanCode=$"PM{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10,99)}";
                int loanId;
                await using(var addLoan=new SqlCommand("INSERT INTO Loans(LoanCode,ReaderId,StaffUsername,LoanDate) OUTPUT INSERTED.LoanId VALUES(@code,@reader,@staff,SYSDATETIME())",connection,tx))
                {
                    addLoan.Parameters.Add("@code",SqlDbType.NVarChar,40).Value=loanCode;
                    addLoan.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;
                    addLoan.Parameters.Add("@staff",SqlDbType.NVarChar,80).Value=User.Identity?.Name??"staff";
                    loanId=Convert.ToInt32(await addLoan.ExecuteScalarAsync());
                }
                await using(var addDetail=new SqlCommand("INSERT INTO LoanDetails(LoanId,CopyId,DueDate) VALUES(@loan,@copy,DATEADD(day,@days,CAST(SYSDATETIME() AS date)))",connection,tx))
                {
                    addDetail.Parameters.Add("@loan",SqlDbType.Int).Value=loanId;addDetail.Parameters.Add("@copy",SqlDbType.Int).Value=copyId;
                    addDetail.Parameters.Add("@days",SqlDbType.Int).Value=policy.LoanDays;await addDetail.ExecuteNonQueryAsync();
                }
                await using(var updateCopy=new SqlCommand("UPDATE BookCopies SET Status='Borrowed' WHERE CopyId=@copy AND Status='Available'",connection,tx))
                {
                    updateCopy.Parameters.Add("@copy",SqlDbType.Int).Value=copyId;
                    if(await updateCopy.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("Bản sao vừa được xử lý bởi yêu cầu khác.");
                }
                await using var approve=new SqlCommand("UPDATE LoanRequests SET Status='Approved',ProcessedAt=SYSDATETIME(),ProcessedBy=@staff,CopyId=@copy,LoanId=@loan WHERE LoanRequestId=@id AND Status='Requested'",connection,tx);
                approve.Parameters.Add("@staff",SqlDbType.NVarChar,80).Value=User.Identity?.Name??"staff";
                approve.Parameters.Add("@copy",SqlDbType.Int).Value=copyId;approve.Parameters.Add("@loan",SqlDbType.Int).Value=loanId;
                approve.Parameters.Add("@id",SqlDbType.Int).Value=id;await approve.ExecuteNonQueryAsync();
            }
            var staff=User.Identity?.Name??"staff";
            var message=status=="Approved"?$"Yêu cầu mượn sách '{title}' đã được duyệt. Phiếu mượn đã được tạo.":$"Yêu cầu mượn sách '{title}' bị từ chối.{(string.IsNullOrWhiteSpace(reason)?"":$" Lý do: {reason}")}";
            await ReaderNotificationStore.AddAsync(connection,tx,readerId,"LoanRequest",message);
            await ReaderNotificationStore.AuditAsync(connection,tx,staff,$"LoanRequest.{status}","LoanRequest",id.ToString());
            await tx.CommitAsync();
            return NoContent();
        }
        catch(InvalidOperationException ex)
        {
            await tx.RollbackAsync();logger.LogInformation(ex,"Không thể duyệt yêu cầu mượn {RequestId}",id);return Conflict(new{error=ex.Message});
        }
        catch(Exception ex)
        {
            await tx.RollbackAsync();logger.LogError(ex,"Không thể xử lý yêu cầu mượn {RequestId}",id);throw;
        }
    }
}
