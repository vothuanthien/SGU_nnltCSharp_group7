using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    /// <summary>Hiển thị biểu mẫu tra cứu thẻ thư viện.</summary>
    [HttpGet]
    public IActionResult CardLookup() => View(new CardLookupPageVm());

    /// <summary>Tra cứu thẻ và lịch sử đang mượn qua API; MVC không kết nối trực tiếp SQL Server.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CardLookup(CardLookupPageVm model)
    {
        model.CardNumber = model.CardNumber?.Trim() ?? string.Empty;
        if (model.CardNumber.Length is 0 or > 30)
        {
            model.Error = "Vui lòng nhập số thẻ hợp lệ (tối đa 30 ký tự).";
            return View(model);
        }

        try
        {
            using var http = clients.CreateClient("LibraryApi");
            using var response = await http.GetAsync($"api/readers/card/{Uri.EscapeDataString(model.CardNumber)}");
            if (!response.IsSuccessStatusCode)
            {
                model.Error = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Vui lòng liên hệ thủ thư để tra cứu thẻ.",
                    HttpStatusCode.NotFound => "Không tìm thấy thẻ thư viện.",
                    _ => "Dịch vụ tra cứu tạm thời không khả dụng."
                };
                return View(model);
            }

            model.Result = await response.Content.ReadFromJsonAsync<ReaderCardVm>();
            if (model.Result is null) model.Error = "API không trả về thông tin thẻ.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            log.LogWarning(ex, "Không thể tra cứu thẻ {CardNumber} qua API", model.CardNumber);
            model.Error = "Hiện chưa kết nối được dịch vụ tra cứu. Vui lòng thử lại sau.";
        }

        return View(model);
    }

    /// <summary>Gửi yêu cầu đặt trước qua API khi sách không còn bản sao khả dụng.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reserve(int id, string? cardNumber)
    {
        var normalizedCard = cardNumber?.Trim();
        if (id <= 0 || string.IsNullOrWhiteSpace(normalizedCard) || normalizedCard.Length > 30)
        {
            TempData["ReservationError"] = "Vui lòng nhập số thẻ thư viện hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            using var http = clients.CreateClient("LibraryApi");
            using var response = await http.PostAsJsonAsync("api/catalog/reservations",
                new { cardNumber = normalizedCard, bookId = id });
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
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            log.LogWarning(ex, "Không thể đặt trước sách {BookId}", id);
            TempData["ReservationError"] = "Hiện chưa kết nối được dịch vụ đặt trước. Vui lòng thử lại sau.";
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
public sealed class CardLookupPageVm
{
    public string CardNumber { get; set; } = string.Empty;
    public ReaderCardVm? Result { get; set; }
    public string? Error { get; set; }
}
public sealed record ReaderCardVm(string cardNumber, string fullName, string expiryDate, string status,
    bool isExpired, bool canBorrow, int openLoansCount, int maxLoans, List<BorrowedBookVm> borrowedBooks);
public sealed record BorrowedBookVm(string loanCode, string copyCode, string title, string loanDate,
    string dueDate, int overdueDays, decimal fineExpected, bool isOverdue);
public sealed record ReservationResultVm(string message);
