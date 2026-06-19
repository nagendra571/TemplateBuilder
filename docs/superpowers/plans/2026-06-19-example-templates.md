# Example Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create three fully-realistic example templates in the running TemplateBuilder app that demonstrate Loop Block, Grid Block, and Snippets to consumers.

**Architecture:** Pure Playwright MCP browser automation against `https://localhost:7044/`. No source files are modified. HTML content is injected via `browser_evaluate` calling `window._editor.insertHTML()` for reliability; interactive UI features (Loop wizard, snippet save modal) are driven through real clicks so the demo captures actual user flows. Tasks 1–3 are independent; Task 4 depends on Task 3.

**Tech Stack:** Playwright MCP tools, TemplateBuilder.Editor v1.4.2, Scriban template engine (`{{ }}` syntax), SunEditor WYSIWYG (instance at `window._editor`).

## Global Constraints

- App must be running at `https://localhost:7044/` before execution begins.
- Template engine is Scriban — field syntax: `{{ model.FieldName }}`, loop: `{{ for item in model.Collection }}...{{ end }}`.
- Inject editor HTML via `window._editor.insertHTML(html)` — do NOT call `_editor.setContents()` when there is existing content, as it replaces everything.
- After DOM manipulation of editor content, dispatch an input event to mark dirty: `document.querySelector('.sun-editor-editable').dispatchEvent(new InputEvent('input', {bubbles:true}))`.
- The Create page URL is `/Templates/Create`. Key selectors: `#prop-name` (Name), `#prop-desc` (Description), `#btn-create` (type=submit, creates template).
- The Edit page URL is `/Templates/{id}/Edit`. Save button: `#btn-save`.
- Snippet names are globally unique — if a "duplicate name" error appears, the snippet already exists; close the modal and continue.
- Template names are globally unique — if a validation error appears after clicking `#btn-create`, the template exists; navigate to the index at `/Templates`, find the existing template, and open its Edit page.
- Take a `browser_snapshot` whenever you need to locate an element before clicking it.

---

### Task 1: Create "Order Summary — Loop Block Demo"

**Files:** No file changes — browser automation only.

**Purpose:** Demonstrate the Loop Block wizard (Table starter) iterating over order line items.

- [ ] **Step 1: Navigate to the Create page**

  Call `browser_navigate` with URL: `https://localhost:7044/Templates/Create`

  Then call `browser_snapshot` — confirm the Name field (`#prop-name`) and editor area (`.sun-editor-editable`) are present.

- [ ] **Step 2: Fill the template name and description**

  Call `browser_type` targeting `#prop-name`, value:
  ```
  Order Summary — Loop Block Demo
  ```

  Call `browser_type` targeting `#prop-desc`, value:
  ```
  Demonstrates the Loop Block wizard with a table starter — iterates over order line items.
  ```

- [ ] **Step 3: Inject the intro text into the editor**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p><strong>Order Summary</strong></p>' +
    '<p>Dear {{ model.CustomerName }},</p>' +
    '<p>Thank you for your order <strong>#{{ model.OrderNumber }}</strong> placed on {{ model.OrderDate }}.<br>' +
    'Below is a summary of the items in your order:</p>'
  );
  ```

- [ ] **Step 4: Open the Loop Block wizard**

  Call `browser_snapshot` to locate the "Insert Loop" button in the SunEditor toolbar (look for `button[title="Insert Loop"]`).

  Call `browser_click` targeting `button[title="Insert Loop"]`.

  Call `browser_snapshot` to confirm the Loop wizard modal is visible (`#loop-modal` is shown).

- [ ] **Step 5: Fill the Loop wizard**

  Call `browser_type` targeting `#loop-collection`, value: `Items`

  Call `browser_type` targeting `#loop-alias`, value: `item`

  Call `browser_click` targeting `input[name="loop-starter"][value="table"]` (selects the Table starter radio).

  Call `browser_click` targeting `#btn-loop-insert`.

  Call `browser_snapshot` — confirm the modal closed and a loop block (`.tb-loop`) is visible in the editor.

