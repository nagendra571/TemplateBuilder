using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTemplateBuilder(
        this IServiceCollection services,
        Action<TemplateBuilderOptions> configure)
    {
        // Eager validation: fail fast at registration time rather than first resolve
        var snapshot = new TemplateBuilderOptions();
        configure(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.ConnectionString))
            throw new InvalidOperationException(
                "TemplateBuilder ConnectionString must be set in the options delegate. " +
                "Example: services.AddTemplateBuilder(o => o.ConnectionString = config.GetConnectionString(\"TemplateDb\"));");

        // Register options (configure delegate is invoked again by the options system at first resolve)
        services.AddOptions<TemplateBuilderOptions>()
            .Configure(configure)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                "TemplateBuilder ConnectionString must not be empty.");

        // DbContext reads connection string from IOptions at resolution time (single source of truth)
        services.AddDbContext<TemplateBuilderDbContext>((sp, dbOptions) =>
        {
            var opts = sp.GetRequiredService<IOptions<TemplateBuilderOptions>>().Value;
            dbOptions.UseSqlServer(opts.ConnectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));
        });

        services.TryAddScoped<ITemplateRepository, TemplateRepository>();
        services.TryAddScoped<ISnippetRepository, SnippetRepository>();
        services.AddMemoryCache();
        services.TryAddScoped<ITemplateEngine, TemplateEngine>();

        return services;
    }
}
