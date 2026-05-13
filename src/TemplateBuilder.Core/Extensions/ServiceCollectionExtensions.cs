using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var options = new TemplateBuilderOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException(
                "TemplateBuilder ConnectionString must be set in the options delegate. " +
                "Example: services.AddTemplateBuilder(o => o.ConnectionString = config.GetConnectionString(\"TemplateDb\"));");

        services.Configure(configure);

        services.AddDbContext<AppDbContext>(dbOptions =>
            dbOptions.UseSqlServer(options.ConnectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddMemoryCache();
        services.AddScoped<ITemplateEngine, TemplateEngine>();

        return services;
    }
}