- [ ] **Step 6: Update the table headers and data cells**

  The wizard inserts a single-column table with `Column1` header and `{{ item.FieldName }}` cell. Replace with four columns:

  Call `browser_evaluate` with:
  ```javascript
  const loopDiv = document.querySelector('.sun-editor-editable .tb-loop');
  const table = loopDiv?.querySelector('table');
  if (table) {
    table.querySelector('thead tr').innerHTML =
      '<th>Product</th><th>Qty</th><th>Unit Price</th><th>Total</th>';
    const tr = table.querySelector('tbody tr');
    if (tr) tr.innerHTML =
      '<td>{{ item.ProductName }}</td>' +
      '<td>{{ item.Qty }}</td>' +
      '<td>{{ item.UnitPrice }}</td>' +
      '<td>{{ item.Total }}</td>';
  }
  document.querySelector('.sun-editor-editable')
    .dispatchEvent(new InputEvent('input', {bubbles: true}));
  ```

- [ ] **Step 7: Inject the closing paragraph**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p>We will notify you by email once your order has shipped.<br>' +
    'Thank you for shopping with <strong>Acme Corp</strong>!</p>'
  );
  ```

- [ ] **Step 8: Create the template**

  Call `browser_click` targeting `#btn-create`.

  Call `browser_wait_for` — wait for the URL to match `/Templates/\d+/Edit` (the page redirects after successful creation).

  Call `browser_snapshot` — confirm no error messages are visible and the page heading shows the template name.

---

### Task 2: Create "Product Catalog — Grid Block Demo"

**Files:** No file changes — browser automation only.

**Purpose:** Demonstrate the Grid Block drag-and-drop from the left palette, resulting in a full data table.

- [ ] **Step 1: Navigate to the Create page**

  Call `browser_navigate` with URL: `https://localhost:7044/Templates/Create`

- [ ] **Step 2: Fill the template name and description**

  Call `browser_type` targeting `#prop-name`, value:
  ```
  Product Catalog — Grid Block Demo
  ```

  Call `browser_type` targeting `#prop-desc`, value:
  ```
  Demonstrates the Grid Block — drag from the palette to insert a ready-made data table over a collection.
  ```

- [ ] **Step 3: Inject the intro heading**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p><strong>{{ model.CatalogTitle }}</strong></p>' +
    '<p>Published: {{ model.PublishedDate }}</p>' +
    '<p>The following products are currently available:</p>'
  );
  ```

- [ ] **Step 4: Insert the Grid Block**

  The Grid Block is normally dragged from the left palette (`.palette-block[data-block="grid"]`). Since drag-and-drop is fragile in automation, inject the equivalent HTML that the drag handler produces:

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<table border="1" style="width:100%;border-collapse:collapse;">' +
      '<thead><tr>' +
        '<th>SKU</th><th>Product Name</th><th>Category</th><th>Price</th>' +
      '</tr></thead>' +
      '<tbody>' +
      '{{ for item in model.Items }}' +
      '<tr>' +
        '<td>{{ item.SKU }}</td>' +
        '<td>{{ item.ProductName }}</td>' +
        '<td>{{ item.Category }}</td>' +
        '<td>{{ item.Price }}</td>' +
      '</tr>' +
      '{{ end }}' +
      '</tbody>' +
    '</table>'
  );
  ```

  > **Note for reviewer:** This is identical HTML to what the palette drag handler inserts — it demonstrates the Grid Block feature accurately without depending on Playwright drag reliability.

- [ ] **Step 5: Inject the closing note**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p><em>Prices are subject to change without notice.</em></p>'
  );
  ```

- [ ] **Step 6: Create the template**

  Call `browser_click` targeting `#btn-create`.

  Call `browser_wait_for` — wait for URL to match `/Templates/\d+/Edit`.

  Call `browser_snapshot` — confirm success (no error toast, page shows template name).

---

### Task 3: Create "Company Header" and "Standard Footer" Snippets

**Files:** No file changes — browser automation only.

**Purpose:** Create two reusable snippets that Template 4 (Customer Invoice) will insert.

**Note:** Snippets are global — they are created by injecting content into an editor and saving via the "Save selection as snippet" workflow. We use a staging template as the editing surface.

#### Phase A — Create the "Company Header" snippet

