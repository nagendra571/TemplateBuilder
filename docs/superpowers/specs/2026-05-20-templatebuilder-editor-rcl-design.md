# TemplateBuilder.Editor — Razor Class Library Design Spec

**Date:** 2026-05-20  
**Status:** Approved

## Context

TemplateBuilder currently ships two artifacts: a standalone ASP.NET MVC web app (the template designer) and a `TemplateBuilder.Core` NuGet package (render-only). Teams using `TemplateBuilder.Core` to render templates in their own apps have no way to manage (create/edit) those templates from within their own application — they must navigate to the separate TemplateBuilder web app.

The goal is a second NuGet package, `TemplateBuilder.Editor`, that any .NET web app can install to get the full template management UI (list, create, edit, version history, preview, restore) embedded in their own site, wrapped in their own layout — without deploying or hosting the TemplateBuilder app separately.

When a feature is added or fixed in the editor, the developer updates it in the TemplateBuilder repo, bumps the package version, publishes, and consumers run `dotnet update package TemplateBuilder.Editor` to receive it.

---

## Architecture

A new `TemplateBuilder.Editor` project is added to the solution using the `Microsoft.NET.Sdk.Razor` SDK — this is a Razor Class Library (RCL). It is packable as a NuGet package.

The existing `TemplateBuilder.Web` is slimmed to a thin dev/demo host: it retains only the shared layout, `HomeController`, and references `TemplateBuilder.Editor` as a project reference. This proves the package experience in-repo during development.

**Project dependency graph after change:**
```
Domain ← Infrastructure ← Application ← Core   (NuGet #1 — render only)
                                      ← Editor  (NuGet #2 — full UI)
                                             ↑
                                      Web (thin host, not shipped as NuGet)
```

---

## Solution Structure

```
src/
├── TemplateBuilder.Domain/          ← unchanged
├── TemplateBuilder.Infrastructure/  ← unchanged (owns EF Core DbContext, migrations)
├── TemplateBuilder.Application/     ← unchanged (Scriban engine, services)
├── TemplateBuilder.Core/            ← unchanged (render NuGet)
├── TemplateBuilder.Editor/          ← NEW RCL
│   ├── TemplateBuilder.Editor.csproj
│   ├── Controllers/
│   │   └── TemplatesController.cs  (moved from Web)
│   ├── Models/                     (ViewModels moved from Web)
│   ├── Views/
│   │   ├── _ViewImports.cshtml     (namespace: TemplateBuilder.Editor)
│   │   └── Templates/
│   │       ├── Index.cshtml
│   │       ├── Edit.cshtml
│   │       └── _VersionHistory.cshtml
│   └── wwwroot/
│       ├── css/template-editor.css
│       └── js/template-editor.js
└── TemplateBuilder.Web/             ← SLIMMED to thin host
    ├── Program.cs                   (calls AddTemplateBuilderEditor())
    ├── appsettings.json
    ├── Controllers/HomeController.cs
    └── Views/
        ├── _ViewStart.cshtml        (Layout = "_Layout" — applies to RCL views too)
        ├── Shared/_Layout.cshtml
        └── Home/Index.cshtml

tests/
├── TemplateBuilder.Editor.Tests/    ← replaces TemplateBuilder.Web.Tests
├── TemplateBuilder.Application.Tests/ ← unchanged
├── TemplateBuilder.Infrastructure.Tests/ ← unchanged
└── TemplateBuilder.Domain.Tests/   ← unchanged
```

---

## TemplateBuilder.Editor.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>TemplateBuilder.Editor</PackageId>
    <Version>1.0.0</Version>
    <Description>Full template management UI (create, edit, version history) as an embeddable Razor Class Library. Call AddTemplateBuilderEditor() then navigate to /Templates.</Description>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\TemplateBuilder.Application\TemplateBuilder.Application.csproj" />
    <ProjectReference Include="..\TemplateBuilder.Infrastructure\TemplateBuilder.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

