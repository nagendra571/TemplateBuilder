# TemplateBuilder

A Scriban-powered HTML template management system for .NET. Ships two independent NuGet packages:

| Package | Version | Purpose |
|---|---|---|
| [`TemplateBuilder.Editor`](https://www.nuget.org/packages/TemplateBuilder.Editor) | 1.4.5 | Full management UI — create, edit, version, compare, preview, restore |
| [`TemplateBuilder.Core`](https://www.nuget.org/packages/TemplateBuilder.Core) | 1.0.3 | Render templates to HTML strings — lightweight, no UI |

> `TemplateBuilder.Editor` includes everything `TemplateBuilder.Core` does. If you install Editor you do not need Core separately.

---

## Consuming TemplateBuilder.Editor

### 1. Create an ASP.NET Core MVC app

```bash
dotnet new mvc -n MyApp && cd MyApp
dotnet add package TemplateBuilder.Editor --version 1.4.5
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

## Securing the Editor

By default the editor is **open to all users**. Configure `options.Authorization` to restrict access.

### Anonymous (default)

No configuration needed — existing integrations work unchanged.

### Authenticated users only

```csharp
using TemplateBuilder.Editor.Authorization;

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.Mode = TemplateBuilderAuthorizationMode.Authenticated;
});
```

### Role-based access

A user in **any** of the listed roles is granted access.

```csharp
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.Mode = TemplateBuilderAuthorizationMode.Role;
    options.Authorization.RoleNames = ["Admin", "Supervisor", "SuperAdmin"];
});
```

### Custom policy (escape hatch)

Register your own named ASP.NET Core policy and point the editor at it:

```csharp
builder.Services.AddAuthorization(o =>
    o.AddPolicy("TemplateEditorAccess", pb =>
        pb.RequireAuthenticatedUser().RequireClaim("department", "Engineering")));

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.PolicyName = "TemplateEditorAccess";
});
```

For any secured mode, add both auth middlewares to your pipeline **before** `MapControllers`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

All editor routes — including the snippets API and the `/Templates/_setup` diagnostic page — are covered automatically.

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
| **Live preview** | Click **Preview** → JSON auto-filled from template placeholders → **Render** |
| **Preview auto-fill** | "⚡ Auto-fill from template" scans editor content for `{{ model.X }}` placeholders and populates the JSON textarea with sample string values — no SQL view required |
| **Dark / Light theme** | Toggle in the CANVAS panel heading, persisted in localStorage |
| **Render in code** | Inject `ITemplateEngine`, call `RenderByNameAsync` |
| **Auto-migrations** | Database schema created and updated on startup |
| **Setup diagnostic** | `/Templates/_setup` — checks all integration requirements |
| **Scriban syntax** | `{{ model.X }}`, loops, conditionals, filters |
| **Reusable snippets** | Save any selection as a named snippet, insert into any template |
| **Table toolbar** | Add/remove rows & columns, merge/split cells, header toggle, vertical align, style presets |
| **Find & Replace** | Floating panel (Ctrl+H) with highlight, navigation, and bulk replace |
| **Auto-save drafts** | Unsaved changes preserved in localStorage across page reloads |
| **Clean paste** | Strips Word/Outlook formatting on paste, preserves semantic structure |
| **Line height** | Dropdown (1.0 – 3.0) in the font toolbar group |
| **Custom list styles** | Apply disc/circle/square/decimal/upper-alpha/lower-roman to any list |
| **Special characters** | Floating picker (~80 chars across 5 groups) with live search |
| **Anchor links** | Insert named anchor chips; link to them via `#name` in the link dialog |
| **Print** | One-click browser print from the toolbar |

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

## Release History

### TemplateBuilder.Editor

**v1.4.5**
- **Fix**: Template list page now shows the correct version number for each template — `GetAllAsync` was missing `.Include(t => t.CurrentVersion)`, causing every row to display `v0`.

**v1.4.4**
- **Side-by-side Version Compare** — Click **Compare** on any version in the History panel to open a full-width compare modal. Current editor content renders on the left; the selected old version renders on the right — both use auto-generated sample JSON so loops and grids populate correctly. A **Restore vN** button in the right panel restores directly from the compare view. **← History** returns to the version list without losing context.

