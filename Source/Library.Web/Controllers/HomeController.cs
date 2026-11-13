using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace Library.Web.Controllers;

/// <summary>Điều phối trang danh mục, bộ lọc và trang hướng dẫn dành cho bạn đọc.</summary>
public sealed class HomeController(IHttpClientFactory clients, ILogger<HomeController> log) : Controller
{
    // Tải sách và thể loại cùng lúc; khi API lỗi trang vẫn hiển thị thông báo dễ hiểu.
    public async Task<IActionResult> Index(string? q, int? genreId, bool availableOnly = false,
        string? sort = null, int page = 1)
    {
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            var genres = await http.GetFromJsonAsync<List<GenreVm>>("api/catalog/genres") ?? [];
            var requestedSort = sort?.Trim().ToLowerInvariant();
            sort = requestedSort is "newest" or "available" or "oldest" ? requestedSort : "title";
            var query = $"q={Uri.EscapeDataString(q ?? "")}&page={Math.Max(1, page)}&pageSize=12&sort={sort}" +
                (genreId.HasValue ? $"&genreId={genreId}" : "") + (availableOnly ? "&availableOnly=true" : "");
            var result = await http.GetFromJsonAsync<BookPageVm>($"api/catalog/books?{query}") ?? new([], 0, page, 12);
            ViewBag.Query = q; ViewBag.GenreId = genreId; ViewBag.Page = result.Page;
            ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort;
            ViewBag.Total = result.Total; ViewBag.Genres = genres;
            return View(result.Items);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Không thể tải danh mục từ API");
            ViewBag.ApiError = "Chưa kết nối được API. Hãy khởi động API và kiểm tra SQL Server.";
            ViewBag.Genres = new List<GenreVm>(); ViewBag.Query = q; ViewBag.Page = page;
            ViewBag.GenreId = genreId; ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort ?? "title";
            ViewBag.Total = 0;
            return View(new List<BookVm>());
        }
    }

    // Hiển thị thông tin quy trình và quy định mượn/trả sách cho bạn đọc.
    public IActionResult Rules() => View();

    // Lấy thông tin chi tiết sách theo mã từ API công khai.
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            var book = await http.GetFromJsonAsync<BookDetailsVm>($"api/catalog/books/{id}");
            return book is null ? NotFound() : View(book);
        }
        catch (Exception ex) { log.LogWarning(ex, "Không thể tải chi tiết sách {BookId}", id); return NotFound(); }
    }
}

public sealed record GenreVm(int id, string name);
public sealed record BookVm(int id, string title, string? isbn, int? year, string genre, List<string> authors, int available);
public sealed record BookPageVm(List<BookVm> Items, int Total, int Page, int PageSize);
public sealed record BookDetailsVm(int id, string title, string? isbn, int? year, string? description,
    string genre, string publisher, List<string> authors, int available);
