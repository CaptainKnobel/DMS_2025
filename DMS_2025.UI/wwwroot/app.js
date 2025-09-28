(function () {
    // const API_BASE = '/api/v1';  // (nginx/compose)
    // const API_BASE = 'http://localhost:5062/api/v1'; // (local no-proxy, no nginx)
    const API_BASE = '/api/v1'; // change to local no-proxy if NOT using nginx

    const form = document.getElementById('uploadForm');
    const statusEl = document.getElementById('status');

    function setError(name, msg) {
        const el = document.querySelector(`[data-error-for="${name}"]`);
        if (el) el.textContent = msg || '';
    }
    function clearErrors() {
        ['file', 'title', 'tags'].forEach(n => setError(n, ''));
        statusEl.textContent = '';
        statusEl.className = '';
    }
    function parseTags(raw) {
        if (!raw) return [];
        return raw.split(',').map(s => s.trim()).filter(Boolean);
    }

    function clientValidate(file, title, tags) {
        if (!file) return { field: 'file', message: 'File is required.' };
        if (file.size <= 0 || file.size > 20 * 1024 * 1024)
            return { field: 'file', message: 'File must be >0B and ≤ 20MB.' };

        const allowed = ['application/pdf', 'image/png', 'image/jpeg', 'image/tiff'];
        if (!allowed.includes(file.type))
            return { field: 'file', message: 'Only PDF/PNG/JPG/TIFF allowed.' };

        if (title && title.length > 200)
            return { field: 'title', message: 'Max 200 characters.' };

        for (const t of tags) {
            if (t.length === 0) return { field: 'tags', message: 'Tag cannot be empty.' };
            if (t.length > 50) return { field: 'tags', message: 'Each tag ≤ 50 characters.' };
        }
        return null;
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        clearErrors();

        const file = document.getElementById('file').files[0];
        const title = document.getElementById('title').value?.trim();
        const tags = parseTags(document.getElementById('tags').value);

        const err = clientValidate(file, title, tags);
        if (err) { setError(err.field, err.message); return; }

        const fd = new FormData();
        fd.append('file', file);
        if (title) fd.append('title', title);
        for (const t of tags) fd.append('tags', t);

        try {
            statusEl.textContent = 'Uploading…';
            statusEl.className = 'muted';

            const res = await fetch(`${API_BASE}/documents`, {
                method: 'POST',
                body: fd
            });

            if (res.ok) {
                statusEl.textContent = 'Upload successful.';
                statusEl.className = 'ok';
                form.reset();
                return;
            }

            const ct = res.headers.get('content-type') || '';
            if (ct.includes('application/problem+json') || ct.includes('application/json')) {
                const problem = await res.json();
                if (problem && problem.errors) {
                    for (const [field, msgs] of Object.entries(problem.errors)) {
                        setError(field.toLowerCase(), Array.isArray(msgs) ? msgs[0] : String(msgs));
                    }
                    statusEl.textContent = 'Please check your input.';
                    statusEl.className = '';
                    return;
                }
            }

            statusEl.textContent = `Error: ${res.status} ${res.statusText}`;
            statusEl.className = '';
        } catch (ex) {
            console.error(ex);
            statusEl.textContent = 'Network error — please try again.';
            statusEl.className = '';
        }
    });
})();