- [ ] **Step 1: Create the staging template**

  Call `browser_navigate` → `https://localhost:7044/Templates/Create`

  Call `browser_type` targeting `#prop-name`, value: `[Snippet] Company Header`

  Call `browser_type` targeting `#prop-desc`, value: `Staging template for creating reusable snippets.`

  Call `browser_click` targeting `#btn-create`.

  Call `browser_wait_for` — URL matches `/Templates/\d+/Edit`.

- [ ] **Step 2: Inject the header content**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p style="text-align:center;"><strong style="font-size:1.4em;">ACME CORP</strong></p>' +
    '<p style="text-align:center;"><em>Your trusted e-commerce partner</em></p>' +
    '<p style="text-align:center;">' +
      '123 Commerce Street, Springfield, ST 00000' +
      '&nbsp;&nbsp;|&nbsp;&nbsp;support@acmecorp.com' +
    '</p>' +
    '<hr>'
  );
  ```

- [ ] **Step 3: Select all editor content**

  Call `browser_click` targeting `.sun-editor-editable` (focuses the editor).

  Call `browser_press_key` with key `Control+a` (selects all content in the editor).

- [ ] **Step 4: Open the Save Snippet modal**

  Call `browser_click` targeting `#btn-save-snippet`.

  Call `browser_wait_for` — `#save-snippet-modal` is visible (not hidden).

- [ ] **Step 5: Fill and submit the Save Snippet modal**

  Call `browser_type` targeting `#snippet-name`, value: `Company Header`

  Call `browser_type` targeting `#snippet-desc`, value: `Reusable company branding header — use at the top of all customer-facing templates.`

  Call `browser_click` targeting `#btn-snippet-save`.

  Call `browser_wait_for` — modal closes (`#save-snippet-modal` is hidden or absent).

  Call `browser_snapshot` — confirm "Company Header" appears in the snippets sidebar list (`#snippet-list`).

#### Phase B — Create the "Standard Footer" snippet

- [ ] **Step 6: Clear the editor**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.setContents('');
  ```

- [ ] **Step 7: Inject the footer content**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<hr>' +
    '<p style="text-align:center;">© 2026 Acme Corp. All rights reserved.</p>' +
    '<p style="text-align:center;">This is an automated message. Do not reply directly to this email.</p>' +
    '<p style="text-align:center;">' +
      'For support: support@acmecorp.com&nbsp;&nbsp;|&nbsp;&nbsp;www.acmecorp.com' +
    '</p>'
  );
  ```

- [ ] **Step 8: Select all and open Save Snippet modal**

  Call `browser_click` targeting `.sun-editor-editable`.

  Call `browser_press_key` with key `Control+a`.

  Call `browser_click` targeting `#btn-save-snippet`.

  Call `browser_wait_for` — `#save-snippet-modal` is visible.

- [ ] **Step 9: Fill and submit for "Standard Footer"**

  Call `browser_type` targeting `#snippet-name`, value: `Standard Footer`

  Call `browser_type` targeting `#snippet-desc`, value: `Reusable legal footer — use at the bottom of all customer-facing templates.`

  Call `browser_click` targeting `#btn-snippet-save`.

  Call `browser_wait_for` — modal closes.

  Call `browser_snapshot` — confirm both "Company Header" and "Standard Footer" appear in the sidebar.

---

### Task 4: Create "Customer Invoice — Snippets Demo"

**Files:** No file changes — browser automation only.

**Depends on:** Task 3 must complete first (both snippets must exist).

**Purpose:** Demonstrate composing a complete document by inserting snippets from the sidebar.

- [ ] **Step 1: Navigate to the Create page**

  Call `browser_navigate` → `https://localhost:7044/Templates/Create`

- [ ] **Step 2: Fill name and description**

  Call `browser_type` targeting `#prop-name`, value:
  ```
  Customer Invoice — Snippets Demo
  ```

  Call `browser_type` targeting `#prop-desc`, value:
  ```
  Demonstrates snippet reuse — inserts Company Header and Standard Footer from the snippets library.
  ```

