using Library.Api.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Security;

public sealed class SqlAdminSessionRepository(DbFactory db) : IAdminSessionRepository
{
    public async Task SaveAsync(byte[] tokenHash, AdminSession session, CancellationToken cancellationToken)
    {
        await using var connection = db.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.AdminSessions WHERE ExpiresAt <= @now; " +
            "INSERT INTO dbo.AdminSessions(TokenHash,Username,Role,ExpiresAt,ReaderId) " +
            "VALUES(@tokenHash,@username,@role,@expiresAt,@readerId)",
            connection);
        command.Parameters.Add("@now", SqlDbType.DateTimeOffset).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("@tokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = session.Username;
        command.Parameters.Add("@role", SqlDbType.VarChar, 20).Value = session.Role;
        command.Parameters.Add("@expiresAt", SqlDbType.DateTimeOffset).Value = session.ExpiresAt;
        command.Parameters.Add("@readerId", SqlDbType.Int).Value = session.ReaderId is int readerId ? readerId : DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AdminSession?> FindAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = db.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT Username,Role,ExpiresAt,ReaderId FROM dbo.AdminSessions WHERE TokenHash=@tokenHash",
            connection);
        command.Parameters.Add("@tokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new AdminSession(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDateTimeOffset(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    public async Task DeleteAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = db.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.AdminSessions WHERE TokenHash=@tokenHash",
            connection);
        command.Parameters.Add("@tokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteUserSessionsAsync(string username, CancellationToken cancellationToken)
    {
        await using var connection = db.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE FROM dbo.AdminSessions WHERE Username=@username",
            connection);
        command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = username;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
