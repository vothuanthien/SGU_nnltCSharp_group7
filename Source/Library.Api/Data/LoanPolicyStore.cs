using System.Globalization;
using Library.Domain;
using Microsoft.Data.SqlClient;

namespace Library.Api.Data;

/// <summary>Đọc cấu hình mượn/phạt từ bảng Settings để mọi API dùng chung một LoanPolicy của lớp Domain.</summary>
public static class LoanPolicyStore
{
    public static async Task<LoanPolicy> LoadAsync(SqlConnection connection, SqlTransaction transaction)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(
            "SELECT [Key], Value FROM Settings WHERE [Key] IN (N'LoanDays', N'MaxLoans', N'FinePerDay')", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) values[reader.GetString(0)] = reader.GetString(1);
        }

        foreach (var key in new[] { "LoanDays", "MaxLoans", "FinePerDay" })
            if (!values.ContainsKey(key)) throw new InvalidOperationException($"Chưa cấu hình '{key}' trong bảng Settings.");

        try
        {
            return new LoanPolicy(
                int.Parse(values["LoanDays"], CultureInfo.InvariantCulture),
                int.Parse(values["MaxLoans"], CultureInfo.InvariantCulture),
                decimal.Parse(values["FinePerDay"], CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Giá trị cấu hình mượn/phạt trong bảng Settings không hợp lệ.");
        }
    }
}
