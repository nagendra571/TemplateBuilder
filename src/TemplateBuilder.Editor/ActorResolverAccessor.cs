using System;
using Microsoft.AspNetCore.Http;

namespace TemplateBuilder.Editor;

public sealed class ActorResolverAccessor
{
    public ActorResolverAccessor(Func<HttpContext, string?>? resolver) => Resolver = resolver;
    public Func<HttpContext, string?>? Resolver { get; }
}
