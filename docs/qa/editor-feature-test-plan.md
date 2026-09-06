# TemplateBuilder.Editor Feature Testing Plan

## Editor Features to Test

Based on the UI inspection, the following editor features are available in the TemplateBuilder.Editor canvas:

### Text Formatting
- Bold (B)
- Italic (I) 
- Underline (U)
- Strikethrough (S)
- Subscript
- Superscript

### Text Styling
- Font Color
- Highlight Color
- Font Family
- Font Size
- Line Height

### Text Alignment
- Align Left
- Align Center
- Align Right
- Align Justify

### Lists
- Bulleted List
- Numbered List
- List Styles

### Indents & Special
- Outdent
- Indent
- Page Break
- Blockquote
- Remove Format

### Objects & Media
- Link
- Table
- Image
- Insert Anchor
- Insert Field
- Insert Loop
- Insert Conditional
- Quote
- Special Characters
- Print
- Code View
- Full Screen

### Snippets & Templates
- Field Palette (variables)
- Blocks (Loop Block, Grid Block)
- Snippets (Company Header, Standard Footer)
- Save selection as snippet

### Tools
- Undo/Redo
- Validate Template (✓)
- Find & Replace (Ctrl+H)
- Toggle Auto-save
- Toggle Theme (Light/Dark)

## Test Approach

For each feature, the test should:
1. Enter sample text in the editor canvas
2. Select text (where applicable)
3. Apply the formatting/feature
4. Verify visual change in the editor
5. Save template and verify persistence
6. Edit template and verify feature persists

## Sample Test Data
```
This is a test sentence for validating editor features.
We will test bold, italic, underline, and other formatting options.
The quick brown fox jumps over the lazy dog.
```

## Expected Results
- All formatting options should apply correctly to selected text
- Changes should persist when saving and reopening templates
- No JavaScript console errors should occur during testing
- The editor should remain responsive throughout testing