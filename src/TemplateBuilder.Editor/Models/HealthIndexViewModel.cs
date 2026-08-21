using TemplateBuilder.Application.Services;

namespace TemplateBuilder.Editor.Models;

public class HealthRowViewModel
{
    public int TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TemplateHealthReport Report { get; set; } = new();
}

public class HealthIndexViewModel
{
    public List<HealthRowViewModel> Rows { get; set; } = new();
}
