# TemplateBuilder.Editor.Mvc5 → TemplateBuilder.Editor backport status

Tracks porting work items identified from `TemplateBuilder.Editor.Mvc5` (the .NET Framework 4.8
fork) since its v1.3.1, back into this repo's `TemplateBuilder.Editor` (net8.0/net10.0).
Fork source repo: `C:\Users\nchinnam\source\repos\TemplateBuilder.Mvc5`.

Status values: `pending` | `in-progress` | `done`

## Small bug-fix batch

| # | Item | Fork commit(s) | Status | Notes |
|---|------|-----------------|--------|-------|
| 1 | SunEditor stale-width gap in the CANVAS panel on Edit/Create pages — `SUNEDITOR.create` needs an explicit `width: '100%'` | `bfe6495` (v1.3.7) | done | Applied `width: '100%'` next to `height: '100%'` in `template-editor.js` (same one-line fix as fork). `node --check` passes. |
| 2 | Activity drawer shake — `preventScroll` on focus, tab animates via `transform` | `113eb64` | done | Already fixed independently in origin (`5fa7933`, "fix: activity drawer open causing a full-page scroll jump") — same `preventScroll` pattern, both `openDrawer`/`closeDrawer` covered. No port needed. Note: `113eb64` also bundled the avatar/chip timeline markup — that part is still pending, tracked under item 5. |
| 3 | Activity drawer's open/close listeners duplicated on every Save Version | `bad5149` (v1.3.12) | done | Not applicable here — origin's drawer code is already structured as a single `initActivityDrawer()` IIFE that wires the tab/close-button/Escape listeners exactly once, outside `refreshTimeline()` (its `loadTimeline()` equivalent). The fork's older architecture re-wired listeners inside `loadTimeline()` on every call; origin never had that shape, so the duplication bug can't occur. No port needed. |
| 4 | Code-view textarea still narrow when a host page styles bare textareas | `18b41f2` (v1.3.13) | done | Origin never had the earlier v1.3.9/v1.3.10 `width: 100% !important` fix either (only `height` was set on `[id*="code-viewer"]`), so ported both `width` and `max-width: 100% !important` together onto the same selector in `template-editor.css`. |

## UI feature

| # | Item | Fork commit(s) | Status | Notes |
|---|------|-----------------|--------|-------|
| 5 | Refined activity timeline — action chips, actor avatars, equal-height audit cards | `8b59327` | done | Equal-height audit cards were already done independently in origin (`8d45a56`, `align-items: stretch` on `.tb-audit-top`). Ported action chips + avatar initials: added `avatarInitials()` and restructured `renderTimeline()` in `template-editor.js`; added `.tb-activity-avatar`/`.tb-activity-chip`/`.tb-activity-meta` + `--accent-border` var in `template-editor.css`, replacing the old dot+plain-text row. Adapted to origin's existing `tb-activity-*` naming (fork uses `tb-timeline-*`) and its already-transform-based tab/drawer animation, which origin had independently. Skipped the fork's bundled SVG icon polish (tab icon, header icon, icon close-button) as out of scope for this item's stated title. `dotnet build` clean; visual confirmation in-browser blocked by a Chrome-extension connectivity issue (couldn't reach `localhost:5299`) — sample host is running for manual check. |

## Infrastructure feature

| # | Item | Fork commit(s) | Status | Notes |
|---|------|-----------------|--------|-------|
| 6 | DBA-managed databases — generated schema script + `options.ApplyMigrations` (no DDL for app login) | `a297df8`, `7495603` | done | Ported with an EF-Core-native design, simpler than the fork's EF6 approach (no static-initializer trick needed): added `TemplateBuilderEditorOptions.ApplyMigrations` (default `true`); `AddTemplateBuilderEditor` now only registers `MigrationHostedService` as a hosted service when it's `true`. Generated `src/TemplateBuilder.Editor/Scripts/TemplateBuilder.schema.2.3.0.sql` via `dotnet ef migrations script --idempotent` (no live DB needed) and wired it into the nupkg via a `Pack` entry in the csproj. Added a golden-file test (`SchemaScriptGenerationTests`, net10.0-only — EF Core 8 vs 10 format migration scripts slightly differently, so the gate is pinned to one canonical generator) that regenerates the script in-process via `IMigrator` and compares it to the committed file (`TB_REGEN_SCHEMA=1` to regenerate), plus `ApplyMigrationsOptionTests` verifying the hosted service is/isn't registered. Documented in README (`## Database` > new "DBA-managed database" subsection + `What's New`). Also backfilled README `What's New` bullets for items 1 and 4 (skipped earlier), matching repo convention of documenting every fix. Full solution build + test suite green (`dotnet test TemplateBuilder.slnx`: 56+56+36+69+70 = all passed). No live-UI verification needed — backend/packaging-only change. |

## Large cross-cutting feature

| # | Item | Fork commit(s) | Status | Notes |
|---|------|-----------------|--------|-------|
| 7 | Subject field on `TemplateVersion`, end-to-end (Domain, EF Core mapping + migration, `RenderEmailAsync`, Template Promotion export/import schema v3, DTOs, Create/Edit/SaveVersion/Preview/GetVersionBody/Restore/Duplicate wiring, editor JS, Subject row styling) | `6fde994`..`d96d276` (v1.3.8–v1.3.11) | done | Implemented per plan `docs/superpowers/plans/2026-09-04-email-subject-field.md` across 9 commits (Domain/`RenderEmailAsync` → EF Core mapping + migration + schema script → Promotion export/import schema v3 → DTOs → Create/Edit/SaveVersion/Preview/GetVersionBody/Restore/Duplicate wiring → Edit.cshtml markup → editor JS → Preview/Compare Subject row styling). Full solution test suite green (`dotnet test TemplateBuilder.slnx`: 3+3+60+60+74+38+75 = 313 passed, 0 failed) and full build clean (0 errors, same pre-existing NU1510 warnings). Live end-to-end verified via `curl`: created an Email template with `subject:"Order {{ model.OrderId }} shipped"` through `/Templates/Create`, fetched its `/Templates/{id}/Edit` page, and confirmed `id="prop-subject"` renders with that exact value. Browser-based visual verification was attempted but blocked again by the same Chrome-extension connectivity issue noted under item 5 (`navigate`/tab-context report success but the frame shows an error page) — only the `curl` round-trip is confirmed, not the Type-toggle visual behavior. README `What's New` (`v2.3.0`) updated. Version bump/packaging/publish left as a separate follow-up decision, matching items 1/4/5/6. |

## Not portable

| Item | Fork commit | Reason |
|------|-------------|--------|
| LayoutPath views failing on real IIS/.NET Framework hosting | `64aced1` (v1.3.6) | Mvc5/RazorGenerator/IIS-specific; no equivalent in this repo's ASP.NET Core hosting model |
