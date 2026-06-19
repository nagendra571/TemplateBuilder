# Auto-fill Loop & Grid Sample Data — Design Spec
**Date:** 2026-06-19
**Goal:** Extend the "⚡ Auto-fill from template" button so it also generates sample array data for Loop Block and Grid Block collections, not just top-level scalar fields.

---

## Problem

The existing `_tbGenerateSampleFromTemplate()` function (lines 465–479 of `template-editor.js`) only matches `{{ model.FieldName }}` tokens. It ignores:

- Loop declarations: `{{ for item in model.Items }}`
- Item-level field references inside loops: `{{ item.ProductName }}`, `{{ item.Qty }}`

This means clicking "Auto-fill from template" on a template that contains a Loop or Grid block produces JSON with no array data, so the preview render fails or shows empty loops.

---

## Solution

Extend `_tbGenerateSampleFromTemplate()` with two additional regex passes. No new files, no new dependencies — the change is fully self-contained inside the existing function.

### Algorithm — Three Passes Over the Template HTML

**Pass 1 — Top-level scalars (existing, unchanged)**
Pattern: `/\{\{-?\s*model\.(\w+)\s*-?\}\}/g`
Captures: `{{ model.CustomerName }}` → `obj.CustomerName = "Sample CustomerName"`

**Pass 2 — Loop declarations (new)**
Pattern: `/\{\{-?\s*for\s+(\w+)\s+in\s+model\.(\w+)\s*-?\}\}/g`
Captures: `{{ for item in model.Items }}` → `aliasMap["item"] = "Items"`
Builds a map of `alias → collectionName` used in Pass 3.

**Pass 3 — Item-level fields (new)**
Pattern: `/\{\{-?\s*(\w+)\.(\w+)\s*-?\}\}/g`
Captures: `{{ item.ProductName }}` → if `"item"` is in `aliasMap`, adds `"ProductName"` to `colFields["Items"]`
Skips aliases not in `aliasMap` (e.g., `model.` prefix won't match here since it's a two-part token).

### Output Construction

After all three passes:
- Scalar fields → top-level string values (unchanged)
- Collection fields → array of **2 sample items** per collection, overwriting any same-named scalar

Each collection produces:
```json
"Items": [
  { "ProductName": "Sample ProductName", "Qty": "Sample Qty" },
  { "ProductName": "Sample ProductName 2", "Qty": "Sample Qty 2" }
]
```

The second item appends `" 2"` to each value so the two rows are visually distinct in the preview.

---

## File Changed

| File | Change |
|---|---|
| `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` | Replace `_tbGenerateSampleFromTemplate()` body (lines 465–479) |

**No other files are modified.** This is a pure client-side JavaScript change.

---

## New Function Body

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

---

## Expected Output Per Template

### Order Summary — Loop Block Demo
```json
{
  "CustomerName": "Sample CustomerName",
  "OrderNumber": "Sample OrderNumber",
  "OrderDate": "Sample OrderDate",
  "Items": [
    {
      "ProductName": "Sample ProductName",
      "Qty": "Sample Qty",
      "UnitPrice": "Sample UnitPrice",
      "Total": "Sample Total"
    },
    {
      "ProductName": "Sample ProductName 2",
      "Qty": "Sample Qty 2",
      "UnitPrice": "Sample UnitPrice 2",
      "Total": "Sample Total 2"
    }
  ]
}
```

### Product Catalog — Grid Block Demo
```json
{
  "CatalogTitle": "Sample CatalogTitle",
  "PublishedDate": "Sample PublishedDate",
  "Items": [
    { "SKU": "Sample SKU", "ProductName": "Sample ProductName", "Category": "Sample Category", "Price": "Sample Price" },
    { "SKU": "Sample SKU 2", "ProductName": "Sample ProductName 2", "Category": "Sample Category 2", "Price": "Sample Price 2" }
  ]
}
```

### Customer Invoice — Snippets Demo
```json
{
  "InvoiceNumber": "Sample InvoiceNumber",
  "InvoiceDate": "Sample InvoiceDate",
  "CustomerName": "Sample CustomerName",
  "CustomerAddress": "Sample CustomerAddress",
  "OrderNumber": "Sample OrderNumber",
  "Subtotal": "Sample Subtotal",
  "TaxRate": "Sample TaxRate",
  "TaxAmount": "Sample TaxAmount",
  "TotalDue": "Sample TotalDue",
  "Items": [
    { "ProductName": "Sample ProductName", "Qty": "Sample Qty", "UnitPrice": "Sample UnitPrice", "LineTotal": "Sample LineTotal" },
    { "ProductName": "Sample ProductName 2", "Qty": "Sample Qty 2", "UnitPrice": "Sample UnitPrice 2", "LineTotal": "Sample LineTotal 2" }
  ]
}
```

---

## Verification (Playwright)

After the code change, Playwright navigates to each of the three example templates, opens the Preview modal, clicks "⚡ Auto-fill from template", and asserts:

1. **Order Summary** — `preview-json` textarea contains `"Items"` key with a 2-element array, each having `ProductName`, `Qty`, `UnitPrice`, `Total`.
2. **Product Catalog** — textarea contains `"Items"` array with `SKU`, `ProductName`, `Category`, `Price`.
3. **Customer Invoice** — textarea contains `"Items"` array with `ProductName`, `Qty`, `UnitPrice`, `LineTotal`, plus top-level invoice scalar fields.

---

## Success Criteria

- [ ] Clicking "Auto-fill from template" on Order Summary produces JSON with `Items` as a 2-element array
- [ ] Clicking "Auto-fill from template" on Product Catalog produces JSON with `Items` as a 2-element array
- [ ] Clicking "Auto-fill from template" on Customer Invoice produces JSON with `Items` as a 2-element array and all invoice scalar fields
- [ ] Top-level scalar fields (CustomerName, OrderNumber, OrderDate) still appear as strings in the Order Summary output — no regression from the existing Pass 1 logic
- [ ] Existing templates with no loop/grid blocks still produce flat JSON (no regression)
- [ ] The version is bumped to v1.4.3 and a new commit is made
