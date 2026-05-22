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

## Consuming TemplateBuilder.Editor — End to End

### 1. Create a new ASP.NET Core MVC app

```bash
dotnet new mvc -n MyApp
cd MyApp
```

### 2. Install the package

```bash
dotnet add package TemplateBuilder.Editor
```

### 3. Add a connection string

```json
// appsettings.json
{
  "ConnectionStrings": {
    "TemplateDb": "Server=.;Database=TemplateBuilder;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

> Point this at any SQL Server instance you have. The database and schema are created automatically on first run.

### 4. Register in Program.cs

```csharp
using TemplateBuilder.Editor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = builder.Configuration
        .GetConnectionString("TemplateDb")!;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets(); // serves /_content/TemplateBuilder.Editor/ assets

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### 5. Add a Templates link to your layout

Open `Views/Shared/_Layout.cshtml` and add a nav link:

```html
<li class="nav-item">
    <a class="nav-link text-dark" asp-controller="Templates" asp-action="Index">
        Templates
    </a>
</li>
```

The editor ships no `_ViewStart.cshtml` — your app's existing one applies your layout to the editor views automatically.

### 6. Run the app

```bash
dotnet run
```

On first startup EF Core migrations run automatically, creating the `TemplateBuilder` database and all required tables. Navigate to `/Templates` — the full editor appears inside your app's layout.

### 7. Create your first template

1. Click **+ New Template**
2. Enter a **Template Name** (e.g. `Welcome Email`) and select a **Type**
3. Write the body using Scriban syntax:
   ```html
   <h1>Welcome, {{ model.FirstName }}!</h1>
   <p>Thanks for signing up on {{ model.SignupDate }}.</p>
   ```
4. Click **Create Template**

Use the **Preview** button to enter sample JSON and see the rendered output live.

### 8. Render the template in your application code

Inject `ITemplateEngine` into any service or controller:

```csharp
using TemplateBuilder.Domain.Interfaces;

public class WelcomeEmailService
{
    private readonly ITemplateEngine _engine;

    public WelcomeEmailService(ITemplateEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> BuildEmailAsync(string firstName, DateTime signupDate)
    {
        var model = new { FirstName = firstName, SignupDate = signupDate };

        // Name matches exactly what you entered in the editor UI
        return await _engine.RenderByNameAsync("Welcome Email", model);
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<WelcomeEmailService>();
```

### What you get

| Capability | How |
|---|---|
| Create / edit templates | Navigate to `/Templates` in your browser |
| Version history & restore | Click **History** on the Edit page |
| Live preview with sample data | Click **Preview** → enter JSON → **Render** |
| Render to HTML in code | Inject `ITemplateEngine`, call `RenderByNameAsync` |
| Database schema | Created and migrated automatically on startup |
| Static assets | Served from `/_content/TemplateBuilder.Editor/` automatically |

> **Note:** `TemplateBuilder.Editor` includes everything `TemplateBuilder.Core` does. If you install Editor you do not need Core separately.

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
