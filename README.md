# TemplateBuilder

A Scriban-powered HTML template management system for .NET. Ships two independent NuGet packages:

| Package | Version | Purpose |
|---|---|---|
| [`TemplateBuilder.Editor`](https://www.nuget.org/packages/TemplateBuilder.Editor) | 1.1.7 | Full management UI — create, edit, version, preview, restore |
| [`TemplateBuilder.Core`](https://www.nuget.org/packages/TemplateBuilder.Core) | 1.0.3 | Render templates to HTML strings — lightweight, no UI |

> `TemplateBuilder.Editor` includes everything `TemplateBuilder.Core` does. If you install Editor you do not need Core separately.

---

## Consuming TemplateBuilder.Editor

### 1. Create an ASP.NET Core MVC app

```bash
dotnet new mvc -n MyApp && cd MyApp
dotnet add package TemplateBuilder.Editor --version 1.1.7
```

### 2. Add a connection string

```json
// appsettings.json
{
  "ConnectionStrings": {
    "TemplateDb": "Server=.;Database=TemplateBuilder;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Register in Program.cs

```csharp
using TemplateBuilder.Editor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    // Required: prevents empty template body from silently failing on Create
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();       // serves /_content/TemplateBuilder.Editor/ assets
app.UseRouting();
app.UseAuthorization();

app.MapControllers();       // registers attribute-routed endpoints (Edit, Preview, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### 4. Wire up the layout

In `Views/Shared/_Layout.cshtml`, add the two section hooks:

```html
<head>
    ...
    @await RenderSectionAsync("Styles", required: false)
</head>
<body>
    ...
    @RenderBody()
    ...
    @await RenderSectionAsync("Scripts", required: false)
</body>
```

Add a nav link to the editor:

```html
<a asp-controller="Templates" asp-action="Index">Templates</a>
```

### 5. Verify the setup

Start the app and navigate to **`/Templates/_setup`**.

This diagnostic page checks every integration requirement and shows a clear fix for each failure — database connection, migrations, routing, static assets, and layout configuration. It returns 404 in Production.

### 6. Create your first template

Navigate to `/Templates` → **+ New Template** → enter a name and body:

```html
<h1>Hello, {{ model.FirstName }}!</h1>
<p>Your order <strong>#{{ model.OrderId }}</strong> is confirmed.</p>
```

Click **Create Template**, then use the **Preview** button with sample JSON to see the live output.

### 7. Render templates in code

Inject `ITemplateEngine` anywhere:

```csharp
using TemplateBuilder.Domain.Interfaces;

public class OrderEmailService(ITemplateEngine engine)
{
    public Task<string> BuildAsync(int orderId, string firstName) =>
        engine.RenderByNameAsync("Order Confirmation", new { OrderId = orderId, FirstName = firstName });
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<OrderEmailService>();
```

---

## Consuming TemplateBuilder.Core

Use this when you only need to render templates in code (no editor UI).

```bash
dotnet add package TemplateBuilder.Core --version 1.0.3
```

```csharp
using TemplateBuilder.Core.Extensions;

builder.Services.AddTemplateBuilder(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});
```

```csharp
public class InvoiceService(ITemplateEngine engine)
{
    public Task<string> RenderAsync(InvoiceModel invoice) =>
        engine.RenderByNameAsync("Invoice", invoice);
}
```

Both packages share the same database schema — point them at the same connection string.

---

## What You Get

| Capability | Detail |
|---|---|
| **Create / edit** | Navigate to `/Templates` |
| **Version history & restore** | Click **History** on the Edit page |
| **Live preview** | Click **Preview** → enter JSON → **Render** |
| **Dark / Light theme** | Toggle in the CANVAS panel heading, persisted in localStorage |
| **Render in code** | Inject `ITemplateEngine`, call `RenderByNameAsync` |
| **Auto-migrations** | Database schema created and updated on startup |
| **Setup diagnostic** | `/Templates/_setup` — checks all integration requirements |
| **Scriban syntax** | `{{ model.X }}`, loops, conditionals, filters |

---

## Integration Checklist

| Requirement | Why |
|---|---|
| `app.MapControllers()` before `MapControllerRoute` | Registers attribute-routed endpoints (Edit, Preview, SaveVersion) |
| `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` | Allows creating templates with an empty body |
| `@await RenderSectionAsync("Styles", required: false)` in `<head>` | Loads SunEditor CSS and editor stylesheet |
| `@await RenderSectionAsync("Scripts", required: false)` before `</body>` | Loads SunEditor JS and editor initialisation script |
| `app.UseStaticFiles()` or `app.MapStaticAssets()` | Serves `/_content/TemplateBuilder.Editor/` static files |

Run `/Templates/_setup` to validate all of these automatically.

---

## Template Syntax

Templates use [Scriban](https://github.com/scriban/scriban). Model properties are accessed case-insensitively via `model.*`:

```html
<p>Hello <strong>{{ model.FirstName }}</strong>,</p>

{{ for item in model.Items }}
  <tr>
    <td>{{ item.Name }}</td>
    <td>{{ item.Price | math.format "C" }}</td>
  </tr>
{{ end }}

{{ if model.IsPremium }}
  <p>Premium member discount applied.</p>
{{ end }}
```

---

## Development

### Run the thin host

```bash
dotnet run --project src/TemplateBuilder.Web
```

Navigate to `https://localhost:7275/Templates`.

### Run tests

```bash
dotnet test
```

### Pack and publish

```bash
# Pack
dotnet pack src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
dotnet pack src/TemplateBuilder.Core/TemplateBuilder.Core.csproj -c Release

# Publish
dotnet nuget push src/TemplateBuilder.Editor/bin/Release/TemplateBuilder.Editor.1.1.7.nupkg \
  --api-key <KEY> --source https://api.nuget.org/v3/index.json

dotnet nuget push src/TemplateBuilder.Core/bin/Release/TemplateBuilder.Core.1.0.3.nupkg \
  --api-key <KEY> --source https://api.nuget.org/v3/index.json
```

### Solution structure

```
src/
├── TemplateBuilder.Domain/          # Entities, interfaces
├── TemplateBuilder.Infrastructure/  # EF Core DbContext, migrations, repositories
├── TemplateBuilder.Application/     # Scriban engine, HTML sanitizer, services
├── TemplateBuilder.Core/            # NuGet: render-only
├── TemplateBuilder.Editor/          # NuGet: full management UI (RCL)
└── TemplateBuilder.Web/             # Thin dev/demo host

tests/
├── TemplateBuilder.Domain.Tests/
├── TemplateBuilder.Application.Tests/
├── TemplateBuilder.Infrastructure.Tests/
└── TemplateBuilder.Editor.Tests/
```
