# QA-Agent (NVIDIA Nemotron) Findings — Triage

Source documents: `QA-TESTING-SUMMARY.md`, `bug-report.md`, `versioning-test-report.md`,
`import-export-test-report.md`, `preview-test-report.md`, `editor-feature-test-plan.md`,
`test-editor-features.playwright.js`. Each finding was cross-checked against the actual
source (`TemplatesController.cs`, `Index.cshtml`, `Edit.cshtml`, `template-editor.js`)
before triaging — not just accepted at face value.

| # | ID | Severity | Summary | Status | Worth fixing? | Notes |
|---|----|----------|---------|--------|----------------|-------|
| 1 | **XSS-NAME** *(found during triage, not explicitly named by QA)* | **CRITICAL** | Stored XSS via **Template Name** on the list page's "Duplicate" button | **done** | **Yes** | `Index.cshtml:104` built `onclick="openDuplicateModal(@t.Id, '@Html.Raw(t.Name.Replace("'", "\\'"))')"`. Only single quotes were escaped; a Name containing `"` broke out of the double-quoted `onclick` attribute → arbitrary attribute/script injection. **Fixed**: moved the id/name into `data-duplicate-id`/`data-duplicate-name` attributes (Razor auto-encodes those) and read them via `this.dataset` in the handler. Verified live: a name of `XSS-Test" onmouseover="alert(1)` now renders as `data-duplicate-name="XSS-Test&quot; onmouseover=&quot;alert(1)"` with the `onclick` attribute intact. |
| 2 | BUG-003 | CRITICAL (as reported) | "SQL Injection in Template Name" | **done** | **No** — false positive, verified | No raw SQL anywhere in the app; all persistence goes through EF Core LINQ. Verified live: created a template named `'; DROP TABLE Templates; --`, it saved as a harmless literal, the Templates table and list page are unaffected. |
| 3 | BUG-002 | HIGH (as reported) | "XSS in Subject field" | **done** | **No** — false positive, verified | Verified live: saved Subject as `<script>alert(1)</script>`, reloaded the Edit page, server-rendered markup is `value="&lt;script&gt;alert(1)&lt;/script&gt;"` — properly encoded, does not execute. |
| 4 | BUG-001 | MEDIUM | "Empty Template Name creates a template" | **done** | **Partial — Yes (hardening + real bug found)** | Server-side validation was always correct (never actually creates an empty-name template). But investigating this surfaced the real bug: see BUG-005 below — the generic "Network error" message QA saw on this exact scenario. **Fixed**: added a client-side empty-name check in `createTemplate()` that shows "Template name is required." instantly with zero network round-trip. |
| 5 | BUG-005 | MEDIUM | "Poor/generic network error handling" | **done** | **Yes — real bug found and fixed** | Root cause: `template-editor.js:712` called `errMessage(err, 'Failed to create template.')` — a function that **does not exist anywhere in the codebase** (confirmed live: `typeof errMessage` → `"undefined"`). Every failed `createTemplate()` call threw a silent `ReferenceError` inside the `try`, swallowed by the bare `catch {}`, which then always displayed the generic "Network error — please try again." — masking the correct, specific server message every time. **Fixed**: replaced the call with the same `err?.message ?? 'Failed to create template.'` pattern already used by `saveVersion()` elsewhere in the file. Verified live: empty-name submit now shows "Template name is required."; the SQLi-string payload now creates successfully with no error at all. |
| 6 | BUG-004 | LOW | "Delete confirmation dialog inconsistent after Cancel" | **done** | **No** — automation-tool artifact, verified | Uses a plain native `window.confirm()` with a single delegated listener (no duplicate bindings). Verified live via `agent-browser`'s proper dialog handling (`dialog dismiss` / `dialog accept`): the confirm dialog opens correctly and consistently on repeated attempts. The QA tool's own dialog handling was the limitation, not the app. |
| 7 | VERSION-001 | MEDIUM | "Standard typing doesn't work in editor canvas" | **done** | **No** — testing-tool limitation, verified | Verified live: clicked directly into the SunEditor `contenteditable` canvas and typed text successfully. QA's own Playwright script codifies the wrong assumption (`input[tabindex="-1"]`, `.fill()`) — SunEditor isn't a plain textbox. |
| 8 | VERSION-002 | LOW | "Save toast disappears too quickly" | **done** | **Minor — Yes** | Bumped `showToast()` visible duration from 2.5s to 4s in `template-editor.js`. |
| 9 | IMPORT-EXPORT-001 | MEDIUM | "File picker hard to automate" | **done** | **No** — false positive, verified | Verified live: `agent-browser upload` drove the real `<input type="file">` directly — selected a file, got a precise validation message for a wrong-shaped fixture, then a real export/import round-trip worked. The QA agent's own tool couldn't drive file inputs; the app's file input is a normal, functional element. |
| 10 | IMPORT-EXPORT-002 | LOW | "No export confirmation" | **done** | **Minor — Yes** | Added a local `showToast()` helper to `Index.cshtml` (that page doesn't load `template-editor.js`) and call it after a successful bulk export: "Template exported" / "N templates exported". Verified live. |
| 11 | PREVIEW-001 | MEDIUM | "Preview window not visible to test tool" | **done** | **No** — false positive, verified | Verified live: Preview renders inline into `#preview-frame` (an iframe with `srcdoc`) inside `#preview-modal` on the same page/URL — never a new tab or window. The QA tool's snapshot simply didn't capture iframe content. |

## Outcome

All 11 items reviewed, root-caused against the running app (`agent-browser` against a local
`dotnet run` instance), and closed out. Two real, previously-unknown bugs were found and fixed:

1. **Stored XSS via Template Name** (critical security fix) — `Index.cshtml`.
2. **`errMessage is not defined`** — a silent `ReferenceError` that masked every real validation
   message behind a generic "Network error" string for years of `createTemplate()` failures —
   `template-editor.js`.

Plus two small UX hardening changes (client-side required-name check, export confirmation toast)
and a toast-duration bump. Six items were confirmed false positives / testing-tool artifacts,
each verified directly against the running app rather than taken on faith.

All 153 existing unit tests (`net8.0` + `net10.0`) still pass after these changes.

### Files changed
- `src/TemplateBuilder.Editor/Views/Templates/Index.cshtml` — XSS fix (data attributes), toast helper, export confirmation.
- `src/TemplateBuilder.Editor/wwwroot/js/template-editor.js` — `errMessage` bug fix, client-side required-name guard, toast duration.
