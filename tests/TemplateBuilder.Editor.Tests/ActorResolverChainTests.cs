using System;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TemplateBuilder.Editor;
using Xunit;

namespace TemplateBuilder.Editor.Tests;

public class ActorResolverChainTests
{
    private static HttpContext Http() => new DefaultHttpContext();

    [Fact]
    public void Resolve_uses_resolver_result_when_non_blank()
    {
        var resolver = new Func<HttpContext, string?>(_ => "jdoe");
        ActorResolverChain.Resolve(resolver, "alice", Http()).Should().Be("jdoe");
    }

    [Fact]
    public void Resolve_falls_back_to_identity_name_when_resolver_returns_null()
    {
        var resolver = new Func<HttpContext, string?>(_ => null);
        ActorResolverChain.Resolve(resolver, "alice", Http()).Should().Be("alice");
    }

    [Fact]
    public void Resolve_falls_back_to_identity_name_when_resolver_returns_whitespace()
    {
        var resolver = new Func<HttpContext, string?>(_ => "   ");
        ActorResolverChain.Resolve(resolver, "alice", Http()).Should().Be("alice");
    }

    [Fact]
    public void Resolve_falls_back_to_identity_name_when_no_resolver()
    {
        ActorResolverChain.Resolve(null, "alice", Http()).Should().Be("alice");
    }

    [Fact]
    public void Resolve_uses_anonymous_when_resolver_and_identity_are_absent()
    {
        ActorResolverChain.Resolve(null, null, Http()).Should().Be("anonymous");
    }

    [Fact]
    public void Resolve_uses_anonymous_when_identity_name_is_whitespace()
    {
        ActorResolverChain.Resolve(null, "  ", Http()).Should().Be("anonymous");
    }

    [Fact]
    public void Resolve_passes_http_context_to_resolver()
    {
        var ctx = Http();
        HttpContext? received = null;
        var resolver = new Func<HttpContext, string?>(c => { received = c; return "bob"; });
        ActorResolverChain.Resolve(resolver, "alice", ctx);
        received.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Resolve_truncates_result_to_100_characters()
    {
        var longValue = new string('x', 150);
        ActorResolverChain.Resolve(null, longValue, Http()).Should().Be(new string('x', 100));
    }

    [Fact]
    public void Resolve_keeps_exactly_100_characters()
    {
        var value = new string('x', 100);
        ActorResolverChain.Resolve(null, value, Http()).Should().Be(value);
    }

    [Fact]
    public void Resolve_propagates_resolver_exceptions()
    {
        var resolver = new Func<HttpContext, string?>(_ => throw new InvalidOperationException("user store down"));
        var act = () => ActorResolverChain.Resolve(resolver, "alice", Http());
        act.Should().Throw<InvalidOperationException>().WithMessage("user store down");
    }

    [Fact]
    public void Resolve_caches_per_request_context()
    {
        var ctx = Http();
        var calls = 0;
        var resolver = new Func<HttpContext, string?>(_ => { calls++; return "bob"; });
        ActorResolverChain.Resolve(resolver, "alice", ctx);
        ActorResolverChain.Resolve(resolver, "alice", ctx);
        calls.Should().Be(1);
        ActorResolverChain.Resolve(resolver, "alice", Http()).Should().Be("bob");
        calls.Should().Be(2);
    }
}
