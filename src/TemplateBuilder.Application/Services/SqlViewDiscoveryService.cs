using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.DTOs;
using TemplateBuilder.Application.Options;

namespace TemplateBuilder.Application.Services;

public class SqlViewDiscoveryService
{
    private readonly string _connectionString;
    private readonly TemplateBuilderOptions _options;

    private static readonly IReadOnlySet<string> ExcludedSchemas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sys", "INFORMATION_SCHEMA", "guest" };

    public SqlViewDiscoveryService(string connectionString, IOptions<TemplateBuilderOptions> options)
    {
        _connectionString = connectionString;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetViewNamesAsync(CancellationToken ct = default)
    {
        if (_options.ViewAllowlist is not null)
            return _options.ViewAllowlist.ToList().AsReadOnly();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS
              WHERE TABLE_NAME LIKE @prefix + '%'
              AND TABLE_SCHEMA NOT IN ('sys','INFORMATION_SCHEMA','guest')
              ORDER BY TABLE_NAME", conn);
        cmd.Parameters.AddWithValue("@prefix", _options.ViewPrefix);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var views = new List<string>();
        while (await reader.ReadAsync(ct))
            views.Add(reader.GetString(0));
        return views;
    }

    public async Task<IReadOnlyList<SqlColumnInfo>> GetViewColumnsAsync(string viewName, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            @"SELECT COLUMN_NAME, DATA_TYPE
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = @viewName
              ORDER BY ORDINAL_POSITION", conn);
        cmd.Parameters.AddWithValue("@viewName", viewName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var columns = new List<SqlColumnInfo>();
        while (await reader.ReadAsync(ct))
            columns.Add(new SqlColumnInfo(reader.GetString(0), reader.GetString(1)));
        return columns;
    }
}
