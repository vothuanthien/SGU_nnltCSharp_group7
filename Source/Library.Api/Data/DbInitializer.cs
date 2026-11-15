using System.Text.RegularExpressions;
using Library.Api.Security;
using Microsoft.Data.SqlClient;

namespace Library.Api.Data;

/// <summary>
/// Khởi tạo database khi chạy trong Docker: chờ SQL Server sẵn sàng, chạy script 01–04 nếu database chưa có,
/// rồi tạo tài khoản admin từ biến môi trường. Chỉ chạy khi Database:AutoInitialize = true.
/// </summary>
public static class DbInitializer
{
    private static readonly string[] SchemaScripts =
        ["01_CreateDatabase.sql", "02_CreateTables.sql", "03_Constraints_Indexes.sql", "04_SeedData.sql"];

    private static readonly Regex GoSeparator = new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public static async Task RunAsync(IConfiguration config, ILogger logger)
    {
        if (!config.GetValue<bool>("Database:AutoInitialize")) return;

        var connectionString = config.GetConnectionString("Library")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Library.");
        var scriptsPath = config["Database:ScriptsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Database");

        var target = new SqlConnectionStringBuilder(connectionString);
        var databaseName = string.IsNullOrWhiteSpace(target.InitialCatalog) ? "LibraryManagement" : target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

        await WaitForServerAsync(master, logger);

        if (await IsInitializedAsync(master, databaseName))
        {
            logger.LogInformation("Database {Database} đã được khởi tạo trước đó, bỏ qua script schema/seed.", databaseName);
        }
        else
        {
            foreach (var script in SchemaScripts) await RunScriptAsync(master, Path.Combine(scriptsPath, script), logger);
            logger.LogInformation("Đã tạo database {Database} và dữ liệu mẫu.", databaseName);
        }

        // Giai đoạn 2 bổ sung cột tài khoản và chỉ mục đặt trước; migration idempotent,
        // nên chạy cho cả database mới lẫn volume Docker đã tồn tại.
        await RunScriptAsync(master, Path.Combine(scriptsPath, "07_ProfessionalSkills_Update.sql"), logger);

        await EnsureAdminAsync(connectionString, config["Database:SeedAdminPassword"], logger);
    }

    // Docker Compose có healthcheck, nhưng API vẫn tự thử lại để không phụ thuộc thứ tự khởi động.
    private static async Task WaitForServerAsync(string connectionString, ILogger logger)
    {
        const int maxAttempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (SqlException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("SQL Server chưa sẵn sàng (lần {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    private static async Task<bool> IsInitializedAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT CASE WHEN DB_ID(@db) IS NOT NULL AND OBJECT_ID(QUOTENAME(@db) + N'.dbo.Settings', N'U') IS NOT NULL THEN 1 ELSE 0 END",
            connection);
        command.Parameters.AddWithValue("@db", databaseName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    // Tách script theo dòng GO (SqlClient không hiểu GO) rồi chạy từng batch.
    private static async Task RunScriptAsync(string connectionString, string path, ILogger logger)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Không tìm thấy script SQL: {path}");
        logger.LogInformation("Chạy script {Script}", Path.GetFileName(path));

        var text = (await File.ReadAllTextAsync(path)).TrimStart('\uFEFF');
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in GoSeparator.Split(text))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
    }

    // Admin được tạo từ biến môi trường, không có mật khẩu mặc định trong mã nguồn Docker.
    private static async Task EnsureAdminAsync(string connectionString, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Database:SeedAdminPassword trống: không tạo tài khoản admin tự động.");
            return;
        }
        if (password.Length < 8) throw new InvalidOperationException("Mật khẩu admin khởi tạo phải có ít nhất 8 ký tự.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM Accounts WHERE Username = N'admin') " +
            "INSERT INTO Accounts(Username, PasswordHash, Role, IsActive) VALUES (N'admin', @hash, 'Admin', 1)", connection);
        command.Parameters.AddWithValue("@hash", PasswordHashing.Hash(password));
        var created = await command.ExecuteNonQueryAsync();
        logger.LogInformation(created > 0 ? "Đã tạo tài khoản admin." : "Tài khoản admin đã tồn tại.");
    }
}
