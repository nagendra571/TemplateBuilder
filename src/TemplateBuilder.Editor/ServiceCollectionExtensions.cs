using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Authorization;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Editor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTemplateBuilderEditor(
        this IServiceCollection services,
        Action<TemplateBuilderEditorOptions> configure)
    {
        var options = new TemplateBuilderEditorOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException(
                "TemplateBuilder.Editor requires a connection string. " +
                "Set options.ConnectionString in AddTemplateBuilderEditor().");

        var connectionString = options.ConnectionString;

        services.AddDbContext<TemplateBuilderDbContext>(db =>
            db.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ISnippetRepository, SnippetRepository>();
        services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();

        services.AddOptions<TemplateBuilderOptions>();
        services.AddMemoryCache();

        services.AddScoped<ITemplateEngine, TemplateEngine>();

        services.AddScoped<ISqlViewDiscoveryService>(sp =>
            new SqlViewDiscoveryService(
                connectionString,
                sp.GetRequiredService<IOptions<TemplateBuilderOptions>>()));

        services.AddScoped<ISampleDataGenerator, SampleDataGenerator>();

        services.AddHostedService<MigrationHostedService>();

        // ── Authorization ──────────────────────────────────────────────────
        var auth = options.Authorization;
        bool useCustomPolicy = !string.IsNullOrWhiteSpace(auth.PolicyName);
        bool isSecured = useCustomPolicy || auth.Mode != TemplateBuilderAuthorizationMode.Anonymous;

        if (isSecured)
        {
            const string builtInPolicy = "TemplateBuilder.Access";
            string effectivePolicy = useCustomPolicy ? auth.PolicyName! : builtInPolicy;

            if (!useCustomPolicy)
            {
                services.AddAuthorization(o => o.AddPolicy(builtInPolicy, pb =>
                {
                    if (auth.Mode == TemplateBuilderAuthorizationMode.Authenticated)
                    {
                        pb.RequireAuthenticatedUser();
                    }
                    else if (auth.Mode == TemplateBuilderAuthorizationMode.Role)
                    {
                        if (auth.RoleNames is not { Length: > 0 })
                            throw new InvalidOperationException(
                                "TemplateBuilder.Editor: Authorization.RoleNames must contain at least one role when Mode is Role.");
                        pb.RequireRole(auth.RoleNames);
                    }
                }));
            }

            services.Configure<MvcOptions>(o =>
                o.Conventions.Add(new TemplateBuilderControllerConvention(effectivePolicy)));
        }

        return services;
    }
}
