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
        var template = await _repository.GetByIdAsync(templateId, ct);
        if (template is null)
            throw new TemplateNotFoundException(templateId);
        if (!template.IsActive)
            throw new TemplateInactiveException(templateId);

        var activeVersion = await _repository.GetLastActiveVersionAsync(templateId, ct)
            ?? throw new NoActiveVersionException(templateId);

        var body = await GetBodyAsync(templateId, activeVersion.Id, ct);
        return await RenderBodyAsync(body, model, ct);
    }

    public async Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default)
    {
        var template = await _repository.GetByNameAsync(templateName, ct);
        if (template is null)
            throw new TemplateNotFoundException(templateName);
        if (!template.IsActive)
            throw new TemplateInactiveException(template.Id);

        var activeVersion = await _repository.GetLastActiveVersionAsync(template.Id, ct)
            ?? throw new NoActiveVersionException(template.Id);

        var body = await GetBodyAsync(template.Id, activeVersion.Id, ct);
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

        ScriptObject wrapper;
        if (_options.AllowTopLevelModelAccess)
        {
            // Import the same model members at global scope so `{{ X }}` works alongside
            // `{{ model.X }}`. If the model itself has a top-level member literally named
            // "model" (case-insensitively), the user's value wins — the wrapper is not set.
            var topLevel = new CaseInsensitiveScriptObject();
            topLevel.Import(model, filter: null, renamer: m => m.Name);
            bool hasModelKey = topLevel.Keys.Any(k => string.Equals(k, "model", StringComparison.OrdinalIgnoreCase));
            if (!hasModelKey)
                topLevel["model"] = scriptObject;
            wrapper = topLevel;
        }
        else
        {
            wrapper = new ScriptObject();
            wrapper["model"] = scriptObject;
        }

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

        // Check cache first — if version matches, return immediately
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached!.VersionId == currentVersionId)
            return cached.Body;

        // Version changed — evict stale entry so GetOrCreateAsync factory runs
        if (cached is not null)
            _cache.Remove(cacheKey);

        // Cache miss (or stale entry evicted) — fetch body and update cache
        // GetOrCreateAsync prevents stampede: concurrent misses for same key share one fetch
        var entry = await _cache.GetOrCreateAsync(cacheKey, async cacheEntry =>
        {
            cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheDurationMinutes);
            var body = await _repository.GetVersionBodyAsync(currentVersionId, ct)
                       ?? throw new TemplateNotFoundException(templateId);
            return new CacheEntry(currentVersionId, body);
        }) ?? throw new TemplateNotFoundException(templateId);

        return entry.Body;
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
                    return base.TryGetValue(context, span, key, out value);
            }

            value = null;
            return false;
        }
#nullable restore
    }
}
