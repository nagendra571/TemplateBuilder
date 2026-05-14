using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("TemplateDb")
    ?? throw new InvalidOperationException(
        "Connection string 'TemplateDb' not found. " +
        "Verify appsettings.json or the CONNECTIONSTRINGS__TEMPLATEDB environment variable.");

builder.Services.AddDbContext<TemplateBuilderDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();

// Bind TemplateBuilderOptions from config (uses defaults if section absent)
builder.Services.Configure<TemplateBuilderOptions>(
    builder.Configuration.GetSection("TemplateBuilder"));

// Memory cache needed by TemplateEngine
builder.Services.AddMemoryCache();

// Template rendering engine
builder.Services.AddScoped<ITemplateEngine, TemplateEngine>();

// SQL view discovery — design-time only, not used at render time
builder.Services.AddScoped(sp =>
    new SqlViewDiscoveryService(
        connectionString,
        sp.GetRequiredService<IOptions<TemplateBuilderOptions>>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
