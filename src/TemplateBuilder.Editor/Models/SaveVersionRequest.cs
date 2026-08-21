namespace TemplateBuilder.Editor.Models;

public record SaveVersionRequest(
    string Name,
    string TemplateType,
    string? Description,
    string Body,
    string? ChangeComment,
    bool? IsActive = null,
    string? SourceView = null);
