# QA Hand-off: MVC5-fork Backport + Email Subject Field

**Date:** 2026-09-04
**Repo:** `TemplateBuilder` (this repo), package under test: `TemplateBuilder.Editor`
**Branch:** `main` (work committed directly here — see "Git state" below)

## 1. What this is testing

This repo ships two related NuGet packages: `TemplateBuilder.Editor` (ASP.NET Core, this repo) and a sibling `TemplateBuilder.Editor.Mvc5` (.NET Framework 4.8, a separate repo — not part of this test pass). Several fixes and one large feature were ported from the newer MVC5 fork back into this repo's `TemplateBuilder.Editor` this session. You're testing that port.

Seven items, tracked in `docs/status.md` (read it for full technical detail per item — this document is the testing guide, `docs/status.md` is the engineering record):

| # | Item | Type | Visible in UI? |
|---|------|------|-----------------|
| 1 | SunEditor canvas width fix | Bug fix | Yes |
| 2 | Activity drawer shake fix | Already fixed independently — no code changed | Yes (sanity check only) |
| 3 | Activity drawer duplicate-listener fix | Not applicable here (different architecture) — no code changed | Yes (sanity check only) |
| 4 | Code-view textarea width fix | Bug fix | Yes |
| 5 | Refined activity timeline (avatars + chips) | UI feature | Yes |
| 6 | DBA-managed databases (`ApplyMigrations` option) | Infra feature | No (backend/packaging only) |
| 7 | **Email Subject field** | **Large feature** | **Yes — this is the main event** |

**Priority for this pass: item 7, then item 5, then items 1/4.** Items 2/3/6 need only a light sanity check since no code changed for them (2/3) or nothing is UI-visible (6).

## 2. Why this hand-off exists

The session that made these changes could not get real browser automation working against this app (a Chrome-extension connectivity issue, unrelated to the app itself, blocked `mcp__claude-in-chrome` from reaching `localhost`). Everything was verified via `dotnet build`/`dotnet test` (all green — 251 tests, 0 failures) and, for item 7, one `curl`-based round-trip check. **No one has actually clicked through this in a browser yet.** That's your job — use the `agent-browser` skill/tool for this pass.

## 3. Git state — you don't need to touch git, just read this once

- Current branch: `main`. Local `main` is 11 commits ahead of `origin/main` (all for item 7 — items 1–6 are **uncommitted working-tree changes**, not commits).
- This means: the code on disk right now already has all 7 items applied, regardless of commit status. Just run the app as-is — no checkout, no branch switch, no `git pull` needed.
- Do not commit, stash, or run any destructive git command. If you need to see exactly what changed per item, `docs/status.md` links each item to its rationale; `git log --oneline -12` shows item 7's 11 commits; `git diff` shows items 1–6 (still uncommitted).

## 4. Environment setup

Requires SQL Server reachable at `localhost\SQLEXPRESS` (already configured in `src/TemplateBuilder.Web/appsettings.json`, database `TemplateBuilderClient` — EF Core creates/migrates it automatically on first run, no manual DB setup needed).

**Before starting:** kill any already-running `dotnet run` process for this app — a stale one locks the build output.

```bash
dotnet run --project src/TemplateBuilder.Web/TemplateBuilder.Web.csproj --urls http://localhost:5299
```

Wait for `Now listening on: http://localhost:5299` in the output, then navigate to **http://localhost:5299/Templates**.

