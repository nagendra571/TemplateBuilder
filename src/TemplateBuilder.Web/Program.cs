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
