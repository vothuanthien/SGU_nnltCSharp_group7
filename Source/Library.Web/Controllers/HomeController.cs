using Library.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

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

    [HttpGet]
    public IActionResult RegisterReader() => View(new ReaderRegistrationVm());

    /// <summary>Gửi đăng ký bạn đọc tới API để Web và WinForms dùng chung dữ liệu SQL.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterReader(ReaderRegistrationVm model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            using var response = await http.PostAsJsonAsync("api/readers/register", new
            {
                username = model.Username,
                fullName = model.FullName,
                email = model.Email,
                phone = model.Phone,
                password = model.Password
            });
            if (!response.IsSuccessStatusCode)
            {
                model.Error = await ReadApiErrorAsync(response) ?? "Không thể tạo thẻ bạn đọc.";
                return View(model);
            }

            model.Result = await response.Content.ReadFromJsonAsync<ReaderRegistrationResultVm>();
            if (model.Result is null) model.Error = "API không trả về thông tin thẻ bạn đọc.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            log.LogWarning(ex, "Không thể đăng ký bạn đọc qua API");
            model.Error = "Chưa kết nối được hệ thống thư viện. Vui lòng thử lại sau.";
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult ReaderLogin(string? returnUrl = null) => View(new ReaderLoginVm { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReaderLogin(ReaderLoginVm model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            using var response = await http.PostAsJsonAsync("api/readers/login", new { username = model.Username, password = model.Password });
            if (!response.IsSuccessStatusCode)
            {
                model.Error = await ReadApiErrorAsync(response) ?? "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View(model);
            }
            var result = await response.Content.ReadFromJsonAsync<ReaderLoginResultVm>();
            if (result is null) { model.Error = "API không trả về thông tin phiên đăng nhập."; return View(model); }
            HttpContext.Session.SetString("ReaderToken", result.token);
            HttpContext.Session.SetString("ReaderCard", result.cardNumber);
            HttpContext.Session.SetString("ReaderName", result.fullName);
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Action(nameof(MyAccount))!);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            log.LogWarning(ex, "Không thể đăng nhập cổng bạn đọc");
            model.Error = "Chưa kết nối được hệ thống thư viện. Vui lòng thử lại sau.";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> MyAccount()
    {
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(ReaderLogin));
        try
        {
            using var http = clients.CreateClient("LibraryApi");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var model = await http.GetFromJsonAsync<ReaderPortalVm>("api/readers/me/portal");
            if (model is not null)
            {
                // Older API instances may omit collections introduced by newer portal features.
                // Normalize them so the account page still renders while the API is being restarted.
                return View(model with
                {
                    loans = model.loans ?? [],
                    reservations = model.reservations ?? [],
                    loanRequests = model.loanRequests ?? [],
                    notifications = model.notifications ?? []
                });
            }
        }
        catch (HttpRequestException ex) { log.LogWarning(ex, "Không thể tải cổng bạn đọc"); }
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(ReaderLogin));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReaderLogout()
    {
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            using var http = clients.CreateClient("LibraryApi");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try { using var _ = await http.PostAsync("api/auth/logout", null); } catch (HttpRequestException) { }
        }
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(int id)
    {
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if (string.IsNullOrWhiteSpace(token)) return RedirectToAction(nameof(ReaderLogin));
        using var http = clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.DeleteAsync($"api/readers/me/reservations/{id}");
        TempData[response.IsSuccessStatusCode ? "PortalSuccess" : "PortalError"] = response.IsSuccessStatusCode
            ? "Đã hủy yêu cầu đặt trước."
            : await ReadApiErrorAsync(response) ?? "Không thể hủy yêu cầu này.";
        return RedirectToAction(nameof(MyAccount));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestLoan(int id)
    {
        var token=User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if(string.IsNullOrWhiteSpace(token))
            return RedirectToAction(nameof(ReaderLogin),new{returnUrl=Url.Action(nameof(Details),new{id})});
        using var http=clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        try
        {
            using var response=await http.PostAsJsonAsync("api/readers/me/loan-requests",new{bookId=id});
            if (response.IsSuccessStatusCode)
                TempData["LoanRequestSuccess"] = "Đã gửi yêu cầu mượn. Thủ thư sẽ kiểm tra và xác nhận.";
            else
                TempData["LoanRequestError"] = await ReadApiErrorAsync(response) ?? (response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "API đang chạy phiên bản cũ chưa có chức năng yêu cầu mượn. Hãy dừng API cũ rồi khởi động lại.",
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Phiên đăng nhập đã hết hạn hoặc thẻ bạn đọc chưa được phép mượn. Hãy đăng nhập lại.",
                    _ => $"Không thể gửi yêu cầu mượn (HTTP {(int)response.StatusCode}). Hãy kiểm tra cửa sổ API để xem lỗi chi tiết."
                });
        }
        catch(HttpRequestException ex)
        {
            log.LogWarning(ex,"Không thể gửi yêu cầu mượn sách {BookId}",id);
            TempData["LoanRequestError"]="Chưa kết nối được hệ thống. Vui lòng thử lại sau.";
        }
        return RedirectToAction(nameof(Details),new{id});
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelLoanRequest(int id)
    {
        var token=User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if(string.IsNullOrWhiteSpace(token))return RedirectToAction(nameof(ReaderLogin));
        using var http=clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.DeleteAsync($"api/readers/me/loan-requests/{id}");
        TempData[response.IsSuccessStatusCode?"PortalSuccess":"PortalError"]=response.IsSuccessStatusCode
            ? "Đã hủy yêu cầu mượn."
            : await ReadApiErrorAsync(response)??"Không thể hủy yêu cầu này.";
        return RedirectToAction(nameof(MyAccount));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewLoan(int id)
    {
        var token=User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if(string.IsNullOrWhiteSpace(token))return RedirectToAction(nameof(ReaderLogin));
        using var http=clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.PostAsync($"api/readers/me/loans/{id}/renew",null);
        TempData[response.IsSuccessStatusCode?"PortalSuccess":"PortalError"]=response.IsSuccessStatusCode
            ? "Đã gia hạn sách thành công. Hạn trả mới được cập nhật bên dưới."
            : await ReadApiErrorAsync(response)??"Không thể gia hạn lượt mượn này.";
        return RedirectToAction(nameof(MyAccount));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeReaderPassword(string currentPassword,string newPassword,string confirmPassword)
    {
        if(string.IsNullOrEmpty(newPassword)||newPassword.Length<8||newPassword!=confirmPassword)
        {
            TempData["PortalError"]=newPassword!=confirmPassword?"Mật khẩu mới nhập lại chưa khớp.":"Mật khẩu mới cần ít nhất 8 ký tự.";
            return RedirectToAction(nameof(MyAccount));
        }
        var token=User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if(string.IsNullOrWhiteSpace(token))return RedirectToAction(nameof(ReaderLogin));
        using var http=clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.PostAsJsonAsync("api/readers/me/change-password",new{currentPassword,newPassword});
        if(!response.IsSuccessStatusCode)
        {
            TempData["PortalError"]=await ReadApiErrorAsync(response)??"Không thể đổi mật khẩu.";
            return RedirectToAction(nameof(MyAccount));
        }
        var renewed=await response.Content.ReadFromJsonAsync<ReaderTokenRefreshVm>();
        if(!string.IsNullOrWhiteSpace(renewed?.token))HttpContext.Session.SetString("ReaderToken",renewed.token);
        TempData["PortalSuccess"]="Đổi mật khẩu thành công. Các phiên đăng nhập khác đã bị đăng xuất.";
        return RedirectToAction(nameof(MyAccount));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        var token=User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value ?? HttpContext.Session.GetString("ReaderToken");
        if(string.IsNullOrWhiteSpace(token))return RedirectToAction(nameof(ReaderLogin));
        using var http=clients.CreateClient("LibraryApi");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.PostAsync($"api/readers/me/notifications/{id}/read",null);
        if(!response.IsSuccessStatusCode)TempData["PortalError"]=await ReadApiErrorAsync(response)??"Không thể cập nhật thông báo.";
        return RedirectToAction(nameof(MyAccount));
    }

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
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value
            ?? HttpContext.Session.GetString("ReaderToken");
        var normalizedCard = HttpContext.Session.GetString("ReaderCard");
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ReservationError"] = "Vui lòng đăng nhập tài khoản bạn đọc để đặt trước.";
            return RedirectToAction("Login", "ReaderAccount", new { returnUrl = Url.Action(nameof(Details), new { id }) });
        }
        if (id <= 0 || (HttpContext.Session.GetString("ReaderToken") is not null &&
            (string.IsNullOrWhiteSpace(normalizedCard) || normalizedCard.Length > 30)))
        {
            TempData["ReservationError"] = "Đầu sách không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
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
public sealed class ReaderRegistrationVm
{
    [Required(ErrorMessage = "Vui lòng tạo tên đăng nhập.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Tên đăng nhập cần từ 3 đến 80 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Chỉ dùng chữ không dấu, số, dấu chấm, gạch ngang hoặc gạch dưới.")]
    public string Username { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Họ tên cần từ 2 đến 200 ký tự.")]
    public string FullName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng.")]
    [StringLength(254)]
    public string? Email { get; set; }
    [StringLength(30, ErrorMessage = "Số điện thoại tối đa 30 ký tự.")]
    public string? Phone { get; set; }
    [Required(ErrorMessage = "Vui lòng tạo mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần từ 8 đến 100 ký tự.")]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Hai mật khẩu chưa khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? Error { get; set; }
    public ReaderRegistrationResultVm? Result { get; set; }
}
public sealed record ReaderRegistrationResultVm(int readerId, string readerCode, string cardNumber, string username, string fullName, string expiryDate);
public sealed class ReaderLoginVm
{
    [Required(ErrorMessage = "Nhập tên đăng nhập.")]
    public string Username { get; set; } = string.Empty;
    [Required(ErrorMessage = "Nhập mật khẩu.")]
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
}
public sealed record ReaderLoginResultVm(string token, string cardNumber, string fullName, DateTimeOffset expiresAt);
public sealed record ReaderPortalVm(ReaderPortalProfileVm profile, List<ReaderLoanHistoryVm> loans, List<ReaderReservationVm> reservations, List<ReaderLoanRequestVm> loanRequests, List<ReaderNotificationVm> notifications);
public sealed record ReaderPortalProfileVm(string readerCode,string cardNumber,string fullName,string? email,string? phone,string expiryDate,string status,string? username);
public sealed record ReaderLoanHistoryVm(int loanDetailId,string loanCode,string title,string copyCode,string loanDate,string dueDate,string? returnedAt,decimal fineAmount,decimal overdueFine,decimal compensationAmount,decimal paidAmount,int renewalCount,bool canRenew,string status);
public sealed record ReaderReservationVm(int reservationId,string title,string status,string requestedAt,string? holdUntil,string? copyCode);
public sealed record ReaderLoanRequestVm(int requestId,string title,string status,string requestedAt,string? processedAt,string? rejectionReason,string? copyCode,string? loanCode);
public sealed record ReaderNotificationVm(int notificationId,string type,string message,string createdAt,bool isRead);
public sealed record ReaderTokenRefreshVm(string token,DateTimeOffset expiresAt);
