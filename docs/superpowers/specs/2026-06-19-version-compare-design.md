# Version Compare — Design Spec
**Date:** 2026-06-19  
**Goal:** Add side-by-side version comparison to the History modal so users can visually compare the current editor content against any saved version before deciding whether to restore it.

---

## Problem

The History modal lists all saved versions with a Restore button on each. There is no way to preview what a version looked like before restoring it. Users must blindly restore a version and undo if it was wrong.

---

## Solution

Add a **Compare** button alongside each version's Restore button. Clicking Compare opens a full-width compare modal that renders both versions (current and selected) side-by-side as HTML previews, then lets the user restore directly from the compare view.

---

## UX Flow

1. User opens the History modal (existing **History** button in the properties panel).
2. Each non-current version card now shows two buttons: **Compare** (secondary) and **Restore** (primary).
3. Clicking **Compare** on an old version:
   - Closes the History modal.
   - Opens the Compare modal (full-width, ~95vw × 85vh).
4. The Compare modal renders two panels in parallel:
   - **Left — Current:** live editor content (`_editor.getContents()`), labeled with the current version number and a "Current" badge.
   - **Right — Selected:** the old version's body fetched from the API, labeled with its version number, date, and change comment.
5. Both panels use the existing `POST /Templates/{id}/Preview` endpoint for rendering, with auto-generated sample JSON (same three-pass regex as the Auto-fill feature).
6. User actions from the compare modal:
   - **Restore vN** (bottom of right panel) → calls the existing Restore endpoint → page reloads.
   - **← History** (header button) → closes the compare modal, reopens the History modal.
   - **Keep Current** (bottom of left panel) or **✕** → closes the compare modal, no action taken.

### Layout Wireframe

```
┌─────────────────────────────────────────────────────────────┐
│  Compare Versions            [← History]              [✕]   │
├───────────────────────────┬─────────────────────────────────┤
│  v4 · Current             │  v2 · 19 Jun 2026               │
│                           │  Initial version                │
│  ┌─────────────────────┐  │  ┌─────────────────────────┐   │
│  │                     │  │  │                         │   │
│  │  rendered preview   │  │  │  rendered preview       │   │
│  │  (sandboxed iframe) │  │  │  (sandboxed iframe)     │   │
│  │                     │  │  │                         │   │
│  └─────────────────────┘  │  └─────────────────────────┘   │
│  [Keep Current]           │  [Restore v2]                   │
└───────────────────────────┴─────────────────────────────────┘
```

---

## Architecture

### Files Changed

| File | Change |
|---|---|
| `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs` | Add `GetVersionBody` action |
| `src/TemplateBuilder.Editor/Views/Templates/_VersionHistory.cshtml` | Add Compare button; Restore styled as btn-primary |
| `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml` | Add compare modal HTML; register `compare-modal` in escape-key handler |
| `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` | Extract `_tbGenerateSampleFromHtml(html)`, add `openCompareView()`, refactor `_tbGenerateSampleFromTemplate()` |
| `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css` | Compare modal, panel, header, iframe, action-row styles |
| `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` | Bump `<Version>` from `1.4.3` to `1.4.4` |

No new files. No new dependencies. No database changes.

---

## New API Endpoint

```
GET /Templates/{id:int}/Versions/{versionId:int}/Body
Authorization: none (same as other editor endpoints — auth handled at app level)
Response 200: { "body": "<html string>" }
Response 404: { "code": "VERSION_NOT_FOUND", "message": "Version {versionId} not found." }
```

Uses the existing `_repository.GetVersionBodyAsync(versionId, ct)` — no new repository method needed.

Version metadata (number, date, change comment) is passed directly as inline arguments on the Compare button's `onclick` handler in `_VersionHistory.cshtml`, so the JS function has everything it needs without a second API call.

---

## JavaScript

### `_tbGenerateSampleFromHtml(html)` — new standalone helper

Extract the three-pass regex logic from `_tbGenerateSampleFromTemplate()` into a function that accepts an HTML string directly. Same algorithm, same output format.

