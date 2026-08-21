using System.Text.Json;
using Scriban.Syntax;
using TemplateBuilder.Application.DTOs;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Services;

/// <summary>
/// Compares a template's <c>model.*</c> token usage (extracted from the Scriban AST) against
/// its bound SQL source view — both the live schema and a stored point-in-time snapshot — and
/// reports drift as a set of <see cref="HealthFinding"/>s.
/// </summary>
public class TemplateHealthService : ITemplateHealthService
{
    private static readonly JsonSerializerOptions CamelJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions SnapshotReadJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITemplateRepository _templateRepository;
    private readonly ISqlViewDiscoveryService _sqlViewDiscoveryService;

    public TemplateHealthService(ITemplateRepository templateRepository, ISqlViewDiscoveryService sqlViewDiscoveryService)
    {
        _templateRepository = templateRepository;
        _sqlViewDiscoveryService = sqlViewDiscoveryService;
    }

    public async Task<TemplateHealthReport> CheckAsync(int templateId, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(templateId, ct)
            ?? throw new TemplateNotFoundException(templateId);

        var body = template.CurrentVersion?.Body ?? string.Empty;
        var tokens = await ExtractModelPathsAsync(body, ct);

        var report = new TemplateHealthReport
        {
            TemplateId = templateId,
            SourceView = template.SourceView,
            Tokens = tokens
        };

        if (string.IsNullOrWhiteSpace(template.SourceView))
        {
            if (tokens.Count > 0)
            {
                report.Findings.Add(new HealthFinding
                {
                    Severity = HealthSeverity.Warning,
                    Code = "unbound_tokens",
                    Message = $"Template references {tokens.Count} model token(s) but is not bound to a source view."
                });
            }

            return report;
        }

        var liveColumns = await _sqlViewDiscoveryService.GetViewColumnsAsync(template.SourceView, ct);

        if (liveColumns.Count == 0)
        {
            report.ViewMissing = true;
            report.Findings.Add(new HealthFinding
            {
                Severity = HealthSeverity.Critical,
                Code = "view_missing",
                Message = $"Source view '{template.SourceView}' was not found."
            });
            return report;
        }

        var liveByName = liveColumns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var snapshot = ParseSnapshot(template.SourceViewSnapshot);
        report.SnapshotTakenAt = snapshot?.TakenAt;
        var snapshotByName = snapshot?.Columns?.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, SqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

        var columnNames = tokens
            .Select(t => t.Contains('.') ? t[..t.IndexOf('.')] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var columnName in columnNames)
        {
            if (!liveByName.TryGetValue(columnName, out var liveColumn))
            {
                report.Findings.Add(new HealthFinding
                {
                    Severity = HealthSeverity.Critical,
                    Code = "column_missing",
                    Message = $"Column '{columnName}' referenced by the template was not found in view '{template.SourceView}'."
                });
                continue;
            }

            if (!snapshotByName.TryGetValue(columnName, out var snapshotColumn))
            {
                continue;
            }

            if (!string.Equals(liveColumn.DataType, snapshotColumn.DataType, StringComparison.OrdinalIgnoreCase))
            {
                report.Findings.Add(new HealthFinding
                {
                    Severity = HealthSeverity.Warning,
                    Code = "column_type_changed",
                    Message = $"Column '{columnName}' type changed from '{snapshotColumn.DataType}' to '{liveColumn.DataType}'."
                });
                continue;
            }

            if (liveColumn.MaxLength != snapshotColumn.MaxLength)
            {
                report.Findings.Add(new HealthFinding
                {
                    Severity = HealthSeverity.Warning,
                    Code = "column_length_changed",
                    Message = $"Column '{columnName}' max length changed from '{Describe(snapshotColumn.MaxLength)}' to '{Describe(liveColumn.MaxLength)}'."
                });
            }

            if (liveColumn.IsNullable != snapshotColumn.IsNullable)
            {
                report.Findings.Add(new HealthFinding
                {
                    Severity = HealthSeverity.Warning,
                    Code = "column_nullability_changed",
                    Message = $"Column '{columnName}' nullability changed from '{snapshotColumn.IsNullable}' to '{liveColumn.IsNullable}'."
                });
            }
        }

        return report;
    }

    public Task<IReadOnlyList<string>> ExtractModelPathsAsync(string body, CancellationToken ct = default)
    {
        IReadOnlyList<string> result = Array.Empty<string>();

        if (!string.IsNullOrEmpty(body))
        {
            var parsed = Scriban.Template.Parse(body);
            if (parsed.Page is not null)
            {
                var paths = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                Visit(parsed.Page, paths, seen);

                result = paths;
            }
        }

        return Task.FromResult(result);
    }

    public async Task<string> BuildSnapshotJsonAsync(string viewName, CancellationToken ct = default)
    {
        var columns = await _sqlViewDiscoveryService.GetViewColumnsAsync(viewName, ct);
        return JsonSerializer.Serialize(new { takenAt = DateTime.UtcNow, columns }, CamelJson);
    }

    /// <summary>
    /// Walks the Scriban AST looking for member-access expressions rooted at the "model" variable
    /// (e.g. <c>model.FirstName</c>, <c>model.User.Name</c>) and collects the dotted path with the
    /// "model." prefix stripped. String literals, loop/local variables, and any other expression
    /// kind are ignored — only real <see cref="ScriptMemberExpression"/> chains are inspected, so
    /// text that merely looks like "model.X" inside a string literal is never matched.
    /// </summary>
    private static void Visit(ScriptNode node, List<string> paths, HashSet<string> seen)
    {
        if (node is ScriptMemberExpression memberExpression)
        {
            var fullPath = GetVariablePath(memberExpression);
            if (fullPath is not null)
            {
                const string prefix = "model.";
                if (fullPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var path = fullPath[prefix.Length..];
                    if (path.Length > 0 && seen.Add(path))
                    {
                        paths.Add(path);
                    }
                }

                // The whole chain was made up of variables/members and has been fully consumed
                // above — nothing further to discover by recursing into Target/Member.
                return;
            }

            // Target isn't a plain variable/member chain (e.g. an indexer) — keep walking so any
            // model.* reference nested inside it (like arr[model.Index]) is still found.
        }

        foreach (var child in node.Children)
        {
            Visit(child, paths, seen);
        }
    }

    /// <summary>
    /// Resolves an expression that is purely a chain of variables and member-accesses
    /// (e.g. <c>model.User.Name</c>) into its dotted string form. Returns null for anything else
    /// (indexers, literals, function calls, etc.) so the caller knows not to treat it as a bound path.
    /// </summary>
    private static string? GetVariablePath(ScriptExpression? expression)
    {
        switch (expression)
        {
            case ScriptVariable variable:
                return variable.Name;
            case ScriptMemberExpression member when member.Member is not null:
                var targetPath = GetVariablePath(member.Target);
                return targetPath is null ? null : $"{targetPath}.{member.Member.Name}";
            default:
                return null;
        }
    }

    private static string Describe(int? value) => value?.ToString() ?? "null";

    private static SnapshotDto? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SnapshotDto>(json, SnapshotReadJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class SnapshotDto
    {
        public DateTime? TakenAt { get; set; }
        public List<SqlColumnInfo>? Columns { get; set; }
    }
}
