using Library.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:7002", "http://localhost:5002")));
builder.Services.AddSingleton(new DbFactory(builder.Configuration.GetConnectionString("Library")!));
builder.Services.AddDbContext<LibraryDbContext>(options=>options.UseSqlServer(builder.Configuration.GetConnectionString("Library")));
builder.Services.AddControllers();
var app = builder.Build();
if(!app.Environment.IsDevelopment()) app.UseHttpsRedirection(); app.UseCors(); app.MapControllers(); app.MapGet("/health", () => Results.Ok(new { status="ok", service="Library.Api" }));
app.Run();
public sealed class DbFactory(string connectionString) {
 public SqlConnection Create() => new(connectionString);
}
