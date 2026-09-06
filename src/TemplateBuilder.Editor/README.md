# TemplateBuilder.Editor

**Current version: 3.0.1**

Embed a full Scriban-powered HTML template management UI into any ASP.NET Core web application. Install the package, call two methods, and your users can create, edit, version, preview, and restore templates — all wrapped in your own site layout.

---

## Requirements

- .NET 8 or .NET 10
- ASP.NET Core MVC
- SQL Server

---

## Quick Start

### 1. Install

```bash
dotnet add package TemplateBuilder.Editor --version 3.0.1
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

## Access Control

By default the editor is **open to all users** — no authentication is required. To restrict access, configure `options.Authorization` inside `AddTemplateBuilderEditor()`.

### Anonymous (default — no change required)

```csharp
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    // options.Authorization.Mode defaults to Anonymous
});
```

### Authenticated users only

Any signed-in user can access the editor.

```csharp
using TemplateBuilder.Editor.Authorization;

builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.Mode = TemplateBuilderAuthorizationMode.Authenticated;
});
```

### Role-based access

A user in **any** of the listed roles is granted access (OR logic).

```csharp
using TemplateBuilder.Editor.Authorization;

// Single role
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.Mode = TemplateBuilderAuthorizationMode.Role;
    options.Authorization.RoleNames = ["Admin"];
});

// Multiple roles — user only needs one
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.Mode = TemplateBuilderAuthorizationMode.Role;
    options.Authorization.RoleNames = ["Admin", "Supervisor", "SuperAdmin"];
});
```

### Custom policy (escape hatch)

For advanced scenarios (claims-based, multi-tenant, composite rules) — register a named ASP.NET Core authorization policy yourself and hand its name to the editor:

```csharp
// 1. Register your own policy
builder.Services.AddAuthorization(o =>
    o.AddPolicy("TemplateEditorAccess", pb =>
        pb.RequireAuthenticatedUser()
          .RequireClaim("department", "Engineering", "Content")));

