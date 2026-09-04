# QA Report: MVC5-fork Backport + Email Subject Field

**Date:** 2026-09-04
**Tester:** Claude (agent-browser-driven browser QA)
**Scope:** `docs/qa/2026-09-04-subject-field-and-backport-handoff.md` — items 1–7, priority on item 7 (Email Subject field)
**Environment:** `dotnet run --project src/TemplateBuilder.Web` on `http://localhost:5299`, SQL Server `localhost\SQLEXPRESS`, DB `TemplateBuilderClient`. Browser automation via `agent-browser` (Chrome 152, both headless and headed modes tested).

## Summary

All 13 Item-7 (Subject field) scenarios and items 1–6 were exercised in a real browser. **One Critical bug** was found and root-caused in the Create-template flow (not Subject-specific, but it blocks the create workflow for every template type). Two Low/Medium cosmetic findings were also found. Every scenario the hand-off doc asked me to verify **passed** except where noted below — including the two "real bugs fixed this session" (Duplicate dropping Subject, Autosave draft dropping Subject), both of which are now confirmed fixed.

| Severity | Count |
|---|---|
| Critical | 1 |
| Medium | 1 |
| Low | 2 |

---

## Findings

### [QA-1 / Critical] "Create Template" triggers a native "leave site?" dialog and freezes the tab after every successful save

**Title:** `createTemplate()` never clears the dirty flag before its post-save redirect, so the page's own `beforeunload` guard blocks its own navigation.

**Steps to reproduce:**
1. Go to `/Templates/Create`.
2. Fill in a Template Name (or edit Subject, or Type, or insert anything into the body — any of these mark the page dirty).
3. Click **Create Template**.

**Expected:** The template saves and the browser navigates straight to `/Templates/{id}/Edit`.

**Actual:** The template *does* save correctly server-side (confirmed via EF Core command logs — `Templates`/`TemplateVersions`/`AuditLogs` inserts all complete in milliseconds). But the client-side redirect (`window.location.href = ...`) immediately triggers the page's `beforeunload` handler, which — because `_isDirty` is still `true` — calls `e.preventDefault()`, producing the browser's native "Leave site? Changes you made may not be saved" confirmation. Under `agent-browser` automation (both headless and headed Chrome), this reliably made the tab **completely unresponsive** to any further command (`get url`, `get title`, `screenshot`, `dialog status`, `eval` — all failed with raw connection timeouts) until the tab was force-closed. Reproduced **3/3** times: once with an Email template with a Subject, once with a plain Report template with no Subject, and once more in headed mode — confirming it is a general Create-flow bug, not specific to the Subject feature.

**Root cause (verified in source):** `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`, `createTemplate()`:
```js
if (res.ok) {
    const data = await res.json();
    window.location.href = `/Templates/${data.templateId}/Edit`;   // <-- missing markClean() before this
}
```
Compare with the sibling `saveVersion()` (same file), which correctly calls `clearDraft(); markClean(); showToast('Version saved');` on success. `createTemplate()` has no equivalent `markClean()` call, so `_isDirty` (set by typing into any Create-form field — see the `beforeunload` handler at line 31) is still `true` at the moment of redirect, and the `beforeunload` listener (line 31–36) fires its `preventDefault()`/`returnValue` guard against the app's own successful, code-driven navigation.

