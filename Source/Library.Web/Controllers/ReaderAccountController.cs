using Library.Web.Models;
using Library.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Library.Web.Controllers;

public sealed class ReaderAccountController(
    IHttpClientFactory clients,
    ILogger<ReaderAccountController> logger) : Controller
{
    private sealed record LoginResponse(string Token, string Username, DateTimeOffset ExpiresAt);
    private sealed record ApiReadResult<T>(T? Value, string? Error, bool Unauthorized);

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? username = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHistory(returnUrl);

        return View(new ReaderLoginViewModel
        {
            ReturnUrl = IsSafeReturnUrl(returnUrl) ? returnUrl : null,
            Username = username ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(ReaderLoginViewModel model)
    {
        model.ReturnUrl = IsSafeReturnUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        if (!ModelState.IsValid) return View(model);

        try
        {
            using var client = clients.CreateClient("LibraryApi");
            using var response = await client.PostAsJsonAsync("api/readers/auth/login",
                new { username = model.Username.Trim(), password = model.Password }, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                ViewData["Error"] = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Tên đăng nhập hoặc mật khẩu không đúng."
                    : await ReadApiErrorAsync(response) ?? "Không thể đăng nhập lúc này. Vui lòng thử lại.";
                model.Password = string.Empty;
                return View(model);
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(HttpContext.RequestAborted);
            if (result is null || string.IsNullOrWhiteSpace(result.Token) ||
                string.IsNullOrWhiteSpace(result.Username) || result.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                ViewData["Error"] = "Dịch vụ chưa tạo được phiên đăng nhập hợp lệ. Vui lòng thử lại.";
                model.Password = string.Empty;
                return View(model);
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, result.Username),
                new Claim(ClaimTypes.Role, "Reader"),
                new Claim(ReaderWebAuthentication.ApiTokenClaim, result.Token)
            };
            var identity = new ClaimsIdentity(claims, ReaderWebAuthentication.Scheme);
            await HttpContext.SignInAsync(
                ReaderWebAuthentication.Scheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = false,
                    ExpiresUtc = result.ExpiresAt
                });

            TempData["ReaderNotice"] = "Đăng nhập thành công.";
            return RedirectToLocalOrHistory(model.ReturnUrl);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Không kết nối được API khi bạn đọc đăng nhập");
            ViewData["Error"] = "Không kết nối được dịch vụ. Vui lòng kiểm tra API rồi thử lại.";
            model.Password = string.Empty;
            return View(model);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API đăng nhập bạn đọc trả về dữ liệu không hợp lệ");
            ViewData["Error"] = "Dịch vụ đăng nhập đang gặp sự cố. Vui lòng thử lại sau.";
            model.Password = string.Empty;
            return View(model);
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(ex, "API đăng nhập bạn đọc phản hồi quá thời gian");
            ViewData["Error"] = "Dịch vụ phản hồi quá thời gian. Vui lòng thử lại.";
            model.Password = string.Empty;
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(History));
        return View(new ReaderRegistrationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ReaderRegistrationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            using var client = clients.CreateClient("LibraryApi");
            using var response = await client.PostAsJsonAsync("api/readers/auth/register", new
            {
                cardNumber = model.CardNumber.Trim(),
                username = model.Username.Trim(),
                email = model.Email.Trim(),
                password = model.Password
            }, HttpContext.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                ViewData["Error"] = await ReadApiErrorAsync(response) ??
                    "Không thể tạo tài khoản. Vui lòng kiểm tra thông tin và thử lại.";
                model.Password = string.Empty;
                model.ConfirmPassword = string.Empty;
                return View(model);
            }

            TempData["ReaderNotice"] = "Tạo tài khoản thành công. Hãy đăng nhập để sử dụng dịch vụ bạn đọc.";
            return RedirectToAction(nameof(Login), new { username = model.Username.Trim() });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Không kết nối được API khi bạn đọc đăng ký");
            ViewData["Error"] = "Không kết nối được dịch vụ. Vui lòng kiểm tra API rồi thử lại.";
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API đăng ký bạn đọc trả về dữ liệu không hợp lệ");
            ViewData["Error"] = "Dịch vụ đăng ký đang gặp sự cố. Vui lòng thử lại sau.";
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(ex, "API đăng ký bạn đọc phản hồi quá thời gian");
            ViewData["Error"] = "Dịch vụ phản hồi quá thời gian. Vui lòng thử lại.";
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }
    }

    [Authorize(Roles = "Reader")]
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            await HttpContext.SignOutAsync(ReaderWebAuthentication.Scheme);
            TempData["ReaderError"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Login));
        }

        try
        {
            var profile = await ReadReaderApiAsync<ReaderProfileViewModel>("api/readers/me", token);
            if (profile.Unauthorized) return await RestartReaderSessionAsync();
            if (profile.Value is null)
                return View(new ReaderHistoryViewModel { ErrorMessage = profile.Error ?? "Không tải được hồ sơ bạn đọc." });

            var loans = await ReadReaderApiAsync<List<ReaderLoanViewModel>>("api/readers/me/loans", token);
            if (loans.Unauthorized) return await RestartReaderSessionAsync();
            if (loans.Value is null)
                return View(new ReaderHistoryViewModel { Profile = profile.Value, ErrorMessage = loans.Error });

            var reservations = await ReadReaderApiAsync<List<ReaderReservationViewModel>>(
                "api/readers/me/reservations", token);
            if (reservations.Unauthorized) return await RestartReaderSessionAsync();
            if (reservations.Value is null)
                return View(new ReaderHistoryViewModel { Profile = profile.Value, Loans = loans.Value, ErrorMessage = reservations.Error });

            return View(new ReaderHistoryViewModel
            {
                Profile = profile.Value,
                Loans = loans.Value,
                Reservations = reservations.Value
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Không kết nối được API khi tải lịch sử bạn đọc");
            return View(new ReaderHistoryViewModel { ErrorMessage = "Không kết nối được dịch vụ. Vui lòng thử lại sau." });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "API lịch sử bạn đọc trả về dữ liệu không hợp lệ");
            return View(new ReaderHistoryViewModel { ErrorMessage = "Dịch vụ trả về dữ liệu không hợp lệ. Vui lòng thử lại sau." });
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(ex, "API lịch sử bạn đọc phản hồi quá thời gian");
            return View(new ReaderHistoryViewModel { ErrorMessage = "Dịch vụ phản hồi quá thời gian. Vui lòng thử lại." });
        }
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var token = User.FindFirst(ReaderWebAuthentication.ApiTokenClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                using var client = clients.CreateClient("LibraryApi");
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/readers/auth/logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request, HttpContext.RequestAborted);
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    logger.LogWarning("API không thu hồi được phiên bạn đọc: HTTP {StatusCode}", response.StatusCode);
                    TempData["ReaderError"] = "Đã đăng xuất khỏi Web nhưng chưa xác nhận được việc thu hồi phiên tại API.";
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Không kết nối được API để thu hồi phiên bạn đọc");
                TempData["ReaderError"] = "Đã đăng xuất khỏi Web; phiên API sẽ tự hết hạn theo chính sách.";
            }
            catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(ex, "API thu hồi phiên bạn đọc phản hồi quá thời gian");
                TempData["ReaderError"] = "Đã đăng xuất khỏi Web; chưa xác nhận được việc thu hồi phiên API.";
            }
        }

        await HttpContext.SignOutAsync(ReaderWebAuthentication.Scheme);
        TempData["ReaderNotice"] ??= "Bạn đã đăng xuất.";
        return RedirectToAction("Index", "Home");
    }

    private async Task<ApiReadResult<T>> ReadReaderApiAsync<T>(string path, string token)
    {
        using var client = clients.CreateClient("LibraryApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpContext.RequestAborted);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ApiReadResult<T>(default, null, true);
        if (!response.IsSuccessStatusCode)
            return new ApiReadResult<T>(default, await ReadApiErrorAsync(response) ??
                "Không tải được thông tin bạn đọc.", false);

        var value = await response.Content.ReadFromJsonAsync<T>(HttpContext.RequestAborted);
        return new ApiReadResult<T>(value, value is null ? "Dịch vụ không trả về dữ liệu." : null, false);
    }

    private async Task<IActionResult> RestartReaderSessionAsync()
    {
        await HttpContext.SignOutAsync(ReaderWebAuthentication.Scheme);
        TempData["ReaderError"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocalOrHistory(string? returnUrl)
    {
        if (returnUrl is not null && IsSafeReturnUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(History))!;
    }

    private bool IsSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl)) return false;
        var path = returnUrl.Split('?', '#')[0].TrimEnd('/');
        return path.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/Home/Details", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/Home/Details/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/ReaderAccount/History", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadApiErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return body is not null &&
                body.RootElement.ValueKind == JsonValueKind.Object &&
                body.RootElement.TryGetProperty("error", out var error)
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
