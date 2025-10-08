(() => {
    const API_BASE = (() => {
        const p = window.location;
        const runningBehindNginx = p.port === '8081' || p.pathname.startsWith('/ui/');
        return runningBehindNginx ? '/api/v1' : 'http://localhost:8081/api/v1';
    })();

    // Elements
    const form = document.getElementById('uploadForm');
    const fileInput = document.getElementById('file');
    const drop = document.getElementById('drop');
    const pick = document.getElementById('pick');
    const fileMeta = document.getElementById('fileMeta');
    const bar = document.getElementById('bar');
    const btnUpload = document.getElementById('btnUpload');
    const btnReset = document.getElementById('btnReset');
    const statusEl = document.getElementById('status');
    const alertEl = document.getElementById('alert');

    // Helpers
    const qErr = (n) => document.querySelector(`[data-error-for="${n}"]`);
    const clearFieldErrors = () => {
        ['title', 'location', 'author', 'creationDate'].forEach(id => {
            const el = document.getElementById(id);
            el.classList.remove('is-invalid');
            qErr(id).textContent = '';
        });
        drop.classList.remove('invalid');
        qErr('file').textContent = '';
        hideAlert();
    };
    const setFieldError = (name, message) => {
        if (name === 'file') {
            drop.classList.add('invalid');
            qErr('file').textContent = message || '';
            return;
        }
        const el = document.getElementById(name);
        if (el) el.classList.add('is-invalid');
        qErr(name).textContent = message || '';
    };
    const setStatus = (t) => statusEl.textContent = t || '';
    const setProgress = (p) => bar.style.width = `${Math.max(0, Math.min(100, p))}%`;
    const showAlert = (msg, type = 'danger') => {
        alertEl.className = `alert alert-${type}`;
        alertEl.textContent = msg;
    };
    const hideAlert = () => alertEl.className = 'alert d-none';
    const fmtBytes = n => {
        const u = ['B', 'KB', 'MB', 'GB']; let i = 0;
        while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
        return `${n.toFixed(i ? 1 : 0)} ${u[i]}`;
    };

    // Drag&Drop (minimal)
    const openPicker = (e) => { e?.preventDefault?.(); fileInput.click(); };
    const updateMeta = (file) => {
        fileMeta.textContent = file ? `${file.name} • ${file.type || 'unknown'} • ${fmtBytes(file.size)}` : '';
    };
    ['dragenter', 'dragover'].forEach(ev => drop.addEventListener(ev, e => {
        e.preventDefault(); drop.classList.add('drag');
    }));
    ;['dragleave', 'dragend', 'drop'].forEach(ev => drop.addEventListener(ev, e => {
        e.preventDefault(); drop.classList.remove('drag');
    }));
    drop.addEventListener('drop', e => {
        const f = e.dataTransfer?.files?.[0];
        if (f) { fileInput.files = e.dataTransfer.files; updateMeta(f); }
    });
    drop.addEventListener('click', openPicker);
    pick.addEventListener('click', openPicker);
    fileInput.addEventListener('change', () => updateMeta(fileInput.files[0]));

    // Limit future date (UX)
    const dt = document.getElementById('creationDate');
    if (dt) {
        const now = new Date();
        const pad = n => String(n).padStart(2, '0');
        dt.max = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
    }

    // Client validation
    const allowed = ['application/pdf', 'image/png', 'image/jpeg', 'image/tiff'];
    function validateClient(file, dto) {
        if (!file) return { field: 'file', message: 'File is required.' };
        if (file.size <= 0 || file.size > 20 * 1024 * 1024) return { field: 'file', message: 'File must be > 0 B and ≤ 20 MB.' };
        if (!allowed.includes(file.type)) return { field: 'file', message: 'Only PDF/PNG/JPG/TIFF allowed.' };
        const tooLong = (s, m) => s && s.length > m;
        if (tooLong(dto.title, 255)) return { field: 'title', message: 'Max 255 characters.' };
        if (tooLong(dto.location, 255)) return { field: 'location', message: 'Max 255 characters.' };
        if (tooLong(dto.author, 255)) return { field: 'author', message: 'Max 255 characters.' };
        return null;
    }

    // Submit
    form.addEventListener('submit', (e) => {
        e.preventDefault();
        clearFieldErrors(); hideAlert(); setStatus(''); setProgress(0);

        const file = fileInput.files[0];
        const dto = {
            title: document.getElementById('title').value?.trim() || '',
            location: document.getElementById('location').value?.trim() || '',
            author: document.getElementById('author').value?.trim() || '',
            creationDate: document.getElementById('creationDate').value || ''
        };
        const err = validateClient(file, dto);
        if (err) { setFieldError(err.field, err.message); return; }

        const fd = new FormData();
        fd.append('file', file);
        if (dto.title) fd.append('title', dto.title);
        if (dto.location) fd.append('location', dto.location);
        if (dto.author) fd.append('author', dto.author);
        if (dto.creationDate) fd.append('creationDate', new Date(dto.creationDate).toISOString());

        // XHR für Fortschritt
        const xhr = new XMLHttpRequest();
        xhr.open('POST', `${API_BASE}/documents/upload`, true);

        // UI busy
        btnUpload.disabled = true; btnReset.disabled = true;
        setStatus('Uploading…');
        bar.classList.add('progress-bar-striped', 'progress-bar-animated');

        xhr.upload.onprogress = (ev) => {
            if (ev.lengthComputable) setProgress(Math.round(ev.loaded / ev.total * 100));
        };

        xhr.onreadystatechange = () => {
            if (xhr.readyState !== XMLHttpRequest.DONE) return;

            btnUpload.disabled = false; btnReset.disabled = false;
            bar.classList.remove('progress-bar-striped', 'progress-bar-animated');

            const ok = xhr.status >= 200 && xhr.status < 300;
            if (ok) {
                setProgress(100); setStatus('Upload successful.');
                showAlert('Upload successful.', 'success');
                form.reset(); updateMeta(null);
                loadDocs();
                return;
            }

            // Fehlerbehandlung
            const ct = xhr.getResponseHeader('content-type') || '';
            let problem = null;
            if (ct.includes('json')) { try { problem = JSON.parse(xhr.responseText); } catch { } }

            if (xhr.status === 413) {
                setFieldError('file', 'File too large (server limit).');
                showAlert('413 – Payload too large', 'danger');
                setStatus(''); return;
            }

            if (problem?.errors) {
                Object.entries(problem.errors).forEach(([field, msgs]) => {
                    setFieldError(field.toLowerCase(), Array.isArray(msgs) ? msgs[0] : String(msgs));
                });
                showAlert('Please check your input.', 'warning');
                return;
            }

            showAlert(`Error ${xhr.status} ${xhr.statusText || ''}`, 'danger');
        };

        xhr.onerror = () => {
            btnUpload.disabled = false; btnReset.disabled = false;
            bar.classList.remove('progress-bar-striped', 'progress-bar-animated');
            showAlert('Network error — please try again.', 'danger');
        };

        xhr.send(fd);
    });

    // Reset
    btnReset.addEventListener('click', () => {
        clearFieldErrors(); hideAlert(); setStatus(''); setProgress(0); updateMeta(null);
    });

    // more CRUD stuff
    async function loadDocs() {
        const res = await fetch(`${API_BASE}/documents?page=1&pageSize=50`);
        const rows = await res.json();
        const tbody = document.querySelector('#docsTable tbody');
        tbody.innerHTML = '';
        rows.forEach(doc => {
            const tr = document.createElement('tr');
            const tdTitle = document.createElement('td');
            tdTitle.textContent = doc.title ?? '';
            tr.appendChild(tdTitle);

            const tdAuthor = document.createElement('td');
            tdAuthor.textContent = doc.author ?? '';
            tr.appendChild(tdAuthor);

            const tdCreated = document.createElement('td');
            tdCreated.textContent = doc.creationDate ? new Date(doc.creationDate).toLocaleString() : '';
            tr.appendChild(tdCreated);

            const tdFile = document.createElement('td');
            tdFile.textContent = doc.hasFile
                ? `${doc.originalFileName ?? 'file'} (${((doc.fileSize ?? 0) / 1024) | 0} KB)`
                : '—';
            tr.appendChild(tdFile);

            const tdActions = document.createElement('td');
            tdActions.className = 'text-nowrap';

            const aDl = document.createElement('a');
            aDl.className = 'btn btn-sm btn-outline-primary me-2';
            aDl.href = `${API_BASE}/documents/${doc.id}/file`;
            aDl.target = '_blank';
            aDl.rel = 'noopener';
            aDl.innerHTML = '<i class="bi bi-download"></i> Download';
            if (!doc.hasFile) aDl.classList.add('disabled');
            tdActions.appendChild(aDl);

            const label = document.createElement('label');
            label.className = 'btn btn-sm btn-outline-warning me-2 mb-0';
            label.innerHTML = '<i class="bi bi-arrow-repeat"></i> Replace';
            if (!doc.hasFile) label.classList.add('disabled');
            const input = document.createElement('input');
            input.type = 'file';
            input.className = 'd-none';
            input.addEventListener('change', () => replaceFile(doc.id, input.files[0]));
            label.appendChild(input);
            tdActions.appendChild(label);

            const btnDel = document.createElement('button');
            btnDel.className = 'btn btn-sm btn-outline-danger';
            btnDel.innerHTML = '<i class="bi bi-trash"></i> Delete';
            btnDel.addEventListener('click', () => deleteDoc(doc.id));
            tdActions.appendChild(btnDel);

            tr.appendChild(tdActions);
            tbody.appendChild(tr);
        });
    }

    window.replaceFile = async (id, file) => {
        if (!file) return;
        const fd = new FormData();
        fd.append('file', file);
        const res = await fetch(`${API_BASE}/documents/${id}/file`, { method: 'PUT', body: fd });
        if (res.ok) { loadDocs(); showAlert('File replaced.', 'success'); } else { showAlert('Replace failed.', 'danger'); }
    };

    window.deleteDoc = async (id) => {
        if (!confirm('Delete document?')) return;
        const res = await fetch(`${API_BASE}/documents/${id}`, { method: 'DELETE' });
        if (res.ok) { loadDocs(); showAlert('Deleted.', 'success'); } else { showAlert('Delete failed.', 'danger'); }
    };

    // initial
    loadDocs();


})();
