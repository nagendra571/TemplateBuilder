using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
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
        services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();

        services.AddOptions<TemplateBuilderOptions>();
        services.AddMemoryCache();

        services.AddScoped<ITemplateEngine, TemplateEngine>();

        services.AddScoped<ISqlViewDiscoveryService>(sp =>
            new SqlViewDiscoveryService(
                connectionString,
                sp.GetRequiredService<IOptions<TemplateBuilderOptions>>()));

        services.AddHostedService<MigrationHostedService>();

        return services;
    }
}
