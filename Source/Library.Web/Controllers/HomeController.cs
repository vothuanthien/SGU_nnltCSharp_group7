using Library.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Web.Controllers;

/// <summary>Điều phối trang danh mục, bộ lọc và trang hướng dẫn dành cho bạn đọc.</summary>
public sealed class HomeController(IHttpClientFactory clients, ILogger<HomeController> log) : Controller
{
    // Tải sách và thể loại cùng lúc; khi API lỗi trang vẫn hiển thị thông báo dễ hiểu.
    public async Task<IActionResult> Index(string? q, int? genreId, bool availableOnly = false,
        string? sort = null, int page = 1, string? author = null, int? year = null)
    {
        q = q?.Trim();
        if (q?.Length > 120) q = q[..120];
        page = Math.Clamp(page, 1, 10000);
        if (genreId is <= 0) genreId = null;
        author = string.IsNullOrWhiteSpace(author) ? null : author.Trim()[..Math.Min(author.Trim().Length, 100)];
        if (year is < 1000 or > 2100) year = null;
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            var genres = await http.GetFromJsonAsync<List<GenreVm>>("api/catalog/genres") ?? [];
            var requestedSort = sort?.Trim().ToLowerInvariant();
            sort = requestedSort is "newest" or "available" or "oldest" ? requestedSort : "title";
            var query = $"q={Uri.EscapeDataString(q ?? "")}&page={Math.Max(1, page)}&pageSize=12&sort={sort}" +
                (genreId.HasValue ? $"&genreId={genreId}" : "") +
                (availableOnly ? "&availableOnly=true" : "") +
                (author is not null ? $"&author={Uri.EscapeDataString(author)}" : "") +
                (year.HasValue ? $"&year={year}" : "");
            var result = await http.GetFromJsonAsync<BookPageVm>($"api/catalog/books?{query}") ?? new([], 0, page, 12);
            ViewBag.Query = q; ViewBag.GenreId = genreId; ViewBag.Page = result.Page;
            ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort;
            ViewBag.Author = author; ViewBag.Year = year;
            ViewBag.Total = result.Total; ViewBag.Genres = genres;
            return View(result.Items);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Không thể tải danh mục từ API");
            ViewBag.ApiError = "Hiện chưa kết nối được dịch vụ danh mục. Vui lòng kiểm tra kết nối hoặc thử lại sau.";
            ViewBag.Genres = new List<GenreVm>(); ViewBag.Query = q; ViewBag.Page = page;
            ViewBag.GenreId = genreId; ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort ?? "title";
            ViewBag.Author = author; ViewBag.Year = year;
            ViewBag.Total = 0;
            return View(new List<BookVm>());
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "API danh mục trả về dữ liệu không hợp lệ");
            ViewBag.ApiError = "Dịch vụ danh mục đang gặp sự cố. Vui lòng thử lại sau.";
            ViewBag.Genres = new List<GenreVm>(); ViewBag.Query = q; ViewBag.Page = page;
            ViewBag.GenreId = genreId; ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort ?? "title";
            ViewBag.Author = author; ViewBag.Year = year;
            ViewBag.Total = 0;
            return View(new List<BookVm>());
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            log.LogWarning(ex, "Quá thời gian chờ khi tải danh mục từ API");
            ViewBag.ApiError = "Dịch vụ phản hồi quá thời gian. Vui lòng thử lại sau.";
            ViewBag.Genres = new List<GenreVm>(); ViewBag.Query = q; ViewBag.Page = page;
            ViewBag.GenreId = genreId; ViewBag.AvailableOnly = availableOnly; ViewBag.Sort = sort ?? "title";
            ViewBag.Author = author; ViewBag.Year = year;
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
            using var response = await http.GetAsync($"api/catalog/books/{id}", HttpContext.RequestAborted);
            if (response.StatusCode == HttpStatusCode.NotFound) return NotFound();
            response.EnsureSuccessStatusCode();
            var book = await response.Content.ReadFromJsonAsync<BookDetailsVm>(HttpContext.RequestAborted);
            if (book is not null) return View(book);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
            ex is OperationCanceledException && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            log.LogWarning(ex, "Không thể tải chi tiết sách {BookId}", id);
        }

        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("Error");
    }

    [Route("/Home/Error")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error");
    }

    /// <summary>Mở lịch sử mượn cá nhân; bạn đọc phải đăng nhập.</summary>
    [HttpGet]
    public IActionResult CardLookup() => RedirectToAction("History", "ReaderAccount");

    /// <summary>Gửi yêu cầu đặt trước qua API khi sách không còn bản sao khả dụng.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reserve(int id)
    {
        if (id <= 0)
        {
            TempData["ReservationError"] = "Đầu sách không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            await HttpContext.SignOutAsync(ReaderWebAuthentication.Scheme);
            return RedirectToAction("Login", "ReaderAccount", new
            {
                returnUrl = Url.Action(nameof(Details), new { id })
            });
        }

        try
        {
            using var http = clients.CreateClient("LibraryApi");
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/catalog/reservations")
            {
                Content = JsonContent.Create(new { bookId = id })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, HttpContext.RequestAborted);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                await HttpContext.SignOutAsync(ReaderWebAuthentication.Scheme);
                TempData["ReaderError"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "ReaderAccount", new
                {
                    returnUrl = Url.Action(nameof(Details), new { id })
                });
            }

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ReservationResultVm>();
                TempData["ReservationSuccess"] = result?.message ?? "Đã gửi yêu cầu đặt trước.";
            }
            else
            {
                TempData["ReservationError"] = await ReadApiErrorAsync(response)
                    ?? "Không thể gửi yêu cầu đặt trước.";
            }
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Không thể đặt trước sách {BookId}", id);
            TempData["ReservationError"] = "Hiện chưa kết nối được dịch vụ đặt trước. Vui lòng thử lại sau.";
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "API đặt trước trả về dữ liệu không hợp lệ cho sách {BookId}", id);
            TempData["ReservationError"] = "Dịch vụ đặt trước trả về dữ liệu không hợp lệ. Vui lòng thử lại.";
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            log.LogWarning(ex, "API đặt trước phản hồi quá thời gian cho sách {BookId}", id);
            TempData["ReservationError"] = "Dịch vụ đặt trước phản hồi quá thời gian. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private static async Task<string?> ReadApiErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (body is not null && body.RootElement.ValueKind == JsonValueKind.Object &&
                body.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record GenreVm(int id, string name);
public sealed record BookVm(int id, string title, string? isbn, int? year, string genre, List<string> authors, int available);
public sealed record BookPageVm(List<BookVm> Items, int Total, int Page, int PageSize);
public sealed record BookDetailsVm(int id, string title, string? isbn, int? year, string? description,
    string genre, string publisher, List<string> authors, int available);
public sealed record ReservationResultVm(string message);
