(function () {
    // const API_BASE = '/api/v1';  // (nginx/compose)
    // const API_BASE = 'http://localhost:5062/api/v1'; // (local no-proxy, no nginx)
    const API_BASE = '/api/v1';

    const form = document.getElementById('uploadForm');
    const statusEl = document.getElementById('status');
    const setError = (n, m) => {
        const el = document.querySelector(`[data-error-for="${n}"]`);
        if (el) el.textContent = m || '';
    };
    const clear = () => {
        ['file', 'title', 'location', 'author', 'creationDate'].forEach(n => setError(n, ''));
        statusEl.textContent = '';
        statusEl.className = '';
    };

    function clientValidate(file, dto) {
        if (!file) return { field: 'file', message: 'File is required.' };
        if (file.size <= 0 || file.size > 20 * 1024 * 1024) return { field: 'file', message: 'File must be >0B and ≤ 20MB.' };
        const allowed = ['application/pdf', 'image/png', 'image/jpeg', 'image/tiff'];
        if (!allowed.includes(file.type)) return { field: 'file', message: 'Only PDF/PNG/JPG/TIFF allowed.' };

        const tooLong = (s, max) => s && s.length > max;
        if (tooLong(dto.title, 255)) return { field: 'title', message: 'Max 255 characters.' };
        if (tooLong(dto.location, 255)) return { field: 'location', message: 'Max 255 characters.' };
        if (tooLong(dto.author, 255)) return { field: 'author', message: 'Max 255 characters.' };
        return null;
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault(); clear();

        const file = document.getElementById('file').files[0];
        const dto = {
            title: document.getElementById('title').value?.trim() || '',
            location: document.getElementById('location').value?.trim() || '',
            author: document.getElementById('author').value?.trim() || '',
            creationDate: document.getElementById('creationDate').value || ''
        };

        const err = clientValidate(file, dto);
        if (err) { setError(err.field, err.message); return; }

        const fd = new FormData();
        fd.append('file', file);
        if (dto.title) fd.append('title', dto.title);
        if (dto.location) fd.append('location', dto.location);
        if (dto.author) fd.append('author', dto.author);
        if (dto.creationDate) {
            // send ISO string; server will bind to DateTime?
            const iso = new Date(dto.creationDate).toISOString();
            fd.append('creationDate', iso);
        }

        try {
            statusEl.textContent = 'Uploading…';
            statusEl.className = 'muted';

            const res = await fetch(`${API_BASE}/documents/upload`, {
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
                const problem = await res.json().catch(() => null);
                if (problem?.errors) {
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
