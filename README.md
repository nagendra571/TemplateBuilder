# TemplateBuilder

A Scriban-powered HTML template management system for .NET. Ships two independent NuGet packages:

| Package | Purpose |
|---|---|
| `TemplateBuilder.Core` | Render templates to HTML strings — lightweight, no UI |
| `TemplateBuilder.Editor` | Full management UI (create, edit, version history, preview) embedded in your app via a Razor Class Library |

---

## Packages

### TemplateBuilder.Editor

Embed the full template editor into any .NET 10 web application. Install the package, register services, and navigate to `/Templates` — the editor appears wrapped in your own layout.

### TemplateBuilder.Core

Render a saved template to an HTML string from any .NET project (web, worker, console). No UI dependencies.

---

## Packaging

### Prerequisites

- .NET 10 SDK
- (For publishing) a NuGet API key from [nuget.org](https://www.nuget.org)

### Pack locally

```bash
# Pack TemplateBuilder.Editor
dotnet pack src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj \
  --configuration Release \
  --output ./nupkgs

# Pack TemplateBuilder.Core
dotnet pack src/TemplateBuilder.Core/TemplateBuilder.Core.csproj \
  --configuration Release \
  --output ./nupkgs
```

The `.nupkg` files appear in `./nupkgs/`.

### Bump the version before publishing

Edit the `<Version>` element in the `.csproj` before packing:

```xml
<!-- src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -->
<Version>1.1.0</Version>
```

### Publish to NuGet.org

```bash
dotnet nuget push ./nupkgs/TemplateBuilder.Editor.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push ./nupkgs/TemplateBuilder.Core.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

### Publish to a private feed (Azure Artifacts / GitHub Packages)

```bash
dotnet nuget push ./nupkgs/TemplateBuilder.Editor.1.0.0.nupkg \
  --api-key <YOUR_API_KEY> \
  --source https://pkgs.dev.azure.com/<org>/<project>/_packaging/<feed>/nuget/v3/index.json
```

---

## Consuming TemplateBuilder.Editor

### 1. Install the package

```bash
dotnet add package TemplateBuilder.Editor
```

### 2. Add a connection string

```json
// appsettings.json
{
  "ConnectionStrings": {
    "TemplateDb": "Server=.;Database=TemplateBuilder;Trusted_Connection=True;"
  }
}
```

### 3. Register services in Program.cs

```csharp
using TemplateBuilder.Editor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();  // required — serves /_content/TemplateBuilder.Editor/ assets

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### 4. Apply your layout to the editor views

The editor ships no `_ViewStart.cshtml`. Your app's existing `_ViewStart.cshtml` applies your layout automatically:

```csharp
// Views/_ViewStart.cshtml  (already present in most MVC apps)
@{
    Layout = "_Layout";
}
```

Add a "Templates" link to your nav in `_Layout.cshtml`:

```html
<a asp-controller="Templates" asp-action="Index">Templates</a>
```

### 5. Database setup

`AddTemplateBuilderEditor()` registers a hosted service that runs EF Core migrations on startup. The schema is created or updated automatically — no manual steps required.

### 6. Navigate to the editor

Start your app and go to `/Templates`. The full editor is now embedded in your site.

---

## Consuming TemplateBuilder.Core

### 1. Install the package

```bash
dotnet add package TemplateBuilder.Core
```

### 2. Register services in Program.cs

```csharp
using TemplateBuilder.Core;

builder.Services.AddTemplateBuilderCore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});
```

### 3. Render a template

```csharp
public class InvoiceService
{
    private readonly ITemplateEngine _engine;
    private readonly ITemplateRepository _repo;

    public InvoiceService(ITemplateEngine engine, ITemplateRepository repo)
    {
        _engine = engine;
        _repo = repo;
    }

    public async Task<string> RenderInvoiceAsync(InvoiceModel invoice)
    {
        var template = await _repo.GetByNameAsync("Invoice Email");
        return await _engine.RenderAsync(template!, invoice);
    }
}
```

---

## Updating to a new version

When a new version of `TemplateBuilder.Editor` is published:

```bash
dotnet add package TemplateBuilder.Editor --version <new-version>
```

EF migrations are bundled in the package — schema changes are applied automatically on next startup.

---

## Development

### Run the thin host (dev/demo app)

```bash
dotnet run --project src/TemplateBuilder.Web
```

Navigate to `https://localhost:7275/Templates`.

### Run all tests

```bash
dotnet test
```

### Solution structure

```
src/
├── TemplateBuilder.Domain/          # Entities, interfaces
├── TemplateBuilder.Infrastructure/  # EF Core DbContext, migrations, repositories
├── TemplateBuilder.Application/     # Scriban engine, services
├── TemplateBuilder.Core/            # NuGet: render-only
├── TemplateBuilder.Editor/          # NuGet: full management UI (RCL)
└── TemplateBuilder.Web/             # Thin dev/demo host

tests/
├── TemplateBuilder.Domain.Tests/
├── TemplateBuilder.Application.Tests/
├── TemplateBuilder.Infrastructure.Tests/
└── TemplateBuilder.Editor.Tests/
```
