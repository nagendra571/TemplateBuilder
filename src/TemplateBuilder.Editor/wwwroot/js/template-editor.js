const _csrf = document.querySelector('input[name=__RequestVerificationToken]')?.value ?? '';

// ── Theme ─────────────────────────────────────────────────────────────────────
const _host = document.getElementById('tb-editor-host');
const _theme = localStorage.getItem('tb-theme') || 'dark';
if (_theme === 'light') _host?.classList.add('tb-theme-light');

function escapeHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ── Unsaved-change tracking ───────────────────────────────────────────────────

let _isDirty = false;
function markDirty() { _isDirty = true; }
function markClean() { _isDirty = false; }

let _currentColumns = [];


window.addEventListener('beforeunload', (e) => {
    if (_isDirty) {
        e.preventDefault();
        e.returnValue = '';
    }
});

// ── Editor initialisation ─────────────────────────────────────────────────────

let _editor = null;

// ── Custom plugins (must be registered before SUNEDITOR.create) ───────────────

const blockquotePlugin = {
    name: 'blockquote',
    display: 'command',
    title: 'Blockquote',
    innerHTML: '<span style="font-size:1rem;font-weight:700;">❝</span>',
    add: function(core) {},
    action: function() {
        if (!_editor) return;
        document.execCommand('formatBlock', false, 'blockquote');
        markDirty();
    }
};

const pageBreakPlugin = {
    name: 'pageBreak',
    display: 'command',
    title: 'Page Break',
    innerHTML: '<span style="font-size:.7rem;letter-spacing:.03em;">PG↵</span>',
    add: function(core) {},
    action: function() {
        if (!_editor) return;
        _editor.insertHTML('<div class="tb-page-break" contenteditable="false">— Page Break —</div>');
        markDirty();
    }
};

function makeHrPlugin(name, title, iconStyle, suffix) {
    return {
        name,
        display: 'command',
        title,
        innerHTML: `<span style="display:inline-block;width:14px;${iconStyle};vertical-align:middle;"></span>`,
        add: function(core) {},
        action: function() {
            if (!_editor) return;
            _editor.insertHTML(`<hr class="tb-hr tb-hr--${suffix}">`);
            markDirty();
        }
    };
}
const hrThin   = makeHrPlugin('hrThin',   'Thin Rule',   'border-top:1px solid currentColor',   'thin');
const hrThick  = makeHrPlugin('hrThick',  'Thick Rule',  'border-top:3px solid currentColor',   'thick');
const hrSpaced = makeHrPlugin('hrSpaced', 'Spaced Rule', 'border-top:1px dashed currentColor',  'spaced');

const insertFieldPlugin = {
    name: 'insertField',
    display: 'command',
    title: 'Insert Field',
    innerHTML: '<span style="font-size:.72rem;font-weight:600;letter-spacing:.03em;">&#123;&#123; &#125;&#125;</span>',
    add: function(core) {},
    action: function() {
        if (!_editor) return;
        const view = document.getElementById('view-selector')?.value;
        if (!view) { showToast('Select a SQL view first'); return; }
        toggleFieldDropdown();
    }
};

const insertLoopPlugin = {
    name: 'insertLoop',
    display: 'command',
    title: 'Insert Loop',
    innerHTML: '<span style="font-size:.72rem;font-weight:600;">&#8635;</span>',
    add: function(core) {},
    action: function() { if (!_editor) return; openLoopWizard(); }
};

const insertConditionalPlugin = {
    name: 'insertConditional',
    display: 'command',
    title: 'Insert Conditional',
    innerHTML: '<span style="font-size:.72rem;font-weight:600;">if</span>',
    add: function(core) {},
    action: function() { if (!_editor) return; openConditionalWizard(); }
};

const validatePlugin = {
    name: 'validate',
    display: 'command',
    title: 'Validate Template',
    innerHTML: '<span style="font-size:.72rem;font-weight:600;">&#x2713;</span>',
    add: function(core) {},
    action: function() { if (!_editor) return; runValidate(); }
};

const findReplacePlugin = {
    name: 'findReplace',
    display: 'command',
    title: 'Find & Replace (Ctrl+H)',
    innerHTML: '<span style="font-size:.82rem;font-weight:600;">⌕</span>',
    add: function(core) {},
    action: function() { window._openFindReplace?.(); }
};


_editor = SUNEDITOR.create(document.getElementById('template-body'), {
    plugins: [
        blockquotePlugin,
        pageBreakPlugin,
        hrThin, hrThick, hrSpaced,
        insertFieldPlugin,
        insertLoopPlugin,
        insertConditionalPlugin,
        validatePlugin,
        findReplacePlugin,
    ],
    height: '100%',
    theme: _theme === 'dark' ? 'dark' : undefined,
    buttonList: [
        ['undo', 'redo'],
        ['bold', 'italic', 'underline', 'strike'],
        ['subscript', 'superscript'],
        ['formatBlock', 'font', 'fontSize'],
        ['fontColor', 'hiliteColor'],
        ['align'],
        ['list', 'hrThin', 'hrThick', 'hrSpaced'],
        ['pageBreak'],
        ['link', 'table', 'image'],
        ['insertField'],
        ['insertLoop'],
        ['insertConditional'],
        ['blockquote', 'removeFormat'],
        ['validate', 'findReplace'],
        ['codeView', 'fullScreen'],
    ],
    font: ['Arial', 'Georgia', 'Courier New', 'Trebuchet MS', 'Verdana', 'Times New Roman', 'Tahoma', 'Impact'],
    fontSize: [10, 12, 14, 16, 18, 20, 24, 28, 32, 36],
    addTagsWhitelist: 'span|div|img|hr|blockquote',
    attributesWhitelist: {
        span:  'class|style|contenteditable',
        div:   'class|style|contenteditable',
        img:   'src|alt|width|height|style',
        table: 'border|cellpadding|cellspacing|style|class',
        tr:    'style|class',
        td:    'style|class|contenteditable|colspan|rowspan',
        th:    'style|class|contenteditable|colspan|rowspan',
        all:   'data-*'
    },
    onChange: () => { markDirty(); updateWordCount(); },
    linkTargetNewWindow: true,
    imageUploadBeforeHandler: function(files, info, core, uploadHandler) {
        const alt = (info?.altText ?? info?.alt ?? '').trim();
        if (!alt) {
            showToast('Alt text is required for images (accessibility).');
            uploadHandler?.(null);
            return false;
        }
        return true;
    }
});

