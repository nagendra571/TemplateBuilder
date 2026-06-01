# TemplateBuilder.Editor

**Current version: 1.3.2**

Embed a full Scriban-powered HTML template management UI into any ASP.NET Core web application. Install the package, call two methods, and your users can create, edit, version, preview, and restore templates — all wrapped in your own site layout.

---

## Requirements

- .NET 10
- ASP.NET Core MVC
- SQL Server

---

## Quick Start

### 1. Install

```bash
dotnet add package TemplateBuilder.Editor --version 1.3.2
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
app.UseStaticFiles();       // required — serves /_content/TemplateBuilder.Editor/ assets
app.UseRouting();
app.UseAuthorization();

app.MapControllers();       // required — registers attribute-routed endpoints (Edit, Preview, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

> **Two things that are easy to miss:**
> - `app.MapControllers()` must appear before `MapControllerRoute`. Without it, Edit, Preview, SaveVersion, and Versions routes return 404.
> - `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` is needed to allow creating templates with an empty body.

### 4. Wire up the layout

Add `@await RenderSectionAsync("Styles", required: false)` inside `<head>` and `@await RenderSectionAsync("Scripts", required: false)` before `</body>` in your `Views/Shared/_Layout.cshtml`:

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

### 5. Run

```bash
dotnet run
```

EF Core migrations run automatically on first startup — the database and schema are created for you. Navigate to `/Templates`.

---

## Setup Diagnostic Page

After installation, navigate to **`/Templates/_setup`** in Development to verify every integration requirement at once:

| Check | What it detects |
|---|---|
| Database connection | SQL Server reachable with the configured connection string |
| Migrations applied | All EF Core schema migrations are current |
| `app.MapControllers()` registered | Attribute-routed endpoints are accessible |
| `SuppressImplicitRequired` | Empty template body won't silently fail |
| Static assets | `/_content/TemplateBuilder.Editor/` is being served |
| `@section Styles` | `RenderSectionAsync("Styles")` is in your layout `<head>` |
| `@section Scripts` | `RenderSectionAsync("Scripts")` is in your layout before `</body>` |
| CSS/JS files loaded | `template-editor.css` and `template-editor.js` are linked |

Every failing check shows a one-line fix. The page returns 404 in non-Development environments.

---

## What's New

### v1.3.2
- **Line Height** — line height dropdown (1.0–3.0) in the font toolbar group.
- **Special Characters** — floating Ω picker with 5 groups (~80 chars) and live search.
- **Print** — 🖨 toolbar button triggers browser print dialog.
- **Custom List Styles** — "≡▾" dropdown applies disc/circle/square/decimal/upper-alpha/lower-roman to the active list.
- **Anchor Links** — ⚓ toolbar button inserts a named anchor chip; link to it via `#name` in the standard link dialog.

### v1.3.1
- **Fix**: SunEditor content no longer lost when Create form fails validation — body is now synced to the form before submission.
- **Fix**: Validation errors (e.g. missing template name) are now shown inline on the Create form instead of silently resetting the page.
- **Fix**: Body content entered on the Create screen is now saved as v1 when the template is created, so the Edit screen opens with content intact.

### v1.3.0
- **Reusable Content Snippets** — Save any selection as a named snippet and insert it into any template from the Snippets panel. Full CRUD API (`GET/POST/DELETE /Templates/Api/Snippets`).

### v1.2.1
- **Merge & Split Table Cells** — Multi-cell selection with colspan/rowspan support via the floating table toolbar.

### v1.2.0
- **Font Family Selection** — Choose from common web-safe and system font families in the toolbar.
- **Fullscreen Editing** — Toggle distraction-free fullscreen mode.
- **Word & Character Count** — Live count displayed in the editor status bar.
- **Auto-save Drafts** — Unsaved changes are preserved across page reloads.
- **Find & Replace** — Floating panel (Ctrl+H) with highlight, navigation, and bulk replace.
- **Clean Paste from Word/Outlook** — Strips proprietary formatting on paste, preserving semantic structure.

---

## Features

| Feature | Route |
|---|---|
| Template list | `GET /Templates` |
| Create template | `GET/POST /Templates/Create` |
| Edit template | `GET /Templates/{id}/Edit` |
| Save version | `POST /Templates/{id}/SaveVersion` |
| Version history | `GET /Templates/{id}/Versions` |
| Restore version | `POST /Templates/{id}/Restore/{versionId}/{sourceVersionNumber}` |
| Live preview | `POST /Templates/{id}/Preview` |
| Duplicate | `POST /Templates/{id}/Duplicate` |
| Validate syntax | `POST /Templates/{id}/Validate` |
| Toggle active | `POST /Templates/{id}/ToggleActive` |
| List snippets | `GET /Templates/Api/Snippets` |
| Create snippet | `POST /Templates/Api/Snippets` |
| Delete snippet | `DELETE /Templates/Api/Snippets/{id}` |
| Setup check | `GET /Templates/_setup` *(Development only)* |

---

## Theming

The editor ships with a **dark theme** by default. A **☀ Light / 🌙 Dark** toggle button appears in the CANVAS panel heading and persists the preference in `localStorage`.

The editor's styles are fully scoped to `#tb-editor-host` — they do not affect the rest of your application. The editing canvas is always white (document-like) regardless of the selected theme.

---

## Template Syntax

Templates use [Scriban](https://github.com/scriban/scriban) — access model properties via `model.*`:

```html
<p>Hello <strong>{{ model.FirstName }}</strong>,</p>

{{ for item in model.Items }}
  <p>{{ item.Name }} — {{ item.Price }}</p>
{{ end }}

{{ if model.IsPremium }}
  <p>Thank you for being a premium member.</p>
{{ end }}
```

---

## Render Templates in Code

`TemplateBuilder.Editor` includes the rendering engine. Inject `ITemplateEngine` anywhere:

```csharp
using TemplateBuilder.Domain.Interfaces;

public class WelcomeEmailService(ITemplateEngine engine)
{
    public Task<string> BuildAsync(string firstName) =>
        engine.RenderByNameAsync("Welcome Email", new { FirstName = firstName });
}
```

---

## Database

`AddTemplateBuilderEditor()` registers a hosted service that runs EF Core migrations on startup. No manual migration steps are required.

---

## Static Assets

CSS and JS are served automatically from:

```
/_content/TemplateBuilder.Editor/css/template-editor.css
/_content/TemplateBuilder.Editor/js/template-editor.js
```

Your `Program.cs` must call `app.UseStaticFiles()` (or `app.MapStaticAssets()`). After upgrading to a new package version, do a hard refresh (Ctrl+Shift+R) to clear cached assets.

---

## Updating

```bash
dotnet add package TemplateBuilder.Editor --version <new-version>
```

EF migrations are bundled — schema changes apply automatically on next startup.
