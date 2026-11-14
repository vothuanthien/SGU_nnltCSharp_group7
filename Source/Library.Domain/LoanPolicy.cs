namespace Library.Domain;

/// <summary>Defines configurable loan limits, durations, and overdue fines.</summary>
/// <param name="LoanDays">The number of days allowed for a loan.</param>
/// <param name="MaxLoans">The maximum number of open loans per reader.</param>
/// <param name="FinePerDay">The fine charged for each overdue day.</param>
public sealed record LoanPolicy(int LoanDays, int MaxLoans, decimal FinePerDay)
{
    /// <summary>Validates a requested loan against policy settings and current loans.</summary>
    /// <param name="currentlyOpen">The reader's current number of open loans.</param>
    /// <param name="requested">The number of copies requested in this loan.</param>
    /// <exception cref="InvalidOperationException">The policy or request violates a limit.</exception>
    public void ValidateRequest(int currentlyOpen, int requested)
    {
        if (LoanDays <= 0 || MaxLoans <= 0 || FinePerDay < 0)
        {
            throw new InvalidOperationException("Cấu hình mượn không hợp lệ.");
        }

        if (requested < 1)
        {
            throw new InvalidOperationException("Phiếu mượn cần ít nhất một cuốn.");
        }

        if (currentlyOpen + requested > MaxLoans)
        {
            throw new InvalidOperationException("Bạn đọc vượt giới hạn mượn.");
        }
    }

    /// <summary>Calculates overdue days, returning zero for on-time or early returns.</summary>
    /// <param name="due">The due date.</param>
    /// <param name="returned">The return date.</param>
    /// <returns>The number of days returned after the due date.</returns>
    public int OverdueDays(DateOnly due, DateOnly returned) =>
        Math.Max(0, returned.DayNumber - due.DayNumber);

    /// <summary>Calculates the fine for a return using this policy's daily fine.</summary>
    /// <param name="due">The due date.</param>
    /// <param name="returned">The return date.</param>
    /// <returns>The total overdue fine.</returns>
    public decimal Fine(DateOnly due, DateOnly returned) =>
        OverdueDays(due, returned) * FinePerDay;
}