---

## Consumer Onboarding

Any .NET 10 web app installs the package and adds two things:

**Program.cs:**
```csharp
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = config.GetConnectionString("Default");
});
```

**Standard controller routing (already present in MVC apps):**
```csharp
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
```

Navigate to `/Templates` — the full editor UI appears wrapped in the consumer's layout.

---

## `AddTemplateBuilderEditor()` Implementation

`ServiceCollectionExtensions` in `TemplateBuilder.Editor`:
- Registers `TemplateBuilderDbContext` with the provided connection string
- Registers all services: `ITemplateRepository`, `ITemplateEngine`, `ISqlViewDiscoveryService`, `IHtmlSanitizerService`
- Registers `MigrationHostedService : IHostedService` that calls `db.Database.Migrate()` on `StartAsync`
- Does NOT need to register controllers explicitly — ASP.NET Core's `AddControllersWithViews()` on the host automatically discovers controllers in all referenced assemblies including the RCL

---

## Layout Integration

The RCL ships **no** `_ViewStart.cshtml`. ASP.NET Core applies the consumer app's `_ViewStart.cshtml` (which sets `Layout = "_Layout"`) to all views including RCL views. The consumer's layout wraps the editor pages with zero configuration.

---

## Static Assets

`template-editor.js` and `template-editor.css` are served from:
```
/_content/TemplateBuilder.Editor/js/template-editor.js
/_content/TemplateBuilder.Editor/css/template-editor.css
```

The RCL views reference them with this path. Consumer app must call `app.UseStaticFiles()` (standard).

SunEditor continues loading from CDN (unchanged). Bootstrap and jQuery are referenced via CDN in the RCL views — consumer controls their own Bootstrap/jQuery version in their layout.

---

## Database / Migrations

Migrations live in `TemplateBuilder.Infrastructure` (unchanged). `MigrationHostedService` calls `db.Database.Migrate()` on host startup — schema is created or updated automatically. No manual migration steps for consumers.

---

## Routes

Fixed at `/Templates` — no configurable prefix. All 12 existing action routes preserved exactly as-is.

---

## NuGet Packages

Two independent packages with independent versioning:

| Package | Purpose |
|---|---|
| `TemplateBuilder.Core` | Render templates to HTML string — lightweight, no UI |
| `TemplateBuilder.Editor` | Full management UI — install for create/edit capability |

Packages are independent. A consumer can install one or both.

---

## TemplateBuilder.Web After Slimming

Keeps only:
- `Program.cs` — calls `AddTemplateBuilderEditor()` via project reference
- `appsettings.json` — connection string
- `Controllers/HomeController.cs` — `Index()` and `Error()` only
- `Views/Shared/_Layout.cshtml` — nav includes "Templates" link
- `Views/_ViewStart.cshtml` — `Layout = "_Layout"`
- `Views/Home/Index.cshtml` — welcome page
- `wwwroot/css/site.css`, `wwwroot/js/site.js`
- `wwwroot/lib/` — Bootstrap, jQuery (needed by the thin host itself)

Deleted from Web: `TemplatesController.cs`, `Views/Templates/`, `wwwroot/css/template-editor.css`, `wwwroot/js/template-editor.js`, view models used only by Templates.

---

## Tests

`TemplateBuilder.Web.Tests` is renamed to `TemplateBuilder.Editor.Tests`. References `TemplateBuilder.Editor` instead of `TemplateBuilder.Web`. All existing controller tests migrate with only namespace and project reference changes.

---

## Verification Checklist

1. `dotnet build` — all 6 projects build, zero warnings
2. `dotnet test` — 46+ tests passing
3. Start `TemplateBuilder.Web` → `/Templates` loads with host layout applied
4. Create a template, edit it, preview it, restore a version — all workflows functional
5. Browser network tab confirms static assets load from `/_content/TemplateBuilder.Editor/`
6. `__EFMigrationsHistory` table populated on first startup
