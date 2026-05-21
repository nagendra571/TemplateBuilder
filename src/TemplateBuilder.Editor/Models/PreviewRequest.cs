namespace TemplateBuilder.Editor.Models;

public record PreviewRequest(string Body, string? ModelJson);

public record ValidateRequest(string Body);
