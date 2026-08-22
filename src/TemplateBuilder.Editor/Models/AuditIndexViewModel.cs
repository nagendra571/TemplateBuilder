using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Editor.Models;

public class AuditIndexViewModel
{
    public List<AuditLog> Rows { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? EntityType { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public AuditStats Stats { get; set; } = new();
    public IReadOnlyList<string> KnownActions { get; set; } = new List<string>();
}