**Smoke test first** (optional but recommended — confirms you're starting from a known-good state):
```bash
dotnet build TemplateBuilder.slnx --nologo -v minimal   # expect: Build succeeded, 0 errors
dotnet test TemplateBuilder.slnx --nologo -v minimal    # expect: 0 Failed across all 5 test projects
```

Useful routes:
| Route | Purpose |
|---|---|
| `/Templates` | Template list |
| `/Templates/Create` | New template (GET form) |
| `/Templates/{id}/Edit` | Edit page — most of the action happens here |
| `/Audit` | Global audit/activity log page |
| `/Templates/_setup` | Setup diagnostic page (Development env only) |

## 5. Test scenarios by item

### Item 1 — SunEditor canvas width

Open any template's Edit page. The WYSIWYG toolbar and canvas should span the **full width** of the CANVAS panel — no gap before the PROPERTIES panel on the right, toolbar buttons shouldn't wrap onto an extra row that a full-width toolbar wouldn't need.

### Items 2 & 3 — Activity drawer (sanity check only, no code changed)

Open a template's Edit page that has version history, click the vertical "Activity" tab on the right edge. It should slide open smoothly with **no page-scroll jump**. Save a version (Save Draft or Save Version) while the drawer is open/closed a few times — the tab and drawer should never get visually "stuck" (e.g. tab shifted left permanently). This exercises the same code path a sibling repo once had a real bug in — we don't expect one here, but confirm.

### Item 4 — Code-view textarea width

On the Edit page, switch the editor to **Code View** (usually a toolbar toggle button in the WYSIWYG toolbar). The raw-HTML textarea should fill the full canvas width, matching the WYSIWYG view's width.

### Item 5 — Refined activity timeline

Open the Activity drawer (vertical tab, right edge) on a template with some history. Each entry should show:
- A small circular **avatar** with 1–2 letter initials of the actor (e.g. "AB" or "?" if actor is unknown)
- A rounded, colored **chip** for the action — green for `published`, red for `deleted`/`rejected`, amber for `restored`/`toggled_active`/`duplicated`, indigo/purple for everything else
- The actor's name and a relative timestamp (e.g. "2m ago") — hover the timestamp for an absolute one
- If present, a comment shown as a distinct quoted block

Also check `/Audit`: the activity chart card and the filters card (top of page, side by side) should be **exactly the same height**.

### Item 6 — DBA-managed databases (not directly testable via UI)

No UI surface. The app booting and creating/migrating its database normally on `dotnet run` (Section 4) already exercises the default path (`ApplyMigrations = true`). Nothing further to click through — just note in your report that the app started and ran without DB errors.

### Item 7 — Email Subject field (the main feature — spend most of your time here)

**Setup:** Create a new template, set **Type = Email**. A "Subject" text input should appear in the PROPERTIES panel, directly under Template Name (element id `prop-subject`, row wrapper `prop-subject-row`).

Work through these in order — each builds on the last:

1. **Type-toggle show/hide.** With Subject filled in, switch Type to `Report` — the Subject row should hide (`display:none`). Switch back to `Email` — Subject row reappears **with your text still there** (it must never be silently cleared just from toggling Type).

2. **Field-palette insertion targeting.** Pick a SQL view with a few columns so the field palette on the left populates. Click into the **Subject** input, then click a palette field's "Insert" button — the `{{ model.X }}` token should be inserted into **Subject at the cursor position**, not into the body. Then click into the WYSIWYG body, click Insert again — token should land in the **body** this time, wrapped as a styled `tb-field` chip (existing behavior, should be unaffected).

3. **Used-field indicator.** After inserting a field into Subject only (not Body), the palette row for that field should show its "used" checkmark — confirms Subject is included in the used-field scan, not just Body.

4. **Save and reload.** Type a Subject like `Order {{ model.OrderNumber }} shipped`, click **Save Version** (or **Save Draft**). Reload the Edit page — Subject should still be there.

5. **Preview.** Open the Preview modal, provide sample JSON with an `OrderNumber` value (or use the auto-generate button), click **Render**. If Type is Email and Subject is non-empty, a "Subject: ..." line should appear above the preview iframe, showing the rendered value (e.g. "Order 42 shipped") — plain text, not HTML-escaped weirdly, no stray markup.

6. **Auto-generate sample data.** Use the "Auto-fill from template" button — fields referenced **only in Subject** (not in Body) should still get sample values generated.

7. **Compare.** Save two versions with different Subjects, open Version History, then Compare two versions. Each side of the Compare modal should show its **own** "Subject: ..." line (current vs. the older version) matching that version's actual stored Subject.

8. **Restore.** Restore an older version (via Compare or History) that had a different Subject — after restore, the Edit page's Subject input should reflect the **restored version's** Subject, not whatever was there before restoring.

9. **Duplicate — this one had a real bug fixed this session, test it carefully.** Duplicate a template that has a Subject set. Open the new copy's Edit page — its Subject should match the **source's** Subject exactly. (Before the fix, Duplicate silently dropped Subject entirely.)

10. **Non-Email templates keep their Subject data.** Create/edit a `Report`-type template, and (via the API or by briefly switching Type to Email, filling Subject, saving, then switching back to Report and saving again) confirm a non-Email template can still carry a stored Subject value without it being force-cleared — the field is just hidden for that type, by design.

11. **Dirty tracking.** On a freshly loaded Edit page, edit **only** the Subject field (don't touch Body). The page should immediately show its "unsaved changes" state the same way editing Body does (Save button becomes relevant / dirty indicator appears).

12. **Auto-save draft — this one had a real bug fixed this session, test it carefully.** Turn on the Auto-save toggle (usually near the word count / status area). Edit **only** the Subject field and wait for autosave to fire (or trigger it via whatever dev affordance exists — check the word-count/draft-status area for "Draft saved..."). Reload the page — a "Draft available" banner should appear; click **Restore** — your Subject edit should come back, not just get silently dropped. (Before the fix, an autosaved draft never captured Subject at all, so restoring one after a Subject-only edit would silently discard it.)

13. **Template Promotion export/import — deliberate breaking change, verify it's enforced.**
    - Export an Email template with a Subject: `GET /Templates/Export/{id}` downloads a `.template.json` file. Open it — confirm it's `"schemaVersion": 3` and each version object has a `"subject"` field with the right value.
    - Import that file back in (`/Templates` list page has an Import action) — confirm it round-trips (creates or updates with the right Subject).
    - **Import an old-format file** — hand-edit a copy of an exported file to `"schemaVersion": 2` (or use a file exported before this change, if one exists) and try importing it. It **must be rejected** with an error mentioning `schemaVersion` — not silently imported and not silently "upgraded." This is a deliberate breaking change this release ships; confirm it's actually enforced, not just documented.

## 6. What this session could NOT verify — your priority list

- **Nothing in this document has been visually confirmed in a real browser.** Everything above is new territory for a human/agent click-through. Treat all of Section 5 as unverified until you've actually done it.
- Item 7's only live check this session was a `curl`-based round-trip (Create → fetch Edit page HTML → confirm the Subject value is present in the rendered `<input>`). Every interactive scenario (2–13 above) is untested.
- If `agent-browser` also can't reach `localhost:5299` for some reason, say so plainly in your report rather than reporting a scenario as passed — that happened once already this session with a different browser-automation tool.

## 7. Reference material

- `docs/status.md` — the engineering tracker for all 7 items, with implementation notes and links to source commits in the sibling fork.
- `docs/superpowers/plans/2026-09-04-email-subject-field.md` — the full implementation plan for item 7, if you need to understand *why* something behaves a certain way (e.g. why Subject is versioned with Body, why old promotion files are rejected).
- `src/TemplateBuilder.Editor/README.md` — the package's own user-facing docs, including a `### v2.3.0` "What's New" section describing every one of these 7 items from a consumer's point of view.

## 8. Reporting issues

For each bug found, include: the exact route/URL, steps to reproduce, expected vs. actual behavior, and a screenshot if `agent-browser` can capture one. Reference the numbered scenario from Section 5 (e.g. "Item 7, scenario 9 — Duplicate") so it's easy to cross-reference against the implementation.
