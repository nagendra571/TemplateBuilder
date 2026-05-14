const _csrf = document.querySelector('input[name=__RequestVerificationToken]')?.value ?? '';

function escapeHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

tinymce.init({
    selector: '#template-body',
    height: '100%',
    menubar: false,
    plugins: 'lists link table code',
    toolbar: 'bold italic underline | h1 h2 | bullist numlist | link table | code',
    skin: 'oxide-dark',
    content_css: 'dark',
    content_style: `
        .tb-field { background: #3a3aff22; color: #6366f1; border-radius: 3px; padding: 0 4px; font-family: monospace; }
        .tb-loop { border: 2px dashed #f59e0b; border-radius: 6px; padding: 8px; margin: 4px 0; }
        .tb-loop-label { font-size: 11px; color: #f59e0b; margin-bottom: 4px; }
    `,
    setup(editor) {
        editor.on('drop', (e) => handleDrop(e, editor));
    }
});

function handleDrop(e, editor) {
    const fieldName = e.dataTransfer?.getData('field-name');
    const blockType = e.dataTransfer?.getData('block-type');
    if (fieldName) {
        e.preventDefault();
        editor.insertContent(`<span class="tb-field" contenteditable="false">{{ model.${fieldName} }}</span>&nbsp;`);
    } else if (blockType === 'loop') {
        e.preventDefault();
        const view = document.getElementById('view-selector').value || 'Items';
        editor.insertContent(`
            <div class="tb-loop">
                <div class="tb-loop-label">LOOP — ${view}</div>
                {{ for item in model.${view} }}<p><!-- drag fields here --></p>{{ end }}
            </div>`);
    } else if (blockType === 'grid') {
        e.preventDefault();
        const view = document.getElementById('view-selector').value || 'Items';
        editor.insertContent(`
            <table border="1" style="width:100%;border-collapse:collapse;">
                <thead><tr><th>Column1</th><th>Column2</th></tr></thead>
                <tbody>
                {{ for item in model.${view} }}
                <tr><td>{{ item.Column1 }}</td><td>{{ item.Column2 }}</td></tr>
                {{ end }}
                </tbody>
            </table>`);
    }
}

document.addEventListener('dragstart', (e) => {
    const field = e.target.closest('[data-field]');
    const block = e.target.closest('[data-block]');
    if (field) e.dataTransfer.setData('field-name', field.dataset.field);
    if (block) e.dataTransfer.setData('block-type', block.dataset.block);
});

async function loadViewColumns(viewName) {
    const palette = document.getElementById('field-palette');
    if (!viewName) {
        palette.innerHTML = '<div style="color:var(--text-muted);font-size:.78rem;text-align:center;margin-top:1rem;">Select a view to see fields</div>';
        return;
    }
    palette.innerHTML = '<div style="color:var(--text-muted);font-size:.78rem;padding:.5rem;">Loading…</div>';
    try {
        const res = await fetch(`/Templates/Api/Views/${encodeURIComponent(viewName)}/Columns`);
        if (!res.ok) throw new Error('Failed to load columns');
        const columns = await res.json();
        if (columns.length === 0) {
            palette.innerHTML = '<div style="color:var(--text-muted);font-size:.78rem;text-align:center;margin-top:1rem;">No columns found</div>';
            return;
        }
        palette.innerHTML = columns.map(c => `
            <div class="palette-field" draggable="true" data-field="${escapeHtml(c.name)}"
                 style="background:var(--accent);opacity:.85;color:white;border-radius:var(--radius);padding:.25rem .5rem;font-size:.75rem;margin-bottom:.3rem;cursor:grab;user-select:none;">
                ${escapeHtml(c.name)} <span style="opacity:.6;font-size:.68rem;">${escapeHtml(c.dataType)}</span>
            </div>`).join('');
    } catch {
        palette.innerHTML = '<div style="color:var(--danger);font-size:.78rem;padding:.5rem;">Failed to load columns</div>';
    }
}

async function saveVersion() {
    const btn = document.getElementById('btn-save');
    const errorEl = document.getElementById('save-error');
    errorEl.style.display = 'none';
    btn.disabled = true;
    const tinyEditor = tinymce.get('template-body');
    if (!tinyEditor) {
        errorEl.textContent = 'Editor is still loading — please wait a moment.';
        errorEl.style.display = 'block';
        btn.disabled = false;
        return;
    }
    const body = tinyEditor.getContent();
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

async function openVersionHistory() {
    const modal = document.getElementById('version-modal');
    const content = document.getElementById('version-history-content');
    const restoreErrorEl = document.getElementById('restore-error');
    content.innerHTML = 'Loading…';
    restoreErrorEl.style.display = 'none';
    modal.classList.add('open');
    try {
        const res = await fetch(`/Templates/${templateId}/Versions`);
        content.innerHTML = res.ok ? await res.text() : '<p style="color:var(--danger)">Failed to load version history.</p>';
    } catch {
        content.innerHTML = '<p style="color:var(--danger)">Network error loading version history.</p>';
    }
}

async function restoreVersion(versionId) {
    const btn = event.currentTarget;
    const restoreErrorEl = document.getElementById('restore-error');
    restoreErrorEl.style.display = 'none';
    btn.disabled = true;
    try {
        const res = await fetch(`/Templates/${templateId}/Restore/${versionId}`, {
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

function openPreview() {
    document.getElementById('preview-modal').classList.add('open');
}

async function renderPreview() {
    const btn = document.getElementById('btn-render');
    const errorEl = document.getElementById('preview-error');
    const frameWrap = document.getElementById('preview-frame-wrap');
    errorEl.style.display = 'none';
    btn.disabled = true;
    const tinyEditor = tinymce.get('template-body');
    if (!tinyEditor) {
        errorEl.textContent = 'Editor is still loading — please wait a moment.';
        errorEl.style.display = 'block';
        btn.disabled = false;
        return;
    }
    const body = tinyEditor.getContent();
    const modelJson = document.getElementById('preview-json').value;
    try {
        const res = await fetch(`/Templates/${templateId}/Preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
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

function closeModal(id) {
    document.getElementById(id).classList.remove('open');
}

document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    ['version-modal', 'preview-modal'].forEach(id => {
        const el = document.getElementById(id);
        if (el?.classList.contains('open')) closeModal(id);
    });
});

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
