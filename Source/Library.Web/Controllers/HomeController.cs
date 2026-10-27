using Microsoft.AspNetCore.Mvc; using System.Net.Http.Json;
namespace Library.Web.Controllers;
public class HomeController(IHttpClientFactory clients,ILogger<HomeController> log):Controller {
 public async Task<IActionResult> Index(string? q,int page=1){ try {using var h=clients.CreateClient("LibraryApi"); ViewBag.Query=q; ViewBag.Page=page; return View(await h.GetFromJsonAsync<List<BookVm>>($"api/catalog/books?q={Uri.EscapeDataString(q??"")}&page={page}") ?? []);} catch(Exception ex){log.LogWarning(ex,"API unavailable");ViewBag.ApiError="Chưa kết nối được API. Hãy khởi động Library.Api và kiểm tra SQL Server.";return View(new List<BookVm>());} }
 public IActionResult Privacy()=>View();
}
public record BookVm(int id,string title,string? isbn,int? year,string genre,int available);
