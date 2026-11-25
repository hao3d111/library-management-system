import { apiRequest, escapeHtml, formatDate, openModal, closeModal, showToast } from '../api.js';

export async function renderUsersView(container, topbarActions) {
  topbarActions.innerHTML = `<button id="add-user-btn" class="btn btn-primary">+ Create User</button>`;
  topbarActions.querySelector('#add-user-btn').addEventListener('click', () => openUserModal(null, loadUsers));

  container.innerHTML = `
    <div class="card">
      <div class="toolbar">
        <form id="user-filter-form" class="search-bar">
          <input id="user-search" class="form-control" type="search" placeholder="Search users..." />
          <select id="user-role-filter" class="form-control" style="max-width:190px;">
            <option value="">All Roles</option>
            <option value="Administrator">Administrator</option>
            <option value="Librarian">Librarian</option>
            <option value="LibraryMember">LibraryMember</option>
          </select>
          <button type="submit" class="btn btn-primary">Filter</button>
        </form>
      </div>
      <div id="users-region">Loading users...</div>
    </div>
  `;

  const searchInput = container.querySelector('#user-search');
  const roleSelect = container.querySelector('#user-role-filter');
  container.querySelector('#user-filter-form').addEventListener('submit', (e) => {
    e.preventDefault();
    loadUsers();
  });

  async function loadUsers() {
    const region = container.querySelector('#users-region');
    const params = new URLSearchParams();
    if (searchInput.value.trim()) params.set('search', searchInput.value.trim());
    if (roleSelect.value) params.set('role', roleSelect.value);

    try {
      const users = await apiRequest(`/api/users${params.toString() ? `?${params}` : ''}`);
      region.innerHTML = `
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Username</th>
                <th>Full Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${users.map((u) => `
                <tr>
                  <td><code>${escapeHtml(u.username)}</code></td>
                  <td><strong>${escapeHtml(u.fullName)}</strong></td>
                  <td>${escapeHtml(u.email)}</td>
                  <td><span class="badge badge-info">${escapeHtml(u.role)}</span></td>
                  <td>${u.isActive ? '<span class="badge badge-success">Active</span>' : '<span class="badge badge-neutral">Inactive</span>'}</td>
                  <td>${formatDate(u.createdAt)}</td>
                  <td class="actions-cell">
                    <button class="btn btn-secondary btn-sm" data-edit-id="${u.id}">Edit</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-edit-id]').forEach((btn) => {
        btn.addEventListener('click', () => {
          const u = users.find((x) => x.id === Number(btn.dataset.editId));
          openUserModal(u, loadUsers);
        });
      });
    } catch (err) {
      region.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadUsers();
}

function openUserModal(user, onSaved) {
  const isEdit = Boolean(user);
  const modal = openModal({
    title: isEdit ? `Edit User (${user.username})` : 'Create User Account',
    bodyHtml: `
      <div id="user-modal-error" class="alert alert-error hidden"></div>
      <form id="user-form">
        ${!isEdit ? `
          <div class="form-row">
            <div class="form-group">
              <label for="uf-username">Username *</label>
              <input id="uf-username" class="form-control" required />
            </div>
            <div class="form-group">
              <label for="uf-password">Password *</label>
              <input id="uf-password" type="password" class="form-control" required />
            </div>
          </div>
        ` : ''}
        <div class="form-row">
          <div class="form-group">
            <label for="uf-fullname">Full Name *</label>
            <input id="uf-fullname" class="form-control" value="${escapeHtml(user?.fullName || '')}" required />
          </div>
          <div class="form-group">
            <label for="uf-role">Role *</label>
            <select id="uf-role" class="form-control">
              <option value="Administrator" ${user?.role === 'Administrator' ? 'selected' : ''}>Administrator</option>
              <option value="Librarian" ${user?.role === 'Librarian' ? 'selected' : ''}>Librarian</option>
              <option value="LibraryMember" ${!user || user?.role === 'LibraryMember' ? 'selected' : ''}>LibraryMember</option>
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label for="uf-email">Email *</label>
            <input id="uf-email" type="email" class="form-control" value="${escapeHtml(user?.email || '')}" required />
          </div>
          <div class="form-group">
            <label for="uf-phone">Phone</label>
            <input id="uf-phone" class="form-control" value="${escapeHtml(user?.phone || '')}" />
          </div>
        </div>
        ${isEdit ? `
          <div class="form-row">
            <div class="form-group">
              <label for="uf-active">Account Status</label>
              <select id="uf-active" class="form-control">
                <option value="true" ${user.isActive ? 'selected' : ''}>Active</option>
                <option value="false" ${!user.isActive ? 'selected' : ''}>Inactive</option>
              </select>
            </div>
            <div class="form-group">
              <label for="uf-newpass">New Password (optional)</label>
              <input id="uf-newpass" type="password" class="form-control" />
            </div>
          </div>
        ` : ''}
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px;">
          <button type="button" class="btn btn-secondary" id="uf-cancel">Cancel</button>
          <button type="submit" class="btn btn-primary">${isEdit ? 'Save Changes' : 'Create User'}</button>
        </div>
      </form>
    `
  });

  modal.querySelector('#uf-cancel').addEventListener('click', closeModal);
  modal.querySelector('#user-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errBox = modal.querySelector('#user-modal-error');
    errBox.classList.add('hidden');

    const payload = isEdit
      ? {
          fullName: modal.querySelector('#uf-fullname').value.trim(),
          email: modal.querySelector('#uf-email').value.trim(),
          phone: modal.querySelector('#uf-phone').value.trim(),
          role: modal.querySelector('#uf-role').value,
          isActive: modal.querySelector('#uf-active').value === 'true',
          newPassword: modal.querySelector('#uf-newpass').value || null
        }
      : {
          username: modal.querySelector('#uf-username').value.trim(),
          password: modal.querySelector('#uf-password').value,
          fullName: modal.querySelector('#uf-fullname').value.trim(),
          email: modal.querySelector('#uf-email').value.trim(),
          phone: modal.querySelector('#uf-phone').value.trim(),
          role: modal.querySelector('#uf-role').value
        };

    try {
      await apiRequest(isEdit ? `/api/users/${user.id}` : '/api/users', {
        method: isEdit ? 'PUT' : 'POST',
        body: JSON.stringify(payload)
      });
      closeModal();
      showToast(isEdit ? 'User updated.' : 'User created.');
      onSaved();
    } catch (err) {
      errBox.textContent = err.message;
      errBox.classList.remove('hidden');
    }
  });
}
