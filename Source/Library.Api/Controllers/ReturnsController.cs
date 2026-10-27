using Microsoft.AspNetCore.Mvc; using Microsoft.Data.SqlClient; using System.Data;
namespace Library.Api.Controllers;
[ApiController,Route("api/returns")]
public class ReturnsController(DbFactory db):ControllerBase {
 public record ReturnRequest(string CopyCode,string Condition,string StaffUsername);
 [HttpPost] public async Task<IActionResult> Return([FromBody]ReturnRequest request){
  if(string.IsNullOrWhiteSpace(request.CopyCode)||!new[]{"Normal","Damaged","Lost"}.Contains(request.Condition)) return BadRequest(new{error="Mã bản sao hoặc tình trạng sách không hợp lệ."});
  await using var c=db.Create(); await c.OpenAsync(); await using var tx=await c.BeginTransactionAsync(IsolationLevel.Serializable);
  try {
   int detailId; DateTime due;
   await using(var find=new SqlCommand("SELECT TOP(1) d.LoanDetailId,d.DueDate FROM LoanDetails d JOIN BookCopies b ON b.CopyId=d.CopyId WHERE b.CopyCode=@code AND d.ReturnedAt IS NULL ORDER BY d.LoanDetailId",c,(SqlTransaction)tx)){find.Parameters.AddWithValue("@code",request.CopyCode.Trim()); await using var r=await find.ExecuteReaderAsync(); if(!await r.ReadAsync()) throw new InvalidOperationException("Không tìm thấy lượt mượn đang mở cho mã cuốn này."); detailId=r.GetInt32(0); due=r.GetDateTime(1);}
   int overdue=Math.Max(0,(DateTime.Today-due.Date).Days); decimal perDay; await using(var setting=new SqlCommand("SELECT CAST(Value AS decimal(12,2)) FROM Settings WHERE [Key]='FinePerDay'",c,(SqlTransaction)tx)){var v=await setting.ExecuteScalarAsync();perDay=v is null?0:Convert.ToDecimal(v);}
   decimal fine=overdue*perDay;
   await using(var update=new SqlCommand("UPDATE LoanDetails SET ReturnedAt=SYSDATETIME() OUTPUT INSERTED.CopyId WHERE LoanDetailId=@id AND ReturnedAt IS NULL",c,(SqlTransaction)tx)){update.Parameters.AddWithValue("@id",detailId);var v=await update.ExecuteScalarAsync();if(v is null)throw new InvalidOperationException("Chi tiết mượn đã được xử lý trả.");var copyId=Convert.ToInt32(v);
    await using var add=new SqlCommand("INSERT Returns(LoanDetailId,ReturnedAt,Condition,OverdueDays,FineAmount,StaffUsername) VALUES(@detail,SYSDATETIME(),@condition,@days,@fine,@staff); UPDATE BookCopies SET Status=@status WHERE CopyId=@copy; SELECT SCOPE_IDENTITY();",c,(SqlTransaction)tx);add.Parameters.AddWithValue("@detail",detailId);add.Parameters.AddWithValue("@condition",request.Condition);add.Parameters.AddWithValue("@days",overdue);add.Parameters.AddWithValue("@fine",fine);add.Parameters.AddWithValue("@staff",request.StaffUsername);add.Parameters.AddWithValue("@status",request.Condition=="Normal"?"Available":request.Condition);add.Parameters.AddWithValue("@copy",copyId);var returnId=Convert.ToInt32(await add.ExecuteScalarAsync());await tx.CommitAsync();return Ok(new{returnId,overdueDays=overdue,fineAmount=fine,status=request.Condition=="Normal"?"Available":request.Condition});}
  } catch(InvalidOperationException ex){await tx.RollbackAsync();return Conflict(new{error=ex.Message});}catch{await tx.RollbackAsync();throw;}
 }
}
