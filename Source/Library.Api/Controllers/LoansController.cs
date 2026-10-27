using Microsoft.AspNetCore.Mvc; using Microsoft.Data.SqlClient; using System.Data;
namespace Library.Api.Controllers;
[ApiController,Route("api/loans")]
public class LoansController(DbFactory db):ControllerBase {
 public record CreateLoan(string CardNumber,string StaffUsername,string[] CopyCodes);
 [HttpPost] public async Task<IActionResult> Create([FromBody]CreateLoan request) {
  if(request.CopyCodes is null || request.CopyCodes.Length==0) return BadRequest(new{error="Phiếu mượn phải có ít nhất một bản sao."});
  await using var c=db.Create(); await c.OpenAsync(); await using var tx=await c.BeginTransactionAsync(IsolationLevel.Serializable);
  try {
   int readerId; await using(var check=new SqlCommand("SELECT ReaderId FROM Readers WHERE CardNumber=@card AND Status='Active' AND ExpiryDate>=CAST(SYSDATETIME() AS date)",c,(SqlTransaction)tx)){check.Parameters.AddWithValue("@card",request.CardNumber.Trim()); var v=await check.ExecuteScalarAsync(); if(v is null) throw new InvalidOperationException("Thẻ bạn đọc không hợp lệ hoặc đã hết hạn."); readerId=Convert.ToInt32(v);}
   int openLoans; int maxLoans;
   await using(var limits=new SqlCommand("SELECT (SELECT COUNT(*) FROM LoanDetails d JOIN Loans l ON l.LoanId=d.LoanId WHERE l.ReaderId=@reader AND d.ReturnedAt IS NULL),(SELECT CAST(Value AS int) FROM Settings WHERE [Key]='MaxLoans')",c,(SqlTransaction)tx)){limits.Parameters.AddWithValue("@reader",readerId);await using var r=await limits.ExecuteReaderAsync();if(!await r.ReadAsync())throw new InvalidOperationException("Chưa cấu hình giới hạn mượn.");openLoans=r.GetInt32(0);maxLoans=r.GetInt32(1);}
   if(openLoans+request.CopyCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count()>maxLoans)throw new InvalidOperationException("Bạn đọc đã đạt giới hạn số cuốn được mượn.");
   var code= "PM"+DateTime.Now.ToString("yyyyMMddHHmmssfff"); int loanId;
   await using(var cmd=new SqlCommand("INSERT INTO Loans(LoanCode,ReaderId,StaffUsername,LoanDate) OUTPUT INSERTED.LoanId VALUES(@code,@reader,@staff,SYSDATETIME())",c,(SqlTransaction)tx)){cmd.Parameters.AddWithValue("@code",code);cmd.Parameters.AddWithValue("@reader",readerId);cmd.Parameters.AddWithValue("@staff",request.StaffUsername);loanId=Convert.ToInt32(await cmd.ExecuteScalarAsync());}
   foreach(var copy in request.CopyCodes.Distinct(StringComparer.OrdinalIgnoreCase)) {
    await using var item=new SqlCommand("UPDATE BookCopies WITH (UPDLOCK,HOLDLOCK) SET Status='Borrowed' OUTPUT INSERTED.CopyId WHERE CopyCode=@code AND Status='Available'",c,(SqlTransaction)tx); item.Parameters.AddWithValue("@code",copy.Trim()); var id=await item.ExecuteScalarAsync(); if(id is null) throw new InvalidOperationException($"Bản sao {copy} không tồn tại hoặc không sẵn sàng.");
    await using var detail=new SqlCommand("INSERT INTO LoanDetails(LoanId,CopyId,DueDate) VALUES(@loan,@copy,DATEADD(day,(SELECT CAST(Value AS int) FROM Settings WHERE [Key]='LoanDays'),CAST(SYSDATETIME() AS date)))",c,(SqlTransaction)tx); detail.Parameters.AddWithValue("@loan",loanId); detail.Parameters.AddWithValue("@copy",id); await detail.ExecuteNonQueryAsync();
   }
   await tx.CommitAsync(); return Ok(new{loanId,loanCode=code});
  } catch(InvalidOperationException ex){await tx.RollbackAsync();return Conflict(new{error=ex.Message});} catch {await tx.RollbackAsync();throw;}
 }
}

