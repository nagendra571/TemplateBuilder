# Quick Win Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 5 additive toolbar features to TemplateBuilder.Editor — line height, special characters picker, print button, custom list styles, and anchor/bookmark links.

**Architecture:** All features are self-contained SunEditor toolbar plugins. Plugin objects are defined before `SUNEDITOR.create`, registered in the `plugins` array and `buttonList`, and wired up via IIFEs after `SUNEDITOR.create`. No existing functions are modified except appending to the toolbar config.

**Tech Stack:** SunEditor 2.47.10, vanilla JS, ASP.NET Core Razor (.cshtml), CSS custom properties scoped to `#tb-editor-host`.

---

## File Map

| File | Role |
|---|---|
| `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` | All plugin definitions, toolbar config, IIFEs, event wiring |
| `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css` | Styles for special chars panel, list style menu, anchor chip |
| `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml` | Special chars panel HTML (added after the Find & Replace panel) |

---

## Task 1: Line Height

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`

- [ ] **Step 1: Add `lineHeights` config to `SUNEDITOR.create`**

  In `template-editor.js`, find the `fontSize` array (line ~169). Add `lineHeights` immediately after it:

  ```js
  fontSize: [10, 12, 14, 16, 18, 20, 24, 28, 32, 36],
  lineHeights: [
      { text: '1.0',  value: '1'    },
      { text: '1.15', value: '1.15' },
      { text: '1.5',  value: '1.5'  },
      { text: '2.0',  value: '2'    },
      { text: '2.5',  value: '2.5'  },
      { text: '3.0',  value: '3'    },
  ],
  ```

- [ ] **Step 2: Add `'lineHeight'` to the font toolbar group**

  Find the `buttonList` entry `['formatBlock', 'font', 'fontSize']` (line ~156). Change it to:

  ```js
  ['formatBlock', 'font', 'fontSize', 'lineHeight'],
  ```

- [ ] **Step 3: Build and verify**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Manual verification**

  Run the web app. Open a template for editing. Confirm a "Line Height" dropdown appears in the toolbar (between fontSize and fontColor). Select text, change line height to 2.0, confirm the paragraph spacing increases.

- [ ] **Step 5: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git commit -m "feat: add line height control to editor toolbar"
  ```

---

## Task 2: Print Button

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`

- [ ] **Step 1: Define the `printPlugin` object**

  In `template-editor.js`, find the `saveSnippetPlugin` object (line ~128). Add `printPlugin` immediately after it, before the `_editor = SUNEDITOR.create(...)` call:

  ```js
  const printPlugin = {
      name: 'print',
      display: 'command',
      title: 'Print',
      innerHTML: '<span style="font-size:.82rem">🖨</span>',
      add: function(core) {},
      action: function() { window.print(); }
  };
  ```

- [ ] **Step 2: Register in `SUNEDITOR.create` plugins array**

  Find the `plugins: [...]` array inside `SUNEDITOR.create`. Add `printPlugin` at the end:

  ```js
  plugins: [
      blockquotePlugin,
      pageBreakPlugin,
      hrThin, hrThick, hrSpaced,
      insertFieldPlugin,
      insertLoopPlugin,
      insertConditionalPlugin,
      validatePlugin,
      findReplacePlugin,
      saveSnippetPlugin,
      printPlugin,
  ],
  ```

- [ ] **Step 3: Add `'print'` to the utility toolbar group**

  Find `['validate', 'findReplace', 'saveSnippet']` in `buttonList`. Change it to:

  ```js
  ['validate', 'findReplace', 'saveSnippet', 'print'],
  ```

- [ ] **Step 4: Build and verify**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Manual verification**

  Run the web app. Open a template for editing. Confirm a 🖨 button appears in the toolbar. Click it — browser print dialog should open immediately.

- [ ] **Step 6: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git commit -m "feat: add print button to editor toolbar"
  ```

---

