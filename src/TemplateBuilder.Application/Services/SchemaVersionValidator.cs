using Microsoft.Data.SqlClient;
using TemplateBuilder.Domain.Exceptions;

namespace TemplateBuilder.Application.Services;

public class SchemaVersionValidator
{
    public static async Task ValidateAsync(string connectionString, string requiredMigrationId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM __EFMigrationsHistory WHERE MigrationId = @id", conn);
        cmd.Parameters.AddWithValue("@id", requiredMigrationId);
        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;
        if (count == 0)
            throw new SchemaVersionMismatchException(requiredMigrationId);
    }
}
