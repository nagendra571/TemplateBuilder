using Microsoft.AspNetCore.Mvc;
using System.Linq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Controllers;

public class HealthController : Controller
{
    private readonly ITemplateRepository _repository;
    private readonly ITemplateHealthService _health;

    public HealthController(ITemplateRepository repository, ITemplateHealthService health)
    {
        _repository = repository;
        _health = health;
    }

    [HttpGet("Health")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var templates = await _repository.GetAllIncludingInactiveAsync(ct);
        var rows = new List<HealthRowViewModel>();
        foreach (var t in templates)
            rows.Add(new HealthRowViewModel { TemplateId = t.Id, Name = t.Name, Report = await _health.CheckAsync(t.Id, ct) });
        return View(new HealthIndexViewModel { Rows = rows });
    }

    [HttpGet("Health/Summaries")]
    public async Task<IActionResult> Summaries(string? ids, CancellationToken ct = default)
    {
        var list = new List<object>();
        foreach (var raw in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(raw, out var id)) continue;
            var report = await _health.CheckAsync(id, ct);
            list.Add(new
            {
                templateId = id,
                severity = SeverityName(report.Worst),
                findingCount = report.Findings.Count(f => f.Severity != HealthSeverity.Info)
            });
        }
        return Ok(list);
    }

    private static string SeverityName(HealthSeverity s) => s switch
    {
        HealthSeverity.Critical => "critical",
        HealthSeverity.Warning => "warning",
        _ => "healthy"
    };
}
