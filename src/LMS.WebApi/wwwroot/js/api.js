const SESSION_KEY = 'lms_session';

export function getSession() {
  try {
    const raw = localStorage.getItem(SESSION_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function setSession(data) {
  localStorage.setItem(SESSION_KEY, JSON.stringify(data));
}

export function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}

export async function apiRequest(path, options = {}) {
  const session = getSession();
  const headers = {
    'Content-Type': 'application/json',
    ...(options.headers || {})
  };

  if (session?.token && !options.skipAuth) {
    headers['Authorization'] = `Bearer ${session.token}`;
  }

  const response = await fetch(path, {
    ...options,
    headers
  });

  if (response.status === 204) {
    return null;
  }

  let body = null;
  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    body = await response.json();
  }

  if (!response.ok) {
    if (response.status === 401 && !options.skipAuth) {
      clearSession();
      window.location.href = '/index.html';
    }
    const err = new Error(body?.message || `Request failed (${response.status})`);
    err.status = response.status;
    err.errors = body?.errors || null;
    throw err;
  }

  return body;
}

export function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

export function showToast(message, type = 'success') {
  const container = document.getElementById('toast-container');
  if (!container) return;
  const el = document.createElement('div');
  el.className = `toast toast-${type}`;
  el.textContent = message;
  container.appendChild(el);
  setTimeout(() => el.remove(), 3600);
}

export function openModal({ title, bodyHtml, footerHtml = '' }) {
  const root = document.getElementById('modal-root');
  root.innerHTML = `
    <div class="modal-box">
      <div class="modal-header">
        <h2>${escapeHtml(title)}</h2>
        <button type="button" class="btn btn-secondary btn-sm" id="modal-close-btn">Close</button>
      </div>
      <div class="modal-body">${bodyHtml}</div>
      ${footerHtml ? `<div class="modal-footer">${footerHtml}</div>` : ''}
    </div>
  `;
  root.classList.remove('hidden');
  root.querySelector('#modal-close-btn')?.addEventListener('click', closeModal);
  root.onclick = (e) => {
    if (e.target === root) closeModal();
  };
  return root;
}

export function closeModal() {
  const root = document.getElementById('modal-root');
  if (!root) return;
  root.classList.add('hidden');
  root.innerHTML = '';
}
