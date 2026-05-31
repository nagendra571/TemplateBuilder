using TemplateBuilder.Editor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(o =>
{
    // Non-nullable string properties (e.g. Body) in the RCL's models would otherwise
    // receive an implicit [Required] from ASP.NET Core's nullable-awareness, which
    // rejects empty strings and breaks the Create flow.
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapStaticAssets(); // serves /_content/TemplateBuilder.Editor/ assets

app.UseRouting();

app.UseAuthorization();

app.MapControllers(); // registers attribute-routed actions (Edit, Preview, SaveVersion, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
