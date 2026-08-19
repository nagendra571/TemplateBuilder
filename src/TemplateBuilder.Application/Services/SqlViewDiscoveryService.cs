using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.DTOs;
using TemplateBuilder.Application.Options;

namespace TemplateBuilder.Application.Services;

public class SqlViewDiscoveryService : ISqlViewDiscoveryService
{
    private readonly string _connectionString;
    private readonly TemplateBuilderOptions _options;

    private static readonly IReadOnlySet<string> ExcludedSchemas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sys", "INFORMATION_SCHEMA", "guest" };

    private static readonly string ExcludedSchemaSql =
        string.Join(",", ExcludedSchemas.Select(s => $"'{s}'"));

    public SqlViewDiscoveryService(string connectionString, IOptions<TemplateBuilderOptions> options)
    {
        _connectionString = connectionString;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetViewNamesAsync(CancellationToken ct = default)
    {
        if (_options.ViewAllowlist is not null)
            return _options.ViewAllowlist.ToList().AsReadOnly();

        var prefixes = _options.ViewPrefix
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        string prefixFilter;
        if (prefixes.Count == 0)
        {
            prefixFilter = "1=1";
        }
        else
        {
            var clauses = prefixes.Select((_, i) => $"TABLE_NAME LIKE @p{i} + '%'");
            prefixFilter = "(" + string.Join(" OR ", clauses) + ")";
        }

        await using var cmd = new SqlCommand(
            $@"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS
              WHERE {prefixFilter}
              AND TABLE_SCHEMA NOT IN ({ExcludedSchemaSql})
              ORDER BY TABLE_NAME", conn);

        for (int i = 0; i < prefixes.Count; i++)
            cmd.Parameters.AddWithValue($"@p{i}", EscapeLikePattern(prefixes[i]));

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
            $@"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = @viewName
              AND TABLE_SCHEMA NOT IN ({ExcludedSchemaSql})
              ORDER BY ORDINAL_POSITION", conn);
        cmd.Parameters.AddWithValue("@viewName", viewName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var columns = new List<SqlColumnInfo>();
        while (await reader.ReadAsync(ct))
            columns.Add(new SqlColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                MaxLength: reader.IsDBNull(2) ? null : reader.GetInt32(2),
                IsNullable: reader.GetString(3) == "YES"));
        return columns;
    }

    /// <summary>Escapes SQL LIKE metacharacters in <paramref name="value"/> so it is treated as a literal prefix.</summary>
    internal static string EscapeLikePattern(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
