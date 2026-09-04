# Fix Report: QA findings from the Subject-field + backport pass

**Companion to:** `docs/qa/2026-09-04-subject-field-and-backport-qa-report.md` (the QA report itself)
**Repo:** `TemplateBuilder` (this repo — ASP.NET Core / EF Core), package `TemplateBuilder.Editor`
**Commits:** `088dcac`, `82b77a5`, `44b0466`, `c80938d`, `cdfb847` (5 commits, all on `main`)

## Purpose of this document

This is the engineering record of how each QA finding was root-caused and fixed in **this** repo. It's written to be handed, together with the QA report, to whoever fixes the equivalent bugs in the sibling **`TemplateBuilder.Editor.Mvc5`** fork (`.NET Framework 4.8 / EF6 / MVC5`, separate repo at `C:\Users\nchinnam\source\repos\TemplateBuilder.Mvc5`).

**Read this before touching the fork:** the two repos are explicitly a **standalone fork, not a shared codebase** (per that repo's own `CLAUDE.md`) — `Domain`/`Application` were duplicated verbatim at fork time and have since diverged independently; the Editor/controller/JS layers were written separately from scratch and have diverged further. Do not assume a bug here implies the identical bug there, or that a fix here can be copy-pasted there unchanged. **I already checked the fork's actual current code for each of these four findings** (see the "Fork status" box under each one) — two of the four don't need any fork changes at all. Verify live in the fork before applying anything, the same way each fix below was verified live in this repo before being called done.

---

## QA-1 (Critical) — Create Template triggered a native "leave site?" dialog and froze the tab

### Root cause

`createTemplate()` in `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` redirected to the new template's Edit page on success (`window.location.href = ...`) without first clearing the dirty flag. The app has a global `beforeunload` guard:

```javascript
window.addEventListener('beforeunload', (e) => {
    if (_isDirty) {
        e.preventDefault();
        e.returnValue = '';
    }
});
```

Any interaction on the Create form (typing a name, editing Subject, inserting a field) sets `_isDirty = true` via `markDirty()`. Since `createTemplate()` never called `markClean()`, the flag was still `true` at the moment of the code-driven redirect, so the browser's own `beforeunload` guard fired against the app's own successful navigation — producing a native "Leave this page?" confirmation the user never asked for, and (under browser automation) a tab that stopped responding to any further command.

Confirmed by direct comparison with `saveVersion()` in the same file, which already did this correctly:
```javascript
clearDraft();
markClean();
showToast('Version saved');
```

### Fix

One-line addition — `markClean()` before the redirect, mirroring `saveVersion()`:

```diff
         if (res.ok) {
             const data = await res.json();
+            markClean();
             window.location.href = `/Templates/${data.templateId}/Edit`;
```

### Verification

Live, via `agent-browser`: filled the Create form, clicked Create, and confirmed (a) no dialog appeared, (b) the redirect completed to `/Templates/{id}/Edit`, and (c) the tab remained fully responsive (`get title`/`get url` both returned immediately) — the exact repro QA used to detect the freeze.

### Fork status: ✅ already correct, no action needed

Checked `TemplateBuilder.Mvc5/src/TemplateBuilder.Editor.Mvc5/StaticAssets/template-editor.js`. Its `createTemplate()` (line ~748) **already** calls `markClean()` before the redirect, with its own explanatory comment:
```javascript
// The create succeeded and the body it sent is already saved server-side — clear
// the dirty flag before navigating so the browser doesn't show a spurious "leaving
// site, unsaved changes" prompt for a save that already happened.
markClean();
window.location.href = `/Templates/${data.templateId}/Edit`;
```
This bug does not reproduce in the fork. Nothing to port.

---

## QA-2 (Medium) — Field-token insertion never wrapped the token in its `tb-field` chip span

### Root cause

Every field-insertion code path builds the intended markup as a string:
```javascript
`<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(field)} }}</span>&nbsp;`
```
and passes it to `_editor.insertHTML(html)` — SunEditor's programmatic insert API. Despite `span`/`class`/`contenteditable` all being present in the editor's configured `attributesWhitelist`/`addTagsWhitelist`, the span never survived — insertion happened, but as plain text (`{{ model.EmailAddress }}` with no wrapper), reproducing identically on live insertion and in previously-saved template bodies.

