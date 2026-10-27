namespace Library.Domain;
/// <summary>Chính sách mượn cấu hình được; không coi các ngưỡng là quy định phổ quát.</summary>
public sealed record LoanPolicy(int LoanDays,int MaxLoans,decimal FinePerDay) {
 public void ValidateRequest(int currentlyOpen,int requested){if(LoanDays<=0||MaxLoans<=0||FinePerDay<0) throw new InvalidOperationException("Cấu hình mượn không hợp lệ."); if(requested<1) throw new InvalidOperationException("Phiếu mượn cần ít nhất một cuốn."); if(currentlyOpen+requested>MaxLoans) throw new InvalidOperationException("Bạn đọc vượt giới hạn mượn.");}
 public int OverdueDays(DateOnly due,DateOnly returned)=>Math.Max(0,returned.DayNumber-due.DayNumber);
 public decimal Fine(DateOnly due,DateOnly returned)=>OverdueDays(due,returned)*FinePerDay;
}