// ── Drag-and-drop into editor ─────────────────────────────────────────────────

(function wireEditorDrop() {
    const editorArea = document.querySelector('.sun-editor-editable');
    if (!editorArea) return;

    editorArea.addEventListener('dragover', (e) => {
        if (e.dataTransfer.types.includes('field-name') || e.dataTransfer.types.includes('block-type')) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        }
    }, true);

    editorArea.addEventListener('drop', (e) => {
        const fieldName = e.dataTransfer.getData('field-name');
        const blockType = e.dataTransfer.getData('block-type');
        if (!fieldName && !blockType) return;

        e.preventDefault();
        e.stopImmediatePropagation();

        // Position cursor at the drop point (Chrome/Safari then Firefox)
        if (document.caretRangeFromPoint) {
            const range = document.caretRangeFromPoint(e.clientX, e.clientY);
            if (range) {
                const sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(range);
            }
        } else if (document.caretPositionFromPoint) {
            const pos = document.caretPositionFromPoint(e.clientX, e.clientY);
            if (pos) {
                const r = document.createRange();
                r.setStart(pos.offsetNode, pos.offset);
                r.collapse(true);
                const sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(r);
            }
        }

        if (fieldName) {
            _editor.insertHTML(
                `<span class="tb-field" contenteditable="false">{{ model.${fieldName} }}</span>&nbsp;`
            );
        } else if (blockType === 'loop') {
            const view = document.getElementById('view-selector').value || 'Items';
            const safeView = escapeHtml(view);
            _editor.insertHTML(`
                <div class="tb-loop">
                    <div class="tb-loop-label">LOOP — ${safeView}</div>
                    {{ for item in model.${safeView} }}<p><!-- drag fields here --></p>{{ end }}
                </div>`);
        } else if (blockType === 'grid') {
            const view = document.getElementById('view-selector').value || 'Items';
            const safeView = escapeHtml(view);
            _editor.insertHTML(`
                <table border="1" style="width:100%;border-collapse:collapse;">
                    <thead><tr><th>Column1</th><th>Column2</th></tr></thead>
                    <tbody>
                    {{ for item in model.${safeView} }}
                    <tr><td>{{ item.Column1 }}</td><td>{{ item.Column2 }}</td></tr>
                    {{ end }}
                    </tbody>
                </table>`);
        }
    }, true);
})();

// Aspect-ratio lock: re-enforce after SunEditor resize-handle drag ends
(function wireImageAspectLock() {
    setTimeout(() => {
        const editable = document.querySelector('.sun-editor-editable');
        if (!editable) return;
        editable.addEventListener('mouseup', () => {
            const resized = editable.querySelector('img[style*="width"]');
            if (!resized || !resized.naturalWidth) return;
            const ratio = resized.naturalWidth / resized.naturalHeight;
            const w = resized.offsetWidth;
            if (w && ratio) resized.style.height = Math.round(w / ratio) + 'px';
        });
    }, 500); // wait for editor DOM to be ready
})();

document.addEventListener('dragstart', (e) => {
    const field = e.target.closest('[data-field]');
    const block = e.target.closest('[data-block]');
    if (field) e.dataTransfer.setData('field-name', field.dataset.field);
    if (block) e.dataTransfer.setData('block-type', block.dataset.block);
});

// ── Field palette — load columns + keyboard Insert ────────────────────────────

async function loadViewColumns(viewName) {
    const palette = document.getElementById('field-palette');
    if (!viewName) {
        _currentColumns = [];
        palette.innerHTML = '<div class="tb-palette-msg">Select a view to see fields</div>';
        return;
    }
    palette.innerHTML = '<div class="tb-palette-msg">Loading…</div>';
    try {
        const res = await fetch(`/Templates/Api/Views/${encodeURIComponent(viewName)}/Columns`);
        if (!res.ok) throw new Error('Failed to load columns');
        const columns = await res.json();
        _currentColumns = columns;
        if (columns.length === 0) {
            palette.innerHTML = '<div class="tb-palette-msg">No columns found</div>';
            return;
        }
        palette.innerHTML = columns.map(c => `
            <div class="palette-field" draggable="true" data-field="${escapeHtml(c.name)}">
                <span class="palette-field-label">${escapeHtml(c.name)}
                    <span class="palette-field-type">${escapeHtml(c.dataType)}</span>
                </span>
                <button type="button" class="palette-insert-btn"
                        aria-label="Insert ${escapeHtml(c.name)} field"
                        data-field="${escapeHtml(c.name)}">Insert</button>
            </div>`).join('');
    } catch {
        _currentColumns = [];
        palette.innerHTML = '<div class="tb-palette-msg tb-palette-msg--error">Failed to load columns</div>';
    }
}

// Keyboard insert — event delegation on the palette container
document.getElementById('field-palette').addEventListener('click', (e) => {
    const btn = e.target.closest('.palette-insert-btn');
    if (!btn || !_editor) return;
    e.stopPropagation();
    _editor.insertHTML(
        `<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(btn.dataset.field)} }}</span>&nbsp;`
    );
    document.querySelector('.sun-editor-editable')?.focus();
    markDirty();
});

// ── Save version ──────────────────────────────────────────────────────────────

