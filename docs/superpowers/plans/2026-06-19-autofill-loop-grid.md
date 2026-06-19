# Auto-fill Loop & Grid Sample Data — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `_tbGenerateSampleFromTemplate()` so clicking "⚡ Auto-fill from template" also generates sample array data for Loop and Grid block collections, not just top-level scalar fields.

**Architecture:** Single function replacement in `template-editor.js` (lines 468–479). Three regex passes over the editor HTML: (1) existing scalar pass, (2) new loop-declaration pass building an alias→collection map, (3) new item-field pass collecting per-collection fields. Output arrays of 2 sample items per collection. No new files, no new dependencies.

**Tech Stack:** Vanilla JavaScript, SunEditor 2.47.10, Scriban `{{ }}` syntax, Playwright MCP for verification.

## Global Constraints

- File to modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`
- Function to replace: `_tbGenerateSampleFromTemplate()` at lines 468–479 — replace the body only, keep the function name identical
- Sample item count: exactly **2** per collection
- Second item values append `" 2"` suffix: e.g. `"Sample ProductName 2"`
- Version bump: `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` `<Version>` → `1.4.3`
- No other files are modified
- App is running at `https://localhost:7044/` for Task 2 verification
- Demo templates exist at: `/Templates/3/Edit` (Order Summary), `/Templates/4/Edit` (Product Catalog), `/Templates/6/Edit` (Customer Invoice)

---

### Task 1: Replace `_tbGenerateSampleFromTemplate()` and bump version

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js:468-479`
- Modify: `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj:9`

**Interfaces:**
- Produces: `_tbGenerateSampleFromTemplate() → string` — same signature, new body. Called at line 1340 by the `#btn-gen-sample` click handler (no change needed there).

- [ ] **Step 1: Read the current function to confirm exact text**

  Read `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` lines 466–480. Confirm it looks like:

  ```javascript
  // ── Preview sample JSON helpers ───────────────────────────────────────────────

  function _tbGenerateSampleFromTemplate() {
      if (!_editor) return '{}';
      const html = _editor.getContents();
      const pattern = /\{\{-?\s*model\.(\w+)\s*-?\}\}/g;
      const fields = new Set();
      let m;
      while ((m = pattern.exec(html)) !== null) fields.add(m[1]);
      if (fields.size === 0) return '{}';
      const obj = {};
      for (const f of fields) obj[f] = `Sample ${f}`;
      return JSON.stringify(obj, null, 2);
  }
  ```

- [ ] **Step 2: Replace the function body**

  In `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`, replace the entire `_tbGenerateSampleFromTemplate` function (lines 468–479) with:

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

  > **Edit tip:** Use the Edit tool with `old_string` = the full 12-line existing function body (from `function _tbGenerateSampleFromTemplate()` through the closing `}`) and `new_string` = the new 27-line body above.

- [ ] **Step 3: Bump the version in the csproj**

  In `src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj` line 9, change:
  ```xml
  <Version>1.4.2</Version>
  ```
  to:
  ```xml
  <Version>1.4.3</Version>
  ```

- [ ] **Step 4: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git add src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj
  git commit -m "feat: extend auto-fill to generate sample arrays for Loop/Grid blocks, bump to v1.4.3"
  ```

---

### Task 2: Playwright verification against the three demo templates

**Files:** No file changes — browser verification only.

**Depends on:** Task 1 must be committed and the app reloaded (or the browser cache cleared) before running this task.

**Note on app reload:** The JS file is a static asset served by the RCL. After the code change, the browser needs to load the updated file. Either hard-refresh (`Ctrl+Shift+R`) the page, or add `?v=1` to the URL to bust the cache. Use `browser_evaluate` to confirm the new function is loaded:
```javascript
_tbGenerateSampleFromTemplate.toString().includes('loopPat')
// must return true
```

- [ ] **Step 1: Load Playwright MCP tools**

  Call ToolSearch with query:
  `"select:mcp__plugin_playwright_playwright__browser_navigate,mcp__plugin_playwright_playwright__browser_click,mcp__plugin_playwright_playwright__browser_evaluate,mcp__plugin_playwright_playwright__browser_snapshot,mcp__plugin_playwright_playwright__browser_wait_for"`

- [ ] **Step 2: Verify the new function is loaded in the browser**

  Call `browser_navigate` → `https://localhost:7044/Templates/3/Edit`

  Call `browser_evaluate`:
  ```javascript
  typeof _tbGenerateSampleFromTemplate !== 'undefined' &&
  _tbGenerateSampleFromTemplate.toString().includes('loopPat')
  ```
  Expected: `true`

  If `false`, the browser cached the old JS. Call `browser_evaluate`:
  ```javascript
  // Force reload with cache bust
  location.href = location.href + (location.href.includes('?') ? '&' : '?') + '_cb=' + Date.now();
  ```
  Then wait and retry.

