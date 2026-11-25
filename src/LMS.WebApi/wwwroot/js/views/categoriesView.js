import { apiRequest, escapeHtml, openModal, closeModal, showToast } from '../api.js';

export async function renderCategoriesView(container, topbarActions) {
  topbarActions.innerHTML = `<button id="add-cat-btn" class="btn btn-primary">+ Add Category</button>`;
  topbarActions.querySelector('#add-cat-btn').addEventListener('click', () => openCategoryModal(null, loadCategories));

  container.innerHTML = `<div class="card"><div id="categories-region">Loading categories...</div></div>`;

  async function loadCategories() {
    const region = container.querySelector('#categories-region');
    try {
      const categories = await apiRequest('/api/categories');
      if (!categories.length) {
        region.innerHTML = '<div class="empty-state"><h3>No categories yet</h3></div>';
        return;
      }

      region.innerHTML = `
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Books</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${categories.map((c) => `
                <tr>
                  <td><strong>${escapeHtml(c.name)}</strong></td>
                  <td>${escapeHtml(c.description || '—')}</td>
                  <td><span class="badge badge-info">${c.bookCount} books</span></td>
                  <td class="actions-cell">
                    <button class="btn btn-secondary btn-sm" data-edit-id="${c.id}">Edit</button>
                    <button class="btn btn-danger btn-sm" data-delete-id="${c.id}">Delete</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-edit-id]').forEach((btn) => {
        btn.addEventListener('click', () => {
          const cat = categories.find((x) => x.id === Number(btn.dataset.editId));
          openCategoryModal(cat, loadCategories);
        });
      });

      region.querySelectorAll('[data-delete-id]').forEach((btn) => {
        btn.addEventListener('click', async () => {
          const cat = categories.find((x) => x.id === Number(btn.dataset.deleteId));
          if (!window.confirm(`Delete category "${cat.name}"?`)) return;
          try {
            await apiRequest(`/api/categories/${cat.id}`, { method: 'DELETE' });
            showToast('Category deleted.');
            loadCategories();
          } catch (err) {
            showToast(err.message, 'error');
          }
        });
      });
    } catch (err) {
      region.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadCategories();
}

function openCategoryModal(category, onSaved) {
  const isEdit = Boolean(category);
  const modal = openModal({
    title: isEdit ? 'Edit Category' : 'Add Category',
    bodyHtml: `
      <div id="cat-modal-error" class="alert alert-error hidden"></div>
      <form id="cat-form">
        <div class="form-group">
          <label for="cf-name">Category Name *</label>
          <input id="cf-name" class="form-control" value="${escapeHtml(category?.name || '')}" required />
        </div>
        <div class="form-group">
          <label for="cf-desc">Description</label>
          <textarea id="cf-desc" rows="3" class="form-control">${escapeHtml(category?.description || '')}</textarea>
        </div>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px;">
          <button type="button" class="btn btn-secondary" id="cf-cancel">Cancel</button>
          <button type="submit" class="btn btn-primary">${isEdit ? 'Save Changes' : 'Create Category'}</button>
        </div>
      </form>
    `
  });

  modal.querySelector('#cf-cancel').addEventListener('click', closeModal);
  modal.querySelector('#cat-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errBox = modal.querySelector('#cat-modal-error');
    errBox.classList.add('hidden');
    try {
      await apiRequest(isEdit ? `/api/categories/${category.id}` : '/api/categories', {
        method: isEdit ? 'PUT' : 'POST',
        body: JSON.stringify({
          name: modal.querySelector('#cf-name').value.trim(),
          description: modal.querySelector('#cf-desc').value.trim()
        })
      });
      closeModal();
      showToast(isEdit ? 'Category updated.' : 'Category created.');
      onSaved();
    } catch (err) {
      errBox.textContent = err.message;
      errBox.classList.remove('hidden');
    }
  });
}