**v1.4.3**
- **Auto-fill for Loop & Grid blocks** — "⚡ Auto-fill from template" now generates 2-item sample arrays for Loop Block and Grid Block collections. Each array item includes all referenced sub-fields (`ProductName`, `Qty`, `UnitPrice`, etc.). Second items append a ` 2` suffix for visual distinction. Existing scalar field behaviour is unchanged.

**v1.4.2**
- **Fix**: Draft banner ("Unsaved draft found") no longer appears on page load when there is no saved draft — CSS specificity bug caused `display:flex` to override the `[hidden]` attribute.
- **Fix**: Validate panel no longer appears on page load — same CSS `[hidden]` specificity fix applied.
- **Preview auto-fill**: "⚡ Auto-fill from template" button now parses `{{ model.X }}` placeholders directly from the editor content and fills the JSON textarea with sample string values. Works for every template regardless of whether a SQL view is loaded — button is always enabled.

**v1.4.1**
- **Fix**: Special Characters floating panel no longer visible on page load; close button now functions correctly.
- **Fix**: Find & Replace floating panel no longer visible on page load — same CSS `[hidden]` specificity fix.

**v1.4.0**
- **Modern SaaS UI redesign** — New design token system (Inter font, CSS custom properties for color/shadow/radius) scoped to `#tb-editor-host`. Consistent light and dark themes across all panels.
- **Card-style Field Palette** — SQL view columns displayed as cards with a hover-reveal Insert button.
- **Badge system** — Type badges (Email, Report, Notice, Custom) and status badges (Active/Inactive) with matching light/dark variants on the template list page.
- **Version History polish** — Version entries rendered as cards with a "Current" indicator and per-entry Restore buttons.
- **Panel heading icons** — Visual icons on Field Palette (⊞), Canvas (✏), and Properties (⚙) headings.

**v1.3.7**
- **Access Control** — Configure authorization via `options.Authorization` in `AddTemplateBuilderEditor()`: `Anonymous` (default), `Authenticated`, `Role` (supports multiple roles with OR logic), or a custom named ASP.NET Core policy. Fully backward compatible — existing installations require no changes.

**v1.3.6**
- **Style**: Outer borders added to Field Palette (left) and Properties (right) panels.
- **Default**: Editor opens in light mode for new users (was dark).
- **Default**: Auto-save defaults to OFF for new users (was ON).

**v1.3.5**
- **Fix**: Editor canvas now scrolls to show full template content — `.tb-canvas-body` had `overflow: hidden`, clipping everything below the initial viewport height.

**v1.3.4**
- **Fix**: Snippets panel now loads correctly — `ISnippetRepository` was missing from `AddTemplateBuilderEditor()` DI registrations, causing a 500 on `GET /Templates/Api/Snippets`.

**v1.3.3**
- **Fix**: Editor canvas no longer blank on load — the custom anchor plugin was renamed to avoid colliding with SunEditor's internal `core.context.anchor` context used by the link plugin.

**v1.3.2**
- **Line Height** — dropdown (1.0–3.0) in the font toolbar group.
- **Special Characters** — floating Ω picker with 5 groups (~80 chars) and live search.
- **Print** — 🖨 toolbar button triggers browser print dialog.
- **Custom List Styles** — dropdown applies disc/circle/square/decimal/upper-alpha/lower-roman to the active list.
- **Anchor Links** — ⚓ toolbar button inserts a named anchor chip; link to it via `#name` in the standard link dialog.

**v1.3.1**
- **Fix**: SunEditor content no longer lost when Create form fails validation.
- **Fix**: Validation errors shown inline on the Create form instead of silently resetting the page.
- **Fix**: Body content entered on Create is saved as v1 so the Edit screen opens with content intact.

**v1.3.0**
- **Reusable Content Snippets** — save any selection as a named snippet and insert it into any template from the Snippets panel. Full CRUD API (`GET/POST/DELETE /Templates/Api/Snippets`).

**v1.2.1**
- **Merge & Split Table Cells** — multi-cell selection with colspan/rowspan support via the floating table toolbar.

**v1.2.0**
- **Font Family Selection**, **Fullscreen Editing**, **Word & Character Count**, **Auto-save Drafts**, **Find & Replace** (Ctrl+H), **Clean Paste from Word/Outlook**.

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
dotnet nuget push nupkg/TemplateBuilder.Editor.1.4.5.nupkg \
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
