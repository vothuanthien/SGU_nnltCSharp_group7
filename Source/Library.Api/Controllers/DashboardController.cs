using Library.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

/// <summary>
/// Cung cấp số liệu thống kê Dashboard và trích xuất dữ liệu báo cáo cho ứng dụng quản trị.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class DashboardController(DbFactory db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        await using var connection = db.Create();
        await connection.OpenAsync();

        // 1. KPI 1: Tổng số đầu sách (đang phục vụ và tổng số)
        int totalActiveBooks, totalBooks;
        await using (var cmd = new SqlCommand("SELECT COUNT(*), SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) FROM BookTitles", connection))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            totalBooks = r.GetInt32(0);
            totalActiveBooks = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        }

        // 2. KPI 2: Tổng số bản sao & KPI 3: Bản sao đang được mượn
        int totalCopies, borrowedCopies, availableCopies;
        await using (var cmd = new SqlCommand(
            "SELECT COUNT(*), SUM(CASE WHEN Status='Borrowed' THEN 1 ELSE 0 END), " +
            "SUM(CASE WHEN Status='Available' THEN 1 ELSE 0 END) FROM BookCopies", connection))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            totalCopies = r.GetInt32(0);
            borrowedCopies = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            availableCopies = r.IsDBNull(2) ? 0 : r.GetInt32(2);
        }

        // 3. KPI 4: Số lượt mượn quá hạn (chưa trả và DueDate < hôm nay)
        int overdueLoans;
        await using (var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM LoanDetails WHERE ReturnedAt IS NULL AND DueDate < CAST(SYSDATETIME() AS date)", connection))
        {
            overdueLoans = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 4. KPI 5: Doanh thu phạt đã thu & Tiền phạt phát sinh
        decimal finesCollected, finesIncurred;
        await using (var cmd = new SqlCommand(
            "SELECT ISNULL(SUM(Amount), 0) FROM FinePayments", connection))
        {
            finesCollected = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }

        await using (var cmd = new SqlCommand(
            "SELECT ISNULL(SUM(FineAmount), 0) FROM [Returns]", connection))
        {
            finesIncurred = Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }

        return Ok(new
        {
            totalActiveBooks,
            totalBooks,
            totalCopies,
            borrowedCopies,
            availableCopies,
            overdueLoans,
            totalFinesCollected = finesCollected,
            totalFinesIncurred = finesIncurred,
            updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    // Dữ liệu phục vụ xuất báo cáo danh sách sách
    [HttpGet("export/books")]
    public async Task<IActionResult> ExportBooks()
    {
        await using var connection = db.Create();
        await connection.OpenAsync();

        const string sql = """
            SELECT b.BookId, b.Title, ISNULL(g.Name, N'Chưa phân loại') AS Genre,
                   ISNULL(p.Name, N'Chưa cập nhật') AS Publisher,
                   ISNULL(a.Authors, N'Chưa có tác giả') AS Authors,
                   ISNULL(c.TotalCopies, 0) AS TotalCopies,
                   ISNULL(c.AvailableCopies, 0) AS AvailableCopies,
                   CASE WHEN b.IsActive = 1 THEN N'Đang phục vụ' ELSE N'Ngừng phục vụ' END AS Status
            FROM BookTitles b
            LEFT JOIN Genres g ON g.GenreId = b.GenreId
            LEFT JOIN Publishers p ON p.PublisherId = b.PublisherId
            LEFT JOIN (
                SELECT ba.BookId, STRING_AGG(a.FullName, N', ') AS Authors
                FROM BookAuthors ba JOIN Authors a ON a.AuthorId = ba.AuthorId
                GROUP BY ba.BookId
            ) a ON a.BookId = b.BookId
            LEFT JOIN (
                SELECT BookId, COUNT(*) AS TotalCopies,
                       SUM(CASE WHEN Status = 'Available' THEN 1 ELSE 0 END) AS AvailableCopies
                FROM BookCopies GROUP BY BookId
            ) c ON c.BookId = b.BookId
            ORDER BY b.BookId ASC
            """;

        var list = new List<object>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new
            {
                bookId = r.GetInt32(0),
                title = r.GetString(1),
                genre = r.GetString(2),
                publisher = r.GetString(3),
                authors = r.GetString(4),
                totalCopies = r.GetInt32(5),
                availableCopies = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                status = r.GetString(7)
            });
        }

        return Ok(list);
    }

    // Dữ liệu phục vụ xuất báo cáo lịch sử mượn trả
    [HttpGet("export/loans")]
    public async Task<IActionResult> ExportLoans()
    {
        await using var connection = db.Create();
        await connection.OpenAsync();

        const string sql = """
            SELECT l.LoanCode, r.CardNumber, r.FullName, c.CopyCode, b.Title,
                   l.LoanDate, d.DueDate, d.ReturnedAt,
                   CASE WHEN d.ReturnedAt IS NOT NULL THEN N'Đã trả'
                        WHEN d.DueDate < CAST(SYSDATETIME() AS date) THEN N'Quá hạn'
                        ELSE N'Đang mượn' END AS Status,
                   ISNULL(ret.FineAmount, 0) AS FineAmount
            FROM LoanDetails d
            JOIN Loans l ON l.LoanId = d.LoanId
            JOIN Readers r ON r.ReaderId = l.ReaderId
            JOIN BookCopies c ON c.CopyId = d.CopyId
            JOIN BookTitles b ON b.BookId = c.BookId
            LEFT JOIN [Returns] ret ON ret.LoanDetailId = d.LoanDetailId
            ORDER BY l.LoanDate DESC
            """;

        var list = new List<object>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var loanDate = r.GetDateTime(5);
            var dueDate = r.GetDateTime(6);
            var returnedAt = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);

            list.Add(new
            {
                loanCode = r.GetString(0),
                cardNumber = r.GetString(1),
                readerName = r.GetString(2),
                copyCode = r.GetString(3),
                bookTitle = r.GetString(4),
                loanDate = loanDate.ToString("yyyy-MM-dd HH:mm"),
                dueDate = dueDate.ToString("yyyy-MM-dd"),
                returnedAt = returnedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-",
                status = r.GetString(8),
                fineAmount = r.GetDecimal(9)
            });
        }

        return Ok(list);
    }
}
