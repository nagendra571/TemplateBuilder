namespace TemplateBuilder.Editor.Models;

public record PreviewRequest(string Body, string? ModelJson, string? Subject = null);

public record ValidateRequest(string Body);