## Task 3: Custom List Styles

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`
- Modify: `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css`

- [ ] **Step 1: Define the `listStylePlugin` object**

  In `template-editor.js`, add `listStylePlugin` after `printPlugin`:

  ```js
  const listStylePlugin = {
      name: 'listStyle',
      display: 'command',
      title: 'List Style ▾',
      innerHTML: '<span style="font-size:.7rem;font-weight:600">≡▾</span>',
      add: function(core) {},
      action: function() { window._toggleListStyleMenu?.(); }
  };
  ```

- [ ] **Step 2: Register `listStylePlugin` in the plugins array**

  ```js
  plugins: [
      // ... existing plugins ...
      printPlugin,
      listStylePlugin,
  ],
  ```

- [ ] **Step 3: Add `'listStyle'` to the list toolbar group**

  Find `['list', 'hrThin', 'hrThick', 'hrSpaced']` in `buttonList`. Change it to:

  ```js
  ['list', 'listStyle', 'hrThin', 'hrThick', 'hrSpaced'],
  ```

- [ ] **Step 4: Add the `wireListStyleMenu` IIFE**

  In `template-editor.js`, add this IIFE after the `wireTableToolbar` IIFE (after line ~1100):

  ```js
  // ── List style dropdown ───────────────────────────────────────────────────────

  (function wireListStyleMenu() {
      const menu = document.createElement('div');
      menu.id = 'tb-list-style-menu';
      menu.hidden = true;
      menu.innerHTML = `
          <button type="button" data-ls="disc">● Disc</button>
          <button type="button" data-ls="circle">○ Circle</button>
          <button type="button" data-ls="square">▪ Square</button>
          <div class="tb-ls-sep"></div>
          <button type="button" data-ls="decimal">1. Decimal</button>
          <button type="button" data-ls="upper-alpha">A. Upper Alpha</button>
          <button type="button" data-ls="lower-roman">i. Lower Roman</button>`;
      document.body.appendChild(menu);

      function openMenu() {
          const btn = document.querySelector('[data-command="listStyle"]') ??
                      document.querySelector('[title="List Style ▾"]');
          if (btn) {
              const r = btn.getBoundingClientRect();
              menu.style.top  = (r.bottom + window.scrollY + 4) + 'px';
              menu.style.left = (r.left  + window.scrollX) + 'px';
          }
          menu.hidden = false;
      }

      function closeMenu() { menu.hidden = true; }

      menu.addEventListener('click', e => {
          const btn = e.target.closest('[data-ls]');
          if (!btn) return;
          const sel = window.getSelection();
          const anchor = sel?.anchorNode;
          const list = anchor
              ? (anchor.nodeType === 3 ? anchor.parentElement : anchor)?.closest('ul, ol')
              : null;
          closeMenu();
          if (!list) { showToast('Place cursor inside a list first'); return; }
          list.style.listStyleType = btn.dataset.ls;
          markDirty();
      });

      document.addEventListener('mousedown', e => {
          if (!menu.hidden && !menu.contains(e.target)) closeMenu();
      });

      window._toggleListStyleMenu = () => menu.hidden ? openMenu() : closeMenu();
  })();
  ```

- [ ] **Step 5: Add CSS for the list style menu**

  In `template-editor.css`, append at the end of the file:

  ```css
  /* ── List style dropdown ────────────────────────────────────────────────────── */
  #tb-list-style-menu {
      position: absolute;
      z-index: 9999;
      background: var(--surface2);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: .25rem;
      min-width: 130px;
      box-shadow: 0 4px 12px rgba(0,0,0,.4);
  }
  #tb-list-style-menu button {
      display: block;
      width: 100%;
      text-align: left;
      background: none;
      border: none;
      color: var(--text);
      padding: .3rem .6rem;
      font-size: .82rem;
      border-radius: 4px;
      cursor: pointer;
  }
  #tb-list-style-menu button:hover { background: var(--accent); color: white; }
  .tb-ls-sep { border-top: 1px solid var(--border); margin: .25rem 0; }
  ```

- [ ] **Step 6: Build and verify**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Manual verification**

  Run the web app. Open a template for editing. Insert a bulleted list. Click the "≡▾" List Style button — a dropdown should appear with 6 options. Select "A. Upper Alpha" — the list should change to lettered style. Click outside to close without selecting — dropdown should dismiss.

- [ ] **Step 8: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git add src/TemplateBuilder.Editor/wwwroot/css/template-editor.css
  git commit -m "feat: add custom list styles dropdown to editor toolbar"
  ```

---

## Task 4: Anchor / Bookmark Links