**Why:** `_editor.insertHTML(html)` was called with only one argument. SunEditor's actual signature is `insertHTML(html, notCleaningData, checkCharCount, rangeSelection)` — `notCleaningData` defaults to `false`, meaning SunEditor runs its own internal HTML cleaner on inserted content. That cleaner is a *different* code path from the configured `attributesWhitelist` (which governs paste-sanitization), and it stripped the whole span. Confirmed live, in-browser, via direct `eval`:

```js
// WITHOUT notCleaningData (the bug):
_editor.insertHTML('<span class="tb-field" contenteditable="false">TEST</span>&nbsp;');
// → innerHTML shows: TEST  (span gone entirely)

// WITH notCleaningData=true (the fix):
_editor.insertHTML('<span class="tb-field" contenteditable="false">TEST</span>&nbsp;', true);
// → innerHTML shows: <span class="tb-field" contenteditable="false">TEST</span>&nbsp;
```

This was a **pre-existing** bug, unrelated to and predating the Subject-field work (QA confirmed it on an older, previously-saved template's stored body too).

### Fix

Added `true` as the second argument at every call site in this repo that constructs a `tb-field` span — **three** total (QA's pass only exercised the first two; the third, drag-and-drop, was found while researching this fork hand-off and fixed the same way):

1. Field-palette "Insert" button click handler (~line 659)
2. Legacy toolbar "Insert Field" dropdown, `insertFieldToken()` (~line 1642)
3. Drag-and-drop of a field onto the canvas, the `editorArea`'s `'drop'` handler (~line 317)

Same one-line change at each: append `, true` to the `insertHTML(...)` call. Each is commented explaining why it's safe to skip the cleaner (the inserted string is entirely our own construction — an escaped field name in a fixed template shape — never raw user or pasted HTML; contrast with the *other* `insertHTML` calls in this file for pasted Word content, snippet bodies, etc., which are deliberately left cleaned since that content isn't ours).

### Verification

Live, via `agent-browser`, for all three call sites: selected a SQL view, inserted a field via the palette button, the toolbar dropdown, — checked `document.querySelector('.sun-editor-editable').innerHTML` after each and confirmed the `<span class="tb-field" contenteditable="false">...</span>` wrapper was present and intact. (Drag-and-drop specifically wasn't re-verified live — same code path, same proven argument, `node --check` clean — but flag this if you want it re-confirmed by an actual drag interaction.)

### Fork status: ⚠️ reproduces — needs the same fix at 3 call sites

Checked `TemplateBuilder.Mvc5/src/TemplateBuilder.Editor.Mvc5/StaticAssets/template-editor.js`. Same `attributesWhitelist`/`addTagsWhitelist` config, same single-argument `insertHTML(html)` pattern, at three matching call sites:

| Call site | Fork location |
|---|---|
| Drag-and-drop `'drop'` handler | line ~368-370 |
| Field-palette "Insert" button handler | line ~682-684 |
| `insertFieldToken()` (toolbar dropdown) | line ~1723-1725 |

All three currently look like:
```javascript
_editor.insertHTML(
    `<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(field)} }}</span>&nbsp;`
);
```
and need `, true` added as the second argument — identical fix to this repo's. **Before applying:** confirm the fork's vendored `StaticAssets/suneditor.min.js` actually exhibits the same stripping behavior first (it's a local vendored copy, not necessarily the same version as this repo's CDN-loaded `suneditor@2.47.10` — verify with the same live `eval` test shown above against the fork's own running app before assuming the root cause transfers unchanged). If confirmed, this is the exact same one-line fix at all three sites.

**Also worth checking in the fork** (not verified either way in this repo or the fork — flagging as a related open question, not a confirmed bug): are `tb-loop`/`tb-conditional`/other custom-attribute `insertHTML` calls in this same file subject to the same or a different stripping behavior? Not investigated — the four confirmed `tb-field` sites were the only ones root-caused.

---

## QA-3 (Low) — "Imported" activity chip rendered amber instead of the documented indigo

### Root cause

**Not a code bug.** `actionKind()` in `template-editor.js` has classified `'imported'` into the `warning` (amber) bucket since the Audit feature's *original* implementation:
```bash
$ git log --oneline -S"function actionKind" -- src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
3061979 feat: audit page UI — filters, chart, stat chips, diffs, pagination, live poll
```
That commit already contained `['restored', 'duplicated', 'toggled_active', 'imported'].includes(action)) return 'warning'` — this predates and is entirely untouched by this session's activity-timeline redesign (the port that added avatars/chips). The actual, established, intentional app behavior is correct and was never in question.

The real defect was in **this session's own README changelog bullet**, which summarized the color scheme as "restore/toggle/duplicate = amber, everything else = indigo" — an inaccurate simplification that didn't account for `imported` already being in the amber bucket. QA read that README line as the spec, tested against it, and correctly found a mismatch — between the doc and the (correct, unchanged) code.

### Fix

Corrected the README wording, not the classification logic:
```diff
-  restore/toggle/duplicate = amber, everything else = indigo), replacing the previous
-  plain-text action label and status dot.
+  restored/toggled/duplicated/imported = amber, everything else = indigo), replacing the
+  previous plain-text action label and status dot.
```

### Verification

`git log -S` confirmed the classification's origin and age, as shown above. No code path was touched, so no live re-verification was needed beyond re-reading the corrected sentence against the actual `actionKind()` source.

### Fork status: N/A — nothing to port

Checked `TemplateBuilder.Mvc5/src/TemplateBuilder.Editor.Mvc5/StaticAssets/template-editor.js`. Its `actionKind()` has a **different** action vocabulary and does not even reference `'imported'`:
```javascript
function actionKind(action) {
    if (action === 'published' || action === 'approved') return 'success';
    if (action === 'rejected' || action === 'snippet_deleted') return 'danger';
    if (['restored', 'snippet_restored', 'toggled_active', 'duplicated', 'review_cancelled'].includes(action)) return 'warning';
    return 'info';
}
```
The fork *does* have an `Imported` audit action (`TemplateBuilder.Domain.Entities.AuditActions.Imported = "imported"`, used by its Template Promotion import flow), but since `actionKind()` never lists it explicitly, it falls through to the `'info'` (indigo) default — which is exactly what this repo's README *intended* to describe. There is nothing to fix here; if anything, the fork's actual behavior is the one that matches the originally-intended color scheme. No changes needed.

---

## QA-4 (Low) — Create Template silently dropped the selected Source SQL View

### Root cause

Server-side, not client-side. `CreateTemplateJson` in `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs` builds the new `Template` entity from only three fields:
```csharp
var template = await _repository.CreateAsync(new Template
{
    Name = model.Name.Trim(),
    TemplateType = model.TemplateType,
    Description = model.Description
}, ct);
```
`TemplateEditorViewModel.SourceView` exists and is populated from the Create form, but this action never reads it — unlike `SaveVersion`, which does:
```csharp
template.SourceView = string.IsNullOrWhiteSpace(request.SourceView) ? null : request.SourceView.Trim();
if (!string.Equals(previousSourceView, template.SourceView, StringComparison.OrdinalIgnoreCase))
    template.SourceViewSnapshot = template.SourceView is null ? null : await _health.BuildSnapshotJsonAsync(template.SourceView, ct);
```
The JS payload for `createTemplate()` also never sent `sourceView` at all (unlike `saveVersion()`'s payload) — a secondary gap, but the primary root cause is the controller silently ignoring the field even if it had been sent.

### Fix

Two changes, mirroring `SaveVersion`'s existing pattern (no "previous value" comparison needed here since it's always a brand-new template — if a view is provided, always build its snapshot):

**Controller** (`TemplatesController.cs`):
```diff
+            var sourceView = string.IsNullOrWhiteSpace(model.SourceView) ? null : model.SourceView.Trim();
             var template = await _repository.CreateAsync(new Template
             {
                 Name = model.Name.Trim(),
                 TemplateType = model.TemplateType,
-                Description = model.Description
+                Description = model.Description,
+                SourceView = sourceView,
+                SourceViewSnapshot = sourceView is null ? null : await _health.BuildSnapshotJsonAsync(sourceView, ct)
             }, ct);
```

**JS payload** (`createTemplate()`):
```diff
                 subject: document.getElementById('prop-subject')?.value ?? null,
+                sourceView: document.getElementById('prop-source-view')?.value || null,
                 body
```

Added two controller tests (`CreateTemplateJson_WithSourceView_SetsSourceViewAndBuildsSnapshot`, `CreateTemplateJson_NoSourceView_DoesNotBuildSnapshot`) mirroring the existing `SaveVersion_SourceViewChanged_RebuildsSnapshot` test's mocking pattern.

### Verification

`dotnet build`/`dotnet test` (both new tests pass, full suite green — **note:** this required rebuilding and restarting the local `dotnet run` process, since it was still serving the pre-fix DLL; a C# controller change needs a server restart to take effect, unlike the JS fixes above which are static assets picked up on browser refresh alone). Then live via `agent-browser`: selected a Source SQL View on Create, submitted, and confirmed the Edit page's "Source SQL View" dropdown showed the selected view (not "— None —") without any manual reselection.

### Fork status: ⚠️ reproduces — needs the identical fix

Checked `TemplateBuilder.Mvc5/src/TemplateBuilder.Editor.Mvc5/Controllers/TemplatesController.cs`. Its `CreateTemplateJson` (line ~70-100) has the **exact same gap**:
```csharp
var template = await _repository.CreateAsync(new Template
{
    Name = model.Name.Trim(),
    TemplateType = model.TemplateType,
    Description = model.Description
});
```
No `SourceView`/`SourceViewSnapshot` assignment. Its `SaveVersion`-equivalent action (line ~144-147) has the identical reference pattern to mirror:
```csharp
var previousSourceView = template.SourceView;
template.SourceView = string.IsNullOrWhiteSpace(request.SourceView) ? null : request.SourceView.Trim();
...
template.SourceViewSnapshot = template.SourceView is null ? null : await _health.BuildSnapshotJsonAsync(template.SourceView);
```
(Note: the fork's `BuildSnapshotJsonAsync` doesn't take a `CancellationToken` parameter — its `_health` service has a different signature than this repo's. Adjust accordingly, don't copy the `ct` argument.)

Also checked the fork's `createTemplate()` JS payload (`StaticAssets/template-editor.js`, line ~735) — it sends `name`/`templateType`/`description`/`subject`/`body` but not `sourceView`, same gap as this repo had. Both sides need the same two-part fix as above.

---

## Summary for the fork agent

| Finding | Fork action needed |
|---|---|
| QA-1 | **None** — already fixed there |
| QA-2 | **Fix** — add `, true` to `insertHTML()` at 3 confirmed call sites (line numbers above); verify the root cause transfers before assuming it (vendored SunEditor copy, not CDN-loaded) |
| QA-3 | **None** — doesn't apply; fork's classification already does the "right" thing by omission |
| QA-4 | **Fix** — same two-part change (controller field mapping + JS payload field), exact locations above |

Follow the same discipline used here: confirm each bug actually reproduces live in the fork's running app before patching (per that repo's own `CLAUDE.md`, and general good practice) — don't apply any of the above as a blind port. The line numbers and code snippets above are what I found when I read the fork's current source directly during this write-up; re-verify locally since I did not run its app live from this session.