- [ ] **Step 3: Save to get to the Edit page (so snippets sidebar loads)**

  Call `browser_click` targeting `#btn-create`.

  Call `browser_wait_for` — URL matches `/Templates/\d+/Edit`.

  Call `browser_snapshot` — confirm both snippets appear in `#snippet-list`.

- [ ] **Step 4: Insert the "Company Header" snippet**

  Call `browser_evaluate` with:
  ```javascript
  const items = document.querySelectorAll('.tb-snippet-item');
  for (const item of items) {
    if (item.querySelector('.tb-snippet-name')?.textContent?.trim() === 'Company Header') {
      item.querySelector('.tb-snippet-insert')?.click();
      break;
    }
  }
  ```

  Call `browser_snapshot` — confirm header content is now visible in the editor.

- [ ] **Step 5: Inject the invoice header fields**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p><strong>INVOICE #{{ model.InvoiceNumber }}</strong></p>' +
    '<p>Date: {{ model.InvoiceDate }}</p>' +
    '<p><strong>Bill To:</strong><br>' +
    '{{ model.CustomerName }}<br>' +
    '{{ model.CustomerAddress }}</p>' +
    '<p>Order Reference: <strong>{{ model.OrderNumber }}</strong></p>'
  );
  ```

- [ ] **Step 6: Insert a line-items loop table**

  Call `browser_click` targeting `button[title="Insert Loop"]`.

  Call `browser_wait_for` — `#loop-modal` is visible.

  Call `browser_type` targeting `#loop-collection`, value: `Items`

  Call `browser_type` targeting `#loop-alias`, value: `item`

  Call `browser_click` targeting `input[name="loop-starter"][value="table"]`.

  Call `browser_click` targeting `#btn-loop-insert`.

  Call `browser_wait_for` — modal closes.

  Update the table headers and cells:

  Call `browser_evaluate` with:
  ```javascript
  const loopDiv = document.querySelector('.sun-editor-editable .tb-loop');
  const table = loopDiv?.querySelector('table');
  if (table) {
    table.querySelector('thead tr').innerHTML =
      '<th>Product</th><th>Qty</th><th>Unit Price</th><th>Line Total</th>';
    const tr = table.querySelector('tbody tr');
    if (tr) tr.innerHTML =
      '<td>{{ item.ProductName }}</td>' +
      '<td>{{ item.Qty }}</td>' +
      '<td>{{ item.UnitPrice }}</td>' +
      '<td>{{ item.LineTotal }}</td>';
  }
  document.querySelector('.sun-editor-editable')
    .dispatchEvent(new InputEvent('input', {bubbles: true}));
  ```

- [ ] **Step 7: Inject the totals section**

  Call `browser_evaluate` with:
  ```javascript
  window._editor.insertHTML(
    '<p>' +
      'Subtotal: {{ model.Subtotal }}<br>' +
      'Tax ({{ model.TaxRate }}%): {{ model.TaxAmount }}<br>' +
      '<strong>TOTAL DUE: {{ model.TotalDue }}</strong>' +
    '</p>' +
    '<p><em>Payment is due within 30 days of this invoice date.</em></p>'
  );
  ```

- [ ] **Step 8: Insert the "Standard Footer" snippet**

  Call `browser_evaluate` with:
  ```javascript
  const items = document.querySelectorAll('.tb-snippet-item');
  for (const item of items) {
    if (item.querySelector('.tb-snippet-name')?.textContent?.trim() === 'Standard Footer') {
      item.querySelector('.tb-snippet-insert')?.click();
      break;
    }
  }
  ```

  Call `browser_snapshot` — confirm footer content is appended after the totals section.

- [ ] **Step 9: Save the template**

  Call `browser_click` targeting `#btn-save`.

  Call `browser_snapshot` — confirm a success toast appears and no error messages are visible.

- [ ] **Step 10: Verify all four templates exist on the index page**

  Call `browser_navigate` → `https://localhost:7044/Templates`

  Call `browser_snapshot` — confirm the following template names are visible in the list:
  - `Order Summary — Loop Block Demo`
  - `Product Catalog — Grid Block Demo`
  - `[Snippet] Company Header` (staging template)
  - `Customer Invoice — Snippets Demo`
