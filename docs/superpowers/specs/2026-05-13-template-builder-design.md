# TemplateBuilder — Design Spec

**Date:** 2026-05-13  
**Status:** Approved  
**Stack:** ASP.NET MVC · .NET 10 · SQL Server · EF Core · Scriban · TinyMCE

---

## 1. Problem & Goal

Teams need to create and maintain reusable document templates (emails, reports, notices, custom) where dynamic content — driven by SQL data — is merged at runtime. Today this is done ad-hoc in code, making templates hard to change without a deployment.

**Goal:** A two-part system:
1. A **visual designer web app** where developers and BAs build templates by dragging SQL view fields onto a rich-text canvas.
2. A **NuGet package** any .NET application installs to render a template to an HTML string by passing a template ID and a model object.

---

## 2. System Architecture

Two deployable artifacts sharing one SQL Server database.

```
TemplateBuilder.sln
├── TemplateBuilder.Domain/          # Entities, interfaces — no dependencies
├── TemplateBuilder.Application/     # Scriban engine, services — depends on Domain
├── TemplateBuilder.Infrastructure/  # EF Core, SQL repos — depends on Domain
├── TemplateBuilder.Web/             # ASP.NET MVC .NET 10 — depends on all layers
└── TemplateBuilder.Core/            # NuGet wrapper — depends on Application + Infrastructure
```

**Dependency rule:** Domain ← Application ← Infrastructure ← Web/Core. The NuGet package (Core) references Application and Infrastructure only — it does not reference Web.

### Data flow at runtime
```
Consumer App → RenderAsync(templateId, model)
             → NuGet fetches template body from SQL (cached)
             → Scriban merges model into body
             → Returns HTML string
```

---

## 3. Database Schema

### Templates

| Column | Type | Notes |
|---|---|---|
| Id | INT | PK, identity |
| Name | NVARCHAR(200) | Required, unique |
| TemplateType | NVARCHAR(50) | Email / Report / Notice / Custom |
| Description | NVARCHAR(500) | Nullable |
| CurrentVersionId | INT | FK → TemplateVersions.Id |
| IsActive | BIT | Soft delete — false hides from designer AND blocks runtime rendering (throws TemplateNotFoundException) |
| CreatedAt | DATETIME2 | |
| UpdatedAt | DATETIME2 | |
| RowVersion | ROWVERSION | EF Core [Timestamp] optimistic concurrency token |

### TemplateVersions

| Column | Type | Notes |
|---|---|---|
| Id | INT | PK, identity |
| TemplateId | INT | FK → Templates.Id |
| VersionNumber | INT | Sequential per template (1, 2, 3…) |
| Body | NVARCHAR(MAX) | HTML + Scriban syntax |
| ChangeComment | NVARCHAR(500) | Optional save note from designer |
| CreatedAt | DATETIME2 | |
| CreatedBy | NVARCHAR(100) | Nullable for v1 (no auth) |

**Key decisions:**
- `CurrentVersionId` is a direct FK pointer — NuGet fetches the current body in a single indexed lookup, no aggregation.
- SQL view metadata is never stored. Views matching `ViewPrefix` (default: `TemplateBuilder_`) or an explicit `ViewAllowlist` are auto-discovered live from the database at design time only. Sensitive schemas (`sys`, `INFORMATION_SCHEMA`, `guest`) are always excluded.
- Schema managed via EF Core code-first migrations.

**Name uniqueness rule:** The unique constraint on `Templates.Name` uses the database's default collation (`SQL_Latin1_General_CP1_CI_AS` — case-insensitive, accent-sensitive). The application normalizes names by trimming leading/trailing whitespace before insert or update. Duplicate name conflict returns HTTP 400 `{ "code": "VALIDATION_ERROR", "message": "A template named '[name]' already exists." }`.

**Audit trail (v1):** `CreatedBy` is nullable — no auth in v1. When authentication is added, populate `CreatedBy` from the authenticated user's identity at save time. All `TemplateRenderException` events must be logged at ERROR level with `templateId`, `versionId`, and UTC timestamp.

---

## 4. Template Syntax (Scriban)

Templates are HTML documents with Scriban expressions embedded. The rendering engine processes these at runtime using the caller-supplied model.

### Scalar field
```html
<p>Dear {{ model.CustomerName }},</p>
<p>Order placed on {{ model.OrderDate }}.</p>
```

