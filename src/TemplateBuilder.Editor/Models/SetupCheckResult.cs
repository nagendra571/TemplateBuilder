namespace TemplateBuilder.Editor.Models;

public record SetupCheckResult(
    string Name,
    string Description,
    bool Passed,
    string FixHint,
    string? Detail = null);