**Files:**
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`
- Modify: `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css`

- [ ] **Step 1: Define the `anchorPlugin` object**

  In `template-editor.js`, add `anchorPlugin` after `listStylePlugin`:

  ```js
  const anchorPlugin = {
      name: 'anchor',
      display: 'command',
      title: 'Insert Anchor',
      innerHTML: '<span style="font-size:.82rem">⚓</span>',
      add: function(core) {},
      action: function() {
          if (!_editor) return;
          const raw = prompt('Anchor name (will be used as #name in links):');
          if (!raw?.trim()) return;
          const name = raw.trim().toLowerCase()
              .replace(/\s+/g, '-')
              .replace(/[^a-z0-9-]/g, '');
          if (!name) { showToast('Invalid anchor name — use letters, numbers, and hyphens only'); return; }
          _editor.insertHTML(
              `<a name="${name}" class="tb-anchor" contenteditable="false" title="#${name}">⚓ ${name}</a>&nbsp;`
          );
          markDirty();
      }
  };
  ```

- [ ] **Step 2: Register `anchorPlugin` in the plugins array**

  ```js
  plugins: [
      // ... existing plugins ...
      listStylePlugin,
      anchorPlugin,
  ],
  ```

- [ ] **Step 3: Add `'anchor'` to the link toolbar group**

  Find `['link', 'table', 'image']` in `buttonList`. Change it to:

  ```js
  ['link', 'table', 'image', 'anchor'],
  ```

- [ ] **Step 4: Add CSS for the anchor chip**

  In `template-editor.css`, append at the end of the file:

  ```css
  /* ── Anchor chip ────────────────────────────────────────────────────────────── */
  .tb-anchor {
      display: inline-block;
      background: #1e3a5f;
      color: #60a5fa;
      border: 1px solid #3b82f6;
      border-radius: 4px;
      padding: 0 6px;
      font-size: .75rem;
      font-family: monospace;
      cursor: default;
      user-select: none;
  }
  ```

- [ ] **Step 5: Build and verify**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Manual verification**

  Run the web app. Open a template. Click the ⚓ button — a browser prompt should appear. Enter "intro". Confirm a blue `⚓ intro` chip appears at the cursor. Hover over it — tooltip should show `#intro`. Use the existing link button, type `#intro` as the URL — the link should insert correctly. Enter blank name in the prompt — nothing should happen.

