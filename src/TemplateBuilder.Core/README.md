# TemplateBuilder.Core

**Current version: 2.0.0**

Render Scriban-powered HTML templates to strings in any .NET 10 application. Lightweight, no UI — just service registration and injection.

To create and manage templates, install the companion package [`TemplateBuilder.Editor`](https://www.nuget.org/packages/TemplateBuilder.Editor).

---

## Requirements

- .NET 10
- SQL Server

---

## Quick Start

### 1. Install

```bash
dotnet add package TemplateBuilder.Core --version 2.0.0
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
using TemplateBuilder.Core.Extensions;

builder.Services.AddTemplateBuilder(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});
```

### 4. Inject and render

```csharp
using TemplateBuilder.Domain.Interfaces;

public class EmailService(ITemplateEngine engine)
{
    // Render by template name (as entered in the editor UI)
    public Task<string> RenderByNameAsync(string name, object model, CancellationToken ct = default)
        => engine.RenderByNameAsync(name, model, ct);

    // Render by template database ID
    public Task<string> RenderByIdAsync(int id, object model, CancellationToken ct = default)
        => engine.RenderAsync(id, model, ct);
}
```

---

## API Reference

### `AddTemplateBuilder(options)`

Registers all TemplateBuilder services. Call once in `Program.cs`.

| Option | Default | Description |
|---|---|---|
| `ConnectionString` | *(required)* | SQL Server connection string |
| `EnableCaching` | `true` | Cache rendered template bodies in memory |
| `CacheDurationMinutes` | `60` | How long to cache a template version |

### `ITemplateEngine`

The primary rendering interface. Inject into any service or controller.

```csharp
// Render a template by its database ID
Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);

// Render a template by its name (must be active)
Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);

// Render an arbitrary Scriban body string directly (no DB lookup)
Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default);
```

`RenderAsync` / `RenderByNameAsync` serve the **last Active version** of the template — its highest-numbered version with `IsActive = true` — skipping any newer Draft version saved via the Editor's **Save Draft** button. As of `2.0.0`, three typed exceptions (`TemplateBuilder.Domain.Exceptions`) can be thrown:

| Exception | Thrown when |
|---|---|
| `TemplateNotFoundException` | No template with that ID/name exists |
| `TemplateInactiveException` | The template itself is deactivated (`Template.IsActive == false`) |
| `NoActiveVersionException` | The template is active but every saved version is a Draft — nothing Active to render |

**Breaking change from `1.x`:** an inactive template previously threw `TemplateNotFoundException`; it now throws the more specific `TemplateInactiveException`.

### `ITemplateRepository`

Direct access to template data.

```csharp
Task<Template?> GetByIdAsync(int id, CancellationToken ct = default);
Task<Template?> GetByNameAsync(string name, CancellationToken ct = default);
Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default);
Task<IReadOnlyList<TemplateVersion>> GetVersionHistoryAsync(int templateId, CancellationToken ct = default);
```

---

## Template Syntax

Templates use [Scriban](https://github.com/scriban/scriban). Model properties are accessed via `model.*`:

```html
<p>Hello <strong>{{ model.FirstName }}</strong>,</p>

{{ for item in model.Items }}
  <p>{{ item.Name }} — {{ item.Price | math.format "C" }}</p>
{{ end }}

{{ if model.IsPremium }}
  <p>Thank you for being a premium member.</p>
{{ end }}
```

Model property lookup is **case-insensitive** — `{{ model.firstname }}` and `{{ model.FirstName }}` resolve to the same value.

---

## Example: Sending a Rendered Email

```csharp
public class InvoiceEmailSender(ITemplateEngine engine, IEmailClient emailClient)
{
    public async Task SendAsync(InvoiceModel invoice, CancellationToken ct = default)
    {
        var html = await engine.RenderByNameAsync("Invoice Email", invoice, ct);

        await emailClient.SendAsync(new EmailMessage
        {
            To      = invoice.CustomerEmail,
            Subject = $"Invoice #{invoice.Number}",
            HtmlBody = html
        }, ct);
    }
}
```

---

## Managing Templates

Templates are stored in SQL Server and managed through [`TemplateBuilder.Editor`](https://www.nuget.org/packages/TemplateBuilder.Editor). Install it in any ASP.NET Core app to get the full create/edit/version UI:

```bash
dotnet add package TemplateBuilder.Editor
```

Both packages share the same database schema — point them at the same connection string. If you already have `TemplateBuilder.Editor` installed you do **not** need `TemplateBuilder.Core` separately; Editor includes the rendering engine.

---

## What's New

### v2.0.0
- **Two-state save model** — templates managed via `TemplateBuilder.Editor` now save each version as either **Draft** or **Active**. This package's render API is unaffected in shape but is now Active-version-aware — see below.
- **Breaking: render API now serves the last Active version, not simply the newest one.** `RenderAsync` / `RenderByNameAsync` walk version history for the highest-numbered version with `IsActive = true`; a newer Draft version is skipped. See the [`ITemplateEngine`](#itemplateengine) section above for the two new exceptions this introduces (`TemplateInactiveException`, `NoActiveVersionException`) and the change to when `TemplateNotFoundException` is thrown.

### v1.0.5
- **Fix**: This package README was still advertising `1.0.3` as current after 1.0.4 shipped — the version bump wasn't repacked into the actual `.nupkg`. Now confirmed fixed by extracting and inspecting the published package before every push.

### v1.0.4
- **Dependency updates** — HtmlSanitizer 9.0.892 → 9.2.995 and Scriban 7.2.0 → 7.2.6, clearing known moderate/high-severity NuGet security advisories. Microsoft.Data.SqlClient, EF Core SqlServer, and `Microsoft.Extensions.*` packages bumped to their latest 10.0.x patch releases. No breaking changes.

---

## Updating

```bash
dotnet add package TemplateBuilder.Core --version <new-version>
```