### Loop block (repeating rows)
```html
{{ for item in model.OrderItems }}
  <tr>
    <td>{{ item.ProductName }}</td>
    <td>{{ item.Quantity }}</td>
    <td>{{ item.UnitPrice }}</td>
  </tr>
{{ end }}
```

### Grid block (table with header)
The designer places a Grid Block. The editor auto-generates `<thead>`. The body is a loop:
```html
<table>
  <thead><tr><th>Product</th><th>Qty</th><th>Price</th></tr></thead>
  <tbody>
    {{ for item in model.OrderItems }}
      <tr><td>{{ item.ProductName }}</td><td>{{ item.Quantity }}</td><td>{{ item.UnitPrice }}</td></tr>
    {{ end }}
  </tbody>
</table>
```

### Conditional
```html
{{ if model.IsPremium }}<strong>Premium Member</strong>{{ end }}
```

### Filters
```html
{{ model.OrderDate | date.to_string "%d/%m/%Y" }}
{{ model.CustomerName | string.upcase }}
```

**Convention:** The collection property name on the runtime model must match the collection name used in the `for` loop. The palette shows column names from the selected SQL view — these are the names the developer must use as model property names.

### Model Binding Contract

| Scenario | Behavior |
|---|---|
| Property name casing | Case-insensitive — `{{ model.CustomerName }}` matches POCO property `customerName` |
| Null property value | Renders as empty string — no exception |
| Missing property | Renders as empty string — no exception |
| `Dictionary<string, object>` model | Keys treated as property names — case-insensitive lookup |
| Collection name | Loop variable must match collection property name (case-insensitive): `{{ for item in model.OrderItems }}` requires `OrderItems` |
| Nesting depth | One level: `model.Collection[i].Property`. Deeper nesting is unsupported in v1. |

These behaviors are Scriban's native reflection-based binding and are not configurable.

---

## 5. NuGet Package — `TemplateBuilder.Core`

### Registration
```csharp
// Program.cs
builder.Services.AddTemplateBuilder(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("TemplateDb");
    options.EnableCaching = true;               // optional, default: true
    options.CacheDurationMinutes = 30;          // optional, default: 30
    options.ViewPrefix = "TemplateBuilder_";    // only views matching this prefix appear in the palette
    // options.ViewAllowlist = new[] { "TemplateBuilder_CustomerOrders" }; // explicit list overrides prefix
    options.StrictMode = false;                 // true = sanitize rendered output before returning to caller
    options.ValidateSchemaOnStartup = false;    // true = check DB migration compatibility on startup
});
```

### Interface
```csharp
public interface ITemplateEngine
{
    Task<string> RenderAsync(int templateId, object model, CancellationToken ct = default);
    Task<string> RenderByNameAsync(string templateName, object model, CancellationToken ct = default);
}
```

- `model` accepts any object — strongly typed POCOs, anonymous types, or dictionaries. Reflection is used to bind properties to Scriban variables.
- `RenderByNameAsync` uses the `Templates.Name` column — useful when IDs differ across environments.

### Exceptions
| Exception | When thrown |
|---|---|
| `TemplateNotFoundException` | Template ID/name not found, or `IsActive = false` |
| `TemplateRenderException` | Scriban syntax error in the template body |
| `SchemaVersionMismatchException` | DB schema is behind the required migration (when ValidateSchemaOnStartup = true or on first render) |

`IsActive` governs editor visibility only. Both `RenderAsync` and `RenderByNameAsync` throw `TemplateNotFoundException` when `IsActive = false` — the inactive status is not leaked to callers (same response as not found).

### Output Trust Model
Template bodies are authored exclusively by internal trusted users. Scriban outputs model property values as-is — no HTML encoding is applied by the engine.

`@Html.Raw(...)` is safe only when all model values originate from trusted internal sources. Callers passing model values from user-controlled or external input MUST HTML-encode those values before passing them to `RenderAsync`.

v1 assumes model data comes from trusted internal databases. Multi-tenant use requires revisiting this contract.

### Schema Compatibility
`TemplateBuilder.Core` declares a `MinimumSchemaVersion` constant matching the EF Core migration ID it requires. On the first `RenderAsync` call (lazy) or during `AddTemplateBuilder` registration (if `ValidateSchemaOnStartup = true`), the package checks `__EFMigrationsHistory` for the required migration ID.

If the migration is absent, it throws `SchemaVersionMismatchException`:
> "TemplateBuilder.Core requires DB migration '{MigrationId}' which has not been applied. Run: dotnet ef database update --project TemplateBuilder.Infrastructure"

