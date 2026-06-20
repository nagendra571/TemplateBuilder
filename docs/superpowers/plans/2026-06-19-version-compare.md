# Version Compare Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add side-by-side version comparison to the History modal so users can render and compare two versions of a template before deciding whether to restore an older one.

**Architecture:** Three-task pipeline — Task 1 adds the server-side `GetVersionBody` endpoint (C#, xUnit-tested). Task 2 wires the full client-side feature: compare modal HTML + CSS, Compare button in `_VersionHistory.cshtml`, and three new JS functions (`_tbGenerateSampleFromHtml`, `openCompareView`, `restoreFromCompare`). Task 3 verifies end-to-end with Playwright. No new files, no new dependencies, no DB changes.

**Tech Stack:** ASP.NET Core MVC, Razor Class Library, Vanilla JS (ES2021), CSS custom properties, xUnit + Moq + FluentAssertions, Playwright MCP.

## Global Constraints

- Version bump: `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` `<Version>` → `1.4.4`
- New endpoint route: `GET /Templates/{id:int}/Versions/{versionId:int}/Body` → `200 { body }` / `404 ErrorResult`
- ErrorResult shape: `new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found.")` — matches existing controller pattern
- JS function names (exact): `_tbGenerateSampleFromHtml(html)`, `openCompareView(btn)`, `_renderComparePanel(side, body, versionId)`, `restoreFromCompare(btn, versionId, sourceVersionNumber)`
- All CSS selectors must be scoped under `#tb-editor-host` — match every existing rule in `template-editor.css`
- Modal IDs: `compare-modal`, `compare-iframe-current`, `compare-iframe-old`, `compare-loading-current`, `compare-loading-old`, `compare-error`, `compare-current-num`, `compare-old-num`, `compare-old-meta`, `btn-compare-back-history`, `btn-compare-keep`, `btn-compare-restore`
- Preview fetch body: `JSON.stringify({ body, modelJson })` — matches existing `renderPreview()` pattern at line 643 of `template-editor.js`
- `_tbGenerateSampleFromTemplate()` behaviour unchanged — zero regression (it becomes a one-liner calling `_tbGenerateSampleFromHtml`)
- App runs at `https://localhost:7275/` for Task 3 verification

---

### Task 1: `GetVersionBody` endpoint + xUnit tests + version bump

**Files:**
- Modify: `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs` (add action after `GetVersionHistory`)
- Modify: `tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs` (add 2 tests)
- Modify: `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` line 9

**Interfaces:**
- Produces: `GET /Templates/{id:int}/Versions/{versionId:int}/Body` — consumed by `_renderComparePanel` in Task 2
- Response shape: `Ok(new { body })` where `body` is the HTML string from `GetVersionBodyAsync`

- [ ] **Step 1: Write two failing tests**

  Open `tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs`.
  Add these two tests inside the `TemplatesControllerTests` class, after the last existing test:

  ```csharp
  [Fact]
  public async Task GetVersionBody_ExistingVersion_ReturnsBodyJson()
  {
      var mockRepo = new Mock<ITemplateRepository>();
      mockRepo.Setup(r => r.GetVersionBodyAsync(42, It.IsAny<CancellationToken>()))
          .ReturnsAsync("<p>Hello {{ model.Name }}</p>");
      var controller = CreateController(mockRepo.Object);

      var result = await controller.GetVersionBody(1, 42);

      result.Should().BeOfType<OkObjectResult>();
      var ok = (OkObjectResult)result;
      ok.Value.Should().BeEquivalentTo(new { body = "<p>Hello {{ model.Name }}</p>" });
  }

  [Fact]
  public async Task GetVersionBody_NonExistentVersion_ReturnsNotFound()
  {
      var mockRepo = new Mock<ITemplateRepository>();
      mockRepo.Setup(r => r.GetVersionBodyAsync(99, It.IsAny<CancellationToken>()))
          .ReturnsAsync((string?)null);
      var controller = CreateController(mockRepo.Object);

      var result = await controller.GetVersionBody(1, 99);

      result.Should().BeOfType<NotFoundObjectResult>();
  }
  ```

- [ ] **Step 2: Run the tests to confirm they fail**

  ```
  dotnet test tests/TemplateBuilder.Editor.Tests/ --filter "GetVersionBody" -v
  ```

  Expected: 2 tests fail with `GetVersionBody` not found / not a method on the controller.

- [ ] **Step 3: Implement the action**

  Open `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs`.
  After the closing brace of `GetVersionHistory` (around line 152), add:

  ```csharp
  [HttpGet("Templates/{id:int}/Versions/{versionId:int}/Body")]
  public async Task<IActionResult> GetVersionBody(int id, int versionId, CancellationToken ct = default)
  {
      var body = await _repository.GetVersionBodyAsync(versionId, ct);
      if (body is null)
          return NotFound(new ErrorResult("VERSION_NOT_FOUND", $"Version {versionId} not found."));
      return Ok(new { body });
  }
  ```

- [ ] **Step 4: Run tests — confirm both pass**

  ```
  dotnet test tests/TemplateBuilder.Editor.Tests/ --filter "GetVersionBody" -v
  ```

  Expected: 2 tests pass. Output includes `Passed  GetVersionBody_ExistingVersion_ReturnsBodyJson` and `Passed  GetVersionBody_NonExistentVersion_ReturnsNotFound`.

- [ ] **Step 5: Run the full test suite — confirm no regressions**

  ```
  dotnet test tests/TemplateBuilder.Editor.Tests/ -v
  ```

  Expected: all tests pass (same count as before plus 2 new).

- [ ] **Step 6: Bump version to 1.4.4**

  In `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` line 9, change:
  ```xml
  <Version>1.4.3</Version>
  ```
  to:
  ```xml
  <Version>1.4.4</Version>
  ```

- [ ] **Step 7: Commit**

  ```
  git add src/TemplateBuilder.Editor/Controllers/TemplatesController.cs
  git add tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs
  git add src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj
  git commit -m "feat: add GetVersionBody endpoint and bump to v1.4.4"
  ```

---

### Task 2: Compare modal — CSS, HTML, partial view, JS

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css` (append compare styles)
- Modify: `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml` (add compare modal HTML; add `compare-modal` to escape handler)
- Modify: `src/TemplateBuilder.Editor/Views/Templates/_VersionHistory.cshtml` (add Compare button)
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` (extract helper, add 3 functions, add listeners)

**Interfaces:**
- Consumes: `GET /Templates/{id}/Versions/{versionId}/Body` from Task 1
- Consumes: `POST /Templates/{id}/Preview` (existing) — body `{ body, modelJson }`
- Consumes: `POST /Templates/{id}/Restore/{versionId}/{sourceVersionNumber}` (existing)
- Consumes: `openVersionHistory()` (existing, line ~562) — called by Back to History button
- Consumes: `closeModal(id)` (existing, line 784), `trapFocus(modal)` (existing, line 754)
- Consumes: `clearDraft()` (existing) — called after successful restore
- Consumes: `_csrf` module-level variable (existing) — antiforgery token
- Consumes: `templateId` module-level variable (existing) — current template's DB id
- Produces: `openCompareView(btn)` — called from `onclick` in `_VersionHistory.cshtml`

#### Step 1 — CSS

- [ ] **Step 1: Append compare modal styles to template-editor.css**

  Open `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css`.
  Scroll to the very end of the file. Append:

  ```css
  /* ── 16. Compare Modal ───────────────────────────────────────── */

  #tb-editor-host .modal--compare {
      width: 95vw;
      max-width: 1400px;
      height: 85vh;
  }

  #tb-editor-host .compare-panels {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
      flex: 1;
      overflow: hidden;
      padding: 0 1rem 1rem;
      min-height: 0;
  }

  #tb-editor-host .compare-panel {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--border);
      border-radius: var(--radius);
      overflow: hidden;
      min-height: 0;
  }

  #tb-editor-host .compare-panel-header {
      display: flex;
      align-items: center;
      gap: .5rem;
      padding: .5rem .75rem;
      background: var(--surface2);
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
      font-size: .85rem;
  }

  #tb-editor-host .compare-panel-meta {
      font-size: .8rem;
      color: var(--text-muted);
      flex: 1;
  }

  #tb-editor-host .compare-iframe-wrap {
      flex: 1;
      position: relative;
      overflow: hidden;
      min-height: 0;
  }

  #tb-editor-host .compare-iframe {
      width: 100%;
      height: 100%;
      border: none;
      display: block;
      background: #fff;
  }

  #tb-editor-host .compare-loading {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--surface);
      font-size: .9rem;
      color: var(--text-muted);
  }

  #tb-editor-host .compare-panel-actions {
      padding: .5rem .75rem;
      border-top: 1px solid var(--border);
      display: flex;
      justify-content: flex-end;
      flex-shrink: 0;
  }
  ```

#### Step 2 — Compare modal HTML in Edit.cshtml

- [ ] **Step 2: Add the compare modal div to Edit.cshtml**

  Open `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml`.
  Find the line:
  ```html
  <!-- Preview Modal -->
  ```
  Insert the following block immediately BEFORE that line (after the closing `</div>` of version-modal):

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
              <div class="compare-panel">
                  <div class="compare-panel-header">
                      <span id="compare-current-num" class="tb-version-num"></span>
                      <span class="tb-version-badge">Current</span>
                      <span class="compare-panel-meta"></span>
                  </div>
                  <div class="compare-iframe-wrap">
                      <div class="compare-loading" id="compare-loading-current">Loading…</div>
                      <iframe id="compare-iframe-current" class="compare-iframe"
                              sandbox="allow-same-origin" title="Current version preview"></iframe>
                  </div>
                  <div class="compare-panel-actions">
                      <button type="button" id="btn-compare-keep" class="btn btn-sm btn-secondary">Keep Current</button>
                  </div>
              </div>
              <div class="compare-panel">
                  <div class="compare-panel-header">
                      <span id="compare-old-num" class="tb-version-num"></span>
                      <span id="compare-old-meta" class="compare-panel-meta"></span>
                  </div>
                  <div class="compare-iframe-wrap">
                      <div class="compare-loading" id="compare-loading-old">Loading…</div>
                      <iframe id="compare-iframe-old" class="compare-iframe"
                              sandbox="allow-same-origin" title="Selected version preview"></iframe>
                  </div>
                  <div class="compare-panel-actions">
                      <button type="button" id="btn-compare-restore" class="btn btn-sm btn-primary">Restore</button>
                  </div>
              </div>
          </div>
      </div>
  </div>

  ```

#### Step 3 — Register compare-modal in the escape handler

- [ ] **Step 3: Add compare-modal to the escape-key handler**

  In `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`, find line 793 (the `document.addEventListener('keydown'...` handler):

  ```javascript
  ['version-modal', 'preview-modal', 'loop-modal', 'conditional-modal', 'save-snippet-modal'].forEach(id => {
  ```

  Replace with:

  ```javascript
  ['version-modal', 'preview-modal', 'loop-modal', 'conditional-modal', 'save-snippet-modal', 'compare-modal'].forEach(id => {
  ```

#### Step 4 — Update _VersionHistory.cshtml

- [ ] **Step 4: Add Compare button to each non-current version card**

  Open `src/TemplateBuilder.Editor/Views/Templates/_VersionHistory.cshtml`.
  Find the block:

  ```cshtml
                  @if (!isCurrent)
                  {
                      <button class="btn btn-sm btn-secondary" onclick="restoreVersion(this, @v.Id, @v.VersionNumber)">Restore</button>
                  }
  ```

  Replace with:

  ```cshtml
                  @if (!isCurrent)
                  {
                      <button class="btn btn-sm btn-secondary"
                              data-version-id="@v.Id"
                              data-version-num="@v.VersionNumber"
                              data-comment="@(v.ChangeComment ?? string.Empty)"
                              data-created-at="@v.CreatedAt.ToString("dd MMM yyyy HH:mm")"
                              onclick="openCompareView(this)">Compare</button>
                      <button class="btn btn-sm btn-primary" onclick="restoreVersion(this, @v.Id, @v.VersionNumber)">Restore</button>
                  }
  ```

  > **Note:** Razor automatically HTML-encodes `@(v.ChangeComment ?? string.Empty)` in an HTML attribute context. The JS reads it back with `btn.dataset.comment`, which gives the decoded string — no double-encoding issue.

#### Step 5 — JS: extract `_tbGenerateSampleFromHtml` and refactor `_tbGenerateSampleFromTemplate`

- [ ] **Step 5: Extract the three-pass regex into a standalone helper**

  In `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`, find the existing `_tbGenerateSampleFromTemplate` function. It currently looks like:

  ```javascript
  function _tbGenerateSampleFromTemplate() {
      if (!_editor) return '{}';
      const html = _editor.getContents();
      const obj = {};

      // Pass 1 — top-level scalars: {{ model.FieldName }}
      const scalarPat = /\{\{-?\s*model\.(\w+)\s*-?\}\}/g;
      let m;
      while ((m = scalarPat.exec(html)) !== null)
          if (!(m[1] in obj)) obj[m[1]] = `Sample ${m[1]}`;

      // Pass 2 — loop declarations: {{ for alias in model.Collection }}
      const loopPat = /\{\{-?\s*for\s+(\w+)\s+in\s+model\.(\w+)\s*-?\}\}/g;
      const aliasMap = {};
      while ((m = loopPat.exec(html)) !== null) aliasMap[m[1]] = m[2];

      // Pass 3 — item fields: {{ alias.FieldName }}
      const itemPat = /\{\{-?\s*(\w+)\.(\w+)\s*-?\}\}/g;
      const colFields = {};
      while ((m = itemPat.exec(html)) !== null) {
          if (!(m[1] in aliasMap)) continue;
          const col = aliasMap[m[1]];
          (colFields[col] ??= new Set()).add(m[2]);
      }

      // Build 2-item arrays for each collection (overwrites any same-named scalar)
      for (const [col, fields] of Object.entries(colFields)) {
          const row1 = {}, row2 = {};
          for (const f of fields) { row1[f] = `Sample ${f}`; row2[f] = `Sample ${f} 2`; }
          obj[col] = [row1, row2];
      }

      return Object.keys(obj).length ? JSON.stringify(obj, null, 2) : '{}';
  }
  ```

  Replace the entire function with:

  ```javascript
  function _tbGenerateSampleFromHtml(html) {
      const obj = {};

      // Pass 1 — top-level scalars: {{ model.FieldName }}
      const scalarPat = /\{\{-?\s*model\.(\w+)\s*-?\}\}/g;
      let m;
      while ((m = scalarPat.exec(html)) !== null)
          if (!(m[1] in obj)) obj[m[1]] = `Sample ${m[1]}`;

      // Pass 2 — loop declarations: {{ for alias in model.Collection }}
      const loopPat = /\{\{-?\s*for\s+(\w+)\s+in\s+model\.(\w+)\s*-?\}\}/g;
      const aliasMap = {};
      while ((m = loopPat.exec(html)) !== null) aliasMap[m[1]] = m[2];

      // Pass 3 — item fields: {{ alias.FieldName }}
      const itemPat = /\{\{-?\s*(\w+)\.(\w+)\s*-?\}\}/g;
      const colFields = {};
      while ((m = itemPat.exec(html)) !== null) {
          if (!(m[1] in aliasMap)) continue;
          const col = aliasMap[m[1]];
          (colFields[col] ??= new Set()).add(m[2]);
      }

      // Build 2-item arrays for each collection (overwrites any same-named scalar)
      for (const [col, fields] of Object.entries(colFields)) {
          const row1 = {}, row2 = {};
          for (const f of fields) { row1[f] = `Sample ${f}`; row2[f] = `Sample ${f} 2`; }
          obj[col] = [row1, row2];
      }

      return Object.keys(obj).length ? JSON.stringify(obj, null, 2) : '{}';
  }

  function _tbGenerateSampleFromTemplate() {
      if (!_editor) return '{}';
      return _tbGenerateSampleFromHtml(_editor.getContents());
  }
  ```

#### Step 6 — JS: add openCompareView, _renderComparePanel, restoreFromCompare

- [ ] **Step 6: Add three new functions after the existing `restoreVersion` function**

  In `template-editor.js`, find the closing brace of `restoreVersion` — the function ends with:

  ```javascript
      } catch {
          restoreErrorEl.textContent = 'Network error — please try again.';
          restoreErrorEl.style.display = 'block';
          btn.disabled = false;
      }
  }
  ```

  Immediately after that closing `}`, insert:

  ```javascript

  // ── Version Compare ───────────────────────────────────────────────────────────

  async function openCompareView(btn) {
      const versionId      = parseInt(btn.dataset.versionId, 10);
      const versionNum     = parseInt(btn.dataset.versionNum, 10);
      const comment        = btn.dataset.comment || '';
      const createdAt      = btn.dataset.createdAt || '';

      closeModal('version-modal');

      // Populate left (current) panel header
      document.getElementById('compare-current-num').textContent =
          document.getElementById('version-display')?.textContent?.trim() ?? 'Current';

      // Populate right (old) panel header
      document.getElementById('compare-old-num').textContent = `v${versionNum}`;
      document.getElementById('compare-old-meta').textContent =
          comment ? `${createdAt} · ${comment}` : createdAt;

      // Wire Restore button for this specific version
      const restoreBtn = document.getElementById('btn-compare-restore');
      restoreBtn.textContent = `Restore v${versionNum}`;
      restoreBtn.disabled = false;
      restoreBtn.onclick = () => restoreFromCompare(restoreBtn, versionId, versionNum);

      // Reset panels to loading state
      ['current', 'old'].forEach(side => {
          const loading = document.getElementById(`compare-loading-${side}`);
          loading.textContent = 'Loading…';
          loading.style.display = 'flex';
          document.getElementById(`compare-iframe-${side}`).srcdoc = '';
      });
      document.getElementById('compare-error').style.display = 'none';

      const modal = document.getElementById('compare-modal');
      modal.classList.add('open');
      trapFocus(modal);

      const currentBody = _editor ? _editor.getContents() : '';
      await Promise.all([
          _renderComparePanel('current', currentBody, null),
          _renderComparePanel('old', null, versionId)
      ]);
  }

  async function _renderComparePanel(side, body, versionId) {
      const loadingEl = document.getElementById(`compare-loading-${side}`);
      const iframeEl  = document.getElementById(`compare-iframe-${side}`);
      try {
          if (body === null) {
              const res = await fetch(`/Templates/${templateId}/Versions/${versionId}/Body`);
              if (!res.ok) {
                  loadingEl.textContent = 'Failed to load version.';
                  return;
              }
              body = (await res.json()).body;
          }
          const modelJson = _tbGenerateSampleFromHtml(body);
          const previewRes = await fetch(`/Templates/${templateId}/Preview`, {
              method: 'POST',
              headers: {
                  'Content-Type': 'application/json',
                  'RequestVerificationToken': _csrf
              },
              body: JSON.stringify({ body, modelJson })
          });
          if (!previewRes.ok) {
              const err = await previewRes.json().catch(() => null);
              loadingEl.textContent = `Preview failed: ${err?.message ?? previewRes.status}`;
              return;
          }
          iframeEl.srcdoc = (await previewRes.json()).html;
          loadingEl.style.display = 'none';
      } catch {
          loadingEl.textContent = 'Network error loading preview.';
      }
  }

  async function restoreFromCompare(btn, versionId, sourceVersionNumber) {
      const errEl = document.getElementById('compare-error');
      errEl.style.display = 'none';
      btn.disabled = true;
      try {
          const res = await fetch(`/Templates/${templateId}/Restore/${versionId}/${sourceVersionNumber}`, {
              method: 'POST',
              headers: { 'RequestVerificationToken': _csrf }
          });
          if (res.ok) {
              clearDraft();
              window.location.reload();
          } else {
              const err = await res.json().catch(() => null);
              errEl.textContent = err?.message ?? 'Failed to restore version.';
              errEl.style.display = 'block';
              btn.disabled = false;
          }
      } catch {
          errEl.textContent = 'Network error — please try again.';
          errEl.style.display = 'block';
          btn.disabled = false;
      }
  }
  ```

#### Step 7 — JS: wire Back-to-History and Keep-Current buttons

- [ ] **Step 7: Add event listeners for the two compare modal buttons**

  In `template-editor.js`, find the `.modal-close` generic handler block (around line 1369):

  ```javascript
  document.querySelectorAll('.modal-close').forEach(btn => {
      btn.addEventListener('click', () => {
          const overlay = btn.closest('.modal-overlay');
          if (overlay) closeModal(overlay.id);
      });
  });
  ```

  Immediately AFTER that block's closing `});`, add:

  ```javascript
  document.getElementById('btn-compare-back-history')?.addEventListener('click', () => {
      closeModal('compare-modal');
      openVersionHistory();
  });
  document.getElementById('btn-compare-keep')?.addEventListener('click', () => closeModal('compare-modal'));
  ```

#### Step 8 — Build and commit

- [ ] **Step 8: Build the project to confirm no compile errors**

  ```
  dotnet build src/TemplateBuilder.Editor/
  ```

  Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 9: Commit all client-side changes**

  ```
  git add src/TemplateBuilder.Editor/wwwroot/css/template-editor.css
  git add src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml
  git add src/TemplateBuilder.Editor/Views/Templates/_VersionHistory.cshtml
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git commit -m "feat: add side-by-side version compare modal"
  ```

---

### Task 3: Playwright end-to-end verification

**Files:** No file changes — browser verification only.

**Depends on:** Tasks 1 and 2 committed + app rebuilt and running at `https://localhost:7275/`.

**Note:** The JS is embedded in a Razor Class Library. After the Task 2 commit, the app must be rebuilt (`dotnet build`) and restarted before the new JS is served. Confirm the new code is live with this cache-bust check before any other step:

```javascript
typeof openCompareView === 'function' && typeof _tbGenerateSampleFromHtml === 'function'
// must return true
```

- [ ] **Step 1: Load Playwright MCP tools**

  Call ToolSearch:
  ```
  select:mcp__plugin_playwright_playwright__browser_navigate,mcp__plugin_playwright_playwright__browser_click,mcp__plugin_playwright_playwright__browser_evaluate,mcp__plugin_playwright_playwright__browser_snapshot,mcp__plugin_playwright_playwright__browser_wait_for
  ```

- [ ] **Step 2: Navigate to the Customer Invoice template (has multiple versions)**

  ```
  browser_navigate → https://localhost:7275/Templates/6/Edit
  ```

  Call `browser_evaluate`:
  ```javascript
  typeof openCompareView === 'function' && typeof _tbGenerateSampleFromHtml === 'function'
  ```
  Expected: `true`. If `false`, the app needs to be rebuilt and restarted.

- [ ] **Step 3: Verify Compare button appears in the History modal**

  Click the History button:
  ```
  browser_click → #btn-history
  ```

  Take a snapshot: `browser_snapshot → target: #version-modal`

  Assert both buttons exist on each non-current card:
  ```javascript
  const cards = document.querySelectorAll('.tb-version-card:not(.is-current)');
  const allHaveCompare = [...cards].every(c => c.querySelector('button[onclick^="openCompareView"]'));
  const allHaveRestore = [...cards].every(c => c.querySelector('button[onclick^="restoreVersion"]'));
  allHaveCompare && allHaveRestore ? 'PASS' : `FAIL: compare=${allHaveCompare} restore=${allHaveRestore}`;
  ```
  Expected: `'PASS'`

- [ ] **Step 4: Open the compare modal by clicking Compare on the oldest version**

  Click the Compare button on the last (oldest) version card:
  ```javascript
  // Click the first Compare button found (lowest numbered version in the list)
  document.querySelectorAll('[onclick^="openCompareView"]')[document.querySelectorAll('[onclick^="openCompareView"]').length - 1].click();
  ```

  Wait briefly for the modal to open and iframes to load:
  ```
  browser_wait_for → selector: #compare-modal.open
  ```

  Take a snapshot: `browser_snapshot → target: #compare-modal`

- [ ] **Step 5: Assert compare modal structure**

  ```javascript
  const modal = document.getElementById('compare-modal');
  const checks = [
      modal.classList.contains('open'),                                    // modal is open
      document.getElementById('compare-current-num').textContent.length > 0, // current version label set
      document.getElementById('compare-old-num').textContent.startsWith('v'), // old version label set
      document.getElementById('btn-compare-restore').textContent.includes('Restore v'), // restore btn labeled
      document.getElementById('btn-compare-back-history') !== null,        // back button exists
      document.getElementById('btn-compare-keep') !== null,                // keep button exists
  ];
  checks.every(Boolean) ? 'PASS' : 'FAIL: ' + checks.map((c,i)=>`[${i}]=${c}`).join(' ');
  ```
  Expected: `'PASS'`

- [ ] **Step 6: Wait for both panels to finish rendering**

  Poll until both loading spinners are hidden:
  ```javascript
  const bothLoaded =
      document.getElementById('compare-loading-current').style.display === 'none' &&
      document.getElementById('compare-loading-old').style.display === 'none';
  bothLoaded ? 'LOADED' : 'STILL_LOADING';
  ```
  Re-run a few times with `browser_wait_for` or a short `browser_evaluate` loop until it returns `'LOADED'`.

- [ ] **Step 7: Assert both iframes have rendered HTML content**

  ```javascript
  const currentDoc = document.getElementById('compare-iframe-current').contentDocument;
  const oldDoc = document.getElementById('compare-iframe-old').contentDocument;
  const currentHasContent = (currentDoc?.body?.innerHTML?.length ?? 0) > 10;
  const oldHasContent = (oldDoc?.body?.innerHTML?.length ?? 0) > 10;
  currentHasContent && oldHasContent ? 'PASS' : `FAIL: current=${currentHasContent} old=${oldHasContent}`;
  ```
  Expected: `'PASS'`

- [ ] **Step 8: Test Back to History**

  ```
  browser_click → #btn-compare-back-history
  ```

  Assert history modal reopens and compare modal closes:
  ```javascript
  const historyOpen = document.getElementById('version-modal').classList.contains('open');
  const compareClose = !document.getElementById('compare-modal').classList.contains('open');
  historyOpen && compareClose ? 'PASS' : `FAIL: history=${historyOpen} compareClose=${compareClose}`;
  ```
  Expected: `'PASS'`

- [ ] **Step 9: Test Keep Current button**

  Open compare modal again (click Compare on any non-current version), then:
  ```
  browser_click → #btn-compare-keep
  ```

  Assert compare modal closes and no page reload occurred:
  ```javascript
  !document.getElementById('compare-modal').classList.contains('open') ? 'PASS' : 'FAIL';
  ```
  Expected: `'PASS'`

- [ ] **Step 10: Take final snapshot**

  Reopen the compare modal one more time. Take:
  ```
  browser_snapshot → target: #compare-modal
  ```

  Record the snapshot as evidence. Report the two version labels shown (e.g. "v4 Current | v1 Initial version") and confirm both panels have content.

- [ ] **Step 11: Write verification report**

  Write results to `C:/Users/nchinnam/source/repos/TemplateBuilder/.git/sdd/task-3-compare-verify.md` containing:
  - Status: DONE / BLOCKED
  - Cache-bust check result
  - Per-assertion results from Steps 3–9
  - Final snapshot description (Step 10)
