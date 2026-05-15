const _csrf = document.querySelector('input[name=__RequestVerificationToken]')?.value ?? '';

function escapeHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ── Unsaved-change tracking ───────────────────────────────────────────────────

let _isDirty = false;
function markDirty() { _isDirty = true; }
function markClean() { _isDirty = false; }

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
        document.execCommand('formatBlock', false, 'blockquote');
    }
};
SUNEDITOR.plugins.blockquote = blockquotePlugin;

const pageBreakPlugin = {
    name: 'pageBreak',
    display: 'command',
    title: 'Page Break',
    innerHTML: '<span style="font-size:.7rem;letter-spacing:.03em;">PG↵</span>',
    add: function(core) {},
    action: function() {
        _editor.$.html.insert('<div class="tb-page-break" contenteditable="false">— Page Break —</div>');
    }
};
SUNEDITOR.plugins.pageBreak = pageBreakPlugin;

function makeHrPlugin(name, title, style) {
    return {
        name,
        display: 'command',
        title,
        innerHTML: `<hr style="${style};width:16px;display:inline-block;vertical-align:middle;margin:0;">`,
        add: function(core) {},
        action: function() {
            _editor.$.html.insert(`<hr class="tb-hr tb-hr--${name.replace('hr','')}">`, false, true);
        }
    };
}
SUNEDITOR.plugins.hrThin   = makeHrPlugin('hrThin',   'Thin Rule',   'border:none;border-top:1px solid');
SUNEDITOR.plugins.hrThick  = makeHrPlugin('hrThick',  'Thick Rule',  'border:none;border-top:3px solid');
SUNEDITOR.plugins.hrSpaced = makeHrPlugin('hrSpaced', 'Spaced Rule', 'border:none;border-top:1px dashed');

_editor = SUNEDITOR.create(document.getElementById('template-body'), {
    plugins: {
        list:            SUNEDITOR.plugins.list,
        table:           SUNEDITOR.plugins.table,
        link:            SUNEDITOR.plugins.link,
        blockStyle:      SUNEDITOR.plugins.blockStyle,
        align:           SUNEDITOR.plugins.align,
        fontSize:        SUNEDITOR.plugins.fontSize,
        fontColor:       SUNEDITOR.plugins.fontColor,
        backgroundColor: SUNEDITOR.plugins.backgroundColor,
        image:           SUNEDITOR.plugins.image,
        subscript:       SUNEDITOR.plugins.subscript,
        superscript:     SUNEDITOR.plugins.superscript,
        blockquote:      SUNEDITOR.plugins.blockquote,
        pageBreak:       SUNEDITOR.plugins.pageBreak,
        hrThin:          SUNEDITOR.plugins.hrThin,
        hrThick:         SUNEDITOR.plugins.hrThick,
        hrSpaced:        SUNEDITOR.plugins.hrSpaced,
    },
    height: '100%',
    theme: 'dark',
    buttonList: [
        ['undo', 'redo'],
        ['bold', 'italic', 'underline', 'strike'],
        ['subscript', 'superscript'],
        ['blockStyle', 'fontSize'],
        ['fontColor', 'backgroundColor'],
        ['align'],
        ['list', 'hrThin', 'hrThick', 'hrSpaced'],
        ['pageBreak'],
        ['link', 'table', 'image'],
        ['blockquote', 'removeFormat'],
        ['codeView'],
    ],
    fontSize: [10, 12, 14, 16, 18, 20, 24, 28, 32, 36],
    addTagsWhitelist: 'span|div|img|hr|blockquote',
    attributesWhitelist: {
        span:  'class|style|contenteditable',
        div:   'class|style',
        img:   'src|alt|width|height|style',
        table: 'border|cellpadding|cellspacing|style|class',
        tr:    'style|class',
        td:    'style|class|contenteditable|colspan|rowspan',
        th:    'style|class|contenteditable|colspan|rowspan',
        all:   'data-*'
    },
    onChange: markDirty
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
            _editor.$.html.insert(
                `<span class="tb-field" contenteditable="false">{{ model.${fieldName} }}</span>&nbsp;`
            );
        } else if (blockType === 'loop') {
            const view = document.getElementById('view-selector').value || 'Items';
            _editor.$.html.insert(`
                <div class="tb-loop">
                    <div class="tb-loop-label">LOOP — ${view}</div>
                    {{ for item in model.${view} }}<p><!-- drag fields here --></p>{{ end }}
                </div>`);
        } else if (blockType === 'grid') {
            const view = document.getElementById('view-selector').value || 'Items';
            _editor.$.html.insert(`
                <table border="1" style="width:100%;border-collapse:collapse;">
                    <thead><tr><th>Column1</th><th>Column2</th></tr></thead>
                    <tbody>
                    {{ for item in model.${view} }}
                    <tr><td>{{ item.Column1 }}</td><td>{{ item.Column2 }}</td></tr>
                    {{ end }}
                    </tbody>
                </table>`);
        }
    }, true);
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
        palette.innerHTML = '<div class="tb-palette-msg">Select a view to see fields</div>';
        return;
    }
    palette.innerHTML = '<div class="tb-palette-msg">Loading…</div>';
    try {
        const res = await fetch(`/Templates/Api/Views/${encodeURIComponent(viewName)}/Columns`);
        if (!res.ok) throw new Error('Failed to load columns');
        const columns = await res.json();
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
        palette.innerHTML = '<div class="tb-palette-msg tb-palette-msg--error">Failed to load columns</div>';
    }
}

