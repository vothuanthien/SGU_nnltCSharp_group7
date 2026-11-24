using Library.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("LibraryApi",c=>c.BaseAddress=new Uri(builder.Configuration["LibraryApi:BaseUrl"]!));
builder.Services.AddAuthentication(ReaderWebAuthentication.Scheme)
    .AddCookie(ReaderWebAuthentication.Scheme, options =>
    {
        options.LoginPath = "/ReaderAccount/Login";
        options.AccessDeniedPath = "/ReaderAccount/Login";
        options.Cookie.Name = "Library.Reader";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
var app=builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
if(!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
app.MapControllerRoute(name:"default",pattern:"{controller=Home}/{action=Index}/{id?}");app.Run();
