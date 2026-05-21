# TemplateBuilder.Core

Render Scriban-powered HTML templates to strings in any .NET 10 application. Lightweight, no UI — just service registration and injection.

To create and manage templates, install the companion package [`TemplateBuilder.Editor`](https://www.nuget.org/packages/TemplateBuilder.Editor).

## Requirements

- .NET 10
- SQL Server

## Quick Start

### 1. Install

```bash
dotnet add package TemplateBuilder.Core
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
using TemplateBuilder.Core.Extensions;

builder.Services.AddTemplateBuilder(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb")!;
});
```

### 4. Inject and render

```csharp
public class EmailService
{
    private readonly ITemplateEngine _engine;

    public EmailService(ITemplateEngine engine)
    {
        _engine = engine;
    }

    // Render by template ID
    public Task<string> RenderByIdAsync(int templateId, object model, CancellationToken ct = default)
        => _engine.RenderAsync(templateId, model, ct);

    // Render by template name
    public Task<string> RenderByNameAsync(string name, object model, CancellationToken ct = default)
        => _engine.RenderByNameAsync(name, model, ct);
}
```

## API Reference

### `AddTemplateBuilder(options)`

Registers all TemplateBuilder services. Must be called once in `Program.cs`.

```csharp
builder.Services.AddTemplateBuilder(options =>
{
    options.ConnectionString = "...";
});
```

### `ITemplateEngine`

The primary rendering interface. Inject into any service or controller.

```csharp
// Render a template by its database ID
Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);

// Render a template by its name
Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);

// Render an arbitrary Scriban body string directly (no DB lookup)
Task<string> RenderBodyAsync(string body, object model, CancellationToken ct = default);
```

### `ITemplateRepository`

Direct access to template data. Inject when you need metadata or version history.

```csharp
Task<Template?> GetByIdAsync(int id, CancellationToken ct = default);
Task<Template?> GetByNameAsync(string name, CancellationToken ct = default);
Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default);
Task<IReadOnlyList<TemplateVersion>> GetVersionHistoryAsync(int templateId, CancellationToken ct = default);
```

## Template Syntax

Templates use [Scriban](https://github.com/scriban/scriban) syntax. Pass any object as the model — properties are accessed via `model.*`:

```html
<p>Hello <strong>{{ model.FirstName }}</strong>,</p>

{{ for item in model.Items }}
  <p>{{ item.Name }} — {{ item.Price | math.format "C" }}</p>
{{ end }}

{{ if model.IsPremium }}
  <p>Thank you for being a premium member.</p>
{{ end }}
```

## Example: Sending a Rendered Email

```csharp
public class InvoiceEmailSender
{
    private readonly ITemplateEngine _engine;
    private readonly IEmailClient _emailClient;

    public InvoiceEmailSender(ITemplateEngine engine, IEmailClient emailClient)
    {
        _engine = engine;
        _emailClient = emailClient;
    }

    public async Task SendAsync(InvoiceModel invoice, CancellationToken ct = default)
    {
        var html = await _engine.RenderByNameAsync("Invoice Email", invoice, ct);

        await _emailClient.SendAsync(new EmailMessage
        {
            To = invoice.CustomerEmail,
            Subject = $"Invoice #{invoice.Number}",
            HtmlBody = html
        }, ct);
    }
}
```

## Managing Templates

Templates are stored in SQL Server and managed through `TemplateBuilder.Editor`. Install it in any ASP.NET Core app to get the full create/edit/version UI:

```bash
dotnet add package TemplateBuilder.Editor
```

Both packages share the same database schema — point them at the same connection string.
