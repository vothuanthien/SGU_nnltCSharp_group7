using Library.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Controllers;

/// <summary>API danh mục công khai và yêu cầu đặt trước dành cho bạn đọc đã đăng nhập.</summary>
[ApiController, Route("api/catalog")]
public sealed class CatalogController(LibraryDbContext db) : ControllerBase
{
    // Lấy danh sách thể loại đang hoạt động để tạo bộ lọc danh mục trên trang chủ.
    [HttpGet("genres")]
    public async Task<IActionResult> Genres() => Ok(await db.Genres.AsNoTracking()
        .Where(x => x.IsActive).OrderBy(x => x.Name)
        .Select(x => new { id = x.GenreId, name = x.Name }).ToListAsync());

    // Tìm theo tên sách, ISBN hoặc tác giả; hỗ trợ lọc tồn kho/thể loại, sắp xếp và phân trang.
    [HttpGet("books")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int? genreId,
        [FromQuery] bool availableOnly = false, [FromQuery] string? sort = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12,
        [FromQuery] string? author = null, [FromQuery] int? year = null)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var term = q?.Trim();
        var books = db.BookTitles.AsNoTracking().Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(term)) books = books.Where(b => b.Title.Contains(term) ||
            (b.ISBN != null && b.ISBN.Contains(term)) || b.BookAuthors.Any(x => x.Author.FullName.Contains(term)));
        if (genreId.HasValue) books = books.Where(b => b.GenreId == genreId.Value);
        if (!string.IsNullOrWhiteSpace(author)) books = books.Where(b => b.BookAuthors.Any(x => x.Author.FullName.Contains(author.Trim())));
        if (year.HasValue) books = books.Where(b => b.PublishedYear == year.Value);
        if (availableOnly) books = books.Where(b => b.Copies.Any(x => x.Status == "Available"));
        var total = await books.CountAsync();
        var lastPage = total == 0 ? 1 : 1 + (total - 1) / pageSize;
        page = Math.Min(page, lastPage);

        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            "newest" => books.OrderByDescending(b => b.PublishedYear).ThenBy(b => b.Title),
            "available" => books.OrderByDescending(b => b.Copies.Count(x => x.Status == "Available")).ThenBy(b => b.Title),
            "oldest" => books.OrderBy(b => b.PublishedYear).ThenBy(b => b.Title),
            _ => books.OrderBy(b => b.Title)
        };

        var rows = await ordered.ThenBy(b => b.BookId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new { id = b.BookId, title = b.Title, isbn = b.ISBN, year = b.PublishedYear,
                genre = b.Genre == null ? "Chưa phân loại" : b.Genre.Name,
                authors = b.BookAuthors.Select(x => x.Author.FullName).ToList(),
                available = b.Copies.Count(x => x.Status == "Available") }).ToListAsync();
        return Ok(new { items = rows, total, page, pageSize });
    }

    // Chi tiết sách công khai gồm tác giả, thể loại, nhà xuất bản và số bản sao còn sẵn.
    [HttpGet("books/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var book = await db.BookTitles.AsNoTracking().Where(b => b.BookId == id && b.IsActive)
            .Select(b => new { id = b.BookId, title = b.Title, isbn = b.ISBN, year = b.PublishedYear,
                description = b.Description, genre = b.Genre == null ? "Chưa phân loại" : b.Genre.Name,
                publisher = b.Publisher == null ? "Chưa cập nhật" : b.Publisher.Name,
                authors = b.BookAuthors.Select(x => x.Author.FullName).ToList(),
                available = b.Copies.Count(x => x.Status == "Available") }).FirstOrDefaultAsync();
        return book is null ? NotFound() : Ok(book);
    }

    public sealed record ReservationRequest(int BookId);

    // Tiếp nhận yêu cầu đặt trước sách khi đầu sách hết bản sao sẵn có.
    [HttpPost("reservations")]
    [Authorize(Roles = "Reader")]
    public async Task<IActionResult> CreateReservation([FromBody] ReservationRequest request)
    {
        if (request.BookId <= 0)
            return BadRequest(new { error = "Mã sách không hợp lệ." });

        var book = await db.BookTitles.Include(b => b.Copies).FirstOrDefaultAsync(b => b.BookId == request.BookId && b.IsActive);
        if (book is null)
            return NotFound(new { error = "Không tìm thấy đầu sách hoặc sách đã ngừng phục vụ." });

        var availableCopies = book.Copies.Count(c => c.Status == "Available");
        if (availableCopies > 0)
            return BadRequest(new { error = $"Đầu sách này hiện vẫn còn {availableCopies} bản sao sẵn sàng. Bạn đọc vui lòng liên hệ thủ thư để mượn trực tiếp thay vì đặt trước." });

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ReaderId, FullName, Status, ExpiryDate FROM Readers WHERE ReaderId = @readerId";
        var pCard = cmd.CreateParameter(); pCard.ParameterName = "@readerId";
        pCard.Value = int.Parse(User.FindFirst("reader_id")!.Value); cmd.Parameters.Add(pCard);

        int readerId;
        string readerName;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync())
                return NotFound(new { error = "Không tìm thấy hồ sơ bạn đọc." });

            readerId = r.GetInt32(0);
            readerName = r.GetString(1);
            var status = r.GetString(2);
            var expiry = r.GetDateTime(3);

            if (status != "Active")
                return BadRequest(new { error = "Thẻ bạn đọc đang bị khóa hoặc không hoạt động." });

            if (expiry.Date < DateTime.Today)
                return BadRequest(new { error = "Thẻ bạn đọc đã hết hạn sử dụng. Vui lòng gia hạn thẻ trước khi đặt sách." });
        }

        int reservationId;
        try
        {
            await using var insert = conn.CreateCommand();
            insert.CommandText =
                "INSERT INTO Reservations(ReaderId,BookId,RequestedAt,Status) " +
                "OUTPUT INSERTED.ReservationId VALUES(@readerId,@bookId,SYSDATETIME(),'Pending')";
            var readerParameter = insert.CreateParameter();
            readerParameter.ParameterName = "@readerId";
            readerParameter.Value = readerId;
            insert.Parameters.Add(readerParameter);
            var bookParameter = insert.CreateParameter();
            bookParameter.ParameterName = "@bookId";
            bookParameter.Value = request.BookId;
            insert.Parameters.Add(bookParameter);
            reservationId = Convert.ToInt32(await insert.ExecuteScalarAsync());
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // The filtered unique index is the concurrency-safe check for Pending duplicates.
            return Conflict(new { error = "Bạn đọc đã có yêu cầu đặt trước cho sách này đang chờ xử lý." });
        }

        return Ok(new
        {
            reservationId,
            bookId = book.BookId,
            bookTitle = book.Title,
            readerName,
            status = "Pending",
            requestedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            message = "Đã ghi nhận yêu cầu đặt trước. Bạn có thể theo dõi trạng thái trong mục Lịch sử mượn."
        });
    }
}
