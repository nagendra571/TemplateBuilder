using System;
using Microsoft.AspNetCore.Http;

namespace TemplateBuilder.Editor;

internal static class ActorResolverChain
{
    private const int MaxActorLength = 200;
    private const string CacheKey = "TemplateBuilder.Editor.Actor";

    public static string Resolve(Func<HttpContext, string?>? resolver, string? identityName, HttpContext? httpContext)
    {
        if (httpContext?.Items[CacheKey] is string cached)
            return cached;
        var actor = resolver?.Invoke(httpContext!);
        if (string.IsNullOrWhiteSpace(actor))
            actor = identityName;
        if (string.IsNullOrWhiteSpace(actor))
            actor = "anonymous";
        var result = actor.Length <= MaxActorLength ? actor : actor.Substring(0, MaxActorLength);
        if (httpContext is not null)
            httpContext.Items[CacheKey] = result;
        return result;
    }
}
