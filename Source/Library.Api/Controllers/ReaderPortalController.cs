using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController, Route("api/readers/me"), Authorize(Roles = "Reader")]
public sealed class ReaderPortalController(DbFactory db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (!TryGetReaderId(out var readerId)) return Forbid();

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT ReaderCode,CardNumber,FullName,Email,Phone,ExpiryDate,Status FROM Readers WHERE ReaderId=@id",
            connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = readerId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync()) return NotFound();

        return Ok(new
        {
            readerCode = reader.GetString(0),
            cardNumber = reader.GetString(1),
            fullName = reader.GetString(2),
            email = reader.IsDBNull(3) ? null : reader.GetString(3),
            phone = reader.IsDBNull(4) ? null : reader.GetString(4),
            expiryDate = reader.GetDateTime(5).ToString("yyyy-MM-dd"),
            status = reader.GetString(6)
        });
    }

    [HttpGet("loans")]
    public async Task<IActionResult> Loans()
    {
        if (!TryGetReaderId(out var readerId)) return Forbid();

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT l.LoanCode,b.Title,c.CopyCode,l.LoanDate,d.DueDate,d.ReturnedAt " +
            "FROM LoanDetails d JOIN Loans l ON l.LoanId=d.LoanId " +
            "JOIN BookCopies c ON c.CopyId=d.CopyId JOIN BookTitles b ON b.BookId=c.BookId " +
            "WHERE l.ReaderId=@id ORDER BY l.LoanDate DESC,d.DueDate DESC",
            connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = readerId;

        var loans = new List<object>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dueDate = reader.GetDateTime(4);
            var returnedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);
            loans.Add(new
            {
                loanCode = reader.GetString(0),
                title = reader.GetString(1),
                copyCode = reader.GetString(2),
                loanDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                dueDate = dueDate.ToString("yyyy-MM-dd"),
                returnedAt = returnedAt?.ToString("yyyy-MM-dd"),
                isOverdue = returnedAt is null && dueDate.Date < DateTime.Today
            });
        }

        return Ok(loans);
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations()
    {
        if (!TryGetReaderId(out var readerId)) return Forbid();

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT r.ReservationId,b.Title,r.RequestedAt,r.Status,r.HoldUntil " +
            "FROM Reservations r JOIN BookTitles b ON b.BookId=r.BookId " +
            "WHERE r.ReaderId=@id ORDER BY r.RequestedAt DESC",
            connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = readerId;

        var reservations = new List<object>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var holdUntil = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
            reservations.Add(new
            {
                reservationId = reader.GetInt32(0),
                bookTitle = reader.GetString(1),
                requestedAt = reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm"),
                status = reader.GetString(3),
                holdUntil = holdUntil?.ToString("yyyy-MM-dd HH:mm")
            });
        }

        return Ok(reservations);
    }

    private bool TryGetReaderId(out int readerId) =>
        int.TryParse(User.FindFirst("reader_id")?.Value, out readerId);
}
