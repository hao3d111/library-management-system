import { apiRequest, escapeHtml, openModal, closeModal, showToast } from '../api.js';

export async function renderBooksView(container, topbarActions, user, { canManage }) {
  let categories = [];
  try {
    categories = await apiRequest('/api/categories');
  } catch (err) {
    showToast(err.message, 'error');
  }

  if (canManage) {
    topbarActions.innerHTML = `<button id="add-book-btn" class="btn btn-primary">+ Add Book</button>`;
    topbarActions.querySelector('#add-book-btn').addEventListener('click', () => openBookModal(null, categories, loadBooks));
  }

  container.innerHTML = `
    <div class="card">
      <div class="toolbar">
        <form id="book-search-form" class="search-bar">
          <input id="book-q" class="form-control" type="search" placeholder="Search by title, author, or ISBN..." />
          <select id="book-category-filter" class="form-control" style="max-width: 210px;">
            <option value="">All Categories</option>
            ${categories.map((c) => `<option value="${c.id}">${escapeHtml(c.name)}</option>`).join('')}
          </select>
          <button type="submit" class="btn btn-primary">Search</button>
          <button type="button" id="book-reset-btn" class="btn btn-secondary">Reset</button>
        </form>
      </div>
      <div id="books-table-region">Loading books...</div>
    </div>
  `;

  const searchInput = container.querySelector('#book-q');
  const categorySelect = container.querySelector('#book-category-filter');

  container.querySelector('#book-search-form').addEventListener('submit', (e) => {
    e.preventDefault();
    loadBooks();
  });

  container.querySelector('#book-reset-btn').addEventListener('click', () => {
    searchInput.value = '';
    categorySelect.value = '';
    loadBooks();
  });

  async function loadBooks() {
    const region = container.querySelector('#books-table-region');
    const params = new URLSearchParams();
    if (searchInput.value.trim()) params.set('q', searchInput.value.trim());
    if (categorySelect.value) params.set('categoryId', categorySelect.value);

    try {
      const books = await apiRequest(`/api/books${params.toString() ? `?${params}` : ''}`);
      if (!books.length) {
        region.innerHTML = `
          <div class="empty-state">
            <h3>No books found</h3>
            <p>No titles matched your search criteria. Try adjusting your keywords or category filter.</p>
          </div>
        `;
        return;
      }

      region.innerHTML = `
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Title & Author</th>
                <th>ISBN</th>
                <th>Category</th>
                <th>Copies</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${books.map((b) => `
                <tr>
                  <td>
                    <strong>${escapeHtml(b.title)}</strong>
                    <div style="font-size:12.5px;color:var(--ink-muted);">${escapeHtml(b.author)}</div>
                  </td>
                  <td><code>${escapeHtml(b.isbn)}</code></td>
                  <td>${escapeHtml(b.categoryName)}</td>
                  <td><strong>${b.availableQuantity}</strong> / ${b.totalQuantity}</td>
                  <td>
                    ${b.availableQuantity > 0
                      ? '<span class="badge badge-success">Available</span>'
                      : '<span class="badge badge-danger">Out of Stock</span>'}
                  </td>
                  <td class="actions-cell">
                    <button class="btn btn-secondary btn-sm" data-view-id="${b.id}">Details</button>
                    ${canManage ? `
                      <button class="btn btn-secondary btn-sm" data-edit-id="${b.id}">Edit</button>
                      <button class="btn btn-danger btn-sm" data-delete-id="${b.id}">Delete</button>
                    ` : ''}
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-view-id]').forEach((btn) => {
        btn.addEventListener('click', async () => {
          const detail = await apiRequest(`/api/books/${btn.dataset.viewId}`);
          openModal({
            title: detail.title,
            bodyHtml: `
              <p><strong>Author:</strong> ${escapeHtml(detail.author)}</p>
              <p style="margin-top:8px;"><strong>ISBN:</strong> <code>${escapeHtml(detail.isbn)}</code></p>
              <p style="margin-top:8px;"><strong>Category:</strong> ${escapeHtml(detail.categoryName)}</p>
              <p style="margin-top:8px;"><strong>Availability:</strong> ${detail.availableQuantity} of ${detail.totalQuantity} copies available</p>
              <p style="margin-top:12px;color:var(--ink-muted);">${escapeHtml(detail.description || 'No description provided.')}</p>
            `
          });
        });
      });

      if (canManage) {
        region.querySelectorAll('[data-edit-id]').forEach((btn) => {
          btn.addEventListener('click', () => {
            const book = books.find((x) => x.id === Number(btn.dataset.editId));
            openBookModal(book, categories, loadBooks);
          });
        });

        region.querySelectorAll('[data-delete-id]').forEach((btn) => {
          btn.addEventListener('click', async () => {
            const book = books.find((x) => x.id === Number(btn.dataset.deleteId));
            if (!window.confirm(`Delete "${book.title}"?`)) return;
            try {
              await apiRequest(`/api/books/${book.id}`, { method: 'DELETE' });
              showToast('Book deleted successfully.');
              loadBooks();
            } catch (err) {
              showToast(err.message, 'error');
            }
          });
        });
      }
    } catch (err) {
      region.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadBooks();
}

