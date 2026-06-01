# Quick Win Features — Design Spec

**Date:** 2026-06-01
**Status:** Approved
**Scope:** 5 additive toolbar features for TemplateBuilder.Editor

---

## Context

This spec covers 5 "quick win" features identified during the TinyMCE feature gap analysis. All features are additive — no existing behaviour changes. All changes are confined to three files: `template-editor.js`, `template-editor.css`, and `Edit.cshtml`.

---

## Feature 1 — Line Height

**Effort:** ~2 lines JS

SunEditor has native `lineHeights` support. This feature requires only adding the config option and toolbar entry.

**Config addition to `SUNEDITOR.create`:**
```js
lineHeights: [
  { text: '1.0', value: '1' },
  { text: '1.15', value: '1.15' },
  { text: '1.5', value: '1.5' },
  { text: '2.0', value: '2' },
  { text: '2.5', value: '2.5' },
  { text: '3.0', value: '3' },
],
```

**Toolbar placement:** Add `'lineHeight'` to the font row group alongside `'formatBlock'`, `'font'`, `'fontSize'`.

**Files changed:** `template-editor.js` only.

---

## Feature 2 — Special Characters Picker

**Effort:** ~70 lines JS, ~20 lines CSS, ~30 lines Razor HTML

A custom SunEditor toolbar plugin (`specialCharsPlugin`) opens a non-blocking floating panel. The pattern matches the existing Find & Replace panel (no overlay, positioned fixed, `hidden` attribute toggle).

### Character Set (~80 chars, 5 groups)

| Group | Characters |
|---|---|
| Currency | $ £ € ¥ ₹ ₩ ¢ |
| Legal | © ® ™ § ¶ ° |
| Math | ± × ÷ ≠ ≤ ≥ ∞ √ ∑ ≈ |
| Arrows | → ← ↑ ↓ ↔ ⇒ ⇐ ↗ ↘ |
| Typography | … — – " " ' ' · • † ‡ ½ ¼ ¾ |

### Behaviour

- Panel has a search input at the top — typing filters all groups live (hides non-matching chars, collapses empty group headings).
- Clicking a character calls `_editor.insertText(char)`, closes the panel, and returns focus to the editor.
- Escape key closes the panel.
- Panel is non-blocking: no modal overlay, positioned near the toolbar button.
- Toolbar button icon: `Ω` with title "Special Characters".

### Panel HTML structure (in `Edit.cshtml`)

```html
<div id="special-chars-panel" class="tb-special-chars" hidden role="dialog" aria-label="Special Characters">
  <div class="tb-sc-header">
    <span class="tb-sc-title">Special Characters</span>
    <button type="button" id="btn-sc-close" class="tb-sc-close" aria-label="Close">&#x2715;</button>
  </div>
  <div class="tb-sc-search-row">
    <input type="search" id="sc-search" class="tb-sc-search" placeholder="Search…" autocomplete="off">
  </div>
  <div id="sc-groups" class="tb-sc-groups">
    <!-- groups rendered by JS -->
  </div>
</div>
```

### JS wiring (`wireSpecialChars` IIFE)

- On open: render groups into `#sc-groups`, focus search input.
- On search input: filter visible chars, hide empty group headings.
- On char click: insert + close.
- Expose `window._openSpecialChars` for toolbar plugin action.

**Files changed:** `template-editor.js`, `template-editor.css`, `Edit.cshtml`.

---

## Feature 3 — Print Button

**Effort:** ~5 lines JS

A custom SunEditor toolbar plugin that calls `window.print()` immediately on click. No modal, no panel.

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

Add `'print'` to the utility toolbar group alongside `'validate'`, `'findReplace'`, `'saveSnippet'`.

**CSS print styles** already exist in the browser's default handling for the editor canvas. No CSS changes needed.

**Files changed:** `template-editor.js` only.

---

## Feature 4 — Custom List Styles

**Effort:** ~20 lines JS, ~10 lines CSS

A new `listStylePlugin` toolbar button labelled "List Style ▾" opens an inline dropdown menu with 6 style options. Selecting a style applies `list-style-type` to the nearest `<ul>` or `<ol>` ancestor of the current cursor position.

### Styles

| Type | CSS value | Display |
|---|---|---|
| Disc | `disc` | ● bullet |
| Circle | `circle` | ○ circle |
| Square | `square` | ▪ square |
| Decimal | `decimal` | 1. 2. 3. |
| Upper Alpha | `upper-alpha` | A. B. C. |
| Lower Roman | `lower-roman` | i. ii. iii. |

### Behaviour

- Button click toggles the dropdown menu open/close.
- Clicking a style option: find `node.closest('ul, ol')` from the current selection anchor node, set `el.style.listStyleType = value`, call `markDirty()`, close menu.
- If cursor is not inside a list: show a brief toast "Place cursor inside a list first".
- Clicking outside closes the menu.

### Toolbar placement

The existing toolbar group is `['list', 'hrThin', 'hrThick', 'hrSpaced']`. Insert `'listStyle'` immediately after `'list'`: `['list', 'listStyle', 'hrThin', 'hrThick', 'hrSpaced']`.

**Files changed:** `template-editor.js`, `template-editor.css`.

---

## Feature 5 — Anchor / Bookmark Links

**Effort:** ~35 lines JS, ~8 lines CSS

### Part A — Insert Anchor

A custom SunEditor toolbar plugin (`anchorPlugin`) prompts for an anchor name using the browser `prompt()` dialog. On confirm:

1. Sanitise the input: lowercase, replace spaces with hyphens, strip non-alphanumeric (except `-`).
2. Insert at cursor: `<a name="{sanitised}" class="tb-anchor" contenteditable="false" title="#{sanitised}">⚓ {sanitised}</a>`.
3. Call `markDirty()`.

If the user cancels the prompt or enters an empty string, do nothing.

### Part B — Link to Anchor

No code change required. The existing SunEditor link dialog accepts any URL — users type `#anchor-name` in the URL field. The chip's `title` attribute displays the exact `#name` value so users can copy it.

### Chip appearance

```css
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

### Toolbar placement

Add `'anchor'` to the link group: `['link', 'table', 'image', 'anchor']`.

**Files changed:** `template-editor.js`, `template-editor.css`.

---

## File Change Summary

| File | Changes |
|---|---|
| `template-editor.js` | +~132 lines: 5 plugin objects + 1 IIFE (`wireSpecialChars`) + toolbar config updates |
| `template-editor.css` | +~38 lines: special chars panel, list style menu, anchor chip |
| `Edit.cshtml` | +~30 lines: special chars panel HTML |

All changes are additive. No existing functions, plugins, or CSS classes are modified.

---

## Out of Scope

- Anchor management UI (list/delete all anchors) — not needed for v1
- Link-to-anchor autocomplete in the link dialog — deferred
- Print preview rendering — `window.print()` uses browser default
