using Library.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Controllers;

/// <summary>Các API công khai phục vụ duyệt danh mục và tra cứu sách trên Web.</summary>
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
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var term = q?.Trim();
        var books = db.BookTitles.AsNoTracking().Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(term)) books = books.Where(b => b.Title.Contains(term) ||
            (b.ISBN != null && b.ISBN.Contains(term)) || b.BookAuthors.Any(x => x.Author.FullName.Contains(term)));
        if (genreId.HasValue) books = books.Where(b => b.GenreId == genreId.Value);
        if (availableOnly) books = books.Where(b => b.Copies.Any(x => x.Status == "Available"));
        var total = await books.CountAsync();

        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            "newest" => books.OrderByDescending(b => b.PublishedYear).ThenBy(b => b.Title),
            "available" => books.OrderByDescending(b => b.Copies.Count(x => x.Status == "Available")).ThenBy(b => b.Title),
            "oldest" => books.OrderBy(b => b.PublishedYear).ThenBy(b => b.Title),
            _ => books.OrderBy(b => b.Title)
        };

        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
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
}
