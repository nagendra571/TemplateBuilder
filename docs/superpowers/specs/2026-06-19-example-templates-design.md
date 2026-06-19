# Example Templates Design Spec
**Date:** 2026-06-19  
**Domain:** E-commerce / Order Management  
**Goal:** Create three realistic, fully-content templates that demonstrate Loop Block, Grid Block, and Snippets to consumers of TemplateBuilder.Editor.

---

## Overview

Three templates are created via Playwright automation against the running app at `https://localhost:7044/`. They share a coherent e-commerce theme (Acme Corp) so they read as a tutorial set rather than isolated demos.

| # | Template Name | Feature Demonstrated |
|---|---|---|
| 1 | Order Summary — Loop Block Demo | Loop Block wizard (Table starter) |
| 2 | Product Catalog — Grid Block Demo | Grid Block drag-and-drop |
| 3 | Customer Invoice — Snippets Demo | Snippets: create Header + Footer, compose invoice |

**Template engine:** Scriban (`{{ }}` syntax)  
**No code changes required** — all work is browser automation only.

---

## Template 1: Order Summary — Loop Block Demo

### Purpose
Teach consumers how to insert a Loop Block via the toolbar wizard, choose the Table starter, and reference collection item fields inside the loop.

### Creation Steps
1. Navigate to `/Templates` → click **Create New**
2. Name: `Order Summary — Loop Block Demo`
3. Description: `Demonstrates the Loop Block wizard with a table starter — iterates over order line items.`
4. In the editor, type an intro section:
   ```
   Dear {{ model.CustomerName }},

   Thank you for your order #{{ model.OrderNumber }} placed on {{ model.OrderDate }}.
   Below is a summary of the items in your order:
   ```
5. Click the **Insert Loop** toolbar button → Loop Wizard opens:
   - Collection: `Items`
   - Item alias: `item`
   - Starter: **Table**
   - Click **Insert**
6. Edit the inserted table headers (Column1 → `Product`, Column2 → `Qty`) and add two more columns: `Unit Price`, `Total`
7. Edit the data row cells to: `{{ item.ProductName }}`, `{{ item.Qty }}`, `{{ item.UnitPrice }}`, `{{ item.Total }}`
8. After the loop, type a closing paragraph:
   ```
   We will notify you by email once your order has shipped.
   Thank you for shopping with Acme Corp!
   ```
9. Click **Save**

### Expected Body (approximate Scriban)
```html
<p>Dear {{ model.CustomerName }},</p>
<p>Thank you for your order #{{ model.OrderNumber }} placed on {{ model.OrderDate }}.<br>
Below is a summary of the items in your order:</p>

<div class="tb-loop">
  {{ for item in model.Items }}
  <table border="1" style="width:100%;border-collapse:collapse;">
    <thead><tr><th>Product</th><th>Qty</th><th>Unit Price</th><th>Total</th></tr></thead>
    <tbody><tr>
      <td>{{ item.ProductName }}</td>
      <td>{{ item.Qty }}</td>
      <td>{{ item.UnitPrice }}</td>
      <td>{{ item.Total }}</td>
    </tr></tbody>
  </table>
  {{ end }}
</div>

<p>We will notify you by email once your order has shipped.<br>
Thank you for shopping with Acme Corp!</p>
```

---

## Template 2: Product Catalog — Grid Block Demo

### Purpose
Teach consumers how to drag a **Grid Block** from the left palette into the editor canvas, and how to rename the auto-generated column headers and field placeholders.

### Creation Steps
1. Navigate to `/Templates` → click **Create New**
2. Name: `Product Catalog — Grid Block Demo`
3. Description: `Demonstrates the Grid Block drag-and-drop — generates a full data table over a collection.`
4. In the editor, type a heading intro:
   ```
   {{ model.CatalogTitle }}
   Published: {{ model.PublishedDate }}

   The following products are currently available:
   ```
5. Drag **Grid Block** from the left panel palette into the editor
6. Edit the auto-generated table:
   - Header columns: `SKU` | `Product Name` | `Category` | `Price`
   - Data row cells: `{{ item.SKU }}` | `{{ item.ProductName }}` | `{{ item.Category }}` | `{{ item.Price }}`
7. Add a closing note: `Prices are subject to change without notice.`
8. Click **Save**

### Expected Body (approximate Scriban)
```html
<p><strong>{{ model.CatalogTitle }}</strong><br>
Published: {{ model.PublishedDate }}</p>
<p>The following products are currently available:</p>

<table border="1" style="width:100%;border-collapse:collapse;">
  <thead><tr>
    <th>SKU</th><th>Product Name</th><th>Category</th><th>Price</th>
  </tr></thead>
  <tbody>
  {{ for item in model.Items }}
  <tr>
    <td>{{ item.SKU }}</td>
    <td>{{ item.ProductName }}</td>
    <td>{{ item.Category }}</td>
    <td>{{ item.Price }}</td>
  </tr>
  {{ end }}
  </tbody>
</table>

<p><em>Prices are subject to change without notice.</em></p>
```