```javascript
function _tbGenerateSampleFromHtml(html) {
    const obj = {};
    // Pass 1 — scalars
    const scalarPat = /\{\{-?\s*model\.(\w+)\s*-?\}\}/g;
    let m;
    while ((m = scalarPat.exec(html)) !== null)
        if (!(m[1] in obj)) obj[m[1]] = `Sample ${m[1]}`;
    // Pass 2 — loop declarations
    const loopPat = /\{\{-?\s*for\s+(\w+)\s+in\s+model\.(\w+)\s*-?\}\}/g;
    const aliasMap = {};
    while ((m = loopPat.exec(html)) !== null) aliasMap[m[1]] = m[2];
    // Pass 3 — item fields
    const itemPat = /\{\{-?\s*(\w+)\.(\w+)\s*-?\}\}/g;
    const colFields = {};
    while ((m = itemPat.exec(html)) !== null) {
        if (!(m[1] in aliasMap)) continue;
        const col = aliasMap[m[1]];
        (colFields[col] ??= new Set()).add(m[2]);
    }
    for (const [col, fields] of Object.entries(colFields)) {
        const row1 = {}, row2 = {};
        for (const f of fields) { row1[f] = `Sample ${f}`; row2[f] = `Sample ${f} 2`; }
        obj[col] = [row1, row2];
    }
    return Object.keys(obj).length ? JSON.stringify(obj, null, 2) : '{}';
}
```

### `_tbGenerateSampleFromTemplate()` — refactored (zero behavior change)

```javascript
function _tbGenerateSampleFromTemplate() {
    if (!_editor) return '{}';
    return _tbGenerateSampleFromHtml(_editor.getContents());
}
```

### `openCompareView(versionId, versionNumber, changeComment, createdAt)` — new function

Called from the Compare button's `onclick` in `_VersionHistory.cshtml`.

Steps:
1. Close `version-modal`.
2. Populate right-panel header with `versionNumber`, `createdAt`, `changeComment`.
3. Populate left-panel header with current version number (from `#version-display` text content).
4. Open `compare-modal`.
5. In parallel: render current body (left) and old body (right):
   - **Left:** `body = _editor.getContents()`, `json = _tbGenerateSampleFromHtml(body)`, POST to Preview → write HTML into `#compare-iframe-current`.
   - **Right:** `fetch GET /Templates/{id}/Versions/{versionId}/Body` → `body = data.body`, `json = _tbGenerateSampleFromHtml(body)`, POST to Preview → write HTML into `#compare-iframe-old`.
6. Wire the **Restore vN** button to call `restoreFromCompare(btn, versionId, versionNumber)`.
7. Wire **← History** to close compare-modal and reopen history modal.

Spinners are shown in each iframe's placeholder `<div>` until the Preview response arrives.

### `restoreFromCompare(btn, versionId, versionNumber)` — thin wrapper

Same flow as `restoreVersion()` but surfaces errors in `#compare-error` (inside the compare modal) instead of `#restore-error` (inside the history modal, which is closed during compare).

---

## HTML — Compare Modal (added to Edit.cshtml)

```html
<!-- Compare Modal -->
<div class="modal-overlay" id="compare-modal" role="dialog" aria-modal="true"
     aria-labelledby="compare-modal-title">
  <div class="modal modal--compare">
    <div class="modal-header">
      <span id="compare-modal-title" class="modal-title">Compare Versions</span>
      <button type="button" id="btn-compare-back-history" class="btn btn-sm btn-secondary">← History</button>
      <button class="modal-close" aria-label="Close">&#x2715;</button>
    </div>
    <div id="compare-error" class="tb-error-msg" role="alert" style="display:none;"></div>
    <div class="compare-panels">
      <div class="compare-panel" id="compare-panel-current">
        <div class="compare-panel-header">
          <span id="compare-current-num" class="tb-version-num"></span>
          <span class="tb-version-badge">Current</span>
          <span id="compare-current-meta" class="compare-panel-meta"></span>
        </div>
        <div class="compare-iframe-wrap">
          <div class="compare-loading" id="compare-loading-current">Loading…</div>
          <iframe id="compare-iframe-current" class="compare-iframe" sandbox="allow-same-origin"></iframe>
        </div>
        <div class="compare-panel-actions">
          <button type="button" id="btn-compare-keep" class="btn btn-sm btn-secondary">Keep Current</button>
        </div>
      </div>
      <div class="compare-panel" id="compare-panel-old">
        <div class="compare-panel-header">
          <span id="compare-old-num" class="tb-version-num"></span>
          <span id="compare-old-meta" class="compare-panel-meta"></span>
        </div>
        <div class="compare-iframe-wrap">
          <div class="compare-loading" id="compare-loading-old">Loading…</div>
          <iframe id="compare-iframe-old" class="compare-iframe" sandbox="allow-same-origin"></iframe>
        </div>
        <div class="compare-panel-actions">
          <button type="button" id="btn-compare-restore" class="btn btn-sm btn-primary">Restore</button>
        </div>
      </div>
    </div>
  </div>
</div>
```

