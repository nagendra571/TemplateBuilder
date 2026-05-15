namespace TemplateBuilder.Web.Models;

public record PreviewRequest(string Body, string? ModelJson);

public record ValidateRequest(string Body);