---

## Template 3: Customer Invoice — Snippets Demo

### Purpose
Teach consumers how to:
1. Create reusable snippets (Header and Footer) from editor content
2. Insert those snippets into a new template to compose a complete document

This is done in three phases.

### Phase A — Create "Company Header" Snippet
1. Navigate to `/Templates` → click **Create New**
2. Name: `[Snippet] Company Header`  
   Description: `Staging template for creating the Company Header snippet.`
3. Type the header content in the editor:
   ```
   ACME CORP
   Your trusted e-commerce partner
   123 Commerce Street, Springfield, ST 00000  |  support@acmecorp.com
   ─────────────────────────────────────────────────────────
   ```
4. Select all content (Ctrl+A)
5. Click **"Save selection as snippet…"** in the editor toolbar (or sidebar button)
6. Snippet name: `Company Header`
7. Description: `Reusable company branding header — use at the top of all customer-facing templates.`
8. Click **Save Snippet**

### Phase B — Create "Standard Footer" Snippet
9. Clear the editor (Ctrl+A → Delete) — still in the same staging template
10. Type the footer content:
    ```
    ─────────────────────────────────────────────────────────
    © 2026 Acme Corp. All rights reserved.
    This is an automated message. Do not reply directly to this email.
    For support: support@acmecorp.com  |  www.acmecorp.com
    ```
11. Select all content (Ctrl+A)
12. Click **"Save selection as snippet…"**
13. Snippet name: `Standard Footer`
14. Description: `Reusable legal footer — use at the bottom of all customer-facing templates.`
15. Click **Save Snippet**

### Phase C — Compose the Customer Invoice Template
16. Navigate to `/Templates` → click **Create New**
17. Name: `Customer Invoice — Snippets Demo`
18. Description: `Demonstrates snippet reuse — inserts Company Header and Standard Footer from the snippets library.`
19. In the editor:
    - Click **Insert** on `Company Header` from the snippets sidebar → header content appears
    - Press Enter, then type the invoice body:
      ```
      INVOICE #{{ model.InvoiceNumber }}
      Date: {{ model.InvoiceDate }}

      Bill To:
      {{ model.CustomerName }}
      {{ model.CustomerAddress }}

      Order Reference: {{ model.OrderNumber }}
      ```
    - Click **Insert Loop** toolbar button → Collection: `Items`, Alias: `item`, Starter: **Table** → Insert. Edit headers to: `Product`, `Qty`, `Unit Price`, `Line Total`. Edit cells to: `{{ item.ProductName }}`, `{{ item.Qty }}`, `{{ item.UnitPrice }}`, `{{ item.LineTotal }}`
    - Type a totals section:
      ```
      Subtotal: {{ model.Subtotal }}
      Tax ({{ model.TaxRate }}%): {{ model.TaxAmount }}
      TOTAL DUE: {{ model.TotalDue }}

      Payment is due within 30 days of this invoice date.
      ```
    - Click **Insert** on `Standard Footer` from the snippets sidebar
20. Click **Save**

---

## Implementation Approach

**Tooling:** Playwright MCP tools (`browser_navigate`, `browser_fill_form`, `browser_click`, `browser_type`, `browser_snapshot`, etc.)

**Execution order:**
1. Template 1 (Loop Block) — independent, create first
2. Template 2 (Grid Block) — independent, create second
3. Template 3, Phase A (Header snippet) — must precede Phase C
4. Template 3, Phase B (Footer snippet) — must precede Phase C
5. Template 3, Phase C (Invoice template) — depends on A and B

**Error handling:**
- Before creating each template, check if a template with that name already exists (navigate to index, scan list). If it does, skip creation or delete and recreate.
- After each Save, verify success by checking the URL changed to the edit page (not staying on create with errors).

**No code changes.** This is purely browser automation — no `.cs`, `.cshtml`, `.css`, or `.js` files are modified.

---

## Success Criteria
- [ ] Template 1 exists in the DB, body contains `{{ for item in model.Items }}` with a `<table>` inside
- [ ] Template 2 exists in the DB, body contains a `<table>` grid with `{{ for item in model.Items }}` rows
- [ ] Snippet "Company Header" exists and is visible in the sidebar of any template's edit page
- [ ] Snippet "Standard Footer" exists and is visible in the sidebar
- [ ] Template 3 exists and its body contains both snippet content blocks plus invoice fields
- [ ] All three templates save without errors (no validation messages visible after save)