---

## CSS — New Classes (added to template-editor.css)

```css
.modal--compare {
    width: 95vw;
    max-width: 1400px;
    height: 85vh;
    display: flex;
    flex-direction: column;
}

.compare-panels {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
    flex: 1;
    overflow: hidden;
    padding: 0 1rem 1rem;
}

.compare-panel {
    display: flex;
    flex-direction: column;
    border: 1px solid var(--border);
    border-radius: var(--radius);
    overflow: hidden;
    background: var(--surface);
}

.compare-panel-header {
    display: flex;
    align-items: center;
    gap: .5rem;
    padding: .5rem .75rem;
    background: var(--surface-2, #f8f9fa);
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
}

.compare-panel-meta {
    font-size: .8rem;
    color: var(--text-muted);
    flex: 1;
}

.compare-iframe-wrap {
    flex: 1;
    position: relative;
    overflow: hidden;
}

.compare-iframe {
    width: 100%;
    height: 100%;
    border: none;
    background: #fff;
}

.compare-loading {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--surface);
    font-size: .9rem;
    color: var(--text-muted);
}

.compare-panel-actions {
    padding: .5rem .75rem;
    border-top: 1px solid var(--border);
    display: flex;
    justify-content: flex-end;
    flex-shrink: 0;
}
```

---

## _VersionHistory.cshtml — Changes

Each non-current version card's button row changes from:
```cshtml
<button class="btn btn-sm btn-secondary" onclick="restoreVersion(...)">Restore</button>
```
to:
```cshtml
<button class="btn btn-sm btn-secondary"
        data-version-id="@v.Id"
        data-version-num="@v.VersionNumber"
        data-comment="@Html.Encode(v.ChangeComment ?? string.Empty)"
        data-created-at="@v.CreatedAt.ToString("dd MMM yyyy HH:mm")"
        onclick="openCompareView(this)">
    Compare
</button>
<button class="btn btn-sm btn-primary"
        onclick="restoreVersion(this, @v.Id, @v.VersionNumber)">
    Restore
</button>
```

`openCompareView(btn)` reads `btn.dataset.versionId`, `btn.dataset.versionNum`, `btn.dataset.comment`, and `btn.dataset.createdAt`. Using data attributes avoids inline JS-string encoding of arbitrary user-supplied comment text.

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| Body fetch returns 404 | Panel spinner replaced with `"Version not found."` error message |
| Body fetch network error | Panel shows `"Network error loading version."` |
| Preview render fails (400/408) | Panel shows `"Preview failed: {message}"` |
| Restore fails from compare modal | `#compare-error` banner appears at the top of the compare modal |

---

## Escape / Close Behaviour

`compare-modal` is added to the existing escape-key handler list in `template-editor.js`:
```javascript
['version-modal', 'preview-modal', 'loop-modal', 'conditional-modal', 'save-snippet-modal', 'compare-modal']
```

---

## Version

`TemplateBuilder.Editor.csproj` `<Version>` bumped from `1.4.3` to `1.4.4`.

---

## Success Criteria

- [ ] Non-current version cards in the History modal show both a Compare and a Restore button
- [ ] Clicking Compare closes the History modal and opens the Compare modal
- [ ] Left panel renders the live editor content; right panel renders the selected old version
- [ ] Both panels use auto-generated sample JSON (same three-pass regex) for rendering
- [ ] "← History" reopens the History modal without a page reload
- [ ] "Restore vN" button in the right panel calls the existing restore endpoint correctly
- [ ] "Keep Current" and ✕ close the compare modal with no side effects
- [ ] Network or render errors are surfaced per-panel, not as page-level failures
- [ ] `_tbGenerateSampleFromTemplate()` behaviour is unchanged (zero regression)
- [ ] Version is bumped to 1.4.4
