using Microsoft.Data.SqlClient;
using System.Data;

namespace Library.Api.Data;

internal static class ReaderNotificationStore
{
    public static async Task AddAsync(SqlConnection connection, SqlTransaction transaction, int readerId, string type, string message)
    {
        await using var command = new SqlCommand(
            "INSERT INTO ReaderNotifications(ReaderId,[Type],Message) VALUES(@reader,@type,@message)", connection, transaction);
        command.Parameters.Add("@reader", SqlDbType.Int).Value = readerId;
        command.Parameters.Add("@type", SqlDbType.VarChar, 30).Value = type;
        command.Parameters.Add("@message", SqlDbType.NVarChar, 500).Value = message;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task AuditAsync(SqlConnection connection, SqlTransaction transaction, string username,
        string action, string entity, string entityId)
    {
        await using var command = new SqlCommand(
            "INSERT INTO AuditLogs(Username,Action,Entity,EntityId) VALUES(@user,@action,@entity,@id)", connection, transaction);
        command.Parameters.Add("@user", SqlDbType.NVarChar, 80).Value = username;
        command.Parameters.Add("@action", SqlDbType.NVarChar, 100).Value = action;
        command.Parameters.Add("@entity", SqlDbType.NVarChar, 100).Value = entity;
        command.Parameters.Add("@id", SqlDbType.NVarChar, 80).Value = entityId;
        await command.ExecuteNonQueryAsync();
    }
}