- [ ] **Step 7: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git add src/TemplateBuilder.Editor/wwwroot/css/template-editor.css
  git commit -m "feat: add anchor/bookmark link insertion to editor toolbar"
  ```

---

## Task 5: Special Characters Picker

**Files:**
- Modify: `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml`
- Modify: `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js`
- Modify: `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css`

- [ ] **Step 1: Add the panel HTML to `Edit.cshtml`**

  Find the Find & Replace panel block (ending with `</div>` before `/tb-editor-host`). Add the special chars panel immediately after it, before `</div>@* /tb-editor-host *@`:

  ```html
  <!-- Special Characters floating panel (non-blocking — no overlay) -->
  <div id="special-chars-panel" class="tb-special-chars" hidden role="dialog" aria-label="Special Characters">
      <div class="tb-sc-header">
          <span class="tb-sc-title">Special Characters</span>
          <button type="button" id="btn-sc-close" class="tb-sc-close" aria-label="Close">&#x2715;</button>
      </div>
      <div class="tb-sc-search-row">
          <input type="search" id="sc-search" class="tb-sc-search" placeholder="Search…" autocomplete="off">
      </div>
      <div id="sc-groups" class="tb-sc-groups"></div>
  </div>
  ```

- [ ] **Step 2: Define the `specialCharsPlugin` object**

  In `template-editor.js`, add `specialCharsPlugin` after `anchorPlugin`:

  ```js
  const specialCharsPlugin = {
      name: 'specialChars',
      display: 'command',
      title: 'Special Characters',
      innerHTML: '<span style="font-size:.88rem;font-weight:600">Ω</span>',
      add: function(core) {},
      action: function() { window._openSpecialChars?.(); }
  };
  ```

- [ ] **Step 3: Register `specialCharsPlugin` in the plugins array**

  ```js
  plugins: [
      // ... existing plugins ...
      anchorPlugin,
      specialCharsPlugin,
  ],
  ```

- [ ] **Step 4: Add `'specialChars'` to the utility toolbar group**

  Find `['validate', 'findReplace', 'saveSnippet', 'print']` in `buttonList`. Change it to:

  ```js
  ['validate', 'findReplace', 'saveSnippet', 'specialChars', 'print'],
  ```

- [ ] **Step 5: Add the `wireSpecialChars` IIFE**

  In `template-editor.js`, add after the `wireListStyleMenu` IIFE:

  ```js
  // ── Special characters picker ─────────────────────────────────────────────────

  (function wireSpecialChars() {
      const SC_GROUPS = [
          { label: 'Currency',   chars: ['$','£','€','¥','₹','₩','¢'] },
          { label: 'Legal',      chars: ['©','®','™','§','¶','°'] },
          { label: 'Math',       chars: ['±','×','÷','≠','≤','≥','∞','√','∑','≈'] },
          { label: 'Arrows',     chars: ['→','←','↑','↓','↔','⇒','⇐','↗','↘'] },
          { label: 'Typography', chars: ['…','—','–','“','”','‘','’','·','•','†','‡','½','¼','¾'] },
      ];

      const SC_NAMES = {
          '$':'Dollar','£':'Pound','€':'Euro','¥':'Yen','₹':'Rupee','₩':'Won','¢':'Cent',
          '©':'Copyright','®':'Registered','™':'Trademark','§':'Section','¶':'Paragraph','°':'Degree',
          '±':'Plus Minus','×':'Multiply','÷':'Divide','≠':'Not Equal','≤':'Less Equal','≥':'Greater Equal',
          '∞':'Infinity','√':'Square Root','∑':'Sigma','≈':'Approximately',
          '→':'Right Arrow','←':'Left Arrow','↑':'Up Arrow','↓':'Down Arrow','↔':'Left Right Arrow',
          '⇒':'Double Right Arrow','⇐':'Double Left Arrow','↗':'Up Right Arrow','↘':'Down Right Arrow',
          '…':'Ellipsis','—':'Em Dash','–':'En Dash','“':'Left Double Quote','”':'Right Double Quote',
          '‘':'Left Single Quote','’':'Right Single Quote','·':'Middle Dot','•':'Bullet',
          '†':'Dagger','‡':'Double Dagger','½':'One Half','¼':'One Quarter','¾':'Three Quarters',
      };

      const panel     = document.getElementById('special-chars-panel');
      const searchEl  = document.getElementById('sc-search');
      const groupsEl  = document.getElementById('sc-groups');
      if (!panel) return;

      function renderGroups(query) {
          const q = query.trim().toLowerCase();
          groupsEl.innerHTML = SC_GROUPS.map(g => {
              const visible = q
                  ? g.chars.filter(c => (SC_NAMES[c] || '').toLowerCase().includes(q) || c === q)
                  : g.chars;
              if (!visible.length) return '';
              return `<div class="tb-sc-group">
                  <div class="tb-sc-group-label">${g.label}</div>
                  <div class="tb-sc-chars">${visible.map(c =>
                      `<button type="button" class="tb-sc-char" title="${escapeHtml(SC_NAMES[c] || c)}" data-char="${escapeHtml(c)}">${c}</button>`
                  ).join('')}</div>
              </div>`;
          }).join('');
      }

      function openPanel() {
          renderGroups('');
          searchEl.value = '';
          panel.hidden = false;
          searchEl.focus();
      }

      function closePanel() {
          panel.hidden = true;
          document.querySelector('.sun-editor-editable')?.focus();
      }

      searchEl.addEventListener('input', e => renderGroups(e.target.value));

      groupsEl.addEventListener('click', e => {
          const btn = e.target.closest('.tb-sc-char');
          if (!btn || !_editor) return;
          _editor.insertText(btn.dataset.char);
          markDirty();
          closePanel();
      });

      document.getElementById('btn-sc-close')?.addEventListener('click', closePanel);

      window._openSpecialChars   = openPanel;
      window._closeSpecialChars  = closePanel;
      window._isSpecialCharsOpen = () => !panel.hidden;
  })();
  ```

- [ ] **Step 6: Add Escape key handling for the special chars panel**

  Find the existing `document.addEventListener('keydown', ...)` Escape handler (the one that handles modals and find-replace, around line ~690). Add one line inside it:

  ```js
  document.addEventListener('keydown', e => {
      if (e.key !== 'Escape') return;
      ['version-modal', 'preview-modal', 'loop-modal', 'conditional-modal', 'save-snippet-modal'].forEach(id => {
          const el = document.getElementById(id);
          if (el?.classList.contains('open')) closeModal(id);
      });
      const fd = document.getElementById('tb-field-dropdown');
      if (fd && !fd.hidden) fd.hidden = true;
      if (window._isFindReplaceOpen?.()) window._closeFindReplace?.();
      if (window._isSpecialCharsOpen?.()) window._closeSpecialChars?.();  // add this line
  });
  ```

- [ ] **Step 7: Add CSS for the special chars panel**

  In `template-editor.css`, append at the end of the file:

  ```css
  /* ── Special characters panel ───────────────────────────────────────────────── */
  .tb-special-chars {
      position: fixed;
      z-index: 9999;
      background: var(--surface2);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      box-shadow: 0 4px 16px rgba(0,0,0,.45);
      width: 280px;
      top: 80px;
      right: 1.5rem;
      display: flex;
      flex-direction: column;
      max-height: 380px;
  }
  .tb-sc-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: .5rem .75rem;
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
  }
  .tb-sc-title { font-size: .82rem; font-weight: 600; color: var(--text); }
  .tb-sc-close {
      background: none;
      border: none;
      color: var(--text-muted);
      cursor: pointer;
      font-size: .9rem;
      padding: 0 .25rem;
  }
  .tb-sc-close:hover { color: var(--text); }
  .tb-sc-search-row {
      padding: .5rem .75rem;
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
  }
  .tb-sc-search {
      width: 100%;
      box-sizing: border-box;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 4px;
      color: var(--text);
      padding: .3rem .5rem;
      font-size: .82rem;
  }
  .tb-sc-search:focus { outline: none; border-color: var(--accent); }
  .tb-sc-groups {
      overflow-y: auto;
      padding: .5rem .75rem;
      flex: 1;
  }
  .tb-sc-group { margin-bottom: .5rem; }
  .tb-sc-group-label {
      font-size: .7rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .06em;
      color: var(--text-muted);
      margin-bottom: .25rem;
  }
  .tb-sc-chars { display: flex; flex-wrap: wrap; gap: 2px; }
  .tb-sc-char {
      width: 28px;
      height: 28px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 4px;
      color: var(--text);
      font-size: .95rem;
      cursor: pointer;
      transition: background .1s, border-color .1s;
  }
  .tb-sc-char:hover { background: var(--accent); border-color: var(--accent); color: white; }
  ```

- [ ] **Step 8: Build and verify**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  ```
  Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Manual verification**

  Run the web app. Open a template. Click the Ω toolbar button — a floating panel should appear at the top-right. Confirm 5 groups are visible (Currency, Legal, Math, Arrows, Typography). Type "arrow" in the search box — only arrow characters should remain. Click → — it should be inserted at the cursor and the panel should close. Press Ctrl+H then close — re-open Ω, press Escape — panel should close and focus should return to editor.

