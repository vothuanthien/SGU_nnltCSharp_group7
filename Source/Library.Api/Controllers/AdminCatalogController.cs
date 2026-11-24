using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Library.Api.Controllers;

/// <summary>CRUD có xác thực cho sách, bản sao, bạn đọc, tác giả, thể loại và nhà xuất bản.</summary>
[ApiController]
[Route("api/admin/catalog")]
[Authorize(Roles = "Admin,Librarian")]
public sealed class AdminCatalogController(DbFactory db, ILogger<AdminCatalogController> logger) : ControllerBase
{
    private sealed record Field(string JsonName, string Column, SqlDbType Type, int Size = 0,
        bool Required = false, object? DefaultValue = null, string[]? Allowed = null);

    private sealed record Resource(string Table, string IdColumn, string SelectSql, string SearchSql,
        string OrderBy, Field[] Fields, string? SoftDeleteSql = null);

    // Tên bảng, cột và biểu thức SQL chỉ lấy từ allowlist này; dữ liệu người dùng luôn truyền bằng tham số.
    private static readonly IReadOnlyDictionary<string, Resource> Resources =
        new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase)
        {
            ["books"] = new("BookTitles", "BookId",
                "SELECT TOP (500) b.BookId AS id,b.Title AS title,b.ISBN AS isbn,b.PublishedYear AS publishedYear," +
                "b.Description AS description,b.GenreId AS genreId,g.Name AS genre,b.PublisherId AS publisherId," +
                "p.Name AS publisher,b.IsActive AS isActive,ISNULL(a.authors,N'Chưa cập nhật') AS authors," +
                "ISNULL(c.totalCopies,0) AS totalCopies,ISNULL(c.borrowedCopies,0) AS borrowedCopies," +
                "ISNULL(c.availableCopies,0) AS availableCopies FROM BookTitles b LEFT JOIN Genres g ON g.GenreId=b.GenreId " +
                "LEFT JOIN Publishers p ON p.PublisherId=b.PublisherId " +
                "LEFT JOIN (SELECT ba.BookId,STRING_AGG(au.FullName,N', ') AS authors FROM BookAuthors ba " +
                "JOIN Authors au ON au.AuthorId=ba.AuthorId GROUP BY ba.BookId) a ON a.BookId=b.BookId " +
                "LEFT JOIN (SELECT BookId,COUNT(*) AS totalCopies,SUM(CASE WHEN Status='Borrowed' THEN 1 ELSE 0 END) AS borrowedCopies," +
                "SUM(CASE WHEN Status='Available' THEN 1 ELSE 0 END) AS availableCopies FROM BookCopies GROUP BY BookId) c ON c.BookId=b.BookId",
                "b.Title LIKE @term OR ISNULL(b.ISBN,N'') LIKE @term OR EXISTS (SELECT 1 FROM BookCopies cx WHERE cx.BookId=b.BookId AND cx.CopyCode LIKE @term)", "b.Title",
                [
                    new("title", "Title", SqlDbType.NVarChar, 300, true),
                    new("isbn", "ISBN", SqlDbType.NVarChar, 20),
                    new("publishedYear", "PublishedYear", SqlDbType.Int),
                    new("description", "Description", SqlDbType.NVarChar, -1),
                    new("genreId", "GenreId", SqlDbType.Int),
                    new("publisherId", "PublisherId", SqlDbType.Int),
                    new("isActive", "IsActive", SqlDbType.Bit, Required: true, DefaultValue: true)
                ], "UPDATE BookTitles SET IsActive=0 WHERE BookId=@id"),

            ["copies"] = new("BookCopies", "CopyId",
                "SELECT TOP (500) c.CopyId AS id,c.CopyCode AS copyCode,c.BookId AS bookId,b.Title AS title," +
                "c.ShelfLocation AS shelfLocation,c.AcquiredAt AS acquiredAt,c.Status AS status " +
                "FROM BookCopies c JOIN BookTitles b ON b.BookId=c.BookId",
                "c.CopyCode LIKE @term OR b.Title LIKE @term OR ISNULL(c.ShelfLocation,N'') LIKE @term", "c.CopyCode",
                [
                    new("copyCode", "CopyCode", SqlDbType.NVarChar, 40, true),
                    new("bookId", "BookId", SqlDbType.Int, Required: true),
                    new("shelfLocation", "ShelfLocation", SqlDbType.NVarChar, 80),
                    new("acquiredAt", "AcquiredAt", SqlDbType.Date),
                    new("status", "Status", SqlDbType.VarChar, 20, Required: true, DefaultValue: "Available",
                        Allowed: ["Available", "Borrowed", "Reserved", "Damaged", "Lost", "Retired"])
                ], "UPDATE BookCopies SET Status='Retired' WHERE CopyId=@id AND Status<>'Borrowed'"),

            ["readers"] = new("Readers", "ReaderId",
                "SELECT TOP (500) ReaderId AS id,ReaderCode AS readerCode,CardNumber AS cardNumber,FullName AS fullName," +
                "Email AS email,Phone AS phone,RegisteredAt AS registeredAt,ExpiryDate AS expiryDate,Status AS status FROM Readers",
                "ReaderCode LIKE @term OR CardNumber LIKE @term OR FullName LIKE @term", "FullName",
                [
                    new("readerCode", "ReaderCode", SqlDbType.NVarChar, 30, true),
                    new("cardNumber", "CardNumber", SqlDbType.NVarChar, 30, true),
                    new("fullName", "FullName", SqlDbType.NVarChar, 200, true),
                    new("email", "Email", SqlDbType.NVarChar, 254),
                    new("phone", "Phone", SqlDbType.NVarChar, 30),
                    new("registeredAt", "RegisteredAt", SqlDbType.Date, Required: true, DefaultValue: DateTime.Today),
                    new("expiryDate", "ExpiryDate", SqlDbType.Date, Required: true),
                    new("status", "Status", SqlDbType.VarChar, 20, Required: true, DefaultValue: "Active", Allowed: ["Active", "Inactive", "Suspended"])
                ], "UPDATE Readers SET Status='Inactive' WHERE ReaderId=@id"),

            ["genres"] = new("Genres", "GenreId",
                "SELECT TOP (500) GenreId AS id,Name AS name,IsActive AS isActive FROM Genres",
                "Name LIKE @term", "Name",
                [new("name", "Name", SqlDbType.NVarChar, 100, true),
                 new("isActive", "IsActive", SqlDbType.Bit, Required: true, DefaultValue: true)],
                "UPDATE Genres SET IsActive=0 WHERE GenreId=@id"),

            ["authors"] = new("Authors", "AuthorId",
                "SELECT TOP (500) AuthorId AS id,FullName AS fullName FROM Authors",
                "FullName LIKE @term", "FullName",
                [new("fullName", "FullName", SqlDbType.NVarChar, 200, true)]),

            ["publishers"] = new("Publishers", "PublisherId",
                "SELECT TOP (500) PublisherId AS id,Name AS name FROM Publishers",
                "Name LIKE @term", "Name",
                [new("name", "Name", SqlDbType.NVarChar, 200, true)])
        };

    // Trả tối đa 500 bản ghi theo danh mục; tìm kiếm chỉ ghép từ các câu SQL trong allowlist.
    [HttpGet("{resource}")]
    public async Task<IActionResult> List(string resource, [FromQuery] string? q, [FromQuery] int? genreId)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();

        await using var connection = db.Create();
        await connection.OpenAsync();
        var genreClause = resource.Equals("books", StringComparison.OrdinalIgnoreCase) && genreId.HasValue
            ? " AND b.GenreId=@genreId" : string.Empty;
        await using var command = new SqlCommand(
            $"{definition.SelectSql} WHERE (@term=N'' OR ({definition.SearchSql})){genreClause} ORDER BY {definition.OrderBy}", connection);
        if (!string.IsNullOrWhiteSpace(genreClause)) command.Parameters.Add("@genreId", SqlDbType.Int).Value = genreId!.Value;
        command.Parameters.Add("@term", SqlDbType.NVarChar, 300).Value =
            string.IsNullOrWhiteSpace(q) ? string.Empty : $"%{q.Trim()}%";

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return Ok(rows);
    }

    // Kiểm tra trường đầu vào, tạo bản ghi và ghi AuditLogs trong cùng transaction.
    [HttpPost("{resource}")]
    public async Task<IActionResult> Create(string resource, [FromBody] JsonElement body)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();
        if (!TryReadValues(definition, body, out var values, out var error)) return BadRequest(new { error });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var columns = string.Join(',', definition.Fields.Select(x => $"[{x.Column}]"));
            var parameters = string.Join(',', definition.Fields.Select(x => $"@{x.JsonName}"));
            await using var command = new SqlCommand(
                $"INSERT INTO [{definition.Table}] ({columns}) OUTPUT INSERTED.[{definition.IdColumn}] VALUES ({parameters})",
                connection, transaction);
            AddParameters(command, definition, values);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            await WriteAudit(connection, transaction, "Create", resource, id);
            await transaction.CommitAsync();
            return CreatedAtAction(nameof(List), new { resource }, new { id });
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể tạo bản ghi resource={Resource}", resource);
            return SqlConflict(ex);
        }
    }

    // Cập nhật toàn bộ trường được cho phép và ghi lại người thực hiện thao tác.
    [HttpPut("{resource}/{id:int}")]
    public async Task<IActionResult> Update(string resource, int id, [FromBody] JsonElement body)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();
        if (!TryReadValues(definition, body, out var values, out var error)) return BadRequest(new { error });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var assignments = string.Join(',', definition.Fields.Select(x => $"[{x.Column}]=@{x.JsonName}"));
            await using var command = new SqlCommand(
                $"UPDATE [{definition.Table}] SET {assignments} WHERE [{definition.IdColumn}]=@id", connection, transaction);
            AddParameters(command, definition, values);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                await transaction.RollbackAsync();
                return NotFound(new { error = "Không tìm thấy dữ liệu cần cập nhật." });
            }
            if (resource.Equals("readers", StringComparison.OrdinalIgnoreCase) &&
                IsReaderInactive(definition, values))
                await RevokeReaderSessionsAsync(connection, transaction, id);
            await WriteAudit(connection, transaction, "Update", resource, id);
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể cập nhật resource={Resource} id={Id}", resource, id);
            return SqlConflict(ex);
        }
    }

    // Ngừng hoạt động riêng với xóa cứng để người dùng chọn đúng ý định.
    [HttpPut("{resource}/{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(string resource, int id)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();
        if (definition.SoftDeleteSql is null)
            return BadRequest(new { error = "Danh mục này không hỗ trợ ngừng hoạt động." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(definition.SoftDeleteSql, connection, transaction);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                await transaction.RollbackAsync();
                return NotFound(new { error = "Không tìm thấy bản ghi hoặc không thể ngừng hoạt động ở trạng thái hiện tại." });
            }
            if (resource.Equals("readers", StringComparison.OrdinalIgnoreCase))
                await RevokeReaderSessionsAsync(connection, transaction, id);
            await WriteAudit(connection, transaction, "Deactivate", resource, id);
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể ngừng hoạt động resource={Resource} id={Id}", resource, id);
            return SqlConflict(ex);
        }
    }

    // Giữ route DELETE cũ cho tương thích; thao tác xóa cứng có route explicit riêng.
    [HttpDelete("{resource}/{id:int}")]
    public async Task<IActionResult> Delete(string resource, int id)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();
        var sql = definition.SoftDeleteSql ?? $"DELETE FROM [{definition.Table}] WHERE [{definition.IdColumn}]=@id";

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                await transaction.RollbackAsync();
                return NotFound(new { error = "Không tìm thấy dữ liệu hoặc dữ liệu không thể xóa ở trạng thái hiện tại (ví dụ bản sao đang được mượn)." });
            }
            if (resource.Equals("readers", StringComparison.OrdinalIgnoreCase))
                await RevokeReaderSessionsAsync(connection, transaction, id);
            await WriteAudit(connection, transaction, definition.SoftDeleteSql is null ? "Delete" : "Deactivate", resource, id);
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể xóa resource={Resource} id={Id}", resource, id);
            return SqlConflict(ex);
        }
    }

    [HttpDelete("{resource}/{id:int}/permanent")]
    public async Task<IActionResult> DeletePermanent(string resource, int id)
    {
        if (!Resources.TryGetValue(resource, out var definition)) return NotFound();

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            if (resource.Equals("readers", StringComparison.OrdinalIgnoreCase))
            {
                await RevokeReaderSessionsAsync(connection, transaction, id);
                await using var deleteAccount = new SqlCommand(
                    "DELETE FROM dbo.ReaderAccounts WHERE ReaderId=@id", connection, transaction);
                deleteAccount.Parameters.Add("@id", SqlDbType.Int).Value = id;
                await deleteAccount.ExecuteNonQueryAsync();
            }

            await using var command = new SqlCommand(
                $"DELETE FROM [{definition.Table}] WHERE [{definition.IdColumn}]=@id", connection, transaction);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                await transaction.RollbackAsync();
                return NotFound(new { error = "Không tìm thấy bản ghi cần xóa." });
            }

            await WriteAudit(connection, transaction, "DeletePermanent", resource, id);
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể xóa vĩnh viễn resource={Resource} id={Id}", resource, id);
            return SqlConflict(ex);
        }
    }

    public sealed record BookAuthorsRequest(int[]? AuthorIds);

    // Danh sách mã tác giả hiện tại của một đầu sách.
    [HttpGet("books/{id:int}/authors")]
    public async Task<IActionResult> GetBookAuthors(int id)
    {
        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT AuthorId FROM BookAuthors WHERE BookId=@id ORDER BY AuthorId", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        var ids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        return Ok(new { authorIds = ids });
    }

    // Thay toàn bộ danh sách tác giả của đầu sách trong một transaction (bảng nối nhiều-nhiều BookAuthors).
    [HttpPut("books/{id:int}/authors")]
    public async Task<IActionResult> SetBookAuthors(int id, [FromBody] BookAuthorsRequest request)
    {
        var authorIds = (request.AuthorIds ?? []).Distinct().ToArray();
        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using (var exists = new SqlCommand("SELECT COUNT(*) FROM BookTitles WHERE BookId=@id", connection, transaction))
            {
                exists.Parameters.Add("@id", SqlDbType.Int).Value = id;
                if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { error = "Không tìm thấy đầu sách." });
                }
            }
            await using (var clear = new SqlCommand("DELETE FROM BookAuthors WHERE BookId=@id", connection, transaction))
            {
                clear.Parameters.Add("@id", SqlDbType.Int).Value = id;
                await clear.ExecuteNonQueryAsync();
            }
            foreach (var authorId in authorIds)
            {
                await using var add = new SqlCommand("INSERT INTO BookAuthors(BookId,AuthorId) VALUES(@book,@author)", connection, transaction);
                add.Parameters.Add("@book", SqlDbType.Int).Value = id;
                add.Parameters.Add("@author", SqlDbType.Int).Value = authorId;
                await add.ExecuteNonQueryAsync();
            }
            await WriteAudit(connection, transaction, "SetAuthors", "books", id);
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            await transaction.RollbackAsync();
            return Conflict(new { error = "Có mã tác giả không tồn tại." });
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Không thể gán tác giả cho sách {BookId}", id);
            return SqlConflict(ex);
        }
    }

    private bool TryReadValues(Resource definition, JsonElement body, out object?[] values, out string? error)
    {
        values = new object?[definition.Fields.Length];
        error = null;
        if (body.ValueKind != JsonValueKind.Object)
        {
            error = "Dữ liệu gửi lên không hợp lệ.";
            return false;
        }

        try
        {
            for (var i = 0; i < definition.Fields.Length; i++)
            {
                var field = definition.Fields[i];
                if (!body.TryGetProperty(field.JsonName, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    if (field.DefaultValue is not null) values[i] = field.DefaultValue;
                    else if (field.Required) throw new ArgumentException($"Thiếu trường bắt buộc: {field.JsonName}.");
                    continue;
                }

                values[i] = field.Type switch
                {
                    SqlDbType.Int => value.GetInt32(),
                    SqlDbType.Bit => value.GetBoolean(),
                    SqlDbType.Date => value.GetDateTime().Date,
                    SqlDbType.NVarChar or SqlDbType.VarChar => ReadString(value, field),
                    _ => throw new ArgumentException($"Kiểu dữ liệu chưa được hỗ trợ: {field.JsonName}.")
                };

                if (definition.Table == "Readers" && field.JsonName == "phone" && values[i] is string phone &&
                    (phone.Length != 10 || phone.Any(character => character is < '0' or > '9')))
                    throw new ArgumentException("Số điện thoại phải gồm đúng 10 chữ số.");
            }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ReadString(JsonElement value, Field field)
    {
        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (field.Required) throw new ArgumentException($"Trường {field.JsonName} không được để trống.");
            return null;
        }
        if (field.Size > 0 && text.Length > field.Size)
            throw new ArgumentException($"Trường {field.JsonName} vượt quá {field.Size} ký tự.");
        if (field.Allowed is not null && !field.Allowed.Contains(text, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Trường {field.JsonName} chỉ nhận: {string.Join(", ", field.Allowed)}.");
        return text;
    }

    private static void AddParameters(SqlCommand command, Resource definition, object?[] values)
    {
        for (var i = 0; i < definition.Fields.Length; i++)
        {
            var field = definition.Fields[i];
            var parameter = new SqlParameter($"@{field.JsonName}", field.Type) { Value = values[i] ?? DBNull.Value };
            if (field.Size != 0) parameter.Size = field.Size;
            command.Parameters.Add(parameter);
        }
    }

    private static bool IsReaderInactive(Resource definition, object?[] values)
    {
        var statusIndex = Array.FindIndex(definition.Fields, field => field.JsonName == "status");
        var expiryIndex = Array.FindIndex(definition.Fields, field => field.JsonName == "expiryDate");
        return (statusIndex >= 0 &&
                !string.Equals(values[statusIndex] as string, "Active", StringComparison.OrdinalIgnoreCase)) ||
               (expiryIndex >= 0 && values[expiryIndex] is DateTime expiryDate &&
                expiryDate.Date < DateTime.Today);
    }

    private static async Task RevokeReaderSessionsAsync(
        SqlConnection connection, SqlTransaction transaction, int readerId)
    {
        await using var command = new SqlCommand(
            "DELETE FROM dbo.AdminSessions WHERE ReaderId=@readerId",
            connection,
            transaction);
        command.Parameters.Add("@readerId", SqlDbType.Int).Value = readerId;
        await command.ExecuteNonQueryAsync();
    }

    private async Task WriteAudit(SqlConnection connection, SqlTransaction transaction, string action, string entity, int id)
    {
        await using var command = new SqlCommand(
            "INSERT INTO AuditLogs(Username,Action,Entity,EntityId) VALUES(@username,@action,@entity,@id)", connection, transaction);
        command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = User.Identity?.Name ?? "unknown";
        command.Parameters.Add("@action", SqlDbType.NVarChar, 100).Value = action;
        command.Parameters.Add("@entity", SqlDbType.NVarChar, 100).Value = entity;
        command.Parameters.Add("@id", SqlDbType.NVarChar, 80).Value = id.ToString();
        await command.ExecuteNonQueryAsync();
    }

    private IActionResult SqlConflict(SqlException ex) => ex.Number switch
    {
        2601 or 2627 => Conflict(new { error = "Giá trị bị trùng với dữ liệu đã có." }),
        547 => Conflict(new { error = "Không thể xóa dữ liệu đang được mục khác sử dụng." }),
        _ => StatusCode(500, new { error = "Không thể lưu dữ liệu lúc này. Hãy xem log API để biết chi tiết." })
    };
}
