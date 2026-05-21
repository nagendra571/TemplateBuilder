# TemplateBuilder.Editor

Embed a full Scriban-powered HTML template management UI into any ASP.NET Core 10 web application. Install the package, call one method, and your users can create, edit, version, preview, and restore templates — all wrapped in your own site layout.

## Requirements

- .NET 10
- ASP.NET Core MVC (`AddControllersWithViews()`)
- SQL Server

## Quick Start

### 1. Install

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

### 3. Register in Program.cs

```csharp
using TemplateBuilder.Editor;

builder.Services.AddControllersWithViews();

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});

// ...

app.UseStaticFiles();
app.MapStaticAssets(); // serves /_content/TemplateBuilder.Editor/ assets

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

### 4. Wire up the layout

The editor views use your app's existing `_ViewStart.cshtml` — no extra configuration needed. Add a link to your nav:

```html
<a asp-controller="Templates" asp-action="Index">Templates</a>
```

### 5. Run

Start your app and navigate to `/Templates`.

## What's Included

| Feature | Route |
|---|---|
| Template list | `GET /Templates` |
| Create template | `GET /Templates/Create` |
| Edit template | `GET /Templates/{id}/Edit` |
| Save version | `POST /Templates/{id}/SaveVersion` |
| Version history | `GET /Templates/{id}/VersionHistory` |
| Restore version | `POST /Templates/{id}/Restore/{versionId}` |
| Preview (live render) | `POST /Templates/{id}/Preview` |
| Duplicate | `POST /Templates/{id}/Duplicate` |
| Validate syntax | `POST /Templates/{id}/Validate` |
| Enable / Disable | `POST /Templates/{id}/Enable` / `Disable` |

## Database

`AddTemplateBuilderEditor()` registers an `IHostedService` that runs EF Core migrations on startup. The schema is created or updated automatically — no manual steps required.

## Static Assets

CSS and JS are served from:

```
/_content/TemplateBuilder.Editor/css/template-editor.css
/_content/TemplateBuilder.Editor/js/template-editor.js
```

Your app must call `app.UseStaticFiles()` and `app.MapStaticAssets()` (both standard in ASP.NET Core MVC apps).

## Template Syntax

Templates use [Scriban](https://github.com/scriban/scriban) syntax:

```html
<p>Hello <strong>{{ model.FirstName }}</strong>,</p>

{{ for item in model.Items }}
  <p>{{ item.Name }} — {{ item.Price }}</p>
{{ end }}

{{ if model.IsPremium }}
  <p>Thank you for being a premium member.</p>
{{ end }}
```

## Rendering Templates in Code

To render a saved template to an HTML string (e.g. for sending emails), install the companion package:

```bash
dotnet add package TemplateBuilder.Core
```

```csharp
var html = await templateEngine.RenderAsync(template, myModel);
```

## Updating

```bash
dotnet add package TemplateBuilder.Editor --version <new-version>
```

EF migrations are bundled — schema changes apply automatically on next startup.