function openBookModal(book, categories, onSaved) {
  const isEdit = Boolean(book);
  const modal = openModal({
    title: isEdit ? 'Edit Book' : 'Add New Book',
    bodyHtml: `
      <div id="book-modal-error" class="alert alert-error hidden"></div>
      <form id="book-form">
        <div class="form-group">
          <label for="bf-title">Title *</label>
          <input id="bf-title" class="form-control" value="${escapeHtml(book?.title || '')}" required />
        </div>
        <div class="form-group">
          <label for="bf-author">Author *</label>
          <input id="bf-author" class="form-control" value="${escapeHtml(book?.author || '')}" required />
        </div>
        <div class="form-row">
          <div class="form-group">
            <label for="bf-isbn">ISBN (ISBN-10 or ISBN-13) *</label>
            <input id="bf-isbn" class="form-control" value="${escapeHtml(book?.isbn || '')}" placeholder="978-0132350884" required />
          </div>
          <div class="form-group">
            <label for="bf-qty">Total Quantity *</label>
            <input id="bf-qty" type="number" min="0" class="form-control" value="${book?.totalQuantity ?? 1}" required />
          </div>
        </div>
        <div class="form-group">
          <label for="bf-cat">Category *</label>
          <select id="bf-cat" class="form-control" required>
            <option value="">Select a category...</option>
            ${categories.map((c) => `<option value="${c.id}" ${book?.categoryId === c.id ? 'selected' : ''}>${escapeHtml(c.name)}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label for="bf-desc">Description</label>
          <textarea id="bf-desc" rows="3" class="form-control">${escapeHtml(book?.description || '')}</textarea>
        </div>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:16px;">
          <button type="button" class="btn btn-secondary" id="bf-cancel">Cancel</button>
          <button type="submit" class="btn btn-primary">${isEdit ? 'Save Changes' : 'Create Book'}</button>
        </div>
      </form>
    `
  });

  modal.querySelector('#bf-cancel').addEventListener('click', closeModal);
  modal.querySelector('#book-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errBox = modal.querySelector('#book-modal-error');
    errBox.classList.add('hidden');

    const payload = {
      title: modal.querySelector('#bf-title').value.trim(),
      author: modal.querySelector('#bf-author').value.trim(),
      isbn: modal.querySelector('#bf-isbn').value.trim(),
      categoryId: Number(modal.querySelector('#bf-cat').value),
      totalQuantity: Number(modal.querySelector('#bf-qty').value),
      description: modal.querySelector('#bf-desc').value.trim()
    };

    try {
      await apiRequest(isEdit ? `/api/books/${book.id}` : '/api/books', {
        method: isEdit ? 'PUT' : 'POST',
        body: JSON.stringify(payload)
      });
      closeModal();
      showToast(isEdit ? 'Book updated.' : 'Book added to catalog.');
      onSaved();
    } catch (err) {
      const details = err.errors ? Object.values(err.errors).flat().join(' ') : '';
      errBox.textContent = details ? `${err.message} ${details}` : err.message;
      errBox.classList.remove('hidden');
    }
  });
}
