# TemplateBuilder.Editor Import/Export Feature Testing Plan

## Export Feature Testing
1. **Single Template Export**:
   - Select one template from the list
   - Click Export button
   - Verify .template.json file is downloaded
   - Verify file contains correct template data

2. **Multiple Template Export**:
   - Select multiple templates from the list
   - Click Export button
   - Verify .template.json file is downloaded
   - Verify file contains data for all selected templates

## Import Feature Testing
1. **Valid Import**:
   - Click Import button
   - Select a valid .template.json file (previously exported)
   - Click Import button
   - Verify new template appears in list with correct data

2. **Invalid Import**:
   - Click Import button
   - Select invalid file (wrong format, corrupted JSON)
   - Click Import button
   - Verify appropriate error message is shown

3. **Duplicate Handling**:
   - Import template with same name as existing template
   - Verify system handles duplicates appropriately (rename, overwrite prompt, etc.)

## Test Data Requirements
- Need to create test .template.json files for import testing
- Should test with various template types (Email, Report, Notice, Custom)
- Should test with templates containing snippets, blocks, fields, etc.

## Expected Results
- Export should produce valid JSON files containing all template data
- Import should correctly parse JSON and create functional templates
- Error handling should be clear and helpful for invalid files
- Imported templates should be fully functional (editable, versionable, etc.)

## Files to Create for Testing
- sample-email-template.template.json
- sample-report-template.template.json
- invalid-template-file.txt (for error testing)