Default: `ValidateSchemaOnStartup = false` (lazy check on first render).

### Caching
On each `RenderAsync` call the NuGet executes one lightweight query: `SELECT CurrentVersionId FROM Templates WHERE Id = @id`. If the result matches the cached version key, the cached body is returned with no further DB access. If it differs (designer saved a new version), the full body is re-fetched and the cache is updated. This guarantees callers always see the latest published version immediately after a designer save, with minimal DB overhead.

### Error Contracts

All API error responses use a consistent JSON shape:
```json
{ "code": "ERROR_CODE", "message": "Human-readable message safe to display." }
```

| Scenario | HTTP Status | code |
|---|---|---|
| Template ID/name not found or inactive | 404 | `TEMPLATE_NOT_FOUND` |
| Scriban syntax error | 500 | `TEMPLATE_RENDER_ERROR` |
| Concurrent save conflict | 409 | `CONFLICT` |
| Validation failure (name collision, etc.) | 400 | `VALIDATION_ERROR` |
| Preview JSON malformed | 400 | `PREVIEW_JSON_INVALID` |
| Preview JSON too large | 400 | `PREVIEW_JSON_TOO_LARGE` |
| Preview render timeout | 408 | `PREVIEW_TIMEOUT` |

**Safety rule:** `message` must never contain raw Scriban exception internals, stack traces, or SQL error text. Log the full error server-side; return a sanitized message to callers.

### Common usage patterns
```csharp
// Email body
var html = await engine.RenderAsync(42, model);
await emailService.SendAsync(to, subject, html);

// Embed in Razor view
ViewBag.Content = await engine.RenderAsync(42, model);
// In view: @Html.Raw(ViewBag.Content)

// PDF generation
var html = await engine.RenderAsync(42, model);
var pdf = pdfService.FromHtml(html);
```

---

## 6. Web App — Page Flows

### Page 1: Template List (`/templates`)
- Search bar + type filter dropdown (All / Email / Report / Notice / Custom)
- Table: Name, Type badge, current version, last updated, Active status
- Actions per row: Edit, **Duplicate**, Settings (activate/deactivate)
- "+ New Template" button → opens editor with blank canvas
- Quick stats sidebar: count by type

**Duplicate action:**
- Clicking "Duplicate" on any row opens a small modal
- Modal pre-fills the name field with `"Copy of [OriginalName]"`
- User edits the name if desired, then confirms
- System creates a new Template record (same `TemplateType` and `Description` as original, `IsActive = true`)
- Copies the original's current version body as Version 1 of the new template (with `ChangeComment = "Duplicated from '[OriginalName]'"`)
- Redirects user directly to the new template's editor (`/templates/{newId}/edit`)

### Page 2: Template Editor (`/templates/{id}/edit`)
3-panel layout:

**Left panel — Field Palette**
- SQL View dropdown (auto-populated from database — lists views matching the configured `ViewPrefix` or `ViewAllowlist`)
- On view select: columns listed as draggable chips (colour-coded by scalar vs collection)
- Blocks section: Loop Block and Grid Block as draggable items

**Center panel — Canvas (TinyMCE)**
- Rich text editor with standard formatting toolbar (bold, italic, headings, lists, links)
- Drag a scalar field → inserts `{{ model.FieldName }}` token, rendered as a highlighted chip
- Drop a Loop Block → creates a bordered region; inner columns from the selected view appear in the palette scoped to that loop
- Drop a Grid Block → auto-scaffolds a table with `<thead>` and loop body

**Right panel — Properties**
- Template name (editable)
- Template type (dropdown)
- Current version number + "History" link
- Optional save comment field
- Preview button (opens modal)
- Save Version button (creates new TemplateVersions row and updates CurrentVersionId in a single atomic transaction — partial failure rolls back both)

### Page 3: Version History (drawer/modal)
- Lists all versions in reverse order: version number, change comment, date
- Current version highlighted
- "Restore" button on past versions → creates a new version with the old body (does not delete history)

### Page 4: Live Preview (modal)
- JSON editor pre-populated with auto-generated sample data from the selected SQL view schema
- User can edit the JSON to test edge cases
- "Render" button calls the same `TemplateEngine.RenderAsync` internally
- Output displayed in an iframe below the JSON editor

**Preview constraints:**

