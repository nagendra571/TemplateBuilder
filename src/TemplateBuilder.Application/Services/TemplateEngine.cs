using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Services;

public class TemplateEngine : ITemplateEngine
{
    private readonly ITemplateRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly TemplateBuilderOptions _options;

    private record CacheEntry(int VersionId, string Body);

    public TemplateEngine(ITemplateRepository repository, IMemoryCache cache, IOptions<TemplateBuilderOptions> options)
    {
        _repository = repository;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default)
    {
        var currentVersionId = await _repository.GetCurrentVersionIdAsync(templateId, ct)
            ?? throw new TemplateNotFoundException(templateId);

        var body = await GetBodyAsync(templateId, currentVersionId, ct);
        return await RenderBodyAsync(body, model, ct);
    }

    public async Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default)
    {
        var template = await _repository.GetByNameAsync(templateName, ct);
        if (template is null || !template.IsActive)
            throw new TemplateNotFoundException(templateName);

        // Lightweight version check for cache invalidation (per spec caching contract)
        var currentVersionId = await _repository.GetCurrentVersionIdAsync(template.Id, ct)
            ?? throw new TemplateNotFoundException(templateName);

        var body = await GetBodyAsync(template.Id, currentVersionId, ct);
        return await RenderBodyAsync(body, model, ct);
    }

    public async Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default)
    {
        var parsed = Template.Parse(body);
        if (parsed.HasErrors)
            throw new TemplateRenderException(
                string.Join("; ", parsed.Messages.Select(m => m.Message)));

        var scriptObject = new CaseInsensitiveScriptObject();
        scriptObject.Import(model, filter: null, renamer: m => m.Name);
        var wrapper = new ScriptObject();
        wrapper["model"] = scriptObject;
        var context = new TemplateContext { MemberRenamer = m => m.Name };
        context.PushGlobal(wrapper);

        try
        {
            return await parsed.RenderAsync(context);
        }
        catch (Exception ex)
        {
            throw new TemplateRenderException($"Render failed: {ex.Message}", ex);
        }
    }

    private async Task<string> GetBodyAsync(int templateId, int currentVersionId, CancellationToken ct)
    {
        if (!_options.EnableCaching)
        {
            return await _repository.GetVersionBodyAsync(currentVersionId, ct)
                   ?? throw new TemplateNotFoundException(templateId);
        }

        var cacheKey = $"tb_{templateId}";
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached!.VersionId == currentVersionId)
            return cached.Body;

        var body = await _repository.GetVersionBodyAsync(currentVersionId, ct)
                   ?? throw new TemplateNotFoundException(templateId);

        _cache.Set(cacheKey, new CacheEntry(currentVersionId, body),
            TimeSpan.FromMinutes(_options.CacheDurationMinutes));

        return body;
    }

    /// <summary>
    /// A <see cref="ScriptObject"/> that performs case-insensitive member lookup,
    /// allowing templates to access model properties regardless of case.
    /// </summary>
    private sealed class CaseInsensitiveScriptObject : ScriptObject
    {
#nullable disable
        public override bool TryGetValue(TemplateContext context, SourceSpan span, string member, out object value)
        {
            if (base.TryGetValue(context, span, member, out value))
                return true;

            foreach (var key in Keys)
            {
                if (string.Equals(key, member, StringComparison.OrdinalIgnoreCase))
                {
                    if (base.TryGetValue(context, span, key, out value))
                        return true;
                }
            }

            value = null;
            return false;
        }
#nullable restore
    }
}