// Keyboard insert — event delegation on the palette container
document.getElementById('field-palette').addEventListener('click', (e) => {
    const btn = e.target.closest('.palette-insert-btn');
    if (!btn || !_editor) return;
    e.stopPropagation();
    _editor.$.html.insert(
        `<span class="tb-field" contenteditable="false">{{ model.${btn.dataset.field} }}</span>&nbsp;`
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
    const body = _editor.$.html.get();
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
    const body = _editor.$.html.get();
    const modelJson = document.getElementById('preview-json').value;
    try {
        const res = await fetch(`/Templates/${templateId}/Preview`, {
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
    ['version-modal', 'preview-modal'].forEach(id => {
        const el = document.getElementById(id);
        if (el?.classList.contains('open')) closeModal(id);
    });
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
        '.__se__font_size{width:65px!important;min-width:0!important;}'
    ].join('');
    document.head.appendChild(style);

    setTimeout(() => {
        const fontSwatch = document.querySelector('[data-command="fontColor"] .se-svg-color-helper');
        const bgSwatch   = document.querySelector('[data-command="backgroundColor"] .se-svg-color-helper');
        if (fontSwatch && !fontSwatch.getAttribute('fill')) fontSwatch.setAttribute('fill', '#e2e2f0');
        if (bgSwatch   && !bgSwatch.getAttribute('fill'))   bgSwatch.setAttribute('fill', '#f59e0b');
    }, 300);
})();

// ── Event wiring (replaces inline onclick/onchange attrs) ─────────────────────

document.getElementById('view-selector')?.addEventListener('change', e => loadViewColumns(e.target.value));
document.getElementById('btn-history')?.addEventListener('click', openVersionHistory);
document.getElementById('btn-preview')?.addEventListener('click', openPreview);
document.getElementById('btn-save')?.addEventListener('click', saveVersion);
document.getElementById('btn-render')?.addEventListener('click', renderPreview);

document.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', () => {
        const overlay = btn.closest('.modal-overlay');
        if (overlay) closeModal(overlay.id);
    });
});

['prop-name', 'prop-type', 'prop-desc', 'save-comment'].forEach(id => {
    const el = document.getElementById(id);
    if (!el) return;
    el.addEventListener('input', markDirty);
    el.addEventListener('change', markDirty);
});
