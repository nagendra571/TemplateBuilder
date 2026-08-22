using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Controllers;

public class AuditController : Controller
{
    private readonly IAuditRepository _auditRepository;
    private readonly IAuditStatsRepository _statsRepository;

    public AuditController(IAuditRepository auditRepository, IAuditStatsRepository statsRepository)
    {
        _auditRepository = auditRepository;
        _statsRepository = statsRepository;
    }

    [HttpGet("Audit")]
    public async Task<IActionResult> Index(string? entityType, [FromQuery(Name = "action")] string? actionName, string? actor, string? from, string? to, string? search, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = BuildQuery(entityType, actionName, actor, from, to, search, page, pageSize);
        var rows = await _auditRepository.QueryAsync(query, ct);
        var total = await _auditRepository.CountAsync(query, ct);
        var stats = await _statsRepository.GetStatsAsync(query, ct);

        return View(new AuditIndexViewModel
        {
            Rows = rows.ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            Search = search,
            EntityType = entityType,
            Action = actionName,
            Actor = actor,
            From = from,
            To = to,
            Stats = stats,
            KnownActions = KnownActions()
        });
    }

    [HttpGet("Audit/Stats")]
    public async Task<IActionResult> Stats(string? entityType, [FromQuery(Name = "action")] string? actionName, string? actor, string? from, string? to, string? search, CancellationToken ct = default)
    {
        var query = BuildQuery(entityType, actionName, actor, from, to, search);
        var stats = await _statsRepository.GetStatsAsync(query, ct);
        return Ok(stats);
    }

    [HttpGet("Audit/Export")]
    public async Task<IActionResult> Export(string? entityType, [FromQuery(Name = "action")] string? actionName, string? actor, string? from, string? to, string? search, CancellationToken ct = default)
    {
        var query = BuildQuery(entityType, actionName, actor, from, to, search, page: 1, pageSize: int.MaxValue);
        var rows = await _auditRepository.QueryAsync(query, ct);

        var sb = new StringBuilder();
        sb.Append("OccurredAt,EntityType,EntityId,Action,Actor,Comment,BeforeState,AfterState\r\n");
        foreach (var row in rows)
        {
            sb.Append(Quote(row.OccurredAt.ToString("o")));
            sb.Append(',');
            sb.Append(Quote(row.EntityType));
            sb.Append(',');
            sb.Append(row.EntityId.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(Quote(row.Action));
            sb.Append(',');
            sb.Append(Quote(row.Actor));
            sb.Append(',');
            sb.Append(Quote(row.Comment));
            sb.Append(',');
            sb.Append(Quote(row.BeforeState));
            sb.Append(',');
            sb.Append(Quote(row.AfterState));
            sb.Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);

        return File(bytes, "text/csv", "template-builder-audit.csv");
    }

    private static IReadOnlyList<string> KnownActions() =>
        typeof(AuditActions).GetFields()
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    private static AuditQuery BuildQuery(string? entityType, string? actionName, string? actor, string? from, string? to, string? search, int page = 1, int pageSize = 25)
    {
        return new AuditQuery
        {
            EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType,
            Action = string.IsNullOrWhiteSpace(actionName) ? null : actionName,
            Actor = string.IsNullOrWhiteSpace(actor) ? null : actor,
            From = ParseDate(from),
            To = ParseToDate(to),
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Page = page,
            PageSize = pageSize
        };
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null;

    private static DateTime? ParseToDate(string? value)
    {
        var dt = ParseDate(value);
        return dt?.Date.AddDays(1).AddTicks(-1);
    }

    private static string Quote(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(new[] { '"', ',', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
