namespace TemplateBuilder.Web.Models;

public record SaveVersionRequest(
    string Name,
    string TemplateType,
    string? Description,
    string Body,
    string? ChangeComment);
