using Library.Api.Data;
using Library.Api.Security;
using Library.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Library")
    ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:Library.");

builder.Services.AddSingleton(new DbFactory(connectionString));
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

// Phiên đăng nhập là bearer token ngẫu nhiên lưu trong bộ nhớ API (không phải JWT).
builder.Services.AddSingleton<AdminSessionStore>();
builder.Services.AddAuthentication(SessionAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationDefaults.Scheme, null);
builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// Mọi lỗi không lường trước trả về cùng định dạng { error } và được ghi log, không lộ chi tiết nội bộ.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var failure = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(failure, "Lỗi không xử lý được tại {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Hệ thống gặp lỗi. Vui lòng thử lại sau." });
}));

// Chỉ chạy khi Database:AutoInitialize=true (Docker): chờ SQL Server, tạo database/seed nếu cần.
await DbInitializer.RunAsync(app.Configuration, app.Logger);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Library.Api" }));
// Kiểm tra API thực sự truy vấn được SQL Server (dùng khi kiểm tra Docker).
app.MapGet("/health/db", async (DbFactory factory, ILogger<Program> log) =>
{
    try
    {
        await using var connection = factory.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM BookTitles", connection);
        var books = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Results.Ok(new { status = "ok", database = "connected", books });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Không kết nối được SQL Server");
        return Results.Json(new { status = "error", database = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public sealed class DbFactory(string connectionString)
{
    public SqlConnection Create() => new(connectionString);
}
