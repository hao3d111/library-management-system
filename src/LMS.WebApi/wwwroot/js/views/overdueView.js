import { apiRequest, escapeHtml, formatDate, showToast } from '../api.js';

export async function renderOverdueView(container, topbarActions) {
  topbarActions.innerHTML = `<button id="sync-overdue-btn" class="btn btn-secondary">Scan Past-Due Loans</button>`;
  topbarActions.querySelector('#sync-overdue-btn').addEventListener('click', async () => {
    try {
      const res = await apiRequest('/api/borrowings/sync-overdue', { method: 'POST' });
      showToast(`Scan complete. ${res.newlyFlagged} additional transaction(s) flagged as Overdue.`);
      loadOverdue();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });

  container.innerHTML = `
    <div id="overdue-summary" class="stats-grid"></div>
    <div class="card">
      <div class="toolbar">
        <form id="overdue-filter-form" class="search-bar">
          <input id="overdue-search" class="form-control" type="search" placeholder="Filter by member, code, book title, or ISBN..." />
          <input id="overdue-min-days" class="form-control" type="number" min="0" style="max-width:170px;" placeholder="Min days overdue" />
          <button type="submit" class="btn btn-primary">Filter</button>
          <button type="button" id="overdue-reset" class="btn btn-secondary">Reset</button>
        </form>
      </div>
      <div id="overdue-region">Loading overdue records...</div>
    </div>
  `;

  const searchInput = container.querySelector('#overdue-search');
  const minDaysInput = container.querySelector('#overdue-min-days');

  container.querySelector('#overdue-filter-form').addEventListener('submit', (e) => {
    e.preventDefault();
    loadOverdue();
  });

  container.querySelector('#overdue-reset').addEventListener('click', () => {
    searchInput.value = '';
    minDaysInput.value = '';
    loadOverdue();
  });

  async function loadOverdue() {
    const region = container.querySelector('#overdue-region');
    const summary = container.querySelector('#overdue-summary');
    const params = new URLSearchParams();
    if (searchInput.value.trim()) params.set('search', searchInput.value.trim());
    if (minDaysInput.value) params.set('minDaysOverdue', minDaysInput.value);

    try {
      const records = await apiRequest(`/api/borrowings/overdue${params.toString() ? `?${params}` : ''}`);
      const totalEstimatedFines = records.reduce((acc, r) => acc + Number(r.estimatedFine || 0), 0);

      summary.innerHTML = `
        <div class="stat-card">
          <div class="stat-label">Overdue Loans</div>
          <div class="stat-value ${records.length > 0 ? 'danger' : ''}">${records.length}</div>
        </div>
        <div class="stat-card">
          <div class="stat-label">Accrued Pending Fines</div>
          <div class="stat-value danger">$${totalEstimatedFines.toFixed(2)}</div>
        </div>
      `;

      if (!records.length) {
        region.innerHTML = `
          <div class="empty-state">
            <h3>No overdue books found</h3>
            <p>All matching loans are currently within their due dates.</p>
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
                <th>Member</th>
                <th>Book</th>
                <th>Due Date</th>
                <th>Days Overdue</th>
                <th>Accrued Fine</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              ${records.map((r) => `
                <tr>
                  <td>#${r.id}</td>
                  <td>
                    <strong>${escapeHtml(r.memberName)}</strong>
                    <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(r.memberCode)} • ${escapeHtml(r.memberEmail)}</div>
                  </td>
                  <td>
                    <strong>${escapeHtml(r.bookTitle)}</strong>
                    <div style="font-size:12px;color:var(--ink-muted);">${escapeHtml(r.bookISBN)}</div>
                  </td>
                  <td>${formatDate(r.dueDate)}</td>
                  <td><span class="badge badge-danger">${r.daysOverdue} day(s)</span></td>
                  <td><strong>$${Number(r.estimatedFine).toFixed(2)}</strong></td>
                  <td>
                    <button class="btn btn-primary btn-sm" data-return-id="${r.id}">Return & Assess Fine</button>
                  </td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `;

      region.querySelectorAll('[data-return-id]').forEach((btn) => {
        btn.addEventListener('click', async () => {
          btn.disabled = true;
          try {
            const res = await apiRequest(`/api/borrowings/${btn.dataset.returnId}/return`, { method: 'PUT' });
            showToast(`Returned "${res.bookTitle}". Final fine assessed: $${Number(res.fineAmount).toFixed(2)}.`);
            loadOverdue();
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

  await loadOverdue();
}