async function saveVersion() {
    const btn = document.getElementById('btn-save');
    const errorEl = document.getElementById('save-error');
    errorEl.style.display = 'none';
    btn.disabled = true;
    if (!_editor) {
        errorEl.textContent = 'Editor is still loading — please wait a moment.';
        errorEl.style.display = 'block';
        btn.disabled = false;
        return;
    }
    const body = _editor.getContents();
    try {
        const res = await fetch(`/Templates/${templateId}/SaveVersion`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _csrf
            },
            body: JSON.stringify({
                name: document.getElementById('prop-name').value,
                templateType: document.getElementById('prop-type').value,
                description: document.getElementById('prop-desc').value,
                body,
                changeComment: document.getElementById('save-comment').value
            })
        });
        if (res.ok) {
            const data = await res.json();
            document.getElementById('version-display').textContent = `v${data.versionNumber}`;
            document.getElementById('save-comment').value = '';
            clearDraft();
            markClean();
            showToast('Version saved');
        } else {
            const err = await res.json().catch(() => null);
            errorEl.textContent = err?.message ?? 'Failed to save version.';
            errorEl.style.display = 'block';
        }
    } catch {
        errorEl.textContent = 'Network error — please try again.';
        errorEl.style.display = 'block';
    } finally {
        btn.disabled = false;
    }
}

// ── Version history modal ─────────────────────────────────────────────────────

async function openVersionHistory() {
    const modal = document.getElementById('version-modal');
    const content = document.getElementById('version-history-content');
    const restoreErrorEl = document.getElementById('restore-error');
    content.innerHTML = 'Loading…';
    restoreErrorEl.style.display = 'none';
    modal.classList.add('open');
    trapFocus(modal);
    try {
        const res = await fetch(`/Templates/${templateId}/Versions`);
        content.innerHTML = res.ok
            ? await res.text()
            : '<p style="color:var(--danger)">Failed to load version history.</p>';
    } catch {
        content.innerHTML = '<p style="color:var(--danger)">Network error loading version history.</p>';
    }
}