- [ ] **Step 10: Commit**

  ```bash
  git add src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml
  git add src/TemplateBuilder.Editor/wwwroot/js/template-editor.js
  git add src/TemplateBuilder.Editor/wwwroot/css/template-editor.css
  git commit -m "feat: add special characters picker to editor toolbar"
  ```

---

## Final Step: Version bump and NuGet pack

- [ ] **Step 1: Bump version to 1.3.2 in `TemplateBuilder.Editor.csproj`**

  ```xml
  <Version>1.3.2</Version>
  ```

- [ ] **Step 2: Update README.md**

  Change `**Current version: 1.3.1**` → `**Current version: 1.3.2**`

  Change the install command version: `--version 1.3.2`

  Add a new changelog entry above the v1.3.1 entry:

  ```markdown
  ### v1.3.2
  - **Line Height** — line height dropdown (1.0–3.0) in the font toolbar group.
  - **Special Characters** — floating Ω picker with 5 groups (~80 chars) and live search.
  - **Print** — 🖨 toolbar button triggers browser print dialog.
  - **Custom List Styles** — "≡▾" dropdown applies disc/circle/square/decimal/upper-alpha/lower-roman to the active list.
  - **Anchor Links** — ⚓ toolbar button inserts a named anchor chip; link to it via `#name` in the standard link dialog.
  ```

- [ ] **Step 3: Build and pack**

  ```powershell
  dotnet build src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release
  dotnet pack src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj -c Release --no-build -o src/TemplateBuilder.Editor/bin/Release
  ```
  Expected: `Successfully created package '...TemplateBuilder.Editor.1.3.2.nupkg'`

- [ ] **Step 4: Commit and push**

  ```bash
  git add src/TemplateBuilder.Editor/TemplateBuilder.Editor.csproj src/TemplateBuilder.Editor/README.md
  git commit -m "chore: bump TemplateBuilder.Editor to v1.3.2"
  git push origin main
  ```