- [ ] **Step 3: Verify Order Summary — Loop Block Demo (`/Templates/3/Edit`)**

  Open the Preview modal by clicking the Preview button. Take a `browser_snapshot` first to locate the Preview button if needed (look for a button with text "Preview" in the toolbar or right panel).

  Click the Preview button → modal opens.

  Click `#btn-gen-sample` ("⚡ Auto-fill from template").

  Read the textarea value:
  ```javascript
  document.getElementById('preview-json').value
  ```

  Parse and assert via `browser_evaluate`:
  ```javascript
  const json = JSON.parse(document.getElementById('preview-json').value);
  const checks = [
    typeof json.CustomerName === 'string',        // scalar preserved
    typeof json.OrderNumber === 'string',          // scalar preserved
    Array.isArray(json.Items),                     // Items is array
    json.Items.length === 2,                       // exactly 2 items
    typeof json.Items[0].ProductName === 'string', // item fields present
    typeof json.Items[0].Qty === 'string',
    typeof json.Items[0].UnitPrice === 'string',
    typeof json.Items[0].Total === 'string',
    json.Items[1].ProductName.endsWith(' 2'),      // second item has " 2" suffix
  ];
  checks.every(Boolean) ? 'PASS' : 'FAIL: ' + checks.map((c,i)=>`[${i}]=${c}`).join(' ');
  ```
  Expected: `'PASS'`

- [ ] **Step 4: Verify Product Catalog — Grid Block Demo (`/Templates/4/Edit`)**

  Call `browser_navigate` → `https://localhost:7044/Templates/4/Edit`

  Open Preview modal → click `#btn-gen-sample`.

  Assert via `browser_evaluate`:
  ```javascript
  const json = JSON.parse(document.getElementById('preview-json').value);
  const checks = [
    typeof json.CatalogTitle === 'string',
    typeof json.PublishedDate === 'string',
    Array.isArray(json.Items),
    json.Items.length === 2,
    typeof json.Items[0].SKU === 'string',
    typeof json.Items[0].ProductName === 'string',
    typeof json.Items[0].Category === 'string',
    typeof json.Items[0].Price === 'string',
    json.Items[1].SKU.endsWith(' 2'),
  ];
  checks.every(Boolean) ? 'PASS' : 'FAIL: ' + checks.map((c,i)=>`[${i}]=${c}`).join(' ');
  ```
  Expected: `'PASS'`

- [ ] **Step 5: Verify Customer Invoice — Snippets Demo (`/Templates/6/Edit`)**

  Call `browser_navigate` → `https://localhost:7044/Templates/6/Edit`

  Open Preview modal → click `#btn-gen-sample`.

  Assert via `browser_evaluate`:
  ```javascript
  const json = JSON.parse(document.getElementById('preview-json').value);
  const checks = [
    typeof json.InvoiceNumber === 'string',
    typeof json.CustomerName === 'string',
    typeof json.TotalDue === 'string',
    Array.isArray(json.Items),
    json.Items.length === 2,
    typeof json.Items[0].ProductName === 'string',
    typeof json.Items[0].Qty === 'string',
    typeof json.Items[0].UnitPrice === 'string',
    typeof json.Items[0].LineTotal === 'string',
    json.Items[1].LineTotal.endsWith(' 2'),
  ];
  checks.every(Boolean) ? 'PASS' : 'FAIL: ' + checks.map((c,i)=>`[${i}]=${c}`).join(' ');
  ```
  Expected: `'PASS'`

- [ ] **Step 6: Take final screenshot of Customer Invoice preview**

  With the preview modal still open from Step 5, click `#btn-render` to render the template with the auto-filled JSON.

  Take a `browser_snapshot` to confirm the rendered HTML shows the loop content (two rows of line items) in the iframe.

  Report the snapshot as evidence of end-to-end success.
