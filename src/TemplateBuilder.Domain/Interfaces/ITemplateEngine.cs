namespace TemplateBuilder.Domain.Interfaces;

public interface ITemplateEngine
{
    Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);
    Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);
    Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default);
    Task<RenderedEmail> RenderEmailAsync(int templateId, object model, CancellationToken ct = default);
}

public class RenderedEmail
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
