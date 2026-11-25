import { apiRequest, escapeHtml, formatDate, openModal, closeModal, showToast } from '../api.js';

export async function renderBorrowingsView(container, topbarActions) {
  topbarActions.innerHTML = `<button id="new-borrow-btn" class="btn btn-primary">+ Borrow Book</button>`;
  topbarActions.querySelector('#new-borrow-btn').addEventListener('click', () => openBorrowModal(loadBorrowings));

  container.innerHTML = `
    <div class="card">
      <div class="toolbar">
        <div class="search-bar">
          <select id="borrow-status-filter" class="form-control" style="max-width:200px;">
            <option value="">All Statuses</option>
            <option value="Borrowed">Borrowed (Active)</option>
            <option value="Overdue">Overdue</option>
            <option value="Returned">Returned</option>
          </select>
          <button id="borrow-refresh-btn" class="btn btn-secondary">Refresh</button>
        </div>
      </div>
      <div id="borrowings-region">Loading borrowing transactions...</div>
    </div>
  `;

  const statusSelect = container.querySelector('#borrow-status-filter');
  statusSelect.addEventListener('change', loadBorrowings);
  container.querySelector('#borrow-refresh-btn').addEventListener('click', loadBorrowings);

  async function loadBorrowings() {
    const region = container.querySelector('#borrowings-region');
    const status = statusSelect.value;
    try {
      const items = await apiRequest(`/api/borrowings${status ? `?status=${encodeURIComponent(status)}` : ''}`);
      if (!items.length) {
        region.innerHTML = `
          <div class="empty-state">
            <h3>No borrowing transactions found</h3>
            <p>Click "+ Borrow Book" to issue a book to a library member.</p>
          </div>
        `;
        return;
      }

      region.innerHTML = `
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Book</th>
                <th>Member</th>
                <th>Borrow Date</th>
                <th>Due Date</th>
                <th>Return Date</th>
                <th>Status</th>
                <th>Fine</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${items.map((t) => `
                <tr>
                  <td>#${t.id}</td>
                  <td>
                    <strong>${escapeHtml(t.bookTitle)}</strong>
                    <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(t.bookISBN)}</div>
                  </td>
                  <td>
                    <strong>${escapeHtml(t.memberName)}</strong>
                    <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(t.memberCode)}</div>
                  </td>
                  <td>${formatDate(t.borrowDate)}</td>
                  <td>${formatDate(t.dueDate)}</td>
                  <td>${formatDate(t.returnDate)}</td>
                  <td>${renderStatusBadge(t.status)}</td>
                  <td>$${Number(t.status === 'Returned' ? t.fineAmount : t.estimatedFine).toFixed(2)}</td>
                  <td>
                    ${t.status !== 'Returned'
                      ? `<button class="btn btn-primary btn-sm" data-return-id="${t.id}">Process Return</button>`
                      : '<span style="color:var(--ink-muted);font-size:12.5px;">Completed</span>'}
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-return-id]').forEach((btn) => {
        btn.addEventListener('click', async () => {
          const id = Number(btn.dataset.returnId);
          btn.disabled = true;
          try {
            const res = await apiRequest(`/api/borrowings/${id}/return`, { method: 'PUT' });
            const fineMsg = res.fineAmount > 0
              ? ` Overdue fine applied: $${Number(res.fineAmount).toFixed(2)}.`
              : ' Returned on time ($0.00 fine).';
            showToast(`"${res.bookTitle}" returned successfully.${fineMsg}`);
            loadBorrowings();
          } catch (err) {
            showToast(err.message, 'error');
            btn.disabled = false;
          }
        });
      });
    } catch (err) {
      region.innerHTML = `<div class="alert alert-error">${escapeHtml(err.message)}</div>`;
    }
  }

  await loadBorrowings();
}

async function openBorrowModal(onBorrowed) {
  let members = [];
  let books = [];
  try {
    [members, books] = await Promise.all([
      apiRequest('/api/members'),
      apiRequest('/api/books')
    ]);
  } catch (err) {
    showToast(err.message, 'error');
    return;
  }

  const activeMembers = members.filter((m) => m.isActive);

  const modal = openModal({
    title: 'Issue Book (Borrow Transaction)',
    bodyHtml: `
      <div id="borrow-modal-error" class="alert alert-error hidden"></div>
      <form id="borrow-form">
        <div class="form-group">
          <label for="br-member">Library Member *</label>
          <select id="br-member" class="form-control" required>
            <option value="">Select member...</option>
            ${activeMembers.map((m) => `
              <option value="${m.id}">${escapeHtml(m.fullName)} (${escapeHtml(m.membershipCode)}) — ${m.activeBorrowingsCount} active</option>
            `).join('')}
          </select>
        </div>
        <div class="form-group">
          <label for="br-book">Book *</label>
          <select id="br-book" class="form-control" required>
            <option value="">Select book...</option>
            ${books.map((b) => `
              <option value="${b.id}" ${b.availableQuantity <= 0 ? 'disabled' : ''}>
                ${escapeHtml(b.title)} by ${escapeHtml(b.author)} (${b.availableQuantity}/${b.totalQuantity} available)
              </option>
            `).join('')}
          </select>
        </div>
        <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:18px;">
          <button type="button" class="btn btn-secondary" id="br-cancel">Cancel</button>
          <button type="submit" class="btn btn-primary">Confirm Borrow</button>
        </div>
      </form>
    `
  });

  modal.querySelector('#br-cancel').addEventListener('click', closeModal);
  modal.querySelector('#borrow-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errBox = modal.querySelector('#borrow-modal-error');
    errBox.classList.add('hidden');

    const payload = {
      memberId: Number(modal.querySelector('#br-member').value),
      bookId: Number(modal.querySelector('#br-book').value)
    };

    try {
      const created = await apiRequest('/api/borrowings', {
        method: 'POST',
        body: JSON.stringify(payload)
      });
      closeModal();
      showToast(`Issued "${created.bookTitle}" to ${created.memberName}. Due ${formatDate(created.dueDate)}.`);
      onBorrowed();
    } catch (err) {
      errBox.textContent = err.message;
      errBox.classList.remove('hidden');
    }
  });
}

function renderStatusBadge(status) {
  if (status === 'Returned') return '<span class="badge badge-success">Returned</span>';
  if (status === 'Overdue') return '<span class="badge badge-danger">Overdue</span>';
  return '<span class="badge badge-info">Borrowed</span>';
}
