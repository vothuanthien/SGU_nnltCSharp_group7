using Library.Api.Data;
using Library.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>
/// API tra cứu thông tin thẻ độc giả và danh sách sách đang mượn.
/// Phục vụ cả màn hình Circulation (Desktop) và trang tra cứu thẻ (Web MVC).
/// </summary>
[ApiController]
[Route("api/readers")]
public sealed class ReadersController(DbFactory db) : ControllerBase
{
    [HttpGet("card/{cardNumber}")]
    public async Task<IActionResult> LookupCard(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return BadRequest(new { error = "Vui lòng nhập số thẻ bạn đọc." });

        await using var connection = db.Create();
        await connection.OpenAsync();

        // 1. Tra cứu thông tin bạn đọc
        const string readerSql = """
            SELECT ReaderId, CardNumber, FullName, ExpiryDate, Status
            FROM Readers
            WHERE CardNumber = @card
            """;

        int readerId;
        string fullName, status;
        DateTime expiryDate;

        await using (var cmd = new SqlCommand(readerSql, connection))
        {
            cmd.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = cardNumber.Trim();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return NotFound(new { error = $"Không tìm thấy bạn đọc với số thẻ '{cardNumber}'." });

            readerId = reader.GetInt32(0);
            cardNumber = reader.GetString(1);
            fullName = reader.GetString(2);
            expiryDate = reader.GetDateTime(3);
            status = reader.GetString(4);
        }

        var policy = await LoanPolicyStore.LoadAsync(connection, null);
        var today = DateTime.Today;
        var isExpired = expiryDate.Date < today;

        // 2. Lấy danh sách các cuốn đang mượn (chưa trả)
        const string loansSql = """
            SELECT l.LoanCode, l.LoanDate, d.DueDate, c.CopyCode, b.Title
            FROM LoanDetails d
            JOIN Loans l ON l.LoanId = d.LoanId
            JOIN BookCopies c ON c.CopyId = d.CopyId
            JOIN BookTitles b ON b.BookId = c.BookId
            WHERE l.ReaderId = @readerId AND d.ReturnedAt IS NULL
            ORDER BY d.DueDate ASC
            """;

        var borrowedBooks = new List<object>();
        await using (var cmd = new SqlCommand(loansSql, connection))
        {
            cmd.Parameters.Add("@readerId", SqlDbType.Int).Value = readerId;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var loanCode = reader.GetString(0);
                var loanDate = reader.GetDateTime(1);
                var dueDate = reader.GetDateTime(2);
                var copyCode = reader.GetString(3);
                var title = reader.GetString(4);

                var overdueDays = today > dueDate.Date ? (today - dueDate.Date).Days : 0;
                var fineExpected = overdueDays * policy.FinePerDay;

                borrowedBooks.Add(new
                {
                    loanCode,
                    copyCode,
                    title,
                    loanDate = loanDate.ToString("yyyy-MM-dd"),
                    dueDate = dueDate.ToString("yyyy-MM-dd"),
                    overdueDays,
                    fineExpected,
                    isOverdue = overdueDays > 0
                });
            }
        }

        return Ok(new
        {
            cardNumber,
            fullName,
            expiryDate = expiryDate.ToString("yyyy-MM-dd"),
            status,
            isExpired,
            canBorrow = status == "Active" && !isExpired && borrowedBooks.Count < policy.MaxLoans,
            openLoansCount = borrowedBooks.Count,
            maxLoans = policy.MaxLoans,
            borrowedBooks
        });
    }
}
