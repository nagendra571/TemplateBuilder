# QA-Agent Recommendations — Triage

Every "Recommendations" section from the NVIDIA Nemotron QA pass, deduplicated across all
6 source documents in `docs/qa/`, and checked against the current codebase (not taken on
faith — several were already implemented, already fixed by the prior triage pass, or
based on a misunderstanding of the UI). `editor-feature-test-plan.md` and
`import-export-test-plan.md` are test plans with no "Recommendations" section, so nothing
from them appears here.

| # | Recommendation | Source(s) | Status | isWorthDoing | Notes |
|---|-----------------|-----------|--------|--------------|-------|
| 1 | Implement parameterized queries to prevent SQL injection | QA-TESTING-SUMMARY, bug-report | done | **No** | Already true — all persistence goes through EF Core LINQ, verified live in the prior triage pass (BUG-003). |
| 2 | HTML encode all user-generated content before rendering | QA-TESTING-SUMMARY, bug-report | done | **No** | Already fixed in the prior pass (stored XSS via Template Name / `Index.cshtml`'s Duplicate button, commit `6b7190e`). |
| 3 | Provide clear, specific error messages instead of generic network errors | QA-TESTING-SUMMARY | done | **No** | Already fixed in the prior pass (the `errMessage()` bug, commit `6b7190e`). |
| 4 | Enhance import/export UX with confirmation messages | QA-TESTING-SUMMARY, import-export-test-report | done | **No** | Export toast already added. Import already gives detailed per-item Created/Updated/Skipped/Error feedback in the modal (`appendResultEntry` in `Index.cshtml`) — this was already solid before the QA pass. |
| 5 | Ensure preview functionality is clearly visible and accessible | QA-TESTING-SUMMARY, preview-test-report | done | **No** | False positive — preview renders inline via `#preview-modal` + iframe `srcdoc`, verified live in the prior pass. |
| 6 | Fix delete confirmation dialog consistency | QA-TESTING-SUMMARY | done | **No** | False positive — native `confirm()` behaves correctly on repeat, verified live in the prior pass (BUG-004). |
| 7 | Improve editor canvas interaction for better text editing | QA-TESTING-SUMMARY, versioning-test-report | done | **No** | False positive — SunEditor's `contenteditable` canvas accepts direct typing fine, verified live in the prior pass (VERSION-001). |
| 8 | Add version comparison side-by-side view | versioning-test-report | done | **No** | Already exists — the "Compare" button per version renders old vs. current side-by-side in two iframes (`_renderComparePanel`, `#compare-modal`). QA didn't find it because their tool couldn't drive the editor UI far enough. |
| 9 | Add version information to exported templates for change tracking | import-export-test-report | done | **No** | Already exists — every export document carries `schemaVersion`, `exporter.name`/`version`, and `exportedAt` (`TemplateExportDocument`). |
| 10 | Implement proper state management for UI components | bug-report | n/a | **No** | Tied to the delete-dialog false positive (#6) — no actual state bug was found to fix. |
| 11 | Add comprehensive input validation and sanitization / proper client-and-server-side validation | QA-TESTING-SUMMARY, bug-report | **done** | **Yes** | Real gap found while investigating: `TemplateEditorViewModel.Name` has `[StringLength(200)]` matching the `nvarchar(200)` DB column, but the controller never checked `ModelState` — so a name over 200 chars threw a raw `DbUpdateException`, mislabeled by all three write actions as **"already exists"**. Fixed with a shared `ValidateTemplateName()` helper used by `Create`, `SaveVersion`, and `Duplicate` — the last of which had *no* name validation at all (`request.NewName.Trim()` on a missing field would NRE). Verified live: a 201-char name now gets "Template name cannot exceed 200 characters." |
| 12 | Add persistent/clear feedback for version actions (save, restore, etc.) | QA-TESTING-SUMMARY, versioning-test-report | **done** | **Yes** | **Restore** (`restoreVersion()` and `restoreFromCompare()` in `template-editor.js`) did a silent `window.location.reload()` on success with zero feedback. Fixed with a `queueToastAcrossReload()` helper (stashes the message in `sessionStorage`, shown by the next page load) — Restore now shows "Restored to vN". Verified live. |
| 13 | Add drag-and-drop import functionality | QA-TESTING-SUMMARY, import-export-test-report | **done** | **Yes** | Added a `#import-dropzone` around the existing file input in `Index.cshtml`, with `dragenter`/`dragover`/`dragleave`/`drop` handlers that assign the dropped file to the existing input (`fileInput.files = e.dataTransfer.files`) — the rest of the import pipeline is unchanged. Verified live via a synthetic `DragEvent` + `DataTransfer`: dropped file lands in the input, drag-active styling toggles correctly. |
| 14 | Validate file uploads for security/correctness (size and type) | QA-TESTING-SUMMARY | **done** | **Yes (minor)** | Malformed JSON was already handled gracefully. Added a `MaxImportFileBytes` (5 MB) size check and a `.json` extension check to `Import()` in `TemplatesController`, both before the file is read into memory. Verified live: a 6 MB file gets `FILE_TOO_LARGE`, a `.txt` file gets `INVALID_FILE_TYPE`. |
| 15 | Add automated tests for security vulnerabilities | bug-report | **done** | **Yes** | This project has no Razor-rendering/`WebApplicationFactory` test host, and building one just for two markup/script fixes would be disproportionate. Added `SecurityRegressionTests.cs` instead — source-guard tests (same pattern as the existing `SchemaScriptGenerationTests`) asserting `Index.cshtml` never re-introduces `Html.Raw(t.Name` and `template-editor.js` never re-introduces a call to the nonexistent `errMessage(`. Also added controller-level tests for the #11 validation gaps (name-length, missing-name-on-Duplicate). |
| 16 | Enhance preview with sample-data viewing and print options | QA-TESTING-SUMMARY, preview-test-report | **done** | **Yes (scoped to print only)** | Sample-data viewing already existed. Added a "🖨 Print" button to `#preview-frame-wrap` in `Edit.cshtml` that calls `.print()` on the preview iframe's own `contentWindow` — printing just the rendered template, not the host page. Verified live (confirmed it targets the iframe, not `window`). |
| 17 | Consider version tagging/labeling for easier identification | QA-TESTING-SUMMARY, versioning-test-report | open | **Maybe** | `ChangeComment` (the "save note") already provides free-text per-version labeling. A structured, filterable tag is a bigger feature (schema change, UI for managing tag vocabulary) — a product decision, not a bug fix. Flagging for a decision rather than assuming. |
| 18 | Add ability to delete specific versions from history (with confirmation) | versioning-test-report | open | **Maybe** | Doesn't exist today. This codebase leans heavily on an immutable version/audit trail (every write path is audited via `ActorResolver` + `AuditService`); allowing version deletion cuts against that design and has data-integrity implications. Flagging for a product decision rather than assuming it should be built. |
| 19 | Consider API-level testing for import/export to bypass UI limitations | QA-TESTING-SUMMARY, import-export-test-report | open | **Maybe** | A testing-infrastructure suggestion, not a product feature. Reasonable long-term, but lower priority than the concrete bugs/gaps above — parking it rather than doing it opportunistically. |

## Outcome

All 6 items (the 5 **Yes** plus #16, print-preview only, per user decision) implemented and
verified live against a local `dotnet run` instance via `agent-browser`. All 169 tests
(`net8.0` + `net10.0`, up from 153) pass.

### Files changed
- `src/TemplateBuilder.Editor/Controllers/TemplatesController.cs` — `ValidateTemplateName()` helper (#11), import file size/type guard (#14).
- `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` — `queueToastAcrossReload()` + restore-toast wiring (#12), print-preview handler (#16).
- `src/TemplateBuilder.Editor/Views/Templates/Index.cshtml` — drag-and-drop dropzone (#13).
- `src/TemplateBuilder.Editor/Views/Templates/Edit.cshtml` — print button markup (#16).
- `src/TemplateBuilder.Editor/wwwroot/css/template-editor.css` — dropzone + preview-toolbar styles.
- `tests/TemplateBuilder.Editor.Tests/SecurityRegressionTests.cs` — new (#15).
- `tests/TemplateBuilder.Editor.Tests/Controllers/TemplatesControllerTests.cs` — new validation/guard tests (#11, #14).

Items 17–19 (**Maybe**) remain net-new features or product/design questions rather than bugs or
gaps in already-committed behavior — left documented but unbuilt, pending a separate decision.
