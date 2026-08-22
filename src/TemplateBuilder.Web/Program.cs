using TemplateBuilder.Editor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("TemplateDb")
    ?? throw new InvalidOperationException(
        "Connection string 'TemplateDb' not found. " +
        "Verify appsettings.json or the CONNECTIONSTRINGS__TEMPLATEDB environment variable.");

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = connectionString;
    // Demo of ActorResolver: any custom identity logic. Reads an optional X-TB-Actor header
    // so the flow is verifiable end-to-end (curl -H "X-TB-Actor: alice" ...). In a real app
    // resolve from claims/session instead — a raw header is spoofable and is demo-only here.
    options.ActorResolver = ctx =>
    {
        var header = ctx?.Request.Headers["X-TB-Actor"].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Templates}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