**Impact:** Every real user who fills out any part of the Create form (which is unavoidable — even just typing a name) and clicks Create will see a jarring, confusing native "Leave this page?" browser prompt immediately after a successful save, and must click through it to reach the Edit page. This is not cosmetic — it looks like an error/crash to an end user, and it broke browser automation for the rest of this QA pass whenever the Create button was used (worked around by using direct URL navigation to the new template's Edit page for the remaining Item-7 scenarios instead of clicking through Create each time).

**Suggested fix:** Add `markClean();` immediately before (or as part of) the `window.location.href = ...` line in `createTemplate()`, mirroring `saveVersion()`.

**Note on scope:** Not caused by the Subject-field work — reproduces identically on a bare Report template with no Subject involved. Pre-existing bug surfaced by this QA pass.

---

### [QA-2 / Medium] Inserted field tokens are never wrapped in the `tb-field` chip span — plain text only

**Title:** Both the new field-palette "Insert" button and the pre-existing toolbar "Insert Field" dropdown fail to produce the styled `tb-field` chip in the actual editor DOM; tokens land as plain unstyled text.

**Steps to reproduce:**
1. On any template's Edit page with a SQL view selected, click into the WYSIWYG body.
2. Click a field palette "Insert" button (or use the toolbar's "Insert Field" dropdown).

**Expected (per hand-off doc, item 7 scenario 2):** Token inserted "wrapped as a styled `tb-field` chip (existing behavior, should be unaffected)".

**Actual:** The token appears as plain text, e.g. `<p>{{ model.Age }}&nbsp;<br></p>` — no `<span class="tb-field">` wrapper, even though `template-editor.js` explicitly constructs `<span class="tb-field" contenteditable="false">{{ model.${field} }}</span>&nbsp;` before calling `_editor.insertHTML(...)` in both the palette-click handler (line ~659) and the legacy `insertFieldToken()` (line ~1642). SunEditor appears to strip the custom span/attribute on insert. Confirmed this is **pre-existing** (not a regression from the Subject-field work) by checking an older, previously-saved template's body (`Customer Invoice — Snippets Demo`, id 6) — its stored field tokens are also plain text with no chip wrapper.

**Impact:** Cosmetic/UX only — the token still functions correctly as a Scriban placeholder and renders correctly in Preview — but users lose the intended visual distinction between literal text and dynamic fields in the editor.

**Scope note:** Matches the hand-off doc's "existing behavior, should be unaffected" — it is indeed unaffected by the Subject work (broken the same way before and after), so this does not block item 7 sign-off, but is worth a follow-up ticket since it affects the wider editor UX, not just Subject.

---

### [QA-3 / Low] Activity timeline: "Imported" action uses the amber ("restored") chip color instead of the indigo "everything else" bucket

**Steps to reproduce:** Import a template (see scenario 13), then open its Activity drawer.

**Expected (item 5 spec):** amber is reserved for `restored`/`toggled_active`/`duplicated`; anything else (including, implicitly, `imported`) should be indigo/purple.

**Actual:** The "Imported" chip renders with the identical amber colors (`rgb(217,119,6)` text on `rgb(255,251,235)` background) as "Restored", rather than the indigo used for "Created" (`rgb(99,102,241)` on `rgb(238,242,255)`). Verified via computed styles, not just visual inspection.

**Impact:** Cosmetic only, low severity — a color-taxonomy inconsistency versus the written spec, doesn't affect functionality.

---

### [QA-4 / Low, informational] Create-page's "Source SQL View" selection is silently dropped on save

**Steps to reproduce:** On `/Templates/Create`, select a Source SQL View, fill in required fields, click Create Template.

**Expected:** The new template retains the selected source view.

**Actual:** `createTemplate()`'s POST body (`{ name, templateType, description, subject, body }`) does not include `sourceView`, unlike `saveVersion()` which does. After creation, the new template's "Source SQL View" reverts to "— None —", and the field palette must be manually reselected on the Edit page.

**Impact:** Minor usability gap — not in the original 7-item scope, flagged as an incidental observation from this pass. Low priority.

---

## Verified passing (no issues found)

### Item 6 — DBA-managed databases
App booted cleanly against `localhost\SQLEXPRESS`; EF Core applied/verified migrations with no errors (`No migrations were applied. The database is already up to date.`).

### Item 1 — SunEditor canvas width
Confirmed via screenshot: toolbar and canvas span the full CANVAS panel width, no gap before PROPERTIES, no unnecessary toolbar wrapping.

### Items 2 & 3 — Activity drawer sanity
Opened/closed the drawer (including a fresh page load and multiple toggles); no page-scroll jump (`window.scrollY` stayed `0`), tab never got visually stuck after repeated open/close.

### Item 4 — Code-view textarea width
Confirmed via screenshot: Code View textarea fills the full canvas width, matching the WYSIWYG view.

### Item 5 — Refined activity timeline
- Circular avatar with actor initials present (`A` for `anonymous`).
- Action chips colored correctly for Published (green) and Restored (amber) — see QA-3 for the one exception (Imported).
- Actor name + relative timestamp ("6m ago" etc.) present.
- Comment shown in a distinct quoted block ("Restored from v1").
- `/Audit` page: chart card and filters card measured at **exactly** 218.1875px height each (`getBoundingClientRect()`), confirming equal-height layout.

### Item 7 — Email Subject field (all 13 scenarios)
1. **Type-toggle show/hide** — Subject row hides on switch to Report (`display:none`), reappears on switch back to Email with text intact. **PASS**
2. **Field-palette insertion targeting** — Insert with focus in Subject places the token at the cursor in Subject; insert with focus in Body places it in Body. Targeting logic is correct (see QA-2 for the separate chip-styling issue, pre-existing and unrelated to targeting). **PASS**
3. **Used-field indicator** — Inserting a field into Subject only correctly marks that field `palette-field--used` with the ✓ checkmark, confirming Subject is included in the used-field scan. **PASS**
4. **Save and reload** — Subject text (`Order for {{ model.ContactName }} on {{ model.SignupDate }}`) persisted correctly across a full page reload. **PASS**
5. **Preview** — "Subject: Order for Jane Doe on 2026-09-04T00:00:00-04:00" rendered correctly above the preview iframe; plain text, properly escaped. **PASS**
6. **Auto-generate sample data** — Fields referenced only in Subject (`ContactName`, `SignupDate`) received auto-generated sample values even though absent from Body. **PASS**
7. **Compare** — Current version showed "Subject: Version 2 subject — updated"; the older v1 side showed its own stored Subject with its own sample data. Each side independently correct. **PASS**
8. **Restore** — Restoring v1 correctly updated the live Subject input to v1's Subject value. **PASS**
9. **Duplicate (previously-fixed bug)** — Duplicating a template with a Subject produced a copy whose Subject exactly matched the source. Confirmed fixed. **PASS**
10. **Non-Email templates keep Subject data** — A Report-type template that briefly held an Email Subject retained that Subject value (verified via the raw input value) across type switches and a reload, even while the field stays hidden. **PASS**
11. **Dirty tracking** — Source inspection confirms `prop-subject` has both `input` and `change` listeners wired to `markDirty()` identically to Body's `onChange`; behaviorally confirmed (editing Subject alone sets the dirty flag, as evidenced by the `beforeunload` guard firing — see QA-1). **PASS**
12. **Auto-save draft (previously-fixed bug)** — With autosave enabled, editing only the Subject field and triggering a draft save correctly captured the Subject in the `localStorage` draft payload. Reloading showed the "Draft available" banner, and clicking Restore correctly brought back the Subject-only edit. Confirmed fixed. **PASS**
13. **Template Promotion export/import** —
    - Export produced `"schemaVersion": 3` with a `"subject"` field on every version object, correct values. **PASS**
    - Re-importing the same file round-tripped correctly, updating the existing template (by `externalKey`) rather than duplicating it, with the right Subject. **PASS**
    - Importing a hand-edited `"schemaVersion": 2` copy of the same file was correctly **rejected** with the error `"QA Subject Test — Unsupported schemaVersion 2; expected 3."` — not silently imported or upgraded. **PASS**

### Additional security check (not explicitly requested, done as part of general QA hygiene)
Injected `<script>...</script><img src=x onerror=...>` into the Subject field and rendered Preview. The payload was correctly HTML-escaped (`&lt;script&gt;...`) in the rendered "Subject:" line; the injected JS did not execute. Also noted from server logs that all DB writes use parameterized EF Core commands (no string-concatenated SQL observed) — no SQL-injection surface found in the Subject path.

---

## Test artifacts created during this pass
Templates created in the dev database for testing (safe to leave or clean up):
- `QA Subject Test` (id 14) — Email, exercised scenarios 1–13
- `QA Baseline Report` (id 15) — Report, used for the Create-flow regression isolation and scenario 10
- `QA Headed Test` (id 16) — Email, used to confirm QA-1 reproduces in headed Chrome too
- `Copy of QA Subject Test` (id 17) — Duplicate-scenario output

## Recommendation
Fix QA-1 before shipping — it's a one-line fix (`markClean()` before the Create redirect) with an outsized user-facing impact. QA-2/3/4 can be follow-up tickets; they don't block this release's Subject-field feature, which is otherwise solid — all 13 scenarios, including both previously-fixed bugs, verified working correctly.