| Constraint | Value |
|---|---|
| Max JSON payload | 64 KB — hard reject with 400 `PREVIEW_JSON_TOO_LARGE` |
| Malformed JSON | 400 `PREVIEW_JSON_INVALID` |
| Render timeout | 5 seconds — 408 `PREVIEW_TIMEOUT` |
| Caching | None — every Render click executes fresh |

---

## 7. Key Technical Decisions

| Decision | Choice | Reason |
|---|---|---|
| Template engine | Scriban | Fast, safe (no arbitrary code execution), supports loops/conditionals/filters, works with `object` via reflection |
| Rich text editor | TinyMCE | Mature, well-supported in MVC projects, supports custom toolbar buttons for field insertion |
| ORM | EF Core (code-first) | Native .NET 10 support, migrations manage schema, LINQ queries |
| Output format | HTML string always | Caller decides what to do with it — email, PDF, Razor embed |
| Model binding | `object` + reflection | Accepts POCOs, anonymous types, dictionaries — maximum flexibility for callers |
| Auth | None for v1 | Internal tool, can be added (ASP.NET Identity or Windows Auth) without structural changes |
| Versioning | Append-only TemplateVersions | No overwrites, full history, restore by creating a new version |

### Non-Functional Targets (v1)

| Concern | Target |
|---|---|
| Render latency (cached) | p95 < 150 ms |
| Render latency (cache miss) | p95 < 400 ms |
| Template body size | Soft limit 512 KB — designer warns; hard reject at 1 MB |
| Max preview JSON payload | 64 KB — hard reject (400) |
| Max loop item count | No hard limit in v1; > 10 000 items untested — caller responsibility |

These are design targets, not enforced SLAs.

### Concurrency Policy
`Templates.RowVersion` (SQL `ROWVERSION`) is mapped as an EF Core `[Timestamp]` concurrency token. On concurrent saves or restores:

1. First save succeeds and advances `RowVersion`.
2. Second save detects the stale token → EF Core throws `DbUpdateConcurrencyException`.
3. Web app returns HTTP 409 `CONFLICT`: *"This template was modified by another user while you were editing. Please refresh and try again."*

No version history is lost — the first save's TemplateVersion row is preserved.

### HTML Sanitization
Template bodies are treated as untrusted content at two enforcement points:

1. **On save (always):** The template body is passed through an allowlist HTML sanitizer before being persisted to `TemplateVersions.Body`. Disallowed tags and attributes are stripped silently.
2. **On render output (strict mode):** If `options.StrictMode = true`, the rendered HTML string is sanitized before being returned to the caller. Default: `false`.

Default allowlist: `p, div, span, strong, em, b, i, u, h1–h6, ul, ol, li, br, hr, table, thead, tbody, tr, th, td, a [href: https/http only], img [src: https/data:image/* only]`.

Library: `Ganss.Xss` (HtmlSanitizer). Added to `TemplateBuilder.Application`.

---

## 8. Verification Plan

### Designer (Web App)
1. Create a new template of each type (Email, Report, Notice, Custom)
2. On the Template List, click "Duplicate" on an existing template — verify modal opens with name pre-filled as "Copy of [OriginalName]"
3. Confirm duplicate — verify redirect to new template's editor, body matches original, version shows v1, change comment says "Duplicated from '[OriginalName]'"
4. Verify the original template is unchanged
2. Select a SQL view — verify columns appear in the palette
3. Drag scalar fields into the canvas — verify `{{ model.X }}` tokens are inserted
4. Drop a Loop Block — verify the bordered region appears and inner fields are scoped to the loop
5. Drop a Grid Block — verify `<thead>` is auto-scaffolded
6. Save — verify a new row in `TemplateVersions` is created and `CurrentVersionId` is updated
7. Open Version History — verify all versions listed, Restore creates a new version with the old body
8. Open Preview with sample JSON — verify rendered HTML matches expected output

### NuGet (Runtime)
1. Install `TemplateBuilder.Core` in a test console app
2. Register with `AddTemplateBuilder` pointing at the TemplateBuilder SQL database
3. Call `RenderAsync` with a templateId and a matching model — verify correct HTML string returned
4. Call `RenderByNameAsync` with a template name — verify same result
5. Pass a wrong templateId — verify `TemplateNotFoundException` thrown
6. Introduce a Scriban syntax error in a template body via the designer — verify `TemplateRenderException` thrown on next render call
7. Call `RenderAsync` twice for the same template — verify second call is served from cache (no DB query)
8. Save a new version in the designer — verify next `RenderAsync` call returns updated content (cache invalidated)
