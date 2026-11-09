using Library.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using Library.Api.Security;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:7002", "http://localhost:5002")));
builder.Services.AddSingleton(new DbFactory(builder.Configuration.GetConnectionString("Library")!));
// Phiên quản trị được lưu trong bộ nhớ API; mọi yêu cầu admin phải có bearer token hợp lệ.
builder.Services.AddSingleton<AdminSessionStore>();
builder.Services.AddDbContext<LibraryDbContext>(options=>options.UseSqlServer(builder.Configuration.GetConnectionString("Library")));
builder.Services.AddControllers();
var app = builder.Build();
if(!app.Environment.IsDevelopment()) app.UseHttpsRedirection(); app.UseCors();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api/admin")) {
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : "";
        var sessions = context.RequestServices.GetRequiredService<AdminSessionStore>();
        if (!sessions.TryGet(token, out var session)) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; await context.Response.WriteAsJsonAsync(new { error = "Vui lòng đăng nhập quản trị." }); return; }
        if (!(session.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || session.Role.Equals("Librarian", StringComparison.OrdinalIgnoreCase))) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        context.Items[AdminSessionStore.ContextItemKey] = session;
    }
    await next();
});
app.MapControllers(); app.MapGet("/health", () => Results.Ok(new { status="ok", service="Library.Api" }));
app.Run();
public sealed class DbFactory(string connectionString) {
 public SqlConnection Create() => new(connectionString);
}
