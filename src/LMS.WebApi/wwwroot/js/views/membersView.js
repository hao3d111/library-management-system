import { apiRequest, escapeHtml, formatDate, openModal, closeModal, showToast } from '../api.js';

export async function renderMembersView(container, topbarActions) {
  topbarActions.innerHTML = `<button id="add-member-btn" class="btn btn-primary">+ Register Member</button>`;
  topbarActions.querySelector('#add-member-btn').addEventListener('click', () => openMemberModal(null, loadMembers));

  container.innerHTML = `
    <div class="card">
      <div class="toolbar">
        <form id="member-search-form" class="search-bar">
          <input id="member-search-input" class="form-control" type="search" placeholder="Search by name, code, username, or email..." />
          <button type="submit" class="btn btn-primary">Search</button>
          <button type="button" id="member-reset-btn" class="btn btn-secondary">Reset</button>
        </form>
      </div>
      <div id="members-region">Loading members...</div>
    </div>
  `;

  const searchInput = container.querySelector('#member-search-input');
  container.querySelector('#member-search-form').addEventListener('submit', (e) => {
    e.preventDefault();
    loadMembers();
  });
  container.querySelector('#member-reset-btn').addEventListener('click', () => {
    searchInput.value = '';
    loadMembers();
  });

  async function loadMembers() {
    const region = container.querySelector('#members-region');
    const q = searchInput.value.trim();
    try {
      const members = await apiRequest(`/api/members${q ? `?search=${encodeURIComponent(q)}` : ''}`);
      if (!members.length) {
        region.innerHTML = '<div class="empty-state"><h3>No members found</h3><p>Try a different search query or register a new member.</p></div>';
        return;
      }

      region.innerHTML = `
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Member</th>
                <th>Contact</th>
                <th>Active Loans</th>
                <th>Status</th>
                <th>Joined</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${members.map((m) => `
                <tr>
                  <td><code>${escapeHtml(m.membershipCode)}</code></td>
                  <td>
                    <strong>${escapeHtml(m.fullName)}</strong>
                    <div style="font-size:12px;color:var(--ink-muted);">@${escapeHtml(m.username)}</div>
                  </td>
                  <td>
                    <div>${escapeHtml(m.email)}</div>
                    <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(m.phone || '—')}</div>
                  </td>
                  <td><span class="badge badge-info">${m.activeBorrowingsCount}</span></td>
                  <td>${m.isActive ? '<span class="badge badge-success">Active</span>' : '<span class="badge badge-neutral">Inactive</span>'}</td>
                  <td>${formatDate(m.createdAt)}</td>
                  <td>
                    <button class="btn btn-secondary btn-sm" data-edit-id="${m.id}">Edit</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-edit-id]').forEach((btn) => {
        btn.addEventListener('click', () => {
          const member = members.find((x) => x.id === Number(btn.dataset.editId));
          openMemberModal(member, loadMembers);
        });
      });
    } catch (err) {
      region.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadMembers();
}

function openMemberModal(member, onSaved) {
  const isEdit = Boolean(member);
  const modal = openModal({
    title: isEdit ? `Edit Member (${member.membershipCode})` : 'Register Library Member',
    bodyHtml: `
      <div id="member-modal-error" class="alert alert-error hidden"></div>
      <form id="member-form">
        ${!isEdit ? `
          <div class="form-row">
            <div class="form-group">
              <label for="mf-username">Username *</label>
              <input id="mf-username" class="form-control" required />
            </div>
            <div class="form-group">
              <label for="mf-password">Password (min 6 chars) *</label>
              <input id="mf-password" type="password" class="form-control" required />
            </div>
          </div>
        ` : ''}
        <div class="form-group">
          <label for="mf-fullname">Full Name *</label>
          <input id="mf-fullname" class="form-control" value="${escapeHtml(member?.fullName || '')}" required />
        </div>
        <div class="form-row">
          <div class="form-group">
            <label for="mf-email">Email *</label>
            <input id="mf-email" type="email" class="form-control" value="${escapeHtml(member?.email || '')}" required />
          </div>
          <div class="form-group">
            <label for="mf-phone">Phone</label>
            <input id="mf-phone" class="form-control" value="${escapeHtml(member?.phone || '')}" />
          </div>
        </div>
        ${isEdit ? `
          <div class="form-row">
            <div class="form-group">
              <label for="mf-active">Status</label>
              <select id="mf-active" class="form-control">
                <option value="true" ${member.isActive ? 'selected' : ''}>Active</option>
                <option value="false" ${!member.isActive ? 'selected' : ''}>Inactive</option>
              </select>
            </div>
            <div class="form-group">
              <label for="mf-newpass">Reset Password (optional)</label>
              <input id="mf-newpass" type="password" class="form-control" placeholder="Leave blank to keep" />
            </div>
          </div>
        ` : ''}
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px;">
          <button type="button" class="btn btn-secondary" id="mf-cancel">Cancel</button>
          <button type="submit" class="btn btn-primary">${isEdit ? 'Save Changes' : 'Register Member'}</button>
        </div>
      </form>
    `
  });

  modal.querySelector('#mf-cancel').addEventListener('click', closeModal);
  modal.querySelector('#member-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errBox = modal.querySelector('#member-modal-error');
    errBox.classList.add('hidden');

    const payload = isEdit
      ? {
          fullName: modal.querySelector('#mf-fullname').value.trim(),
          email: modal.querySelector('#mf-email').value.trim(),
          phone: modal.querySelector('#mf-phone').value.trim(),
          isActive: modal.querySelector('#mf-active').value === 'true',
          newPassword: modal.querySelector('#mf-newpass').value || null
        }
      : {
          username: modal.querySelector('#mf-username').value.trim(),
          password: modal.querySelector('#mf-password').value,
          fullName: modal.querySelector('#mf-fullname').value.trim(),
          email: modal.querySelector('#mf-email').value.trim(),
          phone: modal.querySelector('#mf-phone').value.trim()
        };

    try {
      await apiRequest(isEdit ? `/api/members/${member.id}` : '/api/members', {
        method: isEdit ? 'PUT' : 'POST',
        body: JSON.stringify(payload)
      });
      closeModal();
      showToast(isEdit ? 'Member updated.' : 'Member registered.');
      onSaved();
    } catch (err) {
      const details = err.errors ? Object.values(err.errors).flat().join(' ') : '';
      errBox.textContent = details ? `${err.message} ${details}` : err.message;
      errBox.classList.remove('hidden');
    }
  });
}