// Called from _VersionHistory.cshtml inline onclick; btn passed as `this`
async function restoreVersion(btn, versionId, sourceVersionNumber) {
    const restoreErrorEl = document.getElementById('restore-error');
    restoreErrorEl.style.display = 'none';
    btn.disabled = true;
    try {
        const res = await fetch(`/Templates/${templateId}/Restore/${versionId}/${sourceVersionNumber}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': _csrf }
        });
        if (res.ok) {
            clearDraft();
            window.location.reload();
        } else {
            const err = await res.json().catch(() => null);
            restoreErrorEl.textContent = err?.message ?? 'Failed to restore version.';
            restoreErrorEl.style.display = 'block';
            btn.disabled = false;
        }
    } catch {
        restoreErrorEl.textContent = 'Network error — please try again.';
        restoreErrorEl.style.display = 'block';
        btn.disabled = false;
    }
}

// ── Preview modal ─────────────────────────────────────────────────────────────

function openPreview() {
    const modal = document.getElementById('preview-modal');
    modal.classList.add('open');
    trapFocus(modal);
}

async function renderPreview() {
    const btn = document.getElementById('btn-render');
    const errorEl = document.getElementById('preview-error');
    const frameWrap = document.getElementById('preview-frame-wrap');
    errorEl.style.display = 'none';
    btn.disabled = true;
    if (!_editor) {
        errorEl.textContent = 'Editor is still loading — please wait a moment.';
        errorEl.style.display = 'block';
        btn.disabled = false;
        return;
    }
    const body = _editor.getContents();
    const modelJson = document.getElementById('preview-json').value;
    try {
        const res = await fetch(`/Templates/${templateId ?? 0}/Preview`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _csrf
            },
            body: JSON.stringify({ body, modelJson })
        });
        if (res.ok) {
            const { html } = await res.json();
            document.getElementById('preview-frame').srcdoc = html;
            frameWrap.style.display = 'block';
        } else {
            const data = await res.json().catch(() => null);
            errorEl.textContent = data?.message ?? 'Preview failed.';
            errorEl.style.display = 'block';
        }
    } catch {
        errorEl.textContent = 'Network error — please try again.';
        errorEl.style.display = 'block';
    } finally {
        btn.disabled = false;
    }
}

// ── Validate ──────────────────────────────────────────────────────────────────

async function runValidate() {
    const panel = document.getElementById('validate-panel');
    const msgEl = document.getElementById('validate-msg');
    if (!panel || !msgEl) return;
    panel.hidden = true;

    const body = _editor.getContents();
    try {
        const res = await fetch(`/Templates/${templateId}/Validate`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': _csrf
            },
            body: JSON.stringify({ body })
        });
        if (!res.ok) {
            const data = await res.json().catch(() => null);
            msgEl.textContent = data?.message ?? 'Validation request failed.';
            panel.classList.remove('tb-validate-panel--ok');
            panel.classList.add('tb-validate-panel--error');
            panel.hidden = false;
            return;
        }
        const data = await res.json();
        if (data.valid) {
            msgEl.textContent = 'No errors found.';
            panel.classList.remove('tb-validate-panel--error');
            panel.classList.add('tb-validate-panel--ok');
        } else {
            msgEl.textContent = data.message ?? 'Template has errors.';
            panel.classList.remove('tb-validate-panel--ok');
            panel.classList.add('tb-validate-panel--error');
        }
        panel.hidden = false;
    } catch {
        msgEl.textContent = 'Network error — please try again.';
        panel.classList.remove('tb-validate-panel--ok');
        panel.classList.add('tb-validate-panel--error');
        panel.hidden = false;
    }
}

// ── Loop wizard modal ─────────────────────────────────────────────────────────

function openLoopWizard() {
    const modal = document.getElementById('loop-modal');
    // Populate datalist from _currentColumns
    const dl = document.getElementById('loop-collection-list');
    dl.innerHTML = _currentColumns.map(c => `<option value="${escapeHtml(c.name)}">`).join('');
    // Reset fields
    document.getElementById('loop-collection').value = '';
    document.getElementById('loop-alias').value = 'item';
    const emptyRadio = document.querySelector('input[name="loop-starter"][value="empty"]');
    if (emptyRadio) emptyRadio.checked = true;
    document.getElementById('loop-error').style.display = 'none';
    modal.classList.add('open');
    trapFocus(modal);
    document.getElementById('loop-collection').focus();
}

// ── Conditional wizard modal ──────────────────────────────────────────────────

function openConditionalWizard() {
    const modal = document.getElementById('conditional-modal');
    // Populate datalist from _currentColumns using model.X prefix
    const dl = document.getElementById('cond-field-list');
    dl.innerHTML = _currentColumns.map(c => `<option value="model.${escapeHtml(c.name)}">`).join('');
    // Reset fields
    document.getElementById('cond-field').value = '';
    document.getElementById('cond-operator').value = '==';
    document.getElementById('cond-value').value = '';
    document.getElementById('cond-include-else').checked = false;
    document.getElementById('cond-value-row').style.display = '';
    document.getElementById('cond-error').style.display = 'none';
    modal.classList.add('open');
    trapFocus(modal);
    document.getElementById('cond-field').focus();
}

// Wire operator change once (module-level IIFE) to avoid stacking listeners
(function wireCondOperatorChange() {
    document.getElementById('cond-operator')?.addEventListener('change', function() {
        document.getElementById('cond-value-row').style.display =
            this.value === '!= null' ? 'none' : '';
    });
})();

// ── Modal focus trap ──────────────────────────────────────────────────────────

function trapFocus(modal) {
    const sel = 'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

    function getFocusable() { return Array.from(modal.querySelectorAll(sel)); }

    const initial = getFocusable();
    if (initial.length) initial[0].focus();

    function onKeydown(e) {
        if (e.key !== 'Tab') return;
        const focusable = getFocusable();
        if (!focusable.length) return;
        const idx = focusable.indexOf(document.activeElement);
        if (e.shiftKey) {
            if (idx <= 0) { e.preventDefault(); focusable[focusable.length - 1].focus(); }
        } else {
            if (idx >= focusable.length - 1) { e.preventDefault(); focusable[0].focus(); }
        }
    }
    modal._focusTrap = onKeydown;
    modal.addEventListener('keydown', onKeydown);
}

function releaseFocusTrap(modal) {
    if (modal._focusTrap) {
        modal.removeEventListener('keydown', modal._focusTrap);
        delete modal._focusTrap;
    }
}

function closeModal(id) {
    const modal = document.getElementById(id);
    if (!modal) return;
    releaseFocusTrap(modal);
    modal.classList.remove('open');
}

document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    ['version-modal', 'preview-modal', 'loop-modal', 'conditional-modal'].forEach(id => {
        const el = document.getElementById(id);
        if (el?.classList.contains('open')) closeModal(id);
    });
    const fd = document.getElementById('tb-field-dropdown');
    if (fd && !fd.hidden) fd.hidden = true;
    if (window._isFindReplaceOpen?.()) window._closeFindReplace?.();
});

// ── Toast ─────────────────────────────────────────────────────────────────────

function showToast(msg) {
    const toast = document.createElement('div');
    toast.setAttribute('role', 'status');
    toast.textContent = msg;
    Object.assign(toast.style, {
        position: 'fixed', bottom: '1.5rem', right: '1.5rem',
        background: 'var(--accent)', color: 'white',
        padding: '.6rem 1rem', borderRadius: 'var(--radius)',
        fontSize: '.85rem', zIndex: '999', transition: 'opacity .3s'
    });
    document.body.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), 300);
    }, 2500);
}

// ── Word / character count ────────────────────────────────────────────────────

function updateWordCount() {
    const raw = _editor?.getContents() ?? '';
    const stripped = raw
        .replace(/\{\{[\s\S]*?\}\}/g, '')  // remove Scriban tokens
        .replace(/<[^>]+>/g, ' ')           // strip HTML tags
        .replace(/&[a-z#0-9]+;/gi, ' ');   // strip HTML entities
    const words  = stripped.trim() === '' ? 0 : stripped.trim().split(/\s+/).length;
    const chars  = stripped.length;
    const nospace = stripped.replace(/\s/g, '').length;
    const fmt = n => n.toLocaleString();
    const wEl = document.getElementById('wc-words');
    const cEl = document.getElementById('wc-chars');
    const nEl = document.getElementById('wc-nospace');
    if (wEl) wEl.textContent = fmt(words);
    if (cEl) cEl.textContent = fmt(chars);
    if (nEl) nEl.textContent = fmt(nospace);
}

// ── SunEditor UI fixes ────────────────────────────────────────────────────────

// Fix SunEditor v3 code view: inject CSS so the wrapper expands to fill the canvas.
// SunEditor sets .se-code-wrapper to height:65px via its own stylesheet; we need flex:1
// when the parent .se-wrapper has the se-source-view-status class (code view active).
(function fixEditorUI() {
    // 1. Code view: expand wrapper and fix line-numbers column stealing 100% width
    // 2. Font-size input: SunEditor renders it at 172px — shrink to 65px
    // 3. Color swatches: set visible defaults on dark toolbar
    const style = document.createElement('style');
    style.textContent = [
        '.sun-editor .se-wrapper.se-source-view-status .se-code-wrapper{',
        '  flex:1 1 auto!important;height:auto!important;overflow:hidden!important;}',
        '.sun-editor .se-code-wrapper .se-code-view-line{display:none!important;}',
        '.sun-editor .se-code-wrapper .se-code-viewer{',
        '  flex:1 1 auto!important;width:100%!important;min-width:0!important;',
        '  height:100%!important;box-sizing:border-box!important;padding:.75rem!important;',
        '  background:#1e1e1e!important;color:#c9d1d9!important;',
        '  font-family:Consolas,"Courier New",monospace!important;font-size:.82rem!important;}',
        '.__se__font_size{width:65px!important;min-width:0!important;}',
        '.__se__font{width:120px!important;min-width:0!important;}'
    ].join('');
    document.head.appendChild(style);

    setTimeout(() => {
        const fontSwatch = document.querySelector('[data-command="fontColor"] .se-svg-color-helper');
        const bgSwatch   = document.querySelector('[data-command="backgroundColor"] .se-svg-color-helper');
        if (fontSwatch && !fontSwatch.getAttribute('fill')) fontSwatch.setAttribute('fill', '#e2e2f0');
        if (bgSwatch   && !bgSwatch.getAttribute('fill'))   bgSwatch.setAttribute('fill', '#f59e0b');
    }, 300);
})();

// ── Floating table toolbar ────────────────────────────────────────────────────

(function wireTableToolbar() {
    const editable = document.querySelector('.sun-editor-editable');
    if (!editable) return;

    // Create the toolbar element
    const toolbar = document.createElement('div');
    toolbar.id = 'tb-table-toolbar';
    toolbar.setAttribute('aria-label', 'Table tools');
    // Start visibility:hidden (not hidden attr) so offsetHeight is measurable on first show
    toolbar.style.visibility = 'hidden';
    toolbar.innerHTML = `
        <button type="button" data-tt="addRowBelow"  title="Add row below">+ Row</button>
        <button type="button" data-tt="delRow"       title="Delete row">− Row</button>
        <button type="button" data-tt="addColAfter"  title="Add column after">+ Col</button>
        <button type="button" data-tt="delCol"       title="Delete column">− Col</button>
        <span class="tt-sep"></span>
        <button type="button" data-tt="toggleHeader" title="Toggle header row">Header</button>
        <span class="tt-sep"></span>
        <button type="button" data-tt="valignTop"    title="Align top">↑</button>
        <button type="button" data-tt="valignMid"    title="Align middle">↕</button>
        <button type="button" data-tt="valignBot"    title="Align bottom">↓</button>
        <span class="tt-sep"></span>
        <div class="tt-style-wrap">
            <button type="button" data-tt="styleToggle" title="Table style">Style ▾</button>
            <div class="tt-style-menu" hidden>
                <button type="button" data-ts="tb-table--striped">Striped</button>
                <button type="button" data-ts="tb-table--compact">Compact</button>
                <button type="button" data-ts="tb-table--bordered">Bordered</button>
            </div>
        </div>`;
    document.body.appendChild(toolbar);

    let _activeCell = null;
    let _activeTable = null;
    let _positioned = false;

    function getCell(node) {
        return node?.closest('td, th');
    }

    function showToolbar(cell) {
        _activeCell = cell;
        _activeTable = cell.closest('table');

        if (!_positioned) {
            // First show: element is visibility:hidden but has layout — measure it
            toolbar.style.visibility = 'hidden';
            toolbar.hidden = false;
            _positioned = true;
        }

        const rect = _activeTable.getBoundingClientRect();
        const toolbarH = toolbar.offsetHeight || 32;
        toolbar.style.top  = (rect.top + window.scrollY - toolbarH - 6) + 'px';
        toolbar.style.left = (rect.left + window.scrollX) + 'px';
        toolbar.style.visibility = 'visible';
        toolbar.hidden = false;
    }

    function hideToolbar() {
        toolbar.hidden = true;
        toolbar.style.visibility = 'hidden';
        _activeCell = null;
        _activeTable = null;
        toolbar.querySelector('.tt-style-menu').hidden = true;
    }

    // Show on click inside a table cell
    editable.addEventListener('mousedown', (e) => {
        const cell = getCell(e.target);
        if (cell) { showToolbar(cell); }
        else if (!toolbar.contains(e.target)) { hideToolbar(); }
    });

    // Table operations
    toolbar.addEventListener('click', (e) => {
        const action = e.target.closest('[data-tt]')?.dataset.tt;
        const styleClass = e.target.closest('[data-ts]')?.dataset.ts;

        if (!action && !styleClass) return;
        if (!_activeCell || !_activeTable) return;

        if (action === 'styleToggle') {
            toolbar.querySelector('.tt-style-menu').hidden =
                !toolbar.querySelector('.tt-style-menu').hidden;
            return;
        }

        if (styleClass) {
            const classes = ['tb-table--striped', 'tb-table--compact', 'tb-table--bordered'];
            classes.forEach(c => _activeTable.classList.remove(c));
            _activeTable.classList.add(styleClass);
            toolbar.querySelector('.tt-style-menu').hidden = true;
            markDirty();
            return;
        }

        const row = _activeCell.closest('tr');
        const rowIndex = row.rowIndex;  // 0-based in the table
        const cellIndex = _activeCell.cellIndex;

        if (action === 'addRowBelow') {
            const newRow = _activeTable.insertRow(rowIndex + 1);
            const colCount = row.cells.length;
            for (let i = 0; i < colCount; i++) {
                const td = newRow.insertCell(i);
                td.innerHTML = '&nbsp;';
            }
        } else if (action === 'delRow') {
            if (_activeTable.rows.length > 1) _activeTable.deleteRow(rowIndex);
        } else if (action === 'addColAfter') {
            Array.from(_activeTable.rows).forEach(r => {
                const td = r.insertCell(cellIndex + 1);
                td.innerHTML = '&nbsp;';
            });
        } else if (action === 'delCol') {
            if (_activeTable.rows[0].cells.length > 1) {
                Array.from(_activeTable.rows).forEach(r => r.deleteCell(cellIndex));
            }
        } else if (action === 'toggleHeader') {
            const firstRow = _activeTable.rows[0];
            const isHeader = firstRow.cells[0].tagName === 'TH';
            Array.from(firstRow.cells).forEach(cell => {
                const newCell = document.createElement(isHeader ? 'td' : 'th');
                newCell.innerHTML = cell.innerHTML;
                Array.from(cell.attributes).forEach(a => newCell.setAttribute(a.name, a.value));
                cell.replaceWith(newCell);
            });
            // Wrap/unwrap in thead
            if (!isHeader) {
                const thead = document.createElement('thead');
                thead.appendChild(firstRow);
                _activeTable.insertBefore(thead, _activeTable.firstChild);
            } else {
                const thead = _activeTable.querySelector('thead');
                if (thead) {
                    _activeTable.insertBefore(firstRow, _activeTable.firstChild);
                    thead.remove();
                }
            }
        } else if (action === 'valignTop') {
            _activeCell.style.verticalAlign = 'top';
        } else if (action === 'valignMid') {
            _activeCell.style.verticalAlign = 'middle';
        } else if (action === 'valignBot') {
            _activeCell.style.verticalAlign = 'bottom';
        }

        markDirty();
    });

    // Hide when clicking outside both editor and toolbar
    document.addEventListener('mousedown', (e) => {
        if (toolbar.hidden) return;
        if (!editable.contains(e.target) && !toolbar.contains(e.target)) {
            hideToolbar();
        }
    });
})();

// ── Insert Field dropdown ─────────────────────────────────────────────────────

(function wireFieldDropdown() {
    const dropdown = document.createElement('div');
    dropdown.id = 'tb-field-dropdown';
    dropdown.hidden = true;
    dropdown.innerHTML = `
        <input type="search" id="tb-field-search" placeholder="Search fields…" autocomplete="off">
        <div id="tb-field-list"></div>`;
    document.body.appendChild(dropdown);

    function openDropdown() {
        const btn = document.querySelector('[data-command="insertField"]') ??
                    document.querySelector('[title="Insert Field"]');
        if (btn) {
            const r = btn.getBoundingClientRect();
            dropdown.style.top  = (r.bottom + window.scrollY + 4) + 'px';
            dropdown.style.left = (r.left + window.scrollX) + 'px';
            // Clamp to viewport right edge
            const dropW = 220;
            const maxLeft = window.innerWidth + window.scrollX - dropW - 4;
            if (parseFloat(dropdown.style.left) > maxLeft) {
                dropdown.style.left = maxLeft + 'px';
            }
        }
        renderFieldList('');
        dropdown.hidden = false;
        document.getElementById('tb-field-search').value = '';
        document.getElementById('tb-field-search').focus();
    }

    function closeDropdown() {
        dropdown.hidden = true;
    }

    function renderFieldList(query) {
        const list = document.getElementById('tb-field-list');
        const cols = query
            ? _currentColumns.filter(c => c.name.toLowerCase().includes(query.toLowerCase()))
            : _currentColumns;
        list.innerHTML = cols.map(c =>
            `<div class="tb-field-item" data-field="${escapeHtml(c.name)}" tabindex="0">
                ${escapeHtml(c.name)}<span class="tb-field-item-type">${escapeHtml(c.dataType)}</span>
             </div>`
        ).join('') || '<div style="padding:.25rem .5rem;font-size:.78rem;color:var(--text-muted)">No fields found</div>';
    }

    function insertFieldToken(fieldName) {
        if (!_editor) return;
        _editor.insertHTML(
            `<span class="tb-field" contenteditable="false">{{ model.${escapeHtml(fieldName)} }}</span>&nbsp;`
        );
        document.querySelector('.sun-editor-editable')?.focus();
        markDirty();
        closeDropdown();
    }

    document.getElementById('tb-field-search').addEventListener('input', (e) => {
        renderFieldList(e.target.value);
    });

    document.body.addEventListener('click', (e) => {
        const item = e.target.closest('.tb-field-item');
        if (item && !dropdown.hidden) {
            insertFieldToken(item.dataset.field);
            return;
        }
        if (!dropdown.hidden && !dropdown.contains(e.target)) {
            const btn = e.target.closest('[data-command="insertField"], [title="Insert Field"]');
            if (!btn) closeDropdown();
        }
    });

    dropdown.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') { closeDropdown(); document.querySelector('.sun-editor-editable')?.focus(); return; }
        const items = Array.from(dropdown.querySelectorAll('.tb-field-item'));
        const idx = items.indexOf(document.activeElement);
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            items[idx + 1 < items.length ? idx + 1 : 0]?.focus();
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            items[idx - 1 >= 0 ? idx - 1 : items.length - 1]?.focus();
        } else if (e.key === 'Enter') {
            const focused = dropdown.querySelector('.tb-field-item:focus');
            if (focused) insertFieldToken(focused.dataset.field);
        } else if (e.key === 'Tab' && document.activeElement.id === 'tb-field-search') {
            e.preventDefault();
            items[0]?.focus();
        }
    });

    window.toggleFieldDropdown = function() {
        if (dropdown.hidden) openDropdown();
        else closeDropdown();
    };
})();


// ── Event wiring (replaces inline onclick/onchange attrs) ─────────────────────

document.getElementById('view-selector')?.addEventListener('change', e => loadViewColumns(e.target.value));
document.getElementById('btn-history')?.addEventListener('click', openVersionHistory);
document.getElementById('btn-preview')?.addEventListener('click', openPreview);
document.getElementById('btn-save')?.addEventListener('click', saveVersion);
document.getElementById('btn-render')?.addEventListener('click', renderPreview);
document.getElementById('btn-validate-dismiss')?.addEventListener('click', () => {
    document.getElementById('validate-panel').hidden = true;
});

document.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', () => {
        const overlay = btn.closest('.modal-overlay');
        if (overlay) closeModal(overlay.id);
    });
});

document.getElementById('btn-loop-insert')?.addEventListener('click', () => {
    const collection = document.getElementById('loop-collection').value.trim();
    const alias = document.getElementById('loop-alias').value.trim() || 'item';
    const starter = document.querySelector('input[name="loop-starter"]:checked')?.value ?? 'empty';
    const errorEl = document.getElementById('loop-error');
    errorEl.style.display = 'none';

    if (!collection) {
        errorEl.textContent = 'Collection name is required.';
        errorEl.style.display = 'block';
        document.getElementById('loop-collection').focus();
        return;
    }

    if (!_editor) { errorEl.textContent = 'Editor is still loading.'; errorEl.style.display = 'block'; return; }

    const safeCol   = escapeHtml(collection);
    const safeAlias = escapeHtml(alias);

    let innerHtml = '';
    if (starter === 'list') {
        innerHtml = `<ul><li>{{ ${safeAlias}.FieldName }}</li></ul>`;
    } else if (starter === 'table') {
        innerHtml = `<table border="1" style="width:100%;border-collapse:collapse;"><thead><tr><th>Column1</th></tr></thead><tbody><tr><td>{{ ${safeAlias}.FieldName }}</td></tr></tbody></table>`;
    }

    _editor.insertHTML(
        `<div class="tb-loop"><div class="tb-loop-label" contenteditable="false">LOOP — ${safeCol}</div>` +
        `{{ for ${safeAlias} in model.${safeCol} }}${innerHtml}{{ end }}</div>`
    );
    markDirty();
    closeModal('loop-modal');
    document.querySelector('.sun-editor-editable')?.focus();
});

document.getElementById('btn-cond-insert')?.addEventListener('click', () => {
    const field    = document.getElementById('cond-field').value.trim();
    const operator = document.getElementById('cond-operator').value;
    const value    = document.getElementById('cond-value').value.trim();
    const includeElse = document.getElementById('cond-include-else').checked;
    const errorEl  = document.getElementById('cond-error');
    errorEl.style.display = 'none';

    if (!field) {
        errorEl.textContent = 'Field is required.';
        errorEl.style.display = 'block';
        document.getElementById('cond-field').focus();
        return;
    }

    if (!_editor) {
        errorEl.textContent = 'Editor is still loading.';
        errorEl.style.display = 'block';
        return;
    }

    const safeField = escapeHtml(field);
    const safeValue = escapeHtml(value);

    let condition;
    if (operator === '!= null') {
        condition = `${safeField} != null`;
    } else if (operator === 'contains') {
        condition = `${safeField} | string.contains "${safeValue}"`;
    } else {
        condition = `${safeField} ${operator} "${safeValue}"`;
    }

    let scaffold =
        `{{ if ${condition} }}<p><!-- content here --></p>`;
    if (includeElse) {
        scaffold += `{{ else }}<p><!-- else content --></p>`;
    }
    scaffold += `{{ end }}`;

    _editor.insertHTML(scaffold);
    markDirty();
    closeModal('conditional-modal');
    document.querySelector('.sun-editor-editable')?.focus();
});

// ── Theme toggle ──────────────────────────────────────────────────────────────

function toggleTheme() {
    const isLight = _host.classList.toggle('tb-theme-light');
    localStorage.setItem('tb-theme', isLight ? 'light' : 'dark');
    // Reload so SunEditor reinitialises with the correct toolbar theme
    window.location.reload();
}

(function applyThemeButton() {
    const btn = document.getElementById('btn-theme-toggle');
    if (!btn) return;
    btn.textContent = _theme === 'light' ? '🌙 Dark' : '☀ Light';
    btn.addEventListener('click', toggleTheme);
})();

// Suppress the beforeunload guard when the Create form is submitted normally.
document.getElementById('editor-form')?.addEventListener('submit', () => markClean());

['prop-name', 'prop-type', 'prop-desc', 'save-comment'].forEach(id => {
    const el = document.getElementById(id);
    if (!el) return;
    el.addEventListener('input', markDirty);
    el.addEventListener('change', markDirty);
});

// ── Find & Replace ────────────────────────────────────────────────────────────

(function wireFindReplace() {
    const panel = document.getElementById('find-replace-panel');
    if (!panel) return;

    const findInput    = document.getElementById('fr-find');
    const replaceInput = document.getElementById('fr-replace');
    const matchCount   = document.getElementById('fr-match-count');
    const caseCb       = document.getElementById('fr-case-sensitive');
    const wordCb       = document.getElementById('fr-whole-word');

    let _matches = [];
    let _idx     = -1;
    let _debounce = null;

    // Walk editable text nodes, skipping contenteditable="false" subtrees
    function getTextNodes() {
        const editable = document.querySelector('.sun-editor-editable');
        if (!editable) return [];
        const nodes = [];
        const walker = document.createTreeWalker(editable, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                let p = node.parentElement;
                while (p && p !== editable) {
                    if (p.getAttribute('contenteditable') === 'false') return NodeFilter.FILTER_REJECT;
                    p = p.parentElement;
                }
                return node.nodeValue.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
            }
        });
        let n;
        while ((n = walker.nextNode())) nodes.push(n);
        return nodes;
    }

    function buildRegex() {
        const q = findInput.value;
        if (!q) return null;
        try {
            let pat = q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            if (wordCb.checked) pat = `\\b${pat}\\b`;
            return new RegExp(pat, caseCb.checked ? 'g' : 'gi');
        } catch { return null; }
    }

    function clearMarks() {
        const editable = document.querySelector('.sun-editor-editable');
        if (!editable) return;
        editable.querySelectorAll('mark.tb-find-match').forEach(mk => {
            const p = mk.parentNode;
            if (!p) return;
            while (mk.firstChild) p.insertBefore(mk.firstChild, mk);
            p.removeChild(mk);
        });
        editable.normalize();
        _matches = [];
        _idx = -1;
    }

    function highlightAll(regex) {
        const allMarks = [];
        getTextNodes().forEach(textNode => {
            const text = textNode.nodeValue;
            regex.lastIndex = 0;
            const local = [];
            let m;
            while ((m = regex.exec(text)) !== null) local.push([m.index, regex.lastIndex, m[0]]);
            if (!local.length) return;

            const frag = document.createDocumentFragment();
            let last = 0;
            local.forEach(([start, end, matched]) => {
                if (start > last) frag.appendChild(document.createTextNode(text.slice(last, start)));
                const mk = document.createElement('mark');
                mk.className = 'tb-find-match';
                mk.textContent = matched;
                frag.appendChild(mk);
                allMarks.push(mk);
                last = end;
            });
            if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
            textNode.parentNode.replaceChild(frag, textNode);
        });
        return allMarks;
    }

    function updateCount() {
        if (!matchCount) return;
        if (!_matches.length) {
            matchCount.textContent = findInput.value ? 'No matches' : '';
            matchCount.className   = findInput.value ? 'tb-fr-count tb-fr-count--none' : 'tb-fr-count';
        } else {
            matchCount.textContent = `${_idx + 1} of ${_matches.length}`;
            matchCount.className   = 'tb-fr-count';
        }
    }

    function activate(index) {
        _matches.forEach((mk, i) => mk.classList.toggle('tb-find-match--active', i === index));
        _idx = index;
        _matches[index]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        updateCount();
    }

    function runSearch() {
        clearMarks();
        const regex = buildRegex();
        if (!regex) { updateCount(); return; }
        _matches = highlightAll(regex);
        if (_matches.length) activate(0);
        else updateCount();
    }

    function navigate(dir) {
        if (!_matches.length) return;
        activate((_idx + dir + _matches.length) % _matches.length);
    }

    function replaceCurrent() {
        if (_idx < 0 || !_matches[_idx]?.parentNode) return;
        _matches[_idx].parentNode.replaceChild(document.createTextNode(replaceInput.value), _matches[_idx]);
        document.querySelector('.sun-editor-editable')?.normalize();
        markDirty();
        runSearch();
    }

    function replaceAll() {
        const count = _matches.length;
        if (!count) return;
        const val = replaceInput.value;
        _matches.forEach(mk => { if (mk.parentNode) mk.parentNode.replaceChild(document.createTextNode(val), mk); });
        document.querySelector('.sun-editor-editable')?.normalize();
        _matches = []; _idx = -1;
        updateCount();
        markDirty();
        showToast(`Replaced ${count} occurrence${count === 1 ? '' : 's'}`);
    }

    function openFindReplace() {
        panel.hidden = false;
        findInput.focus();
        findInput.select();
        if (findInput.value) runSearch();
    }

    function closeFindReplace() {
        clearMarks();
        panel.hidden = true;
        document.querySelector('.sun-editor-editable')?.focus();
    }

    // Input events
    findInput.addEventListener('input', () => {
        clearTimeout(_debounce);
        _debounce = setTimeout(runSearch, 220);
    });
    findInput.addEventListener('keydown', e => {
        if (e.key === 'Enter') { e.preventDefault(); navigate(e.shiftKey ? -1 : 1); }
    });
    [caseCb, wordCb].forEach(cb => cb?.addEventListener('change', runSearch));

    // Button events
    document.getElementById('btn-fr-close')?.addEventListener('click', closeFindReplace);
    document.getElementById('btn-fr-prev')?.addEventListener('click', () => navigate(-1));
    document.getElementById('btn-fr-next')?.addEventListener('click', () => navigate(1));
    document.getElementById('btn-fr-replace')?.addEventListener('click', replaceCurrent);
    document.getElementById('btn-fr-replace-all')?.addEventListener('click', replaceAll);

    // Global Ctrl+H shortcut
    document.addEventListener('keydown', e => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'h') {
            e.preventDefault();
            openFindReplace();
        }
    });

    // Expose to toolbar plugin + Escape handler
    window._openFindReplace   = openFindReplace;
    window._closeFindReplace  = closeFindReplace;
    window._isFindReplaceOpen = () => !panel.hidden;
})();

// ── Auto-save draft (localStorage only — no server writes) ───────────────────

const DRAFT_KEY         = `tb-draft-${templateId}`;
const AUTOSAVE_PREF_KEY = 'tb-autosave-enabled';
const AUTOSAVE_INTERVAL = 60_000;

function isAutoSaveEnabled() {
    return localStorage.getItem(AUTOSAVE_PREF_KEY) !== 'false';
}

function updateAutoSaveToggle() {
    const btn = document.getElementById('btn-autosave-toggle');
    if (!btn) return;
    const on = isAutoSaveEnabled();
    btn.textContent = on ? '⏳ Auto-save: ON' : '⏳ Auto-save: OFF';
    btn.title = on ? 'Auto-save is on — click to disable' : 'Auto-save is off — click to enable';
    btn.classList.toggle('tb-autosave-on',  on);
    btn.classList.toggle('tb-autosave-off', !on);
}

function updateDraftStatus() {
    const el = document.getElementById('wc-draft-status');
    if (!el) return;
    const raw = localStorage.getItem(DRAFT_KEY);
    if (!raw) { el.textContent = ''; return; }
    try {
        const { timestamp } = JSON.parse(raw);
        const mins = Math.round((Date.now() - timestamp) / 60000);
        el.textContent = mins < 1 ? 'Draft saved just now' : `Draft saved ${mins}m ago`;
    } catch { el.textContent = ''; }
}

function clearDraft() {
    localStorage.removeItem(DRAFT_KEY);
    const el = document.getElementById('wc-draft-status');
    if (el) el.textContent = '';
}

function saveDraft() {
    if (!_isDirty || !isAutoSaveEnabled() || !_editor) return;
    try {
        localStorage.setItem(DRAFT_KEY, JSON.stringify({
            body: _editor.getContents(),
            timestamp: Date.now(),
            versionNumber: currentVersionNumber
        }));
        updateDraftStatus();
    } catch { /* storage quota exceeded — silently skip */ }
}

function loadDraft() {
    if (templateId === null) return;
    const raw = localStorage.getItem(DRAFT_KEY);
    if (!raw) return;
    try {
        const draft = JSON.parse(raw);
        if (draft.versionNumber !== currentVersionNumber) { clearDraft(); return; }
        const banner = document.getElementById('tb-draft-banner');
        if (!banner) return;
        const ageEl = document.getElementById('draft-age');
        if (ageEl) {
            const mins = Math.round((Date.now() - draft.timestamp) / 60000);
            ageEl.textContent = mins < 1 ? 'just now' : `${mins} min ago`;
        }
        banner.hidden = false;
        document.getElementById('btn-draft-restore')?.addEventListener('click', () => {
            _editor.setContents(draft.body);
            markDirty();
            updateWordCount();
            clearDraft();
            banner.hidden = true;
            showToast('Draft restored — remember to save when ready');
        }, { once: true });
        document.getElementById('btn-draft-discard')?.addEventListener('click', () => {
            clearDraft();
            banner.hidden = true;
        }, { once: true });
    } catch { clearDraft(); }
}

document.getElementById('btn-autosave-toggle')?.addEventListener('click', () => {
    localStorage.setItem(AUTOSAVE_PREF_KEY, isAutoSaveEnabled() ? 'false' : 'true');
    updateAutoSaveToggle();
    showToast(isAutoSaveEnabled() ? 'Auto-save enabled' : 'Auto-save disabled');
});

updateAutoSaveToggle();
setInterval(saveDraft, AUTOSAVE_INTERVAL);
setTimeout(loadDraft, 500);

// Initialize word count once SunEditor has rendered its content
setTimeout(updateWordCount, 400);

