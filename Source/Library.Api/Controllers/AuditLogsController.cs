using Library.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles="Admin")]
public sealed class AuditLogsController(DbFactory db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string? q)
    {
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var command=new SqlCommand("""
            SELECT TOP(500) AuditId,Username,Action,Entity,EntityId,CreatedAt
            FROM AuditLogs
            WHERE (@q IS NULL OR Username LIKE @like OR Action LIKE @like OR Entity LIKE @like OR EntityId LIKE @like)
            ORDER BY CreatedAt DESC,AuditId DESC
            """,connection);
        command.Parameters.Add("@q",SqlDbType.NVarChar,100).Value=string.IsNullOrWhiteSpace(q)?DBNull.Value:q.Trim();
        command.Parameters.Add("@like",SqlDbType.NVarChar,102).Value=string.IsNullOrWhiteSpace(q)?DBNull.Value:$"%{q.Trim()}%";
        var rows=new List<object>();
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync()) rows.Add(new { auditId=reader.GetInt64(0),username=reader.GetString(1),action=reader.GetString(2),
            entity=reader.IsDBNull(3)?null:reader.GetString(3),entityId=reader.IsDBNull(4)?null:reader.GetString(4),createdAt=reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss") });
        return Ok(rows);
    }
}