// 2. Point the editor at it
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = ...;
    options.Authorization.PolicyName = "TemplateEditorAccess";
});
```

### Middleware prerequisite

For any mode other than Anonymous your pipeline must include both auth middlewares, in this order:

```csharp
app.UseAuthentication(); // must come before UseAuthorization
app.UseAuthorization();
```

### What is protected

The authorization convention is applied at the assembly level — every editor route is covered:

| Route group | Protected |
|---|---|
| `/Templates/*` (list, create, edit, versions, preview, duplicate, validate, toggle) | Yes |
| `/Templates/Api/Snippets` (GET / POST / DELETE) | Yes |
| `/Templates/_setup` (Development diagnostic) | Yes |

---

## Author Identity (CreatedBy)

Every TemplateBuilder table that records an author (`TemplateVersion.CreatedBy` and the
audit log `Actor`) is stamped with the current user, resolved in this order:

1. **`options.ActorResolver`** (your custom resolver, if set)
2. `User.Identity.Name`
3. `"anonymous"`

Without configuration the editor stores `User.Identity.Name` (or `"anonymous"` when the
request is unauthenticated or the name is empty). Existing records are never backfilled —
legacy rows are stored with `CreatedBy = null`.

Supply your own identity from your existing `AddTemplateBuilderEditor` call — e.g. a
claims value:

```csharp
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = connectionString;
    // Store the "sub" claim (or any claim / custom user lookup) as the author
    options.ActorResolver = ctx => ctx.User?.FindFirst("sub")?.Value;
});
```

The resolver receives the request's `HttpContext`, so it can read claims, session, or any
of your own services captured in the closure. It runs once per request; a `null` or blank
result falls back to the chain below it. Values are stored as returned — trim inside the
resolver if your source may carry stray whitespace. The stored value is truncated to 100
characters (the column limit). Exceptions thrown by your resolver propagate.

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

### v3.0.1

- **Fixed (security)**: the template list's **Duplicate** button built its click handler by
  splicing the Template Name directly into an inline `onclick` attribute, escaping only single
  quotes. A name containing a double quote broke out of the attribute, allowing stored HTML/JS
  injection via the Name field. The id/name are now passed through `data-*` attributes instead,
  which are HTML-encoded like any other Razor output.
- **Fixed**: creating a template with an empty or duplicate name (or any other rejected `Create`
  request) always showed a generic "Network error — please try again." instead of the server's
  actual validation message. The error branch called a helper function, `errMessage()`, that
  didn't exist in the codebase — the resulting `ReferenceError` was silently swallowed and masked
  every real error behind the generic fallback. A required-name check now also short-circuits
  before the request is even sent.
- Export and version-save confirmations now stay visible for 4 seconds instead of 2.5, and
  exporting templates from the list page shows a confirmation toast.

### v3.0.0

- **Breaking: `ITemplateEngine` gains a new member, `RenderEmailAsync(templateId, model)`**,
  returning `RenderedEmail { Subject, Body }`. Source-breaking for any external
  implementation or mock of `ITemplateEngine` — add the method to compile against this
  version. Calling through the interface (the common case) is unaffected.
- **Breaking: Template Promotion schema bumped `2 → 3`.** Promotion files exported before
  this release (`schemaVersion: 2`, no `Subject` field) are now **rejected on import**, not
  silently upgraded — matching this package's existing precedent for schema-version changes.
  Re-export from a current instance if you need to re-import an older file.
- **Subject field for Email templates** — an optional `Subject` on `TemplateVersion`, versioned
  alongside `Body`. Shown only when a template's Type is `Email`; supports the same
  `{{ model.X }}` Scriban syntax and field-palette insertion as the body. Rendered via
  `RenderEmailAsync` (above); never passed through the HTML sanitizer (it's plain text).
  Carried through Preview, Restore, Duplicate, Compare, and Template Promotion export/import.
- **DBA-managed databases** — new `TemplateBuilderEditorOptions.ApplyMigrations` (default
  `true`). Set it to `false` and the package never registers its migration hosted service
  and never attempts DDL — for SQL logins with DML-only rights. The package now ships a
  generated schema script (`Scripts/TemplateBuilder.schema.<version>.sql`) that a DBA runs
  once to provision the database (all tables, indexes, and migration-history rows;
  generated from the migration chain so it cannot drift). Upgrades that change the schema
  ship a new versioned script file.
- **Refined activity timeline** — each event now shows the actor as an initials avatar and
  the action as a color-coded chip (published = green, deleted/rejected = red,
  restored/toggled/duplicated/imported = amber, everything else = indigo), replacing the
  previous plain-text action label and status dot.
- **Fixed**: `TemplateVersion.CreatedBy` was truncated to 200 characters in code but the
  column is only 100 — a resolver returning a longer value threw a SQL truncation error
  on save instead of a clean truncation. Now truncated to the actual 100-char limit.
- **Fixed**: `Import` and `BulkDelete` bypassed the configured `ActorResolver` chain (or
  silently swallowed a resolver's exception) instead of honoring it like every other
  write path.
- **Fixed**: the WYSIWYG canvas rendered at a stale, permanently-too-narrow width —
  `SUNEDITOR.create()` had no `width` option, so SunEditor froze whatever pixel width it
  measured at creation time as a permanent inline style. Added `width: '100%'` alongside
  the existing `height: '100%'`.
- **Fixed**: the code view's textarea could still be capped narrow by a host page's own
  generic `textarea { max-width: ... }` styling (a common Bootstrap/form-control pattern),
  since `width: 100%` alone doesn't contest `max-width`. Added `max-width: 100% !important`
  alongside `width`.
- **Fixed**: clicking **Create Template** after typing anything into the form triggered a
  native "Leave this page?" browser prompt immediately after a successful save — the
  post-save redirect never cleared the page's dirty flag, so its own unsaved-changes guard
  blocked its own navigation.
- **Fixed**: inserting a field token (via the field palette, the toolbar's "Insert Field"
  dropdown, or drag-and-drop) never wrapped it in its intended `tb-field` chip span — it
  landed as plain, unstyled text. The token still worked correctly as a Scriban placeholder;
  only the editor's visual distinction between literal text and dynamic fields was affected.
- **Fixed**: selecting a Source SQL View on the Create form was silently dropped — the new
  template always landed on its Edit page with "— None —" selected, and the field palette
  had to be manually reselected.

### v2.3.0

- New `TemplateBuilderEditorOptions.ActorResolver` — supply your own author identity
  (claims, user id, username) stored as `CreatedBy` / audit `Actor`. Falls back to
  `User.Identity.Name`, then `"anonymous"` when unset.
- Template version history now stamps `CreatedBy` on every save (previously never
  populated); existing versions are not backfilled.

### v2.2.1
- **Fix**: `AuditController`'s filter parameter was literally named `action`, which collides with ASP.NET Core MVC's reserved `action` route value (the executing action method's own name). Every request to `/Audit`, `/Audit/Stats`, and `/Audit/Export` silently returned zero rows, filtered or not — the entire audit page was non-functional over real HTTP despite passing unit tests (which call the controller directly in C#, bypassing routing). Renamed the parameter to `actionName` with `[FromQuery(Name = "action")]` so the query-string contract (`?action=published`) is unchanged.
- **Fix**: `AuditLog.OccurredAt` round-tripped from SQL Server as `DateTimeKind.Unspecified`, dropping the `Z` from ISO timestamps and skewing every relative-time display ("2m ago") by the local UTC offset. Forced to UTC via an EF Core value converter.
- **Fix**: three unrelated top-level statements in the shared `template-editor.js` bundle assumed globals/elements that only exist on the Templates Create/Edit page (the `SUNEDITOR` CDN global, a `field-palette` element, and the `templateId` variable Edit.cshtml declares inline) — none guarded. An uncaught exception at the top level of a script aborts every later top-level statement in that same script, so the very first of these silently killed the Audit page's entire client-side behavior (chart, relative timestamps, filters, live poll) while the server-rendered HTML still looked complete. Guarded all three.
- **Fix**: opening the Edit page's Activity drawer caused a jarring full-page scroll jump. The drawer focused its close button before its slide-in animation had actually made it visible, and browsers auto-scroll to reveal a newly focused off-screen element. Fixed with `{ preventScroll: true }` on the drawer's focus calls.
- **Polish**: audit page — chart and filter cards now sit side by side and match height; numbered pagination (page pills) replaces plain Previous/Next; Template entity cells link to that template's Edit page; the live pill is now an always-visible pulsing indicator that turns actionable ("N new — Refresh") instead of being invisible until there's news; filter card's search input shares a row with its Filter button, and Clear moved beside the To date field.
- **Polish**: activity drawer — the vertical tab now travels with the drawer as it opens and sits above it in the stacking order (previously it vanished behind the opened drawer); timeline items now show a connecting line between ring-style dots instead of a flat bulleted list; added `prefers-reduced-motion` support, previously missing entirely from this stylesheet.

### v2.2.0
- **Audit log** — every meaningful template and snippet mutation is now recorded as an append-only `AuditLog` row: `created`, `draft_saved` (Save Draft), `published` (Save Version), `restored`, `duplicated`, `toggled_active`, `imported`, `deleted` for templates; `snippet_created`, `snippet_deleted` for snippets. Rows are never updated or deleted, and the entity id is stored without a foreign key so history survives hard deletes. Only mutations are audited — reads, renders, and health checks are not.
- **Supersedes the 2.1.0 "no audit trail" decision for two actions**: importing a template (`POST /Templates/Import`) now records `imported`, and bulk delete (`POST /Templates/BulkDelete`) records `deleted` per template. Bulk activate/deactivate remain unaudited.
- **Global audit page** — `GET /Audit` lists every audit row with filters (search, entity type, action, actor, date range), five stat chips (total / templates / snippets / actors / date range), a 30-day activity chart, color-coded action badges, expandable before/after state per row, windowed pagination, and a 30-second live-poll pill that flags new activity without an auto-refresh. `GET /Audit/Stats` (JSON) powers the chips/chart/poll; `GET /Audit/Export` downloads a CSV (`OccurredAt,EntityType,EntityId,Action,Actor,Comment,BeforeState,AfterState`, UTF-8 with BOM, quoted fields).
- **Activity drawer on the Edit page** — a right-edge slide-in drawer (open via the vertical "Activity" tab, which shows a live count badge) lists the selected template's own timeline, day-grouped with action-colored dots, fed by `GET /Templates/{id}/Audit` (last 100 events, newest first).

### v2.1.0
- **Export / Import (promotion)** — Export any template to a versioned JSON file (`GET /Templates/Export/{id}`) and import it into another environment (`POST /Templates/Import`, multipart file upload). Each template carries a stable `ExternalKey` (a `Guid`, unique, backfilled on migration) that survives renames — import matches on that key: a match updates the existing template in place and appends the imported versions starting at `max + 1` (preserving each version's Draft/Active flag and the template's own active flag exactly); no match creates a new template with its original version numbers and flags intact. There is no skip/collapse behavior — every import either updates or creates. The exported JSON also includes `sampleData` (nullable string) so a template's saved preview data round-trips with it on both the create and update-by-key-match paths.
- **Template health check** — `TemplateHealthService` parses a template's body via the Scriban AST (not regex) to extract every `model.*` field it references, then compares those fields against the live schema of the SQL view configured as the template's **Source View** (new Properties-panel field, saved via Save Draft/Save Version). Findings: **Critical** — `view_missing` (the configured view no longer exists) or `column_missing` (a referenced field has no matching column); **Warning** — `column_type_changed`, `column_length_changed`, `column_nullability_changed` (the column changed shape since the last snapshot), or `unbound_tokens` (fields referenced with no Source View configured at all). A snapshot of the view's columns is taken and stored with the template whenever the Source View changes, so drift is measured against what was true when it was last bound, not just what's true now. Nested/dotted paths (e.g. `model.Order.Total`) are extracted as tokens but excluded from column-level drift comparison (only top-level fields are checked against columns). `GET /Templates/{id}/Health` returns the full report as JSON; the editor's footer Health button renders it inline.
- **Health overview page** — `GET /Health` lists every template (including inactive ones) with Healthy / Warning / Critical / Unbound chips and a per-template finding table, so drift across your whole template library is visible at a glance without opening each one.
- **Health summaries** — `GET /Health/Summaries?ids=1,2,3` returns lightweight per-template severity + finding-count JSON, used to badge the template list without running a full health check inline for every row.
- **Bulk operations** — The template list gains a checkbox column and a selection toolbar: **Activate**, **Deactivate**, **Export** (downloads a ZIP with one `.template.json` per selected template plus a `_summary.json` manifest, via `POST /Templates/BulkExport`), and **Delete** (`POST /Templates/BulkDelete`, permanent — removes all versions then the template; there's no single-template delete UI, bulk only). Activate/Deactivate (`POST /Templates/BulkActivate` / `BulkDeactivate`) skip templates already in the target state. All four bulk endpoints return a `{ succeeded, failed }` result per id.
- **No audit trail** — none of the above (import, bulk activate/deactivate/delete) writes an audit log entry; this is a deliberate scope decision, not an oversight.

### v2.0.0
- **Two-state save model** — each template version is now either **Draft** or **Active**. The toolbar gains a **Save Draft** button alongside **Save Version**: Save Draft creates a new version marked Draft (safe to iterate without affecting what renders live); Save Version creates a new version marked Active. Draft versions show a **"Draft version"** badge in the editor and a **Draft** badge in the History panel.
- **Breaking: render API now serves the last Active version, not simply the newest one.** `ITemplateEngine.RenderAsync` / `RenderByNameAsync` walk version history for the highest-numbered version with `IsActive = true` — a newer Draft version is skipped. See the [Render Templates in Code](#render-templates-in-code) section below for the two new exceptions this introduces.
- **Autosave and Create behavior are unchanged** — the localStorage autosave/restore flow and template Create still work exactly as before; only the two Save buttons and their History/badge treatment are new.

### v1.6.0
- **JSON Create endpoint** — `POST /Templates/Create` now takes a JSON body instead of a form post, matching every other write endpoint.
- **Server-side sample-data generation** — generate realistic preview-model JSON from a SQL view's columns, from the template's `{{ model.X }}` tokens, or both; save it to the template so Preview works immediately on your next visit. New endpoints: `POST /Templates/Api/SampleData/Generate`, `PUT /Templates/{id}/SampleData`.
- **Scriban syntax reference panel** — searchable in-editor reference with 15 example snippets, click to insert at the caret.
- **Palette search & used-field marks** — filter the field palette live; fields already used in the body are marked automatically.
- **`AllowTopLevelModelAccess` option (opt-in, default off)** — lets templates use `{{ Name }}` in addition to `{{ model.Name }}`. See the XML doc on `TemplateBuilderOptions.AllowTopLevelModelAccess` for the builtin-name-shadowing tradeoff before enabling.
- **CSS scoping fixes** — closed real gaps in `#tb-editor-host` scoping and fixed several icons/panels that were sized in `rem` (relative to the *host page's* root font-size, not the editor) — could render up to ~37% smaller if your app resets root font-size (e.g. Bootstrap 3).

### v1.5.2
- **Fix**: This package README was still advertising 1.4.5 as the current version after the 1.5.0/1.5.1 releases — it's now kept in sync with the actual package version on every release.

### v1.5.1
- **Dependency updates** — HtmlSanitizer 9.0.892 → 9.2.995 and Scriban 7.2.0 → 7.2.6, clearing known moderate/high-severity NuGet security advisories. Microsoft.Data.SqlClient, EF Core, and `Microsoft.Extensions.*` packages bumped to their latest patch releases on each supported line (net8.0 → 8.0.30 / 8.0.x, net10.0 → 10.0.11). No breaking changes.

### v1.5.0
- **.NET 8 support** — The package now multi-targets `net8.0;net10.0`. The published NuGet package contains both `lib/net8.0/` and `lib/net10.0/` asset folders, so it's consumable by .NET 8 (LTS) projects in addition to .NET 10 (LTS). No source or API changes.

### v1.4.5
- **Fix**: Template list page now shows the correct version number for each template. `GetAllAsync` was missing `.Include(t => t.CurrentVersion)`, causing `CurrentVersion` to always be `null` and every row to display `v0`.

### v1.4.4
- **Side-by-side Version Compare** — Click **Compare** on any version in the History panel to open a full-width compare modal. The current editor content renders on the left; the selected old version renders on the right — both use auto-generated sample JSON so loops and grids populate correctly. A **Restore vN** button in the right panel restores directly from the compare view. **← History** returns to the version list without losing context.

### v1.4.3
- **Auto-fill for Loop & Grid blocks** — "⚡ Auto-fill from template" now generates sample array data for Loop Block and Grid Block collections, not just top-level scalar fields. Click the button on any template that contains a `{{ for item in model.Items }}` loop — the JSON textarea is populated with a 2-item array including all referenced item fields (e.g. `ProductName`, `Qty`, `UnitPrice`). Second array items append a ` 2` suffix for visual distinction in the preview. Existing scalar field behaviour is unchanged.

### v1.4.2
- **Fix**: Draft banner ("Unsaved draft found") no longer appears on page load when there is no saved draft — CSS specificity bug caused `display:flex` to override the `[hidden]` attribute. Dismiss (Restore / Discard) buttons work correctly when a real draft exists.
- **Fix**: Validate panel no longer appears on page load — same CSS `[hidden]` specificity fix.
- **Preview auto-fill**: "⚡ Auto-fill from template" button parses `{{ model.X }}` placeholders directly from the current editor content and fills the JSON textarea with sample string values (`"Sample FieldName"`). Works for every template regardless of SQL view usage — button is always enabled.

### v1.4.1
- **Fix**: Special Characters floating panel (Ω) no longer visible on page load; close button now functions correctly.
- **Fix**: Find & Replace floating panel no longer visible on page load — same CSS `[hidden]` specificity fix applied.

### v1.4.0
- **Modern SaaS UI redesign** — New design token system with Inter font and CSS custom properties for color, shadow, and radius, all scoped to `#tb-editor-host`. Refined light and dark themes with consistent tokens across all panels.
- **Card-style Field Palette** — SQL view columns displayed as cards with a hover-reveal Insert button and data type label.
- **Badge system** — Type badges (Email, Report, Notice, Custom) and status badges (Active/Inactive) with matching light/dark variants on the template list page.
- **Version History polish** — Version entries rendered as cards with a "Current" indicator and per-entry Restore buttons.
- **Panel heading icons** — Visual icons on Field Palette (⊞), Canvas (✏), and Properties (⚙) panel headings.

### v1.3.7
- **Access Control** — Configure authorization mode via `options.Authorization` in `AddTemplateBuilderEditor()`. Four modes supported: `Anonymous` (default, fully backward compatible), `Authenticated` (any signed-in user), `Role` (one or more roles — OR logic), and `PolicyName` escape hatch (delegate to any named ASP.NET Core policy). All editor routes including the snippets API are covered automatically via an assembly-scoped `IControllerModelConvention` — no controller source changes required.

### v1.3.6
- **Style**: Outer borders added to Field Palette (left) and Properties (right) panels, framing the 3-panel layout symmetrically.
- **Default**: Editor now opens in light mode for new users (was dark).
- **Default**: Auto-save now defaults to OFF for new users (was ON). Both remain user-overridable via the toolbar toggles.

### v1.3.5
- **Fix**: Editor canvas now scrolls to show full template content — `.tb-canvas-body` was clipping overflow instead of scrolling, hiding everything below the initial viewport height.

### v1.3.4
- **Fix**: Snippets panel now loads correctly — `ISnippetRepository` was missing from `AddTemplateBuilderEditor()` DI registrations, causing a 500 on `GET /Templates/Api/Snippets`.

### v1.3.3
- **Fix**: Editor canvas no longer blank on load — the custom anchor plugin was renamed from `anchor` to `insertAnchor` to avoid colliding with SunEditor's internal `core.context.anchor` context used by the built-in link plugin.

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
| Version body (for compare) | `GET /Templates/{id}/Versions/{versionId}/Body` |
| Restore version | `POST /Templates/{id}/Restore/{versionId}/{sourceVersionNumber}` |
| Live preview | `POST /Templates/{id}/Preview` |
| Duplicate | `POST /Templates/{id}/Duplicate` |
| Validate syntax | `POST /Templates/{id}/Validate` |
| Toggle active | `POST /Templates/{id}/ToggleActive` |
| List snippets | `GET /Templates/Api/Snippets` |
| Create snippet | `POST /Templates/Api/Snippets` |
| Delete snippet | `DELETE /Templates/Api/Snippets/{id}` |
| Generate sample data | `POST /Templates/Api/SampleData/Generate` |
| Save sample data | `PUT /Templates/{id}/SampleData` |
| Export template | `GET /Templates/Export/{id}` |
| Import template export file | `POST /Templates/Import` |
| Bulk activate | `POST /Templates/BulkActivate` |
| Bulk deactivate | `POST /Templates/BulkDeactivate` |
| Bulk export | `POST /Templates/BulkExport` |
| Bulk delete | `POST /Templates/BulkDelete` |
| Template health check | `GET /Templates/{id}/Health` |
| Health overview page | `GET /Health` |
| Health summaries | `GET /Health/Summaries?ids=1,2,3` |
| Setup check | `GET /Templates/_setup` *(Development only)* |
| Audit log page | `GET /Audit` |
| Audit stats (chips/chart/poll) | `GET /Audit/Stats` |
| Audit CSV export | `GET /Audit/Export` |
| Template audit timeline (Activity drawer) | `GET /Templates/{id}/Audit` |

---

## Theming

The editor ships with a **light theme** by default. A **☀ Light / 🌙 Dark** toggle button appears in the CANVAS panel heading and persists the preference in `localStorage`.

The editor's styles are fully scoped to `#tb-editor-host` using CSS custom properties — they do not affect the rest of your application. The editing canvas is always white (document-like) regardless of the selected theme.

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

`RenderAsync` / `RenderByNameAsync` serve the **last Active version** — the highest-numbered version with `IsActive = true` — not simply the newest version. A version saved via **Save Draft** is skipped until it is promoted with **Save Version**. For Email-type templates, `RenderEmailAsync(templateId, model)` renders both `Subject` and `Body` together, returning `RenderedEmail { Subject, Body }`. As of `2.0.0`, three typed exceptions (`TemplateBuilder.Domain.Exceptions`) can surface from either method:

| Exception | Thrown when |
|---|---|
| `TemplateNotFoundException` | No template with that ID/name exists |
| `TemplateInactiveException` | The template itself has been deactivated (`Template.IsActive == false`, via **Toggle Active**) |
| `NoActiveVersionException` | The template exists and is active, but every saved version is a Draft — there is nothing Active to render |

**Breaking change from `1.x`:** previously, an inactive template's render call threw `TemplateNotFoundException`. It now throws the more specific `TemplateInactiveException`. Catch both if you need to preserve the old fallback behavior.

---

## Lifecycle & Ops

Export/import, health checks, and bulk operations, added in `2.1.0` for moving templates between environments and keeping a large template library trustworthy.

### Export / Import

Every template has a stable `ExternalKey` (`Guid`, unique) that identifies it across environments — it's assigned on creation and **survives renames**, so it's what promotion matches on, not the template name.

- **Export** — `GET /Templates/Export/{id}` downloads a `schemaVersion: 3` JSON file: the template's metadata (`externalKey`, `name`, `templateType`, `description`, `sampleData`, `isActive`) plus every version (`versionNumber`, `body`, `subject`, `changeComment`, `createdAt`, `createdBy`, `isActive`). **Breaking in `2.3.0`:** files exported at `schemaVersion: 2` (before the `subject` field existed) are now rejected on import, not silently upgraded — re-export from a current instance if you need to re-import an older file.
- **`sampleData` is included in the exported/imported JSON** — it's a nullable string, preserved on both the create and update-by-key-match import paths, so a template's saved preview data travels with it during promotion.
- **Import** — `POST /Templates/Import` takes a multipart file upload of an exported JSON file:
  - **Key match** (an existing template has the same `ExternalKey`) → updates that template's metadata and `IsActive` in place, then **appends** the imported versions starting at `max version number + 1`, preserving each version's `IsActive` flag exactly as exported.
  - **No key match** → creates a new template, keeping the imported `ExternalKey`, original version numbers, and original flags.
  - There is **no skip/collapse** behavior — an import always either updates or creates; a version is never silently dropped or merged into another.
  - Any version body that fails to parse as valid Scriban, or a file with the wrong `schemaVersion`/missing name/type/versions, is rejected with an error entry rather than partially imported.
- **Bulk export** — `POST /Templates/BulkExport` (`{ ids: [...] }`) downloads a ZIP containing one `{Name}.template.json` per selected template plus a `_summary.json` manifest.

### Template health check

`TemplateHealthService` walks a template body's **Scriban AST** (not a regex) to find every `model.*` field it actually references, then compares those fields against the live column schema of the SQL view configured as the template's **Source View** (a field in the editor's Properties panel, saved via Save Draft/Save Version). A snapshot of the view's columns is taken whenever the Source View changes, so drift is measured against what was true when the view was last bound.

| Finding | Severity | Meaning |
|---|---|---|
| `view_missing` | Critical | The configured Source View no longer exists |
| `column_missing` | Critical | A field the template references has no matching column in the view |
| `column_type_changed` | Warning | The column's SQL data type changed since the last snapshot |
| `column_length_changed` | Warning | The column's max length changed since the last snapshot |
| `column_nullability_changed` | Warning | The column's nullability changed since the last snapshot |
| `unbound_tokens` | Warning | The template references `model.*` fields but has no Source View configured at all |

Nested/dotted paths (e.g. `model.Order.Total`) are extracted as tokens but are excluded from column-level drift comparison — only top-level fields are checked against columns.

- `GET /Templates/{id}/Health` returns the full report as JSON; the editor's footer Health button renders it inline for the template you're editing.
- `GET /Health` is an overview page listing every template (including inactive ones) with Healthy / Warning / Critical / Unbound chips and a per-template finding table.
- `GET /Health/Summaries?ids=1,2,3` returns lightweight `{ templateId, severity, findingCount }` JSON per id, used to badge rows on the template list without running a full check inline.

### Bulk operations

The template list has a checkbox column and a selection toolbar:

| Action | Route | Behavior |
|---|---|---|
| Activate | `POST /Templates/BulkActivate` | Sets `IsActive = true`; templates already active are counted as succeeded, not re-processed |
| Deactivate | `POST /Templates/BulkDeactivate` | Sets `IsActive = false`; same already-in-state handling |
| Export | `POST /Templates/BulkExport` | Downloads a ZIP (see Export / Import above) |
| Delete | `POST /Templates/BulkDelete` | **Permanent** — removes all versions, then the template itself. There is no single-template delete UI; delete is bulk-only |

All four return `{ succeeded: [...], failed: [{ id, reason }] }`. As of `2.2.0`, **import and bulk delete write an audit log entry** (see Audit & Activity below); bulk activate/deactivate remain unaudited.

---

## Audit & Activity

Every meaningful template and snippet mutation is recorded as an append-only audit log entry — rows are never updated or deleted, so history survives even a hard delete of the template or snippet itself.

**Audited actions** — Template: `created`, `draft_saved` (Save Draft), `published` (Save Version), `restored`, `duplicated`, `toggled_active`, `imported`, `deleted` (bulk delete). Snippet: `snippet_created`, `snippet_deleted`. Reads, renders, and health checks are never audited — only mutations.

### Global audit page

`GET /Audit` shows every audit row across all templates and snippets:

- **Filters** — free-text search (matches action, actor, comment), entity type, action, actor, and a from/to date range.
- **Stat chips** — total events, template events, snippet events, unique actors, and the covered date range.
- **30-day activity chart** — a bar chart of daily event counts, rendered client-side with no external chart library.
- **Action badges** — each row is color-coded by action.
- **Before/after state** — expand any row to see the JSON state captured before and/or after the mutation.
- **Live-poll pill** — checks for new activity every 30 seconds and offers a one-click refresh without reloading the page.
- **CSV export** — `GET /Audit/Export` downloads `OccurredAt,EntityType,EntityId,Action,Actor,Comment,BeforeState,AfterState` (UTF-8 with a BOM, fields quoted when they contain a comma/quote/newline).
- `GET /Audit/Stats` (JSON) is the same filtered data that powers the chips, chart, and live-poll pill — useful if you want to build your own dashboard.

### Activity drawer (Edit page)

A vertical "Activity" tab on the right edge of the editor grid — with a live count badge — opens a slide-in drawer showing the selected template's own timeline: `GET /Templates/{id}/Audit` returns its last 100 events (newest first), rendered day-grouped with action-colored dots. The drawer never affects the editor grid's layout; it's absolutely positioned and opens/closes without disturbing the panels underneath.

---

## Database

`AddTemplateBuilderEditor()` registers a hosted service that runs EF Core migrations on startup. No manual migration steps are required.

### DBA-managed database (app login is DML-only)

If your SQL login has no DDL rights (no `CREATE TABLE`/`ALTER` — a common enterprise constraint), the app cannot run migrations. Instead:

1. **Provision the schema once** — the package ships a generated SQL script: `Scripts/TemplateBuilder.schema.<version>.sql` (e.g. `TemplateBuilder.schema.3.0.0.sql`). Have your DBA run it against the target database. The script is generated from the package's EF Core migration chain, creates all tables and indexes, and records migration history so the app considers the database up to date.
2. **Tell the package not to touch DDL**:

```csharp
builder.Services.AddTemplateBuilderEditor(options =>
{
    options.ConnectionString = connectionString;
    options.ApplyMigrations = false;
});
```

With `ApplyMigrations = false` the migration hosted service is never registered and the app never attempts DDL — the app login needs only DML (SELECT/INSERT/UPDATE/DELETE).

3. **Upgrading a DBA-managed database** — every release that changes the schema ships a new versioned script (e.g. `TemplateBuilder.schema.3.1.0.sql`); have the DBA run the new file against the existing database. The runtime never runs migrations on its own when `ApplyMigrations` is `false`.

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
